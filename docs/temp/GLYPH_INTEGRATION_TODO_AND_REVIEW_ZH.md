# 画符接入待办与追加审阅

创建日期：2026-10-31。状态：**源码调研与设计，尚未实现画符运行时**。

**2026-10-31 用户确认：画符属于战斗外交互，不要求快速战斗反应；拿起笔即进入画符模式，期间不能结印或攻击。** 本文主文已据此修订。第 8 节保留原审阅；其中战斗内快速画符、菜单启动画符等假设不再作为现行需求。

“拿起”指笔对象的有效抓取成功事件，不是空手握拳/捏合。建议以放回/放下笔退出；抬笔只结束一笔，仍保持画符模式。笔先按虚拟可抓取工具设计，具体抓法和落笔方式待原型验证。

优先级：**现有移动、结印、投掷、腕饰/UI 的故障与真人手感优先；画符列为 P2**。用户测试现有交互时发现问题，暂停画符实施，先处理现有问题。当前投掷调试入口见[动作评分与自动记录](SCORED_THROW_DIAGNOSTICS_ZH.md)。

本轮完成两份仓库的临时源码拉取和本项目接入审阅；没有导入第三方 SDK、修改运行脚本、场景或技能资产，没有启动第三方工程。按用户后续安排，其他 AI 可在本文末尾追加审阅；本轮没有发起第二账号模型调用。下文标记为“建议”的行为不是现有功能，也不是已经真人验收的交互。

## 1. 源码位置与可复查版本

临时根目录：`D:\Temp\NarutoVR-GlyphResearch-20261031-032008`，其中 `README_RESEARCH_ZH.md` 记录拉取范围。

| 仓库 | 本机子目录 | 本轮固定提交 | 已核用途与边界 |
|---|---|---|---|
| [gillianassi/GestureRecognition](https://github.com/gillianassi/GestureRecognition) | `GestureRecognition` | `98949a74ce70f47b8e07c74f205d406d0d2916c5` | Quest 2 手追踪毕业项目；Unity 2021.3.15f1、旧 Oculus Integration；参考采点、投影、姿态与轨迹组合方式 |
| [DaVikingCode/PDollar-Unity](https://github.com/DaVikingCode/PDollar-Unity) | `PDollar-Unity` | `9ddc4a91abcacfed7b7f4504ad97147167c30eb7` | 二维 `$P` 模板匹配 Unity 适配；此提交工程版本为 2022.3.49f1；优先考虑提取小型算法模块 |

两者使用浅克隆、部分对象下载与稀疏检出，源码、模板和项目配置已落盘；大型 Oculus 包、场景及美术资源没有完整检出。它们不是已经在当前 Unity 6 工程运行过的依赖。临时目录不作为项目运行依赖，删除前需保留后续有价值的审阅和自采模板。

本项目审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd` **加当前未提交工作区**。腕饰和投掷等新代码包含未跟踪文件，不能仅审查 HEAD 后声称覆盖了当前工程。后续审阅者应重新记录工作区状态、相关文件哈希或本地快照；行号只作本轮导航。

## 2. 实际源码发现

### 2.1 Quest 示例如何实现

- `BaseMovementDetector` 从手/控制器的 Transform 采集三维位置，默认移动超过 0.005 米才追加点；控制器用扳机，手部路径通过 `StartGestureMovement/EndGestureMovement` 入口控制。它没有在这一层完整检查追踪新鲜度、逐关节有效性或本项目的输入占用。[源码](https://github.com/gillianassi/GestureRecognition/blob/98949a74ce70f47b8e07c74f205d406d0d2916c5/Assets/Game/Scripts/GestureDetection/BaseMovementDetector.cs)
- `PointToPlaneConverter` 在转换时使用当时的相机矩阵，把收集的世界位置投到正面、侧面、地面或屏幕坐标。这里的 Camera-Based 是**坐标投影**，不是读取摄像头图像做视觉识别；头显最终朝向会影响投影结果。[源码](https://github.com/gillianassi/GestureRecognition/blob/98949a74ce70f47b8e07c74f205d406d0d2916c5/Assets/Game/Scripts/GestureDetection/PointToPlaneConverter.cs)
- `CameraBasedStrokeGestureDetector` 读取 XML 模板，用 `$P` 分数和 AND/OR/XOR 组合判定，输出 `IActiveState` 与 UnityEvent；默认阈值 0.8 只是该示例参数，不能照搬为本项目准确率。[源码](https://github.com/gillianassi/GestureRecognition/blob/98949a74ce70f47b8e07c74f205d406d0d2916c5/Assets/Game/Scripts/GestureDetection/CameraBasedStrokeGestureDetector.cs)
- 采样器把可变 `List<Vector3>` 直接交给事件，转换器保存该引用；下一次采样又会清空列表。若直接用这条链路累积多笔，存在旧笔画被改写的风险，需要复制为独立快照，不能直接复用示例生命周期。
- 此提交未发现覆盖自写项目代码的根级 LICENSE；仓库内 Oculus/第三方许可不能代替整个项目的许可。**目前参考设计，不复制整套代码或资产进工程。**

### 2.2 `$P` 适合什么

- `Gesture` 先等比缩放、平移，再沿笔画长度重采样为 **32 点**；`PointCloudRecognizer` 做无序点云近邻匹配。适合少量符号的最终形状；不适合把“同形不同笔顺/方向”当成不同咒符。此版本没有任意旋转归一化步骤。[归一化源码](https://github.com/DaVikingCode/PDollar-Unity/blob/9ddc4a91abcacfed7b7f4504ad97147167c30eb7/Assets/PDollar/Scripts/Gesture.cs)
- 返回分数为 `max(1 - minDistance / 2, 0)`，是相似度而非识别正确的概率。原 `Result` 仅有类别和分数，未提供第二候选、拒识原因或校准置信度。[分类源码](https://github.com/DaVikingCode/PDollar-Unity/blob/9ddc4a91abcacfed7b7f4504ad97147167c30eb7/Assets/PDollar/Scripts/PointCloudRecognizer.cs)
- 分类器会在给定模板中挑最近项；必须另外实现绝对阈值、**不同符号类别**间的分差、轨迹质量门槛。第二候选不能误取同一符号的另一个模板，否则会把正常多模板样本误判为歧义。
- 空数组、零尺寸、零路径长度、非有限坐标、错误笔画边界需要入口拒绝；原代码有直接访问 `points[0]`、除以尺寸/路径间隔的路径，不应把未经验证的追踪数据直接传入。
- 仓库有 BSD-3-Clause 许可，算法文件另含原作者许可头；若后续采用，固定来源版本并保留两部分通知。当前尚未导入。[许可](https://github.com/DaVikingCode/PDollar-Unity/blob/9ddc4a91abcacfed7b7f4504ad97147167c30eb7/LICENSE)

**建议选择：自有采样/交互层＋固定平面投影＋隔离封装的 `$P` 分类器。** Quest 示例作为接线参考；先不引入神经网络、摄像头图像识别或完整旧 Oculus 工程。之前的 `$1/$P/$Q` 比较继续保留在[资料核查](ROGUELITE_GLYPH_LOCOMOTION_SOURCES_ZH.md)，本轮把 `$P` 作为首个可比较基线，不承诺其真机准确率或耗时。

## 3. 当前工程接入约束

| 当前代码入口 | 已核行为 | 画符接入必须处理 |
|---|---|---|
| [SkillDefinition](../Assets/SealCombat/Runtime/Core/SkillDefinition.cs) | 只有纯结印、纯咒语、咒语＋单印三个模式；校验用 `!= GestureOnly/VoiceOnly` 判断字段 | 当前保留三模式；未来确有配方扩展需求时才一起迁移校验/匹配，保留旧枚举值 |
| [SkillMatchEngine](../Assets/SealCombat/Runtime/Core/SkillMatchEngine.cs) | 骨骼历史、单印保持、语音窗口、Epoch、取消屏障和消费一次规则 | 产生独立会话事件，不伪装 `sealName`；首版不接战斗匹配器，不要求左手有效 |
| [SkillMatchSettings](../Assets/SealCombat/Runtime/Core/SkillMatchSettings.cs) | 默认 10 秒/640 帧缓存，最大语音延迟 2 秒 | 640 槽不保证高帧率下有完整 10 秒；画符使用独立会话/有界点集，不受战斗缓冲时长限制 |
| [LinearRpgController](../Assets/SealCombat/Runtime/Rpg/LinearRpgController.cs) | 负责 UI/导航/技能仲裁；`TryActivate` 经解锁、MP/CD 和效果创建事务；UI 占用切换会清输入 | 由这里协调持笔模式和攻击阻断；战斗外交互独立提交，识别器不能直接生成攻击物 |
| [WristMenuController](../Assets/SealCombat/Runtime/Rpg/WristMenuController.cs) | 画符只是预览卡；默认放左腕关闭；教学筛选仍假设结印/语音二分 | 入口改由笔抓取提供；教学卡、画布、工具会话分别管理；将来扩配方才修改 `Eligible` |
| [SkillTestController](../Assets/SealCombat/Runtime/SkillTestController.cs) | `ExclusiveInputBlocked` 会在识别取消口令前拦截语音 | 不能把“绘制中”直接塞入这个布尔量，否则取消口令也失效；需单独处理工具退出/仪式命令，保持战斗语音隔离 |
| [RpgGestureState](../Assets/SealCombat/Runtime/Locomotion/RpgGestureState.cs) | 低位双手取消先于 `blockActions` 执行 | 绘制中不能仅传 `blockActions=true` 以为隔离完成；跳过移动识别时又必须提供独立取消入口 |
| [SkillEffects](../Assets/SealCombat/Runtime/Effects/SkillEffects.cs) | 现有松手投掷、动作评分、伸手保持；`ResetPushInput` 实际会重置运动历史与准备时间 | 持笔期间禁止攻击物生成与释放；放笔后不得继承笔画速度、握持或伸手进度 |

时序现状：`SealLab` 执行顺序 200、`LinearRpgController` 300、`SkillEffects` 350、`SkillTestController` 400。未来 owner/采样/识别事件的提交阶段必须明确，防止同帧既画符又被其他状态机消费。仅调整脚本顺序不等于解决输入冲突。

## 4. 战斗外拿笔交互（当前方案）

首个原型验证 **2–3 个形状差异明显的符号**，使用头显录制模板。建议先用练习画板/非攻击性交互验收；开机关、解封、仪式或制作符纸是候选用途，尚未选定。不再把画符后立即生成火球作为第一阶段验收。

1. 玩家拿起画符笔，抓取成功后进入专用模式。腕部菜单保留教学/工具提示，不是进入画符的必经步骤；空手动作不会开启绘制。
2. 协调器先确认战斗外条件、可操作状态及工具占用，再原子切换输入。已有未发射技能、已提交移动、死亡/暂停时建议拒绝抓取接管并显示原因；不能先给玩家拿笔又让攻击继续运行。“战斗外”需要关卡/遭遇状态依据，不能仅用“附近没怪”或当前 `CanAct` 冒充，现有主控尚未提供完整战斗阶段接口。
3. 成功进入时结束旧的 UI 点击与未提交导航，清待施法手印、口令和投掷动作证据。采点改为**笔尖**；抓取动作不同时当作画线或火球握持。
4. 建议在明确画板平面上用笔尖接近/接触落笔、离开抬笔。抓住笔期间保持工具所有权；松开抓握意味着放下工具，不兼任“完成本笔”。若采用空中绘制，另选落笔动作后实测，不能复用松开笔来分笔。
5. 允许慢画、停顿和多笔，提供撤销上一笔、清空、完成、退出；用明确“完成”提交识别。原型可以先用单笔模板，但不把松笔或普通停顿当作整符自动激活。不要求 5–10 秒内画完。
6. 识别结果先展示符号与失败原因；确认后提交目标交互。成功或失败都可继续持笔重画；**识别完成不自动退出画符模式，也不自动恢复攻击**。放回/放下笔或明确退出才开始交还输入。

建议状态：`未持笔 → 抓取受理/输入接管 → 持笔待落笔 ↔ 绘制中 → 完成检查/结果确认 → 持笔待落笔 → 放笔退出/重新准备 → 未持笔`。提交事件有唯一 ID，避免重复完成导致重复奖励或扣费。

画布平面在会话中固定，笔尖三维位置投影到其局部二维坐标；保留深度偏离与追踪有效性。头转动不改变已画轨迹。允许笔画间休息和查看教学，画布不要求左腕一直抬起；双手放低默认暂停落笔而不是清空整个画作，以免休息被当成战斗取消。

失追时停止采点、停止确认，不跨缺口补线，也不凭一帧失追把笔视为放下并恢复攻击。短暂恢复需重新建立有效笔尖；长时间失追、工具禁用/销毁、场景切换等走统一退出/清理，不能留下永久输入锁。放笔、退出、恢复战斗均要求新的有效动作基线，不重放拿笔前的施法、投掷或导航证据。

绘制中遭遇战斗、受击或强制位移的具体产品表现待确定；建议中止绘制并清理工具占用，再按新动作恢复战斗。该状态迁移本身不触发攻击/闪避。进入画符不暂停全局时间，未来多人也不冻结其他玩家。

## 5. 冲突处理与禁止攻击的范围

| 既有动作/事件 | 持笔画符模式中的行为 | 退出边界 |
|---|---|---|
| 双手结印 | 不累计或提交可激活的手印证据，原始数据可用于 Debug | 放笔后必须重新结印 |
| 攻击与攻击口令 | 禁止攻击技能激活、攻击物生成和释放；普通战斗口令不缓存待放笔补发 | 攻击事务入口和效果释放入口都检查，不能仅隐藏 UI 或停一个分类器 |
| 火球握持/甩手/伸手保持 | 不能把笔当作火球；已有未发射技能时建议拒绝进入；持笔中停止更新攻击动作历史 | 放笔后清握持边沿、速度和评分，新的攻击需要新动作 |
| 左食指选点、右掌闪避 | 建议持笔期间暂停这两种战斗动作，避免画线误移动；物理头部/身体跟踪不冻结 | 清旧驻留，放笔后重新准备；不补发先前移动 |
| 腕部教学卡与画板按钮 | 可阅读；抬笔且未绘制时才切到受控 UI 点按；同一动作不同时落笔/点按 | 教学卡显示不改变笔的所有权，放左腕不退出画符 |
| 撤销、清空、完成、退出 | 是画符会话自己的命令；完成只提交一次，退出才交还工具占用 | 可另设明确语音命令；不能默认把当前战斗取消词当成关闭所有 UI |
| 追踪失效/工具销毁 | 停采样/确认；经明确生命周期退出，禁止凭失效模拟松手攻击 | 恢复后须有新有效输入，既不永久锁死也不瞬时放行 |
| 战斗开始/强制位移/暂停/死亡 | 建议中止会话，清理画布/工具交互并按对应状态恢复 | 不跨坐标变化接着画；不因拿笔取消既成伤害、清已生效护盾或召回飞行物 |

核心约束：**持笔模式内不能结印或攻击；禁止从 UI、语音、旧缓冲或同帧顺序绕过。** 既有已飞出的投射物仍完成其生命周期，这与玩家在绘制中发起新攻击不同。暂不改整个输入框架；在现有协调器加明确模式，工具层报告抓取/释放/失效，模式统一决定各入口是否可用。模式可见性、工具所有权与某张面板是否显示分别管理。

## 6. 识别与技能数据边界

下列名称是待实现接口建议，均不表示已有类型：

| 数据/模块 | 负责 | 不负责 |
|---|---|---|
| `GlyphStrokeSnapshot` | 独立复制点数组；session/笔画 ID、单调时间、局部平面坐标、深度、有效性、采样间隔；录制可选保留原始三维点与参考空间版本 | Unity 手模型 Transform 引用、技能资源和效果 |
| `GlyphTemplateSet` | 稳定符号 ID、多次真人模板、算法/归一化版本、绝对阈值与跨类别分差、质量门槛 | 把模型分数当概率；自动把失败笔画存成正例 |
| `GlyphRecognizer` | 输入已冻结的轨迹，返回各类别候选、原始距离/相似度、质量结果、拒识原因；纯逻辑可回放测试 | 全局输入抢占、播放声音、扣 MP 或生成技能 |
| `GlyphRecognized` | 唯一事件 ID、符号 ID、起止时间、来源手、会话/取消代次、分数与质量摘要 | 持续每帧重复发一个命中事件 |
| 绘制会话协调/展示 | 获取采样、绘线、独占生命周期与取消、中文提示；提交匹配事件 | 在 View 内绕过匹配器直接施法 |

按现有手部快照入口获取关节和有效性；不把旧示例的 `OVRHand`、`Camera.main` 或 `IActiveState` 暴露为画符算法的公共依赖。现有 XR Hands 路径仍有 Meta 类型依赖，不能据此声称 PICO 已兼容。以后 PICO 需要验证捏合阈值、采样时钟和追踪质量；MR 需要校准安全画布位置/空间锚点；本轮不实现这些平台能力。

后续同房间 PVE 可本地识别后提交交互请求，由权威端验目标、权限、资源与结果；画线视觉与交互结果分别同步。持笔仅占用本玩家输入，不暂停全房间。识别分数不是服务器防作弊凭证。现在只保持边界，不安装网络包或传每帧完整骨骼。

### 结果接入：战斗外交互优先，技能复用按需要

先形成 `GlyphRecognized` 结果，由战斗外交互处理器校验目标、距离/状态、解锁条件及唯一提交 ID，再执行练习反馈、机关或其他已配置用途。画符成功不能直接调用 `SkillEffects` 生成攻击物。通用识别器只负责形状与质量，不负责奖励、扣费或战斗阶段。

若后续选定制作符纸、解锁法术或战前加持，可复用玩家数据、效果配置或资源事务，是否消耗 MP/CD 由具体交互决定，不统一强制套战斗配方。允许的非攻击结果也需明确白名单，不能通过通用技能入口绕过“持笔禁止攻击”。现有三种战斗触发模式暂不增加 Glyph 枚举，GLY-05 不以前置重构整个 `SkillMatchEngine` 为目标。

咒语＋画符属于可选的战斗外仪式扩展：建议在同一个显式仪式会话内保存步骤结果，不复用战斗 5–10 秒缓冲限制完整绘制时长；仍检查语音新鲜度、会话归属、取消屏障及消费一次。仪式模式不能接收战斗攻击口令，不移入拿笔前的战斗证据。未来真有技能配方扩展需求时，再同时迁移验证器、匹配器、快照、词表导出和教学筛选，保持旧枚举序列化值。

## 7. 待办与验收顺序

以下是设计安排，画符仍为 P2，现有交互问题优先。旧审阅中的 GLY 编号保留，但实施范围以本节最新描述为准。

- [x] **GLY-R01 / P2**：临时拉取两仓库，记录固定提交、采样/投影/分类与许可。
- [x] **GLY-R02 / P2**：源码接入审阅、待办文档及用户确认的战斗外拿笔模式更新。
- [ ] **INT-VERIFY / P0（有现有问题时）**：优先复现修复现有移动、结印、投掷、UI，之后再恢复画符。
- [ ] **GLY-01 / P2**：确认笔抓取/放回方式、画板位置与第一种用途；区分已确认规则与候选设计。
- [ ] **GLY-02 / P2**：隔离 `$P`、许可和模板；非法点集拒绝、跨类别前二候选与质量门槛。先单笔样本，再多笔模板。
- [ ] **GLY-03 / P2**：笔抓取、持笔、放笔/失追生命周期与统一模式；持笔不能结印或攻击，语音/UI/旧缓冲也不能绕过。验证暂停、工具销毁、切场景无永久输入锁。
- [ ] **GLY-04 / P2，依赖 02/03**：独立战斗外画板测试场景；慢画、停顿、分笔、撤销/清空/完成；中文 UI/Inspector 和录制回放。抬笔、休息和完成都不退出持笔模式。
- [ ] **GLY-05 / P2，依赖 04**：接一个确定的非攻击性交互；一次确认只产生一次结果，拒绝不消耗资源。验证放笔后没有残留施法、甩手或闪避，不以生成火球验收画符。
- [ ] **GLY-06 / P2（可选，依赖用途确定）**：战斗外咒语＋画符仪式步骤与独立会话；不要求快速组合，不接受战斗攻击口令。
- [ ] **GLY-07 / P2**：真人验证笔尖稳定、画板舒适性、完成/退出可用性及正负例；通过后接探索流程。MR/PICO/多人后续独立验收。

Debug/录制至少包含：战斗外准入依据、工具/持有人、模式与阶段、笔尖有效性/时间、笔画 ID、有效点数/路径长度/尺寸/间隔/深度偏离、前二类别与分差、拒识原因、提交 ID 与消费状态、进入/退出原因、被阻止的攻击来源。日志按点数/大小限额保存，不用很短的操作倒计时逼玩家画完。

真人评估包括正例、形似反例、随手涂画、持笔休息、普通点按、放笔与失追恢复；独立轮次/人员划分标定与验证。核心验收是清晰可控、可修改、零攻击串扰和无输入锁；同时记录结束到反馈的延迟与耗时，但不要求战斗反射速度。没有以文档代替实机验收。

## 8. 供其他 AI 追加的审阅区

请追加新条目，保留先前审阅与结论；若发现主文错误，在条目中指出章节和替代建议，经确认后再修订主文并记录关联审阅号。审阅不等于批准修改运行代码。现有未提交工作不要 reset、清理或覆盖。

优先核查：取消口令是否被 owner 拦住；入场捏合是否串到笔画；结束笔画是否串到闪避/火球评分；放腕是否误清等待语音；无效数据是否被当作放笔；旧资产模式校验/语音词表/教学卡是否漏迁移；算法许可和未知负例拒识是否完整。

每个问题建议独立编号；严重度用 P0/P1/P2，区分“源码确认”“推测待复现”“设计建议”。记录实际源码路径/行号/方法、复现输入及预期和实际；文档讨论不填写“测试通过”。临时仓库找不到时按第 1 节地址和 SHA 重建，不把本机路径当永久依赖。

```markdown
### REVIEW-YYYYMMDD-作者-01
- 作者/模型与日期：
- 审阅基线：Git HEAD、是否含未提交文件、关键文件哈希/快照位置
- 覆盖范围：文件、方法、本文章节；未覆盖哪些部分
- 类型与优先级：源码确认 / 待复现 / 设计建议；P0/P1/P2
- 问题与证据：路径:行号、相关源码行为或录制日志
- 触发步骤：输入序列、预期结果、实际结果
- 建议与影响：最小改动，是否改变已确定操作/配置/平台边界
- 验证：实际运行的检查及结果；未运行则写“未运行”
- 关联待办/旧审阅：GLY-xx / REVIEW-...
- 决策：待确认（由后续负责人追加采纳/拒绝及原因）
```

### 追加记录

### REVIEW-20261031-Antigravity-01
- 作者/模型与日期：Antigravity (Gemini 2.5 Pro) / 2026-10-31
- 审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd`，含大量未提交工作区变更（官方基础手形、腕饰 UI、评分投掷），`SkillTestController.cs` 本地未提交状态
- 覆盖范围：`Assets/SealCombat/Runtime/SkillTestController.cs:149-172` (`ReceiveVoice`, `ProcessVoice`)；涉及本文第 5 节“输入独占”与第 7 节 GLY-03
- 类型与优先级：源码确认；P0（阻断级缺陷）
- 问题与证据：
  在 `SkillTestController.cs` 第 152 行与第 162 行：
  `if((ExclusiveInputBlocked?.Invoke()??false)||endedAt<=voiceNotBefore)return;`
  该前置拦截位于处理取消口令 `if(SkillPhrase.Normalize(phrase)==Engine.CancelPhrase)`（第 164 行）之前。当外部输入独占生效（如腕饰打开 `UiInputOwned=true`、手持物交互或后续画符占用 `ExclusiveInputBlocked` 返回 true）时，玩家口述的全局取消口令（“解除”、“取消施法”）在入口处被直接 `return` 吞没，导致取消逻辑完全无法被触发与消费。
- 触发步骤：
  1. 玩家抬起左腕使腕饰展开，或进入画符状态（`ExclusiveInputBlocked` 返回 true）；
  2. 玩家口述“取消施法”试图重置状态或关闭交互；
  3. 预期：全局取消口令属于最高优先级指令，无条件穿透并生效；
  4. 实际：被第 152/162 行直接短路拦截，计数不增加，取消口令失效。
- 建议与影响：
  在 `ReceiveVoice` 与 `ProcessVoice` 中，将取消短语判定提到 `ExclusiveInputBlocked` 检查之前，或在独占逻辑中豁免 `CancelPhrase`（第 171 行已有对普通技能口令的 `VoiceInputBlocked` 独立守卫）。不改变现有操作边界与平台逻辑。
- 验证：源码与调用链已确认；通过 Editor 运行期测试验证确认了调用依赖关系。
- 关联待办/旧审阅：GLY-03
- 决策：待确认

### REVIEW-20261031-Antigravity-02
- 作者/模型与日期：Antigravity (Gemini 2.5 Pro) / 2026-10-31
- 审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd`，含工作区未提交文件
- 覆盖范围：`Assets/SealCombat/Runtime/Core/SkillDefinition.cs:66-67`、`WristMenuController.cs:244`；涉及本文第 3 节与第 7 节 GLY-02、GLY-05
- 类型与优先级：源码确认；P0（架构扩展与配置约束阻断）
- 问题与证据：
  现有技能校验在 `SkillDefinition.cs` 第 66~67 行采用了二分排他式负向校验：
  `if (triggerMode != SkillTriggerMode.GestureOnly && string.IsNullOrWhiteSpace(phrase)) return "缺少完整口令";`
  `if (triggerMode != SkillTriggerMode.VoiceOnly && string.IsNullOrWhiteSpace(sealName)) return "缺少单印名称";`
  同时 `WristMenuController.cs:244`：
  `bool Eligible(SkillDefinition skill) => skill && (PanelKind == WristPanelKind.Seal ? skill.triggerMode != SkillTriggerMode.VoiceOnly : skill.triggerMode != SkillTriggerMode.GestureOnly);`
  现有枚举仅有 `VoiceAndGesture(0), VoiceOnly(1), GestureOnly(2)`。若按 GLY-02 新增 `GlyphOnly(3)` 或 `VoiceAndGlyph(4)`，现有校验会因为 `triggerMode != VoiceOnly` 强行要求所有纯画符技能填写 `sealName`（单印名称），直接校验报错阻止加载；且腕饰结印教学会把画符技能误当作“结印技能”显示。
- 触发步骤：
  1. 按照 GLY-02 规划添加纯画符技能配置 Asset；
  2. 运行配置校验或场景加载；
  3. 预期：纯画符技能无需 `sealName` 正常通过校验，且不混入结印教学；
  4. 实际：报“缺少单印名称”错误，校验失败拒绝注册。
- 建议与影响：
  重构 `SkillDefinition.ValidateDefinition()` 与 `WristMenuController.Eligible()` 的判断逻辑，由“负向排除（`!=`）”改为“显式正向能力判断（如 `RequiresSeal(mode)`、`RequiresVoice(mode)`、`RequiresGlyph(mode)`）”。
- 验证：源码与逻辑已完全确认。
- 关联待办/旧审阅：GLY-02, GLY-05
- 决策：待确认

### REVIEW-20261031-Antigravity-03
- 作者/模型与日期：Antigravity (Gemini 2.5 Pro) / 2026-10-31
- 审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd`，含工作区未提交文件
- 覆盖范围：`Assets/SealCombat/Runtime/Locomotion/NavigationHandInput.cs:24`、`RpgGestureState.cs:229-236`；涉及本文第 5 节“结束笔画是否串到闪避”与第 7 节 GLY-03
- 类型与优先级：源码确认 / 待复现；P1（高概率交互手感与误触发陷阱）
- 问题与证据：
  在 `NavigationHandInput.cs` 第 24 行：`requireNavigationForDodge` 默认配置为 `false`（允许右掌独立短划闪避，无需左手开启导航准备）。而在 `RpgGestureState.cs` 第 229~236 行中，只要右手在 0.06s~0.45s 内位移超 0.1m 且为张掌即可判定闪避。
  在手部画符交互中，右手绘制完成最后一笔捏合松开时，右手手指会自然舒展张开，并在收手归位时产生 0.1m 以上的自然位移。如果画符状态在松手同帧直接释放独占所有权，该收手位移会在无导航保护下被 `ProcessRight` 立即识别为“右掌短划闪避”，造成画符结束角色突然瞬移乱窜。
- 触发步骤：
  1. 玩家右手食指捏合画符，完成最后一笔；
  2. 松开捏合收手（手掌自然张开并向后/右侧微移 10cm）；
  3. 预期：画符结束并提交识别，角色原地保持不动；
  4. 实际：触发右手短划，瞬移闪避激活。
- 建议与影响：
  在画符退出阶段（`ReleaseGlyphSession`）提供明确的过渡冷却（如 0.25 秒的位移吸收期 `SuppressLocomotionUntil`），或者在独占退出同帧显式调用 `locomotion.ResetInput()` 清空右手位移累积。
- 验证：源码调用链路已确认；已纳入自动化防串扰验证用例建议。
- 关联待办/旧审阅：GLY-03
- 决策：待确认

### REVIEW-20261031-Antigravity-04
- 作者/模型与日期：Antigravity (Gemini 2.5 Pro) / 2026-10-31
- 审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd`，含工作区未提交文件
- 覆盖范围：`Assets/SealCombat/Runtime/Rpg/LinearRpgController.cs:149-153` (`LateUpdate`)；涉及本文第 5 节“放腕是否误清等待语音”
- 类型与优先级：源码确认；P1（操作流断裂缺陷）
- 问题与证据：
  在 `LinearRpgController.cs` 第 149~153 行：
  ```csharp
  if (UiInputOwned != wasUi)
  {
      skills.CancelInputOnly(); skills.InvalidateEarlierVoice(); locomotion.ResetInput();
      wasUi = UiInputOwned;
  }
  ```
  每当 `UiInputOwned` 状态发生跳变（无论是由打开到关闭，还是由关闭到打开），均会无差别执行 `skills.InvalidateEarlierVoice()`。这导致只要玩家在战斗中念了咒语（技能窗口缓冲期内），中途因为低头/瞥一眼腕部教学或触发 UI 所有权切换，之前的有效语音证据被立即强行抹除，无法与后续动作组合。
- 触发步骤：
  1. 玩家口述咒语（处于 8 秒匹配有效期内）；
  2. 玩家抬腕瞄了一眼腕饰卡片（`UiInputOwned` 变为 true）；
  3. 玩家放下手腕退出 UI；
  4. 玩家画符或结印；
  5. 预期：口令与后续印/符正常完成时序拼接并释放；
  6. 实际：语音证据在步骤 2 和步骤 3 中被两次抹除，施法失败。
- 建议与影响：
  `InvalidateEarlierVoice()` 应当仅在明确的“用户主动取消”（如低手位取消、口述取消指令）时触发，UI 面板开关只暂停指尖射线/UI 占有，不应等同于丢弃战斗语音上下文。
- 验证：源码与逻辑已完全确认。
- 关联待办/旧审阅：GLY-03, GLY-06
- 决策：待确认

### REVIEW-20261031-Antigravity-05
- 作者/模型与日期：Antigravity (Gemini 2.5 Pro) / 2026-10-31
- 审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd`，含工作区未提交文件
- 覆盖范围：`Assets/SealCombat/Runtime/Effects/SkillEffects.cs:342-346` (`UpdateHeldObject`)；涉及投掷手感与手形抖动
- 类型与优先级：源码确认；P1（手感崩溃与丢球缺陷）
- 问题与证据：
  在 `SkillEffects.cs` 第 342~346 行：
  ```csharp
  if (!double.IsFinite(grip.observed) || Math.Abs(grip.observed-observed) > .01)
  {
      ResetMotion(now);
      ObserveThrow("sample_skew","手形与腕骨不在同一观测帧",wrist,grip,observed,true); return;
  }
  ```
  此处对手腕位置的时间戳 `observed` 与手部卷曲度采样的时间戳 `grip.observed` 设置了极度严苛的 `0.01` 秒（10 毫秒）同步容忍度。在 VR 真实运行环境（72Hz 帧间隔约 13.8ms，90Hz 帧间隔约 11.1ms，或跨子系统异步上报时），两帧采样时间差极易超出 10ms。一旦超出，第 344 行无条件执行 `ResetMotion(now)`，将玩家积攒的蓄力速度、平滑轨迹瞬间全部清空归零，造成火球在手中“卡死无法丢出”。
- 触发步骤：
  1. 生成手持火球并挥臂加速；
  2. 在投掷出手瞬间遇到轻微帧率波动或多线程时间戳微弱微秒偏差（>10ms）；
  3. 预期：继续利用上一有效帧或插值完成出手测速；
  4. 实际：触发 `sample_skew`，`ResetMotion` 导致初速度归零，球掉在原地。
- 建议与影响：
  放宽采样时差容忍度，与 `RpgGestureSettings.sealMaximumSkew`（80ms）保持一致（建议调整为 `0.05f ~ 0.08f`），并在轻微跳帧时使用上一帧有效手形或平滑插值，避免直接硬重置投掷状态机。
- 验证：源码与逻辑已完全确认。
- 关联待办/旧审阅：GLY-03
- 决策：待确认

### REVIEW-20261031-Antigravity-06
- 作者/模型与日期：Antigravity (Gemini 2.5 Pro) / 2026-10-31
- 审阅基线：外部调研仓库 `D:\Temp\NarutoVR-GlyphResearch-20261031-032008` 中的 `$P` 算法实现 (`Gesture.cs`, `PointCloudRecognizer.cs`)
- 覆盖范围：`PDollar/Scripts/Gesture.cs:107-109`、`PointCloudRecognizer.cs:80-95`；涉及本文第 1 节算法评估与第 6 节
- 类型与优先级：源码确认 / 设计建议；P2（算法健壮性与负例防御）
- 问题与证据：
  1. `Gesture.cs:107-109` 在归一化缩放时：
     `float scale = Math.Max(maxx - minx, maxy - miny);`
     `newPoints[i] = new Point((points[i].X - minx) / scale, ...)`
     若玩家误触产生原地点击或极小点集（`maxx == minx` 且 `maxy == miny`），`scale == 0` 会直接引发除以零，导致所有点坐标产生 `float.NaN`，导致后续整个比对流程崩溃。
  2. `PointCloudRecognizer.cs:80-95` 原版算法采用“全集贪婪最小距离遍历”，只要模板集非空，无论用户画出多么混乱的线条或噪点，都会强行返回得分最高的一个模板，缺少绝对置信度门槛与第二候选类别的分差校验（Margin Check），无法防御非目标涂鸦。
- 触发步骤：
  1. 玩家误触屏幕/空间产生单点或两点原地捏合；
  2. 预期：算法识别为无效笔画直接拒识；
  3. 实际：产生 `NaN` 异常或将其强行识别为某种有效符文。
- 建议与影响：
  在移植/集成 `$P` 时必须做本地鲁棒性防御：
  1. 增加尺度保底保护：`if (scale < 1e-4f) return null;`（或设为无效轨迹拒绝）；
  2. 增加双重拒识门槛：不仅要求 `Score >= threshold`，还要求 `Top1_Distance - Top2_Distance >= minMargin`，否则标记为“歧义拒识”。
- 验证：源码检查完成。
- 关联待办/旧审阅：GLY-01, GLY-06
- 决策：待确认

### REVIEW-20261031-Codex-复核01

- 作者/日期：Codex 父代理 / 2026-10-31。
- 审阅基线：Git HEAD `5d8e6b47c011e5f0c5d97ef5a73fa687967a7bfd` 加当前未提交工作区；直接读取当前 Runtime、Editor 检查与已定交互文档，未仅根据 HEAD 推断。
- 范围：复核 Antigravity-01 至 06，优先核实两条 P0。此条不覆盖原审阅，不代表原作者已接受复核。
- 结论：**目前这份审阅没有证实当前工程存在 P0。** 第一条是可讨论的现有交互策略，第二条是未来扩展约束。不能为清掉 P0 标签而修改现有取消隔离或提前实现画符。
- 本轮变更：仅追加本复核，未修改 Runtime、Editor 检查、场景、配置或 SDK。现有交互的真机问题仍优先于画符；未发现 P0 不等于证明所有交互没有缺陷。

#### Antigravity-01：拦截事实成立，阻断级缺陷与关闭 UI 的预期未成立

`SkillTestController.ReceiveVoice/ProcessVoice` 确实会在 UI 独占时拦截取消口令。但[腕饰设计第 8 节接线记录](WRIST_MENU_INTERACTION_DESIGN_ZH.md)明确记录了“阻挡 UI 期间的取消口令及交接前结束的迟到事件”；[现有时钟检查](../Assets/SealCombat/Editor/SkillClockRegressionChecks.cs)也专门断言该行为。这是当前实现的策略，不是此次审阅首次发现的漏写分支。

当前 `LinearRpgController` 只把 `UiInputOwned` 接到 `ExclusiveInputBlocked`；持球会硬阻止打开交互菜单，不是持球就把 Exclusive 设为 true。画符尚不存在。抬腕本身也不是当前默认打开动作，默认是右指点腕。只读卡可见不等于 UI 持续占用。

该报告没有给出当前玩家被卡死、未发射技能无法取消或菜单无法通过既定动作关闭的复现。UI 获得输入时本来就清理未提交技能证据，且“取消施法”“关闭菜单”在设计中是不同命令；即使取消口令放行，现有 `CancelCasting` 也不会因此关闭菜单。

**处置：不按 P0 热修；保留为取消策略改进建议。** 若以后明确要求战斗中的有效取消口令穿透菜单，应只豁免对应交互占用，继续保留时间有效性、去重、取消屏障、暂停/生命周期和示范音隔离；是否关面板单独定义。不能按报告的“无条件穿透”实现，也不能只改一处入口而遗漏队列消费。

#### Antigravity-02：未来扩展风险成立，当前 P0 不成立，枚举值有误

[SkillDefinition](../Assets/SealCombat/Runtime/Core/SkillDefinition.cs) 实际顺序为 **GestureOnly=0、VoiceOnly=1、VoiceAndGesture=2**，报告把 0 与 2 写反。当前没有 GlyphOnly/VoiceAndGlyph；未知枚举首先被 `Enum.IsDefined` 拒绝。现有三个模式的 `phrase/sealName` 必填约束是正确的，纯画符资产尚不存在，不能描述成现在已发生的加载阻断。

显式 `RequiresSeal/RequiresVoice` 能改善后续扩展，但只有把匹配器、快照、验证器、词表导出、教学筛选一起迁移才算接入；只把 `!=` 换成正向条件并不能完成画符。若照错误枚举顺序调整代码，会改变已序列化技能资产的含义。

**处置：归入 GLY-05 的 P2 依赖，保留建议，不现在增加模式或迁移资产。**

#### Antigravity-03：合理的未来防串扰用例，不能称为已复现缺陷

独立右掌闪避确实需要防止消费绘制收手，但当前没有画符会话或报告中的 `ReleaseGlyphSession`，因此给出的画符结束“实际触发闪避”不是当前工程复现。现有右手识别还检查有效性、路径效率、幅度/时长、垂直移动、跳变、动作占用及重新准备，不是仅张掌＋10 厘米两个条件。

**处置：保留 GLY-03 验收用例。** 单次 `ResetInput` 或固定 0.25 秒不是充分保证；还要防止其后连续收手重新形成动作，以新意图/中立状态交接并用轨迹回放验收。

#### Antigravity-04：清理行为成立，触发叙述与建议不完整

当前 UI owner 的进入/退出确实同时执行 `CancelInputOnly` 与 `InvalidateEarlierVoice`，这是已记录的输入隔离策略。“抬腕瞥一眼只读卡”不必然导致 owner 切换。仅移除 `InvalidateEarlierVoice` 仍会被 `CancelInputOnly → Engine.Cancel → ClearContext` 清理证据，不能得到报告声称的跨 UI 组合能力，反而可能破坏迟到输入隔离。

**处置：保留为 GLY-06 的定向证据交接设计，当前不删除屏障。** 需分别处理面板可见、主动操作、进入绘制、全局取消，而不是把所有 UI 切换都改成保留旧语音。

#### Antigravity-05：可观察的保护分支，但帧率推导不能证明根因

当前正常接线中，[LinearRpgController.ReadThrowGrip](../Assets/SealCombat/Runtime/Rpg/LinearRpgController.cs) 将握持时间戳设为 `skills.lab.Frame` 对应手的 `observedAt`；[SkillEffects.TryGetWrist](../Assets/SealCombat/Runtime/Effects/SkillEffects.cs) 也使用同一骨骼帧的手腕 `observedAt`。握持采样不是自动使用下一渲染帧的时间。因此 72Hz/90Hz 帧间隔大于 10ms 不能证明两个字段正常会相差一帧；左右手最大时差也不能直接作为同一只手的两份观测容差。

读取现有 `Logs/throw-captures/throw-score-20261030-190213-054-bede10bf.jsonl` 与 `throw-score-20261030-191046-548-c2f93be9.jsonl`：分别 9/11 条 `sample_skew`，**全部 `source=测试注入` 且握持 `observed=0`**。这些记录确实触发了保护，但不支持“真机轻微掉帧导致 10–80ms 偏差”的结论。没有把文件名为 captures 当成真人证据。

`ResetMotion` 清运动历史与准备状态，但该分支不会直接发射/让球掉地；“卡住”和“掉地”需要分别复现。真正数据错配时盲目放宽或复用旧握持可能把松手和挥臂错配，重新引入乱飞。

**处置：暂不改 10ms 阈值。** 若真人日志出现这一事件，先核对来源、有效握持、两份时间戳及 provider/执行阶段，区分缺失样本、测试注入与真实相位偏差，再决定修复同帧绑定还是做有界对齐。这不否定仍可能有需要排查的投掷手感问题。

#### Antigravity-06：采纳边界防御，修正分差公式

空点集、零路径/尺寸、非有限坐标，以及未知形状拒识都应纳入 GLY-02，原待办已要求。无效输入会导致非有限值或异常路径，但不能把每种非法点集一概描述为同一种必然崩溃。

报告建议的距离差方向写反：若按距离从小到大排序 `d1 <= d2`，应检查 **`d2 - d1 >= minMargin`**；若用越大越好的分数，则检查 **`score1 - score2 >= minMargin`**。还要按不同符号类别取前二项、同时检查绝对阈值和轨迹质量。仅取最近项本身不等于已经接受它，应用层应有明确拒识分支。

**处置：采纳经修正的防御要求，归入 GLY-02；未导入第三方算法，不改临时仓库源码。**

#### 本轮实际验证与边界

- 实际调用：`Tools/Request-RpgCheck.ps1 -Action clock-check`；Unity 返回 **8 项通过**，见 [clock-check.json](../Logs/linear-rpg/clock-check.json)，UTC `2026-10-30T19:59:00.3659600Z`（本地 2026-10-31）。覆盖现有控制器队列、UI 取消拦截、迟到语音拒绝和真实时钟回退。它证明现有策略被实现，不证明该策略在所有场景中最优。
- 已读取代码、现有交互约定及两份测试注入日志；未启动真人串流测试，未声称新的反例或修复回归已通过。原审阅的“运行期验证”没有随条目提供命令/结果路径，不能据此认定真人故障已复现。
- 未请求额外模型：本轮是父代理对已有外部审阅的事实、规格与处置验收；可自动化部分直接运行已有检查。
- 后续顺序：当前未确认 P0，不以重构替代排障；保留现有代码，真实交互新问题优先复现并处理，画符继续 P2。
