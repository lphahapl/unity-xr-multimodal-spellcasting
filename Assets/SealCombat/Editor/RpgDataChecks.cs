#if UNITY_EDITOR
using System;
using System.IO;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Deterministic data checks, using only a uniquely owned temporary directory. No scene or player save is touched.</summary>
    public static class RpgDataChecks
    {
        [MenuItem("NarutoVR/线性RPG/运行玩家与存档检查")]
        public static void RunMenu() => Debug.Log("玩家与存档检查通过：" + Run());

        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出播放模式后运行数据检查。");
            int passed = 0;
            Check("首Tick与同时间重复Tick不额外恢复", () =>
            {
                var p = new RpgPlayerState(100, 100, 5, 0);
                p.Tick(500); True(Cast(p, "same-frame", 20, 500, () => true), "同时间首次施法");
                p.Tick(500); p.Tick(500); Equal(80, p.Mana);
                p.Tick(501); Equal(85, p.Mana);
            }); passed++;
            Check("能量19不能支付20且拒绝消费ID", () =>
            {
                var p = Player(); p.Respawn(100, 19, 0);
                int effects = 0;
                True(!Cast(p, "poor", 20, 0, () => { effects++; return true; }), "应拒绝能量不足");
                p.RestoreMana(100);
                True(!Cast(p, "poor", 20, 0, () => { effects++; return true; }), "拒绝ID不得重放");
                Equal(0, effects); Equal(100, p.Mana);
            }); passed++;
            Check("成功精确扣费并开始CD", () =>
            {
                var p = Player(); True(Cast(p, "one", 20, 0, () => true), "首次成功");
                Equal(80, p.Mana); Equal(3, p.GetCooldown("fireball", 0));
                True(!Cast(p, "two", 20, 1, () => true), "冷却中拒绝"); Equal(80, p.Mana);
            }); passed++;
            Check("效果false和异常退款", () =>
            {
                var p = Player(); long revision = p.Revision;
                True(!Cast(p, "false", 20, 0, () => false), "false必须失败");
                True(!Cast(p, "throw", 20, 0, () => throw new InvalidOperationException("test")), "异常必须失败");
                True(!Cast(p, "mutation", 20, 0, () => { p.Heal(1); return true; }), "回调写玩家数值应失败退款");
                Equal(100, p.Mana); Equal(0, p.GetCooldown("fireball", 0));
                True(p.Revision == revision, "失败不得提交数值版本");
            }); passed++;
            Check("重入与回调写入隔离", () =>
            {
                var p = Player(); int innerEffects = 0;
                True(Cast(p, "outer", 20, 0, () =>
                {
                    True(!Cast(p, "inner", 20, 0, () => { innerEffects++; return true; }), "重入应被拒绝");
                    True(!p.ApplyDamage("inner-damage", 20, 0, out _), "回调不能造成玩家数值写入");
                    True(!p.RestoreCheckpoint(new RpgCheckpointData(), 0, out _), "回调不能读档");
                    bool threw = false; try { p.RestoreMana(1); } catch (InvalidOperationException) { threw = true; }
                    True(threw, "普通写方法必须明确拒绝");
                    return true;
                }), "外层成功");
                Equal(0, innerEffects); Equal(80, p.Mana); Equal(100, p.Health);
                True(!Cast(p, "inner", 0, 3, () => true), "重入拒绝ID也必须消费");
            }); passed++;
            Check("重复ID最多执行一次", () =>
            {
                var p = Player(); int effects = 0;
                True(Cast(p, "same", 20, 0, () => { effects++; return true; }), "首次成功");
                True(!Cast(p, "same", 20, 4, () => { effects++; return true; }), "重复拒绝"); Equal(1, effects);
            }); passed++;
            Check("冷却不依赖装备与死亡重生保留", () =>
            {
                var p = Player(); True(Cast(p, "start", 20, 0, () => true), "成功");
                // Equipment is intentionally outside this model: swapping effect callbacks cannot erase the group cooldown.
                True(!Cast(p, "re-equip", 0, 1, () => true), "更换效果仍查同一冷却组");
                True(p.ApplyDamage("kill", 100, 1, out _), "击杀");
                True(!Cast(p, "dead", 0, 1, () => true), "死亡拒绝技能"); Equal(0, p.Heal(100));
                p.Respawn(100, 100, 1); Equal(2, p.GetCooldown("fireball", 1));
                True(Cast(p, "new-life", 0, 3, () => true), "冷却边界成功");
            }); passed++;
            Check("NaN负数倒退时间拒绝", () =>
            {
                var p = Player();
                True(!Cast(p, "nan-cost", float.NaN, 0, () => true), "NaN成本拒绝");
                True(!p.TryActivate("nan-time", "fireball", null, 0, 3, double.NaN, () => true, out _), "NaN时间拒绝");
                True(!p.TryActivate("nan-cooldown", "fireball", null, 0, float.NaN, 0, () => true, out _), "NaN冷却拒绝");
                True(!p.ApplyDamage("nan-damage", float.NaN, 0, out _), "NaN伤害拒绝");
                True(!p.ApplyDamage("negative", -1, 0, out _), "负伤害拒绝");
                p.Tick(2); True(!Cast(p, "past", 0, 1, () => true), "倒退时间拒绝");
                Equal(100, p.Health); Equal(100, p.Mana);
            }); passed++;
            Check("重复伤害仅结算一次", () =>
            {
                var p = Player(); True(p.ApplyDamage("hit", 25, 0, out float applied), "首次伤害"); Equal(25, applied);
                True(!p.ApplyDamage("hit", 25, 0, out applied), "重复伤害拒绝"); Equal(0, applied); Equal(75, p.Health);
            }); passed++;
            Check("共享CD组跨法术阻止激活", () =>
            {
                var p = Player();
                True(p.TryActivate("shared-first", "buff.zi", "guard", 10, 3, 0, () => true, out _), "共享组首次激活");
                True(!p.TryActivate("shared-second", "buff.voice", "guard", 10, 3, 1, () => true, out _), "另一个技能受共享组冷却限制");
                Equal(90, p.Mana); Equal(2, p.GetCooldown("guard", 1));
                True(p.TryActivate("shared-third", "buff.voice", "guard", 10, 3, 3, () => true, out _), "共享组边界可激活"); Equal(80, p.Mana);
            }); passed++;
            Check("零生命检查点保持死亡与校验失败原子性", () =>
            {
                var p = new RpgPlayerState();
                var dead = new RpgCheckpointData { health = 0, mana = 20 };
                dead.cooldowns.Add(new RpgCooldownData { group = "fireball", remainingSeconds = 3 });
                True(p.RestoreCheckpoint(dead, 10, out _), "合法零生命检查点可恢复");
                True(!p.Alive, "恢复零生命不能隐式复活");
                True(!Cast(p, "dead-checkpoint", 0, 10, () => true), "零生命不能施法");
                p.Tick(12); Equal(20, p.Mana); Equal(0, p.Heal(100));
                var invalid = new RpgCheckpointData { health = 101, mana = 100 };
                True(!p.RestoreCheckpoint(invalid, 12, out _), "超上限检查点拒绝");
                Equal(0, p.Health); Equal(20, p.Mana); Equal(1, p.GetCooldown("fireball", 12));
                p.Respawn(12); True(p.Alive, "显式重生允许复活"); Equal(1, p.GetCooldown("fireball", 12));
            }); passed++;
            Check("恢复延迟与检查点剩余时间", () =>
            {
                var p = new RpgPlayerState(); True(Cast(p, "spend", 20, 0, () => true), "成功消耗");
                p.Tick(1); Equal(80, p.Mana);
                True(!Cast(p, "failed", 20, 1, () => false), "CD拒绝不得刷新恢复等待");
                var checkpoint = p.CaptureCheckpoint(1); Equal(1, checkpoint.manaRegenDelayRemaining);
                var restored = new RpgPlayerState(); True(restored.RestoreCheckpoint(checkpoint, 100, out _), "恢复检查点");
                Equal(2, restored.GetCooldown("fireball", 100)); restored.Tick(101); Equal(80, restored.Mana);
                restored.Tick(102); Equal(85, restored.Mana);
                p.Tick(3); Equal(85, p.Mana);
                var failing = new RpgPlayerState(); failing.Respawn(100, 80, 0);
                True(!Cast(failing, "effect-failed", 20, 1, () => false), "效果失败");
                failing.Tick(3); Equal(85, failing.Mana);
            }); passed++;
            Check("奖励购买升级幂等与上限", () =>
            {
                var p = new RpgProgressData(); True(p.TryGrantReward("hidden.1", 100, "fireball"), "首次奖励");
                True(!p.TryGrantReward("hidden.1", 100, "fireball"), "重复奖励拒绝"); Equal(100, p.currency); Equal(1, p.unlockedSkillIds.Count);
                True(p.TryPurchaseSkill("buff.zi", 20), "购买成功"); True(!p.TryPurchaseSkill("buff.zi", 20), "重复购买拒绝");
                True(p.TryUpgradeSkill("fireball", 10, 1), "升级成功"); True(!p.TryUpgradeSkill("fireball", 10, 1), "超等级拒绝"); Equal(70, p.currency);
            }); passed++;
            Check("会话账本到上限不淘汰旧ID", () =>
            {
                var p = Player();
                for (int i = 0; i < RpgPlayerState.MaximumSessionAttempts; i++)
                    True(!Cast(p, "invalid." + i, float.NaN, 0, () => true), "非法请求仍消费账本");
                True(!Cast(p, "overflow", 0, 0, () => true), "满账本必须拒绝新ID");
                True(!Cast(p, "invalid.0", 0, 0, () => true), "最旧ID不得因淘汰重放");
            }); passed++;
            Check("未知技能非法数值与损坏备份", SaveChecks); passed++;
            return passed;
        }

        static void SaveChecks()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NarutoVR-RpgDataChecks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "player.json");
                var store = new RpgSaveStore(path, "test-profile", new[] { "fireball", "buff.zi" });
                var data = new RpgProgressData(); data.unlockedSkillIds.Add("unknown");
                True(!store.TrySave(data, out _), "未知技能不能保存");
                data.unlockedSkillIds.Clear(); data.checkpoint.health = float.PositiveInfinity;
                True(!store.TrySave(data, out _), "无限生命不能保存"); data.checkpoint.health = 100;
                data.checkpoint.cooldowns.Add(new RpgCooldownData { group = "unknown", remainingSeconds = 1 });
                True(!store.TrySave(data, out _), "未知冷却组不能保存"); data.checkpoint.cooldowns.Clear();
                data.unlockedSkillIds.Add("fireball");
                True(store.TrySave(data, out string reason), "首次保存失败：" + reason);
                data.currency = 10; True(store.TrySave(data, out reason), "第二次保存失败：" + reason);
                True(File.Exists(path + ".bak"), "第二次保存必须保留备份");
                string beforeWriteFailure = File.ReadAllText(path), backupBeforeWriteFailure = File.ReadAllText(path + ".bak");
                Directory.CreateDirectory(path + ".tmp");
                True(!store.TrySave(data, out reason), "临时路径不可写时保存必须失败");
                True(File.ReadAllText(path) == beforeWriteFailure && File.ReadAllText(path + ".bak") == backupBeforeWriteFailure,
                    "写入异常不得破坏已有主文件与备份");
                Directory.Delete(path + ".tmp");
                File.WriteAllText(path, "{ damaged JSON");
                True(store.TryLoad(out var loaded, out reason), "损坏主文件必须读备份：" + reason); Equal(0, loaded.currency);
                True(reason != null && reason.Contains("备份"), "恢复备份需给中文原因");
                loaded.currency = 20; True(store.TrySave(loaded, out reason), "恢复后保存：" + reason);
                File.WriteAllText(path, "broken again");
                True(store.TryLoad(out loaded, out _), "恢复后保存不能用坏主文件覆盖好备份"); Equal(0, loaded.currency);
                File.WriteAllText(path, "{\"version\":999,\"profileId\":\"test-profile\"}");
                string future = File.ReadAllText(path);
                string backupBeforeFutureSave = File.ReadAllText(path + ".bak");
                var failedEffect = Player();
                True(!Cast(failedEffect, "before-future-save", 20, 0, () => throw new InvalidOperationException("test")), "效果异常应退款");
                Equal(100, failedEffect.Mana);
                True(!store.TryLoad(out _, out reason) && reason.Contains("较新"), "未知新版本不能退回旧备份");
                True(!store.TrySave(data, out reason), "未知新版本禁止覆盖"); True(File.ReadAllText(path) == future, "新版本原文必须保留");
                True(File.ReadAllText(path + ".bak") == backupBeforeFutureSave, "拒绝新版覆盖也不能改备份");
                File.WriteAllText(path, JsonUtility.ToJson(new SaveEnvelope { version = 1, profileId = "test-profile", progress = data }));
                var other = new RpgSaveStore(path, "other-profile", new[] { "fireball", "buff.zi" });
                True(!other.TryLoad(out _, out _), "不能加载其他档案");
                True(!other.TrySave(data, out _), "不能覆写其他档案");
                File.WriteAllText(path + ".bak", "{\"version\":999,\"profileId\":\"test-profile\"}");
                True(!store.TrySave(data, out _), "未知新版本备份也禁止覆盖");
                File.WriteAllText(path, "{\"profileId\":\"test-profile\"}");
                File.WriteAllText(path + ".bak", "bad backup");
                True(!store.TryLoad(out _, out _), "缺失版本不得隐式当作当前版本");
                File.WriteAllText(path, JsonUtility.ToJson(new SaveEnvelope { version = 1, profileId = "test-profile", progress = new RpgProgressData { unlockedSkillIds = new System.Collections.Generic.List<string> { "unknown" } } }));
                File.WriteAllText(path + ".bak", "bad backup");
                True(!store.TryLoad(out _, out _), "未知技能载入应拒绝");
            }
            finally
            {
                // The path was created by this check from an absolute OS temp root and a fresh GUID.
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        static RpgPlayerState Player() => new RpgPlayerState(100, 100, 0, 2);
        static bool Cast(RpgPlayerState player, string id, float cost, double now, Func<bool> effect)
            => player.TryActivate(id, "fireball", null, cost, 3, now, effect, out _);
        static void Check(string name, Action check)
        {
            try { check(); }
            catch (Exception ex) { throw new InvalidOperationException("RPG数据检查失败：" + name, ex); }
        }
        static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Equal(float expected, float actual)
        { if (float.IsNaN(actual) || Math.Abs(expected - actual) > .0001f) throw new InvalidOperationException("期望" + expected + "，实际" + actual); }
    }
}
#endif
