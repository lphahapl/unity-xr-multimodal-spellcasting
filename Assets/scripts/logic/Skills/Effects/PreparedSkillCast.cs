using System;

namespace NarutoVR.SealCombat
{
    /// <summary>单次施法准备结果。Arm 仅登记本轮未公开的对象；Publish 在资源结算成功后运行。</summary>
    public sealed class PreparedSkillCast : IDisposable
    {
        readonly Func<bool> arm;
        readonly Action publish;
        Action discard;
        public PreparedSkillCast(Func<bool> arm, Action publish, Action discard)
        {
            this.arm = arm ?? throw new ArgumentNullException(nameof(arm));
            this.publish = publish;
            this.discard = discard;
        }
        internal bool Arm() => arm();
        internal void Publish()
        {
            discard = null;
            publish?.Invoke();
        }
        public void Dispose()
        {
            var release = discard;
            discard = null;
            release?.Invoke();
        }
    }
}
