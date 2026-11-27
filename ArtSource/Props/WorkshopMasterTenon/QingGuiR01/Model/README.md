# 清圭 R01 实际模型

依据已获用户认可的 C4 和上级 `design-contract.json` 制作。此目录是独立模型稿；C4 图稿批准不等于本实模或头显验收已批准。未修改正式 Assets、Unity 场景、旧 R01/R02/R03 或共享工作台。

首先看 `Previews/11_ThreeView_SameScale.png`：这是最终 **4916 三角形低模与实际 2K PBR** 的同尺度正、右、背视图。正面是 Blender −Y，高侧在 +X，背面自然镜像。`04_Front45.png`、`05_GoldJadeClose.png`、`06_ApertureClose.png`、`07_ShoulderAlignment.png` 和两张 `CleanWire` 提供近景与真实布线。`08_HighSameView.png` 才是高模同机位对照。

## 冻结与接口

核心冻结时间为 2026-11-26T00:15:10+08:00；最终散列在 `Logs/workshop-master-tenon-qinggui-r01-20261125/model/candidate-ready.json`。父代理负责 Unity 接入、全局文档和工作台。

| 文件 | 实际内容 |
|---|---|
| `QingGui_LOD0.fbx` | 4916 三角形；1 材质、1 UV |
| `QingGui_LODs.fbx` | 4916 / 1800 / 600 三角形，含真实通孔的三档模型 |
| `QingGuiR01.glb` | LOD0，嵌入标准 PBR 基色、法线、粗糙金属通道；AO、Thickness、JadeMask 另外提供 |
| `QingGuiR01.blend` | 高模、三低模、源玉/铜/金材质、烘焙配置与游戏 PBR |
| `QingGuiR01_Source.blend` | 独立高模源；1207404 三角形，含真实刻槽、铜脊层差、圆孔壁与冠部倒圆 |
| `Textures/` | 九张 2048² PNG，具体通道如下 |
| `QingGui_LOD0.fbm/`、`QingGui_LODs.fbm/` | FBX 自动材质引用的四通道副本；NormalGL 已同步最终孔壁版，与 Textures 同字节 |
| `model-report.json` | 预算、实际尺寸、几何截面、握点、厚度统计、QA 与限制 |

单位为米，底端中心为原点，Blender +Z 导出 Unity +Y。LOD0 外廓约 **111 × 106 × 350 mm**，未超过 120 mm 限制。0–105 mm 方插端保持原 106 × 106 mm 方向；与冻结 R03 的 141 个低模顶点比较，最大距离 **1.01e−9 m**，检验容差 5e−7 m。高模准线/刻线只向内刻，不扩大方端。

握位高度建议为 230 mm，设计宽 85 mm、厚 46 mm；初始握点为 Unity `(-.0015, .230, 0)`。报告 `measured_mesh_section_profile` 来自最终低模与逐高度平面的真实交线，包含 X 最小/最大值、前后厚度和偏心；`section_profile` 是生成轮廓参数，两者不要混称。前后中心的符号应以实际 Unity 导入坐标为准。原孔资料名义 146 mm、当前网格测量 153 mm；本件不改孔、落座或 90° 吸附逻辑。新肩与斜冠 **不能继承旧双销通过结论**，实插/新接触体由 Unity 负责人验证。

## 几何与材质如何实现

C4 上体由新参数曲线放样，保留斜冠、单侧 S 形铜脊、中央玉面留白、上下金流线、下端回纹与准线；没有把 R03 等比缩放或换色。主要玉面有约 2 mm 轻冠和 1.2 mm 浅拇指层次，孔为真实贯穿几何。

高模在闭合外表面切出约 **0.20 mm** 的金槽，并以金属表面回填约 0.14 mm，金面仍低于邻接面约 0.06 mm；铜边层差约 0.55 mm。这是可见外边界与金属材质分区的游戏建模表达，不另建不可见的内部金条实体，也没有悬浮描线。高模源保存真实槽形；低模用烘焙保留细节。

金线颜色使用由毫米制曲线生成的连续 4K 源遮罩，配合对应连续槽形法线，避免 POINT 色属性在窄线上的阶梯插值。4K 文件仅供源材质制作，**游戏输出全为 2K**。孔壁另用 CORNER 域径向法线，避免内壁与外面共享 POINT 法线产生横亮带；最终 NormalGL 和嵌图 GLB 已据此重烘/重导。2K 近景仍可见细纹采样，未声称无限放大无锯齿。

常态玉面保持温白、柔润连续高光；铜为暗茶铜与少量局部氧化，金更亮暖。源 Cycles 材质使用 SSS、Transmission 0.02 和温中性体积吸收。`SourcePreview/06a_Backlight_Normal_5W.png` 与 `06b_Backlight_Stress_25W.png` 是同机位的不同强度背光；后者明确是压力图。`06_Backlight_BeforeCorrection_25W.png` 是偏绿、过透的历史中间图，不能作为当前交付代表。关灯图无自发光，像素峰值 1/255 属渲染底噪/抖动，不虚报数学全零。

游戏材质为不透明 PBR。Blender 的 SSS/透光不会自动变成 Unity URP 效果；Unity 使用独立厚度光照近似，并由其负责人执行实际材质与 Quest 检查。

## 游戏贴图与厚度

| 贴图 | 色彩空间与内容 |
|---|---|
| `QingGui_BaseColor.png` | sRGB，无光照基色 |
| `QingGui_NormalGL.png` | Non-Color，切线法线，包含最终孔壁修正 |
| `QingGui_Metallic.png` | Non-Color，玉非金属、铜金金属 |
| `QingGui_Roughness.png` | Non-Color，分区粗糙度 |
| `QingGui_AO.png` | Non-Color，独立 AO 烘焙 |
| `QingGui_Thickness.png` | Non-Color，真实网格射线厚度，米 = R × 0.12 |
| `QingGui_JadeMask.png` | Non-Color，玉 1，铜与金线 0，边缘抗锯齿允许中间值 |
| `QingGui_ORM.png` | R = AO、G = Roughness、B = Metallic |
| `QingGui_MetallicSmoothness.png` | R = Metallic、A = 1−Roughness |

Thickness 对 **603702** 个实际高模顶点从内移 15 μm 处向内作 BVH 射线，测到相对表面。原始范围约 **0.113–318.519 mm**，不是 AO 冒充厚度。6 条未命中射线采用保守 120 mm 值；44903 个超过 120 mm 的测量值被截断。完整分位数、重试/缺失和测量方法在 `thickness-ray-measurement.json`。上限截断使厚处保持不透，不应把 R = 1 理解为精确 120 mm。

正式烘焙前固定低模三角形、UV 与法线；侧面 UV 按实际截面周长分配，封盖/通孔独立岛。Cycles selected-to-active，cage 3 mm、最大射线 12 mm、扩边 16 px。512² 投射中间图保存在 `Checks/`，不属于游戏九图。

## 实跑验证与限制

- 高模与三档低模 QA：0 错、0 警；闭合、单岛、无重复点或退化面。
- 干净 Blender 进程回读两份 FBX：数量、尺寸、原点、三角形、材质、UV 通过，0 警。
- GLB 冷读通过，382 个同位置顶点告警来自 UV/切线缝拆分，源网格无重复点。导出器还报告合并粗糙/金属图时沿用首个采样器；参与图节点均为 Linear/Repeat，采样设置一致。
- LOD1/2 减面后包络存在约 0.01 mm 的浮点/简化差异；碰撞与定位应基于 LOD0 和独立代理体，不能拿远 LOD 作精密量规。
- 静态尺寸保留不等于新销机构通过；本模型工作未操作 Unity、正式场景或头显。父代理管理的 Unity 验证另行交付。

## 可复跑流程

使用独立 `D:/Blender/Blender 5.2/blender.exe -b --factory-startup --python-exit-code 1 -P <脚本>`，不占用已打开的 Blender。实际版本 5.2.2，RTX 4050；本任务没有调用 live Blender MCP 或新云端生成。

1. Blender 运行 `export_source_mask_paths.py`，普通 Python/Pillow 运行 `render_source_masks.py`。
2. Blender 运行 `build_blockout.py` 复现素模检查，运行 `build_source.py` 生成真实高模。
3. `refine_source_lighting.py` 设置内外壁解析遮罩与材质探针；`build_delivery.py` 执行实际厚度射线、九图烘焙、LOD 和导出。
4. `tune_source_transmission.py` 仅调整源 SSS/吸收与独立背光，不改游戏九图；`correct_hole_shading.py` 修正孔壁 CORNER 法线，补 NormalGL/GLB。
5. `measure_interface.py`、`verify_delivery.py`、`review_delivery.py` 完成实测截面、冷回读与最终视图。

放样基础复用同仓库冻结的 `R03/geometry_r03.py` 的单位、底端和插值函数；新上体在本目录定义。流程脚本依赖该只读输入与 Blender 自带 numpy；源遮罩光栅化用本机普通 Python/Pillow。冻结后不要在此目录重跑覆盖，改稿另开版本。自动 `.blend1`、缓存、早期 Blockout/SourcePreview 不是最终下载入口。

制作代理 `/root/tenon_model_build`，Codex（具体服务子型号未提供）；父代理负责统筹与验收。最终核心冻结和全文件清单位于本任务 model Logs。
