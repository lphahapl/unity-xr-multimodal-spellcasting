# 汉白玉校准主榫 R03

R03 是按「R02 太厚、缺少设计感」返工的独立批注稿。父代理已看同机位预览并允许完成烘焙；用户审美、头显握持感和正式接入仍待确认。R01、R02 保留，不替代正式场景。

## 实际改动

- 握位前后厚度由 120 mm 收至腰部 74 mm，减少 38.3%；宽度腰收至 104 mm，上部回放至 112 mm。圆角半径在握位逐渐增至 18–24 mm，保持方形仪器的方向性。
- 原点至 127 mm 完整保留 R02 的实际低模坐标与三角形：309 个顶点、588 个三角形的排序哈希相同。包括 106 mm 方插端、下导角和原肩曲线。127 mm 以上的新柔肩属于本轮变化，不按未落地的肩销契约擅自冻结至 140 mm。
- 正面（Blender −Y）36 × 56 mm 的浅椭圆拇指凹由真实高低模几何构成。最终 LOD0 中心顶点在 z = 252 mm，实测凹深 2.0000003 mm；不是只做法线假凹。连续准线穿过其左侧并连接长短刻度，上方与纹带留白。
- 汉白玉保留微弱无方向絮纹，铜仅作插入端窄暗嵌饰。云雷纹改为沿实际截面周长排列的 12 个连贯单元，避免缩薄后侧面等参数纹样变窄。没有实刻假中文字或自发光。
- 预览低模体积从 R02 的 0.00465126 m³ 降到约 0.00338982 m³，减少约 27.1%。该体积对照来自补拇指中心点前的 1408 三角形小样；最终补点到 1474 三角形，厚度与下端保留结论未变。

## 尺寸与外部配合

Blender 单位为米，轴 +Z，底端中心原点；FBX 导出 Unity +Y。包络仍是 120 × 120 × 350 mm，未扩大原孔。逐高度宽、厚、圆角见 `model-report.json`。

| 底端起高度 | 正面宽 × 前后厚 | 说明 |
|---|---|---|
| 6–105 mm | 106 × 106 mm | 原方插段 |
| 105–127 mm | 106 → 120 mm 方截面 | 原肩曲线 |
| 140 mm | 118 × 105 mm | 新柔肩 |
| 190 mm | 106 × 78 mm | 握位开始 |
| 215–240 mm | 104 × 75 → 74 mm | 窄腰 |
| 302–325 mm | 112 × 80 mm | 上端纹带与握头 |
| 350 mm | 100 × 64 mm | 圆润冠部顶部截面 |

原孔资料名义宽度 146 mm，当前网格测量采用 153 mm。报告兼容字段 `nominal_socket_metres = .153` 指本轮采用的实际测量值，不是把原稿改称 153 mm。120 mm 肩不能跨该孔充当机械止挡。旧落座底 y = .923940、孔口 y = 1.028 沿用。新增肩销、旧顶缘挡销及原孔实插由父代理/Unity 执行，不把下端保留当作销机构已验证。

报告中的四个内部 BoxCollider 是独立样板的保守建议。新柔肩需要按截面凸壳另外验证，不能直接用整件低模作动态非凸 MeshCollider。

## 文件与验证

| 交付 | 内容 |
|---|---|
| `MasterTenon_R03.blend` | 可编辑高模、三个低模、源材质、最终贴图、预览灯光相机 |
| `MasterTenon_LOD0.fbx` | 1474 三角形，1 材质、1 UV |
| `MasterTenon_LODs.fbx` | LOD0 / 1 / 2 = 1474 / 560 / 210 三角形 |
| `MasterTenon_R03.glb` | 嵌入 PBR 纹理的 LOD0 |
| `Textures/MasterTenon_*.png` | 全部 2048²，BaseColor、Normal、AO、Roughness、Metallic、ORM、MetallicSmoothness |
| `MasterTenon_R03_Preview.blend` | 早期同光对照小样，1408 三角形；不能代替最终导出 |
| `build_tenon_r03.py`、`geometry_r03.py` | 实际造型、UV、烘焙、LOD 和导出程序 |
| `verify_exports.py`、`render_clean_wire.py` | 冷回读、下端同一性、真实凹深验证及线框图程序 |

Blender 5.2.2 / Cycles 实际选中到活动烘焙，源高模 365376 三角形，带真实刻槽和拇指凹。先确定低模三角形和分离法线后再烘焙，cage = 3.5 mm，最大射线 9 mm，边距 16 px。侧面 UV 横向按每行实际周长、纵向按高度各取 2 UV/m，封盖同密度；同一材质图集。ORM = R AO / G Roughness / B Metallic；Unity MetallicSmoothness = R Metallic / A 1−Roughness。BaseColor 为 sRGB，其余为 Non-Color。

本轮实跑结果：高模、三档低模 QA 全部 0 错 0 警；干净进程导入 FBX 尺寸、原点、1474 三角形、材质和 UV 通过且 0 警。GLB 冷读通过，有 84 个同位置顶点告警，来源为 UV/切线缝拆分；源模型无重复点、开放边、非流形或分离岛。GLB 材质内含基色/法线/粗糙金属纹理，AO 另由独立 AO 和 ORM 通道交付。游戏 PBR 为不透明玉面近似，源材质少量 SSS 不等于 Quest 半透玉着色器。

最终散列、完整 QA、冷读和几何验证在 `Logs/workshop-master-tenon-r03-20261125/model/`：

- `candidate-ready.json`：冻结状态、核心文件与七通道 SHA256。
- `final-geometry-verification.json`：最终下端同一性和实测拇指凹。
- `MasterTenon_*.qa.json`、`cold-import.json`：实际 QA / 冷读。
- `preview-geometry-comparison.json`：预览阶段体积与三档网格数据，明确不是最终三角形数。

## 看图与重跑

`Previews/Compare_01_Front45.png`、`Compare_02_RightThickness.png`、`Compare_03_GripClose.png` 保留父代理查看的原始对照。最终 PBR 图使用无前缀的 `01_Front45.png` 至 `10_HighSameView.png`；`13_ThumbFrontClose.png` 正对凹面，`11_CleanWireFront.png` / `12_CleanWire45.png` 显示真实边线。`QA_material.png` 是技能的中性形体视图，实际 PBR 以产品视图为准。

在项目根目录，用独立 Blender CLI 运行 `build_tenon_r03.py -- --preview` 可复现小样流程；不带 `--preview` 完整造型、烘焙并导出，再执行 `verify_exports.py` 和 `render_clean_wire.py`。已冻结交付不要原地重跑；新修改另开版本。核心冻结不等于用户审美定稿或头显通过。

制作：Codex 子代理 `/root/tenon_model_build`，模型实际服务子型号未提供，2026-11-25（Asia/Shanghai）。父代理统筹并写项目交接；本子任务未再调用外部代理，未操作 Unity、正式 Assets 或共享工作台。
