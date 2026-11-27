# 二章 A、B 篮载人运输

## 玩家操作

- **A 篮用镇石配重。** 祭坛三敌清场后，走进上层 A 篮甲板，取墨规笔在自由画板上画千钧符，把工奴掉下的同一块镇石牵入槽。站在槽边时向侧面让开。原石就位、玩家登板后吊篮下降至中层。[登板与防误启动](../../../Chapter02RemoteLever/A-boarding.txt) · [三敌清场、原石入槽与步出中层](../../../Chapter02RemoteLever/A-production-route.txt)
- **B 篮用东侧青铜分控杆。** 工坊清场，从取用台拿主榫回④中控插到底。站在东侧杆柄约 3 米内，张开右手指向铜横柄。看见淡金射线落在柄面、握柄亮起后，捏住并向胸前回拉约 14 厘米。未拉满松手会复位；近处也可直接握柄按实际摆向拉下。手被其他交互占用、手势失追、有墙遮挡时不会远程选中。[站姿瞄中](../../../Chapter02RemoteLever/standing-aim.png) · [捏住回拉](../../../Chapter02RemoteLever/standing-pull.png)
- 岸上拉满只把 B 篮置为等待乘客。走进篮内甲板才出发，手指伸进去或站在岸上对准杆柄都不会开程。A、B 分别用实际登板与机关条件判断能否启程。到站后步行下篮。

Unity 6000.3.10f1 的独立项目重开二章进行桌面 Play。每轮使用单独游戏存档。正式主编辑器在核对唯一已加载场景、未进入 Play、无未保存改动、磁盘输入和候选 SHA 后替换文件，并重开检查原绑定。

| 段 | 正式读回 SHA-256 | 桌面结果 |
| --- | --- | --- |
| A | `5dd207725cd973a5cc2f32b04b52322bdf7aa38fbeef5cd4adf79577f7f4818c` | [原石、随篮与步行](A/a-ride.txt)、[离篮和途中恢复](A/a-interrupted.txt)、[中层重启](A/a-cold.txt)、[主编辑器读回](A/main-publish.txt) |
| B | `8c476f15b1774330aeb1ec0461b5bf328b5dc94f506a7de5441a50d2bb0a7af9` | [拉杆、乘篮和走出谷底](B/b-ride.txt)、[插榫前禁用](B/b-early.txt)、[中途读档](B/b-interrupted.txt)、[谷底冷启](B/b-cold.txt)、[旧完章迁移](B/b-legacy.txt)、[主编辑器读回](B/main-publish.txt) |

A 首次正式发布测得 A 实际运输 10480 帧，rig/head相对篮漂移 0.00391/0.00391 m，旋转 0.000/0.000°，始末头部方向 (0.0000, 0.0000, -1.0000)/(0.0000, 0.0000, -1.0000)。最终版复跑结果为 A 实际运输 14117 帧，rig/head相对篮漂移 0.00002/0.00002 m，旋转 0.000/0.000°，始末头部方向 (0.0000, 0.0000, -1.0000)/(0.0000, 0.0000, -1.0000)。[最终版 A 复测](B/final-a-ride.txt)。B 停靠测得 实篮垂降 80m，6.00s，定目帘与乘员锁恢复，漂移 0.0000m，强制转角 0.0000°，分轴误差 (0.00000, 0.00000, 0.00000)，乘员 True，入站 1，中段遮景 True。路线、存档节点、原笔、左右手和口粮引用见逐项记录。头戴设备上的遮挡观感和体感舒适度待实测。

在临时项目中把 `Editor/Chapter02BasketA*.cs` 和 `Editor/Chapter02BasketB*.cs` 复制到现有 NarutoVR.Game.Editor 程序集下。正式场景已经装好。指定环境变量 `CH02_BASKET_QA_OUT` 为每段独立的可写目录，把该段 Fixtures 内文件复制进去。使用 `NarutoVR.SealCombat.Editor.Chapter02BasketAChecks` 的 `Ride`、`Interrupted`、`Cold` 和 `Chapter02BasketBChecks` 的 `Early`、`Ride`、`Interrupted`、`Cold`、`Legacy`。Ride 产生后续冷读所需的存档。Unity 使用 `-batchmode -nographics -executeMethod` 启动，由每个 Play 入口主动退出。原始输入与输出场景差异分别见 [A](A/scene-diff.json) 和 [B](B/scene-diff.json)。
