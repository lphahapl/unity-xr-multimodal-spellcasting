# 首章外景、连接路与性能配置

当前外景已于2026-11-09发布至正式工程。正式场景为 `Assets/Scene/Chapter01_Awakening.unity`，外景预制体为 `Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab`，实例名为“首章外景细化”。Quest实测范围与状态见本页“重建与验证状态”。

[玩家视点360°全景](../visuals/20261109_exterior_world_lod/01_panorama.png) · [展开预览](../visuals/20261109_exterior_world_lod/01_panorama_wide.png) · [全场俯瞰](../visuals/20261109_exterior_world_lod/world-overview.png) · [门庭](../visuals/20261109_exterior_world_lod/world-portal.png) · [校器工作台](../visuals/20261109_exterior_world_lod/world-workbench.png)。这些是Unity实际渲染，使用与发布版本相同的美术资产；不是头显截屏。

## 空间与美术

主屋使用直坡硬山屋顶、青灰石基、旧木直梁、哑光铜箍和白石嵌尺；保留原房间、门窗及教学布局。外围三座校器坊替代旧图生工坊的显示，每坊一张可近看的校器工作台。屋顶有向下的木底板和闭边，室内不再透出天空。旧 Workshop 实例仍以 inactive 保留；这不意味着旧贴图依赖已经从内存卸除。

西、东坊支路、门庭横接和门后北坊支路接入原步道。台基、岸基及支墩按实际三角网格地面落位；原连续侧栏只在指定两处开口，其余关卡碰撞与脚本保持。三坊有固定地板、墙体、门柱和工作台盒碰撞，视觉 LOD 不改变碰撞。

门庭中心 `(10,0,63)`、朝向 Y=180°，双门扇预置开启。`SceneTravelGate` 的 `destinationScene` 为空，`loadEnabled=false`，所以目前不会加载其他场景。以后将目的场景加入启用的 Build Settings，再配置目标并开启加载；组件只认头部进入体积，不因手或敌人进入触发。它没有自动接入新章节的存档、淡入淡出或返回规则。

测候天仪仅完整内环以 7°/秒绕自身斜轴转动；破碎外框、断轴与承托固定。运行组件见 [ArmillaryRingMotion.cs](../../Assets/scripts/logic/Environment/ArmillaryRingMotion.cs) 和 [SceneTravelGate.cs](../../Assets/scripts/logic/Environment/SceneTravelGate.cs)。

## 模型与真实 LOD

| 资产 | 三角形或结构 | 来源与限制 |
|---|---|---|
| 三段主屋、三坊及三张工作台 | 全近级 17,396 | 建筑程序网格，保留主要梁柱、粗檐和铜箍，小瓦棱/刻度交给图集 |
| 单张 A07 工作台 | 3,400 / 1,400 / 500 | 混元 V3.1 图生高模；静态 COLLAPSE 减面，各级重新展开 UV、分别烘焙至共享 1K 图集 |
| 自然物 | 8 个空间块，44 件；23,420 / 18,694 / 15,396 | 山松、闭合远冠、低灌、层岩、苔砾；不透明材质，分块合并 |
| 自然块内的单棵松源 | Blender 回读 2,022 / 1,750 / 1,484 | 保守减面，复用原 UV/法线图；不是重新烘焙。Unity 导入可能相差两面 |
| 原图生层岩 | 3,100 / 1,426 / 588 | 复用烘焙图集，三级实际减面 |
| 原三棵图生松 | 4,400 / 2,024 / 1,486 | 高级只在近处使用，保留原烘焙图集 |
| 门框 | 2,968 / 1,364 / 562 | 开启门扇另合为两种材质网格，触发器固定 |
| 八段山脊 | 每段 2,304 / 576 / 144 | 从同一源表面降采样 |
| 谷地 | 12,800 / 3,200 / 800 | 从同一源表面降采样 |
| 天仪 | 总计 1,800，其中完整环 288 | 小件保持单级；拆环、补固定支承，不旋转破损部分 |

50 万三角原高模仅在 ArtSource；运行“高细节”指上表 LOD0。A07 三档分别占共享图集的不重叠区域，各自按切线基底烘焙法线。历史 Rodin 工坊只作对照，本轮 A07 用混元，程序建筑与 M01/M02 材质没有冒称 Rodin 生成。详见 [资源拆分计划](../architecture/CHAPTER01_EXTERIOR_ASSET_PLAN_ZH.md)。

接地同时考虑谷地和各山脊独立选 LOD 的情况。山松除九点筛选外还检查三档实际根部顶点；低灌、苔砾和岩底也使用保守地表高度，避免近级地面降级后物件悬空。

## LOD 配置位置

最终阈值由 [WorldExteriorAuthor.cs](../../Tools/Environment/ExteriorWorldLOD/WorldExteriorAuthor.cs) 的 `TuneLOD` 统一设置，也可在生成后 Prefab 的对应 LODGroup 查看。

| 类别 | 选择用世界尺寸上限 | LOD0→1 / LOD1→2 / 最末级剔除 |
|---|---:|---|
| 山脊 / 谷地 | 12 / 16m | .90 / .10 / .0001 |
| 自然物 | 6m | .75 / .22 / .001 |
| 建筑 | 6m | .75 / .20 / .001 |
| 工作台 | 1.8m | .34 / .14 / .003 |
| 支路、台基 | 6m | .60 / .10 / .0001 |
| 其他三级道具 | 5m | .75 / .20 / .001 |

这些值是屏幕投影比例，不是固定米距。物体尺度、参考中心、视场和 lodBias 均影响实际切换；尺寸封顶只影响 LOD 选择，不缩小真实网格或碰撞。全部硬切换，不启用同时绘制两档的 CrossFade。Android 默认 Mobile/lodBias=1，PC 质量的结果不能直接当头显效果。

## 材质、灯光与水声

地形使用 [ExteriorTerrain.shader](../../Assets/Shaders/ExteriorTerrain.shader)：单不透明前向 Pass、一次底色采样、顶点级大尺度色差、主光/环境光/主阴影与雾。无额外灯光、法线图采样或附加相机。其 RidgeStrata、DistantStrata、ValleyMossScree 材质在 ExteriorLOD 目录；`_TextureMetres` 控制纹理尺度。M01/M02 源贴图是 AI 底色经周期化处理，法线/AO 由颜色派生，不是高模烘焙。

保留此前天空绑定与艺术参数、唯一太阳及雾设置；太阳强度 1.2、硬阴影 .7，雾距 45–185m。Android 仍使用现有 Forward、4x MSAA、HDR 关闭、renderScale=.8、单级 1024 主阴影/50m；没有因本轮把 PC 的 Forward+/SSAO 配置套到移动端，也不声称已开启运行时固定注视点渲染。

水面沿用 752 三角网格，[CanalFlow.shader](../../Assets/Shaders/CanalFlow.shader) 仍是一次 RGBA 采样、单不透明 Pass；没有屏幕折射/反射相机。`CanalFlow.mat` 的 `_FlowSpeed` 调流速，`_WaveHeight` 调起伏。`WaterAudio.prefab` 的单个空间声源沿渠寻找听者最近点，播放 18 秒循环，读取现有 DuckGain；未改技能音效接线。空间混音还需真人试听。

## 预算与草地

[几何预算 CSV](../../Logs/exterior-world-lod-20261109/integration/world-lod-budget.csv) 记录五个地面机位和俯瞰。当前五个 110° 单目 XR 代理视锥估算约 3.01–4.91 万三角形。它仅按包围盒/投影 LOD 统计整网格，不做遮挡或三角裁剪，不等于双目实际提交、draw calls、GPU 时间或设备帧率；inactive 资源也未计入此几何统计。

当前有限场景继续采用分块 Mesh+LOD，不为草地整体改成 Terrain。草地调研推荐稀疏不透明低模草簇、离线分块、根部固定的顶点弯曲和远处剔除；并非只旋转整片草。最多约 192 簇、每簇 18–24 三角形是试验起点，不是零开销保证。**草地研究已完成，但草地资产和风动 Shader 尚未制作/接入，也未做设备 A/B**，先测基线再定密度。见 [草地调研](../../Logs/exterior-world-lod-20261109/grass-research/调研.md) 与 [Quest/PICO 官方资料核查](../research/QUEST_PICO_EXTERIOR_PERFORMANCE_20261109_ZH.md)。

## 重建与验证状态

工具在 [ExteriorWorldLOD](../../Tools/Environment/ExteriorWorldLOD)。`sync_world.py` 只在重建前使用，会重置隔离工程的外景 Prefab；不要在最终审查/发布之间再次执行。依次运行 `WorldExteriorAuthor.Run`、`merge_world.py`、新 Unity 进程中的 `WorldExteriorAuthor.ReviewAndAccept`。最终发布用 `publish_world.py` 的哈希门槛，禁止整场 SaveScene 覆盖主工程。这些工具绑定本轮原始快照；发布后再次制作应新建任务快照并更新验收基线，不能沿用旧 proof 或绕过哈希保护。

正式发布已完成，[448个发布文件及逐文件哈希](../../Logs/exterior-world-lod-20261109/publication/publication.json)全部匹配验收副本。原场景仅修改四处启用字段：天仪旧 Renderer、原整段石坪 Renderer、左右两条旧侧栏 Collider；替代构造都在原外景 Prefab 内。此外补回“技能训练台”对既有 SkillAudioPlayer（fileID 991080001）的组件引用，消除恢复场景中的孤立组件导入/打包错误。原 168 个脚本序列化块逐字节保持，音频脚本、参数及存档未改。发布前主编辑器未播放、未脏、0错误/4既有警告，未调用SaveScene覆盖用户场景。

v11 [自动验收](../../Logs/exterior-world-lod-20261109/integration/world-parent-review.json) 1,968 项通过，候选导入错误/警告均为零；[父代理视觉验收](../../Logs/exterior-world-lod-20261109/integration/visual-acceptance.json)单独记录。运行组件真实 Play Mode 夹具 28 项通过；地形 Shader 的 Android/Vulkan/XR 变体 AssetBundle 编译通过。这些不是正式传送目标或完整玩法通过。“技能训练台”的组件列表修复单独验收，不删除其他会话的音频组件。

真机截图随后发现既有主相机覆盖 `m_ClearFlags=2`，使URP跳过天空绘制，而参考全景相机使用Skybox。v12仅把该覆盖改为1，复用原青灰薄云材质；其余场景字节、444项外景资产和天空资源均保持。新增[相机与天空检查](../../Logs/exterior-world-lod-20261109/sky-camera-fix/unity-acceptance.json)25项通过，[实际Quest双目截图](../../Logs/exterior-world-lod-20261109/quest/run-20261109-0452-sky-final/sky-device.png)已见云层。[单字段发布记录](../../Logs/exterior-world-lod-20261109/sky-camera-fix/publication.json)接续保留v11记录，不冒称重新执行了全部1,968项。最终场景SHA256为 `a5c26aae7858b8b7330a6838506951c4afc1a4442b86b89f72e2015c4bb52124`。

独立Quest测试包已成功构建并装入Quest 3，包名 `com.kaogonglvdu.chapter01.exteriorperf20261109`，Release/ARM64/Vulkan；[最终天空与扫视版构建证据](../../Logs/exterior-world-lod-20261109/quest-build/build-20261108-204916-5619830/quest-exterior-build.json)记录0错误、4条警告（Meta设置检查2条、OpenXR可选迁移2条），以及设置/场景/Gradle目录恢复。APK SHA256为 `56b61b2d078764dd74e7988b231781a5b5f53dc70362353354b14496a1480663`。未因本轮更改全局Meta设置，构建成功不代表商店检查清零。测试夹具仅在隔离包中，正式Assets没有自动传送探针。

修复Android构建时，只移除库Manifest上错误引用launcher资源的 `application android:label` / `android:icon`；应用名与图标继续由Unity生成的launcher提供，权限和启动Activity保留。构建使用隔离ASCII Gradle缓存并在finally恢复，未修改用户全局配置。[修复记录](../../Logs/exterior-world-lod-20261109/quest-build/manifest-fix.json)。

最终天空版已完成六机位图形采样：每处稳定5秒，以SmoothStep与Quaternion.Slerp平滑左右35°扫视12秒，再采静态60秒；庭院静态延长至1200秒，于2026-11-09T05:19:28+08:00完成并恢复。各处静态应用GPU周期样本P95为4.44–6.19ms，扫视最大样本6.27ms；庭院前/后五分钟GPU均值5.209/5.218ms。有效外景窗口的周期FPS均为72–73，但庭院有一条Stale=1，主循环最大间隔20.79ms，不能宣称逐帧零卡顿。两项发生时间不同，尚无因果结论。[分段统计](../../Logs/exterior-world-lod-20261109/quest-analysis/sky-final/summary.json)保留静态/扫视与边界筛选说明；[清理记录](../../Logs/exterior-world-lod-20261109/quest/run-20261109-0452-sky-final/cleanup.json)确认探针关闭、采集进程退出、临时近距保活撤销及本测试包停止。

最终外景采样全部原始headTracked为true，fallback未使用；但这是离脸图形夹具，冻结了章节和移动输入，不代表完整玩法CPU、真实交互或PICO通过。启动校准阶段出现57/36FPS；恢复原位置和业务组件后另保留32条短观察，最低53FPS，5条日志报告Stale>0、报告值合计25，因此完整玩法性能仍待验证。这些瞬态单独记录，没有从原始日志删除。纯色版在发现天空问题后取消，未完成20分钟，不作为严格A/B；此前普通启动66秒样本也只属于启动房间。详见[设备证据与边界](../research/QUEST_PICO_EXTERIOR_PERFORMANCE_20261109_ZH.md)。20261108版本的917项及旧预算只属于历史快照。
