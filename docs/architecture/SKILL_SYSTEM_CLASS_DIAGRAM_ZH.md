# 技能与玩家系统：模块输入输出、类图及调用流程

核对日期：2026-11-08。范围为当前讨论的玩家、施法和线性 RPG 主链路；附有技能音频调用图；不展开敌人 AI 与编辑器工具。图中的类和方法对应现有源码，不表示待实施的重构。

代码入口：技能运行时位于 Assets/scripts/logic/Skills，玩家与章节位于 Assets/scripts/logic/Rpg，移动位于 Assets/scripts/logic/Locomotion，手势采集位于 Assets/scripts/logic/SealLab；配置及纯数据主要位于 Assets/scripts/data。

## 读图方式

- 模块总图看数据流；类图看谁调用谁，箭头由调用者指向被调用者。连线写实际方法名，事件、属性读取和赋值另外标明。
- 空心三角表示继承或接口实现，不表示调用；实心菱形表示拥有运行态。
- 类图省略部分参数与类型，以便阅读调用关系；完整方法签名以源码为准。
- 六个模块按职责划分。LinearRpgController 同时参与准入、输入仲裁和章节协调，所以会在多张图中出现；当前代码并没有六个独立的总控制器。
- MonoBehaviour 是场景组件；ScriptableObject 是工程中的配置资产；普通 C# 类和接口不直接挂到 GameObject。
- 本页 Mermaid 图为本次更新版本。旧 SKILL_SYSTEM_CLASS_DIAGRAM_ZH.png 保留为历史图片，本页不再将它用作当前实现的替代图。

## 模块总图：输入什么，输出什么

~~~mermaid
flowchart TB
    H["手部追踪<br/>关节姿态、时间戳"] --> A["① 输入采集<br/>SealLab / SkillVoiceReceiver"]
    V["外部语音识别服务<br/>口令、结束时间、UDP 消息"] --> A
    C["SkillCatalog / SkillDefinition<br/>配方、效果、MP、CD"] --> B
    A -->|"Frame / Scores；PhraseReceived 事件"| B["② 技能匹配<br/>SkillTestController / SkillMatchEngine"]
    B -->|"TryActivate(skill)：匹配出的技能"| P["③ RPG 准入与玩家状态<br/>LinearRpgController / PlayerObj"]
    P -->|"效果回调：SkillEffects.TryActivate(skill)"| E["④ 效果执行<br/>SkillEffects / 各效果定义"]
    E -.->|"bool 返回：决定法力回滚或冷却提交"| P
    E --> O["持握物、护盾、飞行物<br/>命中伤害与效果事件"]
    H --> N["⑤ 移动与腕部 UI 仲裁<br/>LinearRpgController / Locomotion / WristMenu"]
    N -.->|"输入门禁回调；CancelInputOnly()"| B
    N -.->|"写入 PushInputSuppressed / ThrowInputSuppressed"| E
    N --> X["XR Rig 位置、菜单显示、输入占用"]
    Q["章节目标 / 已保存进度"] --> F["⑥ 章节、奖励与存档<br/>RpgChapterFlow / LinearRpgController"]
    F -.->|"阶段许可、解锁列表、恢复后的玩家状态"| P
    F --> S["新阶段、奖励、检查点存档"]
~~~

| 模块 | 输入 | 处理 | 输出 |
|---|---|---|---|
| ① 输入采集 | 追踪关节及观测时间；语音服务的 UDP 消息 | 提取手势特征、评分；校验语音会话、时间与白名单 | SealFrame、SealScore 列表；PhraseReceived(phrase, endedAt) 事件 |
| ② 技能匹配 | 帧、评分、口令、技能目录、输入门禁 | 单印/口令/组合窗口、证据消费、歧义处理 | 向策略提交一个 SkillDefinition；匹配状态和日志 |
| ③ RPG 准入与玩家状态 | 匹配出的技能、行动状态、解锁、MP/CD；受击请求 | 玩法准入、请求去重、法力预扣、效果回调、冷却提交；伤害结算 | 激活 bool、原因、更新后的 HP/MP/CD |
| ④ 效果执行 | 技能及效果配置、手部动作、游戏时间、碰撞 | 创建、更新、释放、命中、清理 | 激活 bool；持握物/护盾/剑/飞行物；伤害与事件 |
| ⑤ 移动与腕部 UI | 导航和腕部输入、生命/暂停/持物/结印状态 | UI、移动、施法的输入占用仲裁 | XR Rig 位移、菜单显示、门禁和取消操作 |
| ⑥ 章节与存档 | 章节目标结果、阶段配置、当前或持久化进度 | 检查目标、提交阶段、一次性奖励、保存/恢复 | 阶段与技能许可、奖励、检查点及 RpgProgressData |

## ① 输入采集：把硬件输入变成匹配器能读的数据

~~~mermaid
classDiagram
    direction TB
    class SealLab {
        <<MonoBehaviour>>
        +SealFrame Frame
        +Scores
        -LateUpdate()
        -Evaluate(now)
        +CaptureHand(hand, origin, observed, now, staleAfterSeconds)
    }
    class SealXRHandsSource {
        +Capture(left, origin, now, staleAfterSeconds)
    }
    class IHand {
        <<interface>>
        +GetJointPose(joint, pose)
    }
    class SealFeatureExtractor {
        +Extract(frame, features)
    }
    class SealRecognizer {
        +Update(profiles, features, now, bothHands, margin)
    }
    class SkillVoiceReceiver {
        <<MonoBehaviour>>
        +PhraseReceived
        +ConfigurePhrases(phrases)
        -Process(packet, now)
    }
    class SkillTestController {
        +ReceiveVoice(phrase, endedAt)
    }
    SealLab --> SealXRHandsSource : Capture(...)，XR Hands 路径
    SealLab --> IHand : CaptureHand() 内 GetJointPose(...)，Meta 路径
    SealLab --> SealFeatureExtractor : Extract(Frame, Features)
    SealLab --> SealRecognizer : Evaluate() 内 Update(...)
    SkillTestController --> SealLab : 读取 Frame / Scores
    SkillTestController --> SkillVoiceReceiver : ConfigurePhrases(voicePhrases)
    SkillVoiceReceiver ..> SkillTestController : PhraseReceived 事件 → ReceiveVoice(...)
~~~

两条手部采集路径按配置选用。SkillVoiceReceiver 接收外部服务已经识别出的口令，本类不负责从音频识别汉字。技能入口读取 SealLab 的帧和评分；它不是通过 WhenSealRecognized 事件直接释放技能。

## ② 技能匹配：确定玩家想放哪一个技能

~~~mermaid
classDiagram
    direction TB
    class SkillCatalog {
        <<ScriptableObject>>
        +skills
        +matching
        +Validate()
        +GetVoicePhrases()
    }
    class SkillDefinition {
        <<ScriptableObject>>
        +skillId
        +triggerMode
        +phrase
        +sealName
        +effectDefinition
        +manaCost
        +cooldownSeconds
    }
    class SkillTestController {
        <<MonoBehaviour>>
        +ActivationPolicy
        -Start()
        -LateUpdate()
        -ProcessVoice(phrase, endedAt)
        -ActivateTraced(skill)
        +CancelInputOnly()
    }
    class SkillMatchEngine {
        <<普通CSharp类>>
        +TryActivate
        +ExternalCooldownAuthority
        +Tick(time)
        +Observe(frame, scores, suppressActivation, suppressHeldRearm)
        +Voice(phrase, endedAt, arrivedAt)
        +Cancel(time)
        -Attempt(index, evidence)
    }
    class ISkillActivationPolicy {
        <<interface>>
        +TryActivate(skill) bool
        +ActivationStatus
        +OwnsCooldowns
    }
    SkillCatalog o-- SkillDefinition : skills 配置引用
    SkillTestController --> SkillCatalog : Validate() / GetVoicePhrases()
    SkillTestController *-- SkillMatchEngine : Start() 内 new SkillMatchEngine(...)
    SkillTestController --> SkillMatchEngine : Tick() / Observe() / Voice() / Cancel()
    SkillMatchEngine ..> SkillTestController : TryActivate 委托 → ActivateTraced(skill)
    SkillTestController --> ISkillActivationPolicy : TryActivate(skill)
~~~

Start() 把 Engine.TryActivate 绑定到 ActivateTraced。匹配器先消费这轮输入证据，再请求激活；准入失败也不会留着旧动作等冷却结束后自动发射。

激活分派顺序为 ActivationOverride 委托 → ActivationPolicy 策略 → SkillEffects 直接执行。RPG 在 Awake() 中清空旧委托并注入自身策略；没有策略的旧训练场可以直接执行效果，由匹配器维护本地冷却。RPG 策略 OwnsCooldowns 为 true，冷却由玩家状态维护。

## ③ RPG 准入与玩家状态：这次能不能放、扣多少、是否受伤

~~~mermaid
classDiagram
    direction TB
    class ISkillActivationPolicy {
        <<interface>>
        +TryActivate(skill) bool
        +ActivationStatus
        +OwnsCooldowns
    }
    class LinearRpgController {
        <<MonoBehaviour>>
        +Player
        -Awake()
        -TryActivate(skill) bool
        +ReceiveDamage(hitId, amount)
        +CancelCasting()
    }
    class PlayerObj {
        <<MonoBehaviour>>
        +RpgPlayerState State
        +Initialize(definition)
        +TryReceiveDamage(hitId, amount, gameNow, effects, result)
    }
    class RpgPlayerState {
        <<普通CSharp类>>
        +Health
        +Mana
        +TryActivate(activationId, skillId, group, cost, cooldown, now, activateEffect, reason)
        +ApplyDamage(hitId, amount, now, applied)
        +Tick(now)
    }
    class SkillEffects {
        +TryActivate(skill) bool
        +AbsorbDamage(damage) float
    }
    class RpgDamageReceiver {
        +TryReceiveDamage(hit)
    }
    LinearRpgController ..|> ISkillActivationPolicy : 实现
    LinearRpgController --> PlayerObj : Awake() 内 Initialize(playerDefinition)
    PlayerObj *-- RpgPlayerState : State 持有唯一运行态
    LinearRpgController --> RpgPlayerState : TryActivate(...) / Tick(GameTime)
    RpgPlayerState ..> SkillEffects : activateEffect 委托 → TryActivate(skill)
    RpgDamageReceiver --> LinearRpgController : ReceiveDamage(hit.AttackId, hit.Damage, hit)
    LinearRpgController --> PlayerObj : TryReceiveDamage(...)
    PlayerObj --> SkillEffects : AbsorbDamage(amount)
    PlayerObj --> RpgPlayerState : ApplyDamage(...)
~~~

ISkillActivationPolicy 是“技能激活策略接口”：它接收 SkillDefinition 并返回是否激活成功，还提供状态说明与冷却归属。技能本身的配方和数值在 SkillDefinition；具体效果的扩展入口在 SkillEffectDefinition。

RpgPlayerState.TryActivate 的顺序是：校验与去重 → 检查生命/MP/CD → 预扣 MP → 调用 activateEffect → 成功提交 CD，失败或抛错恢复 MP。图中的效果调用经过外部传入的委托；纯状态类没有直接持有 SkillEffects。

受击则先由 PlayerObj 对命中 ID 去重，再让护盾吸收伤害，剩余部分扣 HP。LinearRpgController 负责外围暂停门禁、反馈和死亡处理。

## ④ 效果执行：配置分派、运行态与命中

### 配置决定执行哪种效果

~~~mermaid
classDiagram
    direction TB
    class SkillEffects {
        <<MonoBehaviour>>
        +TryActivate(skill) bool
        +TryCreateProjectile(definition, skill) bool
        +TryApplyShield(definition, skill) bool
        ~TryEquipThunderSword(settings, skill) bool
        +RegisterEffectLifetime(effect) bool
    }
    class SkillEffectDefinition {
        <<abstract ScriptableObject>>
        +ValidateDefinition()
        +TryActivate(executor, skill) bool
    }
    class ProjectileEffectDefinition
    class ShieldEffectDefinition
    class ThunderSwordEffectDefinition
    class ThunderSwordCast {
        +TryStart(owner, settings, duration, grip, stamp, hasPalm) bool
        +Tick(now)
        +Cancel()
    }
    class ISkillEffectLifetime {
        <<interface>>
        +IsAlive
        +Tick(now)
        +Cancel()
    }
    class IReleasableHandEffect {
        <<interface>>
        +ReleaseHand()
    }
    SkillEffects --> SkillEffectDefinition : ValidateDefinition() / TryActivate(this, skill)
    SkillEffectDefinition <|-- ProjectileEffectDefinition : 继承
    SkillEffectDefinition <|-- ShieldEffectDefinition : 继承
    SkillEffectDefinition <|-- ThunderSwordEffectDefinition : 继承
    ProjectileEffectDefinition --> SkillEffects : TryCreateProjectile(this, skill)
    ShieldEffectDefinition --> SkillEffects : TryApplyShield(this, skill)
    ThunderSwordEffectDefinition --> SkillEffects : TryEquipThunderSword(this, skill)
    SkillEffects --> ThunderSwordCast : TryStart(...)
    ISkillEffectLifetime <|-- IReleasableHandEffect : 接口继承
    ThunderSwordCast ..|> IReleasableHandEffect : 实现
    ThunderSwordCast --> SkillEffects : TryRegisterRightHandEffect(cast)
    SkillEffects --> ISkillEffectLifetime : Tick(now) / Cancel()
~~~

SkillEffectDefinition 及其派生类保存共享配置，运行中的剑、投射物和护盾由执行器及运行态管理。旧技能未配置 effectDefinition 时，仍有 effectKind 的护盾/火球兼容分支。

### 投射物从手持到命中

~~~mermaid
classDiagram
    direction TB
    class SkillEffects {
        -Launch(direction)
        +ProjectileLaunched
    }
    class HandThrowState {
        +Process(hand, head, grip, now)
    }
    class HandThrowIntentState {
        +Process(hand, head, forward, grip, now)
    }
    class SkillProjectile {
        <<MonoBehaviour>>
        +Initialize(...)
        -Update()
        -Hit(collider, contact)
        -SpawnImpact()
        +Finish()
    }
    class SkillStrike {
        ~Resolve(target, point, allowLightning)
    }
    class SkillHitTarget {
        <<struct>>
        +Apply(id, damage, poiseDamage, attacker) bool
    }
    class EnemyObj {
        +ReceiveHit(...)
    }
    class SkillTrainingTarget {
        +ApplyHit(damage)
    }
    class VfxPoolManager {
        +TryPlayImpact(...)
    }
    SkillEffects --> HandThrowState : Process(...)
    SkillEffects --> HandThrowIntentState : Process(...)，启用辅助时
    SkillEffects --> SkillProjectile : Launch() 内 Initialize(...)
    SkillProjectile --> SkillStrike : Resolve(...)，存在 Strike 时
    SkillProjectile --> SkillHitTarget : Apply(...)，普通投射物
    SkillStrike --> SkillHitTarget : Resolve() 内 Apply(...)
    SkillHitTarget --> EnemyObj : ReceiveHit(...)
    SkillHitTarget --> SkillTrainingTarget : ApplyHit(damage)，训练靶路径
    SkillProjectile --> VfxPoolManager : SpawnImpact() 内 TryPlayImpact(...)
~~~

投射物技能的 TryActivate 成功表示手持物已生成，这时 MP/CD 已结算。后续松手/辅助动作或兼容的前推动作，才进入 Launch 并创建飞行组件；投出不重复走一次技能激活。命中特效池没有可用容量时，视觉可以跳过，伤害不依赖视觉播放成功。

## ⑤ 移动与腕部 UI：同一双手当前归谁使用

~~~mermaid
classDiagram
    direction TB
    class LinearRpgController {
        -LateUpdate()
        +UpdateWristUi(frame, ui, hardBlocked, now)
        -SyncWristOwnership()
        +CancelCasting()
    }
    class WristMenuController {
        +PrepareWristTap(sample, hardBlocked, now) bool
        +Tick(sample, controller, busy, now, reason)
        +OwnsInput
    }
    class WristMenuState {
        +Tick(frame, busy, pointerOwns, now, settings, wristToggle)
    }
    class WristMenuView {
        +ShowLauncher(pose)
        +HideLauncher()
        +SetPointer(...)
    }
    class RpgLocomotionController {
        +ProcessInput(frame, forwardOwnedByHeldSpell, blockActions, now)
        +ResetInput()
        +CancelPendingNavigation()
        +TryRestoreCheckpoint(node)
    }
    class SkillTestController {
        +CancelInputOnly()
        +InvalidateEarlierVoice()
    }
    class SkillEffects {
        +PushInputSuppressed
        +ThrowInputSuppressed
        +CancelUnreleased()
    }
    class Transform {
        <<Unity>>
        +position
    }
    LinearRpgController --> WristMenuController : PrepareWristTap(...) / Tick(...)
    WristMenuController --> WristMenuState : Tick(...)
    WristMenuController --> WristMenuView : ShowLauncher() / HideLauncher() / SetPointer()
    LinearRpgController --> RpgLocomotionController : ProcessInput(...)，UI 未占用时
    RpgLocomotionController --> Transform : 写 rigRoot.position
    LinearRpgController --> SkillTestController : CancelInputOnly() / InvalidateEarlierVoice()
    LinearRpgController --> RpgLocomotionController : ResetInput() / CancelPendingNavigation()
    LinearRpgController --> SkillEffects : 写输入抑制属性；CancelUnreleased()
~~~

RPG 先处理腕部 UI，再决定是否推进移动识别。UI 占用切换时，SyncWristOwnership 清除旧技能输入与排队口令，并重置移动输入。RpgLocomotionController 移动引用的 XR Rig 根节点；这些管理组件不要求挂在相机上。

CancelCasting 同时调用 CancelInputOnly、CancelUnreleased、CancelPendingNavigation。它取消当前输入、未释放效果和待导航；已经成功激活所消耗的 MP/CD 不因此返还。

## ⑥ 章节、奖励与存档：何时进入下一阶段

~~~mermaid
classDiagram
    direction TB
    class RpgChapterFlow {
        <<abstract MonoBehaviour>>
        +ObjectiveMet
        +OnStageEntered()
        +TryAdvanceStage(reason) bool
        +AllowsTrialSkill(id) bool
        +AllowsPermanentSkill(id) bool
    }
    class OpeningTrialChapter
    class TrainingChapterFlow
    class ObjectiveChapterFlow
    class LinearRpgController {
        +TryAdvanceStage() bool
        +CompleteChapterObjective(source) bool
        -CommitStage(stage) bool
        -ApplyStage()
        +SaveCheckpoint() bool
        +RestoreCheckpoint() bool
    }
    class RpgRewards {
        +Grant(progress, rewardId, currency, unlocks) bool
    }
    class RpgSaveStore {
        +TrySave(progress, reason) bool
        +TryLoad(progress, reason) bool
    }
    class RpgProgressData {
        <<存档数据>>
    }
    RpgChapterFlow <|-- OpeningTrialChapter : 继承
    RpgChapterFlow <|-- TrainingChapterFlow : 继承
    RpgChapterFlow <|-- ObjectiveChapterFlow : 继承
    LinearRpgController --> RpgChapterFlow : OnStageEntered() / TryAdvanceStage(...)
    LinearRpgController --> RpgChapterFlow : AllowsTrialSkill(id) / AllowsPermanentSkill(id)
    OpeningTrialChapter --> LinearRpgController : CompleteChapterObjective(this)
    TrainingChapterFlow --> LinearRpgController : CompleteChapterObjective(this)
    ObjectiveChapterFlow --> LinearRpgController : CompleteChapterObjective(this)
    LinearRpgController --> RpgRewards : CommitStage() 内 Grant(...)
    LinearRpgController --> RpgSaveStore : TrySave(...) / TryLoad(...)
    RpgSaveStore --> RpgProgressData : TrySave 输入；TryLoad 输出
~~~

具体章节判断自己的目标；LinearRpgController 校验提交者与目标状态，统一提交奖励、切换阶段和保存检查点。TryAdvanceStage 的具体行为由章节实现，不能把任意章节视为可直接跳关。存档恢复还涉及玩家状态恢复和 locomotion.TryRestoreCheckpoint，并清理旧输入及效果。

## 一次 RPG 施法的时间顺序

~~~mermaid
sequenceDiagram
    participant C as SkillTestController
    participant M as SkillMatchEngine
    participant R as LinearRpgController
    participant P as RpgPlayerState
    participant E as SkillEffects
    C->>M: Observe(...) / Voice(...)
    M->>M: Attempt(...)，消费本轮证据
    M->>C: TryActivate 委托 → ActivateTraced(skill)
    C->>R: ISkillActivationPolicy.TryActivate(skill)
    alt 行动、UI、导航或技能许可不通过
        R-->>C: false；更新 ActivationStatus
    else 玩法准入通过
        R->>P: TryActivate(..., activateEffect, out reason)
        alt 玩家状态拒绝：请求、生命、MP 或 CD 不通过
            P-->>R: false；不调用效果
        else 玩家状态准入通过
            P->>P: 预扣 MP
            P->>E: activateEffect() → TryActivate(skill)
            E-->>P: bool
            alt 效果成功
                P->>P: 提交冷却与回蓝延迟
                P-->>R: true
            else 效果失败或回调抛错
                P->>P: 恢复 MP，不提交冷却
                P-->>R: false
            end
        end
        R-->>C: bool；更新状态和反馈
    end
    C-->>M: 激活结果
~~~

初始化顺序：LinearRpgController.Awake 创建/取得 PlayerObj.State 并设置 skills.ActivationPolicy = this；SkillTestController.Start 创建匹配器、绑定激活委托与语音事件。接口没有自己独立运行的生命周期。

## 当前首章挂载位置

以下对应 Chapter01_Awakening 场景的实际接线。逻辑上的“玩家”不等同于一个同时挂全部脚本的 GameObject。

| 对象或资产 | 内容与关系 |
|---|---|
| 技能训练台 | SealLab、SkillVoiceReceiver、SkillTestController、SkillEffects、SkillTestView |
| 线性RPG · 关卡与导航 | LinearRpgController、PlayerObj、RpgLocomotionController；RpgDamageReceiver 若缺失由运行时补齐 |
| 第一章 · 初醒校验室 | OpeningTrialChapter；通过 rpg 引用关卡控制器，控制器的 chapter 引用该组件 |
| 左腕术式菜单与教学卡 | WristMenuController、WristMenuView |
| XROriginCameraRig / CenterEyeAnchor | XR Rig 和头部追踪相机；移动控制器通过 rigRoot/head 字段引用 |
| Project 中的配置资产 | SkillCatalog、SkillDefinition、各 SkillEffectDefinition、RpgPlayerDefinition 等 |
| 不挂物体的类型 | ISkillActivationPolicy、SkillMatchEngine、RpgPlayerState；分别是接口和由代码创建的普通类 |

## 阅读顺序与目录

| 目录或文件 | 职责与阅读重点 |
|---|---|
| [Skills/SkillTestController.cs](../../Assets/scripts/logic/Skills/SkillTestController.cs) | 入口接线、事件订阅、输入门禁、暂停恢复、取消及日志编号 |
| [Skills/Core/SkillDefinition.cs](../../Assets/scripts/data/Skills/Core/SkillDefinition.cs) | 技能身份、配方、效果引用、法力与冷却配置 |
| [Skills/Core/SkillMatchEngine.cs](../../Assets/scripts/logic/Skills/Core/SkillMatchEngine.cs) | 历史缓存、保持证据、双向匹配、Epoch 屏障及一次性消费 |
| [Skills/Core/ISkillActivationPolicy.cs](../../Assets/scripts/logic/Skills/Core/ISkillActivationPolicy.cs) | 将匹配结果接入不同玩法的策略接口 |
| [Rpg/LinearRpgController.cs](../../Assets/scripts/logic/Rpg/LinearRpgController.cs) | 当前 RPG 的资源和输入仲裁适配 |
| [Rpg/PlayerObj.cs](../../Assets/scripts/logic/Rpg/PlayerObj.cs) | 玩家状态所有者、会话命中去重与护盾/生命伤害结算 |
| [Rpg/RpgChapterFlow.cs](../../Assets/scripts/logic/Rpg/RpgChapterFlow.cs) | 章节目标、阶段回调与技能许可 |
| [Rpg/WristMenuController.cs](../../Assets/scripts/logic/Rpg/WristMenuController.cs) | 腕部输入、UI 状态和占用 |
| [Locomotion/RpgLocomotionController.cs](../../Assets/scripts/logic/Locomotion/RpgLocomotionController.cs) | 输入识别、移动与检查点落地 |
| [Rpg/RpgSaveStore.cs](../../Assets/scripts/data/Rpg/RpgSaveStore.cs) | 进度校验与持久化 |
| [Rpg/Data/RpgPlayerState.cs](../../Assets/scripts/data/Rpg/RpgPlayerState.cs) | 请求去重、法力预扣与失败恢复、成功冷却提交 |
| [Skills/Effects/SkillEffects.cs](../../Assets/scripts/logic/Skills/Effects/SkillEffects.cs) | 护盾、手持物、发射、扩展生命周期与资源清理 |
| [Skills/Effects/SkillEffectDefinition.cs](../../Assets/scripts/data/Skills/Effects/SkillEffectDefinition.cs) | 扩展效果配置与可注册生命周期接口 |
| [Skills/Effects/HandThrowState.cs](../../Assets/scripts/logic/Skills/Effects/HandThrowState.cs) | 松手边沿、采样去重、轨迹拟合与速度质量 |
| [Skills/Effects/HandThrowIntentState.cs](../../Assets/scripts/logic/Skills/Effects/HandThrowIntentState.cs) | 后拉前送、伸手保持和动作评分 |
| [Skills/Voice](../../Assets/scripts/logic/Skills/Voice) | 本机语音协议、握手、时钟映射和口令白名单 |
| [Skills/Visuals](../../Assets/scripts/logic/Skills/Visuals) | 纯视觉动画、短时视觉寿命和命中特效对象池 |
| [Editor/Skills/Checks](../../Assets/scripts/logic/Editor/Skills/Checks) | 配方、时钟、语音、场景与效果相关检查 |
| [Editor/Skills/Setup](../../Assets/scripts/logic/Editor/Skills/Setup) | 场景、配置与默认视觉资产的创建入口 |
| [Editor/Skills/Tools](../../Assets/scripts/logic/Editor/Skills/Tools) | 特效预览、定向修复与语音服务启动工具 |

`SkillTestController` 虽保留训练时期的类名，当前 RPG 也使用它作为施法入口。`SkillScenario`、`SkillTestView`、旧式效果枚举及隐藏序列化字段仍有训练场、迁移或检查入口使用。目录整理通过移动原 `.cs` 和 `.meta` 完成，脚本 GUID 随原文件保留。

## 修改时需要理解的约束

- 输入时间使用实时单调时钟，判断语音延迟和追踪新鲜度。RPG 的法力、冷却和效果期限使用会暂停的游戏时间，两种时间分别服务各自职责。
- `CancelInputOnly` 清除匹配上下文。RPG 的 `CancelCasting` 另外取消未发射物及待导航，已经消费的法力和冷却沿用既定规则。
- 组合技能持续保持同印后的再次就绪有独立计时。手部占用时清除该轮准备，避免释放占用后自动补发。
- `ISkillEffectLifetime` 注册成功才转移清理责任。执行器处理重复注册、清理期间注册以及 Tick 中增删效果的情况。任意多效果的通用原子事务仍属后续设计。
- 多印序列、画符触发、领域与移动约束见[架构设计基线](SKILL_FRAMEWORK_AND_INTERACTION_ZH.md)。当前类图只画已有实现，新增能力应同步更新配置校验、运行时匹配及检查入口。


## 技能音频：输入、输出与调用

输入是效果 SO 的 `audioProfile`、已成功发生的技能阶段以及世界坐标；输出是一次性空间声音或跟随持物的可选循环。音频播放失败不改变技能成功、伤害、MP 或 CD。玩家组件的挂载与配置入口见[技能音频接线](../guides/CONFIGURATION_GUIDE_ZH.md#技能音频接线)。

~~~mermaid
classDiagram
    class SkillEffectDefinition {
        SkillAudioProfile audioProfile
    }
    class SkillAudioProfile {
        Get(cue) SkillAudioVariation
    }
    class SkillAudioPlayer {
        TryPlay(profile, cue, position) bool
        StartHeldLoop(profile, follow)
        StopHeldLoop(follow)
        StopAll()
    }
    class SkillEffects {
        TryActivate(skill) bool
        CompleteActivationAudio(skill)
        PlayAudio(profile, cue, position) bool
        ClearAll()
    }
    class ThunderSwordCast {
        LaunchWave(origin)
        OnMeleeHit(hit)
        OnWaveHit(hit)
        ReleaseHand()
    }
    class SkillProjectile {
        SurfaceHit
        WorldHit
        StrikeResolved
    }
    SkillEffectDefinition --> SkillAudioProfile : audioProfile reference
    SkillEffects --> SkillAudioPlayer : TryPlay / StartHeldLoop / StopAll
    SkillAudioPlayer --> SkillAudioProfile : Get(cue)
    ThunderSwordCast --> SkillEffects : PlayAudio / StopHeldAudio
    SkillProjectile --> SkillEffects : SurfaceHit callback invokes PlayAudio
    SkillProjectile --> ThunderSwordCast : StrikeResolved invokes OnWaveHit
~~~

火球在 `TryActivate()` 成功后播放成形音，`Launch()` 播放发射音，飞行物保存本发音频配置并在首次 `SurfaceHit` 播放命中音。雷光剑在挥动判定成立、实际出波、成功直击和实际追加落雷时分别播放。`P1AudioManager` 不在这条调用链上；腕部菜单通过自己的 `PlayCue()` 播放 UI 声音。
