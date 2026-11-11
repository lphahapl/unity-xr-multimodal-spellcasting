# 技能与玩家系统调用图

2026-11-10 核对首章、独立训练及线性训练场的运行路径。这一页标注现有类型与方法。首章五个技能 ID 共享三种执行行为。配置、玩家数值、每次施法的状态分别由不同对象持有。

## 玩家装配

~~~mermaid
flowchart TD
    Manifest["SkillResourceManifest<br/>技能 ID、地址、冷却组和匹配设置"] --> Catalog["RuntimeSkillCatalog<br/>本玩家的运行目录与等级参数"]
    Catalog -->|"Acquire 与持有租约"| Loader["SharedAssetLoader"]
    Loader -->|"同地址合并加载"| Asset["共享 SkillDefinition / SkillEffectDefinition 原型"]
    Catalog -->|"把当前目录交给本玩家"| Input["SkillInputController"]
    Player["PlayerObj"] -->|"单独持有"| Model["PlayerModel / RpgPlayerState"]
    RPG["LinearRpgController"] -->|"装配与恢复"| Player
    RPG --> Catalog
    Input -->|"配置配方索引"| Matcher["SkillMatchEngine"]
    Trainer["独立训练场 SkillCatalog"] --> Input
~~~

首章装配先用字符串 ID 校验存档技能和冷却组，再加载原型，建立本玩家的配方和效果副本。玩家退出时释放玩家目录持有的资源租约。相邻玩家分别保存自己的等级、能量与冷却。独立训练场使用场景目录、独立训练模型及游戏时钟。仍使用旧格式的场景经 `BindDefinitions` 把枚举和场景字段转换成带类型的效果配置。[首章资源清单](../../Assets/ScriptableObjects/技能目录配置/首章技能资源清单.asset) · [运行目录](../../Assets/scripts/logic/Rpg/RuntimeSkillCatalog.cs) · [旧配置转换](../../Assets/scripts/logic/Skills/Effects/SkillController.Legacy.cs)

## 输入与施法

~~~mermaid
flowchart TD
    Seal["SealLab 骨骼与评分"] --> I["SkillInputController"]
    Voice["SkillVoiceReceiver 口令及结束时间"] --> I
    I -->|"Tick / Observe / Voice"| M["SkillMatchEngine"]
    M -->|"消费证据，提交一个技能"| S["SkillController.Cast"]
    S -->|"正式 RPG 权限、本玩家目录、输入占用"| G["ISkillActivationPolicy<br/>LinearRpgController"]
    G --> T["同一 SkillController 内部提交"]
    S -->|"训练目录许可"| T
    T -->|"按配置类型选取"| E["FireballCast / ShieldCast / ThunderSwordCast"]
    E --> H["PreparedSkillCast"]
    T -->|"预留后复核，再确认"| R["RpgPlayerState.CastReservation"]
    R -->|"一次提交"| Stat["MP、冷却组、待生效盾量"]
    T -.->|"确认后 Publish"| Fact["持握、特效、音频、诊断与结果事件"]
~~~

动作或口令命中后，匹配器先消费此次证据。同帧防重入和输入代次属于匹配器。共享冷却由玩家战斗模型按 `cooldownGroup` 结算。护盾的结印配方 `buff.zi` 和口令配方 `buff.voice` 有不同的解锁 ID，共用 `shield` 组。无效追踪、输入独占、死亡、能量或冷却拒绝后都需要新输入。[输入接线](../../Assets/scripts/logic/Skills/SkillInputController.cs) · [战斗模型](../../Assets/scripts/Core/RpgPlayerState.cs)

## 一次请求的边界

~~~mermaid
sequenceDiagram
    participant S as SkillController
    participant C as 本次施法
    participant P as 玩家战斗模型
    participant V as 表现与订阅者
    S->>C: Prepare，建立未激活对象并冻结参数
    S->>P: TryReserveCast
    S->>C: Arm，确认追踪与手部许可
    alt 准备和复核成功
        S->>P: Commit，扣费并开始共享冷却
        S->>C: Publish，启用对象
        C-->>V: 确认后的阶段或表现通知
    else 准备失效或资源拒绝
        S->>C: Dispose，清理本次对象
        S->>P: 释放已取得的预留
    end
~~~

火球使用冻结的参数跟手。发射先把飞行体登记到玩家，在发射通知后检查出手点重叠。目标接受伤害才计命中，墙面接触可以独立产生爆散。普通 RPG 取消收起手中火球或剑，护盾、已经飞出的火球及剑波继续各自的生命周期。暂停、死亡或场景退场按对应清场入口处理。[火球](../../Assets/scripts/logic/Skills/Effects/FireballCast.cs) · [飞行与碰撞](../../Assets/scripts/logic/Skills/Effects/SkillProjectile.cs)

护盾在玩家模型确认时写入盾量及期限。正式受击由 `PlayerObj` 和战斗模型完成去重、减盾和扣血。技能控制器接收实际吸收量播放触觉、破盾与腕部反馈。独立护盾试炼的演示脉冲调用 `AbsorbPracticeDamage`。[护盾施法](../../Assets/scripts/logic/Skills/Effects/ShieldCast.cs) · [护盾试炼](../../Assets/scripts/logic/Skills/SkillScenario.cs)

雷光剑每次施放建立自己的参数快照和右手占用，持续跟踪挥动。一次挥动的剑身接触与剑波共享 `SkillStrike` 的直击账本，落雷另行结算。收剑释放手部，已经发出的剑波和视觉仍由这次施法管理。[雷光剑](../../Assets/scripts/logic/Skills/Effects/ThunderSwordCast.cs) · [直击结算](../../Assets/scripts/logic/Skills/Effects/SkillStrike.cs)

## 玩家、章节与表现

输入仲裁读取腕部 UI、交互和移动状态，决定能否请求下一次施法。完成章节及奖励由章节和成长模块处理。存档出口使用字符串 ID 与可序列化快照。模块间完整关系可从现行总图继续查看。[玩家输入](../../Assets/scripts/logic/Rpg/PlayerInputController.cs) · [检查点协调](../../Assets/scripts/Persistence/CheckpointSaveCoordinator.cs) · [总架构图](TARGET_ARCHITECTURE_20261110_ZH.md)

表现可订阅 `ProjectileStageChanged`、`ProjectileHitTarget`、`ShieldImpact` 和 `ThrowObserved`。技能音频从效果配置的可选 `SkillAudioProfile` 选取激活、维持、释放或命中声音。监听者抛错不撤销已确认的施法。[技能音频](../../Assets/scripts/logic/Skills/Effects/SkillController.Audio.cs) · [实际事务回归](../../Assets/scripts/logic/Editor/Skills/Checks/SkillEffectConfigurationChecks.cs)
