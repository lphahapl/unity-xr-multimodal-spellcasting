# 二章腕部「天工金石」界面实拍

使用前一轮 21 项验收的原二章副本（场景 SHA-256 前缀 `cdadb030`），在 Unity 6000.3.10f1 Play 中完成 25 项检查。四张原图均取自该玩家的 CenterEyeAnchor，同一眼位、朝向、视场与 1920 × 1440 分辨率。手部动作来自模拟观测。

| 玩家操作 | 同机位截图 | 实际显示 |
| --- | --- | --- |
| 新档抬起右掌 | [原始快览](01-glance-fresh.png) | 雷光剑、飞炎术、龙火之术 |
| 临时补全测试授权后抬掌 | [五项快览](02-glance-five.png) | 上述三项加护盾、火球术 |
| 用右食指按左腕准望匣朱砂扣 | [主菜单](03-case.png) | 术式教学、画符、咒语、术法状态，气血与度力 |
| 从主菜单用射线捏合选择「术法状态」，放下左腕 | [独立状态页](04-status-five.png) | 五张图标、跟随／固定及关闭入口 |

三份界面脚本 WristMenuView、SkillStatusRows、KaogongUISprites，图集 KaogongUIAtlas.png 及各自 .meta 全部与正式工程逐字节一致。额外比对了图集导入器和五张技能 PNG 的 .meta 与原件，一共 20 个文件，检查开始时已全部相同。[正式和副本的逐文件 SHA-256](hashes.json)

运行时加载了 16 个原生 Sprite 子资源。实际菜单引用九宫格青铜框 Frame_Bronze_Main、按钮 Button_Normal 和四枚入口纹样 Icon_Seals、Icon_GlyphBrush、Icon_VoiceBell、Icon_StatusWheel。主题开关为真，标题颜色为温玉色 `#E3DDD0FF`。快览和独立状态页运行中的五个图标分别来自 SkillStatus/shield、fireball、thunder.sword、breath.burst、breath.stream。仅在该 Play 的玩家内存临时补齐授权，使用独立 QA 存档名。16 个子资源的完整名称、当前界面实际引用、相机位置和可见范围见[运行记录](ChapterWristGoldCapture.txt)。

[可复测脚本](ChapterWristGoldCapture.cs) 放在隔离工程的 Assets/scripts/logic/Editor/Interaction 目录，将 CH02_WRIST_GOLD_OUTPUT 指向已有输出目录，批处理运行 NarutoVR.SealCombat.Editor.ChapterWristGoldCapture.Run。Unity 参数为 -batchmode -force-d3d11，脚本自行退出。[实测回执及截图 SHA-256](receipt.json)

本次交件为外观验收证据。原二章负责人可继续在最新二章基线上合入[腕部装配增量](../README.md)。
