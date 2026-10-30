# 技术与开发文档总索引

核对日期：2026-10-30。本文汇总已经讨论的决定，并区分代码现状、设计和验证缺口。**文档中出现接口名或流程图，不代表已经实现。** 日常配置从 [README](../README.md) 开始；代码行为以当前源码、实际场景引用和验收记录共同核对。

## 1. 当前决定与落地状态

**2026-10-30 当前方向：线性关卡＋轻量 RPG。** 已确定规则、候选设计、分阶段交付与验收见 [线性 RPG 方案](LINEAR_RPG_DESIGN_ZH.md)。固定解锁、少量隐藏法术、轻量升级购买与检查点优先；暂缓随机奖励池/局内构筑/本局结算/复杂装备词条，大量 NPC 任务不属当前范围。首版全虚拟 VR；MR 后续。新 `LinearRpgTest` 三阶段切片已实现并通过 Unity 检查，**新动作尚待 Quest 真机验收**。[运行入口与配置](LINEAR_RPG_QUICKSTART_ZH.md)。旧四个训练场操作保留兼容。

| 主题 | 已确定的方向 | 当前状态与主要入口 |
|---|---|---|
| 设备与调试 | Quest 优先，当前电脑路径为 Steam Link → SteamVR OpenXR → Unity | 已有可用追踪路径；[连接说明](STEAM_LINK_DEVELOPMENT_ZH.md)。原生 Quest 手追踪曾通过对照，不等于全部技能、语音和性能已在 APK 验收 |
| 手印识别 | 实际追踪骨骼＋有效性 → SDK/双手特征 → 手动权重模板；优先少遮挡、有区分度的特征 | `Assets/SealLab` 已实现；[录制与标定](SEAL_LAB_QUICKSTART_ZH.md)、[独立识别测试](RECOGNITION_TEST_ZH.md)。分数是相似度，不是校准后的正确概率 |
| 录制与数据 | 倒计时、自动结束、带标签 JSONL、整段统计生成 SO 草稿，再人工确认 | 已实现。图片与 Excel 用作动作参考和初始权重；真实范围必须来自设备录制。子印不使用中指姿态特征；保留现有人工调参。十二印资产存在不代表十二印都已标定或测过准确率 |
| 中文口令 | sherpa-onnx KWS，PC 本机服务通过 UDP 发事件 | 已实现 PC 接入；[语音设置](SKILL_VOICE_SETUP_ZH.md)、[真人录音基线](PLAYER_AUDIO_BASELINE_ZH.md)。否定句仍可命中关键词；Android 原生语音未接入 |
| 输入与取消 | 保留单印三模式；新场景为食指导航、导航期间右掌短划与双手低位取消 | 新规则已接线并做状态机/Play检查，[真机待验收项](LINEAR_RPG_DESIGN_ZH.md)。普通取消不清已生效护盾/飞行物；画符/多印仍后续 |
| 技能与场景 | SO 定义＋纯逻辑匹配器＋效果实例＋独立中文 View | 四个单机技能场景、默认右手火球与自身护盾已实现；[场景说明](SKILL_SHOWCASES_ZH.md)、[架构验收](ARCHITECTURE_REFACTOR_QA_ZH.md) |
| 玩家数据 | 共享定义、永久成长、冒险进度、运行态分开；成功生成/加持才扣 MP/CD | `Rpg/Data` 的 HP/MP/CD、奖励和检查点已接线并验证；[当前数据边界](LINEAR_RPG_DESIGN_ZH.md)、[长期设计](PLAYER_DATA_DESIGN_ZH.md)。通用多效果事务、完整装备/商店仍后续 |
| 武器、领域与移动 | 首片节点导航/方向闪避；OnNode/OffNode，不自动吸回；保持真实头姿 | `Runtime/Locomotion` 已通过合成动作与Physics检查，真机阈值/舒适性待测；[操作优先级](LINEAR_RPG_DESIGN_ZH.md)。武器/领域仍设计，地面阵法仍仅视觉；人工位移不混入真实空间映射 |
| Quest/PICO | 设备适配器输出项目数据，保持特征/标定版本，后续验证 PICO | [兼容设计](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)与[来源核对](QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md)已写。接口目前在文档里；当前 XR Hands 链仍依赖 Meta 类型，不能宣称即插即用支持 PICO |
| 多人 | 暂定两人同房间 PvE，单机系统先做好；NGO＋UTP、房主权威为候选 | [多人契约](MULTIPLAYER_LAN_PLAN_ZH.md)与[调研](MULTIPLAYER_SOURCES_ZH.md)已写；没有联网场景、NGO 或空间对齐实现。现在只保留设计约束 |
| 摄像头演出 | 玩家第一视角保留 XR 追踪；观众镜头用 Cinemachine 3，复杂演出用 Timeline | 本轮新增[镜头与演出设计](CAMERA_PRESENTATION_DESIGN_ZH.md)。Timeline 1.8.10 已在工程中；Cinemachine、运行时演出模块、Recorder 尚未安装或实现 |

## 2. 开发时查哪里

| 要处理的工作 | 现有代码/资产入口 | 设计与操作文档 |
|---|---|---|
| 骨骼采样、质量与回放 | `Assets/SealLab/Runtime/HandSnapshot.cs`、`SealXRHandsSource.cs`、`SealRecording.cs` | [SealLab 说明](SEAL_LAB.md)、[录制流程](SEAL_LAB_QUICKSTART_ZH.md)；新格式按[兼容设计](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)迁移 |
| 特征、权重、拒绝原因 | `SealFeatureExtractor.cs`、`SealProfile.cs`、`SealRecognizer.cs`、`SealTestDiagnostics.cs`；`Profiles/` 与 `Resources/SealVisibilityWeights.json` | [识别测试](RECOGNITION_TEST_ZH.md)。日常从正在使用的 SO 调参，不以历史截图或旧表覆盖现值 |
| 配方和匹配 | `Assets/SealCombat/Runtime/Core/`、`Configuration/` | [README 配置分层](../README.md)、[技能框架](SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) |
| 新 RPG 切片 | `Assets/SealCombat/Runtime/Rpg/`、`Assets/SealCombat/Runtime/Locomotion/`；`LinearRpgTest.unity` | [运行与配置](LINEAR_RPG_QUICKSTART_ZH.md)、[设计及真机待验收清单](LINEAR_RPG_DESIGN_ZH.md) |
| 技能接线和界面 | `SkillTestController.cs`、`SkillTestView.cs`、`SkillScenario.cs` | [试炼说明](SKILL_SHOWCASES_ZH.md)、[中文界面说明](CHINESE_UI_ZH.md) |
| 效果和生命周期 | `Runtime/Effects/`、`Runtime/Visuals/` | [README 新增效果](../README.md)、[玩家事务设计](PLAYER_DATA_DESIGN_ZH.md)；不能把美术动画当伤害结算器 |
| 语音词表和麦克风 | `Tools/SkillVoice/`、`Runtime/Voice/SkillVoiceReceiver.cs` | [语音设置](SKILL_VOICE_SETUP_ZH.md)、[测试录音要求](AUDIO_TEST_RECORDING_ZH.md) |
| 观众镜头与演出 | 尚无运行时模块；Editor 截图工具不能代替该功能 | [镜头方案](CAMERA_PRESENTATION_DESIGN_ZH.md)，先落实独立相机绑定与事件，再接 Cinemachine |

表中的短文件名相对各行所在模块。完整入口以 README 的目录说明为准。录制文件在运行时持久目录，不应混入共享配置；模板、技能和效果资产是不同层的数据。

## 3. 下一阶段顺序与完成条件

1. **真机验收首个线性 RPG 切片。** 新 `LinearRpgTest` 已接玩家HP/MP/CD/检查点、导航、闪避、低位取消与火球/护盾，并通过Unity检查。接下来测真实动作消费、自然动作误触发、遮挡恢复和舒适性，不把模拟输入检查当作头显验收。
2. **画符与敌人战斗。** 用少量单笔模板先测正/负例和模糊拒绝，再接敌人属性、韧性与硬直。多模态配方未来配置化，识别分数不直接变伤害。
3. **扩充线性内容与轻量成长。** 现有三阶段试炼、固定解锁、隐藏盾购买、火球升级和检查点作为基线；继续制作正式关卡与头显交互式菜单。完整商店/剧情尚未实现，不先做随机池或复杂词条。
4. **持续稳定输入与设备交付。** 保留旧标定，验证 Quest 原生语音/性能，再接 PICO 骨骼映射与设备标定；MR 与多人同房空间对齐后续独立验收。
5. **可选独立演出。** 观众镜头不接管 XR 头姿、不改数值；不阻塞首个可玩切片。通用多效果事务、领域和网络仍以实际需要分阶段落地。

所有新增用户可见 UI、Inspector Header/Tooltip 使用中文；稳定 ID、类型名与序列化字段可保留英文。配置验证、失败诊断与运行状态说明随功能一起交付。

## 4. 验证口径与历史资料

架构重构的匹配、配置/迁移、效果及录音→UDP→技能链路历史记录见 [2026-10-29 验收](ARCHITECTURE_REFACTOR_QA_ZH.md)。本轮新增[编辑检查记录](../Logs/linear-rpg/build-check.json)和[真实Play检查记录](../Logs/linear-rpg/play-check.json)，覆盖RPG配置与实际场景接线。记录对应当时版本，不自动覆盖以后修改；模拟手姿/语音和Physics检查不代表十二印、真人麦克风、头显舒适性或各设备已验收。

[早期语音调研](VOICE_GESTURE_RESEARCH_ZH.md)的统一就绪/确认提案已被当前三模式设计替代；其“未安装语音”描述仅指调研当时。[早期实验台教程](SEAL_LAB_QUICKSTART_ZH.md)中的 Meta Link 连接步骤是历史路径，当前电脑连接使用 Steam Link 文档。`HAND_TRACKING_*`、`LINK_*`、固件升级交接和崩溃调查文件作为故障记录保留，不能据此推断目前故障仍在。

新增或完成一个系统时，同步维护三处：本表的落地状态、模块的设计/操作文档、带版本与环境的验收记录。若提案被替代，在旧文档顶部写明新的入口，不把历史过程悄悄改写成当前事实。
