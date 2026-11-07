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
- **腕饰**：抬左腕，右食指从外靠近点腕饰开关小菜单，退开后再点可关闭；放腕也关闭入口。主菜单支持近按，独立子卡只用射线高亮后轻捏确认，松开重备。结印/咒语卡默认放腕保留，画符预览默认随腕收起。
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
| `SkillCatalog` | 本场可用技能、共享匹配参数、取消口令 | `Assets/ScriptableObjects/技能目录配置/试炼技能目录.asset` |
| 场景组件 | 骨骼来源、相机/玩家引用、碰撞层、HUD、模拟快捷键及试炼规则 | 场景中的 `SkillTestController`、`SkillEffects`、`SkillTestView`、`SkillScenario` |

同一个目录资产可被多个场景共享：改一次技能列表，各引用场景都会使用它。若某个场景需要不同技能集，复制 `SkillCatalog` 后重新绑定该场景控制器；不要在运行时往共享 SO 写玩家状态。旧序列化字段仅用于兼容，日常配置使用目录、效果资产与 View。

三个试炼场景共享“试炼技能目录”。基础 `SkillTest` 保留原来的简易视觉，使用 `Configuration/迁移/<场景GUID>/技能目录.asset`。旧 `Profiles/` 技能保留用于兼容；日常请从正在使用的目录进入技能资产，避免改到旧副本。再次迁移不会把旧组件数值覆盖回已调好的新资产。

模拟快捷键在 `SkillTestView` 配置；默认分配跳过 F8，避免与护盾试炼冲突。Esc 用于取消，新增技能即使没有绑定快捷键也会有模拟按钮。

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

```text
Meta / OpenXR 手部来源
        ↓
SealLab：骨骼快照、有效性、特征和各印评分
        ↓
SkillTestController：输入订阅、暂停/恢复、取消和模拟事件
        ↓                 ↑
SkillMatchEngine      SkillVoiceReceiver ← 本机 sherpa-onnx
        ↓
SkillEffects → SkillEffectDefinition → 当前效果实例
        ↓
SkillTestView：只读状态 HUD、电脑按钮和快捷键
```

- **Controller** 负责输入生命周期和接线，不拼接 HUD，也不写死技能口令。
- **View** 根据已生效配方显示技能、评分、缓存、语音状态和效果，并发送显式调试命令；不决定匹配结果。
- **Engine** 负责缓存、保持、竞争印消歧、双向时间匹配、冷却和输入消费；不生成场景物体。
- **Effects** 管理当前效果与临时物体，委派效果资产激活；视觉预置体不负责伤害。

新增武器、领域等**不同机制**时，继承 `SkillEffectDefinition`，实现 `Summary`、`ValidateDefinition()` 和 `TryActivate(SkillEffects executor, SkillDefinition skill)`。成功创建后才返回 `true`；失败不能留下半个效果。无需向语音接收器、UI 或匹配器添加技能名称分支。

持续效果把每次施法的状态放在独立对象中，实现 `ISkillEffectLifetime`：

```csharp
bool IsAlive { get; }
void Tick(double now);
void Cancel();
```

创建后调用 `executor.RegisterEffectLifetime(instance)`。返回 `true` 才把生命周期交给执行器；若返回 `false`，创建方负责清理该新实例。执行器负责更新及结束清理，暂停/禁用时也会收回资源。`Cancel()` 应释放实例拥有的物体、订阅和临时资源；SO 只存配置，不存剩余时间、运行对象或玩家数据。旧训练场取消顺序为持握投射物、最近的扩展效果、护盾；已发射投射物不被普通取消追回。新 RPG 场景普通取消只收起未发射物并清待施法，保留已生效护盾与飞行物，不能据旧顺序接线。

自动失败回收仅覆盖本次新注册的扩展句柄。自定义效果若先调用内置护盾/投射物方法再返回失败，需要自行恢复已经提交的变化；当前还没有通用的多效果原子事务，后续在玩家资源与技能事务模块中处理。

工程使用现有 MonoBehaviour 接线、纯逻辑匹配器和 ScriptableObject 配置，未引入额外 DI/ECS 框架。

## 中文语音配置与测试

Unity 菜单 **Tools → NarutoVR → 技能配置 → 导出当前场景语音配置** 将当前目录的有效口令、拼音 tokens、取消词和端口写到 `Tools/SkillVoice/skill-voice-config.json`。修改口令后需要**重新导出、重启服务，并重新进入 Play**，使声学词表与 Unity 接收白名单一致。

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
