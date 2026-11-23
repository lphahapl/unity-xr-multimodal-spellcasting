using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public struct GlyphEffectContext
    {
        public Vector3 hand, tip, forward, body;
        public Transform owner;
        public Transform pen;
        public bool tracked, bodyTracked;
        // 腕部估算与完整握姿切换时，从当前点重新计算手速。
        public bool rebaseHand;
        public double observed;
    }

    /// <summary>符模版持有共享的效果配置；每次命中返回独立执行实例。</summary>
    public abstract class GlyphEffectDefinition : ScriptableObject
    {
        public abstract GlyphEffectInstance Begin(GlyphEffectContext context);
    }

    public abstract class GlyphEffectInstance
    {
        public virtual bool IsComplete => false;
        /// <summary>目标实际锁定期间，持笔会话可随玩家行走并容许短时追踪中断。</summary>
        public virtual bool IsTethered => false;
        public virtual string Status => "";
        public abstract void Tick(GlyphEffectContext context, float deltaTime);
        public virtual void FixedTick(float deltaTime) { }
        public abstract void End();
    }
}
