# T-25 A4　十字铜带镇石

对应 A3 在 2026-11-25 17:02 的批注。中央上带跨过下带，两道铜带经四侧扣向内收底托，顶面四个朱角在交叉处周围组成同一枚方印。工作台保留玩家俯看、侧背和搬运实配图，并新增从正式 Prefab 实拍的正、背、左、右四向图。[玩家俯看](http://127.0.0.1:4190/plans/ch02-t25-ballast.html?v=a4&view=top) · [正式四向](http://127.0.0.1:4190/plans/ch02-t25-ballast.html?v=a4&view=production-four) · [三维模型](http://127.0.0.1:4190/rig/?collection=t25-ballast&model=t25-ballast-a4)

用户在 2026-11-25 21:40:14（北京时间）于玩家俯看视图写下“通过”，21:40:39 于同版三维模型写下“通过”。[两处审批记录](../../../Tools/Validation/Evidence/T25Approved/approval.json)

## 给场景负责人的接入约定

- 工奴的生成配置继续引用原 [BallastStone.prefab](../../../Assets/Art/Enemies/CanJiOu/BallastStone.prefab)。其中的显示网格已换成 A4。外观子根使用零平移、单位旋转、单位缩放。原根对象保留 1.05 m 碰撞盒、5 kg 刚体、千钧符受力组件及组件文件 ID。[根对象逐字段核对](../../../Tools/Validation/Evidence/T25Approved/root-contract.json) · [外观源文件](Model/README.md)
- 已在独立 Unity 6000.3.10f1 工程实际实例化该 FBX。21,456 个三角形，在单位变换下与测距 OBJ 的顶点云双向最大偏差约 0.000032 mm。普通交换 FBX 在此 Unity 导入流程下会左右镜像。现场用文件 `T25-A4-Unity.fbx` 的 SHA-256 为 `297a3ef837361576654a97eac4293f9c2f20f56358761755f5a2e45a780cabc4`。
- 石材和铜的 BaseColor 按 sRGB，NormalGL 按 Normal Map，强度分别为 0.85 和 0.65。URP Lit 的 Metallic Map 可用 [Stone](Textures/Unity/Stone_MetallicSmoothness.png) 与 [Bronze](Textures/Unity/Bronze_MetallicSmoothness.png)，R 为金属度、A 为光滑度，均以线性采样。AO 的灰度版为 [石](Textures/Unity/Stone_Occlusion.png) 与 [铜](Textures/Unity/Bronze_Occlusion.png)。保留 FBX 自身 UV 密度，朱记复用随包的颜色贴图。[原 PBR 通道与材质近照](Textures/README.md)
- 搬运曲线给出石根相对角色根的 XYZ、四元数 xyzw 和缩放，按唯一搬运剪辑的第 0～86 帧、30 fps 采样。只在 Carried 阶段应用。掉落沿用原刚体，入槽由 `BallastStone.Dock` 对准现有 `BallastBasket.slot`。[姿态 CSV](Verification/carry-poses.csv)

## 审批时的几何采样

| 检查 | 冻结版结果 |
|---|---|
| 模型外廓，X × Y × Z | 1.0116 × 1.0470 × 1.0208 m，底面在根下 0.525 m |
| 搬运全身三角面 | 87 帧无相交，最近 4.006 mm，第 65 帧 |
| 右手长件与底托 | 最近 39.285 mm，第 55 帧。80 帧包围盒重叠，三角面均分离 |
| 左掌、右掌与石体 | 最远间隙分别为 8.406、9.399 mm，均在第 67 帧 |
| A 篮锁槽姿态 | 四边净余分别约 119.2、119.2、119.2、110.0 mm。向下至槽底的数值残差约 0.003 mm |
| 入槽角度 | 居中、竖直、绕 Y 轴每 0.25° 检查，在 −19.25° 到 +19.25° 的连续采样内可整块直落。原搬运斜角不直接用于锁槽 |

上表对应审批时的独立采样。篮槽取自场景 `1B4A4E9D…` 的副本。以锁定中心为原点，周边 13 个非触发盒及 5 组实景三角网格均与就位后的石体分离。[审批时搬运逐帧记录](Verification/carry-clearance.csv) · [当时槽口回读](Verification/basket-fit.json) · [角度采样](Verification/basket-yaw.csv)

正式 Prefab、运行时 Animator 及同一镇石从掉落到入槽的回读，见[主工程接入与场景补丁交接](../../../Tools/Validation/Evidence/T25Approved/README.md)。
