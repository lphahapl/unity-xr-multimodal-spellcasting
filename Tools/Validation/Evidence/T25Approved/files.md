# T-25 A4 · 正式变更文件

可从[交付报告](README.md)返回实测与审批。表内路径以仓库根目录为起点。场景源文件由唯一写入人控制，校验用的运行日志在本机 Logs/CH02-T25-A4-production 下。

## 游戏代码与配置

| 路径 | 本次用途 |
|---|---|
| `Assets/scripts/logic/Enemies/EnemyController.cs` | 在既有工奴分支接入持姿启动、导航移动、暂停、受击接管及结束。 |
| `Assets/scripts/logic/Enemies/StoneCarrier.cs` | 只在持有当轮石块时，按角色 Animator 的实际相位更新石根。 |
| `Assets/scripts/data/Enemies/Spawning/StoneCarrierProfile.cs` | 增加搬运持姿资产的引用。 |
| `Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset` | 指定 A4 持姿并保留原石组件引用。 |
| `Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller` | 增加显式进入的搬运状态与该状态专用速度参数。 |
| `Assets/scripts/data/Enemies/Spawning/StoneCarryPose.cs` | 新增循环剪辑与石根姿态采样，提供相邻帧插值。 |
| `Assets/scripts/data/Enemies/Spawning/StoneCarryPose.cs.meta` | 新对象导入标识。 |
| `Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset` | 记录原搬运剪辑、87 组位置与四元数。 |
| `Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset.meta` | 新对象导入标识。 |

## 实物与导入设置

| 路径 | 本次用途 |
|---|---|
| `Assets/Art/Enemies/CanJiOu/BallastStone.prefab` | 原物理石只替换显示子实例。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4.meta` | Unity 资产或目录标识。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials/Bronze.mat` | 为相应网格指派 URP Lit 贴图与参数。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials/Bronze.mat.meta` | Unity 资产或目录标识。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials/Cinnabar.mat` | 为相应网格指派 URP Lit 贴图与参数。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials/Cinnabar.mat.meta` | Unity 资产或目录标识。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials/Stone.mat` | 为相应网格指派 URP Lit 贴图与参数。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials/Stone.mat.meta` | Unity 资产或目录标识。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Materials.meta` | Unity 资产或目录标识。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/T25-A4-Unity.fbx` | 批准网格的 Unity 专用 FBX，米制、单位变换。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/T25-A4-Unity.fbx.meta` | Unity 模型导入与材质映射。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_BaseColor.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_BaseColor.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_MetallicSmoothness.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_MetallicSmoothness.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_NormalGL.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_NormalGL.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_Occlusion.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Bronze_Occlusion.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Cinnabar_BaseColor.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Cinnabar_BaseColor.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_BaseColor.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_BaseColor.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_MetallicSmoothness.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_MetallicSmoothness.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_NormalGL.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_NormalGL.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_Occlusion.png` | 批准的 PBR 贴图，通道与采样设置见美术回读清单。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures/Stone_Occlusion.png.meta` | Unity 贴图导入参数及 GUID。 |
| `Assets/Art/Enemies/CanJiOu/BallastStoneA4/Textures.meta` | Unity 资产或目录标识。 |

## 工作台与源包

| 路径 | 本次用途 |
|---|---|
| `SharedWorkSpace/config/plans/ch02-t25-ballast-a4.json` | 保留用户原批注，登记通过状态与正式四向、搬运及槽口回读。 |
| `SharedWorkSpace/config/rig/t25-ballast.json` | 标记 A4 三维版本已通过，保留同版原始批注。 |
| `SharedWorkSpace/plans/ch02-t25-ballast.html` | 呈现已通过状态及同版批注入口。 |
| `SharedWorkSpace/assets/525c9b32e27c7f397bbde9e151f297f3c13209ed870ef2f8adad300df4dafd30/T25-A4-formal-four-views.png` | 发布正、背、左、右四张正式 Unity 截图的拼图。 |
| `SharedWorkSpace/results/T25-A4/README.md` | 将批准时的源包测距与正式 Play 分列，加入审批、正式实物和交接入口。 |
| `SharedWorkSpace/results/T25-A4/Model/README.md` | 说明获批源模型的正式用途。 |
| `SharedWorkSpace/results/T25-A4/Verification/files.sha256` | 更新源包内 50 个文件的校验清单。 |

## 复核和场景交接

| 路径 | 本次用途 |
|---|---|
| `Tools/Validation/Evidence/T25Approved/approval.json` | 用户审批记录与同版三维附件。 |
| `Tools/Validation/Evidence/T25Approved/Art/04-top.png` | 从正式 Prefab 导出的 Unity 棚光截图。 |
| `Tools/Validation/Evidence/T25Approved/Art/05-quarter.png` | 从正式 Prefab 导出的 Unity 棚光截图。 |
| `Tools/Validation/Evidence/T25Approved/Art/four-cardinal.png` | 从正式 Prefab 导出的 Unity 棚光截图。 |
| `Tools/Validation/Evidence/T25Approved/Art/production-checks.txt` | Unity 美术/物理回读断言。 |
| `Tools/Validation/Evidence/T25Approved/Art/production-integrity.json` | 网格、逐文件散列与导入回读。 |
| `Tools/Validation/Evidence/T25Approved/Art/workbench.json` | Chrome 回读正式四向深链。 |
| `Tools/Validation/Evidence/T25Approved/Motion/play-checks.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/Motion/readback-comparison.json` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/Motion/README.md` | 对应目录的接入或验收说明。 |
| `Tools/Validation/Evidence/T25Approved/Motion/runtime-frames.csv` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/Motion/runtime-poses.csv` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/Motion/StoneCarryPoseChecks.cs` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/Motion/tested-inputs.json` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/README.md` | 对应目录的接入或验收说明。 |
| `Tools/Validation/Evidence/T25Approved/root-contract.json` | 原根对象与生产 Prefab 逐字段对照。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/bone2/candidate.csv` | 测距时实际使用的位置和旋转。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/bone2/frames.csv` | 逐采样帧的碰撞、包围、双掌与长件净空。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/bone2/summary.json` | 该批输入的哈希、最近及最远帧。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/bone4/candidate.csv` | 测距时实际使用的位置和旋转。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/bone4/frames.csv` | 逐采样帧的碰撞、包围、双掌与长件净空。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/bone4/summary.json` | 该批输入的哈希、最近及最远帧。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/default/candidate.csv` | 测距时实际使用的位置和旋转。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/default/frames.csv` | 逐采样帧的碰撞、包围、双掌与长件净空。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/default/summary.json` | 该批输入的哈希、最近及最远帧。 |
| `Tools/Validation/Evidence/T25Approved/RuntimeClearance/verify_runtime.py` | 依据实际运行时模型、同帧石根与骨骼复算可见三角面距离和包含关系。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/prepare_snapshot.py` | 将生产输入复制至专用验收副本并记录散列。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/README.md` | 对应目录的接入或验收说明。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/T25AltarRoomInstaller.cs` | 场景负责人使用的祭坛工奴安装器，支持 Undo。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/T25ApprovedTransit.cs` | 专用副本的运输与安装器回归。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/结果.md` | 场景补丁、实测边界、源文件与运行结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/as-found.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/exit-code.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/input-hashes.json` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/installed-reopened.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/installer-assertions.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/play-assertions.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/post-play-differences.txt` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/SceneHandoff/运行证据/transit.csv` | 对应验收步骤的原始输入、探针或回读结果。 |
| `Tools/Validation/Evidence/T25Approved/serialized-poses.json` | 87 个采样与实际剪辑引用对照。 |

## 项目交接

| 路径 | 本次用途 |
|---|---|
| `docs/模型交火/需求变更交接.md` | 在末尾追加本次正式接入记录。 |
| `docs/模型交火/代码审阅交接.md` | 在末尾追加本次正式接入记录。 |
| `docs/模型交火/世界观变更交接.md` | 在末尾追加本次正式接入记录。 |

本文件记录上述文件与用途，由本轮生成作为交接索引。
