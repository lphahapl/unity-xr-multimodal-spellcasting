using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>共享效果配置的扩展基类；持续时间、生成物等可变状态由执行器或注册的生命周期对象持有。</summary>
    public abstract class SkillEffectDefinition : ScriptableObject
    {
        public virtual string Summary => GetType().Name;
        public virtual string ValidateDefinition() => null;
        /// <summary>效果成功生效后才返回 true；返回 false 时应清理本次创建的效果资源。</summary>
        public abstract bool TryActivate(SkillEffects executor, SkillDefinition skill);
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
}
