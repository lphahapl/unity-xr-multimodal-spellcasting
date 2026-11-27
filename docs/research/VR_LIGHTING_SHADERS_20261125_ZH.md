# VR 游戏光影调研：烘焙阴影、金属高光和局部实时投影

核查日期 2026-11-25。范围为《考工律度》Chapter 01 和 Chapter 02 的环境、机关及交战光影。这份调研补充此前的远景雾与云影方案。建议先做**首章檐下烘焙光影**，再做**二章衡梁旧铜的局部反射**。首轮用现有 URP Lit 就能证明两项价值。需要补自定义 Shader 时，只在关键材质上试受光约束的高光。[上轮全局环境 Shader 调研](GLOBAL_SHADER_VR_20261125_ZH.md)

## 其他 VR 游戏怎么做

| 作品与平台 | 开发者披露的做法 | 可借到本项目 |
|---|---|---|
| **《蝙蝠侠：阿卡姆之影》· Quest 3 系列** | Camouflaj 用 Unity 光照贴图处理静态环境，按贴图主入射方向算高光，再用漫反射亮度衰减暗处高光。角色通过光照探针受光，个别位置可覆盖探针。还用了 Subtractive 混合光照、投影体积阴影，以及以反射探针为底的反射方案。其投影体积阴影会采深度、重建位置再混合画面。 [Camouflaj 官方演讲，12:39–26:08](https://www.youtube.com/watch?v=mz_SZe6i6WA&t=759s) · [Meta 技术记录](https://developers.meta.com/vr/blog/gdc-2025-design-strategies-considerations-tips-mr-vr-horizon-os/) | 先复用静态烘焙、简单探针和高光遮罩思路。深度投影体积及屏幕反射代理留作参考，当前移动档没有为它们申请屏幕深度或颜色。 |
| **《行尸走肉：圣徒与罪人》· Quest 2 / 3** | Skydance 的 Quest 2 基线使用单通道前向渲染及前向雾、色彩校正。2024 年发布的 Quest 3 更新给手电筒额外加了动态阴影。开发者演讲区分 Quest 和 PS VR2 档。 [Skydance GDC 2024](https://www.gdcvault.com/play/1034902/Future-Realities-Summit-Next-Gen) · [Quest 正式更新说明](https://www.meta.com/experiences/the-walking-dead-saints-sinners/2897337400373711/) | 把实时投影预算优先给玩家需要判断的动作。院门、开合机关和活动梁可以沿用现有主光投影。掷火残机掠过墙面的火核光可单测少量无阴影补光。 |
| **《阿斯加德之怒 2》· Quest 2** | 负责构图与照明的关卡作者公开了沉船解谜区灯光与 Lightmap，说明最终选型受 Quest 2 约束。证据覆盖该关卡，详细烘焙设置未公开。 [Celeste Gamble 的关卡拆解](https://sandboxalchemy.artstation.com/projects/1xDqBX) | 先把天仪阁、锻造工位等常被近看的区域烘扎实，按实际观察距离给贴图分配密度。 |
| **《红色物质 2》· Quest 3** | 开发者访谈确认增强版加入动态阴影，也提升了纹理分辨率与过滤。公开资料未列正式版阴影覆盖和帧时。 [Vertical Robot 的 Meta 访谈](https://www.meta.com/blog/best-on-quest-3-red-matter-2-walking-dead-saints-sinners-espire-2/) | 近看质感要一起检查表面采样、微法线与高光稳定性，单独把阴影打开无法解释整体差异。 |
| **《The Lab》· PC VR** | Valve 公布了实际使用的定制前向 Renderer、MSAA 和 Adaptive Quality。可同时处理多盏投影灯，来源是旧 Unity 的 PC VR 实现。 [Valve 源码](https://github.com/ValveSoftware/the_lab_renderer) · [Valve GDC 演讲](https://media.steampowered.com/apps/valve/2016/Alex_Vlachos_Advanced_VR_Rendering_Performance_GDC2016.pdf) | PC 档可单列高质量影子对照。Valve 的灯数和预算对应其平台及实现。 |

## 这个工程哪里最值得补

三份正式场景的 LightingSettings 引用均为空，LightingData 都指向 Unity 内置 GUID。场景与可解析的项目内 Prefab 链中未找到序列化的 Light Probe Group 或 Reflection Probe。当前证据支持**缺少可核实的关卡级光照贴图和局部探针部署**。水尺已有独立烘焙 Cubemap，灰瓦、木构等则已有自己的材质与纹理。两类“烘焙”分别是照明与材质/倒影，收益不同。[苏醒室](../../Assets/Scene/Chapter01_Awakening.unity) · [庭院](../../Assets/Scene/Chapter01_Courtyard.unity) · [断衡悬道](../../Assets/Scene/Chapter02_SuspendedPath.unity) · [渠水倒影烘焙](../../Assets/scripts/logic/Editor/Environment/CanalReflectionBaker.cs)

移动 URP 的主光阴影为 1024、1 级联、50 m，附加光阴影和软阴影均关闭。PC 对应 2048、4 级联，并支持软阴影、附加光阴影和当前的 SSAO。两档都开了 Reflection Probe 混合与盒投影选项。第二章谷口灯的场景设置虽为 Soft，移动管线配置仍限制它。现有架构下，烘焙接触阴影和局部探针的成本可以和软阴影、后处理分开测。[Mobile URP](../../Assets/Settings/Mobile_RPAsset.asset) · [PC URP](../../Assets/Settings/PC_RPAsset.asset) · [PC Renderer](../../Assets/Settings/PC_Renderer.asset)

- **灰瓦与石木。** 二章 `Tile.mat` 有法线、未绑材质 AO 与金属光泽图，Smoothness 为 1。先在孤立瓦样核对实际高光，再把釉面、素陶和木梁的光泽拉开。檐下投影应由贴图或灯光给出，瓦缝的微阴影可来自材质 AO。材质 AO 进 Lit 的间接光计算，接触处需要稳定暗边可用场景烘焙，避免把整张 Base Color 压黑。[二章瓦材质](../../Assets/Art/Environment/SuspendedPath/MountainCraft/Materials/Tile.mat) · [首章灰陶材质](../../Assets/Art/Environment/Opening/PreqinRidgeCap/GreyEarthenware.mat) · [Unity 阴影优化](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/shadows-optimization)
- **铜与木的图集。** 衡梁和支点的 Lit 已有法线与金属光泽图，材质值 `_Smoothness=1` 是纹理系数，不能由它推断整张图光亮。先检验贴图通道。保留木段的粗糙散射，只让铜箍外沿、铆钉和磨损面出现宽、稳的反光。首章普通旧铜目前无金属光泽、法线或 AO 贴图，可作为画面对照。水尺倒影有专门 Shader，改探针时分别验。[衡梁材质](../../Assets/Art/Environment/SuspendedPath/MountainCraft/Materials/HunyuanBeam.mat) · [支点材质](../../Assets/Art/Environment/SuspendedPath/MountainCraft/Materials/HunyuanPivot.mat) · [旧铜](../../Assets/Materials/Chapter01/旧铜.mat) · [CanalFlow](../../Assets/Shaders/CanalFlow.shader)
- **手、机关字和敌人。** 手臂的自定义 Shader 读取球谐与主光，项目中的腕匣衬线和成印另走各自材质。局部探针、主光调整和新 Shader 都需要实看。掷火残机有自己的法线、Mask 与发光，可用来确认暖补光有没有吞掉弱点辉光和火核预警。[手臂](../../Assets/Shaders/PlayerHandForearm.shader) · [腕匣](../../Assets/Art/VFX/Shaders/WristGaugeInlay.shader) · [成印](../../Assets/Art/VFX/Skills/SealConfirmation/SealGlyph.shader) · [掷火残机材质](../../Assets/Art/Enemies/CanJiOu/CanJiOu.mat)

## 两个可直接试的样件

### 1. 首章天仪阁 檐下与柱脚烘焙阴影

在独立副本选天仪阁朝院一面和邻近一段灰瓦回廊。A 保留现状，B 分步增加烘焙。先只烘固定屋瓦、墙、木构、铺石和低强度暖色反弹，校验 UV2、遮缝和跨块接缝。近景重点看瓦檐压在梁柱上的阴影、柱脚贴地和下方木格的明暗。先以非方向 Lightmap 配现有实时主光做基准，只有木石微法线因烘焙失去层次时再对比方向 Lightmap。Lightmap 会增加贴图采样、资产体积和运行内存，方向数据再记一份开销。固定几何若要同时留静态直射和保留角色投影，另做一份 Mixed + Shadowmask 小样，核对只烘焙投影的静态件、角色接地和交界重影。避免同时烧入固定直射又按全强度重照。持续活动的门、衡梁、篮、敌人、玩家和机关另走现有实时光与适用的 Light Probes。[Unity 方向 Lightmap](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/direct-and-indirect-lighting/lightmapping/configure-lightmapping-settings/directional) · [Unity Mixed Lighting](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting-light-sources/components/configuring/lighting-mode/lighting-mode)

### 2. 二章支点和衡梁 铜件局部盒投影反射

对支点机座和玩家走近能看到的铜件放一个静态烘焙 Reflection Probe，限定盒体覆盖轴座附近，站在栈道上转头检查。全长约 120 m 的活动梁按区分配反射影响或保留天空补位，避免让一个小室内探针覆盖整梁。A 为环境反射，B 只加探针，C 在 B 上调整金属光泽贴图与反光强度。分别检验檐下铜面、背太阳一面和亮部铜箍。双眼反射随正确视线变化，转头时无尖细高光闪烁，暗部仍可辨。盒投影调整 Cubemap 取样位置感，动态角色、吊篮和火核不会出现在这份静态捕获里。[Unity 盒投影与探针边界](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/reflections/advanced-ref-probe)

如果 C 仍有明显的“阴影里像塑料发亮”，再加一份**只给地标材质用的高光 Shader 变体**。保留 URP 的受光、光照贴图、阴影与 XR 路径，单独约束高光。针对烘焙直射材质，只有确认方向 Lightmap 的主方向和强度适合重建后，才试按主方向计算一项较宽的直射高光，并用漫反射明度平滑衰减，避免与实时主灯重复叠加。探针反射与直接高光单独调，发光铭文和手印走各自路径。这一想法源自 Camouflaj 的做法，当前项目未实现。[Camouflaj 光照与高光](https://www.youtube.com/watch?v=mz_SZe6i6WA&t=759s)

## 战斗与真机确认

- 动态光先用现有主光的阴影做 A/B。看掷火残机是否与屋面接地、活动机关的影子是否随动作走。若影距外的角色接地仍差，再试仅在近处绘制淡投影或简易贴地网格，做好台阶／栏杆分层和距离淡出。火核补光用一盏会衰减的无阴影灯做可选对照，量重叠火核最坏情况。分别用左、右眼核对双手轮廓、准望匣文字和火核预警。移动档首轮实景避免全屏接触阴影、SSAO、SSR 和实时平面反射。
- 同一机位光照对照三处。天仪阁檐下由暗到亮，手与腕匣在檐下近看，二章从工坊窗口走向衡梁并转头看铜面。各开关独立采集，屋脊、瓦缝、铜箍分别找跳闪、漏光、悬浮和双影。同包同刷新率和眼缓冲记录 GPU P95/P99、连续掉帧、Lightmap 与 Cubemap 的纹理内存／包体、加载时间、ShadowCaster 批次。A/A 先测底噪，沿用上轮每项 GPU P95 增量暂定 ≤0.3 ms 的项目试验线，不把它当成官方或已测数据。对照真机先按 Quest，PICO 单列。[Unity 独立 XR 渲染优化](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization) · [原项目 A/B 方案](GLOBAL_SHADER_VR_20261125_ZH.md#验收与上线判据)

本轮只完成资料和工程静态核查。样件、光照贴图、实机及性能尚未制作或测量。
