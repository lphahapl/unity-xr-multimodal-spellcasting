# 技术与开发文档总索引

2026-10-31 画符调研：[画符接入待办与追加审阅](GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH.md)。已在临时目录核对 Quest 轨迹示例与 PDollar 源码，尚未导入或实现画符运行时；P2，现有交互问题优先。文末留有其他 AI 的追加审阅格式。

2026-10-31 画符调研：[画符接入待办与追加审阅](GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH.md)。已在临时目录核对 Quest 轨迹示例与 PDollar 源码，尚未导入或实现画符运行时；P2，现有交互问题优先。文末留有其他 AI 的追加审阅格式。

2026-10-31 画符调研：[画符接入待办与追加审阅](GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH.md)。已在临时目录核对 Quest 轨迹示例与 PDollar 源码，尚未导入或实现画符运行时；P2，现有交互问题优先。文末留有其他 AI 的追加审阅格式。

2026-10-31 画符调研：[画符接入待办与追加审阅](GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH.md)。已在临时目录核对 Quest 轨迹示例与 PDollar 源码，尚未导入或实现画符运行时；P2，现有交互问题优先。文末留有其他 AI 的追加审阅格式。

2026-10-31新增：[火球动作评分、伸手保持发射与自动诊断](SCORED_THROW_DIAGNOSTICS_ZH.md)。当前RPG保留松手投掷，并加入后拉前送及主动伸手保持；实时Debug显示动作分及硬条件，Play自动写JSONL。点腕开启前提示已关闭，世界诊断面板可开关，护盾腕饰丢追踪时隐藏。真人手感仍待按日志验收。

核对日期：2026-10-30。本文汇总已经讨论的决定，并区分代码现状、设计和验证缺口。**文档中出现接口名或流程图，不代表已经实现。** 日常配置从 [README](../README.md) 开始；代码行为以当前源码、实际场景引用和验收记录共同核对。

本次基础操作更新：官方逐指状态引擎、Curl尺度与小指ID修复、端到端回归及串流排查见[官方基础手形](OFFICIAL_HAND_POSES_ZH.md)。十二印仍采用原加权评分。

## 1. 当前决定与落地状态

最新手感补充见[交互稳定性与详细调试](INTERACTION_FEEL_AND_DEBUG_ZH.md)：导航平滑、切换候选时保留驻留、投掷释放位置冻结与中文调试分区已实现。第二账号整理的[教学 UI 与音频方案](TUTORIAL_UI_RESEARCH_ZH.md)仍为建议，等待首期交互形式确认；画符运行时未实现。

| 主题 | 已确定的方向 | 当前状态与主要入口 |
|---|---|---|
| 设备与调试 | Quest 优先，当前电脑路径为 Steam Link → SteamVR OpenXR → Unity | 已有可用追踪路径；[连接说明](STEAM_LINK_DEVELOPMENT_ZH.md)。原生 Quest 手追踪曾通过对照，不等于全部技能、语音和性能已在 APK 验收 |
| 手印识别 | 实际追踪骨骼＋有效性 → SDK/双手特征 → 手动权重模板；优先少遮挡、有区分度的特征 | `Assets/SealLab` 已实现；[录制与标定](SEAL_LAB_QUICKSTART_ZH.md)、[独立识别测试](RECOGNITION_TEST_ZH.md)。分数是相似度，不是校准后的正确概率 |
| 录制与数据 | 倒计时、自动结束、带标签 JSONL、整段统计生成 SO 草稿，再人工确认 | 已实现。图片与 Excel 用作动作参考和初始权重；真实范围必须来自设备录制。子印不使用中指姿态特征；保留现有人工调参。十二印资产存在不代表十二印都已标定或测过准确率 |
| 中文口令 | sherpa-onnx KWS，PC 本机服务通过 UDP 发事件 | 已实现 PC 接入；[语音设置](SKILL_VOICE_SETUP_ZH.md)、[真人录音基线](PLAYER_AUDIO_BASELINE_ZH.md)。否定句仍可命中关键词；Android 原生语音未接入 |
| 输入与取消 | 纯结印、纯口令、组合三模式；10 秒有界历史，默认 8 秒双向匹配，满足条件即激活 | 已实现**单印配方**、保持、去重、取消和冷却；[操作说明](SKILL_TEST_GUIDE_ZH.md)。多印序列仍是后续设计 |
| 技能与场景 | SO 定义＋纯逻辑匹配器＋效果实例＋独立中文 View | 四个单机技能场景、默认右手火球与自身护盾已实现；[场景说明](SKILL_SHOWCASES_ZH.md)、[架构验收](ARCHITECTURE_REFACTOR_QA_ZH.md) |
| 玩家数据 | 共享定义、运行态、带版本存档分开；施法事务管理资源与提交 | [玩家数据设计](PLAYER_DATA_DESIGN_ZH.md)已写；PlayerRuntime、玩家 HP/能量/解锁/装备/存档尚未实现。现有训练靶生命、护盾和匹配冷却不等于完整玩家系统 |
| 武器、领域与移动 | 技能实例拥有规则；环境表现和游戏移动约束分开，效果结束可清理 | [技能框架设计](SKILL_FRAMEWORK_AND_INTERACTION_ZH.md)已写；尚未实现。当前地面阵法只是视觉装饰；不能通过冻结头显追踪来限制玩家的现实移动 |
| Quest/PICO | 设备适配器输出项目数据，保持特征/标定版本，后续验证 PICO | [兼容设计](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)与[来源核对](QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md)已写。接口目前在文档里；当前 XR Hands 链仍依赖 Meta 类型，不能宣称即插即用支持 PICO |
| 多人 | 暂定两人同房间 PvE，单机系统先做好；NGO＋UTP、房主权威为候选 | [多人契约](MULTIPLAYER_LAN_PLAN_ZH.md)与[调研](MULTIPLAYER_SOURCES_ZH.md)已写；没有联网场景、NGO 或空间对齐实现。现在只保留设计约束 |
| 摄像头演出 | 玩家第一视角保留 XR 追踪；观众镜头用 Cinemachine 3，复杂演出用 Timeline | 本轮新增[镜头与演出设计](CAMERA_PRESENTATION_DESIGN_ZH.md)。Timeline 1.8.10 已在工程中；Cinemachine、运行时演出模块、Recorder 尚未安装或实现 |

## 2. 开发时查哪里

| 要处理的工作 | 现有代码/资产入口 | 设计与操作文档 |
|---|---|---|
| 骨骼采样、质量与回放 | `Assets/SealLab/Runtime/HandSnapshot.cs`、`SealXRHandsSource.cs`、`SealRecording.cs` | [SealLab 说明](SEAL_LAB.md)、[录制流程](SEAL_LAB_QUICKSTART_ZH.md)；新格式按[兼容设计](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)迁移 |
| 特征、权重、拒绝原因 | `SealFeatureExtractor.cs`、`SealProfile.cs`、`SealRecognizer.cs`、`SealTestDiagnostics.cs`；`Profiles/` 与 `Resources/SealVisibilityWeights.json` | [识别测试](RECOGNITION_TEST_ZH.md)。日常从正在使用的 SO 调参，不以历史截图或旧表覆盖现值 |
| 配方和匹配 | `Assets/SealCombat/Runtime/Core/`、`Configuration/` | [README 配置分层](../README.md)、[技能框架](SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) |
| 技能接线和界面 | `SkillTestController.cs`、`SkillTestView.cs`、`SkillScenario.cs` | [试炼说明](SKILL_SHOWCASES_ZH.md)、[中文界面说明](CHINESE_UI_ZH.md) |
| 效果和生命周期 | `Runtime/Effects/`、`Runtime/Visuals/` | [README 新增效果](../README.md)、[玩家事务设计](PLAYER_DATA_DESIGN_ZH.md)；不能把美术动画当伤害结算器 |
| 语音词表和麦克风 | `Tools/SkillVoice/`、`Runtime/Voice/SkillVoiceReceiver.cs` | [语音设置](SKILL_VOICE_SETUP_ZH.md)、[测试录音要求](AUDIO_TEST_RECORDING_ZH.md) |
| 观众镜头与演出 | 尚无运行时模块；Editor 截图工具不能代替该功能 | [镜头方案](CAMERA_PRESENTATION_DESIGN_ZH.md)，先落实独立相机绑定与事件，再接 Cinemachine |

表中的短文件名相对各行所在模块。完整入口以 README 的目录说明为准。录制文件在运行时持久目录，不应混入共享配置；模板、技能和效果资产是不同层的数据。

## 3. 下一阶段顺序与完成条件

1. **稳定 Quest 输入与标定。** 保留当前模板；隔离厂商骨骼、手锚和口令来源，以旧录制核对结果。跟踪失效、双手时差和原点变化不能被适配层掩盖。使用独立录制轮次和错误姿态测误报、漏检；不以连续帧数量代替独立测试样本。
2. **玩家数据与技能事务。** 按玩家设计落实生命、能量、护盾账本和一次提交；失败清理、输入消费、冷却、扣费与重复事件都有明确结果，再扩展敌人和伤害。
3. **装备、存档及更多效果。** 先稳定版本迁移与技能 ID，再增加武器、领域、环境表现、移动约束；SO 不存每个玩家的临时对象或剩余时间。
4. **可选观众演出切片。** 可在火球靶场独立试做，验收镜头开关不改变识别、瞄准、命中或取消。它不是玩家数据的前置条件，不先铺设整套过场框架。
5. **一体机交付与后续设备。** 验证原生语音、性能与平台能力，再测 PICO 的骨骼映射和设备标定；网络和同房空间对齐在单机稳定后落地。

所有新增用户可见 UI、Inspector Header/Tooltip 使用中文；稳定 ID、类型名与序列化字段可保留英文。配置验证、失败诊断与运行状态说明随功能一起交付。

## 4. 验证口径与历史资料

架构重构已经有匹配、配置/迁移、效果及录音→UDP→技能链路的检查记录，见 [2026-10-29 验收](ARCHITECTURE_REFACTOR_QA_ZH.md)。这些记录对应当时版本，不自动覆盖以后修改，也不代表十二印、真人麦克风、所有设备或所有双眼画面均已验收。本轮只更新文档，检查本地链接与工程文件哈希，没有重新执行 Unity 或真机测试。

[早期语音调研](VOICE_GESTURE_RESEARCH_ZH.md)的统一就绪/确认提案已被当前三模式设计替代；其“未安装语音”描述仅指调研当时。[早期实验台教程](SEAL_LAB_QUICKSTART_ZH.md)中的 Meta Link 连接步骤是历史路径，当前电脑连接使用 Steam Link 文档。`HAND_TRACKING_*`、`LINK_*`、固件升级交接和崩溃调查文件作为故障记录保留，不能据此推断目前故障仍在。

新增或完成一个系统时，同步维护三处：本表的落地状态、模块的设计/操作文档、带版本与环境的验收记录。若提案被替代，在旧文档顶部写明新的入口，不把历史过程悄悄改写成当前事实。
