# 官方基础手形接入与串流调试

更新：2026-10-30。用于 `LinearRpgTest` 的左手指向、右手张掌。十二印录制与加权评分保持原样；自动测试通过不代表真机准确率。

## 已修正的问题

旧操作把 SDK 原始 Curl 直接与 25/45 度比较，但本工程 SDK 伸直值接近 180 度。另一个问题是 `HandFinger.Pinky` 与 `Max` 同值，当前枚举字符串是 `Max`，旧操作却查找 `Pinky`。两者均会阻止正常操作。旧角度字段已从控制器和场景移除，小指 ID 通过与原提取器相同的枚举转换取得，**不批量重命名已有模板和录制文件**。

修复前的合成直指骨骼经过真实 `SealFeatureExtractor` 与 `LinearRpgController.ReadNavigationFrame` 后，张掌判定失败，证据见 [修复前复现](../Logs/official-hand-poses/before-fix-repro.json)。

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

[OfficialHandPoseDetector](../Assets/SealCombat/Runtime/Locomotion/OfficialHandPoseDetector.cs) 直接调用当前安装的 Meta Interaction SDK 207.0.1 状态引擎，这是官方 `FingerFeatureStateProvider` 内部使用的类。没有复制 SDK 的阈值算法，也没有引入另一个图像/神经网络识别器。项目适配层负责有效性与游戏手形组合；未挂载整套 `ShapeRecognizerActiveState` 预制体，也不调用其原生组件激活接口，因此不要求把 SteamVR 运行时切回 Meta Link。

[官方基础手形配置](../Assets/SealCombat/Configuration/LinearRpg/官方基础手形.asset) 分别引用官方食指、中指、无名指/小指的 `FingerFeatureStateThresholds`。当前 Curl 状态标识为 Open=`0`、Neutral=`1`、Closed=`2`。左指向要求食指 Open、其他三指 Closed；右张掌要求四指 Open；拇指忽略。

以当前安装资产为准：Open→Neutral 的边界为大于195度，反向为小于185度；Neutral→Closed 为大于245度，反向为小于225度。SDK 配置最短状态确认时间0.022秒。首次初始化的状态选择遵循SDK自身逻辑，不能将这些方向性边界当作一个无状态的角度区间。不要套用网上其他版本的示例阈值。

确认计时使用该手的观测时间；重复快照不会推进计时。左右手独立。过期、低质量、缺特征、非有限值、时间倒退、长间隔或输入来源切换会撤销相关状态，恢复后重新确认。暂停/主控启停也清空状态。手形通过后仍需原来的导航准备、方向驻留、短划轨迹和碰撞检查。

## 下一次 Quest 串流

1. 继续使用 Quest → Steam Link → SteamVR OpenXR。打开 `Assets/SealCombat/Scenes/LinearRpgTest.unity` 后 Play。
2. 双手保持在头显摄像头可见范围。右侧角色面板显示两行手形状态，例如“左：指向通过 [食伸直 中收拢 无收拢 小收拢]”。
3. 先测试左食指伸直、其余三指收拢。看到“指向通过”后稳定约0.2秒进入导航，再平移食指约5厘米、驻留0.65秒。看路径和金色进度圈；回到中点才能再次选点。
4. 右掌可独立短划闪避，无需左手指向。结印保护、持球投掷占用、冷却未结束或落点碰撞仍会阻止闪避，即使张掌通过。
5. RPG火球使用握持投掷：四指官方Closed视为握拳；当前火球可配置至少三指Open、其余没有Closed且全指有效即可松拳，其他场景默认仍四指Open。该放宽不改变闪避的张掌要求。也支持拇食指距离的捏合/松开迟滞。握住后甩手松开，按手轨迹发射。双手低位取消需要两腕仍可见，不能将丢追踪当作取消。旧展示场景保留前推，见[新交互规则](DIRECTIONAL_INTERACTION_AND_THROW_ZH.md)。

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
- [编辑模式报告](../Logs/official-hand-poses/editor-suite.json)、[手形明细](../Logs/official-hand-poses/pose-checks.json)、[播放报告](../Logs/linear-rpg/play-check.json)。报告带时间，真机标志为 false。
- 中文语音配置单元测试、真实KWS合成WAV回放、UDP时钟映射另外运行，结果保存在 `Logs/official-hand-poses/voice-*`；没有开启麦克风或宣称真人准确率。

## 本轮验收结果

最终验证于2026-10-30完成，编辑器Unity 6000.3.10f1。

| 范围 | 实际结果 |
|---|---|
| 完整编辑模式 | 189项通过，其中29项骨骼/官方手形/操作/销毁顺序检查 |
| RPG与效果真实Play | 37项效果及配置、22项关卡联动通过 |
| 护盾试炼 | 1项跨帧伤害流程通过 |
| WAV→真实KWS→UDP→Unity护盾 | 1项端到端通过；使用已有合成WAV，不使用麦克风 |
| Unity检查合计 | 250项；同时检查了退出Play后的状态 |
| Python语音配置 | 16项单元测试通过 |
| 离线KWS | 6个既有WAV/静音/重复口令用例通过；独立UDP时钟映射及模型自检通过 |
| 原有资产保留 | 54个原模板、SealLab运行时代码、包配置文件与修复前备份哈希一致 |
| 最终编辑器 | LinearRpgTest已保存、退出Play、官方配置有效，控制台0错误/0警告 |
| HUD | 新增两行中文手形诊断，截图已检查，无文字截断 |

效果回归主动注入了“扩展Tick异常”和“UI监听异常”，产生两条预期警告；日志确认它们来自对应的测试用例。退出Play的真实MissingReference异常已经修复，重跑后退出错误数为0。最终刷新后控制台无残留错误或警告。[最终状态](../Logs/official-hand-poses/final-state.json)与[汇总报告](../Logs/official-hand-poses/summary.json)分别保存，不用仅在Play结束前写入的报告替代退出验收。

之前提供的三段真人录音也已离线复跑，原音频与默认词表未改动。KWS仍然是关键词检测：“不要释放火球术”里的关键词命中不等于理解否定，保持既有已知限制；长咒语仍为独立词表实验，不代表领域技能已接入。

施工前备份：`D:\UnityProjects\NarutoVR_Backups\before-official-hand-poses-20261030-200246`。包含作者文件和设置，不包含可重建的Library缓存。本轮未打包APK，未测Quest/PICO真机准确率、串流延迟或移动舒适性；下一步由用户串流验收。

本次按路由与账号额度规则，实际委派 `second / gpt-6.1-sol / high` 做只读方案和测试缺口审查，返回 completed。父代理采纳骨骼端到端、失效撤销、时钟独立和配置契约建议并执行验证。外部代理没有操作本机 Unity。

参考：[Meta 官方姿势检测](https://developers.meta.com/vr/documentation/unity/unity-isdk-hand-pose-detection/)。运行行为优先核对工程安装版本源码。
