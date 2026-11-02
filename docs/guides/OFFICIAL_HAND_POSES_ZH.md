# 官方基础手形接入与串流调试

操作核对：2026-11-01。用于 `LinearRpgTest` 的左手指向、右手张掌。十二印录制与加权评分保持原样；自动测试通过不代表真机准确率。

## 已修正的问题

旧操作把 SDK 原始 Curl 直接与 25/45 度比较，但本工程 SDK 伸直值接近 180 度。另一个问题是 `HandFinger.Pinky` 与 `Max` 同值，当前枚举字符串是 `Max`，旧操作却查找 `Pinky`。两者均会阻止正常操作。旧角度字段已从控制器和场景移除，小指 ID 通过与原提取器相同的枚举转换取得，**不批量重命名已有模板和录制文件**。

修复前的合成直指骨骼经过真实 `SealFeatureExtractor` 与 `LinearRpgController.ReadNavigationFrame` 后，张掌判定失败，证据见 修复前复现（本地证据：`Logs/official-hand-poses/before-fix-repro.json`，不随仓库分发）。

完整回归还定位到退出Play的组件销毁顺序问题：技能控制器先销毁时，RPG主控仍设置其 `enabled`。已对暂停、启停和位移回调中的Unity对象做存活检查，并添加两种销毁顺序的回归。此前Play报告在退出前写入，不能单凭其通过判断退出也正常；本轮同时检查退出后的控制台与编辑器状态。

自动测试桥接改为原子发布命令，并重试短暂文件占用，避免编辑器轮询恰好读到正在写的命令文件。命令入口为 `Tools/Request-RpgCheck.ps1`，例如 `./Tools/Request-RpgCheck.ps1 full-editor-check`。

## 接入方式

```text
Meta OVR 或 OpenXR/XR Hands 的实际骨骼
  → SealLab 冻结快照、有效性
  → SDK FingerShapes 原始特征
  → 官方 FeatureStateProvider<FingerFeature,string> ＋ 官方阈值资产
  → 左指向 / 右张掌
  → 原有移动、闪避、取消与持球优先级状态机
```

[OfficialHandPoseDetector](../../Assets/SealCombat/Runtime/Locomotion/OfficialHandPoseDetector.cs) 直接调用当前安装的 Meta Interaction SDK 207.0.1 状态引擎，这是官方 `FingerFeatureStateProvider` 内部使用的类。没有复制 SDK 的阈值算法，也没有引入另一个图像/神经网络识别器。项目适配层负责有效性与游戏手形组合；未挂载整套 `ShapeRecognizerActiveState` 预制体，也不调用其原生组件激活接口，因此不要求把 SteamVR 运行时切回 Meta Link。

[官方基础手形配置](../../Assets/SealCombat/Configuration/LinearRpg/官方基础手形.asset) 分别引用官方食指、中指、无名指/小指的 `FingerFeatureStateThresholds`。当前 Curl 状态标识为 Open=`0`、Neutral=`1`、Closed=`2`。左指向要求食指 Open、其他三指 Closed；右张掌要求四指 Open；拇指忽略。

以当前安装资产为准：Open→Neutral 的边界为大于195度，反向为小于185度；Neutral→Closed 为大于245度，反向为小于225度。SDK 配置最短状态确认时间0.022秒。首次初始化的状态选择遵循SDK自身逻辑，不能将这些方向性边界当作一个无状态的角度区间。不要套用网上其他版本的示例阈值。

确认计时使用该手的观测时间；重复快照不会推进计时。左右手独立。过期、低质量、缺特征、非有限值、时间倒退、长间隔或输入来源切换会撤销相关状态，恢复后重新确认。暂停/主控启停也清空状态。手形通过后仍需原来的导航准备、方向驻留、短划轨迹和碰撞检查。

## 下一次 Quest 串流

先在 Steam Link 路线确认骨骼持续有效与左右官方手形，再按[移动指南](ENCOUNTER_LOCOMOTION_ZH.md)测试战斗选点、胸前前推后撤和探索收指停步；按[火球指南](FIREBALL_TUNING_ZH.md)测试握持与松手。手形通过只是动作的一个门槛，不保证移动、施法或发射被接受。

面板状态的含义：

| 显示 | 排查方向 |
|---|---|
| 追踪无效或过期 | 检查串流、双手视野和骨骼是否继续更新 |
| 特征失效：某指 | 该指的关节/质量未通过；先调整手朝向与遮挡 |
| 确认中 | 刚开始、刚恢复或状态尚未确认 |
| 半弯 | SDK判为Neutral；左手非食指必须收拢，右手四指需要伸直 |
| 手形通过，但不移动 | 看导航开启、候选点、驻留、闪避冷却及“移动”诊断 |

当前配置引用 SDK 包内默认资产。后续如确需调松，应复制阈值 SO 到本工程再改引用，避免改 `Library/PackageCache` 或影响其他交互。可以先分别做30次导航、闪避，再自然活动1分钟记录误触；这些是真机测量计划，不是已完成的准确率。

## 自动回归入口

- `Tools → NarutoVR → 线性RPG → 运行完整编辑模式回归`：骨骼到操作、旧结印录制回放、技能匹配、数值存档、碰撞、配置迁移和场景接线。
- `Tools → NarutoVR → 线性RPG → 运行独立存档播放检查`：真实 Play 中的合成骨骼→SDK→场景LateUpdate，以及效果/预制体、关卡伤害和取消。使用唯一 probe 存档，不覆盖玩家档案。
- 编辑模式报告（本地证据：`Logs/official-hand-poses/editor-suite.json`，不随仓库分发）、手形明细（本地证据：`Logs/official-hand-poses/pose-checks.json`，不随仓库分发）、播放报告（本地证据：`Logs/linear-rpg/play-check.json`，不随仓库分发）。报告带时间，真机标志为 false。
- 中文语音配置单元测试、真实KWS合成WAV回放、UDP时钟映射另外运行，结果保存在 `Logs/official-hand-poses/voice-*`；没有开启麦克风或宣称真人准确率。


## 验证口径

当前检查入口、报告时间和真机边界见[验证说明](VALIDATION_ZH.md)。旧轮次数量不再作为最新通过数维护。
