# 技能、持续效果和视听表现由谁管理

核查日期 2026-11-09（Asia/Shanghai）。本文记录官方资料、样例源码与当前工程的静态对照。实施建议对应[重构方案第 5 节](../architecture/FULL_MODULE_MVC_REFACTOR_PROPOSAL_20261109_ZH.md)。

建议让调用方只通过 `SkillController` 施法。内部按照每项状态何时产生、谁能修改、何时结束来分工。玩家投出火球时，手部占用已经结束，火球仍要飞行。玩家施放护盾后，下一次攻击仍要读取盾值。这两个例子都要求施法动作和施法结果可以独立结束。

## 官方实现如何划分职责

| 参考 | 原始资料中的做法 | 对本项目的启发 |
|---|---|---|
| Epic Gameplay Ability System | Ability 协调动作，显式提交可以应用费用和冷却。Gameplay Effect 修改目标属性，可以持续到施法结束之后。Gameplay Cue 负责声音和画面。[能力执行](https://dev.epicgames.com/documentation/unreal-engine/using-gameplay-abilities-in-unreal-engine) · [效果与表现](https://dev.epicgames.com/documentation/en-us/unreal-engine/understanding-the-unreal-engine-gameplay-ability-system) | “技能效果”需要区分护盾吸收和护盾发光。玩家伤害结算读取前者，美术修改后者。火球成形时扣费是本项目的玩法规则。 |
| Epic Action RPG 火球，UE 4.27 样例 | 施法时将效果规格传给投射物，投射物检测到目标后才补入目标信息并应用效果。[官方火球样例](https://dev.epicgames.com/documentation/unreal-engine/skill-abilities-in-arpg?application_version=4.27) | 手势或语音能确认“已成形”，发射动作能确认“已离手”，只有实际命中才能触发命中教学或伤害。跨过其中一个阶段无需再次申请施法。 |
| Unity Boss Room | v2.2.0 中慢箭有独立物理对象，以命中集合过滤重复目标。快速法师弹在客户端通过一次消息触发视效。v3.0.0 的发射动作在执行时刻生成箭，正常结束会补一次发射检查，取消单独处理。[Unity 样例说明](https://github.com/Unity-Technologies/com.unity.multiplayer.docs/blob/main/docs/learn/rpcnetvarexamples.md) · [物理弹体源码](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/v2.2.0/Assets/Scripts/Gameplay/GameplayObjects/Projectiles/PhysicsProjectile.cs) · [发射动作源码](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/e9b773289a2f67c94528469ba80f3721e3a2c2af/Assets/Scripts/Gameplay/Action/ConcreteActions/LaunchProjectileAction.cs) | 我们的火球能持握、靠墙发射和命中训练靶，需要可查询碰撞的飞行对象。取消未发射火球应直接收起。单机 VR 可借用这种职责划分，联机样例的预测、RPC 和复制策略有各自的适用条件。 |

## 用现有玩法检验方案

| 当前工程中的证据 | 迁移时怎样证明行为保留 |
|---|---|
| **护盾有两条配方。** `buff.zi` 和 `buff.voice` 具有不同技能 ID，共用一个效果资产和 `shield` 冷却组。现有护盾施放会设置容量和到期时间，视觉创建失败仍能成功施放。[子印配方](../../Assets/ScriptableObjects/技能定义配置/子印加持.asset) · [语音配方](../../Assets/ScriptableObjects/技能定义配置/护体咒加持.asset) · [TryApplyShield](../../Assets/scripts/logic/Skills/Effects/SkillEffects.cs) | 两条配方分别检查了解、解锁和章节许可，再调用同一护盾实现。在两条配方都可用时，通过第一条施放后立即尝试第二条，应命中同组冷却。去掉可选盾光后受击，仍按玩家实际盾值吸收。 |
| **火球可能在发射的当前调用内命中。** 现有发射代码先记录在途弹体并发布发射，再调用 `SkillProjectile.Initialize`。它会同步检查初始重叠，火球可在方法返回前结束。[Launch](../../Assets/scripts/logic/Skills/Effects/SkillEffects.cs) · [Initialize](../../Assets/scripts/logic/Skills/Effects/SkillProjectile.cs) | 先确认持球和一次费用，再处理投掷。靠靶发射时验证“发射→命中”的顺序和一次回收，同时重叠墙和靶时保留墙体优先。输入取消仅收未发射持物，切场清理仍可找到在途弹体。 |
| **一次挥击和多次召剑的状态范围不同。** 剑刃和剑波共享本挥的 `SkillStrike`，按敌人的本次生命去重直接伤害。落雷另有施法者级的 `nextSwordLightningAt`，重新召剑不会创建一份新计时。[挥击执行](../../Assets/scripts/logic/Skills/Effects/ThunderSwordCast.cs) · [命中去重](../../Assets/scripts/logic/Skills/Effects/SkillStrike.cs) · [落雷间隔](../../Assets/scripts/logic/Skills/Effects/SkillEffects.ThunderSword.cs) | 同一挥的剑刃和剑波碰到同一目标，只结算一次直接伤害。收剑后已发出的波继续飞行。再次召剑并命中时仍遵守施法者的落雷间隔。刃轨、闪光和声音分别跟随对应事实。 |
| **总控中还有扩展效果的回收契约。** `RegisterEffectLifetime` 跟踪运行实例，激活失败回收本次新增实例，普通收起和全量清场走不同路径。[扩展登记与回收](../../Assets/scripts/logic/Skills/Effects/SkillEffects.cs) | 迁移总控时在技能模块内部保留登记、更新和回收能力。用一个登记后激活失败的扩展验证仅本次实例被清理，再用一次切场验证已有实例全部释放。 |

首个火球切片应先在旧入口记录成形、发射、即时命中、取消和回收的可观察结果，再用同一组输入检验新入口。盾值、剑波与落雷的测试分别覆盖目标、单次挥击和施法者三个状态范围。上述用例待实现后运行。
