# 第二章工坊炉前干粮

正式测试场景为 [Chapter02_SuspendedPath](../../../../Assets/Scene/Chapter02_SuspendedPath.unity)。新局在上层祭坛清完三敌，乘 A 篮到中层。工坊木屋前的主锻造炉面向玩家时，四饼包在炉口左前沿，散饼在右前沿。[站在炉前看](furnace/standing.png) · [完整炉体](furnace/context.png)

在炉前用空手指向任一件，看见射线标记后捏合，也可以靠近直接捏取。收起整包要对准腰前中央软袋。只吃散饼时，整包仍有四块。满血可直接收包，再去取主榫。

## Unity 桌面实跑

Unity 6000.3.10f1 在关闭 XR 的项目副本重开合成场景。最终场景 SHA-256 为 `6c30b163aa06ec42bfb14822dc4f26fbe494e6cbb257ed62d6c405077c3bfbb1`。三敌逐个通过战斗接口受击；脚底位置、手势和手腕姿态由驱动输入。桥前置教学在炉台专项测试中直接推进。

- [炉台食品及实际尺寸](furnace/validation.json)和[冷启动](furnace/cold/checks.txt)覆盖炉沿碰撞、腰袋收纳与独立的第五块。初次拿起恢复正常手持尺寸。两件物品的世界位置、网格尺寸、脚下站位见[测量](furnace/placement.txt)。
- [组合场景](integration/chapter-fresh.txt)从实际清场和取物流程验证语句开始与结束事件。散饼经真实嘴部吸附，受伤时回复 15；只有整包才完成 RT01。满血四饼完整归入中央袋口完成 RT03。[独立进程冷读](integration/chapter-cold.txt)核对散饼消耗、整包数量、归口及旧提示不重播。
- [旁白逐句验收](narration/checks.txt)检查全部 32 个语句的声源实际起播、跨层隔离、提前远抓、离开可选教学与工坊主线接管。[台词与资源校验](narration/script-audit.json)记录新旧资源及字幕一致性。
- [场景引用读回](reference-audit.json)列出变更的序列化对象、两处炉沿锚点和旁白资源。原料包装和散饼均在运行时按清场状态陈列。

[统一道具场移动](adjacent/walk.txt)、[全道具取放](adjacent/joint.txt)、[双手取食](adjacent/food.txt)与[两条射线指向不同道具](adjacent/ray-split.txt)均完成回归。另跑了占用与回槽、松墨与拓片、镇桩、剧情墨规笔及干粮的抓取入口。

Android ARM64 / IL2CPP 的 [Player 脚本编译](android/checks.txt)成功生成 68 个程序集。

本轮没有连接 Quest，头显中的可读性、追踪和听感待体验。
