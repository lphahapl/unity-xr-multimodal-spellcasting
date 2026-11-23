# 《断衡悬道》山谷样件源文件

以金沙江虎跳峡一带 Copernicus GLO-30 两块公开数字表面模型为底稿。用本地近似米制投影裁切，向东北旋转 35°，按 0.225 倍平面、0.160 倍高程比例适配关卡。采样中心为 100.132°E、27.199°N，对应 Unity (35.6, 0, 24) 附近。场景中的山谷是经过缩放和改形的关卡地貌。

| 文件 | 用途 |
| --- | --- |
| [terrain.json](terrain.json) | 两块原始瓦片下载地址和 SHA-256，迭代参数、位置、高程区间、行方向及尺寸。 |
| [GorgeHeights.npz](GorgeHeights.npz) | 499 × 499 的米制轴、原始表面高程、改形前后地形及水蚀变化。数组按 `[z,x]` 读。 |
| [GorgeCenter_513.r16](GorgeCenter_513.r16) | 可编辑的 Unity RAW 高度图。513 × 513，16 位小端。 |
| [MeshSampling.npz](MeshSampling.npz) · [corners.txt](corners.txt) | 中心密、边缘疏的 135 × 135 网格采样和四角高程，可与 FBX 回读核对。 |
| [GorgeRelief.blend](GorgeRelief.blend) | 可编辑地形网格，含预览材质与嵌入底色图。 |
| [模型、材质、预制体和天空盒](../../../../Assets/Art/Environment/SuspendedPath/MountainTerrain) | Unity 可直接引用的运行资产。 |

## 重新生成

根据 `terrain.json` 下载两个源 TIFF 到 `Logs/Chapter02Mountain/`，分别命名 `N27E100.tif` 和 `N27E099.tif`。项目根目录运行下列命令。Python 依赖 NumPy、SciPy 和 Pillow。

```powershell
$source = 'ArtSource/Environment/SuspendedPath/MountainTerrain'
$assets = 'Assets/Art/Environment/SuspendedPath/MountainTerrain'
python "$source/generate_terrain.py" Logs/Chapter02Mountain/N27E100.tif Logs/Chapter02Mountain/N27E099.tif $source $assets
& 'D:/Blender/Blender 5.2/blender.exe' -b --python-exit-code 1 --python "$source/export_relief.py" -- $source $assets
```

可调项见 `generate_terrain.py --help`。只改侵蚀时，以同一底图对照 `--steps 0` 与默认 85 步，再检查沟深和主要分水岭。雨量、泥沙承载、冲刷、沉积、蒸发和最大下切均独立可调。求解器使用离线四邻域水量与泥沙传递，用于地貌塑形。各参数是样件参数，水量和迭代步数没有换算到真实降雨强度或洪水时长。

如要另建 Unity Terrain，位置 (−574.4, −120, −586)，水平尺寸 1220 × 1220 m，Y 尺寸取 `terrain.json` 的最大与最小值之差。导入深度 16 位，Byte Order 选 Windows。RAW 第一行是南侧，列朝 +X、行朝 +Z，按四角高程校验导入方向。已装配的预览使用 FBX、预制体和天空盒。

重新装配对照场景时，在使用相同 Unity 版本和工程资源的隔离副本中将 `MountainPreviewBuilder.cs` 放入 Editor 目录。将四角读数 `corners.txt` 放到报告目录，设环境变量 `CH2_MOUNTAIN_REPORT` 指向该目录，再用 `-batchmode -force-d3d11 -executeMethod MountainPreviewBuilder.Build -quit`。脚本从正式场景另存预览并输出对照图。

## 地形源署名

数据于 2026-11-23 从 Copernicus DSM 的 AWS 公开分发获取。衍生成果沿用许可第 6 条的署名和声明。[官方分发目录](https://registry.opendata.aws/copernicus-dem/) · [产品许可](https://documentation.dataspace.copernicus.eu/APIs/SentinelHub/Data/DEM/resources/license/License-COPDEM-30.pdf#page=2)

Produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved.

The organisations in charge of the Copernicus programme by law or by delegation do not incur any liability for any use of the Copernicus WorldDEM-30.
