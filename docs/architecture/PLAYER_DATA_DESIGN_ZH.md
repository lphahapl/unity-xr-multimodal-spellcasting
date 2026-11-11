# 玩家模型、技能授权与 v2 检查点

2026-11-10 更新。本文描述正式首章、P1 与线性训练场共用的本地玩家数据。首章的章节试用、装备中的手持技能、永久解锁可以独立变化。测试可直接从“读档后能不能用雷光剑”这条路径观察三者。

## 数据归属

| 领域 | 写入者 | 对外读取与保存 |
|---|---|---|
| 玩家生命、能量、恢复等待、冷却组 | 每个 `PlayerObj` 创建一份 `PlayerModel.Vitals`，类型为 `RpgPlayerState`。施法、受击、恢复在同一实例结算 | 安全检查点保存 HP、MP、剩余回蓝等待和各组剩余冷却秒数 |
| 护盾及命中去重 | 同一份 Vitals 持有剩余盾量、容量和到期时刻。命中 ID 先登记，再结算护盾和 HP，最后通知表现 | 护盾、手持物和飞行物属于临时状态。复活、恢复检查点仍保留当前玩家会话的命中去重记录 |
| 技能、物品和货币 | `PlayerModel` 持有已了解和永久解锁的技能 ID、技能等级、物品及激活状态、余额。收集、激活与购买通过 `ProgressionController` 发起 | v2 保存业务 ID 与值。共享配方、效果、Prefab 和当前玩家的技能副本都由运行时装配 |
| 章节阶段与奖励 | `PlayerModel.Chapter` 持有按顺序完成的阶段，根据场景定义推导当前阶段和通关状态。章节目标报告完成，控制器提交一次奖励 | v2 保存完成阶段 ID、已领的奖励 ID 和落点 ID。恢复时须符合当前章节的顺序、奖励及落点约束 |
| HUD 与腕匣 | `LinearRpgController.PlayerView` 提供 `IPlayerReadModel` | 可查 HP、MP、护盾、冷却、知识、等级、余额和解锁数量。界面按钮调用对应会话或成长命令 |

核心代码见 [PlayerModel](../../Assets/scripts/Core/PlayerModel.cs)、[RpgPlayerState](../../Assets/scripts/Core/RpgPlayerState.cs) 和 [UI 只读接口](../../Assets/scripts/Core/IPlayerReadModel.cs)。场景接线见 [PlayerObj](../../Assets/scripts/logic/Rpg/PlayerObj.cs)、[成长命令](../../Assets/scripts/logic/Rpg/ProgressionController.cs) 和 [RPG 会话](../../Assets/scripts/logic/Rpg/LinearRpgController.cs)。

## 首章如何恢复试用技能

1. 玩家进入首章后，控制器先读取清单中稳定的技能与冷却组 ID，再读取 v2 检查点。场景中的实际落点通过本场景节点 ID 验证。
2. `RuntimeSkillCatalog` 异步取得正式目录的配方租约，为当前玩家复制技能和效果配置，按存档等级从原始参数计算数值。当前匹配器、口令词表和冲突校验需要完整的正式目录。P1 与训练场使用各自显式配置的目录。
3. 资源和章节校验通过后，建立匹配器、绑定权限和游戏时钟，再开放输入。加载期间离开场景会取消该拥有者的等待，加载失败可重试。等待期间产生的输入会失效。
4. 激活时分别检查永久解锁加章节许可，或当前章节的试用许可，再检查共享手槽、所需锚点、MP 与冷却。旧 v2 若只在 `discoveredSkillIds` 中存有 `thunder.sword`，授权阶段可以试用。收起剑、换阶段或缓存命中都不会把这个 ID 写入 `unlockedSkillIds`。

例如，存档用 `thunder.sword` 标识技能。中文名可用于面板显示，Addressables 地址由清单映射，修改显示名或保持同一 ID 的资源迁址可以独立于玩家存档。配置规则见 [首章技能资源清单](../guides/CONFIGURATION_GUIDE_ZH.md#首章技能资源清单)，装配实现见 [RuntimeSkillCatalog](../../Assets/scripts/logic/Rpg/RuntimeSkillCatalog.cs)。

## 事务与游戏时钟

**成长。** 收集物品可以先开放知识，激活已收集物品再永久授予技能。购买在同一事务内扣除货币并解锁或升级。控制器先在独立候选模型上运行命令，再验证技能白名单、章节顺序、物品独占授予和完整快照。校验通过才一次性替换当前成长状态。训练场过关还需确认下一落点可达后才结算。

**施法。** `SkillController` 准备效果，再用唯一请求 ID 和冷却组向 Vitals 申请 `CastReservation`。同一提交内扣除 MP、写入 CD 及生效的护盾变化，然后发布确认。准备或武装失败可以释放预留，观察者在确认前读到上次提交的资源。已成功装备后主动收起，已经发生的花费仍保留。确认回调里的伤害在后续命令阶段重新检查并结算，重复命中 ID 只计一次。

**伤害与暂停。** 护盾先吸收未过期的部分，剩余伤害最多扣到零生命。死亡由会话清理手持物和技能表现；恢复从已验证检查点重建资源与章节。冷却和回蓝采用可暂停的游戏时钟，以剩余时长保存。当前版本用一个可刷新的数值护盾，多来源护盾叠加、惯用手档案、多人房主结算属于后续设计。产品与设备范围见 [联机规划](MULTIPLAYER_LAN_PLAN_ZH.md) 和 [头显兼容方案](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)。

## v2 文件和失败恢复

磁盘信封包含 `version = 2`、`profileId` 与 `progress`。角色恢复时只复制校验过的值和集合。消费方通过 `CaptureProgressSnapshot()` 得到最近检查点的独立副本，有待写快照时返回较新的一份。修改导出对象不会修改活动玩家。

| 程序集 | 具体职责 |
|---|---|
| `NarutoVR.Game.Core` | 玩家与章节模型、稳定 ID/数值边界、施法预留、命中结算及领域资源快照 |
| `NarutoVR.Game.Persistence` | 引用 Core。含 v2 DTO、双向映射、磁盘校验、备份和检查点协调。序列化由注入的 `IRpgSaveJson` 完成 |
| `NarutoVR.Game.Runtime` | 通过 `UnityRpgSaveJson` 注入原有 `JsonUtility` 编码，提供存档路径、模型与场景节点、目录和玩法模块接线 |

`RpgSaveStore` 写临时文件后复读并替换正式文件，保留有效备份。主文件损坏可读取有效备份，较新版本或外部档案受保护。`CheckpointSaveCoordinator` 持有最近成功及最新待写快照。有效成长遇到写盘失败会返回 `PendingSave`，同一会话恢复时优先读取它。新的有效交易替换待写快照，重试成功后更新已保存状态。启动时读到不兼容档案，会创建临时玩家并保护原文件。源码见 [v2 信封](../../Assets/scripts/Persistence/RpgProgressData.cs)、[映射](../../Assets/scripts/Persistence/RpgSaveMapper.cs)、[磁盘存储](../../Assets/scripts/Persistence/RpgSaveStore.cs)、[检查点协调器](../../Assets/scripts/Persistence/CheckpointSaveCoordinator.cs) 与 [Unity JSON 适配器](../../Assets/scripts/logic/Rpg/UnityRpgSaveJson.cs)。

## 验证范围

使用原版写出的 v2 样本，SHA-256 为 `bf6c1986cc1aedb842e2f38b252e68c0f375ec23f1baa56b8db1d4072717fe31`。独立 .NET 运行验证了旧样本的全部 v2 字段和列表顺序、无效奖励的原子性，以及预留期间 MP 和护盾保持旧值。隔离 Unity 6000.3.10f1 Play 已验证两组独立程序集的实际引用、读回 17 货币与 37 MP、用恢复后的领域模型和固定时刻资源逐字节复现原版文件、临时试用剑扣费一次，以及写盘失败后的收集、激活和升级恢复。桌面输入使用合成腕部姿态，头显追踪、触觉和帧预算需设备验证。可复用入口见 [Unity 验证脚本](../../Tools/Validation/run_skill_assembly.py)、[玩家与存档检查](../../Assets/scripts/logic/Editor/RpgDataChecks.cs) 和 [成长集成检查](../../Assets/scripts/logic/Editor/ProgressionIntegrationChecks.cs)。
