# 第二章工坊屋顶射手安装与房间接线

## 安装驻点

1. 在编辑模式打开 `Assets/Scene/Chapter02_SuspendedPath.unity`，执行 **Tools → NarutoVR → 第二章断衡悬道 → 安装工坊屋顶双射手**。检查 Hierarchy 中 F1 锻造房下出现 `ForgeRoofPost`，F2 炭棚下出现 `ShedRoofPost`。重复执行会复用这两组实例。场景负责人完成统一装配后保存。
2. 每个驻点根上的 `MeshCollider` 使用本栋 LOD0 瓦面，`NavMeshSurface.navMeshData` 引用随包提供的烘焙结果。两栋的专用区域分别是 `WorkshopForgeRoof`（3）和 `WorkshopShedRoof`（4）。每名射手的 `NavMeshAgent.areaMask` 只含自己的屋顶。建筑与驻点使用单位缩放，移动或旋转时一起操作建筑父节点。
3. 每个驻点内嵌一具正式掷火残机，`EnemyController.patrolPoints` 引用同一驻点里的两个子节点。F1 用固定巡逻，F2 用动态射位，射位受挡或不可达时退回屋顶巡逻。两者继承正式敌人的停留时间 1.5 秒。

| 当前 A1/A2 场景 | 世界出生点 XYZ（米） | 巡逻端点 1 → 2（米） |
|---|---|---|
| F1 锻造房 | (11.497, 4.463, 44.422) | (11.200, 4.494, 41.035) → (11.793, 4.462, 47.809) |
| F2 炭棚 | (10.483, 2.691, 55.872) | (10.751, 2.774, 54.497) → (10.216, 2.744, 57.246) |

## 将射手交给统一战斗房间

- 编辑器装配脚本可直接调用 `NarutoVR.Editor.WorkshopRoofEncounterSetup.InstallPosts(scene)`，返回顺序为 F1、F2 的两个**场景实例** `EnemyController`。将它们与已有场景敌人去重后合并到 `CombatRoomEncounter.enemies`。在 Inspector 中也可将驻点内的两具残机拖到「房间敌人」。
- 本方案走房间的场景敌人分支。该房间的 `enemyManager` 留空。若统一接线采用池化的 `enemyManager`，运行时会读取生成配置，直接填写 `enemies` 对该分支无效。
- 设置 `CombatRoomEncounter.rpg`、触发盒 `area` 和 `LinearRpgController.roomControlsEncounter = true`。玩家需具备有效的 `locomotion.head` 和 `RpgDamageReceiver`。驻点里的 `EnemyController.target` 初始为空，`isSpawnedEnemy` 为假。玩家进入后，房间通过 `InitializeSpawn` 把两名射手的目标绑定为同一玩家头部。玩家识别、受击与衡印沿用玩家本身的组件。火核由开火者持有发射来源，回程是否伤到原主取决于实际碰撞。

## 场景外文件

- [驻点目录](../../Assets/Prefabs/WorkshopRoofEncounter/) 包含 `ForgeRoofPost`、`ShedRoofPost` 两个 Prefab，以及各自的 `Nav.asset` 导航和 `Tiles.asset` 瓦面碰撞。迁移整个目录及目录外的同名 `.meta`，保留每项 GUID。
- [导航区域](../../ProjectSettings/NavMeshAreas.asset) 记录第 3、4 槽名称。[安装脚本](../../Assets/scripts/logic/Editor/WorkshopRoofEncounterSetup.cs) 及其 `.meta` 放在现有 Editor 程序集中。运行时直接使用序列化的 Unity 组件与[正式掷火残机](../../Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab)。

## 隔离重放

在独立工程副本将 [RoofTrial.cs](../../Tools/Validation/RoofTrial.cs) 复制到 `Assets/scripts/logic/Editor/`，调用下列命令。测试保存并重新打开 `Assets/Scene/T16_InstalledRoofTrial.unity`，加载两份正式驻点导航，另在副本中为院落地坪建立可走区域进行断路对照。

```powershell
& 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe' -batchmode -nographics -projectPath $projectCopy -executeMethod RoofTrial.Run -installedRoofPosts -logFile $logFile
```

Unity 6000.3.10f1 Play Mode 实测两名射手各巡到两个端点，边缘和遮挡两种情况均回退。模拟玩家在第 1066 帧释放一次左手衡印，第 1070、1078 帧两枚火核弹回。两名移动射手均保留 150 生命，玩家生命和盾量均为 100。随后独立验证房间登记、绑定玩家头部、离房暂停、重入恢复、双亡清场和重置。正式玩家装配、Quest 结印时机和实机可读性由场景合并后验收。[逐项结果](../../Tools/Validation/Evidence/T16Roof/installed.txt) · [逐帧轨迹与资产哈希](../../Tools/Validation/Evidence/T16Roof/installed-samples.zip)
