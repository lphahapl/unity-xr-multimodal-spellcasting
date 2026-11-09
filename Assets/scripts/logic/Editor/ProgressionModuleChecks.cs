using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using Obj = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>验证教学、知识、物品、交易、效果等级和磁盘结构各自独立，检查的是公开业务结果。</summary>
    public static class ProgressionModuleChecks
    {
        const string Report = "Logs/progression-module-check.txt";
        /// <summary>运行编辑模式模块检查；失败抛出，交给批处理入口记录退出状态。</summary>
        public static void Run()
        {
            Directory.CreateDirectory("Logs"); File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + "\n");
            var teaching = AssetDatabase.LoadAssetAtPath<TeachingCatalog>("Assets/ScriptableObjects/教学目录/首章术式教学.asset");
            var items = AssetDatabase.LoadAssetAtPath<ProgressionItemCatalog>("Assets/ScriptableObjects/成长物品配置/首章成长物品.asset");
            var inventory = Obj.FindFirstObjectByType<RpgItemInventory>();
            var known = new[] { "fireball", "buff.zi", "buff.voice", "buff.hidden" };
            teaching.Validate(known); items.Validate(known);
            Check(teaching.entries.Length == 3 && teaching.entries[1].skillIds.Length == 2, "teaching groups alternative recipes without granting progress");
            var p = new RpgProgressData();
            Check(SkillProgression.Discover(p, new[] { "fireball" }) && SkillProgression.IsDiscovered(p, "fireball") && !SkillProgression.IsUnlocked(p, "fireball"), "discovery is distinct from permanent unlock");
            Check(SkillProgression.Discover(p, new[] { "fireball" }) && p.discoveredSkillIds.Count == 1, "repeated discovery is idempotent");
            Check(SkillProgression.Unlock(p, new[] { "buff.zi" }) && SkillProgression.IsDiscovered(p, "buff.zi"), "ordinary skill unlock works without teaching or inventory");
            var missingItem = new RpgProgressData(); missingItem.unlockedSkillIds.Add("buff.hidden");
            Check(!inventory.ValidateProgress(missingItem), "exclusive item skill cannot bypass item ownership");
            p.items.Add(new RpgItemProgressData { itemId = "leaf.fangyuan" });
            Check(!inventory.ValidateProgress(p), "collected item requires its discovery reward");
            SkillProgression.Discover(p, new[] { "buff.hidden" });
            Check(inventory.ValidateProgress(p) && !SkillProgression.IsUnlocked(p, "buff.hidden"), "collection reveals knowledge without permanent grant");
            SkillProgression.Unlock(p, new[] { "buff.hidden" });
            Check(!inventory.ValidateProgress(p), "unactivated item cannot grant exclusive skill");
            p.items[0].activated = true;
            Check(inventory.ValidateProgress(p), "activated item and grant validate in both directions");
            var stageOnly = new RpgProgressData(); stageOnly.completedStageIds.Add("GATE_CHECK");
            Check(inventory.ValidateProgress(stageOnly) && stageOnly.items.Count == 0 && stageOnly.unlockedSkillIds.Count == 0, "stage history does not fabricate knowledge or items");
            var offer = new SkillShopOffer { skillId = "fireball", kind = SkillOfferKind.Unlock, cost = 30 };
            p.currency = 20; string before = JsonUtility.ToJson(p);
            Check(!SkillShopTransactions.Apply(p, offer, out _) && JsonUtility.ToJson(p) == before, "insufficient balance leaves progress unchanged");
            p.currency = 50;
            Check(SkillShopTransactions.Apply(p, offer, out _) && p.currency == 20 && !SkillShopTransactions.Apply(p, offer, out _) && p.currency == 20, "configured purchase charges once");
            offer.kind = SkillOfferKind.Upgrade; offer.cost = 10; offer.maximumLevel = 1;
            Check(SkillShopTransactions.Apply(p, offer, out _) && !SkillShopTransactions.Apply(p, offer, out _) && p.currency == 10 && SkillProgression.Level(p, "fireball") == 1, "upgrade has a configured cap and no repeat charge");
            EffectIsolation();
            Disk(p, known);
            File.AppendAllText(Report, "PASS END\n");
        }

        /// <summary>共享同一效果原件的两个技能，也必须按各自技能 ID 应用等级。</summary>
        static void EffectIsolation()
        {
            var effect = ScriptableObject.CreateInstance<ProjectileEffectDefinition>(); effect.damage = 25;
            var a = ScriptableObject.CreateInstance<SkillDefinition>(); a.skillId = "test.a"; a.effectDefinition = effect;
            var b = ScriptableObject.CreateInstance<SkillDefinition>(); b.skillId = "test.b"; b.effectDefinition = effect;
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>(); catalog.skills = new() { a, b };
            try
            {
                using var playerOne = new RuntimeSkillCatalog(catalog);
                using var playerTwo = new RuntimeSkillCatalog(catalog);
                var p = new RpgProgressData(); SkillProgression.Unlock(p, new[] { "test.a" });
                SkillProgression.Upgrade(p, "test.a", 3); SkillProgression.Upgrade(p, "test.a", 3);
                playerOne.ApplyLevels(p); playerTwo.ApplyLevels(new RpgProgressData());
                float Damage(RuntimeSkillCatalog c, int index) => ((ProjectileEffectDefinition)c.Catalog.skills[index].effectDefinition).damage;
                Check(Mathf.Approximately(Damage(playerOne, 0), 30) && Mathf.Approximately(Damage(playerOne, 1), 25), "upgrade affects only its own skill even with shared effect source");
                Check(Mathf.Approximately(Damage(playerTwo, 0), 25) && Mathf.Approximately(effect.damage, 25), "player instances and project assets remain isolated");
                playerOne.ApplyLevels(p); Check(Mathf.Approximately(Damage(playerOne, 0), 30), "reapplying levels does not multiply repeatedly");
                playerOne.ApplyLevels(new RpgProgressData()); Check(Mathf.Approximately(Damage(playerOne, 0), 25), "loading lower level restores original effect value");
            }
            finally { Obj.DestroyImmediate(catalog); Obj.DestroyImmediate(a); Obj.DestroyImmediate(b); Obj.DestroyImmediate(effect); }
        }

        /// <summary>验证新结构往返、写盘失败保护、重复物品拒绝及旧版本拒绝。</summary>
        static void Disk(RpgProgressData p, string[] known)
        {
            string directory = Path.Combine(Path.GetTempPath(), "progression-modules-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, "player.json");
            try
            {
                var store = new RpgSaveStore(path, "modules", known);
                Check(store.TrySave(p, out _) && store.TryLoad(out var loaded, out _) && loaded.items[0].activated && loaded.discoveredSkillIds.Contains("buff.hidden"), "version 2 roundtrip keeps item and knowledge state");
                using (new FileStream(path + ".tmp", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    Check(!store.TrySave(p, out _) && store.TryLoad(out _, out _), "disk write failure preserves the previous save");
                p.items.Add(new RpgItemProgressData { itemId = p.items[0].itemId });
                Check(!store.Validate(p, out _), "duplicate generic item records rejected"); p.items.RemoveAt(p.items.Count - 1);
                File.WriteAllText(path, JsonUtility.ToJson(new SaveEnvelope { version = 1, profileId = "modules", progress = p }));
                Check(!store.TryLoad(out _, out _), "old format is rejected without an implicit migration");
            }
            finally { foreach (var suffix in new[] { "", ".tmp", ".bak" }) File.Delete(path + suffix); Directory.Delete(directory); }
        }
        /// <summary>只记录已经实际执行且通过的断言。</summary>
        static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); File.AppendAllText(Report, "PASS " + name + "\n"); }
    }
}
