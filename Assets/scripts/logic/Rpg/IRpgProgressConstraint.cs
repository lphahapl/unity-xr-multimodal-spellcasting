namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>可选进度模块的校验契约。主控统一调用，模块不得在校验中修改玩家进度。</summary>
    public interface IRpgProgressConstraint
    {
        /// <summary>启动前检查本模块引用及与阶段/技能配置的冲突。</summary>
        void ValidateConfiguration(LinearRpgController owner);
        /// <summary>验证完整候选进度；读档、物品和商店提交共用此入口。</summary>
        bool ValidateProgress(RpgProgressData progress);
    }
}
