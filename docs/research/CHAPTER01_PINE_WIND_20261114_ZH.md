# 首章山松低开销风动方案

核查日期 2026-11-14（Asia/Shanghai）。状态为调研完成，待制作样板和验证。目标是在东西坊支路旁看见山松冠梢随风轻摆，同时保持根盘贴地、主干沉稳、原有山松的光照与层岩色调。设备条件沿用风动草的 Unity 6000.3.10f1、URP 17.3、OpenXR、Android Vulkan Forward 与 Quest/PICO 目标。

## 首轮取舍

**先在东侧支路旁的一棵独立山松上做顶点风动样板。** 在保留原有网格档位和烘焙图集的前提下，用一个连续权重控制从树根到冠梢的侧向位移。近档主干底部固定，主干上段与冠层一起轻弯，中档在可见切换处接续，远档保持静止。先做同机位和头显对照，再决定是否扩展到合批树带。

| 候选 | 在本项目的取舍 |
|---|---|
| 保持静态 | 用于实测基线。远处剪影保留这一条路径。 |
| 围绕树根旋转整个模型 | 验证场景风格时可以快速试摆，但同幅转动会让根爪和树干像一整块在转。对已经合并的树带，还需要把每棵重新拆成可独立旋转的对象。 |
| 根部固定的顶点弯曲 | 推荐。复用当前每档网格与同一批材质采样，通常不必增加颜色绘制次数。需要顶点权重、包围盒和正确的阴影顶点位移。 |
| SpeedTree | Unity 的 SpeedTree 导入器支持按档设置风动质量，可作为以后新树种的候选。现有山松是普通 FBX 与合并网格，现成的导入器档位无法给这些资产补齐风动数据。 |

Unity 推荐移动 XR 用 Vulkan、OpenXR、Forward 和双目单通道，并避免几何着色器等使瓦片式 GPU 付出额外开销的路径。本项目首轮使用普通网格和 Shader 顶点阶段。[Unity 一体机 URP 优化](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization) · [SpeedTree 每档风质量](https://docs.unity3d.com/ja/6000.0/ScriptReference/SpeedTreeImporter-windQualities.html)

## 正式资源和最低改动范围

- 首章外景有独立山松，以及自然区块中合并的山松图集网格 `NatureBlock*_LOD*_M3`。远处另有程序化枝干和松针灌苔网格，两者还承载其他植物。独立树样板用它自己的 LOD 网格与材质实例。若以后给 M3 树带加风，在烘焙时保留 `Piece.p`、稳定种子和植株高度，再将各树顶点与根锚同时转换到块局部空间。现有 `BakedPine` 合并保留位置、UV 和法线，当前输出没有逐树根锚。[外景指南](../guides/CHAPTER01_EXTERIOR_VISUALS_ZH.md) · [独立模型 LOD](../../Tools/Environment/ExteriorWorldLOD/WorldExteriorAuthor.cs#L167) · [自然区块网格生成](../../Tools/Environment/ExteriorWorldLOD/NatureBuilder.cs#L216) · [山松合并来源](../../Tools/Environment/ExteriorWorldLOD/NatureBuilder.cs#L273)
- 当前山松材质使用不透明 URP/Lit，绑定底色、法线和遮罩，保留现有 UV0、法线、切线、贴图属性、色调和雾。遮罩同时被金属/光滑度与遮蔽槽位引用，实际采样量应以编译与捕帧确认。首轮只替换从顶点到各 Pass 的位置计算，不另加叶片噪声纹理、贴图或透明采样。[山松原材质](../../Assets/Art/Environment/Opening/MountainPine/MountainPine_Baked.mat) · [项目移动渲染配置](../../Assets/Settings/Mobile_RPAsset.asset)
- 首轮以一棵独立山松的近、中档为上限。后续若扩到 M3，当前五个 LOD0 的 M3 网格合计 42,102 顶点、18,216 三角面。例用一个 Float32×4 通道，单算这五档的新增原始顶点数据约 658 KiB，另有中档、导入与驻留开销。实测应按可见网格及实际 Pass 执行量计算，不用资产总量推断 GPU 毫秒。[发布的自然区块网格](../../Assets/Art/Environment/Opening/NatureLOD/)

## 可实施规格

1. **烘焙权重。** 首轮取 UV0 之外经 Mesh 通道检查确认为空的槽，优先避开烘焙照明常用的 UV1/UV2，例如使用 UV3（`SetUVs(3, …)`，Shader `TEXCOORD3`）。写入块局部根锚 X、Z，以种子确定的相位和弯曲权重。独立山松根锚取实际落地的根盘，按离根高度给主干和冠层连续权重。以实际包围盒验证高度范围，根盘及最下部树干权重严格为零，冠梢逐渐至一。在 UV 缝和材质边界上，重合位置写入同一权重。近、中档从同一树源坐标和根锚生成权重，以免同一树在切档时突然换相位。[Unity Mesh.SetUVs](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/setuvs)
2. **风动。** 取 `w = smoothstep(0.15, 0.95, normalizedHeight)^2` 作为起点。每顶点用一次正弦计算 `offsetXZ = windDirectionXZ * amplitude * w * sin(t * 2π * f + phase) * fade`。使用全场同一风向和时钟，相位按树稳定。先试冠梢横移 4–8 厘米、频率 0.25–0.4 Hz。光照方向先沿用现有法线和切线，再对照树冠高光是否滑动；若高光变化突出，优先收敛摆幅，仍有明显问题时用同一变形梯度带动法线与切线。两眼共享头部中心，按同一根锚距离计算衰减；顶点着色器仍需按双目宏输出各自的投影。现有草地 Shader 可参考双目与风向接线。[Unity 自定义 Shader 双目单通道](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/stereo-rendering/single-pass-instancing) · [项目草地 Shader](../../Assets/Shaders/OpeningGrass.shader)
3. **切档和裁剪。** 保留现有硬切 LOD 与投影比例。近、中档共用振幅曲线、相位和距离衰减，并以实际 Android 质量档走位验证中远切换。可先试 10 米内完整、10–22 米连续减弱，再按支持的镜头和质量档中最早切入静态档的距离调整，确保切换前摆幅已接近零。如果可能在衰减尚未结束时切到远档，优先缩短风动作用距离，再评估是否让交界档共享位移。远档使用静态 Shader 以省去风动顶点计算。按 8 厘米最大位移，样板网格在 X/Z 正负方向各预留至少 10 厘米，并分别验证变形包围盒和 LODGroup 尺寸。动态顶点保留局部根锚坐标，试验网格关闭会重写该坐标的几何静态/动态合批。共享材质常量放 `UnityPerMaterial`，从 Frame Debugger 检查 SRP Batcher 与实际绘制。[Renderer.localBounds](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderer/localbounds) · [SRP Batcher 着色器布局](https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/in-universalrp-srp-batcher)
4. **阴影和深度。** 独立树及合批山松的投影/接影按正式资源逐档检查。现有生成器为独立模型 LOD1 配置投影、合批 M3 在 LOD0 投影。样板保留对应档位的阴影，在已有 ShadowCaster Pass 使用同一个变形函数、同一个头中心和时间，再计算阴影偏移。如果有 DepthOnly / DepthNormals 实际参与相机路径，也共用变形。当前 Android 两个 SpaceWarp Feature 开关均为 0。若将来开启，须同时算上一帧风动位移并检查运动矢量和双眼重投影。项目的 URP 17.3 `LitForwardPass.hlsl`、`ShadowCasterPass.hlsl` 分别使用各自的顶点入口，原版 ShadowCaster 不会自动跟随新顶点位移。[独立模型 LOD](../../Tools/Environment/ExteriorWorldLOD/WorldExteriorAuthor.cs#L167) · [合批网格阴影](../../Tools/Environment/ExteriorWorldLOD/NatureBuilder.cs#L235) · [项目 OpenXR 配置](../../Assets/XR/Settings/OpenXR%20Package%20Settings.asset)

## 验收

- **桌面同机位**用东坊支路、近树根及略过视野边缘的角度。先对比旧版与新网格在零摆幅下的图像，排除 UV、光照和轮廓回退。接着固定时间拍两个反向风相位，检查根盘与主干下段、冠梢轮廓、阴影接触点、两档切换及最远静止，记录帧中是否增加 Renderer、Draw 或 Pass。至少在 Play Mode 做一段连续扫视。
- **Android**先编译草地方案所用的 Vulkan/OpenXR 双目路径，确认近、中档使用的颜色、投影及启用的深度变体都留在包内。随后在 Quest 与 PICO 实体机检查左右眼、快转头、跨 LOD 及长时间运行的帧时。平台文档列出 Vulkan XR 与测量方法，本项目仍需包和设备实测。[Unity 一体机 URP 优化](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization) · [Meta OVR Metrics Tool](https://developers.meta.com/vr/documentation/native/android/ts-ovrmetricstool/) · [PICO 性能采集](https://developer.picoxr.com/document/pico-cli/profiler/)
- 先跑 **A 原版树**、**B 新网格和同一风动变体、振幅为零**、**C 同 B 开启风动**。A/C 测整体变化，B/C 比较实际摆动对画面的影响。设零振幅可能仍执行正弦与顶点取数，若要测纯顶点逻辑成本，加一个编译时剔除位移计算但保留同样网格与材质的静态变体对照。草地固定同一状态，固定刷新率、渲染比例、MSAA、FFR、机位路线和采集方式，记录温度与频率。在各目标设备热稳后交替重复，记录应用 GPU 与主/渲染线程帧时 P50/P95/P99、stale/dropped frames、绘制与内存。先取得同设备基线波动和余量，再决定是否扩大覆盖。此前草地的 Vulkan 编译和原外景 Quest 采样属于各自既有结果，树摇首轮尚无编译、渲染或设备数据。[草地验证](../guides/CHAPTER01_EXTERIOR_VISUALS_ZH.md#预算与草地) · [既有外景真机范围](QUEST_PICO_EXTERIOR_PERFORMANCE_20261109_ZH.md)
