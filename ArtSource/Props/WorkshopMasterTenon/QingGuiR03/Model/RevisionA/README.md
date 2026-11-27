# 清圭 R03 A：独立三件结构首样

这是供首轮结构/Unity 材质验证的候选，不是最终优化游戏资产。玉芯 `QingGuiR03_JadeCore_LOD0`、铜框 `QingGuiR03_BronzeFrame_LOD0`、金丝 `QingGuiR03_GoldWire_LOD0` 各为独立闭合网格、各一个独立材质。没有共用 Body 的铜玉混色贴图区。原 R02B 与 R03 首拍候选保持。

铜框有约 1.6 mm 壁厚、真实终止端面/内壁和约 0.18 mm 边圆转；可见唇高约 0.8 mm。玉芯有约 0.95 mm 实际承口，名义内侧余量约 0.15 mm、边缝约 0.1 mm。帽孔前后各 96 段、7 圈显式环带，孔壁 3 圈；不再把旧布尔孔的长瘦扇面直接做壳厚偏移。

Blender +Z、正面 −Y、右 +X，米制，底端中心原点；FBX 转 Unity +Y。总高 350 mm、最大包络约 110.628 × 107.567 mm。保护插端 0–105 mm 的 141 个顶点对照冻结功能接口，最大差 1.013e-9 m。实际 LOD0：玉 11,196 + 铜 21,328 + 金 7,500 = **40,024 三角、3 材质**；结构阶段预算高，尚未做最终 LOD/性能优化，不声称 Quest 达标。

A 恢复首拍候选意外未生成的 FrontFlow2/4/5，18 条原 C4 流向完整。旧投射函数遇新铜唇的侧向/下向面即停止；A 在小于金丝半径的接缝附近寻找外表面支撑，并让实心圆截面贴合真实侧壁。18 条四分段接触探针最大值均小于零，最浅埋入约 0.016 mm；这是离散采样，不等于连续碰撞证明。未新增装饰。

冷 FBX 与首样逐坐标集合、逐三角几何比较：**JadeCore/BronzeFrame 形状不变**，生成时顶点索引次序可不同；JadeCore 的 CORNER 法向改为可见型面/承口型面连续基准，承口侧壁独立法向。铜框法向未变，GoldWire 因恢复三线和贴合而变。Unity 已用关阴影/标准 Lit/法线可视对照确认首拍大三角硬分区的直接原因是 Mobile 阴影偏置，不能把 A 的法向/测厚质量改进写成对该阴影问题的修复。

`CandidateTextures/QingGuiR03_JadeCore_Thickness.png` 是 **新玉芯** 独立 UV 上 1,930,932 个真实像素位置的 BVH 测厚，方向为与导出一致的插值 CORNER 法向反向，起点内移 15 μm。5 个像素采用三角法向备用，2 个未命中保守取 120 mm，251,393 个超过 120 mm 的路径截断。线性解码 **R × 0.12 m**；8 px 仅作 UV 外填边。保留方向单路径、截断、PNG 量化近似，不是多重散射。`Diagnostic_VertexThickness.png` 仅作旧顶点插值方式的诊断对照，不能接为当前图。JadeMask 全 1，铜/金固定 0；未借用 B 的厚度或法线图。

FBX 冷导 QA 通过。GLB 原始 QA 有 4 对 UV/切线拆点公共边提示；仅在诊断副本以 1e-9 m 恢复相同坐标邻接，三角角点不变，仍用原 10 μm 距离检查通过，实际 GLB 未改。精确坐标、原报告、复核报告在 `revision-a/cold-exports.json`，投影面积另存。JadeCore 11 / BronzeFrame 2 个 10 μm 近邻顶点及 2 对埋入接触背向面提示保留；闭合性、退化、绕序错误为零。

`StructurePreview/11_UntexturedClayAssembly.png` 是统一无贴图灰色装配；`12_ThreeActualPartsExploded.png` 是**说明性三件拆解**，三件均为实际网格，不代表游戏散落行为。旧 `04_ExplodedClay.png` 只有玉/铜两件且铜有金属材质，媒体契约不会将其标为完整三件灰模。普通 GLB 是结构 PBR，不能代替 Unity 玉 shader。冠顶分段高光、铜尾转角小缺口列首样限制；浅刻云雷/刻度的最终细节烘焙、LOD 与最终玉材质尚未完成。本轮重点为分件结构，后续按用户实拍批注推进。

`frozen-manifest.json` 只冻结当前结构候选 5 个核心文件供 Unity/工作台同版本比对；`media-contract.json` 给来源和 SHA。构建/验证脚本在父级 Model：build_structure.py、rebuild_cap.py、qinggui_wires_r03.py、prepare_candidate.py、refine_thickness_texels.py、verify_revision_a.py、render_structure_sheets.py。日志在 `Logs/workshop-master-tenon-qinggui-r03-20261126/model/revision-a/`。本会话未改正式 Assets、Unity 或工作台。
