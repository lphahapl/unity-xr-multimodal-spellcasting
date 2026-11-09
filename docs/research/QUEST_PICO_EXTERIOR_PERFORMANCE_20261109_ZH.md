# 首章外景：独立头显性能与 LOD 调查

- 任务：EXTERIOR-WORLD-LOD-20261109；核查日期：2026-11-09，Asia/Shanghai。
- 执行：Codex 内置子代理；实际服务模型标识未提供。已读 research skill；本次作为父代理分配的研究执行者，不再委派。
- 方法：实际 web 浏览官方 Meta、PICO、Unity 文档及官方组织源码；本机只读检查主工程配置和包源码。研究子任务只写 Logs 下 performance 目录，父代理经正式VRC复核后整理此页；未启动 Unity、操作浏览器焦点、修改配置、安装 SDK、打 APK 或测量头显。
- 机器可读明细、输入 SHA256、逐张贴图审计见 `../../Logs/exterior-world-lod-20261109/performance/audit.json`。以下是静态配置意图，不是已运行设备的有效参数证明。

## 当前结论

本轮制作前的外景基线尚无有效多级 LOD 的源码证据，新增内容应在交付前补 LOD，并保留独立固定碰撞。当前 Android 配置已有 Vulkan、Forward、4x MSAA、HDR 关闭、0.8 Render Scale 等移动端设置，但 FFR 只确认扩展已勾选，未找到项目代码开启强度，不能把节省量记入预算。PC 桌面图像使用另一套渲染路径，不能代替 Android 画质或性能验收。

建议以 **72Hz / 13.89ms** 为首轮稳定目标，目标设备支持且实测有余量后再验 **90Hz / 11.11ms**。时间是 `1000 / Hz` 推导的整帧间隔，不能分别给 CPU 和 GPU 各花一整份后相加，也不是所有余量都归游戏使用。这是本项目原生稳帧目标，不等同于商店最低门槛。父代理复核正式 VRC（页面更新于2025-10-22）：最低应用渲染率为60fps，交互应用刷新率为72/80/90/96/100/120Hz中的设备支持值；符合运动矢量要求的Application SpaceWarp有半刷新率渲染例外。本项目尚未核验AppSW启用，不能把例外记作性能余量。VRC要求按内容全长或45分钟中较短者测试。[Meta VRC.Quest.Performance.1](https://developers.meta.com/vr/resources/vrc-quest-performance-1/) 各设备推荐draw-call/三角范围还取决于状态切换和模拟负载，不能当稳帧保证。[Meta性能分析](https://developers.meta.com/vr/documentation/unity/unity-perf/)

## 主工程实际配置

| 项目 | 已核对事实 | 证据 |
|---|---|---|
| Unity / URP / OpenXR | 6000.3.10f1 / 17.3.0 / 1.15.1；Meta Core 207.0.1 | ProjectVersion、manifest、packages-lock |
| Android Quality | 默认索引 0 `Mobile`，指向 Mobile_RPAsset；编辑器当前索引 1 `PC` 不代表 Android | QualitySettings.asset |
| Android 图形 API | `15000000` 小端枚举 21，即 Vulkan；Automatic=0 | ProjectSettings.asset；Unity GraphicsEnums 官方源码 |
| Android 渲染 | Mobile_Renderer `m_RenderingMode=0`，Forward；无 Renderer Features；Depth Priming=0；Intermediate Texture=Auto | Mobile_Renderer.asset；已安装 URP 枚举源码 |
| 抗锯齿 / HDR / 比例 | Mobile URP MSAA=4，SupportsHDR=0，RenderScale=0.8；Quality antiAliasing=0 不能据此说 URP 没 MSAA | Mobile_RPAsset.asset |
| 纹理拷贝 / 后效 | URP Depth/Opaque Texture均关；主场景相机附加数据 PostProcessing=0。该附加组件处于既有 orphan 问题范围，运行时关联需另验 | Mobile_RPAsset、主场景原始YAML |
| 主阴影 | URP 支持主光阴影、1024图、单 cascade、50m；不支持软阴影、附加灯阴影；首章方向光实际序列化为 Hard、strength .7 | Mobile_RPAsset、首章 Light 块 |
| 额外灯 / 批处理 | 附加光 PerPixel，每物体上限4；SRP Batcher开，Dynamic Batching关；不把该上限当场景可见实时灯数 | Mobile_RPAsset |
| XR stereo | Android OpenXR `m_renderMode=1`，对应 SinglePassInstanced；旧 Player stereo=2仅辅助，不作为有效 OpenXR 模式证据 | OpenXR Package Settings、包内 OpenXRRenderSettings.cs |
| Render Graph | 当前 RenderGraphSettings `m_EnableRenderCompatibilityMode=0`，序列化意图为 Render Graph；顶层旧 `m_EnableRenderGraph=0` 为迁移遗留字段，不能反向误判 | GlobalSettings、URP Settings/RenderGraphSettings.cs |
| PC 差异 | PC URP HDR开、MSAA=1、RenderScale=1、4 cascades/50m；PC_Renderer 为 Forward+，且 SSAO Feature `m_Active=1` | PC_RPAsset、PC_Renderer |

实际相机来源是包内 `com.meta.xr.sdk.interaction.../Runtime/UnityXR/Prefabs/XROriginCameraRig.prefab`，其 Camera 允许 MSAA、HDR，并允许 XR 双眼；Android URP 的 HDR关闭依然有效。包中默认 far clip=1000，是否有场景覆盖需以最终实例读回为准。未找到 Assets 运行时代码调用 SetQualityLevel、改 renderScale/眼缓冲缩放或设置刷新率。Android URP质量覆盖优先于 Graphics 默认 PC管线；不能拿旧 Quality shadowDistance=40 替代 URP实际50m。

Unity 6.3 官方建议独立 XR 使用 Vulkan/OpenXR、单通道/多视图、Render Graph 和 Forward，减少深度/不透明纹理拷贝与 HDR。其 MSAA 平衡起点为2x；Meta 当前相机指南明确 URP 需手动设4x。现有4x符合Meta建议，若GPU不足应真机对比2x/4x，不宜机械改成一套“官方唯一值”。Unity6.3已支持部分 on-tile 后效，不能沿用旧版本“任何后效绝对禁止”的结论；本项目当前无开启它的需要。[Unity 6.3 untethered XR](https://docs.unity3d.com/6000.3/Documentation/Manual/xr-untethered-device-optimization.html)、[Meta camera settings，2026-11-06更新](https://developers.meta.com/vr/documentation/unity/unity-ovrcamerarig/)

## FFR 与跨设备风险

Android 的 Meta XR Foveation、Meta XR Subsampled Layout feature已启用；Unity通用 FoveatedRenderingFeature与眼动FFR feature关闭。OpenXR Foveation API=Legacy。已安装 MetaXRFoveationFeature 源码缓存强度初值0、动态标记初值0，OnSessionCreate只登记session；项目 Assets 未检出设置强度的调用。结论是**启用链未证实，实际设备状态未知**，不是“FFR实测已开”。Unity官方明确无论SRP或Legacy路线，勾选feature之后还必须运行时开启；Legacy只针对Quest，不能套给PICO。[OpenXR 1.15 Foveated rendering](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/manual/features/foveatedrendering.html)

manifest没有PICO SDK依赖，当前XR管理器只登记OpenXR loader。此事实不证明基础OpenXR不能在PICO运行，但不能宣称PICO专有FFR、刷新率、控制器/手追和渲染优化已经配置或验证。不要把PICO Connect/Live Preview或Meta Link的PC渲染结果列为standalone性能结果。

PICO官方 XR Profiling Toolkit 提供设备端自动路径、运行时FFR/MSAA/分辨率切换、数据导出；样例中的72FPS和4xMSAA属于它的测试环境，不是本工程预算。官方仓库仍描述Unity2022.3、PICO SDK3.1.2与Meta74，不能直接当Unity6.3/Meta207兼容保证；可借用测量设计，安装需父代理另外评估。[Pico-Developer/XR-Profiling-Toolkit](https://github.com/Pico-Developer/XR-Profiling-Toolkit)

访问限制：PICO文档页面 `open` 多次只返回导航壳，搜索引擎的官方页缓存能读到性能监控/验证正文，因此具体步骤以可完整读取的官方GitHub仓库补强。未用社区转述代替官方规范，也没有采用旧PICO固定三角形预算。[PICO Monitor device performance](https://developer.picoxr.com/document/unity/monitor-device-performance/)

## 制作前贴图与 LOD 基线审计

本节保留制作前快照，不代表最终场景。当前新增外景已有52个LODGroup，逐级数量和发布状态见[外景指南](../guides/CHAPTER01_EXTERIOR_VISUALS_ZH.md)。

扫描 Opening 目录PNG/JPG/TGA metadata，加上CanalRipples，共34张；34张mipmap开，31张明确Android override ASTC6×6，余下3张是TenonedBeam的BaseColor、MetallicSmoothness、Normal，Android override=0/Automatic，不能统称所有贴图ASTC。31张按图源大小、maxTextureSize、6×6块和mip链计算约 **19.61MiB**，仅是这些贴图的压缩块估算，不含旧横梁自动格式、Mesh、音频、RT、驱动对齐、加载驻留或构建实际重压缩。ASTC数值50在Unity官方枚举对应6×6，每块128bit。[Unity TextureImporterFormat源码](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/AssetPipeline/TextureImporterEnums.cs)、[ASTC6×6 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TextureFormat.ASTC_6x6.html)

Opening五份prefab和首章场景未检出class205 LODGroup；FBX importer的generateMeshLods均为0、lODScreenPercentages为空，已检查的屋瓦/其他独立Mesh资产m_NumLevels=1。仅有m_ForceMeshLod=-1等默认字段不代表存在多级几何。没有启动Unity导入，因此二进制FBX内全部节点不能在此审计中穷举；在该制作前快照中尚无可验收的LOD实现证据；本轮最终LOD与验收见外景指南。

建议实施条件（本项目建议，不冒充官方固定阈值）：

1. 新山体、岩石、树、工坊/屋瓦、远构件建立按资产或空间块的LOD0/1/2，给出真实逐级三角数；起始简化比例可试100%/40%/15%，再按轮廓和烘焙误差调。地标和山脊最远级保留可读轮廓；不要让谷地或远山整块突然消失。
2. LOD用投影视高阈值，根据真实眼高/视野/Android lodBias=1验；PC lodBias=2会延后降级。不能把固定米距当所有大小资产共享的切换规则。Unity的LODGroup就是按屏幕高度比例选择级别。[Unity 6.3 LODGroup](https://docs.unity3d.com/6000.3/Documentation/Manual/class-LODGroup.html)
3. 碰撞、触发器、目标标志、教学回调独立于视觉LOD，沿用已验收固定低复杂度碰撞；别把同一Collider复制进每一级。大网格按合理空间拆块以免一个角进入视锥就提交全部谷地，但同时记录Renderer/材质增加，LOD并不自动降低CPU提交数。
4. 第一轮可用FadeMode.None验几何收益，再在头显审视跳变；CrossFade会同时渲染相邻两级并可能增加片元/绘制峰值，不能只看常态LOD省量。透明树叶和自定义Shader需另验双目/过绘。[Unity LOD transitions](https://docs.unity3d.com/6000.0/Documentation/Manual/class-LODGroup.html)

## 用户后续取舍：Mesh地形、近距高精度与草地

用户明确允许为Quest/PICO性能降低外景面数，要求近处才使用最高级、稍远切中级。实施采用每类资产单独调LODGroup屏幕高度阈值及Object Size，保持碰撞/任务触发独立，不修改整个项目的Quality档。Unity官方说明缩小Object Size会把低精度切换推近相机；它不是统一固定米距，仍受相机视角、lodBias和参考中心影响。[Unity 6.3 LODGroup](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/lod/lod-group/class-lodgroup)

Mesh与Unity Terrain没有无条件的性能排名。当前首章是有限可达路径、分立山脊、固定桥台和窗外构图，因此选择继续用分块Mesh、三级LOD和单层简化地表材质；这是针对现有内容的工程取舍，未做Mesh/Terrain同画面真机A/B。Terrain自带地形精度、实例化、植被距离等控制，适合连续大地形；但默认多层材质、草木和阴影同样花费资源。URP Terrain每pass处理四层，更多层会增加pass。[Terrain设置](https://docs.unity.com/en-us/engine/6000.3/manual/creating-environments/script-terrain/terrain-other-settings)、[Terrain Layers](https://docs.unity.com/en-us/engine/6000.3/manual/creating-environments/script-terrain/terrain-textures/class-terrain-layer)

草地“程序化/模型/Shader”负责不同环节：程序化布置，网格提供轮廓，Shader着色和风动。批量生成或GPU instancing不等于免费渲染。Unity Terrain提供实例化网格和纹理草等方案，但本工程并未为草地转换到Terrain。最低开销基线是地表草色/苔斑；用户希望带动效，独立会话已完成比较，推荐稀疏不透明草簇、分段顶点弯曲、根部固定且向叶尖递增位移，避免整根刚性旋转；192簇、每簇18–24三角是实验起点。见[草地调研](../../Logs/exterior-world-lod-20261109/grass-research/调研.md)。资产与风动Shader尚未接入，等待设备基线后再决定密度。未把推荐面数/距离当官方预算，也未把调研当运行实测。[Unity 6.3草与细节](https://docs.unity.com/en-us/engine/6000.3/manual/creating-environments/script-terrain/terrain-grass)

## 必须留下的真机验收

先分别导出Quest与PICO目标设备的standalone APK，在固定平台配置、设备型号/OS/刷新率/眼缓冲尺寸下，重复门槛、庭心、水道、门庭、回望和最高可到视点；记录冷启动与热稳态、转头/移动、战斗特效、语音识别时的CPU main/render、GPU时间、掉帧/合成、真实draw calls/SetPass、各LOD及过渡峰值、纹理/总内存。把开关LOD、阴影、2x/4xMSAA、FFR强度作为一次一变量对照，不直接改共享PC管线。

前轮camera-statistics里的43844–55398地面视角三角及俯瞰144395是**历史视锥整网格估算**，不扣遮挡、不裁三角、不计实际双眼提交与阴影pass，也不是draw calls。UnityStats离线返回0不等于零绘制。当前审计没有重新测这些数量，不能据其低于某厂商示例预算就声称72/90Hz通过。未解决项继续是EXTERIOR-QUEST；本调查不关闭历史性能、音频或孤立脚本问题。新增外景与LOD的最终数量、图像与验收状态，以[外景指南](../guides/CHAPTER01_EXTERIOR_VISUALS_ZH.md)为准。

## 本轮 Quest 3 启动实测（2026-11-09）

Quest 3（eureka）通过 USB/ADB 实际运行独立 Release 包 `com.kaogonglvdu.chapter01.exteriorperf20261109`，Unity 6000.3.10f1、IL2CPP/ARM64/Vulkan，Mobile 质量，实际显示刷新率72Hz。没有覆盖现有游戏包或清除其存档。设备中的 `com.oculus.ovrmonitormetricsservice` 实际为 `1.0-stub`，因此没有启用或生成 OVR Metrics CSV；本轮 GPU 数据来自实际 VrApi 日志，不能将不支持的 XRDisplaySubsystem GPU 查询零值当作有效时间。

普通首章启动样本来自 `2026-11-09T03:44:25+08:00` 至 `03:45:30.999+08:00`，共66条周期日志；包内没有启用自动机位夹具，已排除此前麦克风权限弹窗。设备离脸且朝向倾斜，画面主要是初始房间，不是整片外景或完整游戏负载。原始数据和显式PID/时间筛选结果见 [启动统计](../../Logs/exterior-world-lod-20261109/quest-analysis/normal-startup/summary.json)。对应APK SHA256为 `d1fc80d26773df5f30313315b1a894ad8487dc872aff7fe3a57d06e72aa9fb0f`。

| 指标 | 实测值 | 解释 |
|---|---:|---|
| 应用 FPS | 均值72.03，最小57，6/66条低于72 | 首条57保留，不能只报稳定段 |
| 应用 GPU 时间 | 均值6.78ms，样本P95 7.33ms，最大7.42ms | VrApi周期样本，不是逐帧P95/P99 |
| TimeWarp GPU | 均值1.01ms，样本P95 1.33ms | 合成器开销单独记录 |
| Render/RHI开始至GPU完成 | 均值12.17ms，样本P95 13.17ms | CPU/GPU合并区间，不冒称CPU帧时 |
| 电池温度 | 27→30°C | 不是芯片结温或热稳态验收 |
| 进程内存 | PSS约966MiB、RSS约1079MiB | 单次dumpsys快照，非峰值 |

04:02独立夹具尝试使用新Input System读取头部状态，实际XR、双目和焦点为true，Position/Rotation等有效位为15，但 `headTracked=false`；90秒后按设计退出并恢复。没有进入六个外景机位，因此该次启动帧率不作为外景测试。用户说明环境为暗光；暗光与推算姿态是可能原因，当前证据不足以断定硬件或业务输入脚本故障。有效位不等于充分追踪，不能据姿态微动改写追踪值。

离脸静态外景夹具显式区分图形采样与佩戴交互测试：原样保留追踪字段，实际XR/双目/焦点、有限且持续更新的有效姿态作为图形采样门槛；它冻结章节/移动等输入逻辑，所以不能用于确认正常游戏CPU、手势、舒适性或完整通关。用户已明确同意脚本传送采集。PICO尚未连接实测，不能从Quest结果直接签署PICO性能通过。

## 最终天空版：六机位平滑扫视与庭院20分钟

2026-11-09实际完成，独立包PID6874，APK SHA256 `56b61b2d078764dd74e7988b231781a5b5f53dc70362353354b14496a1480663`。正式场景SHA256为 `a5c26aae7858b8b7330a6838506951c4afc1a4442b86b89f72e2015c4bb52124`。前一次真机发现主相机SolidColor覆盖使现有天空没有绘制；v12只把clearFlags改为Skybox，复用用户已有薄云素材，Unity25项与真实双目截图确认。纯色版在庭院不足20分钟时取消并恢复，保留为历史对照，不与最终版混合，也不是受控A/B。

设备使用Mobile、Vulkan、真实72Hz双目、1344×1408眼缓冲及0.8渲染比例。每点稳定5秒，12秒内按 `0→-35°→+35°→0` 执行SmoothStep与Quaternion.Slerp，再静态驻留；前五点各60秒，庭院1200.000秒于05:19:28.723+08:00完成并恢复。六处传送、六个PAN_DONE、六个POINT_DONE、COMPLETE、RESTORED及全部15项完整性条件均满足，无中断、取消、失败。全部采样原始headTracked=true，未使用允许推算姿态的fallback。

| 机位 | 静态native样本 | 静态App GPU样本P95 ms | 扫视GPU样本P95 / 最大 ms | 扫视主循环最大间隔 ms |
|---|---:|---:|---:|---:|
| 门槛 | 59 | 6.191 | 5.971 / 5.980 | 15.640 |
| 门庭 | 59 | 4.743 | 4.984 / 5.020 | 15.599 |
| 水道 | 59 | 5.140 | 5.040 / 5.040 | 15.348 |
| 苏醒室窗 | 59 | 5.242 | 6.198 / 6.270 | 16.068 |
| 试炼间窗 | 59 | 4.440 | 4.913 / 4.940 | 15.462 |
| 庭院 | 1199 | 5.330 | 5.342 / 5.400 | 15.402 |

每处PAN含12条Probe窗口，边界保守筛选后10条native样本。所有12个有效阶段的native周期FPS均为72–73；显示刷新率仍是72Hz，73是日志计数结果，不代表切到73Hz。苏醒室窗扫视的GPU峰值明显高于其静态采样，说明仅看正前方会低估该视角的变化；这12秒有限样本不签署全部方向或连续行走的最坏负载。

庭院05:06:01.056有一条 `Stale=1`，其它有效阶段Stale为0；这只是原始报告值，不用它重建逐帧丢帧总量。庭院主循环间隔最大20.793ms在05:12:01.284，时间与Stale事件不同，没有因果证据。主循环间隔不是CPU耗时；此峰值高于72Hz单周期13.889ms，所以不能称“零卡顿”。App GPU、TimeWarp与CPU&GPU组合区间分开保存在[完整分段JSON](../../Logs/exterior-world-lod-20261109/quest-analysis/sky-final/summary.json)，不把组合区间冒称CPU main/render帧时，也未测真实draw calls。

| 庭院比较窗口 | native样本 | GPU均值 / 样本P95 ms | 电池温度均值 / 范围 °C | CPU / GPU MHz |
|---|---:|---:|---:|---:|
| 前五分钟 | 299 | 5.209 / 5.311 | 35.344 / 35–36 | 1382 / 456 |
| 后五分钟 | 299 | 5.218 / 5.340 | 34.452 / 33–35 | 1382 / 456 |

前后GPU均值差约0.009ms，没有观察到持续恶化；两个窗口CPU/GPU档位均2/2、PLS=0。设备此前已运行多轮且接电，这不是受控冷启动或芯片热稳态证明。结束dumpsys单次快照PSS约1012.90MiB、RSS约1118.64MiB，电池100%/35°C；不是内存峰值或结温。

### 阶段外瞬态与结论边界

原始日志完整保留启动/校准阶段04:52:47的57FPS/Stale56、04:52:48的36FPS/Stale68。恢复原位置和业务组件之后，05:19:28.724至05:20:00.847的32条日志另行分析：最低53FPS（05:19:30.114，Stale21），3条低于72，5条Stale>0、报告值合计25；应用GPU均值7.034ms、样本P95 7.376ms、最大7.54ms。该短窗口没有继续Probe姿态/焦点验证，不能当正常玩法验收或归因于某个业务模块。[恢复后独立统计](../../Logs/exterior-world-lod-20261109/quest-analysis/sky-post-restore/summary.json)。边界由运行事件预先划分，没有事后删除坏帧。

本轮结论限于：当前外景在这组Quest 3图形视角下具备GPU余量，连续扫视没有持续掉速，庭院20分钟负载未出现趋势性恶化。启动、恢复、完整业务运行及真人移动/战斗并发仍需专项；PICO尚未实测。此前EXTERIOR-QUEST拆为已完成的外景图形基线与仍开放的EXTERIOR-QUEST-FULL，不能据此关闭音频混音或手追问题。

测试已经[清理](../../Logs/exterior-world-lod-20261109/quest/run-20261109-0452-sky-final/cleanup.json)：恢复origin/原启用状态，关闭本包探针，采集与其logcat进程退出，发出并成功返回近距自动化恢复广播，最后只停止独立测试包。没有覆盖现有游戏、清存档、强改刷新率/边界/散热设置；未声称观察到设备已经休眠。OVR Metrics仍为stub，未启用不存在的CSV功能。分析器保留原始真假字段与不支持GPU查询的无效状态。
