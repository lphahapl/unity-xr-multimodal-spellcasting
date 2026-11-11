# 配置与扩展指南

Unity VR 原型，用骨骼特征识别固定手印，再与中文口令组合释放技能。当前方向为 **线性关卡＋轻量 RPG**；首个三阶段试炼已实现玩家数值、节点导航、闪避、固定奖励和检查点，并通过首轮 Unity 检查。新动作仍需 Quest 真机调参；完整商店、剧情与 MR 放在后续。

本指南保留技能、语音和运行配置的详细操作说明。项目概览见 [README](../../README.md)。`docs/` 中较早的安装、技术选型和测试记录保留历史背景；涉及技能配置时，以正在运行的目录/效果资产及本节指定的现行操作指南为准。

完整决定、实现状态和开发顺序见 [技术与开发文档总索引](../README.md)。索引区分已经可运行的功能、尚未实现的契约和真机验证缺口，避免把设计图当成当前能力。

## 环境与运行

| 项目 | 工程锁定版本 / 当前用途 |
|---|---|
| Unity | 6000.3.10f1 |
| URP | 17.3.0 |
| Meta XR Core / Interaction / Interaction OVR | 207.0.1 |
| OpenXR / XR Hands | 1.15.1 / 1.7.2 |
| Input System | 1.18.0 |
| 中文语音 | PC 本机 Python + sherpa-onnx KWS，通过回环 UDP 向 Unity 发送事件 |

以 `Packages/manifest.json` 和 `ProjectSettings/ProjectVersion.txt` 为准。当前桌面手部调试路径是 **Quest → Steam Link → SteamVR OpenXR → Unity**。中文语音服务运行在电脑上，尚未移植到 Quest Android 包；不要把 Windows 侧测试等同于一体机完整验证。

1. 用上述 Unity 版本打开工程，等待导入完成。
2. 本工程的四个技能场景已经迁移。**Tools → NarutoVR → 技能配置 → 迁移现有技能场景** 仅处理下表四个场景，运行前需退出 Play 并保存场景；自建场景需自行绑定目录和 View。相同配置共享目录；有独立数值或基础视觉的旧场景生成独立目录，不替换手印录制模板。
3. 在 **Tools → NarutoVR → 技能展示** 打开试炼场景，连接头显后进入 Play。
4. 先用模拟口令确认手势与效果，再启动真实语音。初始配置中，**F6** 五秒后模拟“护体咒”，**F7** 五秒后模拟“火球术”；按钮、快捷键和倒计时均可配置。

| 场景 | 路径 | 用途 |
|---|---|---|
| 基础技能测试 | `Assets/Scene/SkillTest.unity` | 最小接线与三种触发模式对照；菜单在 `Tools → NarutoVR → 技能` |
| 焰道·火球靶场 | `Assets/Scene/FireballRange.unity` | 右手生成、轻推发射、远近靶与墙体阻挡 |
| 澄心·护盾试炼 | `Assets/Scene/ShieldTrial.unity` | 子印/口令加持、四次有限伤害演示；F8 准备或停止 |
| 合契·组合施法试炼 | `Assets/Scene/ComboSanctum.unity` | 先印后咒、先咒后印、等待超时与取消 |

默认配方为：**子印 → 护盾；护体咒 → 护盾；火球术＋寅印 → 右手火球**。组合窗口默认 8 秒，先后不限。火球准备约 0.25 秒后，手向前轻推约 12 厘米发射；头向准星用于瞄准。**这四个旧训练场**使用 **Esc**、完整取消口令或张开双手分开保持约 0.5 秒取消（历史兼容行为）；新 RPG 场景采用下节规则。详细动作及取消优先级见 [试炼场景说明](SKILL_SHOWCASES_ZH.md)。

## 线性 RPG 首个切片

打开 **Tools → NarutoVR → 线性RPG → 创建或打开测试场景**。已接入 HP/MP/CD、三阶段固定奖励、火球升级、隐藏盾和版本化检查点；完整剧情、头显商店、画符和敌人 AI 仍为后续。

## 当前操作（LinearRpgTest）

- **战斗移动**：左食指指向可达节点，头向/手指/距离初始权重为 60%/30%/10%；有效准备 0.12 秒、同点驻留 0.32 秒后传送。路径、地面、占位和碰撞仍须通过。
- **战斗后撤**：空手右手从胸前快速前推，沿起手时头显水平前向的反方向滑退，初值 0.8 米/0.25 秒。持球、UI 操作或结印占用时不启动。没有通用无敌帧。
- **探索行走**：左食指持续伸直，沿水平头向行走，默认 0.95 米/秒；收指、放腕、失联或 UI 操作即停。探索不响应闪避，切换模式后需有效收指重备。
- **火球**：寅印与新口令“火球术”组合，先后不限，默认窗口 8 秒。成功生成扣 MP/CD；手槽最多一发。头显朝向决定目标，从手边发射，三种手部动作只触发发射；同印持续有效 1 秒可配新口令重备，持球/失联/UI 占用时不累计。
- **腕饰**：抬左腕，右食指从外靠近点腕饰开关小菜单，退开后再点可关闭；放腕也关闭入口。主菜单支持近按，独立子卡只用射线高亮后轻捏确认，松开重备。结印、咒语和画符子卡均放腕保留，点“关闭”收起；拖动松手只放置卡片，左手暂失追踪时仍可用右手射线轻捏操作。需“结印＋口令”的术式从咒语入口查看时也显示手印图。
- **取消与占用**：可靠双腕同时低位保持约 0.4 秒取消待施法和未发射物，保留已生效护盾、飞行物和已提交 MP/CD；追踪丢失不等于取消或松手。靠近/悬停不占战斗输入，真正菜单操作或拖动才占用；导航证据不同时用于新结印。

参数及优先级见[战斗/探索移动](ENCOUNTER_LOCOMOTION_ZH.md)、[火球调参](FIREBALL_TUNING_ZH.md)和[腕饰操作](WRIST_MENU_INTERACTION_DESIGN_ZH.md)。旧四个展示场景保留自己的前推与取消逻辑，见[展示指南](SKILL_SHOWCASES_ZH.md)。这些桌面实现仍需本轮头显手感验收。

独立存档、词表与检查入口见[运行指南](LINEAR_RPG_QUICKSTART_ZH.md)和[验证说明](VALIDATION_ZH.md)。

## 配置分层：改数值不改识别代码

| 配置 | 负责内容 | 默认位置 |
|---|---|---|
| `SealProfile` | 手印特征权重、范围、覆盖率、保持时间 | `Assets/ScriptableObjects/手印识别配置/` |
| `SkillDefinition` | 稳定 ID、中文名、三种输入模式、单印、口令与拼音 tokens、窗口、冷却、时长、生成手、效果引用 | 从目录中的技能引用定位；默认在 `Assets/ScriptableObjects/技能定义配置/默认配置/` |
| `ShieldEffectDefinition` | 护盾容量、护腕视觉和基础材质 | `Assets/ScriptableObjects/护盾效果配置/默认护盾.asset` |
| `ProjectileEffectDefinition` | 伤害、速度、半径、持握/飞行时长、轻推参数及视觉 | `Assets/ScriptableObjects/投射物效果配置/默认火球.asset` |
| `ThunderSwordEffectDefinition` | 右手雷光剑的握姿、挥动阈值、直击、剑波与落雷 | `Assets/ScriptableObjects/雷光剑配置/雷光剑效果.asset` |
| `SkillAudioProfile` | 技能各阶段素材、变奏、音量与空间衰减 | `Assets/ScriptableObjects/技能音频配置/` |
| `SkillCatalog` | 本场可用技能、共享匹配参数、取消口令 | `Assets/ScriptableObjects/技能目录配置/试炼技能目录.asset` |
| `SkillResourceManifest` | 首章技能 ID、冷却组、资源地址、匹配参数和取消口令 | [首章技能资源清单](../../Assets/ScriptableObjects/技能目录配置/首章技能资源清单.asset) |
| 场景组件 | 骨骼来源、相机/玩家引用、碰撞层、HUD、模拟快捷键及试炼规则 | 场景中的 `SkillInputController`、`SkillController`、`SkillTestView`、`SkillScenario` |

同一个目录资产可被多个场景共享：改一次技能列表，各引用场景都会使用它。若某个场景需要不同技能集，复制 `SkillCatalog` 后重新绑定该场景控制器；不要在运行时往共享 SO 写玩家状态。旧序列化字段仅用于兼容，日常配置使用目录、效果资产与 View。

三个试炼场景共享“试炼技能目录”。基础 `SkillTest` 保留原来的简易视觉，使用 `技能目录配置/迁移配置/<场景GUID>配置/技能目录.asset`。从该目录的技能列表可以打开「子印加持」「护体咒加持」「火球术」。迁移工具按技能显示名生成文件名，运行时和存档仍使用稳定的技能 ID。旧 `Profiles/` 技能保留用于兼容，日常请从场景当前绑定的目录进入技能资产。

模拟快捷键在 `SkillTestView` 配置。每项填写技能的稳定 `skillId`（例如 `fireball`），再选「模拟口令按键」。进入 Play 后，按键使用当前玩家目录中这个 ID 对应的口令。默认分配跳过 F8，避免与护盾试炼冲突。Esc 用于取消，新增技能即使没有绑定快捷键也会有模拟按钮。


### 技能音频接线

从正在使用的 `SkillDefinition.effectDefinition` 打开效果 SO，在「技能音频」字段指定 `SkillAudioProfile`。火球使用 `Assets/ScriptableObjects/技能音频配置/火球音频.asset`，雷光剑使用同目录的 `雷光剑音频.asset`。音频配置只保存素材、音量、变奏、可选持握循环和空间衰减；上次变奏、声源和清理由玩家组件管理。

| 音频槽 | 火球 | 雷光剑 |
|---|---|---|
| 生效或成形 `activated` | 成功生成持握火球 | 成功召剑 |
| 有效挥砍 `swing` | 留空 | 挥动判定成立 |
| 脱手或出波 `released` | 成功发射 | 实际生成剑波 |
| 命中 `impact` | 首次碰撞 | 成功直击或剑波碰到场景；同挥直击去重 |
| 追加落雷 `lightning` | 留空 | 冷却允许且落雷伤害结算成功 |
| 收起或消散 `ended` | 可选 | 可选 |

玩家上与 `SkillController` 同一个物体挂 `SkillAudioPlayer`；首章与 P1 已接好，其余场景需要时自动补挂。它提供 8 个复用的一次性空间声道，保持命中声的世界位置、避免连续重复同一变奏，并在死亡、禁用、暂停时清理或暂停。`masterVolume` 控制技能音效；有配置而主动静音时不会再补通用提示音。

`P1AudioManager` 负责关卡音乐、战斗切换、语音触发的音乐衰减、环境声和脚步。腕部菜单确认/拒绝音在 `WristMenuView.acceptedSound/rejectedSound`，留空沿用合成提示音；其他操作提示在 `RpgInteractionFeedback`。这些组件不再负责选择技能素材。

火球的三组原有音频各保留三条；雷光剑使用从导入库截取、淡入淡出并控制峰值的 11 条音频副本，位于 `Assets/Audio/ThunderSword`。两份配置暂不填持握循环，避免长时间重复声遮盖口令。导入库原始 WAV 保持不变；桌面触发验证与真人试听、Quest 空间混音分别验收。

### 不写代码，增加第四个技能

以新增 **“雷盾”纯口令护盾** 为例：

1. 从目标目录的技能列表定位并复制“护体咒加持”技能资产，设置唯一 ID（例如 `buff.thunder`）、显示名“雷盾”、触发方式“纯咒语”、完整口令“雷盾”。拼音 tokens 填 `l éi d ùn`。这些 token 存在于当前模型词表，但实际识别效果仍需录音验证。
2. 复制 `默认护盾.asset`，例如把容量改为 150，换上自己的视觉预置体；将它赋给新技能的 **效果配置资产**。时长、冷却仍在技能资产上配置。
3. 把新技能加入目标 `SkillCatalog` 的技能列表。目录已绑定的场景无需增加新的判断分支。
4. 打开引用目标目录的场景，执行 **Tools → NarutoVR → 技能配置 → 验证当前场景**，修正重复 ID、口令冲突、快捷键冲突、空引用或无效数值。
5. 涉及真实语音时，执行 **导出当前场景语音配置**，然后重启 Python 服务。重新进入 Play；电脑操作台会按配方生成模拟按钮，需要快捷键时在 `SkillTestView` 的列表中指定。

新增不同伤害、速度或外观的投射物同理：复制火球技能和 `ProjectileEffectDefinition`，改配置并加入目录。新技能可选纯咒语或已有有效单印组合；只要生成手当前追踪有效，就能复用持握与发射实现。

口令会去除空白与标点后比较，不能重复或互相包含。例如已有“火球术”时，不要再加“大火球术”；也不能占用取消口令。纯结印配方与其他配方共用同一个印会形成冲突。当前只支持**单印配方**，不支持在字段中填一串印来表示序列。

## 代码职责与新增效果类型

### 雷光剑与可选近战判定

`Assets/Scene/LinearRpgTest.unity` 已接入雷光剑：其独立 `线性训练场技能目录.asset` 包含该配方，`TrainingChapterFlow.trialSkills` 提供常驻练习授权。新旧 v2 训练场存档都可直接练习，电脑模拟按钮显示“练习授权”；纯口令为“雷光剑”，消耗 20 MP、冷却 4 秒、持续 8 秒。该场景词表已更新。下面的独立场景流程仍可用于只测试剑的效果。

2026-11-08 按用户要求，`KaogongP1Trial.unity` 也绑定同一训练目录并常驻授权 `thunder.sword`，进入 P1 即可练习；APK 原生词表由该目录生成，包含「雷光剑」。P1 练习授权不写入永久解锁；首章当前也已按用户要求增加下述临时测试授权。[P1 操作与打包](P1_TRIAL_ZH.md)

首章的正式清单包含雷光剑，`OpeningTrialChapter.initialDiscoveries` 与全部 20 个 `sections[].trialSkills` 均含 `thunder.sword`。阶段授予临时试用，施放仍检查能量、冷却、输入占用与手部追踪。玩家可以了解雷光剑并在本章试用，永久解锁列表保持原值。

### 首章技能资源清单

1. 在 Project 中按玩法用途命名资产，例如 [雷光剑.asset](../../Assets/ScriptableObjects/雷光剑配置/雷光剑.asset)。配方的 `skillId` 填 `thunder.sword`，供存档、章节和玩法查询。类名写明职责，技能及效果可独立配置。
2. 把配方加入本地 Addressables 分组，设置唯一地址，例如 `skills/thunder.sword`。在 [首章技能资源清单](../../Assets/ScriptableObjects/技能目录配置/首章技能资源清单.asset) 填入业务 ID、地址和实际冷却组；若配方冷却组为空，则填技能 ID。共享护盾的两种输入配方都填 `shield`。
3. 在首章的「关卡控制器 → 异步技能资源清单」选择清单。当前场景一次装入五份配方生成完整词表，并为每个玩家建立独立的技能和效果副本。P1 与训练场继续用各自的 `SkillCatalog`。
4. 在编辑器执行 **Tools → NarutoVR → 技能配置 → 验证当前场景**。校验会查唯一地址、内容包分组、类型、业务 ID、冷却组和词表。需要调整永久解锁或试用时，分别修改成长配置或章节授权。

程序接入时，装配方调用 `await manifest.AcquireAsync("thunder.sword", cancellationToken)`，从返回的租约取 `Asset`。持有期间共用原始定义，最后一位使用者 `Dispose()` 时释放底层引用。玩家施法直接使用已绑定的本地配方，再检查当前阶段授权、能量与冷却。其他种类的资源可以实现 `IResourceCatalog`，由同一个 `SharedAssetLoader.AcquireAsync<T>` 解析各自的业务 ID。[共用资源加载器](../../Assets/scripts/logic/SharedAssetLoader.cs) · [玩家技能装配](../../Assets/scripts/logic/Rpg/RuntimeSkillCatalog.cs)

永久入口为阶段资产 `RpgStageDefinition.unlockSkillIds`（`Unlock Skill Ids`／Inspector「解锁技能标识」），以及成长物品 `ProgressionItemCatalog.items[].unlockSkillIds`（物品激活时授予）。物品独占技能不能同时出现在初始或阶段奖励。`initialDiscoveries` 是初始了解列表，不等于永久解锁；有效旧存档替换初始进度，已领奖阶段与已激活物品不会因修改奖励数组自动补发。需要追补时另行明确存档迁移，不能把重新进入 Play 当作补发。首章永久施用仍由 `permanentSkillGate → GATE_CHECK` 检查；临时授权是独立准入分支。[首章操作](CHAPTER01_AWAKENING_ZH.md#雷光剑临时测试授权与永久解锁)

授权、挥动识别、几何朝向和头显手感分别验收；桌面合成输入通过不等于真人手感已通过。

1. 复制基础技能场景 `Assets/Scene/SkillTest.unity` 作为测试场景。给其中的 `SkillInputController.catalog` 指定 `Assets/ScriptableObjects/雷光剑配置/雷光剑测试目录.asset`。把 `SkillTestView` 上旧技能的模拟快捷键列表清空，控制台会按目录生成“模拟雷光剑”按钮。
2. 进入 Play，保持右腕可追踪，在电脑操作台点模拟按钮。剑生成后看向目标，右手做一次明确的横挥或竖挥。剑波从手前飞向出波瞬间的头向准星；横竖只改变波面朝向，不改变飞行目标。青色弧线显示当前横竖预览，灰色表示恢复/出波间隔未结束，橙色表示路径受阻。测试目录以“雷光剑”作为纯口令配方。真人语音需要另行导出当前场景词表并重启本机服务。
3. 要启用剑身接触，在法宝预制体挂一个 `MeleeHitDetector`，把刃根和剑尖两个子节点拖给 `bladeBase`、`bladeTip`。雷光剑的两个节点位于本地 +Z 的 0.10 和 0.62 米。接触半径初值为 0.035 米。移除或禁用该组件即可单独测试剑波。出波与落雷继续由技能运行态管理。
4. 用可受击的敌人检查紫色落雷。近战和蓝色剑波共用本次挥动的直击 ID；剑先打中前排，波实际接触前排后可穿过并直击后排。每波最多触发一次落雷，同时受施法者冷却限制。训练靶可测直击，墙体可测阻挡。触碰剑身、打靶和击墙均不触发落雷。

当前雷光剑效果配置已经接入 `Assets/Art/VFX/ThunderSword/ImportedTrial/` 的独立特效副本，保留原有青铜剑网格、刃根/剑尖和战斗数值。来源为 EpicToonFX 的 Sword Slash/Wave/Hit，以及 MasterMagicFX 的 LightningLine/LightningExp 贴图和粒子造型；供应商原件不修改。独立剑术测试目录和 LinearRpgTest 的练习目录都引用这份效果；首章当前以全部 20 段临时测试授权提供雷光剑，并未添加永久解锁奖励。

| 视觉层 | 预制体 | 播放与回收 |
|---|---|---|
| 剑身电弧 | `雷光剑_电弧.prefab` | 跟随握持剑，隐藏/收剑时一起处理 |
| 挥砍弧光 | `挥砍_青弧.prefab` | 连续有效挥动触发，按挥动平面定向；根对象 0.32 秒回收 |
| 持续剑波 | `剑波_持续青弧.prefab` | 核心粒子循环，由实际飞行物命中、超时或取消销毁；默认飞行 1.2 秒 |
| 紫电与余波 | `落雷_紫电余波.prefab` | 仍只在剑波成功触发落雷时播放；主电弧最迟约 0.125 秒结束，命中点余波约 0.5 秒，根对象 0.65 秒回收 |
| 命中火花 | `命中_电火花.prefab` | 剑刃成功直击，或剑波终止于敌人/墙面时播放；根对象 0.45 秒回收 |

`ThunderSwordEffectDefinition` 中的 `slashPrefab`、`impactPrefab` 可留空，三个视觉寿命字段只决定瞬时对象回收时间，不改变伤害、判定窗口或射程。`MeleeHitDetector.HitResolved` 在直击结算后通知视觉层；`SkillVisualBursts` 由本次施放持有并回收余光。收剑后余光可自然消散，`ClearAll` 会同时清除它们。剑波循环特效要随 `SkillProjectile` 使用，单独放在场景中会持续播放。

历史外观备份：上级 `Assets/Art/VFX/ThunderSword/` 保留 `雷光剑.prefab`、`雷光波.prefab`、`落雷.prefab`，原件配置备份位于本机 `Logs/thunder-vfx-trial-20261108/before/`。如恢复旧剑波，必须重新核对其模型局部轴与 `ThunderSwordCast.WaveRotation` 的匹配，并验证横竖弧口及预览；当前固定轴修正针对 ImportedTrial 青弧，不能仅替换预制体后直接沿用验证结论。旧落雷视觉寿命为 `0.16` 秒。当前验证为 Unity 6000.3.10f1 独立副本的桌面 Play、粒子采样与渲染；Quest 双目、帧时及头显内亮度仍需实测。

火球和雷光波的飞行实例根节点都使用 `SkillProjectile`。火球在脱手时加上该脚本，使用其中的伤害与削韧字段。剑波优先取预制体上的脚本，缺少时在发射时加上。剑波命中后由本挥的 `SkillStrike` 提交直击，它与剑身共享去重状态，因此只改飞行组件的 `damage` 字段不会改变剑波伤害。当前雷光波在其有效射程内使用固定基础直击。

| 测试配置 | 初值 | 修改位置 |
|---|---|---|
| 持有 / 召剑冷却 / RPG 法力 | 8 秒 / 4 秒 / 20 MP | 雷光剑.asset |
| 挥动位移 / 速度 / 出波间隔 | 0.40 米 / 1.8 米每秒 / 0.85 秒 | 雷光剑效果.asset |
| 手掌横竖最短位移 / 生成及恢复缓冲 / 横竖切换比例 | 0.16 米 / 0.25 秒 / 1.25 | 雷光剑效果.asset，Inspector“有效挥动” |
| 连续刃轨材质 / 寿命 | 雷光剑刃轨.mat / 0.14 秒 | 雷光剑效果.asset，Inspector“近战刃轨” |
| 身体直击 / 独立落雷 / 落雷冷却 | 6 / 7 / 每施法者 2 秒 | 雷光剑效果.asset |

握柄基准由掌部刚性关节确定：剑刃朝向拇指/食指一侧，避免直接套用 SDK 腕骨 +Z。`gripOffset` 与 `gripEuler` 用于掌心握姿微调。

有效挥动至少持续 0.06 秒，手掌位移以横向或纵向的主要分量检查；纯前后推手不会触发剑波。横/竖仍为两档，弧形预制体本地末乘 X 轴 +90°，使弧口朝施法者、弓顶沿发射方向，两端分别横向或纵向展开；预览使用相同旋转。连续刃轨独立于有效攻击窗口，轻挥也可看见，停住后 0.14 秒淡出；仅用一个动态 MeshRenderer、最多 32 顶点/30 三角，无新增实时灯光或纹理采样。`bladeTrailMaterial` 留空可关闭这一层，原有有效挥动弧光和命中特效仍各自保留。

攻击窗口同时要求连续有效样本、足够的剑尖速度/位移及手掌位移；原地转腕不算挥剑。追踪跳变、头部位移和恢复追踪不生成攻击。剑自然到期后释放右手，让在途剑波飞完。RPG 中双腕放低也可收剑，暂停、死亡和清场会同时回收剑波。雷光剑与未发射火球的追踪丢失不再作为取消：0.18 秒内冻结最后视觉，之后暂时隐藏并保留槽位，恢复后重新准备。雷光剑 8 秒和火球持握 60 秒的正常寿命继续计时；明确放低双手/取消口令仍可收起，已发出的剑波继续飞行。火球旧 `trackingLossGrace` 字段仅保留序列化兼容，已隐藏且不再参与取消。新鲜取消口令可越过腕部 UI 的新施法门控，但旧时间戳、暂停和失效输入仍被拒绝。

当前测试配方与美术用于独立试验，正式铜叶归属、课程和数值仍待确定。

### 匹配与执行

```text
Meta / OpenXR 手部来源
        ↓
SealLab：骨骼快照、有效性、特征和各印评分
        ↓
SkillInputController：输入订阅、暂停/恢复、取消和模拟事件
        ↓                 ↑
SkillMatchEngine      SkillVoiceReceiver ← 本机 sherpa-onnx
        ↓
SkillController 读取 SkillEffectDefinition → 准备并提交本次 Cast
        ↓
SkillTestView：只读状态 HUD、电脑按钮和快捷键
```

- **Controller** 负责输入生命周期和接线，不拼接 HUD，也不写死技能口令。
- **View** 根据已生效配方显示技能、评分、缓存、语音状态和效果，并发送显式调试命令；不决定匹配结果。
- **Engine** 负责缓存、保持、竞争印消歧、双向时间匹配、查询玩家冷却和输入消费；不生成场景物体。
- **SkillController** 以效果配置类型选择执行器。火球、护盾和雷光剑有各自的运行实例，结算后的事实供教学、音效和 HUD 使用。

扩展新机制时，继承 `SkillEffectDefinition` 描述资产，在玩家装配时调用 `SkillController.RegisterExecutor<T>(prepare)`。准备函数返回 `PreparedSkillCast`，提供登记、本次提交后的呈现、放弃本次准备三个回调。登记失败时释放新实例，已生效的护盾和玩家资源保持原值。配置共享原件、玩家派生参数、场景中的本次 Cast 分别有自己的寿命。

持续效果把每次施法的状态放在独立对象中，实现 `ISkillEffectLifetime`：

```csharp
bool IsAlive { get; }
void Tick(double now);
void Cancel();
```

创建后调用 `executor.RegisterEffectLifetime(instance)`。返回 `true` 才把生命周期交给执行器；若返回 `false`，创建方负责清理该新实例。执行器负责更新及结束清理，暂停/禁用时也会收回资源。`Cancel()` 应释放实例拥有的物体、订阅和临时资源；SO 只存配置，不存剩余时间、运行对象或玩家数据。旧训练场取消顺序为持握投射物、最近的扩展效果、护盾；已发射投射物不被普通取消追回。新 RPG 场景普通取消只收起未发射物并清待施法，保留已生效护盾与飞行物，不能据旧顺序接线。

提交前的自定义执行器自行管理其准备对象，取得扩展租约后由技能控制器负责结束时清理。确认后的呈现异常会留下诊断并保留已经发生的 MP 和冷却结算。

工程使用现有 MonoBehaviour 接线、纯逻辑匹配器和 ScriptableObject 配置，未引入额外 DI/ECS 框架。

## P1 的护体反馈与火球音效

护体成功时，左腕的青玉符弧合拢；护盾实际吸收伤害时，铜色刻度短暂亮起。普通吸收的光效持续约 0.22 秒，破盾余光约 0.32 秒。常驻护盾外观仍由护盾效果配置的「护盾视觉预置体」指定。短闪材质是 `Assets/Art/VFX/Resources/ShieldWristPulse.mat`。[P1 护体与破盾动图](../visuals/20261107_skill_feedback/shield-play.gif)

在火球效果配置的“音频配置”中打开“火球音频”，为成形（`Activated`）、发射（`Released`）和撞击（`Impact`）设置变奏。玩家的 `SkillAudioPlayer` 负责空间声道及总音量。实际资源位于 `Assets/Audio/Fireball/`。[依次试听成形、发射、撞击](../visuals/20261107_skill_feedback/fireball-form-release-impact.wav)

## 中文语音配置与测试

Windows 编辑器自动启动语音时，会从当前接收器关联的技能目录导出当次词表及收发端口。修改口令后重新进入 Play；P1 的词表包含「金刚护体」。本次配置可在 `Library/SkillVoice/play-config.json` 检查。

手动运行服务时，Unity 菜单 **Tools → NarutoVR → 技能配置 → 导出当前场景语音配置** 可将有效口令、拼音 tokens、取消词和端口写到 `Tools/SkillVoice/skill-voice-config.json`。修改口令后重新导出并重启手动服务，使它和游戏读取同一份目录。

```powershell
# 在工程根目录执行；首次安装见 docs/SKILL_VOICE_SETUP_ZH.md
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --check-config
& './Tools/SkillVoice/Start.ps1'
& './Tools/SkillVoice/Stop.ps1'
```

上面三条依次表示检查、启动、停止，按需要分别执行。使用指定设备可传 `-Device <设备ID>`；`Start.ps1` 还支持 `-Config <JSON路径>` 和 `-Keywords <实验词表路径>`。

默认优先读取导出 JSON；默认 JSON 不存在才回退到旧 `keywords.txt`。显式 `--keywords` 覆盖本次声学词表，适合离线实验，**不会自动修改 Unity 白名单**；显式端口参数优先于 JSON。非法配置会报错，生成的运行词表只放在临时目录，不覆盖原词表。

**模拟按钮成功不代表识别模型成功。** 当前 KWS 只找关键词，不理解整句否定：已提供的“不要释放火球术”录音同样命中“火球术”。“领域展开，诛伏赐死”仅做过离线词表实验，尚未接入领域技能。[真人录音基线](../research/PLAYER_AUDIO_BASELINE_ZH.md)记录了这项限制。实际麦克风应选择 Steam Link 对应输入，连接和启动方法见 [语音接入说明](SKILL_VOICE_SETUP_ZH.md)。

## 缓存、快照与运行状态

默认缓存最多 **10 秒、640 帧**骨骼深拷贝与评分事实；容量与时长都有上限，默认以最高 60 Hz 采样配置使用。目录允许调整 5～10 秒历史和容量，但容量必须覆盖当前采样率，组合窗口还须为语音传输延迟留出预算。

进入 Play 时复制匹配规则、配方和有效口令，用于本次会话。每次施法也复制效果所需参数，已持握或飞行的物体不会被之后调整资产的数值反向改变。编辑配置后停止再运行；不要把改 Inspector 当成动态重载协议。

一直保持同一个印只算一次；取消会清除旧证据，需有效退出旧姿态后再施法。追踪丢失不当成重新结印。评分是相似度，不是校准后的统计置信度；口令减免也不会取消覆盖率、有效性、保持或消歧检查。

旧护盾试炼的未吸收伤害不扣玩家HP；新 `LinearRpgTest` 则已接完整的盾后HP伤害、MP/CD、升级与检查点。当前边界见 [线性 RPG 设计](../architecture/LINEAR_RPG_DESIGN_ZH.md)，长期存档/事务设计见 [玩家数据设计](../architecture/PLAYER_DATA_DESIGN_ZH.md)。地面阵法仍是视觉装饰，没有环境遮罩、移动限制或领域结算。

## Quest 优先与后续 PICO 兼容

当前优先把 Quest 的线性 RPG 最小切片做好，再做画符/敌人属性硬直、关卡与轻量升级。PICO 作为后续比赛适配方向：共享技能规则，通过设备适配器接入骨骼、口令、手锚和环境能力。当前 XR Hands 路径仍依赖 Meta 类型，尚不能宣称已经支持 PICO；已录模板也不能未经标定就保证跨设备准确。方案和迁移关卡见 [Quest/PICO 兼容设计](../architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)，版本差异见 [官方资料核对](../research/QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md)。

本阶段只有兼容契约设计，没有安装 PICO SDK、替换 Rig 或覆盖子丑寅模板。后续新增系统应避免直接依赖厂商类型；Meta 手指特征保留旧版本，新几何公式与设备标定分别验证。

## 后续同房间 PvE：目前只预留接口

多人暂定两人同房间 PvE，再考虑四人。NGO＋Unity Transport、房主权威、局域网 IP 直连仍是候选；先完成单机系统再接网络。本机保留手印/口令识别，未来房主统一确认施法和战斗结果。详细契约见 [局域网方案](../architecture/MULTIPLAYER_LAN_PLAN_ZH.md)，官方框架对比见 [调研来源](../research/MULTIPLAYER_SOURCES_ZH.md)。

当前四个场景仍是单机；网络接口目前是文档草案，没有 NGO SDK 或联网场景。同房间还需要独立的空间标定、漂移/重定中心处理，不能靠连上同一 Wi-Fi 自动对齐。Meta/PICO 原生共享锚不假定互通。现有同步 `TryActivate` 也不能把“网络发送成功”当成“技能已生效”。

## 镜头与技能演出：设计阶段

建议使用 **Cinemachine 3＋Timeline**：玩家相机继续跟随头显，独立观众相机负责火球跟拍、技能特写和领域全景；表现层根据玩法阶段播放，不负责伤害、冷却或资源结算。工程已有 Timeline 1.8.10，尚未安装 Cinemachine，也没有运行时观众镜头。具体接线、取消、平台边界和验收见 [镜头与演出设计](../architecture/CAMERA_PRESENTATION_DESIGN_ZH.md)。

接入前需显式绑定玩家头锚/瞄准，修正工具中单相机的假设。当前全局时间暂停会清理火球等效果，因此演出不能通过 `Time.timeScale = 0` 暂停战斗。演出模块可独立试做，不阻塞玩家数据开发。

## 验证与资料入口

配置修改后先执行 **Tools → NarutoVR → 技能配置 → 验证当前场景**；**运行架构检查** 和 **运行旧场景迁移检查** 用于回归。2026-10-29 的重构验证通过47项匹配核心、34组配置/迁移/运行契约、20项效果检查，以及实际录音模型→UDP→护盾链路，详见 [架构重构验收](VALIDATION_ZH.md)。实际头显发射、双眼画面、真人麦克风和帧率仍需真机验收；这些历史检查不自动覆盖之后的改动。

```powershell
# 配置契约：不打开麦克风、不向 Unity 注入事件
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 -m unittest discover -s './Tools/SkillVoice' -p 'test_configuration.py' -v
# 固定合成音、静音及普通负例的实际模型回归
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/verify_fixtures.py'
```

- [试炼场景与特效资源](SKILL_SHOWCASES_ZH.md)
- [基础技能操作与判定](CONFIGURATION_GUIDE_ZH.md)
- [原交付验收记录](VALIDATION_ZH.md)
- [中文语音接入](SKILL_VOICE_SETUP_ZH.md)、[测试录音要求](AUDIO_TEST_RECORDING_ZH.md)
- [线性 RPG 当前规则与验收](../architecture/LINEAR_RPG_DESIGN_ZH.md)
- [玩家数据设计](../architecture/PLAYER_DATA_DESIGN_ZH.md)
- [技术与开发文档总索引](../README.md)
- [镜头与技能演出方案](../architecture/CAMERA_PRESENTATION_DESIGN_ZH.md)

录制与手印标定代码位于 `Assets/SealLab/`，技能模块位于 `Assets/`，本机语音位于 `Tools/SkillVoice/`。`Library`、`Temp` 和模型运行日志不属于手工配置入口；本次重构前备份路径记录在 `Logs/architecture-backup-path.txt`。

## 仓库与本机产物

本文中 `Logs/`、`Tools/SkillVoice/logs/` 和备份绝对路径指开发机上的历史产物，不随 Git 仓库分发。公开验收摘要见本目录的 QA 文档；重新执行检查会生成新的本地报告。脚本命名空间、Unity 菜单和本地启动器暂保留 `NarutoVR`，仓库名称为 `unity-xr-multimodal-spellcasting`。

## 同印重新就绪例外

组合技能可以配置同印重新就绪时间，当前 RPG 为 1 秒；需要新口令，并受手槽、追踪和 UI 占用门控。不要把“持续同印绝不再施法”的早期说明套用于已启用此选项的 RPG 目录。
