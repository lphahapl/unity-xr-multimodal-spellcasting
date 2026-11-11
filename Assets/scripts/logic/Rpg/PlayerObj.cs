using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>
    /// 玩家实体拥有唯一运行态和会话命中账本。没有自主 Update/生命周期重置；
    /// 控制器负责暂停、反馈与死亡流程，外部攻击应经控制器/伤害接收器进入。
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("NarutoVR/线性RPG/玩家实体 PlayerObj")]
    public sealed partial class PlayerObj : MonoBehaviour
    {
        public PlayerModel Model { get; private set; }
        public RpgPlayerState State => Model?.Vitals;
        public bool IsInitialized => Model != null;
        public int ReceivedHitCount => State?.ReceivedHitCount ?? 0;
        public PlayerDamageResult LastSettledDamage { get; private set; }
        internal bool HasReceivedDamage(string hitId) => State?.HasReceivedDamage(hitId) == true;

        /// <summary>首次创建独立状态；重复接线不重置生命、冷却或命中账本。配置由控制器的玩家基础定义提供。</summary>
        public RpgPlayerState Initialize(RpgPlayerDefinition definition)
        {
            if (State != null) return State;
            var state = definition ? definition.CreateState() : new RpgPlayerState();
            state.Tick(0);
            Model = new PlayerModel(state);
            BindPlayerPresentation();
            return State;
        }

        /// <summary>
        /// 主线程同步伤害管线；调用方先检查游戏暂停/输入门控。
        /// 会话内同一命中先去重再消耗护盾；复活和恢复检查点也不允许重放旧命中。
        /// 返回 true 表示该次命中已接收，完全吸收时仍为 true，与旧控制器入口一致。
        /// </summary>
        public bool TryReceiveDamage(string hitId, float amount, double gameNow, out PlayerDamageResult result)
        {
            result = default;
            if (State == null || !State.SettleHit(hitId,amount,gameNow,out result)) return false;
            LastSettledDamage = result;
            PresentDamage(result);
            return true;
        }
    }
}
