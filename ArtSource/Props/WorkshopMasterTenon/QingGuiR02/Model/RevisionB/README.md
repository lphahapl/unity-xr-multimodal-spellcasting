# 清圭 R02 Revision B — 铜玉包边局部修整

核心冻结于 2026-11-26 03:41:42 +08:00。`frozen-manifest.json` 固定 25 个核心文件；原 R02 的 25 个冻结文件逐哈希保持原样。本目录只交模型、贴图与 Blender 证据；Unity 玉石 shader、最终实际截图和 Quest 验证由父会话合并，不以 Blender 图替代。

## 改动与范围

原 R02 的铜色/金属度在约 3 mm 宽度内渐变，几何只做羽化起伏，共用细碎高模法线使铜看似融入玉、表面波浪。B 将边界收窄为约 0.30 mm，增加真实 **0.50 mm 铜包边台阶**、窄圆转及玉侧约 0.10 mm 内凹接缝。高低模均增加语义对应的边界支撑环，铜面基准法线平顺，低对比细微色差替代大片斑驳。上体轮廓、真通孔、实体 GoldWire、原窄云雷纹/冠部小回纹、下端浅刻准线保留。

新增位移在 105 mm 以上渐入。0–105 mm 方端的 141 个顶点对照冻结接口，最大差 **1.013248818e-9 m**，容差 5e-7 m。LOD0 最终坐标与三角拓扑签名和已交 Unity 的 `QingGuiR02B_GeometryCandidate.fbx` 完全一致。B 外廓约 **110.028 × 106.797 × 350.000 mm**；相比 R02 上体宽增加约 0.493 mm、深增加约 0.700 mm，未超 120 mm 上限。Unity 须使用 B 候选所测接口，不能把外廓变化说成零。

## 文件与接口

| 文件 | 内容 |
|---|---|
| `QingGuiR02B_Source.blend` | 可编辑高模及源材质 |
| `QingGuiR02B.blend` | 高模、三 LOD、实际九图材质 |
| `QingGuiR02B_LOD0.fbx` | Body 8,838 + GoldWire 6,996 = **15,834 三角** |
| `QingGuiR02B_LODs.fbx` | LOD0 **15,834** / LOD1 **6,650** / LOD2 **3,444** |
| `QingGuiR02B.glb` | 普通 PBR LOD0，可旋转预览 |
| `QingGuiR02B_Lookdev.glb` | 同网格，标准 KHR 微透/抛磨预览 |

每档仍为两个 Renderer：`QingGuiR02_Body_LOD0/1/2` → 单槽 `QingGuiR02_Body`；`QingGuiR02_GoldWire_LOD0/1/2` → 单槽 `QingGuiR02_GoldWire`。保留 R02 名称方便增量替换，文件以 B 区分。米制，Blender +Z 上、正面 −Y、右 +X；FBX 转 Unity +Y。底端中心为原点。逐高度真实截面、偏心、体积和握点见 `model-report.json`，孔销兼容与碰撞以 Unity 实测为准。

`Textures/QingGui_*.png` 九图均 2048²：BaseColor 是 sRGB；NormalGL、AO、Roughness、Metallic、Thickness、JadeMask、ORM、MetallicSmoothness 为线性数据。ORM 为 R=AO/G=Roughness/B=Metallic；Unity MetallicSmoothness 为 R=Metallic/A=1−Roughness。Body 的 JadeMask 玉=1、铜=0；独立 GoldWire 全金属、JadeMask=0，不透光。两个 FBX 旁 `.fbm` 各四图已和主贴图逐哈希核对。

Thickness 来自最终高模 **525,108 条真实内向 BVH 射线**，15 μm 起点偏移，0 次未解 miss；38,971 条超过 120 mm 的路径截断。解码 **线性 R × 0.12 m**，不是 AO。保留离散采样、截断、PNG 量化近似。九图为真实高到低 Cycles 投射，详见 `model-report.json/bake`。

普通 GLB 不包含 Unity 玉石 shader。Lookdev GLB 使用 `KHR_materials_transmission/volume/ior/clearcoat`：玉 mask 控制透射 .12 和 coat .18，IOR 1.53，厚度因子 .12 m，衰减距离 .045 m；铜金不透，alpha OPAQUE。查看器需支持扩展；它不是 Cycles SSS，也不是最终 Unity 光学，更不是 Quest 性能证明。

## 验证与边界

两 FBX 干净冷导通过；LOD0 原容差 QA **0 错 0 警**。LOD1 有 277 对有意埋入的闭合金丝背向接触面提示，保留圆丝接触，不是悬空；LOD2 通过。整体 QA eps=10 μm、接触 tol=0.20 mm 未放宽。LOD0 15,834 超过原建议 10k，父代理明确接受以保留包边截面、孔口和圆丝；Quest 实机性能未测。

两个 GLB 原始检查各报告 3 对共面候选。逐对证实是 UV/切线拆点后的**公共边**，每对共有 2 个二进制坐标相同的端点；双精度零容差投影裁剪的面积三对均 **0.0 m²**。只读诊断副本以 1e-9 m 焊回相同位置前后，每个三角角点集合严格一致；在原 10 μm QA 下通过。实际 GLB 未改、未删面。原始报告、三对坐标及焊接副本检查保存在 `cold-import.json`，面积见 `glb-seam-projected-overlap.json`。原始拆点计数 Body 524 / GoldWire 739 与 1 对背向提示均保留在 raw_export_qa，不能声称原始 GLB 报告零提示。

离线源高模的默认 10 μm 检查有一对孔口近邻面命中：约 1 μm 间距、投影面积 1.4095e-13 m²、无实际相交。父代理接受源专用 0.5 μm 复核；原报告与精确证据完整保留，运行模型容差不变。源零退化面、零非流形、零错误绕序；72 个零 UV 面属于使用 Object 坐标的源程序材质，游戏 UV 已通过。FinePattern 的 Non-Color 参与 BaseColor 混色权重是有意用法。

实际游戏近景仍可见孔环分段与 **2K 斜向窄界的像素细毛边**，尤其 9 cm 取景范围的背冠极近图；正常持物比例另图对照，不隐去该限制。不会用高模预览冒充最终低模。铜包边有真实几何，但本轮审美是否足够分件仍以用户对 Unity 成片批注为准。

## 图与复跑

`Previews/12_ThreeView_SameScale.png` 为实际 LOD0 同尺度三视；13/14/15 为原 R02 与 B 同机位同灯光对照；`B_Game_BackHeld` 为正常尺寸；`B_Game_BackCrownCopperTail` / `B_Game_FrontCrownCopperTail` / `B_Game_BackShoulder` / `B_Game_LeftCopperWrap` 为本轮批注对应近景。图源均为 Blender Cycles，清楚区分 Unity 实拍。`10_CleanWire45` / `11_CleanWireFront` 与 QA 材质/线框图已经实看。

独立 Blender 5.2.2 CLI、CUDA RTX4050。最终脚本为准，不重跑会重新派生旧内容的 prepare 脚本。典型顺序：`build_source.py -- --qa-only` → `clean_revision_fins.py`（如需移除已限定的布尔微小翅片）→ `verify_source_precision.py` → `build_delivery.py` → `make_lookdev_glb.py` → `measure_interface.py` → `verify_delivery.py` → `check_glb_seams.py` → `review_delivery.py` → `assemble_review.py` → `freeze_delivery.py`。源微邻面诊断由 `diagnose_source_overlap.py` / `check_overlap_projection.py` 保留证据；最终运行几何不为它重建。

所有本轮运行日志在 `Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b/`。`media-contract.json` 给媒体路径/来源/SHA；`final-file-manifest.json` 补录脚本和图。来源为项目已通过的 C4 和可复跑程序建模，本轮无云建模单、无新增外部材质资产，未写正式 Assets/Unity/工作台。
