# 参考工程 EP07 的玩家与手部数据来源

EP07 有 Quest 3 原生 Android 的双手输入路径，有编辑器通过 SteamVR 的运行记录。两者经过不同的运行时。项目里的已知头显实测支持前者。当前能找到的 SteamVR 测试采用模拟输入，不能用它确认玩家通过 Steam Link 裸手抓物时的外观或追踪质量。

## 查的是哪个场景

仓根的构建表仅启用 `Assets/_Project/Scenes/AWSpace01.unity`，在这个工作副本里该文件缺失。恢复好的运行工程位于 `D:/UnityProjects/mr-aiwo-education-science/Recovery/Project`，Unity 6000.3.10f1，Meta XR Core/Interaction/Interaction.OVR 85.0.0，Unity OpenXR 1.16.1。它与 APK 导出项目的构建表均只启用 `Assets/_Project/Scenes/ep07-meteorology-and-astronomy.unity`，场景 GUID 均为 `0a955cee758dda441b350e4c87b632a1`。后面所有场景文件与引用均取自这个实际构建入口。复核时该场景 SHA-256 为 `d83de18e2ea8ffe71ae23c2ee809082ea5036b2e98a92665449b745210a0e26c`。[仓根构建表](D:/UnityProjects/mr-aiwo-education-science/ProjectSettings/EditorBuildSettings.asset) · [实际工程构建表](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/ProjectSettings/EditorBuildSettings.asset) · [APK 导出构建表](D:/UnityProjects/mr-aiwo-education-science/Recovery/APK-Export/ExportedProject/ProjectSettings/EditorBuildSettings.asset) · [锁定版本](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Packages/packages-lock.json)

## 场景实例与显示

场景 YAML 中 `PrefabInstance` 数量为 0，交互相机和手部组件均已经展开保存在场景中。玩家根是 `[BuildingBlock] Camera Rig`。其下 `[BuildingBlock] OVRInteractionComprehensive` 的 `OVRHands/OVRLeftHandDataSource` 与 `OVRHands/OVRRightHandDataSource` 挂有两只 `OVRHand`。场景也包含用于远抓预览的额外同名 HandVisual。主显示是 OVRInteractionComprehensive 直接子项 `OVRLeftHandVisual` 和 `OVRRightHandVisual`。[实际场景](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/_Project/Scenes/ep07-meteorology-and-astronomy.unity:25641)

| 用途 | 左 fileID | 右 fileID |
| --- | --- | --- |
| OVRHand | 5004 | 5005 |
| FromOVRHandDataSource | 5752 | 5750 |
| LastKnownGoodHand | 5751 | 5749 |
| 显示使用的最终 SyntheticHand | 5263 | 5396 |
| 主 HandVisual | 5451 | 5316 |
| OpenXR SkinnedMeshRenderer | 4737 | 4690 |
| 场景额外挂载的 EP07TrackedHandGrab | 811001 | 811002 |

沿序列化引用反查，左右均经 `OVRHand → FromOVRHandDataSource → LastKnownGoodHand → Hand → SyntheticHand → HandVisual`。HandVisual 默认用 `IHand.GetJointPosesLocal` 写显示骨骼的本地姿态。SDK 中间有多层 Hand 与 SyntheticHand，最终能按抓取交互修改手形。`Camera Rig/TrackingSpace` 为 fileID 2676。两只主显示均保存有旧版和 OpenXR 版两组 renderer/root；编辑器和 Player 的实际编译参数均含 `ISDK_OPENXR_HAND`，SDK 启用 OpenXR 分支。场景序列化的 OpenXR 子根初始 inactive 会由 HandVisual 启动时激活。两只手柄显示物体初始 inactive。[HandVisual 的分支与启用](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Library/PackageCache/com.meta.xr.sdk.interaction@0a0b3efbe99d/Runtime/Scripts/Interaction/Visuals/HandVisual.cs:195) · [局部关节更新](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Library/PackageCache/com.meta.xr.sdk.interaction@0a0b3efbe99d/Runtime/Scripts/Interaction/Visuals/HandVisual.cs:305)

场景把 `GestureModule/LeftHandGesture`、`GestureModule/RightHandGesture` 上的两个 `EP07TrackedHandGrab` 直接连到同侧 OVRHand、主 HandVisual 及 OpenXR renderer。这是项目自己的定制。LateUpdate 读取 `OVRSkeleton.IOVRSkeletonDataProvider.GetSkeletonPoseData()`。仅在跟踪有效、有足够骨骼数据且应用有焦点时处理。如果视觉不可见、视觉 IHand 无效，或显示腕与原生腕之间距离超过 **12 厘米**，且旋转数组完整，才把原生腕与关节的追踪空间姿态转换成世界姿态并覆盖显示骨骼。同时，脚本直接读取原生拇食指尖和食指捏合标志供抓取使用。失去有效输入超过 0.18 秒则释放相关交互。没有记录逐帧命中回退的次数或外观，无法反推出哪一帧采用了这条路径。[左手接线](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/_Project/Scenes/ep07-meteorology-and-astronomy.unity:131368) · [右手接线](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/_Project/Scenes/ep07-meteorology-and-astronomy.unity:133355) · [EP07 原生回退与捏合](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/EP07Recovered/Scripts/EP07TrackedHandGrab.cs:83)

## 平台与证据

- **Quest 原生 Android。** Android 与 Standalone 均指定 OpenXRLoader。Meta 自己的 MetaXRFeature 两个平台均配置启用。OculusProjectConfig 的 `handTrackingSupport=2` 对应 SDK 的 HandsOnly，OculusRuntimeSettings 的 `handSkeletonVersion=1` 对应 OpenXR。11 月 25 日的安装记录包含 Quest 3 APK 成功安装、原生 Android Activity 冷启动与 APK 哈希。另一份头显 logcat 记录实际戴着且有焦点时 `rightTracked=True rightValid=True`，也有丢失跟踪的片段。记录来自该工程只有 Android Player 才编译的诊断函数。安装、记录跟踪和编辑器模拟验收为不同批次，未能逐帧核对某个 APK 的外观。[手追配置](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/Oculus/OculusProjectConfig.asset:17) · [骨架版本](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/Resources/OculusRuntimeSettings.asset:15) · [安装凭证](D:/UnityProjects/mr-aiwo-education-science/Recovery/PlayTest/TrackedHands/device-install.txt) · [头显跟踪记录](D:/UnityProjects/mr-aiwo-education-science/Recovery/PlayTest/HandGrab/device-latest.txt:12) · [产生日志的 Android 条件编译](D:/UnityProjects/mr-aiwo-education-science/Recovery/Project/Assets/_Project/Scripts/Feature/Gesture/MRGestureHandler.cs:31)
- **PC 编辑器 / SteamVR。** VoiceQuizHunter 的 Play 日志明确报告运行时 `SteamVR/OpenXR` 并关闭 MetaXRFeature。TrackedHands 的通过记录明示 `simulated inputs`，也明确记录用模拟手移动测试对象。上述材料可证明桌面会话连过 SteamVR，无法证明在该运行时用 Quest 裸手正常完成整段抓取，也未证明 Meta 原生骨架回退能用。该工程的包锁中没有 `com.unity.xr.hands`，其正式取数入口与 NarutoVR 使用的 Unity XR Hands 接法不同。[SteamVR 启动日志](D:/UnityProjects/mr-aiwo-education-science/Recovery/PlayTest/VoiceQuizHunter/play-final.log:624) · [编辑器模拟输入记录](D:/UnityProjects/mr-aiwo-education-science/Recovery/PlayTest/TrackedHands/verified.txt:1)
- **PICO。** 本次审阅的构建设置、正式依赖和交付记录没有提供 PICO 原生手追或该场景在 PICO 头显运行的验收证据。

对 NarutoVR 可借鉴的是先比较原始腕姿与显示腕姿，再判断是否需要矫正显示。用 Quest 原生 SDK 作回退的具体实现依赖 OVRPlugin 手骨数组；本项目经 Steam Link 使用的输入与这条原生路径应分别验证。
