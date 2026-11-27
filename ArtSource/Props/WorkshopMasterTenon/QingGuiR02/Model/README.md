# 清圭 QingGui R02 — 冻结模型包

2026-11-26 01:43:51 +08:00 冻结。此轮按用户“修过渡、金丝贴在表面、玉要透明光泽”的批注制作；沿用已选 C4，QingGuiR01 及更早版本保持原样。这里只交独立模型，未写正式 Assets、Unity 场景或工作台。审美后续批注、Unity 最终效果与 Quest 实机验收由父会话继续。

`frozen-manifest.json` / `candidate-ready.json` 固定了 25 个核心文件的 SHA256，包括两个可编辑 Blend、两个 FBX、普通/Lookdev GLB、几何候选、九张 2K 贴图、8 个 FBX 贴图副本和源材质细纹 mask。后续文档不改变这些核心文件。

## 模型与接口

| 内容 | 文件 / 名称 | 实际三角数 |
|---|---|---:|
| 高模源 | QingGuiR02_Source.blend；Body_HIGH + GoldWire_HIGH | 详见 model-report.json |
| 游戏 LOD0 | QingGuiR02_LOD0.fbx | Body 5,670 + GoldWire 6,804 = **12,474** |
| 三档 | QingGuiR02_LODs.fbx | LOD0 **12,474** / LOD1 **6,398** / LOD2 **3,356** |
| 可编辑高低模合集 | QingGuiR02.blend | 保留源体及三档 |
| 普通 PBR 旋转预览 | QingGuiR02.glb | 真实 LOD0；不包含 Unity 玉石 shader |
| 标准光学预览 | QingGuiR02_Lookdev.glb | 同一 LOD0；支持 KHR 扩展的查看器可显示微透与抛磨 |

每档两个 Renderer：`QingGuiR02_Body_LOD0/1/2` 与 `QingGuiR02_GoldWire_LOD0/1/2`；各单槽，材质分别为 `QingGuiR02_Body` 与 `QingGuiR02_GoldWire`。Body 用 JadeMask/MetallicSmoothness 分玉铜；GoldWire 是独立闭合金属网格，Metallic=1、Roughness=.24，不透光，不依赖图片。材质上限实际为 2。

以米建模；Blender +Z 上、正面 -Y、右 +X，FBX 转 Unity +Y 上。原点为底端插接中心。LOD0 实测整体包络约 **109.534 × 106.097 × 350.000 mm**，未超 120 mm 横向上限。0–105 mm 插端的 141 个控制顶点对照冻结旧方端，最大差 **1.013e-9 m**（容差 5e-7 m）；106 mm 方端、原点与原落座接口保留。各高度实测截面/偏心和握点建议见 `model-report.json`、日志 `measured-interface.json`。较远 LOD 有微米级边界漂移，碰撞/插接应继续使用 LOD0 接口。新冠顶/肩销不由本模型报告宣称机械兼容。

最终 LOD0 的顶点坐标与三角索引 SHA 与 `QingGuiR02_GeometryCandidate.fbx` **完全一致**，冷回读记录保留逐网格签名。后续只整理金丝 UV，未改变线形、坐标、拓扑或接触体。临时约 10k 目标由父验收接受调整为实际 12,474，以保圆孔口、实体线圆截面和贴面接触；近景仍可见该游戏档孔圈/小枝端的折线，这是已接受的预算取舍，不能宣称无限近距平滑。

## 本轮真实改动

肩的横截面圆角与 145 mm 处切线连续接续；铜层接触边柔化，旧水平明暗断带去掉。孔口使用带连续圆弧断面的实心切割体，替代高密度布尔碎边逐边 Bevel，修复不等宽倒角形成的锯齿；孔内壁铜层位移渐退到零，径向分角法线修复亮片。

18 条主流线、边线、孔环是独立实心金丝：约 0.86–1.10 mm 最大直径、圆截面、圆润收头，末端直径约 0.38 mm。中心随各档真实主体投射，名义埋入 0.10 mm；凹肩处加密支撑采样，主线在收腰无支撑处自然终止。互穿/粘连接头已让距。LOD0 的四分段触面探针均为负间隙，最浅约埋入 0.052 mm；这是离散几何采样证据，不是假称无限连续碰撞证明。

对应长金线的旧凹槽与贴金从 Body 完全移除。C4 原冠部小回纹与下铜领窄云雷纹仍是 **Body 的真实浅刻金属细节**（约 0.16 mm 深）并烘焙；它们有意不升级成粗圆丝。0–105 mm 的准线/左右短刻度仅向内浅刻约 0.10 mm，不加凸金属，不改变配合外包络。

玉材质改细微低对比絮纹与平顺抛磨，去掉高频颗粒 bump；源玉 IOR 1.53、roughness .13、仅玉区 coat .18 / coat roughness .09、SSS .32 / scale 5.5 mm、transmission .055，配温中性吸收。铜主体暗茶色，少量凹色；金独立更亮暖。

较远档的同样离散接触探针，LOD1 最大正间隙约 0.010 mm，LOD2 约 0.080 mm，均小于本次 QA 的 0.20 mm 接触容差；它们是距离简化档，不以近手 LOD0 的全采样埋入结果替它们作证。

## 贴图与光学范围

`Textures/QingGui_*.png` 全为 2048²。BaseColor 按 sRGB；NormalGL、Roughness、Metallic、AO、Thickness、JadeMask、ORM、MetallicSmoothness 均线性数据。ORM：R=AO / G=Roughness / B=Metallic；Unity MetallicSmoothness：R=Metallic / A=1−Roughness。NormalGL 为切线空间 OpenGL 约定，实际 Unity normal importer/绿通道约定由 Unity 会话验证。FBX 的两个 `.fbm` 内四张图片已逐哈希核对与 Textures 一致。

Thickness 是 **607,035 个最终主体高模顶点的真实 BVH 内向射线**，15 μm 起点内移；1 次备用法线重试、0 次未解命中，44,254 个超过 120 mm 的路径截断。解码为 **线性 R × 0.12 m**，不是 AO。JadeMask：玉=1，铜=0；独立 GoldWire 固定为 0。九图由 Cycles 真高到低投射烘焙，cage 3 mm、max ray 12 mm、margin 16 px。厚度图含离散采样/截断/PNG量化近似。

普通 GLB 是标准游戏 PBR，外供 AO/Thickness/JadeMask 给 Unity。Lookdev GLB 额外提供 `KHR_materials_transmission / volume / ior / clearcoat`；透射 .12 与抛磨均由 JadeMask 门控，厚度因子 .12 m、衰减距离 .045 m、IOR 1.53。铜与金不跟着透，不使用 alpha 隐去主体。该预览需查看器支持扩展，只是标准衰减透射，**不等于 Cycles SSS，也不等于 Unity 自定义玉 shader**。不据此声称 Quest 性能/透明排序/额外渲染 pass 已验；两网格、两材质之外的具体 shader 开销以 Unity 实测为准。

## 已测与警告

最终源高模 QA 通过；LOD0 FBX 冷导 0 错 0 警，所有三档/两种 GLB 冷导均通过。LOD1 有 253 对有意埋入接触的闭合金丝背向面提示，保留闭合实体，不是悬空或共面闪烁。GLB 因 UV/切线缝有 Body 477、GoldWire 723 个重复位置顶点；金丝圆柱图表+端盖图表把原来的 10,206 降至 723，坐标/拓扑严格断言不变。

源高模另有 59 个零面积 UV 面与 1 个近重复顶点警告：源材质使用 Object 坐标程序纹理/投射 mask，不依赖这些 UV；源无退化网格面、非流形边、悬空、共面闪烁。游戏 UV 已重新展开，布尔极点的一面分配到保留的 16 px 小岛后再烘焙，运行时 UV 检查通过。FinePattern 的 Non-Color 警告来自其作为**混色权重**被检查器追踪到 BaseColor，不是把颜色贴图错误设置成线性。

实际低模三视与近景在 `Previews/`；源材质柔高光、5 W 常态背光、25 W 压力背光、关灯在 `SourcePreview/`，标签区分清楚。2K 微细刻纹在极近特写仍能看出采样，不以源高模图代替游戏图。没有真人/Quest/最终 Unity 光学验收声明。

## 复跑和证据

Blender 5.2.2 独立 CLI，CUDA RTX 4050；不需要占用打开的 Blender。顺序在 `model-report.json/reproducibility`。`prepare_pipeline.py` 是此轮从只读旧管线派生脚本的历史工具；最终交付脚本本身为准。几何基础的 `section/profile/slopes/value` 读取项目冻结的 `R03/geometry_r03.py`，保留原插接接口。

`review_delivery.py` 渲染真实当前低模/高模；`assemble_review.py` 只等比例排版已有渲染，不生成替代模型图。`verify_delivery.py` 冷读实际 FBX/GLB并对比几何候选签名；`final_source_qa.py` 核最终高模；`freeze_delivery.py` 固定资源并核 QingGuiR01 的四个核心文件哈希未变。详证位于 `Logs/workshop-master-tenon-qinggui-r02-20261126/model/`。来源为项目已选 C4 与可复跑程序建模，没有本轮云建模单或外部未授权素材。

`media-contract.json` 给页面/父会话的实际媒体路径、来源标签与 SHA；`final-file-manifest.json` 补录文档和预览全文件清单。
