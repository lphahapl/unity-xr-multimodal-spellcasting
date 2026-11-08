# Steam Link 结印实验台

## 改动前备份

`D:\UnityProjects\NarutoVR_Backups\before-steamlink-20261029-105429`

包含修改前的 Assets（含场景、模板与 .meta）、Packages、ProjectSettings、Tools、docs，共 171 个文件，逐文件 SHA-256 比对通过。备份不含可重建的 Library、Temp、构建缓存和 APK。备份清单为该目录的 `backup-manifest.json`。

## 两条输入路线

- 原生 Quest / Quest Link：原来的 `SealLab.unity` → `FromOVRHandDataSource` → 实际 `Hand / IHand`。
- Steam Link：`SealLabSteamLink.unity` → Unity OpenXR / XR Hands → SDK `FromUnityXRHandDataSource` → 实际 `Hand / IHand`，同时由 `SealXRHandsSource` 检查原始逐关节有效性、独立的双手更新时间。

两条路线进入相同的骨骼快照、SDK FingerShapes、自定义双手特征、可视化、倒计时录制、回放和加权评分。未安装新的 SteamVR Unity 插件，也未改 SDK 包缓存。Steam Link 依然使用 Quest 本身的手追踪；更换的是向 PC 传输数据与 PC OpenXR runtime 的路线。

SDK 适配器负责坐标、骨骼及手尺度转换。额外的有效性门控用于防止某个关节丢失后，SDK 缓存的位置继续被当成新有效值。只有 Dynamic 更新中的对应手关节更新才刷新其时间，头部/手根更新和另一只手的更新不能延长这只手指姿态的有效期。丢失、超时或子系统停止会使对应数据失效。

XR Hands 的质量标记由关节姿态有效性推导，不是 Meta 原生置信度。HUD 显示 `DERIVED QUALITY`，录制保存 `source` 与 `confidenceSource`，保持 schema 1 并兼容既有录制读取。运行时估计/推断的手指也可能被标记为姿态有效，这不证明相机确实看到了被遮挡关节。

## 第一次使用

1. PC 安装并启动 Steam 与 SteamVR；Quest 安装并打开 Steam Link，与 PC 连接。直接从 Quest 的 Steam Link 连接。推荐 PC 网线接路由器、Quest 使用同一局域网的 5 GHz 或 6 GHz Wi-Fi。
2. 在 SteamVR 设置的 OpenXR 页选择 SteamVR 作为当前 OpenXR runtime。此操作会影响电脑上其他 OpenXR 应用；切回 Quest Link 时，在 Meta Horizon Link 中把 Meta 设回当前 runtime。
3. Unity 退出 Play，选择 Windows 开发目标。菜单 `Tools → NarutoVR → Steam Link → 打开独立实验场景`。首次可运行“准备独立实验场景”。
4. Windows OpenXR 开启 Hand Tracking；准备工具会打开它，并关闭 Windows 的 Meta 功能组、MetaXRFeature 和 MetaHandTrackingAim。为防止 SDK 在重载时再次提示启用功能组，同时记录“不再提示”的工程偏好；Android 功能组及原有 Build Settings 场景列表保持不变。若编辑器另有 Play Mode OpenXR Runtime 覆盖项，选择 SteamVR 或系统默认，避免仍强制使用 Meta。
5. 按 Play，放下控制器，伸出双手。检查 HUD 的 `OpenXR / XR Hands / SteamVR…`（实际 runtime 名称）、`XRHandSubsystem running`、左右手 `TRACKED` 和变化的关节版本。如果 runtime 仍为 Meta，说明尚未切换到 SteamVR。系统菜单里能点选，不能替代应用内骨骼验证。
6. 打开 `Tools → NarutoVR → Seal Lab`，用现有“准备录制”倒计时摆印并自动保存，或切到“实时测试”。记录目录显示在面板中，与原来的 PC 录制流程相同。

“打开实验场景”仍指向原来的 Quest 实验场景，新增的“Steam Link 场景”指向独立场景。两条路线切换时先停止 Play，避免同时加载两个相机 Rig。

## 数据与模板

旧录制和模板仍保留。先拿几个固定动作对照两条路线的特征分布，再决定是否重新标定阈值；相同骨骼格式不能保证不同 runtime 的尺度估计、推断姿态或延迟完全相同。建议把 Steam Link 样本分开标注，录制短片段，核对遮挡和快速换印时的失效行为。

本次调整没有更改加权评分公式、模板权重或时序阈值。MR 透视、空间理解、最终延迟和性能仍需用 Quest 原生 Android 包验证。

## 回到原生开发

停止 Play，打开 `Assets/SealLab/Scenes/SealLab.unity`，选择 Android 目标；独立原生包不依赖 PC 当前 runtime。若要测试 Quest Link，还需把 PC runtime 设回 Meta，并检查 Windows Meta 功能配置；本次关闭的是 Windows MetaXRFeature / MetaHandTrackingAim，原生 Android 设置不受影响。

需要整体恢复修改前版本时，先正常关闭 Unity，再从上方备份目录恢复对应文件；不要在运行中的编辑器里删除整个工程。优先只恢复实际需要的脚本/配置，并保留后续新增录制。

## 验证范围

`Tools → NarutoVR → Run Seal Lab Checks`：覆盖既有评分/录制/回放、XR Hands 无效关节缓存隔离、逐指质量、过期失效与关节映射。

`Tools → NarutoVR → Steam Link → 检查独立实验场景`：核对直接数据来源、HUD、唯一相机及无 OVR runtime 依赖。

编译和静态场景检查通过，不等于 Steam Link 实机成功。只有在头显连接后的 Play 中左右手持续有效、丢失会清空、录制与实时识别可用，才完成实机验收。

2026-10-29 本地验证：修复新增 OpenXR runtime 名称显示引入的 `Constants` 命名冲突后，Runtime / Editor 程序集编译通过，49 项检查及独立场景引用检查通过。原生场景、12 个模板、Packages 和 Android 项目设置与备份核对保留。真实 Steam Link 双手追踪尚未验收。

后续实机对照：Unity 实际 runtime 为 `SteamVR/OpenXR`，SteamVR Meta 插件兼容性关闭时，已观察到左右手同时 `TRACKED / HIGH [DERIVED QUALITY]`、变化的关节版本与骨骼线/特征值，用户确认实验台正常。该结果验证基础链路，不代表结印准确率、长时间稳定性或最终性能已验收。首次 Play 还暴露了 Windows Meta 功能组与单项开关不一致的问题；准备工具现同时关闭功能组，避免域重载后尝试在运行中重新启用功能。
