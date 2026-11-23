# 首章玩家手模与左腕准望匣

适用 Unity 6000.3.10f1、Meta Interaction SDK 207.0.1、URP 17.3.0。玩家手接入首章现有的 Unity XR Hands 数据源。教学右手由 CourtyardTaskGuide 单独持有。

## 成品与外观参考

美术主参照选[主角双手设定图](../visuals/protagonist_hands_model_sheet.jpg)。露出的掌指、拇指和食指上的铜环、从虎口延到前臂的深黛色缠布可以沿用。看缠布的起点、搭接方向和左右手差异时，分别对照[左手背](../visuals/20261102_single_hand_16views/left_07_back.png)、[右手背](../visuals/20261102_single_hand_16views/right_07_back.png)和[掌心与侧面图](../visuals/20261102_single_hand_16views/gallery.html)。图中指姿与透视仅作造型参考，长度、左右手轴向、关节和绑定姿态均取自本次同侧 OpenXR FBX。

当前工程内为 V2。V1 已在修改前完整封存。V2 收紧指骨之间的软组织与掌缘，保留指节头和手背筋腱的起伏。手部沿用[历史底色稿](../visuals/20261102_官方手模贴图初稿/)在原 UV 上登记的掌纹、关节褶皱、指甲和铜环刻纹。新小臂使用单手背视图中的深黛麻布纹理，左右手分别烘焙。铜环高光由主体图集的 Alpha 遮罩控制。腕后几何和准望匣的校准基准沿用 V1。[同灯光、同视角的 V1 / V2 局部对照](../visuals/player_hands_openxr/V1_V2_Hands.jpg)

| 交付物 | 文件 |
|---|---|
| 可试装的 Unity 玩家手 | [左手 Prefab](../../Assets/Art/PlayerHands/PlayerLeftHand.prefab) · [右手 Prefab](../../Assets/Art/PlayerHands/PlayerRightHand.prefab) |
| 对应的蒙皮源网格 | [左手 FBX](../../Assets/Art/PlayerHands/PlayerLeftHand.fbx) · [右手 FBX](../../Assets/Art/PlayerHands/PlayerRightHand.fbx) |
| 可编辑的 Blender 5.2 文件，各自内嵌两张贴图 | [左手工程](../../ArtSource/PlayerHands/PlayerLeftHand.blend) · [右手工程](../../ArtSource/PlayerHands/PlayerRightHand.blend) |
| 准望匣实例使用的原型与佩戴参数 | [现有准望匣 Prefab](../../Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.prefab) · [佩戴配置](../../Assets/Art/PlayerHands/ZhunwangFit.asset) |
| 使用实际成品材质的 Unity 编辑器预览 | [双手伸展](../visuals/player_hands_openxr/PlayerHands_Open.png) · [右手握拳](../visuals/player_hands_openxr/PlayerHands_Flex.png) · [掌面](../visuals/player_hands_openxr/PlayerHands_Palm.png) |

## 阶段 2 · Blender 制作清单

1. 在 Unity Project 展开 `Packages/com.meta.xr.sdk.interaction/Runtime/Meshes`，将 `OpenXRLeftHand.fbx`、`OpenXRRightHand.fbx` **复制到工程外的美术工作目录**，分左右两个 Blender 文件制作。保存一份未动的副本。两手分别沿用自己的母版。对准望匣外形时，用 `Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.fbx` 的副本作参照，实装用带朱砂扣、读数和投影位的既有 Prefab。
2. 新建 Blender 场景，单位设 Metric，Unit Scale 1，记录版本和导入预设。使用 FBX 导入，Scale 从 1 开始，读取文件轴向，关闭 Automatic Bone Orientation、Force Connect Children、Ignore Leaf Bones 和任何会自动改休止姿态的选项。切到 Armature → Rest Position。在物体面板记录网格和 Armature 的位置、旋转和缩放。出现 0.01 或补偿旋转时，保留原值。不要对它们执行 Ctrl+A、Apply Pose as Rest Pose、镜像骨架、重命名或重新连接骨骼。
3. **先做空往返**。在副本中直接导出并用下述 Unity 工具校验。实测并记录双端 `XRHand_Wrist` 到 `XRHand_MiddleProximal` 的世界距离。清除模型预览的缩放后用同一标尺比对。遇到 100 倍、轴翻转、容器被折叠、休止矩阵不同，修正导入/导出配置再制作。部分 Blender FBX 导入器会重建骨头局部轴；若空往返未通过，可只从 DCC 导出编辑后的网格/蒙皮数据，在能保留原 FBX 骨架的流程中合入母版，仍需通过同一校验。
4. 保留 `OXRLeftHand` 或 `OXRRightHand` 容器及其 26 个 `XRHand_*` 关节。保持官方网格的对象空间、原点和 Armature Modifier。复制旧网格用作工作网格，在 Edit Mode 编辑几何。也可给新拓扑指定同一 Armature，用 Data Transfer → Vertex Data → Vertex Groups，Nearest Face Interpolated，将旧网格权重传至新网格，应用传递后在手部 Normalize All、Limit Total 4。检查拇指根、指缝、指蹼及靠近其他手指的内侧，防止最近表面取错手指；测试后恢复 Rest Position。
5. 以腕骨为长度零点，沿腕至掌指根方向的反向延长，别用全局轴套左右手。打开原腕部封盖，由连续的腕圈向肘部放样，焊接接缝，逐环自然加宽。目标腕后 **14 cm**（允许 12–15 cm），准望匣表带中心约 **4 cm**（3–5 cm），其后留约 **10 cm**。整段小臂和整个表带覆盖环移除全部顶点组权重，再仅将 `XRHand_Wrist` 设为 1。用 Weight Paint 和 Vertex Weights 核对不含 `XRHand_Palm`、手指或其他影响；手背过渡留在腕前。左右都延长，设备只戴左腕。
6. 只将末端 **4.5 cm**（4–5 cm）的侧面和端盖分到第二材质槽。UV0 的 U 绕臂一圈，V 沿臂长，从渐隐起始环 0 到肘端 1；跨 U 接缝拆 UV，几何保持焊接。端盖 V 保持在 0.999–1 的全裁剪区，面法线朝外。手指、指缝、手掌、表带下方和前段小臂全部用第一槽。材质边界两侧共享几何位置、权重和连续法线。姿态测试时打开背面剔除检查接缝。
7. 清理复制的参考网格，只导出一个蒙皮网格、原骨架以及原有父容器。网格可保留 `LeftHand` / `RightHand` 名称便于交接；装配工具通过蒙皮组件及骨骼名定位。不要增加 `_end` 骨，原有五根 `XRHand_*Tip` 要保留。FBX 导出仅选中相关对象，保持与空往返通过的轴向及缩放配置一致，关闭 Add Leaf Bones、Only Deform Bones、Bake Animation 和额外的 Apply Transform。不要用“仅变形骨”剔除没有权重的 Palm 或 Tip。准望匣参考件不参与手模导出；新做的配件单独导出静态网格，不带 Armature。回 Unity 同步导入参数后再验骨架、尺寸、权重和蒙皮。

Unity 端在工具内同步同侧母版的 ModelImporter。手动复核 Model 页的 Scale Factor 为 1、Use File Scale 关闭、Bake Axis Conversion 与 Preserve Hierarchy 开启。Rig 页为 Generic / No Avatar，Optimize Game Objects 关闭，每顶点最多 4 个骨权重。

成品保留原掌部 UV。第一槽的小臂部分另占图集右下方的空区，第二槽的末段单独使用 0–1 UV。绘制或烘焙时按材质槽处理，再检查起始环的颜色和法线能否接上。这样可以替换缠布外观而继续使用同一套骨架。

## 阶段 3 · 装配设计

- 比较母版与新 FBX 的关节集合、父链、静置位置/旋转/缩放和蒙皮绑定矩阵。在官方显示 Prefab 的副本上保留 HandVisual、OpenXR 显示根、26 项驱动数组、材质属性组件。按**新网格自身**的 bones 索引逐项找到官方同名 Transform，保留 sharedMesh.bindposes。蒙皮 rootBone 单独绑定 XRHand_Wrist。
- 主体和末段分两槽。主体实心写深度，末段在 UV 空间做覆盖裁剪并写深度。双眼以同一 UV 采样，与屏幕像素和眼索引无关。渐隐与表带区域分开。
- 左腕 WatchMount 使用独立配置和静置手掌基准，承接已有准望匣的场景实例。腕部菜单只有一个位姿来源，朱砂扣、原有事件与铜叶槽继续属于原实例。可选视线组件以表盘法线、距离、朝向和连续停留时间决定读数/交互唤醒，提供 UnityEvent 与 C# 事件。注视本身不会打开教学菜单。
- 安装器限定首章两个已保存的场景路径，显式连接真实左右 Hand，通过 Undo 支持切换。只在用户操作后编辑当前打开的场景，不自动保存。

## 阶段 4 · Unity 替换操作

1. 打开 **Tools → NarutoVR → 首章 → 玩家手模与准望匣**。项目随附 `Assets/Art/PlayerHands/PlayerLeftHand.prefab` 和 `PlayerRightHand.prefab`，可直接从第 4 步试戴。需要制作新皮肤时，把新 FBX 放入 Assets，在窗口选左手或右手，将新资产拖到「自定义 FBX」，核对「官方母版 FBX」对应同侧。使用「同步导入参数并重新导入」，再点「校验骨架和蒙皮」。报告中的缺骨、重名、父链、缩放或矩阵错误须回 DCC 纠正。左右分别校验。
2. 选左手时，槽 0 与槽 1 分别为 `LeftHand.mat`、`LeftCuff.mat`，右手对应 `RightHand.mat`、`RightCuff.mat`。窗口随侧别自动选材质。主体贴图 RGB 控制底色，Alpha 为铜环高光遮罩，材质启用 Copper highlight from Skin alpha。末段使用独立缠布贴图与 UV 裁剪，Start (UV0 V) 和 Feather (UV0 V) 默认为 0、1，Dither cells per UV unit 调整裁剪点大小。需要单看形变时可在窗口手动选 `HandBody.mat` 和 `ForearmEnd.mat` 两个中性材质。
3. 左手在「准望匣佩戴配置」选 ZhunwangFit；点「生成玩家手 Prefab…」选择 Assets 内的输出位置。右手重复操作。在 Prefab Mode 检查根对象 HandVisual 的 `_openXRRoot`、`_openXRJointTransforms`、`_openXRSkinnedMeshRenderer`、`_openXRHandMaterialPropertyBlockEditor`；隐藏的序列化字段可切 Inspector 的 Debug 模式查看。展开 OpenXRLeftHand/LeftHand 或 OpenXRRightHand/RightHand，Skinned Mesh Renderer 的 Mesh 应是新 FBX 子网格，Root Bone 应是同侧 XRHand_Wrist，Materials 的 Element 0 为主体、Element 1 为尾段。换网由工具同时填充 bones；在 Inspector 单拖 Mesh 会漏掉索引重映射。
4. 在 Unity 打开 Chapter01_Courtyard 或 Chapter01_Awakening，核对窗口「左手 Prefab」「右手 Prefab」，点「安装 / 切换玩家手」。确认两个 HandVisual 各自的 Hand 指向「通用手部追踪数据源」下对应的真实 Hand；SealLabView 的「显示追踪骨骼调试线」应关闭。窗口的两个骨骼线按钮供单独比对。「切换为官方玩家手」用于同场景 A/B 对照。场景接受后再由操作者手动保存；第二个场景需独立操作。
5. 左手 Prefab 展开 OpenXRLeftHand/XRHand_Wrist/WatchMount。WatchMount.profile 引用 ZhunwangFit，调整 Offset Metres 和 Tilt Degrees 后重入 Play 试戴。偏移采用静置掌面基准，+Z 向指根。初始腕后 4 cm、倾角 12°，开合手观察表带内缘与皮肤。首章 WristMenuController.wornDevice 继续引用原有准望匣，playerWatchMount 指向该 WatchMount。运行时同一台准望匣挂在这个节点下。使用原 Prefab 的朱砂扣、匣面 World Space 读数和铜叶插槽，保持既有事件与解谜引用。不要往手模里再放一台准望匣。独立刚性道具随腕骨运动，不添加受重力驱动的 Rigidbody。
6. WatchMount.gaze 引用同节点的 WatchGazeActivator。安装器给 Viewer 绑定玩家 HMD；首章读数面的正面取 vitalsAnchor 的本地 −Z。Wake/Sleep Facing 调表盘朝眼角度，Wake/Sleep Look 调头显看向表盘的角度，Distance 以米计，Delay 以秒计。默认唤醒距离 0.62 m、休眠距离 0.74 m，连续满足 0.12 s 唤醒、离开 0.20 s 休眠。原有佩戴读数和朱砂扣准入受该状态控制。自定义交互碰撞体可在 On Wake / On Sleep 中绑定专属 Collider.enabled 为 true / false，或订阅 Changed(bool)。不要将整个准望匣、铜叶插槽、提供追踪的父物体或唤醒组件自身设为待机开关。禁用该可选组件可回到原有读数和按扣交互。

### Quest / Link 验收

桌面交付回归在 Unity 6000.3.10f1 的隔离工程执行。两手各 2814 个三角形，腕后最远 14.09 cm。合成握拳时，指部最大位移约 13.6 cm，两侧受测的表带区顶点位移均为 0。Android Vulkan 与 GLES3 共 48 项主体、铜环高光和渐隐着色器变体编译通过。

腕匣预设在静置掌面坐标中取位移（−4.4、−25.4、−40）mm，旋转（−5、5、−12）°。前两轴补偿手掌基准与两道表带的轴向差，最后一轴让匣面朝内。以成品左手和首章实际佩戴尺寸抽检两道表带共 13 个内孔截面，最小净隙为 0.42 mm。桌面测试使用固定手型，实际跟踪缩放、双眼渐隐和穿戴舒适度按下表在头显复核。

| 动作 | 通过条件 |
|---|---|
| 双手摊开、五指依次弯曲、拇指对掌、双手分开再合十结印 | 每侧 26 关节和正确侧别对应。指缝、虎口、指尖无串指、跳骨、错轴或左右翻转。交叉手指前后遮挡自然。 |
| 静止、缓慢和快速握拳，手腕内外翻转，桌面站姿和真机不同手掌大小各试一次 | 表带区只随腕骨刚性运动，准望匣跟腕且尺寸稳定。近看表带下方、腕背、袖端无穿模或浮空。教学示范手仍只由引导显示。 |
| 抬左手看匣、在距离和角度边缘轻微摆动、放下、双手短时互挡再恢复 | 注视达持续时间后显示读数和允许按扣；迟滞区内不会逐帧闪烁。休眠时不会误开菜单，丢跟踪清除注视状态。右手实按朱砂扣可操作原有菜单，铜叶插入、恢复检查点和分离面板继续工作。 |
| Quest Android 和 Link 分别检查；头部左右平移/旋转，交替遮眼观察手腕末端 | 左右眼均可见主体，4–5 cm 末段的裁剪花纹固定在同一片皮肤。尽头完全消失，跨材质过渡自然，没有一眼全透、单眼缺失或屏幕固定噪点。正常双眼视差应保留。 |
| 近脸看匣、近远各看一次；手靠视野边缘握拳和收回 | 轮廓不因包围盒过小突然消失，手指不因尾段处理而透视。在 Quest 上用帧调试/Profiler 记录双手可见时的 GPU 时间，比较官方手基线。 |
