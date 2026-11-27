# Quest 移植 PICO 的业界实践与本工程落地对照

> 核查日期：2026-11-27。定位为外部行业调研与本工程源码对照参考（不自动视为已完成代码改动或真机验收；本工程长期跨平台契约见[Quest/PICO 兼容设计](../architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)，手模变形与律印遮挡专项量测见[Quest 与 PICO 手模变形、关节遮挡与律印识别技术报告](QUEST_PICO_HAND_TRACKING_TECH_REPORT_20261127_ZH.md)）。

## 1. 业界解决 Quest→PICO 移植的总原则

业界主流跨平台 VR 团队普遍不把“跨设备同步”寄托在某一个 SDK 的自动兼容层上，而是遵循三层隔离原则：

1. **标准层统一**：用 **Unity OpenXR + `XR Origin` + `com.unity.xr.hands`**（或搭配 Unity XR Interaction Toolkit）作为唯一的姿态、手部骨骼与输入主干。
2. **差异层接口化**：把账号/内购/Entitlement、视频透视（Passthrough / VST）、空间锚点、高刷控制等平台能力抽象成业务无关接口，分别由 Meta 与 PICO 适配器实现。
3. **构建配置分包**：Quest 包与 PICO 包在打包期切换 OpenXR Feature Group、包名、权限和 `AndroidManifest.xml`（Quest 仅启用 Meta 特性组，PICO 仅启用 PICO Support / OpenXR 特性组，避免两个运行时的扩展声明与权限互相污染）。

| 领域 | Meta Quest 原生常见写法 | PICO 业界主流迁移落点 | 注意事项 |
|---|---|---|---|
| **核心管理器与 Rig** | `OVRManager` + `OVRCameraRig` | Unity `XR Origin` + PICO OpenXR（或原生模式下挂载 `PXR_Manager`） | 切勿在同一 Android 包内同时激活两家私有初始化组件 |
| **手部关节数据** | `FromOVRHandDataSource` / `OVRHand` | Unity `XRHandSubsystem`（`XR_EXT_hand_tracking` 26 关节） | OpenXR 虽统一了 26 骨定义，但关节置信度、推算姿态策略和骨架尺度估算仍有平台差异 |
| **系统射线与捏合** | `<MetaAimHand>` / `OVRHand` Pinch | 标准 OpenXR `HandInteractionProfile`（`XR_EXT_hand_interaction`） | PICO 运行时不保证提供 `<MetaAimHand>`，应以标准 OpenXR 手部交互或几何骨架回退为主 |
| **手部显示与交互** | Meta Interaction SDK（ISDK） | Unity XRI + `XRHandSkeletonDriver`；或保留 ISDK Essentials（`ISDK_OPENXR_HAND` + `FromUnityXRHandDataSource`） | 若继续用 ISDK 显示现有 OpenXR 骨架手模，需屏蔽其过严的整手丢帧隐身与私有 Aim 假设 |
| **透视与 MR** | `OVRPassthroughLayer` | PICO OpenXR Video See-Through / `PXR_Manager` 透视开关 | 两端深度遮挡与空间锚点权限流程不同，必须封装 |
| **帧率与渲染优化** | `OVRManager` FFR / 72–90Hz | Vulkan + ASTC + Single-Pass Instanced + PICO FFR / ETFR（注视点渲染） | 必须按 PICO 实际分辨率与热降频特性在真机重测 Profiler |

---

### 2. 业界如何解决 PICO 手追体验落差（核心痛点）

Quest 3 在自遮挡和高速运动下有激进的端侧深度学习姿态补全；PICO 光学手追在**指尖卷入掌心、双手交叠遮挡、低照度或手背背对头显相机**时，更容易把局部指节降级为低置信或无有效 Pose，且有效跟踪区（最佳深度约 152–500 mm，水平/垂直视场约 60°–70°）对贴胸/高举动作更敏感。

业内重手追与手势项目（如《Cubism》《Hand Physics Lab》《Waltz of the Wizard》《Maestro》等跨平台实践）通常采用以下 5 项防御式工程手段：

1. **开启平台高频手追**
   - 在 PICO OpenXR / PICO Unity Integration SDK 配置中显式打开高频手追（如 60 Hz 追踪循环），降低快速握拳或挥手时的采样卡顿。
2. **禁止“全手 26 关节一票否决”，改为“最小必要关节判定”**
   - 握拳、指点、捏合只检查该动作真正依赖的关节（或改用**指尖到掌心距离**代替严格的指骨卷曲角阈值），不要因为无名指、小指或拇指尖被遮挡就把整只手判定为无效。
   - 复合特征（如双手距离、掌面角度）的可用性应只绑定各自实际用到的那几个关节，避免无关手指丢帧“连坐”核心特征。
3. **双阈值滞回（Hysteresis）+ 丢帧短时保活（Grace Period）**
   - **滞回防抖**：例如进入握拳要求弯曲度 > 85%，退出握拳要求展开到 < 65%，消除临界角度附近的高频闪烁；状态切换辅以 3–5 帧或短时间窗口防抖。
   - **短时保活**：遇到 150–300 ms 内的短暂遮挡丢帧时，**显示层冻结或平滑插值到上一帧可信姿态，状态机不立刻 `Reset`**；同时保留“过渡窗口”，不因为手指从伸直到握拳的中间帧出现未知态就清除前置准备状态。
4. **滤波与尺度防抖**
   - 对原始关节位置/旋转施加 **OneEuro Filter**（慢速高平滑消抖、快速低延迟跟手）或轻量插值，缓解低帧/遮挡时的抖动跳变。
   - 手模整体缩放应在追踪稳定时缓慢标定并锁定，**避免每帧直接用单根易遮挡指骨长度实时反推缩放**。
5. **分平台标定阈值与交互容错设计**
   - 不把 Quest 录制得到的窄角度/窄距离阈值直接当 PICO 标准：针对 PICO 的骨架比例和偏保守的弯曲估算单独录制标定（或放宽容差）。
   - 对光学易自遮挡手势做微调（引导手背略朝向头显相机、放大触发容差），对长期低追踪质量的环境给出“保持光线充足/双手位于视野前方”的轻提示，并视产品需要保留手柄无缝切换兜底。

---

### 3. 对照本工程现行架构与两个典型卡点

#### 3.1 “XR Hands 取关节 + Meta ISDK 显示手模”是否需要更换路径？

- **结论：主链路可以保留，但识别/门禁层必须与 Meta 的严格默认值解耦。**
- 当前 `Packages/com.meta.xr.sdk.interaction`（`207.0.1`）在 `Oculus.Interaction.asmdef` 中已定义 `ISDK_OPENXR_HAND`，[FromUnityXRHandDataSource.cs](../../Library/PackageCache/com.meta.xr.sdk.interaction@61177ebcfd9a/Runtime/UnityXR/Scripts/FromUnityXRHandDataSource.cs) 本质上通过 Unity `XRHandSubsystem` 读取标准 OpenXR 关节来驱动 [HandVisual.cs](../../Library/PackageCache/com.meta.xr.sdk.interaction@61177ebcfd9a/Runtime/Scripts/Interaction/Visuals/HandVisual.cs)。工程中的 [PlayerLeftHand.prefab](../../Assets/Art/PlayerHands/PlayerLeftHand.prefab)、右手模与左腕 `WatchMount` 均基于这套 26 骨结构制作，**保留该显示路径可避免推翻重做手模资产**。
- 需要注意的移植隐患（可与[手模变形报告](QUEST_PICO_HAND_TRACKING_TECH_REPORT_20261127_ZH.md#变形从哪里来)互参）：
  1. `HandVisual.UpdateSkeleton()` 在 `!Hand.IsTrackedDataValid` 时会直接隐藏 `SkinnedMeshRenderer`，PICO 丢帧易导致手模和准望匣闪烁。
  2. `FromOpenXRHandDataSource.UpdateHandScale()` 每帧按 `Index1→Index2` 距离实时算 `HandScale`，指节遮挡跳动时会导致整手轻微缩放波动。
  3. [WristHandChannels.cs](../../Assets/scripts/logic/Rpg/WristHandChannels.cs) 已经优先使用 `HandInteractionProfile`（`XR_EXT_hand_interaction`），这是正确的跨平台方向；渲染分支不应反向改写识印与移动的真实观测有效性。

#### 3.2 为什么当前“食指伸直后握拳不能跑步”？

源码检查发现现有链路存在三层逐级叠加的严格门禁：

1. **整手 26 关节连坐降级**：[SealXRHandsSource.cs](../../Assets/scripts/logic/SealLab/SealXRHandsSource.cs#L111-L128) 的 `ApplyValidity()` 把五指全部关节的 `Pose` 有效位 `&= ` 到 `snapshot.highConfidence`。PICO 握拳时指尖卷入掌心自遮挡，只要小指或拇指尖任一节丢失 `Pose`，整手 `highConfidence` 立即变 `false`。
2. **状态机遇低置信清零 + 245° 严格握拳门槛**：[OfficialHandPoseDetector.cs](../../Assets/scripts/logic/Locomotion/OfficialHandPoseDetector.cs#L62-L88) 一旦读到 `!hand.highConfidence` 就立刻调用 `Reset("追踪无效或过期")`；且 `closed` 要求食、中、无名、小指 4 根全部处于 Meta 默认阈值 `"2"`（[IndexFingerFeatureStateThresholds.asset](../../Library/PackageCache/com.meta.xr.sdk.interaction@61177ebcfd9a/Runtime/DefaultSettings/PoseDetection/IndexFingerFeatureStateThresholds.asset#L22-L25) 中 `midpoint: 235, width: 20`，即 Curl 须大于 **245°**）。PICO 握拳在自遮挡下估算角经常停在半弯区或单指未达标，难以点亮 `leftClosed`。
3. **极短衔接窗 + 过渡帧清零前置指向**：[RpgLocomotionController.Exploration.cs](../../Assets/scripts/logic/Locomotion/RpgLocomotionController.Exploration.cs#L122-L127) 要求 `now - lastPointAt <= gestures.sampleMaxGap`（仅 **0.18 s**），且第 126 行在指向转握拳期间只要有一帧因丢关节出现 `hand.leftPoseUnconfirmed`，就会立即将 `lastPointAt` 设为 `double.NegativeInfinity`，永久抹掉前置指向记忆。

#### 3.3 为什么当前“律印因部分遮挡很难识别”？

1. **无权重手指遮挡抹掉 `45.5%` 双手核心特征**：[律印.asset](../../Assets/ScriptableObjects/手印识别配置/律印.asset) 总正权重 `16.5`（仅 `Index/Middle.Curl` 与 5 个 `Both.*` 有正权重，无名指与小指权重为 `0`），并配置 `allowLowConfidence: 0`、`minimumCoverage: 0.8`。但 [SealFeatureExtractor.cs](../../Assets/scripts/logic/SealLab/SealFeatureExtractor.cs#L29-L55) 将整手 `left.IsHighConfidence && right.IsHighConfidence` 赋给全部 `Both.*` 特征（权重合计 `7.5`）。只要双手合十时挡住一根无名指或小指，有效覆盖率立即跌至 `9 / 16.5 = 54.5%`（低于 `80%`）而拒识。
2. **双手时间偏斜与 Quest 窄标定**：`SealFeatureExtractor` 要求双手时间戳差 `<= 0.06 s`；同时原模板里的距离/角度区间（如 `Both.IndexTipDistance` `[0.0080, 0.0145]` ± `0.025 m`、`Both.WristDistance` `[0.0854, 0.0922]` ± `0.03 m`）基于 Quest 录制，在 PICO 骨架尺度与遮挡偏移下容错偏窄。

---

### 4. 渲染、性能与工程检查清单

- **图形 API 与着色器**：确认 PICO 构建开启 **Vulkan**、**ASTC** 与 **Single-Pass Instanced / Multi-View**，逐一核查自定义 Shader 的立体宏与透明剔除边界（防止单眼缺失或 Draw Call 翻倍）。
- **视口分辨率与温控优化**：在 PICO 上启用 **FFR / ETFR（注视点渲染）**，真机挂载 Profiler 排查渲染线程与 GC 峰值，剔除未使用的 Meta 运行时程序集以降低内存占用。
- **建议的工程改造优先级**：
  1. **特征级置信度解耦**：`SealFeatureExtractor` 与 `OfficialHandPoseDetector` 改为按实际依赖关节判定可用性，取消 `SealXRHandsSource` 全手连坐。
  2. **跑步握拳状态机加固**：分离出宽松/抗遮挡的握拳条件（或指尖-掌心距离 + 滞回），并在 `RpgLocomotionController.Exploration` 中避免过渡帧 `leftPoseUnconfirmed` 直接清空 `lastPointAt`。
  3. **显示与比例平滑**：在手模驱动侧为短暂丢帧增加短时保持，并对 `HandScale` 做低通平滑或稳定期锁定。
  4. **PICO 真机标定与多端基线回放**：利用现有 `SealRecording` 在 PICO 真机采集正反例与遮挡样例，独立调校/验证 PICO 参数档。

---

### 5. 外部核查来源（2026-11-27）

- PICO 开发者文档 — [Support for the Unity OpenXR Plugin](https://developer.picoxr.com/document/unity-openxr/overview/) · [Hand Tracking（OpenXR）](https://developer.picoxr.com/document/unity-openxr/hand-tracking/)
- PICO Unity Integration SDK — [PXR_Hand.cs 官方手模驱动](https://github.com/Pico-Developer/PICO-Unity-Integration-SDK/blob/e0740bf309b26a0b9adae33c506514569bfe5d38/Runtime/Scripts/Hand/PXR_Hand.cs)
- Unity 手部追踪文档 — [Unity XR Hands 1.7 文档（XRHandSkeletonDriver / Finger Shapes）](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/manual/index.html)
- Meta 开发者文档 — [Meta XR Interaction SDK Input Processing](https://developers.meta.com/vr/documentation/unity/unity-isdk-input-processing/) · [Interaction SDK OpenXR / Unity XR Integration](https://developers.meta.com/vr/documentation/unity/unity-isdk-getting-started/)
