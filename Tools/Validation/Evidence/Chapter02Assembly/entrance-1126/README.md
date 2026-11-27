# 二章入口、祭坛三敌与两种自由符

在 Unity 6000.3.10f1 打开正式场景 `Assets/Scene/Chapter02_SuspendedPath.unity`。新档从①石门外向石案行走，桥头就能看到对岸三具残机。主编辑器已将下列运行接线回读到同一场景。[正式回读](trio/main-publish.txt)

| 玩家操作 | 桌面 Play 验证 |
| --- | --- |
| 从出生走到断桥，在石案按铜扣，取腰间墨规笔画方直之矩 | 靶场文字和十字默认隐藏。站在断桥就能分辨正中的搬运者、旁边的近战和掷火残机，守敌此时不能攻击或受击。[桥头实拍](trio/primary/02-bridge-before-writing-numbered.png) |
| 修桥后走过祭坛接敌线 | 原三具实例接敌，近战实际追击，掷火手从原炮口发射有伤害的投射物。搬运者持同一块 A4，受非致命攻击后撤离。[新档实跑](trio/primary/trio.txt) · [同场三敌](trio/primary/05-first-altar-trio-numbered.png) |
| 击败三敌、登上 A 篮、在主动板画千钧符并持笔指向镇石 | 三种击败顺序已测。第一、第二只倒下时仍被锁定；全清后用原笔反复牵引原石，入槽触发一次，随篮降到中层并走上西栈道。[第二种顺序](trio/alternate/trio.txt) · [实笔和载人第三种顺序](trio/free-pen/trio-free-pen.txt) · [冷启动](trio/cold/a-cold.txt) |
| 在修复态的祭坛北沿绕过旧栏端到甲岸，画通路符，归笔、举右手选岸、握拳 | 可取消并换选乙、丙岸。成路后能实际走上对岸，三处铜榫只负责选岸，玩家脚下的石台保有实体碰撞。[最终支路实走](trio/passage/checks.txt) · [原作者路径演示](../../../../../docs/visuals/20261126_ch02_passage/二章通路符_石案至乙岸.mp4) |
| 抬右掌，或用右手按左腕准望匣朱砂扣 | 原「天工金石」快览、腕匣状态页和真实图集。用 Meta 官方双手。[腕部交互](formal-wrist/wrist.txt) · [同镜头界面实拍](wrist/theme/README.md) |

②使用 `stone-carrier`、`canjiou`、`zhihuocanji` 各一处出生。仅②启用 `showEnemiesBeforeEngagement`，进入原触发盒才开始战斗。工奴继承生产角色的动作、负重配置和速度。R02 上背慢走与正式动画、姿势资源已核对。48 个相位对比上一版，平均髋低 16.1 厘米、肩低 19.5 厘米。[R02 复核与负重近照](trio/R02/README.md)③的工坊双屋顶与院场精英继续使用各自房间。[场景合并记录](trio/merge.json)

A 篮四片槽边的碰撞上沿与甲板齐平，原槽底、木栏和 B 篮保持原配置。当玩家站在镇石即将归座的位置时，A 篮等待玩家退让。玩家从槽旁实际退开 21 厘米后原石才入槽，吊篮出发一次。贴近石头和等待中读档分别做过回归。[槽边退让](trio/occupied-yield.txt)[槽边尺寸变化](trio/floor-clearance.json) · [贴近原石](trio/occupied-touch.txt) · [等待中读档](trio/occupied-restore.txt)

正式 SHA-256 为 `1be75566173fe09ec6efd8ff2bee7f00bcecc8aec5c652f3496469fbd8ef0353`。用户授权保存的现场分别留有原盘、编辑器工作副本和场景元数据。第二次现场保存明确保留了腕部 UI 的 `useKaogongTheme=1`。[入口修复前备份](二章入口修复前备份.zip) · [通路接入前备份](official-backups/passage-f24615f1d6bc.zip) · [祭坛和吊篮接入前备份](official-backups/祭坛和吊篮接入前.zip)

本表采用桌面 Unity Play、模拟追踪手势和生产玩法组件。角色击败顺序由测试驱动，实际近战追击、掷火和千钧牵引另有运行断言。Quest 实机视野、手势表现、帧率和乘篮舒适度待实测。较早的 `post`、`formal-wrist`、`passage` 记录分别对应入口 HUD、腕匣及自由符接入时的阶段快照。
