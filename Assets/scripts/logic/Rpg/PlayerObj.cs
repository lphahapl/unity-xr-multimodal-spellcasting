using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public readonly struct PlayerDamageResult
    {
        public readonly float Requested, Absorbed, HealthLost;
        public PlayerDamageResult(float requested, float absorbed, float healthLost)
        { Requested = requested; Absorbed = absorbed; HealthLost = healthLost; }
    }

    /// <summary>
    /// 玩家实体拥有唯一运行态和会话命中账本。没有自主 Update/生命周期重置；
    /// 控制器负责暂停、反馈与死亡流程，外部攻击应经控制器/伤害接收器进入。
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("NarutoVR/线性RPG/玩家实体 PlayerObj")]
    public sealed class PlayerObj : MonoBehaviour
    {
        readonly HashSet<string> receivedDamage = new(StringComparer.Ordinal);
        public RpgPlayerState State { get; private set; }
        public bool IsInitialized => State != null;
        public int ReceivedHitCount => receivedDamage.Count;
        public PlayerDamageResult LastSettledDamage { get; private set; }
        internal bool HasReceivedDamage(string hitId) => hitId != null && receivedDamage.Contains(hitId);

        /// <summary>首次创建独立状态；重复接线不重置生命、冷却或命中账本。配置由控制器的玩家基础定义提供。</summary>
        public RpgPlayerState Initialize(RpgPlayerDefinition definition)
        {
            if (State != null) return State;
            var state = definition ? definition.CreateState() : new RpgPlayerState();
            state.Tick(0);
            State = state;
            return State;
        }

        /// <summary>
        /// 主线程同步伤害管线；调用方先检查游戏暂停/输入门控。
        /// 会话内同一命中先去重再消耗护盾；复活和恢复检查点也不允许重放旧命中。
        /// 返回 true 表示该次命中已接收，完全吸收时仍为 true，与旧控制器入口一致。
        /// </summary>
        public bool TryReceiveDamage(string hitId, float amount, double gameNow, SkillEffects effects,
            out PlayerDamageResult result)
        {
            result = default;
            if (State == null || !State.Alive || !effects || string.IsNullOrWhiteSpace(hitId) ||
                !float.IsFinite(amount) || amount <= 0 || amount > 10000 ||
                receivedDamage.Count >= 65536 || !receivedDamage.Add(hitId)) return false;
            float remaining = effects.AbsorbDamage(amount);
            float applied = 0;
            if (remaining > 0) State.ApplyDamage(hitId, remaining, gameNow, out applied);
            result = new PlayerDamageResult(amount, amount - remaining, applied);
            LastSettledDamage = result;
            return true;
        }
    }
}
