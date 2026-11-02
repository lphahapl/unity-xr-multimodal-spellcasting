# Quest 优先、PICO 后续兼容：资料核对

> 文档定位：资料核查/历史调研，核对日期见正文，候选方案和外部版本信息需要使用时重新核实。本文不充当当前操作或已完成功能清单；现行入口见[文档索引](../README.md)。

日期：2026-10-29。范围：官方文档、官方 GitHub 和本工程代码。**没有 PICO 实机测试或安装包兼容验证。** 设计取舍见 [兼容方案](../architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)。动态网页和 GitHub main 会变化，正式选包时需要再固定 tag/commit。

## 1. PICO SDK 的三代资料不能混用

| 官方来源 | 本次能确认的事实 | 不能据此推出 |
|---|---|---|
| [PICO Unity OpenXR SDK](https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK) | 旧独立 OpenXR 仓库 README 为 1.4.0；写明 Unity 6、PICO OS 5.13+、60Hz 高频手追踪选项 | 所有固件每帧都有新关节，或结印准确率达到某个水平 |
| [PICO Integration SDK 发布说明](https://github.com/Pico-Developer/PICO-Unity-Integration-SDK/releases) | 3.3.0 已加入 Unity OpenXR 支持；3.4.0 宣布向 PICO 4 系列扩展空间锚、共享锚、网格、场景捕获等能力 | 必须同时安装旧独立 OpenXR 包与新 Integration 包；或目前工程的所有依赖已兼容 |
| [新 PICO Unity SDK](https://github.com/Pico-Developer/PICO-Unity-SDK)、[XR/package.json](https://github.com/Pico-Developer/PICO-Unity-SDK/blob/main/XR/package.json) | 新仓库分 XR 和 SpatialAdapter；本次 main 的 XR 包版本为 6.1.1、包名 `com.bytedance.pico.xr`，清单声明 Unity 2022.3.15f1 基线 | 6.1.1 是本项目应立刻安装的稳定版本，或清单中的 Unity 下限证明 6000.3.10f1 全部功能已验证 |
| [官方项目模板](https://github.com/Pico-Developer/PICO-Unity-Project-Templates) | 当前仓库有 PICOXR、OpenXR、PICOSpatial 三个独立工程目录 | 这些模板可无损覆盖 NarutoVR，或 Shared Space 多应用等同于同房间多人游戏 |

**工程决策：后续先验证目标设备对应的 PICO SDK 的 Unity OpenXR 接入模式；保留 PICO 原生接口作为缺少能力时的适配实现。** 不把“Integration”名称理解为无法跨平台，也不在现有 Quest 工程里试装所有代际 SDK。3.4.0 与新 6.x 均需查清设备/固件和依赖后再选，不把旧教程当安装清单。

## 2. 手追踪能共享数据通道，不保证共享阈值

[PICO 手追踪官方文档](https://developer.picoxr.com/document/unity/hand-tracking/)列出 OpenXR 26 关节、Unity XR Hands 支持和 Neo3/PICO 4/PICO 4 Ultra 系列；页面的 5.11+ 是该能力页要求，**不能替代所选新版 SDK 的完整固件要求**。自适应手模型的设备限制也不同于原始关节能力。

[Unity XR Hands 数据读取](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/manual/hand-data/xr-hand-access-data.html)要求检查 provider 支持的关节布局、每次更新结果、关节有效性与 `TryGet` 结果。复制 `XRHand` 结构本身并不冻结底层数组，录制时必须复制关节值。[Unity XR Hands 包说明](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-xr-hands)说明平台 provider 另有实现；仅安装通用包不保证拿到数据。

上述资料支持统一采样接口的路线，但没有提供 NarutoVR 的遮挡成功率、手印混淆矩阵或端到端延迟。关节 valid、SDK 的 high confidence 和本项目匹配得分是不同概念，不能换算成同一个“正确概率”。

## 3. 原生能力与 PC 串流能力分开验收

[PICO Connect 官方商店页](https://store-global.picoxr.com/en/detail/1/7257472870996230207)列有手追踪支持；这不是“指定 PICO 型号＋Connect 版本＋SteamVR＋Unity XR Hands 一定输出完整关节”的证明。首轮 PICO 验证应分别记录原生 Android 和 PC 串流结果，避免重复将串流输入问题误判成识别算法问题。

本工程当前 Quest/Steam Link 路径已用过，Quest 原生手追踪此前也通过用户对照；本轮不重新声称已完成真机验收。MR 透视、空间场景数据和锚点能否穿过 PC 串流另行确认，不从手部成功推导它们可用。

## 4. 空间能力有版本差异，共享锚也有生态边界

旧 [PICO OpenXR Spatial Anchor 页面](https://developer.picoxr.com/document/unity-openxr/spatial-anchor/)仍列 PICO 4 Ultra，而上面的新版 Integration 发布说明已扩大 PICO 4 系列支持范围。两者属于不同版本资料，不能简单下结论“普通 PICO 4 永远不支持”或“所有 PICO 4 现在都支持”。具体型号、地区固件、SDK、权限与运行模式须实测。

[PICO MR 示例说明](https://developer.picoxr.com/document/unity/mixed-reality-sample/)描述了通过 PICO 网络服务共享锚点的演示。这证明存在官方实现路径，不能证明完全离线或可由 Quest 加载。

[Meta Shared Spatial Anchors](https://developers.meta.com/vr/documentation/unity/unity-shared-spatial-anchors/)说明共享锚与游戏联网是两件事，组共享、空间服务设置、加载和定位都有各自的前置条件；其错误处理包含网络/云存储失败。[Meta 官方共享锚样例](https://github.com/oculus-samples/Unity-SharedSpatialAnchors)可借鉴定位流程，不能把其中网络和账号配置直接等同于 NGO 局域网。

**本次未找到可直接互通 Meta 与 PICO 原生共享锚的官方保证。** 因而将厂商锚作为平台适配器，跨品牌共同坐标需独立标定方案。LAN 游戏数据可直连，不等于采用的空间共享服务也无公网依赖。

## 5. 原生中文语音是独立移植任务

[sherpa-onnx 官方仓库](https://github.com/k2-fsa/sherpa-onnx)列出关键词识别、C/C#/Java 等接口及 Android ARM64 支持，适合作为沿用当前模型的候选。它不证明本工程已具备 Unity Android 插件、IL2CPP 绑定、麦克风采样和生命周期处理。

目前本工程是 Python → PC 回环 UDP → Unity。Quest/PICO Android 的 `127.0.0.1` 都是头显本身。应选择原生库桥接实现同一种口令事件契约，另测权限、重采样、CPU/内存、应用恢复和迟到事件；不把放开 UDP 到局域网当成本地语音移植完成。KWS 的否定句误触发问题也不会因为换头显而消失。

## 6. 本地代码核对入口

| 文件 | 本次确认的耦合/已有保护 |
|---|---|
| [HandSnapshot.cs](../../Assets/SealLab/Runtime/HandSnapshot.cs) | 关节数组依赖 Meta `Constants`；`SnapshotHand` 实现 `IHand`；`schema=1` 和 skeleton/source 文本不能代替特征算法版本 |
| [SealXRHandsSource.cs](../../Assets/SealLab/Runtime/SealXRHandsSource.cs) | 当前 XR Hands 仍经过 Meta `Hand` / `FromUnityXRHandDataSource`，并附加时间和关节有效性保护；不是无 Meta 依赖的 provider |
| [SealFeatureExtractor.cs](../../Assets/SealLab/Runtime/SealFeatureExtractor.cs) | 使用 Meta `FingerShapes`、`HandJointId`；双手特征为自定义；有双手时间偏差限制，但部分质量门控依赖整手/整指 |
| [SealProfile.cs](../../Assets/SealLab/Runtime/SealProfile.cs) | 评分与覆盖率分开，缺失正权重仍留分母；尚无 extractor/calibration 版本兼容声明 |
| [SealLab.cs](../../Assets/SealLab/Runtime/SealLab.cs) | 两类来源切换、相对录制原点、60Hz 上限和过期保护；采样上限不是传感器实际频率 |
| [SkillEffects.cs](../../Assets/SealCombat/Runtime/Effects/SkillEffects.cs) | 直接从 lab 和 Meta 腕索引取生成位置；未来应接 Actor 姿态接口 |
| [SkillVoiceReceiver.cs](../../Assets/SealCombat/Runtime/Voice/SkillVoiceReceiver.cs) | 仅接收回环来源，检查会话/序号/时效；不要在跨设备改造时丢掉这些保护 |

安装的 Meta 包同时包含 Legacy OVR 和 OpenXR 枚举；当前程序集配置启用 OpenXR 手骨骼。不能看到旧枚举文件就把当前数据说成 24 关节，也不能因当前恰好索引一致就省掉后续显式映射和 schema 检查。

资料读取方式：Chrome 远程调试端点当前不可连接，使用官方网页搜索/读取及公开 GitHub 源文件替代；部分 PICO 网页直读只返回导航，相关能力以可见官方检索内容与明确版本的仓库说明交叉核对。没有在浏览器执行安装或账号操作。
