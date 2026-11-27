# 现版工坊玩法接线交接

供二章正式场景唯一写入人使用。输入为炉房 R3 已落盘后的 `Assets/Scene/Chapter02_SuspendedPath.unity`，SHA-256 为 `1b4a4e9d8326d6c9372474dc78f807c625dbe8b0e7e7dd0d202ce88d42761a38`。在独立工程按该文件生成临时副本，装配一个真实 RPG 玩家、两个战斗房间和一组主榫交互。验证从④进入工坊目标，正式场景使用装配端的上层出生和完整路线。

## 合入

从仓库根目录对 [workshop-r3.patch](workshop-r3.patch) 运行 `git apply --check --ignore-space-change Tools/Validation/Chapter02WorkshopTenon/R3/workshop-r3.patch`，再用相同参数去掉 `--check` 应用。补丁 SHA-256 为 `4885679d91115d2cc831e58c61c98bc5af89f071ac7bd87c5fea470be6f9ab4e`。其中包含一项已有 Editor 装配器修改、一份可选白盒提取脚本和该脚本的 meta。

场景负责人已在正式合入候选工程中安装本补丁，写入的补丁路径与 SHA-256 一致。[接收方安装记录](D:/Temp/T27T28FormalRuntime1125/installed-system-package.txt)

- `BindWorkshop(scene, player, manager, stage, propPrefab, authoredHolder)` 继续接受场景负责人自己的可替换主榫 Prefab 和承座挂点。生产默认的两个状态节点只代表锁定与开放，其下的网格与碰撞体数量都为零。旧切片在 `BuildSlice()` 中显式设置 `includeReviewJaws:true`，保留其审稿用途。
- 需要复用场景原占位物时，可在装配前调用 `WorkshopWhiteboxTenon.FromStand(standMeshFilter)`，它直接提取白盒榫本身的子网格、UV 与原材质并补上握点与单刚体。已有功能 Prefab 时可直接把它交给装配器。
- 若候选场景已用旧版生成，重新从装配输入建立，或清理现有锁定/开放状态节点下的旧双销子物体，保留两个状态节点和目标的序列化引用。重复调用装配入口会复用既有物件。
- 检查正式场景内的工坊阶段、出生位置、检查点和 B 篮阶段属于整章路线。保留现版 F1/F2 驻点、两个独立来源的房间，以及已经烘焙的地面导航。

## 独立回归

Unity 6000.3.10f1 执行 `NarutoVR.SealCombat.Editor.R3WorkshopBuild.RunAndPlay`，进程退出 0。30 项桌面 Play 断言通过，覆盖锁定、两个清场顺序、取榫、失手恢复、误插、坐实提交、存读档及现版屋面与院坪导航。冷重开后验证了装配幂等、角色和房间引用，以及待审道具引用数为零。[Play 逐项结果](play.txt) · [冷读记录](cold.txt)

复跑时在同版本独立工程中应用补丁，并将 [建场检查](R3WorkshopBuild.cs) 和 [Play 检查](R3WorkshopPlay.cs) 放进 `Assets/scripts/logic/Editor/WorkshopTenon/`。脚本会使用独立的 `Chapter02_R3_WorkshopGameplay_QA.unity` 和测试存档。右手捏合通过桌面输入样本驱动。头显握持与传送、A/B 篮实际升降和整章连续运行另由正式装配联验。
