# 技能扩展与调试接口

本页对应 2026-11-10 的 Unity 实现。技能的配方与输入方式写在 `SkillDefinition`，效果所需的数值、预置体与音频写在 `SkillEffectDefinition` 子类，逐次施放的可变状态放在施法实例中。[配置基类](../../Assets/scripts/data/Skills/Effects/SkillEffectDefinition.cs) · [现行调用图](SKILL_SYSTEM_CLASS_DIAGRAM_ZH.md)

## 添加配方或共享行为

增加新的结印或语音入口时使用独立技能 ID。两个配方需要共享冷却时填写相同的冷却组。使用既有火球、护盾或雷光剑执行方式时，直接引用相应配置。首章还需要在技能资源清单登记该 ID、地址和组，并为所属章节配置解锁或许可。独立训练场在训练目录登记。[技能配置与目录](../guides/CONFIGURATION_GUIDE_ZH.md)

## 添加执行行为

1. 创建 `SkillEffectDefinition` 子类，提供数值校验与编辑器说明。
2. 在本玩家的 `SkillController` 注册 `RegisterExecutor<T>((settings, skill) => ...)`。每种配置类型只注册一次。准备回调创建本次专用状态，返回 `PreparedSkillCast`。
3. 为句柄提供三个动作。`Arm` 复核并登记可撤销的本次对象。`Publish` 在模型确认后运行，可发布事实及启用表现。`discard` 清理失败路径的私有对象。准备遇到异常时，准备函数自己释放尚未交给句柄的资源。
4. 持续型行为可以用 `RegisterEffectLifetime` 交给中央更新与清理，使用右手的行为调用 `TryRegisterRightHandEffect`。释放后的飞行物或余光仍由原实例持有，完成时结束该实例。

训练与正式输入均调用 `SkillController.Cast`。返回的 `SkillCastResult` 包含本次请求的确认状态、稳定的失败类别和上下文。`TryCast` 为只关心成功或失败的调用者保留布尔入口。正式场景通过 `ISkillActivationPolicy.TryActivate` 检查当前玩家和章节权限，再进入同一提交方法。训练许可来自已绑定的目录。提交失败会清理本次待提交状态，确认后的呈现异常保留该次 MP 和冷却。[准备句柄](../../Assets/scripts/logic/Skills/Effects/PreparedSkillCast.cs) · [控制器与注册接口](../../Assets/scripts/logic/Skills/Effects/SkillController.cs)

## 调试与观察

| 接口 | 用途 |
| --- | --- |
| `SkillInputController.ReceiveVoice`、`ScheduleVoice` | 用口令和单调结束时间经过真实输入路径 |
| `SkillInputController.Cancel` | 使用当前场景绑定的取消策略 |
| `InputClock`、`HandInputClock`、`GameClock` | 分别驱动确定性的输入、手部观察和游戏时间检查 |
| `WristPoseOverride`、`ThrowGripProvider` | 注入腕部位姿或握持样本，继续经过同一投掷判定 |
| `ThrowObserved`、`ProjectileStageChanged`、`ShieldImpact` | 读取动作诊断、投射物阶段和实际吸收的盾量 |
| `WristMenuController.LatestInput` | 读取最近消费的腕部输入值拷贝 |

`SkillEffectConfigurationChecks` 验证扩展注册、失败回滚、发布异常、共享冷却、发射点重叠和真实雷光剑。输入时间、旧目录迁移和控制器的策略准入分别有独立检查。硬件追踪质量使用真实戴机采样记录。[执行检查](../../Assets/scripts/logic/Editor/Skills/Checks/SkillEffectConfigurationChecks.cs) · [口令时序](../../Assets/scripts/logic/Editor/Skills/Checks/SkillClockRegressionChecks.cs) · [迁移检查](../../Assets/scripts/logic/Editor/Skills/Checks/SkillMigrationChecks.cs)
