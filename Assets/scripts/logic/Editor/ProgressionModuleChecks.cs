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
            using var authored = AuthoredSkillCatalog.Open(Obj.FindFirstObjectByType<LinearRpgController>().skills);
            var known = authored.Catalog.skills.Select(skill => skill.skillId).ToArray();
            teaching.Validate(known); items.Validate(known);
            var shieldLesson = teaching.entries.Single(entry => entry.entryId == "shield");
            Check(shieldLesson.displayName == "衡印" && shieldLesson.skillIds.SequenceEqual(new[] { "shield.left", "buff.voice" }), "teaching groups alternative recipes without granting progress");
            var model = new PlayerModel(new RpgPlayerState());
            RpgProgressData Snapshot() => model.Export(model.Vitals.CaptureCheckpoint(0), null);
            bool Accepts(RpgProgressData saved)
            {
                var restored = new PlayerModel(new RpgPlayerState());
                restored.RestoreGrowth(saved);
                return inventory.ValidateProgress(restored);
            }
            Check(model.Discover(new[] { "fireball" }) && model.IsDiscovered("fireball") && !model.IsUnlocked("fireball"), "discovery is distinct from permanent unlock");
            Check(model.Discover(new[] { "fireball" }) && model.DiscoveredSkills.Count == 1, "repeated discovery is idempotent");
            Check(model.Unlock(new[] { "buff.zi" }) && model.IsDiscovered("buff.zi"), "ordinary skill unlock works without teaching or inventory");
            var missingItem = new RpgProgressData(); missingItem.unlockedSkillIds.Add("buff.voice");
            Check(!Accepts(missingItem), "exclusive item skill cannot bypass item ownership");
            var malformed = Snapshot(); malformed.items.Add(new RpgItemProgressData { itemId = "leaf.fangyuan" });
            Check(!Accepts(malformed), "collected item requires its discovery reward");
            var leaf = items.Find("leaf.fangyuan");
            model.ChangeItem(leaf.itemId, leaf.discoverSkillIds, leaf.unlockSkillIds, false);
            Check(Accepts(Snapshot()) && !model.IsUnlocked("buff.voice"), "collection reveals knowledge without permanent grant");
            malformed = Snapshot(); malformed.unlockedSkillIds.Add("buff.voice");
            Check(!Accepts(malformed), "unactivated item cannot grant exclusive skill");
            model.ChangeItem(leaf.itemId, leaf.discoverSkillIds, leaf.unlockSkillIds, true);
            Check(Accepts(Snapshot()), "activated item and grant validate in both directions");
            var stageOnly = new RpgProgressData(); stageOnly.completedStageIds.Add("GATE_CHECK");
            Check(Accepts(stageOnly) && stageOnly.items.Count == 0 && stageOnly.unlockedSkillIds.Count == 0, "stage history does not fabricate knowledge or items");
            bool Buy() => model.Purchase("fireball", SkillOfferKind.Unlock, 30, 1, null, out _);
            bool Upgrade() => model.Purchase("fireball", SkillOfferKind.Upgrade, 10, 1, null, out _);
            model.Credit(20); string before = JsonUtility.ToJson(Snapshot());
            Check(!Buy() && JsonUtility.ToJson(Snapshot()) == before, "insufficient balance leaves progress unchanged");
            model.Credit(30);
            Check(Buy() && model.Currency == 20 && !Buy() && model.Currency == 20, "configured purchase charges once");
            Check(Upgrade() && !Upgrade() && model.Currency == 10 && model.SkillLevel("fireball") == 1, "upgrade has a configured cap and no repeat charge");
            EffectIsolation();
            Disk(Snapshot(), known);
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
                var model = new PlayerModel(new RpgPlayerState());
                model.Unlock(new[] { "test.a" });
                model.Purchase("test.a", SkillOfferKind.Upgrade, 0, 3, null, out _);
                model.Purchase("test.a", SkillOfferKind.Upgrade, 0, 3, null, out _);
                playerOne.ApplyLevels(model.SkillLevel); playerTwo.ApplyLevels(id => 0);
                float Damage(RuntimeSkillCatalog c, int index) => ((ProjectileEffectDefinition)c.Catalog.skills[index].effectDefinition).damage;
                Check(Mathf.Approximately(Damage(playerOne, 0), 30) && Mathf.Approximately(Damage(playerOne, 1), 25), "upgrade affects only its own skill even with shared effect source");
                Check(Mathf.Approximately(Damage(playerTwo, 0), 25) && Mathf.Approximately(effect.damage, 25), "player instances and project assets remain isolated");
                playerOne.ApplyLevels(model.SkillLevel); Check(Mathf.Approximately(Damage(playerOne, 0), 30), "reapplying levels does not multiply repeatedly");
                playerOne.ApplyLevels(id => 0); Check(Mathf.Approximately(Damage(playerOne, 0), 25), "loading lower level restores original effect value");
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
                var store = new RpgSaveStore(UnityRpgSaveJson.Instance, path, "modules", known);
                Check(store.TrySave(p, out _) && store.TryLoad(out var loaded, out _) && loaded.items[0].activated && loaded.discoveredSkillIds.Contains("buff.voice"), "version 2 roundtrip keeps item and knowledge state");
                var legacy = UnityRpgSaveJson.Instance.Deserialize<RpgProgressData>(UnityRpgSaveJson.Instance.Serialize(p));
                legacy.discoveredSkillIds.Add("buff.hidden");
                legacy.unlockedSkillIds.Add("buff.hidden");
                legacy.checkpoint.cooldowns.Add(new RpgCooldownData { group = "shield.hidden", remainingSeconds = 2 });
                legacy.checkpoint.cooldowns.Add(new RpgCooldownData { group = "shield", remainingSeconds = 1 });
                var wardStore = new RpgSaveStore(UnityRpgSaveJson.Instance, path, "modules", known, knownCooldownGroups: new[] { "shield" });
                File.WriteAllText(path, JsonUtility.ToJson(new SaveEnvelope { profileId = "modules", progress = legacy }));
                Check(wardStore.TryLoad(out var migrated, out _) && migrated.unlockedSkillIds.FindAll(id => id == "buff.voice").Count == 1 &&
                    !migrated.unlockedSkillIds.Contains("buff.hidden") && migrated.checkpoint.cooldowns.Count == 1 &&
                    migrated.checkpoint.cooldowns[0].group == "shield" && migrated.checkpoint.cooldowns[0].remainingSeconds == 2 &&
                    migrated.items[0].activated && migrated.currency == p.currency && store.TrySave(p, out _),
                    "old ward progress loads as one voice ward and preserves the longer cooldown");
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
