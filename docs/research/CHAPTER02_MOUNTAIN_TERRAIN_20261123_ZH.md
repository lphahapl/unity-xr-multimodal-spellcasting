# 《断衡悬道》山地数据、水力侵蚀与远景

来源核查于 2026-11-23。第二章场景使用 Unity 6000.3.10f1、URP 17.3.0，天空材质为首章的 `MountainCloudSky`。现行 V2.4 在 +80、0、−80 m 三个标高组织游玩空间，衡梁跨度 120 m。渲染层从上到下要同时容纳石门断桥、祭坛、工坊、中控和谷底。地形样件的工作是让这些构件落在可信的连续山谷里，陡壁、桥洞、平台、基础碰撞与吊篮净空仍由场景几何约束。[第二章 Unity 场景](../../Assets/Scene/Chapter02_SuspendedPath.unity) · [V2.4 距离规格](../architecture/第二章_断衡悬道/04_场景距离规格.md) · [当前场景搭建](../architecture/第二章_断衡悬道/05_美术场景搭建.md)

## 算法与数据

| 方法 | 能解决的问题 | 在第二章的用法 |
| --- | --- | --- |
| Copernicus GLO-30 实测数字表面模型 | 约 30 m 标称采样，山脊、主谷和支谷的空间关系来自地球观测。产品包含地物和植被的表面高程。 | 取虎跳峡区域 N27E100 公开瓦片，并用 N27E099 补齐西侧远端。以米为单位裁切、旋转，再适配游戏谷轴。局部栈道净宽和悬岩仍由建模控制。[Copernicus 数据页](https://dataspace.copernicus.eu/explore-data/data-collections/copernicus-contributing-missions/collections-description/COP-DEM) · [AWS 公开数据目录](https://registry.opendata.aws/copernicus-dem/) |
| 浅水网格水力侵蚀 | 水面高差驱动水流，流速关联冲刷和沉积，泥沙随水迁移。多层材料与崩边可进一步表达软硬岩差异。 | 关卡适配后的山体可以离线做轻量冲刷，连续出沟并软化接坡。保留侵蚀前数据以便对照。标注简化实现实际计算的步骤。[Mei 等，2007](https://ieeexplore.ieee.org/document/4392715/) · [Šťava 等，2008](https://cs.purdue.edu/cgvlab/www/publications/Stava08SCA/) |
| 汇水面积驱动的河流下切 | Stream Power Law 用上游汇水面积、河道坡降和侵蚀系数估计河道下切。FastScape 可迭代求解，解析版的输入进一步包括隆升图与时间。 | 需要批量生成整条架空山脉时，用它形成分水岭和侵蚀网络，再精修关卡。浅水网格更适合在已选定山形上控制局部沟蚀和沉积。[Braun 与 Willett，2013](https://www.sciencedirect.com/science/article/pii/S0169555X12004618) · [FastScape 模型](https://github.com/fastscape-lem/fastscape/blob/main/doc/source/models.rst) · [Tzathas 等，2024](https://www-sop.inria.fr/reves/Basilic/2024/TGSC24/) |
| NASA SRTMGL1 | 约 1 弧秒的雷达测绘高程，可作为同区域走势对照。 | 保留为独立数据来源。弧秒是角度单位，换算成米时东西向间距随纬度变化。[NASA/USGS SRTM 用户指南](https://lpdaac.usgs.gov/documents/179/SRTM_User_Guide_V3.pdf) |

在本项目里，用户说的“洪水算法”可落实为离线浅水侵蚀。可暴露的旋钮包括降雨强度、迭代次数、携沙／沉积速度、蒸发和基岩侵蚀率。限制场景适配后允许改动的区域，比把冲刷加满更重要。建议以同一底图输出无侵蚀与轻侵蚀版本，检查主谷出口是否连通、支沟是否汇入同一谷底、平台保护区有无长出山包。雨量和迭代次数影响侵蚀程度，网格间距和时间步影响结果尺度及稳定性，需与参数一并保存。[浅水速度场与泥沙运输原论文](https://ieeexplore.ieee.org/document/4392715/)

GLO-30 数据应记录源瓦片、版本、裁切范围、坐标和高程适配，以及原数据与衍生成果的来源。许可第 6 条给出了原数据和改编数据各自的署名文字，以及公开传播或分发时用于告知下游的声明。[GLO-30 许可第 6 条](https://documentation.dataspace.copernicus.eu/APIs/SentinelHub/Data/DEM/resources/license/License-COPDEM-30.pdf#page=2)

## 在 Unity 分层

1. **近处用三维地形接住关卡。** 以同一高度场搭连续的谷底和山肩，在工程坐标下约束台基、桥下、吊篮扫掠空间和通行净空。高度场在每个水平坐标只存一个高程，桥下挑岩、凹壁和跨空石构继续用网格。Unity 可导入 16 位 RAW，高度偏置、实际尺寸、字节顺序和行方向可随文件保存，便于后续雕刻。[Unity 6.3 高度图](https://docs.unity3d.com/6000.3/Documentation/Manual/terrain-Heightmaps.html)
2. **中距离用低面数山体建立遮挡和视差。** 在同一高度场上逐步降采样，连接边界并沿用岩色和雾色。超出游玩区的山腰可只渲染，省掉碰撞；是否关闭其投影取决于它会不会在玩家区域产生明显阴影。直接给玩家能走到或降到的高度差配平面山图，容易出现山体固定、谷口无纵深的画面。
3. **天际线烘焙成天空盒。** 从场景中心附近只烘最远一圈，并与保留的中景共享方位、太阳方向和空气透视。Unity 6.3 的 URP Panoramic Skybox 支持 360° 经纬投影，可用两侧山脊、四个方位和地平线接缝校验朝向。以 +80 m 和 −80 m 相隔 160 m 作几何估算，2 km 外同一地标在两端观察的竖向角度差最大约 4.6°。天空盒距离应根据三层画面的山脊重合与谷口遮挡决定。[Unity 6.3 Panoramic Skybox](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-skybox-panoramic.html)

## 样件验收

- 用石门回首、祭坛向谷、中控向工坊、谷底仰视四个同机位前后对照，再沿吊篮运动观察。重点看岩体吊脚、连续谷坡、山脊接缝、天空盒穿帮和上下移动时的视差。
- 用现有桥梁及落地射线、吊篮净空和两态过桥的验证方法回归。谷底一并检查植入的地表是否盖住 −80 m 平台、近岩是否侵入传送落点。桥护栏净宽历史问题 `CH2-BRIDGE-CLIFFS-20261122-WIDTH` 沿用既有记录。[桥侧崖体与 Unity 回读](../architecture/第二章_断衡悬道/05_美术场景搭建.md)
- 先记录中景网格三角面、可见 Renderer 和纹理占用，再在 Quest/PICO 上采中控朝谷、谷底仰视和乘篮三段。对照 CPU/GPU 帧时、双眼遮挡与雾的表现。团队可用相同机位在旧地形与样件间切换测量。[Unity 6.3 独立 XR URP 优化](https://docs.unity3d.com/6000.3/Documentation/Manual/xr-untethered-device-optimization.html) · [本项目第二章既有帧预算研究](CHAPTER02_VERTICAL_VISIBILITY_QUEST_PICO_20261122_ZH.md)
