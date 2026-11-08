#if UNITY_EDITOR
using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>隔离玩家实体检查；不读写存档，不启动语音、XR 或关卡流程。</summary>
    public static class PlayerObjChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying || Time.timeScale <= 0)
                throw new InvalidOperationException("玩家实体检查须在未暂停的隔离 Play 场景运行。");
            int count = 0;
            void Check(bool ok, string reason) { if (!ok) throw new Exception("玩家实体检查：" + reason); ++count; }
            var root = new GameObject("玩家实体检查") { hideFlags = HideFlags.HideAndDontSave };
            var legacy = ScriptableObject.CreateInstance<RpgPlayerDefinition>();
            var definition = ScriptableObject.CreateInstance<PlayerSO>();
            var shield = ScriptableObject.CreateInstance<ShieldEffectDefinition>();
            var buff = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                GameObject Child(string name)
                { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
                var first = Child("玩家一").AddComponent<PlayerObj>();
                var second = Child("玩家二").AddComponent<PlayerObj>();
                var old = Child("旧配置玩家").AddComponent<PlayerObj>();
                var defaults = Child("默认玩家").AddComponent<PlayerObj>();
                var effects = root.AddComponent<SkillEffects>(); effects.head = root.transform; effects.playerRoot = root.transform;
                effects.GameClock = () => 0;
                var visual = Child("空护盾视觉"); visual.SetActive(false); shield.visualPrefab = visual;
                shield.capacity = 50; buff.durationSeconds = 30; buff.displayName = "检查护盾";
                definition.maxHealth = 120; definition.maxMana = 80;
                Check(!first.IsInitialized && !first.TryReceiveDamage("before-init", 10, 0, effects, out _), "未初始化不接收伤害");
                var state = first.Initialize(definition);
                Check(state.MaxHealth == 120 && state.MaxMana == 80, "PlayerSO 继承字段创建状态");
                Check(ReferenceEquals(state, first.State) && first.IsInitialized, "实体持有唯一运行态");
                second.Initialize(definition); old.Initialize(legacy); defaults.Initialize(null);
                Check(!ReferenceEquals(first.State, second.State), "同一 SO 为不同玩家建立独立状态");
                Check(old.State.MaxHealth == 100 && defaults.State.MaxHealth == 100, "旧定义和空定义兼容");
                Check(first.TryReceiveDamage("initial", 20, 0, effects, out var damage) && damage.Requested == 20 &&
                    damage.Absorbed == 0 && damage.HealthLost == 20 && state.Health == 100, "无护盾伤害数值保持");
                Check(second.State.Health == 120 && definition.maxHealth == 120, "伤害不修改其他玩家或配置资产");
                Check(ReferenceEquals(first.Initialize(legacy), state) && state.Health == 100 && first.ReceivedHitCount == 1,
                    "重复初始化不重置状态或账本");
                Check(effects.TryApplyShield(shield, buff), "建立真实 SkillEffects 护盾");
                bool replayAccepted = true;
                Action<float> replay = absorbed => replayAccepted = first.TryReceiveDamage("shielded", 15, 0, effects, out _);
                effects.ShieldImpact += replay;
                try { Check(first.TryReceiveDamage("shielded", 15, 0, effects, out damage), "护盾命中接收"); }
                finally { effects.ShieldImpact -= replay; }
                Check(!replayAccepted && effects.ShieldPoints == 35 && state.Health == 100 &&
                    damage.Absorbed == 15 && damage.HealthLost == 0, "全吸收也在护盾回调前去重，防止重入重复消耗");
                Check(!first.TryReceiveDamage("shielded", 15, 0, effects, out _) && effects.ShieldPoints == 35,
                    "后续同 ID 不再消耗护盾");
                Check(first.TryReceiveDamage("overflow", 50, 0, effects, out damage) && damage.Absorbed == 35 &&
                    damage.HealthLost == 15 && state.Health == 85, "护盾溢出才扣生命");
                int beforeInvalid = first.ReceivedHitCount;
                Check(!first.TryReceiveDamage("invalid", float.NaN, 0, effects, out _) &&
                    !first.TryReceiveDamage("invalid", 0, 0, effects, out _) &&
                    !first.TryReceiveDamage("invalid", 10001, 0, effects, out _) &&
                    !first.TryReceiveDamage(" ", 10, 0, effects, out _) && first.ReceivedHitCount == beforeInvalid,
                    "非法数值和空 ID 不占用账本");
                Check(first.TryReceiveDamage("invalid", 1, 0, effects, out _), "无效请求修正后仍可提交");
                Check(first.TryReceiveDamage("lethal", 500, 0, effects, out damage) && damage.HealthLost == 84 && !state.Alive,
                    "致死伤害受剩余生命上限限制");
                Check(!first.TryReceiveDamage("after-death", 1, 0, effects, out _), "死亡实体拒绝新命中");
                state.Respawn(0);
                Check(!first.TryReceiveDamage("initial", 20, 0, effects, out _) && state.Health == 120,
                    "复活不允许旧命中重放");
                var checkpoint = state.CaptureCheckpoint(0);
                Check(first.TryReceiveDamage("after-respawn", 5, 0, effects, out _), "复活后可接收新命中");
                Check(state.RestoreCheckpoint(checkpoint, 0, out _) && !first.TryReceiveDamage("after-respawn", 5, 0, effects, out _) &&
                    state.Health == 120, "检查点恢复保持会话命中去重");
                Check(state.TryActivate("cast", "fireball", "fireball", 20, 10, 0, () => true, out _) &&
                    state.Mana == 60 && second.State.Mana == 80 && second.State.GetCooldown("fireball", 0) == 0,
                    "法力和冷却仍由同一状态管理，玩家间隔离");
                return $"玩家实体与新旧配置 {count} 项检查通过";
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(legacy); Object.DestroyImmediate(definition);
                Object.DestroyImmediate(shield); Object.DestroyImmediate(buff);
            }
        }
    }
}
#endif
