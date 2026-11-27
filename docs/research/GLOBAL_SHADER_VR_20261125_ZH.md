# 首两章的 VR 环境 Shader 调研

核查日期为 2026-11-25。状态为方案调研。数据来自当前工作区、锁定包源码和官方版本文档。本轮未更改 Unity 配置或资产，未启动 Unity、构建 APK 或进行头显测量。

## 优先做什么

1. **先做远山空气透视与室内外光色 A/B。** 首章让苏醒室暖光、院内石木与远山有清楚的层次。第二章用偏冷的远山托出暖光下的衡梁和支点塔，保住篮索、断桥接点、栈道边线。已有线性距离雾、方向光、暖灯、远岩渐隐和山谷雾卡，第一轮主要调整现有参数。整个关卡可共用一套预设字段，各场景单独取值。
2. **随后试首章庭院的低频云影。** 先在一块室外 URP Lit 石面上试方向光 cookie，再决定是否推广到院面与外屋顶。优先把变化限制在环境表面，观察阴影走过石面时，量水尺、天仪阁机关、准望匣、手印和教学文字的对比度。

湿润与水线可继续在首章石渠局部深化。两项优先方案都能沿用现有材质绘制和灯光路径，首轮移动档无需增加全屏 Renderer Feature。

## 工程基线

Unity **6000.3.10f1**，URP **17.3.0**，OpenXR **1.15.1**，XR Management **4.5.1**，XR Hands **1.7.2**。图形设置的默认管线指向 PC，Android 的默认 Quality 选择 Mobile 并覆盖它，Standalone 默认 PC。编辑器画面不能替代 Android Player 比较。[编辑器版本](../../ProjectSettings/ProjectVersion.txt) · [包锁](../../Packages/packages-lock.json) · [Graphics](../../ProjectSettings/GraphicsSettings.asset) · [Quality](../../ProjectSettings/QualitySettings.asset)

| 配置 | Android / Mobile | Standalone / PC |
|---|---|---|
| Graphics API 与路径 | PlayerSettings 手动 Vulkan，URP Forward | 工作区未为 Standalone 序列化手动 API 列表，运行时读回实际 API。URP Forward+ |
| 渲染比例 / HDR / MSAA | 0.8 / 关 / 4× | 1.0 / 开 / 关闭，序列化值 1 |
| 管线的 Depth / Opaque Texture 默认项 | 关 / 关 | 开 / 开 |
| 主光阴影 | 1024、1 级联、50 m，附加光阴影关 | 2048、4 级联、50 m，附加光阴影开 |
| Renderer Features | Screen Space Decal，已启用，40 m | Screen Space Decal 40 m、SSAO，两者均启用 |
| 共通 | 主光、附加光、Light Cookies 和 Rendering Layers 支持开启。两套 Renderer 均关闭 Depth Priming，Intermediate Texture 为 Auto，启用 Native Render Pass | 同左 |

移动档的管线选项关闭了默认深度/不透明纹理请求。贴花、相机配置或其他执行中的 Pass 仍可改变实际深度资源和拷贝路径，需看 Render Graph。40 m 是贴花距离。PC SSAO 是现有 Feature，与相机的后处理开关分开。Graphics 绑定的全局设置中，当前 `RenderGraphSettings.m_EnableRenderCompatibilityMode=0`，同文件的旧 `m_EnableRenderGraph` 字段由包留作迁移。首章场景中查到的 URP 相机数据均关闭 Post Processing。第二章正式 XR 相机来自 Meta 包的 `XROriginCameraRig`，场景覆盖远裁剪面，当前源 Prefab 相机未声明 URP 附加相机数据。采集时仍要核对实际玩家相机、Renderer 和 Pass 列表。[PlayerSettings](../../ProjectSettings/ProjectSettings.asset) · [Mobile RP](../../Assets/Settings/Mobile_RPAsset.asset) · [Mobile Renderer](../../Assets/Settings/Mobile_Renderer.asset) · [PC RP](../../Assets/Settings/PC_RPAsset.asset) · [PC Renderer](../../Assets/Settings/PC_Renderer.asset) · [URP 全局设置](../../Assets/Settings/UniversalRenderPipelineGlobalSettings.asset)

Android 和 Standalone 均引用 OpenXR Loader 并配置启动初始化，OpenXR 的两份平台对象均设 `m_renderMode=1`，1.15.1 包内枚举对应 Single Pass Instanced。设备端按支持能力使用单通道立体路径，实际 multiview、注视点渲染、眼缓冲和补帧状态须设备读回。项目有 Meta XR 直接依赖，未见 PICO 专用直接依赖。PICO 从启动、双眼和裸手输入开始验证。[XR Loader 接线](../../Assets/XR/XRGeneralSettingsPerBuildTarget.asset) · [OpenXR 平台配置](../../Assets/XR/Settings/OpenXR%20Package%20Settings.asset) · [直接依赖](../../Packages/manifest.json) · [OpenXR 1.15 Render Mode](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/manual/project-configuration.html)

## 场景与可复用资产

当前构建列表含 `Chapter01_Awakening`、`SealLab`、`Chapter01_Courtyard` 和 `Chapter02_SuspendedPath`。生产跨章接线是庭院残规门庭检测头部进入，调用 `AsyncSceneLoader` 单场景加载正式悬道。山体预览未加入构建列表，另有地形材质、天空和裁剪范围。当前自有运行时代码未发现按房间、院落或吊篮高度改写场景雾与灯的控制器。下表是场景序列化值，遇到 Prefab 与外部运行时覆盖须在 Player 读回。[构建列表](../../ProjectSettings/EditorBuildSettings.asset) · [门触发](../../Assets/scripts/logic/Environment/SceneTravelGate.cs) · [加载与激活](../../Assets/scripts/logic/Environment/AsyncSceneLoader.cs)

| 场景 | 天空与雾 | 灯与关键视线 |
|---|---|---|
| [Awakening](../../Assets/Scene/Chapter01_Awakening.unity) | 青灰薄云，线性 45–185 m，RGB (0.58, 0.66, 0.66)，三色环境光强度 0.8 | 方向光 1.20，硬阴影；苏醒室、量水道 |
| [Courtyard](../../Assets/Scene/Chapter01_Courtyard.unity) | MountainCloudSky，线性 63–245 m，RGB (0.69, 0.79, 0.86)，三色环境光强度 0.8 | 方向光 1.32，硬阴影；入庭明渠、量水尺、天仪阁校星案、残规门庭 |
| [SuspendedPath 正式](../../Assets/Scene/Chapter02_SuspendedPath.unity) | MountainCloudSky，线性 70–340 m，RGB (0.49, 0.60, 0.66)，三色环境光强度 1.0 | 谷口斜照 1.65，软阴影，显式 Sun。石门断桥和朱砂祭坛约 +80 m，工坊和中控约 0 m，谷底约 −80 m。标称 120 m 衡梁，初始轴心约 Y=50 m，由双索联系三层。XR 相机远裁剪 450 m |
| [MountainPreview](../../Assets/Scene/Chapter02_SuspendedPath_MountainPreview.unity) | GorgeSkyline，指数雾密度 0.00165，RGB (0.61, 0.69, 0.72)，远裁剪 1,050 m | 沿用谷口方向光字段，替换远山。新增高程山体走 URP Unlit，无接影与探针。预览参数单独评估 |

首章两版各有三盏无阴影暖点光，范围 3.6 m，强度 0.68、0.68、0.81。三份正式场景的方向光 Cookie 槽为空，首章 Sun 槽为空但方向灯存在并启用。第二章正式场景的场景级序列化灯只查到谷口方向光。视线位置和灯光来自场景读取，机关连通与流程进度以关卡接线为准。[第二章实景与地标](../architecture/第二章_断衡悬道/05_美术场景搭建.md)

| 可复用项 | 现有控制和边界 |
|---|---|
| 太阳、环境和距离雾 | 现有方向光的颜色、强度与 Cookie 槽，`RenderSettings` 的 fogColor、fogMode、fogStartDistance、fogEndDistance 与环境三色。指数模式才读 fogDensity。标准 URP Lit 与显式使用 `ComputeFogFactor / MixFog` 的自定义材质能随雾变化。近景文字、透明件、专用 Unlit 路径逐材质确认。[RenderSettings API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendersettings) |
| 天空、雾卡与岩壁 | Awakening 的青灰薄云有 `_CloudMap`、`_WindDirection`、`_WindSpeed`，独立可借用 CirrusDensity 做云影原图。Courtyard 与正式第二章绑定另一个天空。庭院六处谷雾卡共享材质 `_Opacity=0.72`，透明且不写深度。Cloudline Granite 以世界 Y−17.5 到 +5.5 做渐隐，并在 55 m 后自算距离着色，无 `MixFog`。A/B 要单独核对它与雾卡、天空的接缝，避免把另一套雾再乘一层。[青灰薄云](../../Assets/Art/Environment/Opening/Skybox/青灰薄云.mat) · [天空 Shader](../../Assets/Art/Environment/Opening/Skybox/KaogongThinCloudSky.shader) · [CirrusDensity](../../Assets/Art/Environment/Opening/Skybox/CirrusDensity.png) · [山云天空](../../Assets/Art/Environment/Opening/CourtAtmosphere/MountainCloudSky.mat) · [谷雾 Shader](../../Assets/Art/Environment/Opening/CourtyardInfill/C01/ValleyCloud.shader) · [谷雾材质](../../Assets/Art/Environment/Opening/CourtyardInfill/C01/ValleyCloud.mat) · [Cloudline Granite](../../Assets/Art/Environment/Opening/CourtLayout/AtmosphericGranite.shader) |
| 地形与草 | ExteriorTerrain 自算天空间接项、主光与阴影，并行接入距离雾。包的 Lit Pass 有 `_LIGHT_COOKIES` 变体，自定义 ExteriorTerrain 未声明该变体，Cloudline Granite 的零参数 `GetMainLight()` 也未采 Cookie。Opening Grass 使用顶点色、风和距离雾，无 Lit 光路；`OpeningGrassWind` 已示范 `_OpeningGrassViewer` 的 CPU 全局发布与禁用归零，草块按位置裁掉。保留草的原风参数和采样距离，识别现有渲染器再决定是否加接收。首章有草资源和实例，第二章未查到该草风路径的正式接线。[地形 Shader](../../Assets/Shaders/ExteriorTerrain.shader) · [草 Shader](../../Assets/Shaders/OpeningGrass.shader) · [草材质](../../Assets/Art/Environment/Opening/Grass/OpeningGrass.mat) · [草风控制](../../Assets/scripts/logic/Environment/OpeningGrassWind.cs) · [现有风动调研](CHAPTER01_FOLIAGE_ASSETS_AND_WIND_20261123_ZH.md) |
| 渠水与湿石 | Awakening 的量水道、Courtyard 的量水道与入庭明渠已有 `CanalFlow`。可调整 `_WaterColor`、`_SkyWaterColor`、`_FoamStrength`、`_GlintStrength` 与湿石 `_Smoothness`，近景 Gauge 分支有逐眼观察向量、现成渠底几何和 128×128 烘焙 Cubemap。它走自算高光与反射，不同步接收方向光 Cookie 或自动重烘倒影。正式第二章的场景及环境 Prefab 未查到水面。[渠水 Shader](../../Assets/Shaders/CanalFlow.shader) · [湿石](../../Assets/Materials/Chapter01/CanalWetStone.mat) · [庭院渠水](../../Assets/Art/Environment/Opening/CourtLayout/庭院碧水.mat) · [水尺水材质](../../Assets/Art/Environment/Opening/CourtLayout/量水道碧水1.mat) · [反射烘焙](../../Assets/scripts/logic/Editor/Environment/CanalReflectionBaker.cs) |
| 玩法显示与全屏先例 | `PlayerHitVignette` 用玩家相机过滤、头部网格和材质属性块，加载蒙版由场景切换脚本生成。当前确认的移动 Renderer 没有通用 Full Screen Pass Feature。腕匣衬线、成印和教学图走各自材质，应纳入所有 A/B 的读字检查。[受击边缘](../../Assets/scripts/ui/Rpg/PlayerHitVignette.cs) · [加载蒙版](../../Assets/scripts/logic/Environment/SceneLoadingOverlay.cs) · [腕匣衬线](../../Assets/Art/VFX/Shaders/WristGaugeInlay.shader) · [成印](../../Assets/Art/VFX/Skills/SealConfirmation/SealGlyph.shader) · [教学图](../../Assets/Shaders/TeachingSealPhoto.shader) |

## 三种实现及取舍

| 实现 | 适合本项目的用途 | 调用成本和作用范围 |
|---|---|---|
| 场景参数 / Shader 全局 | 优先复用 RenderSettings 与原有光。若共享云影强度、纹理或相位有价值，可由关卡根发布带项目命名空间的全局向量/纹理。 | 改参数本身不新增 Pass。Unity 的 Shader 全局值会被材质同名属性覆盖，具体效果取决于 Shader 的读取与运算。不要与草风现有属性复用相同名字。[Shader 全局参数](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/shader/setglobalvector) |
| 材质 / Shader | 远岩空气色、石面云影、渠缘局部湿带。环境材质依原绘制路径加世界坐标采样或顶点色权重。 | 仅覆盖接入对象，可明确排除 UI 与角色。先在一个地面材质或已有自定义地形试一个低频样本。整体换成新 Lit 变体会影响光照、烘焙、阴影和材质批次，需逐材质验证。 |
| 全屏 Renderer Feature | 可作 PC 上确有需求的取景比较。运行时受击边缘已有局部实现。 | Color / Depth / Normal / Motion 与 Fetch Color Buffer 都须按实际需要声明。屏幕颜色读取常引入中间纹理、复制或 resolve。先看实际 Pass 与 Render Graph。双眼屏幕资源使用 URP 的 XR 纹理和采样辅助，并按 Render Graph 编写。需要拷贝时用 Render Graph / Blitter 的对应入口。旧 `cmd.Blit` / `Graphics.Blit` 有 XR / Native Render Pass 兼容问题。[全屏 Feature 选项](https://docs.unity.com/en-us/engine/6000.3/manual/post-processing-and-full-screen-effects/urp/custom-post-processing/renderer-feature-full-screen-pass) · [URP Blit](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/customizing-urp/blit-overview) |

- **体积雾与全屏泛光暂缓。** 雾先由现有远景分层完成。透明雾卡虽轻于启用全套屏幕效果，重叠仍消耗像素和带宽。徽记亮部先用局部发光材质。PC 的 HDR 和 SSAO 可作为单独平台对照，避免移动头显基线附带变化。Unity 的独立 XR 指南说明，tile GPU 需为中间目标的额外读写、后处理采样及拷贝付出带宽。双眼在 SPI 下仍各有像素负担，MSAA resolve 也要按捕帧检查。[Unity 6.3 独立 XR 指南](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization)
- **全屏描边暂缓。** 深度或法线边缘会同时放大篮索、符纹、植被和手指轮廓，近处容易闪，远处会抢地标。任务交互继续使用已有图形与局部提示。SSAO 替代手段优先用现有贴图或顶点接触色，保留第二章栈道边缘对比。
- **湿润与水线保留局部。** 首章可在水尺、明渠湿石材质上加低频深色与轻微光滑度变化，优先用现有网格边界、UV 或顶点色。避免用单一场景水位切过首二章不同标高，避免为折射打开 Opaque / Depth Texture。水面采样第一轮沿用现有流纹与烘焙倒影。

## 两个可操作样件

### A/B 1 远山空气透视与光色

- A 为现行各章。B 首先只调远段线性雾和现有环境、方向光，保留首章暖点光。分两次切换记录雾与灯各自影响。庭院让近端仍干净，中后段石青向远山收拢。悬道让背后的远峰降低饱和与对比，衡梁木段、铜箍、支点塔仍落在可辨层；正反两向和谷底仰视都能看到连续篮索。
- 首次试 A/B 时，把第二章起雾距离保留现值 70 m，优先调远端雾色与尾段。若跨度 120 m 的梁端在斜视下被吞掉，改用远景岩体材质分层，保持可达路线、衡梁和篮索使用温和的基准雾。首章天空、Cloudline、雾卡与渠水倒影的各自接线可独立回退。
- 室内外以既有苏醒室门洞、开敞廊下和院内为范围。首轮设定场景雾和主光后，室内沿用现有局部光与材质。若还需连续室内外调色，再用挂在关卡根的区域预设与头部/脚下区域判断。房门双向缓变，不按朝向改变环境值。首轮不动态重烘环境反射或 Lightmap。腕匣、文字、手印与机关发光沿用各自材质。

### A/B 2 低频云影

- 最小开关 A 为原方向灯 `cookie=null`。B 给原灯加一张从 CirrusDensity 派生的灰度、低频纹理，先静态。试参可从约 6%–12% 的直射项调制开始，保大块、高底值，禁细碎高对比。逐项看一块 `CourtyardStone` 与一块外屋顶材质，第二章可用 `WorkshopPaving` 做接收验证，但首轮范围仍取庭院。[庭院铺石](../../Assets/Materials/Chapter01/CourtyardStone.mat) · [工坊铺装](../../Assets/Materials/Chapter01/WorkshopPaving.mat) · [Unity 6.3 Cookie](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting-light-sources/cookies)
- Cookie 测得可读后再决定是否移动。若需范围约束，Cookie 可用院外淡为白的单图并用实际投影范围验证，或改成仅几个环境材质采样固定世界 XZ 的低频纹理。不要为做滚动而旋转太阳或改几何投影。手、腕匣、铭文、渠水、发光机关必须逐项检查。开启 Rendering Layers 仅表示管线支持，也需核对当前灯层共用关系；直接改原光的层会同时改基础受光。烘焙间接/Lightmap 与 Unlit 可能观察不到 Cookie，ExteriorTerrain、Cloudline、草和渠水按上表分别处置。
- 若需要精准控制室外接收面和淡出，优先选环境材质分支。世界空间锚定、接收遮罩、一个可清零的强度参数。新路径范围限在选定室外石和屋面，当曝光跨门转换时不用把云影衰减传给近景标记。高频呼吸、相机绑定云影和全屏投影不进入首轮。

### 进入、退出、重进与叠章

设一个环境状态所有者，以当前有效关卡为准。首章出苏醒室进入庭院才进入云影范围，回屋或返回菜单还原。二章预设只在断衡悬道激活后发布，衡梁通过不依赖动态雾取得可见性。跨章先在加载罩内清旧全局，再应用目标场景基线。单场景卸载、禁用、直接开章、异常退出、无特效测试场和编辑器关闭 Domain Reload 时都应归到身份值，云影强度归零且使用白色中性纹理。若以后变为 additive，`SetActiveScene` 决定场景灯光设置来源。加载完成后再确认活动场景和环境所有者。采用单一所有者和显式交接，只有当前所有者能清自己发布的状态，避免旧场景晚卸载把新章参数覆盖。累加挂载以检验机制，现有跨章生产流为 Single。[Active Scene API](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/scenemanagement/scenemanager/setactivescene)

## 验收与上线判据

1. **静态和桌面。** 在固定机位从纯 A 开始，每项开关单独测。A/B 同一提交、相机、敌人数、植被和粒子负载。Render Graph Viewer / Frame Debugger 按眼或 XR Pass 查看有没有新增 Color/Depth Copy、Resolve、Depth Normal Prepass 或中间颜色图。镜头位置依次为苏醒室门内门外、天仪阁校星案和水尺低头位、断桥俯视、上层远望衡梁、篮内看索和谷底仰视。PC 镜像可录图，移动 Player 的 Pass 数和头显双眼另验。
2. **XR 正确性。** 新自定义 Pass 走实际 SPI / Multiview 路径，通过 instance 与 stereo 宏建立对应眼索引，读深度/颜色用 URP XR 数组采样辅助，按实际眼的矩阵重建坐标。世界云影用稳定的世界坐标；区域触发与距离裁剪共用头部中心，避免左右眼处于不同状态。在单眼遮挡、转头偏视、跨门、连续穿门和 A→B→A 重载检查单眼变黑、错眼纹理、边缘跳变及状态残留。多通道可辅助定位，验收以目标单通道为准。[Unity 6.3 SPI 自定义 Shader](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/stereo-rendering/single-pass-instancing)
3. **真机可读性。** Quest 3 与可用的 PICO 分开记录。每个固定站位以同一随机顺序给 A/B 样本，戴机从正常腕距读准望匣状态、辨成印和教学字，在上/中/下层按要求指出梁端、支点塔、篮索和桥口。B 不增加误读或需要凑近的次数，远景至少在两处同机位获得可重复的层次改善。左、右眼和眼缘视野均检查，云影版要另查水面和石面光色脱节。
4. **真机帧时间。** 同设备固定刷新率、实际眼缓冲、动态分辨率、注视点级别、补帧与 Graphics API，记录温度和 CPU/GPU 频档。每天先做 A/A 确认量测噪声，再按 A/B/B/A 至少三轮，每轮关键站位停留 30 秒并走完路线，另做一次热态循环。Quest 取 OVR Metrics 的 App FPS、App GPU Time、Stale Frames 与连续 stale；PICO 取设备监控或 Metrics HUD 的可用项。HUD 只有利用率时，另用可用的 GPU 帧时间采集工具补采，不能用百分比换算时间。保留 Unity Profiler 的主/渲染线程数据。同机记录 P50/P95/P99、载入尖峰与连续漏帧。72 Hz 帧周期约 13.89 ms，90 Hz 约 11.11 ms。沿用先前项目提出的约 20% 余量预警线，分别观察 CPU 关键线程和 GPU P95 是否约 ≤11.1 / 8.9 ms。每项效果暂定 **GPU P95 增量 ≤0.3 ms** 且无新增连续漏帧，增量须能与 A/A 抖动区分。它们都是本项目拟定评审阈值，当前无官方 Shader 耗时或本轮头显结果。只有上限也成立且可读性过关才进入正式实施。[历史项目阈值](CHAPTER02_VERTICAL_VISIBILITY_QUEST_PICO_20261122_ZH.md) · [Meta 指标定义](https://developers.meta.com/vr/documentation/unity/ts-ovrstats/) · [PICO 设备性能监控](https://developer.picoxr.com/document/unity/monitor-device-performance/)

早期资料记录第二章尚无正式场景，系 11 月 22 日快照。截至本次，构建列表、残规门庭目标、正式悬道和独立远山预览已静态核对。移动端实际 Pass、双眼、机型兼容及开关的性能差值待原型与真机验收。
