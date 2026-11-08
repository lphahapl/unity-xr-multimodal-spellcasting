# 火影结印 VR/MR 技术与开源方案调研

调研日期：2026-10-27。目标设备为 Quest 3 与 PICO 4 普通版，按 Android 一体机开发考虑。

这是公开资料核实与工程建议，没有在头显上运行以下项目，没有测得其识别率、延迟或快速连续结印成功率。作者提供演示链接、仓库包含源码、当前设备实测通过，是不同证据等级。

## 1. 是否已有 VR 结印项目

### AdhamElShafie/Naruto-VR-Game

[仓库与 README](https://github.com/AdhamElShafie/Naruto-VR-Game)明确描述 Unity、SteamVR、Leap Motion 和神经网络的火影手势识别项目，列出 Unity 2019.2.18f1、Python 3.7.3、Leap Motion Orion 4.0.0，以及 Rift / Rift S 环境。README 有作业视频和完整运行演示链接，但本次未播放验证。

README 给出的流程为：Leap 数据采集 → Normalize.py → VRProject_NN.py → moves.txt → Unity Core.cs 消费手势组合。项目提供了结印分类与 Unity 忍术响应的参考，属于旧 PCVR / 外部手部传感器路线，不能直接证明 Quest 3 或 PICO 4 内置手部追踪能可靠完成同样任务。文件轮询与桌面 Python 推理也不应原样搬到一体机。

### KiritoAsunaYui2022/NarutoVR

[仓库](https://github.com/KiritoAsunaYui2022/NarutoVR)的说明将其定位为火影手印与部分忍术的 VR 机制演示，作者为 Chase Monson，并明确表示它不是完整游戏。根目录包含 Unity 工程和 Builds。

父代理进一步通过 GitHub API 和原始源码核对了 WizardsMasterBrew 分支提交 34cd7a53ed384af06e76c34f6d4bcb89c10f4bf3：

- [InputData.cs](https://github.com/KiritoAsunaYui2022/NarutoVR/blob/34cd7a53ed384af06e76c34f6d4bcb89c10f4bf3/Assets/Scripts/InputData.cs)查找 Controller | Right/Left 类型的 InputDevice。
- [HandSeals.cs](https://github.com/KiritoAsunaYui2022/NarutoVR/blob/34cd7a53ed384af06e76c34f6d4bcb89c10f4bf3/Assets/Scripts/HandSeals.cs)读取 primaryButton、triggerButton、gripButton、触摸输入，并检查左右手柄的距离和旋转范围来判定印。
- [HandAnimation.cs](https://github.com/KiritoAsunaYui2022/NarutoVR/blob/34cd7a53ed384af06e76c34f6d4bcb89c10f4bf3/Assets/Hand%20Materials/NaturoHandScripts/HandAnimation.cs)设置 Animator 的 Monkey/Bird 等参数，并将渲染手的位置插值到预定位置。

因此，上述被核实的结印实现属于手柄输入与标准手型动画路线，不是读取实际手指关节的复杂裸手识别。这可参考游戏化输入映射与视觉吸附，但不能当作本项目光学追踪成功率的证明。

### Oxiang/Naruto-Game-with-Hand-Signs-Reader

[仓库](https://github.com/Oxiang/Naruto-Game-with-Hand-Signs-Reader)描述 Unity 火影原型，使用 Arduino DIY 手套检测手印，目录含 Naruto VR 与 NarutoGlove。它展示了通过额外接触信号识别结印的思路；这不属于 Quest/PICO 内置光学追踪解决方案，README 也不足以确认当前头显的部署与性能。

### BaekNothing/ninjaVR

[作者项目说明](https://baeknothing.tistory.com/86)记录了 2022 年 Unity 2020.3.19f1 的 VR 忍术玩具项目，采用手印事件列表匹配技能模式。作者提供 [GitHub 地址](https://github.com/BaekNothing/ninjaVR)，但本次仓库页面未成功获取，未确认底层手指追踪方式。可作为序列设计线索，不作已核验可运行方案。

结论：有可参考的 VR 结印原型。当前检索中没有核实到同时满足“Quest 3/PICO 4 内置裸手追踪、复杂双手交织、快速连续结印、公开可复现性能”的成熟完整项目；这只是本次检索边界，不代表此类项目不存在。

## 2. 最相关的可借鉴工具与技术栈

### Unity XR Hands：先建立固定印模板基线

[Custom gestures 文档](https://docs.unity.cn/Packages/com.unity.xr.hands@1.5/manual/gestures/custom-gestures.html)说明其按 hand shape 与 orientation 识别静态姿态，不负责滑动、投掷等动态轨迹。[官方 Hand Capture 介绍](https://unity.com/blog/hand-tracking-tools-for-unity-xr)提供设备上采集、记录 XRHandCaptureSequence、提取 XRHandShape 并调容差的流程。

对固定印的用途是：复用姿态采集与调试工具，自定义双手相对关系及序列识别。不要把两个单手检测器同时为真，直接等同于完整双手结印。每个印应包含必要、辅助及排除特征，并允许多个模板覆盖真实差异。

### Waterloo Reality Labs Universal Gestures：最贴近双手骨骼 ML 管线

[发布包](https://github.com/uwrealitylabs/universal-gestures-release)、[Unity 项目](https://github.com/uwrealitylabs/universal-gestures-unity)、[PyTorch 训练项目](https://github.com/uwrealitylabs/universal-gestures-lab)。发布包 README 包含单/双手选择、正负样本录制、训练服务和导出 ONNX 的流程，强调负样本不足会造成误触发。训练端列出 model_two_hands.py。

限制：发布包接入依赖 OVRHand/Hand 与 Barracuda，不能据 README 宣称已支持当前 OpenXR/XR Hands 跨平台管线；需替换数据提取和推理后端。发布包 LICENSE.md 页面本次显示 0 Bytes；训练及开发仓库未确认明确授权。现阶段适合研究工作流，不作为已确认可商用直接依赖。没有核验其火影印表现。

### Jackknife / DTW：动态换印的研究参考

[Jackknife](https://github.com/ISUE/Jackknife)提供 C# 实现，核心是基于时间序列模板的 DTW 技术。它适合研究换印轨迹与速度变化，不是火影静态印分类器。少样本表现为作者所述适用场景，不等于在本项目里一两个样本就足够。

[LICENSE](https://github.com/ISUE/Jackknife/blob/master/LICENSE)明确包含非商业、学术研究限制。不能因为 GitHub 公开就视为宽松许可依赖。

### 火影专用摄像头项目

- [Kazuhito00/NARUTO-HandSignDetection](https://github.com/Kazuhito00/NARUTO-HandSignDetection)：YOLOX 图像识别与忍术演示；仓库声明 MIT。可参考标签、连续结果处理与施术流程，输入是图像，不能作为 XR 骨骼分类模型使用。
- [bunkerapps/Jutsu-Hero](https://github.com/bunkerapps/Jutsu-Hero)：MediaPipe 关键点、ONNX 分类器、RhythmEngine 结印顺序，训练数据和训练管线不在仓库。[代码许可](https://github.com/bunkerapps/Jutsu-Hero/blob/main/LICENSE)为 PolyForm Noncommercial 1.0.0。MediaPipe 与 XR Hands 的关节约定、坐标和实际误差分布不同，模型权重不能直接代入；需要特征适配和目标域验证，必要时重新训练。
- [Extrys/XRMasterHands](https://github.com/Extrys/XRMasterHands)：将 XR Hands 姿态映射为 Unity Input System 控件的框架，可参考游戏输入封装，不是复杂结印抗遮挡分类器。

## 3. Gemini 建议中的正确方向与修正

正确方向是：通用 XR 数据层、独立结印核心、双手相对特征、时序容错、真机数据采集、轻量模型候选，以及独立的战斗表现层。

需要修正的事项：

1. **“绝对禁止访问透视摄像头”已过时。** [Meta 官方 PCA 文档](https://developers.meta.com/vr/documentation/unity/unity-pca-overview/)支持 Quest 3/3S 前向 RGB 摄像头访问，列出系统版本和相机权限要求。[官方 Unity 示例](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples)包含图像目标检测，因此图像模型不是技术上禁止的路线。PICO 4 的摄像头能力需独立核验，不能类推。
2. **“YOLO 必然拖垮 90Hz”没有实测依据。** [Meta 官方 ML/CV 示例](https://developers.meta.com/vr/documentation/unity/unity-pca-sentis/)使用 YOLO。是否满足游戏预算取决于模型、分辨率、推理频率和后端；骨骼路线通常更容易控制成本，但并非唯一可行路线。
3. **SDK 不宜一概禁止。** [Meta 官方包说明](https://developers.meta.com/vr/documentation/unity/unity-isdk-packages-and-requirements/)支持 Interaction SDK Essentials 与 Unity XR Hands 配合跨平台使用。应隔离平台依赖，不把“安装厂商 SDK”和“核心玩法锁死平台”画等号。
4. **关节角度有用。** 固定印可以同时用手指相对弯曲角、双掌关系、跨手关键点距离。忽略低质量关节，不应禁用所有角度特征。
5. **小模型应与模板基线比较。** MLP 处理单次姿态特征；1D-CNN/TCN 只有输入时间窗口时才利用动作历史。结构名字本身不保证抗遮挡。
6. **不要只置零坐标模拟遮挡。** 增加有效性 mask，避免把合法局部原点误认为缺失；加入实际录得的连续丢追踪、冻结、漂移和错误估计。随机独立关节置零不代表真实双手交织的误差。
7. **“10 帧中 3 帧命中”会受帧率和重复数据影响。** 应根据带时间戳的有效追踪更新积累证据，配合候选差距、必要特征、进入/退出阈值、事件去重。不能把旧帧的不同印任意拼成合法序列。
8. **按设备放宽不应预先写死。** [Meta FMM 文档](https://developers.meta.com/vr/documentation/unity/fast-motion-mode/)提示快速运动优化可能增加抖动并受光照影响。[PICO 文档](https://developer.picoxr.com/document/unity/hand-tracking/)也有 60Hz 高频配置。应基于每个印的混淆矩阵调参；只剩少量外露手指时，如果多个印都符合，应该拒识或继续等待，不能一律放行。
9. **100KB / 0.3ms / 四周完成属于目标或估算。** 需要目标一体机数据验证。完整链路还包括追踪、特征、窗口确认、推理调度及结果回读；GPU 上的小模型不一定比 CPU 更有优势。
10. **视觉效果只改善表现。** 光效、手模型姿态吸附不能修复输入信息丢失；识别应始终消费追踪源，避免读取已经吸附到标准印的渲染骨骼造成自我确认。

## 4. 推荐落地架构（工程判断，待真机验证）

Unity OpenXR + XR Hands + 按构建目标配置的平台扩展
→ 带时间戳与有效性信息的统一手部帧
→ 单手局部手型 + 双手相对变换 + 可靠的跨手距离
→ 可忽略不可靠关节的多模板分类基线
→ 必要时加入轻量 MLP，或短窗口因果 TCN
→ 带 Unknown/拒识的候选分数
→ 有限状态机 / 前缀树管理忍术序列
→ 施术事件与表现层。

分别以各自掌心做局部化时，必须额外保留双掌之间的相对变换，否则会把区分双手印的重要关系消除。各印的必要区分特征缺失时，不应因预期下一印而直接确认。序列先验可以收窄候选和调整阈值，不能自动补齐玩家未完成的印。

若骨骼路径在最难的印上已无法区分，可单独研究 Quest 的双手区域图像分类/骨骼融合。第一人称头显画面与正面网络摄像头数据存在明显差异，需要重新采集验证；这会增加跨平台成本。

## 5. 数据与验收

先用少量最难、最相似的印验证可观测性，保留 Unknown。采集完整试次：准备动作、进入印、保持、退出、快速换印、错误相似印，以及战斗中的普通动作。每条样本记录用户、设备、系统版本、会话、试次、时间戳、追踪状态、标签阶段和原始数据；采样频率不等于有效独立样本频率。

[XRHandJointTrackingState 文档](https://docs.unity.cn/Packages/com.unity.xr.hands@1.7/api/UnityEngine.XR.Hands.XRHandJointTrackingState.html)定义 Pose 等有效性标记，并列出 HighFidelityPose。目标包/运行时是否提供该能力必须实测，不能把它视为所有设备都有的连续关节置信度。

按用户和录制试次/会话分组划分训练与测试，避免同一段录制的邻近帧跨集合。用未见用户、两台设备分别测试。分组验证原则见 [scikit-learn 官方文档](https://scikit-learn.org/stable/modules/cross_validation.html#cross-validation-iterators-for-grouped-data)。

核心指标：每印 precision/recall 与混淆矩阵、普通动作误触发次数/分钟、整套忍术成功率、漏印/跳印/重复触发率、动作到确认的 P50/P95 延迟、丢追踪恢复时间，以及 CPU/GPU、内存和长时温升。帧级分类准确率不能替代战斗体验指标。

## 6. 论文证据边界

[Real-Time Hand Gesture Recognition for OpenXR Using Transformer-Based Machine Learning](https://arxiv.org/abs/2609.25466)研究 Unity/OpenXR 关节输入、手腕朝向及短时间窗口。论文报告的是自建数据集的静态手势结果，并明确把动作间连续过渡与低延迟部署列为后续工作，尚未完成所列公共基准的对照。它可支持骨骼时序路线的研究价值，不能证明复杂火影双手印在一体机上已解决，也不足以据此优先选 Transformer。

## 7. 本地工程与调研协作记录

读取到当前工程为 Unity 6000.3.10f1、URP 17.3.0；manifest 尚无 OpenXR、XR Hands、XR Interaction Toolkit 或推理包。未修改 manifest、场景、Prefab 或运行游戏测试。

推理包需按 Unity 版本选择：[Unity 当前文档](https://docs.unity.cn/Packages/com.unity.ai.inference@2.6/manual/index.html)使用 com.unity.ai.inference，显示名有 Sentis / Inference Engine 的变更历史。不要直接照抄旧 com.unity.sentis 和旧命名空间示例。

外部子代理实际调用为 Antigravity / Gemini 3.8 Flash High，任务 305d3699-06f3-4232-9442-489ebb4ffc47，负责从父代理给定材料提取事实，不联网或改文件。其交付包含版本最低要求与坐标定义的过强概括，父代理未通过原文验收，已确认停止并释放任务；本报告由父代理补查、纠正并写入。Chrome 9222/9223 调试端点未连通，公开资料通过网页检索工具读取；NarutoVR 的目录和上述三个脚本进一步通过 GitHub API/原始源码只读核验。仓库没有克隆或运行，视频没有播放验证。
