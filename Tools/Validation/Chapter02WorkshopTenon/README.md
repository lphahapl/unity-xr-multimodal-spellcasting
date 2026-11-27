# 二章工坊取榫装配

## 二章整章装配

11 月 26 日的[官方双手、断桥、祭坛工奴与 B1 台座联合交件](../Evidence/Chapter02Assembly/combined-1126/README.md)以 `e82f24bd` 的正式场景为基线，提供整场文件、官方左右手的输入绑定、相邻对象差异和读档证据。下方 R3 记录对应前一版工坊玩法合入。

## 炉房 R3 交件

**场景负责人已在合入候选工程安装玩法补丁。** 复验输入是 D 盘正式场景 `Assets/Scene/Chapter02_SuspendedPath.unity`，SHA-256 为 `1b4a4e9d8326d6c9372474dc78f807c625dbe8b0e7e7dd0d202ce88d42761a38`。接收方记录的补丁 SHA-256 与交件一致，为 `4885679d91115d2cc831e58c61c98bc5af89f071ac7bd87c5fea470be6f9ab4e`。正式场景的最终保存和冷重开由唯一写入人负责。[合入补丁](R3/workshop-r3.patch) · [装配与复跑说明](R3/README.md) · [接收方安装记录](R3/scene-owner-receipt.txt)

- **玩家与阶段。** 使用 `WorkshopTenonAssembly.CreateChapterPlayer(scene, out flow, out manager)`。将工坊目标放入 `CH02_WORKSHOP_TENON` 一行。③先等 F1/F2 屋顶预置房间和院场精英租约房间都已启动且清场，再开放拾取。④归座后由 `ObjectiveChapterFlow` 提交一次并转入 `CH02_B_DESCENT`。分控读持续状态 `IsSeated`；首次归座可订阅 `Seated`。正式路线沿用上层出生点和 A 篮抵达④的阶段交接。
- **美术替换。** 调用 `BindWorkshop(scene, player, manager, stage, propPrefab, authoredHolder)`，主榫 Prefab 与③承座挂点可独立替换。生产默认 `includeReviewJaws=false`，保留锁定与开放两个空挂点，均无网格和碰撞体。已有功能灰盒可直接传入；也可用 `WorkshopWhiteboxTenon.FromStand` 从原白盒网格提取可拾主榫。
- **实测范围。** Unity 6000.3.10f1 在上述场景的同哈希副本通过 **30/30 项桌面 Play 检查**，退出码为 0。覆盖两种清场顺序、屋面与地面导航、坠落回座、误插与取消、单次提交和存读档。冷重开验证了一个玩家、两类房间、重复装配复用原引用；样件外观引用数为零。右手捏合使用桌面输入样本，Quest 持榫传送与 A/B 篮全程仍待实机联验。[逐项 Play 结果](R3/play.txt) · [冷读记录](R3/cold.txt)

## 早期 R01 中层演示 · 28 项回归

原切片从④缺榫开始，展示双销提案与 R01 主榫。可在本机用 Unity 6000.3.10f1 打开[隔离工程](D:/Temp/Ch02TenonIntegration1125/Project)中的[中层可玩切片](D:/Temp/Ch02TenonIntegration1125/Project/Assets/Scene/Chapter02_WorkshopTenon_Slice.unity)。以下记录保留该版的原稿依据、包和审稿入口。

### 原稿和新增提案

V2.1 阶段稿及动线修订写明，玩家在③消灭工坊守机，从座上取回汉白玉校准主榫，走回④插紧，解开操纵机关，朱砂红转冰蓝，左右分控通电。V2.4 将本段的取榫返程定为 34.4 米。②的背石工奴掉落压舱镇石，供 A 篮配重。两件物品各用自己的状态和输入。[阶段稿提取，第 9、33、47、53 段](../../../ArtSource/Props/WorkshopMasterTenon/Reference/V21-text-extracted.txt) · [V2.4 规格](../../../docs/architecture/第二章_断衡悬道/04_场景距离规格.md)

工坊进入守备状态时，两枚铜销扣住校准座上的主榫，以免交战时有人误启巨衡。最后一组守机停机，两销退开，玩家可以拔榫。这是待用户审美与叙事确认的提案。切片已做带碰撞体的两态挡销，先用暗锈铜材质验证。主榫取用外观使用道具负责人 R01 样件。

### R01 切片装配入口

- [仅章节接线](Ch02WorkshopHooks.unitypackage) 包含运行脚本、编辑器装配入口与两个阶段资产。用于已有本仓库依赖的同版本工程。
- [完整隔离切片](Ch02WorkshopSlice.unitypackage) 另含演示场景、剔除占位玉榫的承座网格，以及当前 R01 道具依赖。导入后打开 `Assets/Scene/Chapter02_WorkshopTenon_Slice.unity`。其余敌人、技能、导航和场景材质由本项目提供。
- 编辑器入口为 `NarutoVR.SealCombat.Editor.WorkshopTenonAssembly`。`BuildSlice()` 从二章场景生成或重建上述切片，会重存同名切片。正式装配使用下面两个入口。
- `CreateChapterPlayer(scene, out flow, out manager)` 取得或建立通用 `LinearRpgController`、`ObjectiveChapterFlow`、`EnemyManager`。初次建立时移入项目现成 XR Rig 与同一套 SealLab、语音和技能匹配器，沿用当前绑定的 XR Hands 跟踪源，通过玩家地面控制器移动 Rig。通用玩家默认存档文件是 `chapter02-suspended-path.json`，演示切片单独使用 `ch02-workshop-slice.json`。
- `BindWorkshop(scene, player, manager, workshopStage, tenonPrefab, authoredHolder)` 返回 `WorkshopTenonObjective`。传入的主榫根需沿用 R01 的 `GameItemBase`、根部单刚体、插入端原点和 +Y 榫轴。沿用当前白盒座可省略最后一个参数。更换承座美术后，传入道具底端在真实托面上的挂点。正式夹扣可替换 `closedJaws` 与 `openJaws` 两组物件。
- 将返回的目标放入本章 `ObjectiveChapterFlow.stages` 中对应 `CH02_WORKSHOP_TENON` 的一行，给 `LinearRpgController.ConfigureRoute` 的同一行指定合法检查点。正式章节前接 A 篮到④，后接中枢修复后的 B 篮分控。演示到此开放下一阶段，实际升降由分控系统处理。演示的 `CH02_B_DESCENT` 用 `SignalChapterObjective` 等待后续机关的真实完成信号。
- 世界表现读取 `IsSeated`。现场第一次插到底时另发 `Seated`。将正待启用的分控表现引用放进 `poweredCircuits`，即可兼顾现场插紧和读档恢复。切片已接到 A、B 分控杆的冰蓝灯，缺榫与归位灯接在④。实际调用升降前读取 `IsSeated`。
- 编辑器二次装配会核对玩家、目录、阶段和主榫 Prefab。运行时读取脚底是否入院。F1、F2 两名屋顶驻点共用预置敌人房间，院内精英独立使用同一边界的异步租约房间，精英的召唤物仍归其自身租约生命周期管理。两个房间都 `HasStarted && IsComplete` 才松扣。
- 右手在握位旁先张开再捏合，复用 `PlayerInputController` 的持物占用。掉到有效地面可就近再握，低于承座 2 米回原座；未持有且松手满 12 秒也回座。短暂丢追踪冻结，持续超过 0.35 秒安全松手。掉落和丢追踪不会提交。
- ④松手时榫底需在真实开口上方 16–120 毫米、局部 X/Z 各偏差至多 12 毫米，姿态距最近一个方榫正交朝向至多 12°。SnapZone 先在开口上方对齐，再下压至槽底，坐入深度约 104 毫米。只有吸附完成才将状态提交给章节系统。两段临时参数可在真机握持回归后调整。

### R01 验收与边界

[Unity Play 逐项断言](play.txt) · [二次装配与主相机核查](assembly.txt) · [锁闭近景](clamp-closed.jpg) · [开放近景](clamp-open.jpg)

使用真正的房间入场、加载、敌人受击死亡、物品持有、SnapZone 动画以及章节存读档。双手捏合输入使用桌面观测样本。Quest 手指实测、持榫传送同行、出入全章上下层、B 篮真实升降和正式中控美术灯效留给整章联验。该切片没有首章教学、关卡技能奖励或货币奖励。
