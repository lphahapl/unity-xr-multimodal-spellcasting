using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>共享效果配置的扩展基类；持续时间、生成物等可变状态由执行器或注册的生命周期对象持有。</summary>
    public abstract class SkillEffectDefinition : ScriptableObject
    {
        [Header("技能音频")]
        [InspectorName("音频配置（可选）")] public SkillAudioProfile audioProfile;
        public virtual string Summary => GetType().Name;
        public virtual string ValidateDefinition() => null;
        /// <summary>本效果是否实现可购买的等级成长；商店不能向无等级规则的效果出售升级。</summary>
        public virtual bool SupportsLevels => false;
        /// <summary>在玩家副本上从原始配置重建等级参数，不能修改 original 或累乘本次副本。</summary>
        public virtual void ApplyLevelFrom(SkillEffectDefinition original, int level) { }
    }

    /// <summary>
    /// 自定义效果持有的运行时状态。注册成功后由执行器逐帧 Tick，并在结束、取消或停用时
    /// 调用一次 Cancel；Cancel 负责释放该效果拥有的资源。
    /// </summary>
    public interface ISkillEffectLifetime
    {
        bool IsAlive { get; }
        void Tick(double now);
        void Cancel();
    }

    /// <summary>收回手中物时，已发出的效果仍由原生命周期管理。</summary>
    internal interface IReleasableHandEffect : ISkillEffectLifetime
    {
        void ReleaseHand();
    }
}
