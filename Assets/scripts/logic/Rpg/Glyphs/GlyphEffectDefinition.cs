using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public struct GlyphEffectContext
    {
        public Vector3 hand, wrist, tip, forward, body;
        public Quaternion gripRotation;
        public bool rightPointing, rightOpen, rightFist, indexRayValid;
        public Ray indexRay;
        public Transform owner;
        public Transform pen;
        public bool tracked, bodyTracked, holdingBeltPen;
        // 腕部估算与完整握姿切换时，从当前点重新计算手速。
        public bool rebaseHand;
        public double observed;
    }

    /// <summary>符模版持有共享的效果配置；每次命中返回独立执行实例。</summary>
    public abstract class GlyphEffectDefinition : ScriptableObject
    {
        /// <summary>从辨符成功起允许归笔，空手接续效果，包括剧情板的金闪过渡。</summary>
        public virtual bool AllowsEmptyHand => false;
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
