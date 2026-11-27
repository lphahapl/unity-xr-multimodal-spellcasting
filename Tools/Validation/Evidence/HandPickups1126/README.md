# 双手拾取与道具射线

玩家张手指向可拿目标时，细金线与末端圆环标出本次捏合会拿到的实际物件。指尖触到可用表面也可直接近拿。玩家脚本 `LinearRpgController.pickupRayReach` 提供远取上限，默认 3 米。当前工具带近抓仍为 9 厘米。所有权、入袋、剧情回调和消耗量由原组件提交。

原工具包提交 6e5c58c905ec0ed1931b31c30eb517ddec9e1572。其 `DistanceGrabWizard` 配置 Meta 距离抓取组件，`DistanceGrabInteractionController` 提供开关和组件引用。正式工程先前已有 Meta SDK 和 NeuGame 的吸附模块，实体笔的工具带入口仍只在右手指尖接近握点或袋口时取物。此次恢复原远抓运行时封装和 GUID，刚体物件通过原 `DistanceHandGrabInteractable` 暴露形状；无独立刚体的薄饼沿用可见握点。Meta 指向锥筛选候选，真实碰撞体表面决定近取距离和远取接触位置。新物件通过所属交互组件提交可拿状态。[上游 NeuGameMRTools](https://cnb.cool/NeuGameMR/NeuGameMRTools) · [原提交核查](../../../../docs/research/NEUGAMEMRTOOLS_REVIEW_ZH.md)

| 实物 | 正常可取与关闭条件 |
|---|---|
| 墨规笔 | 二章石案教学授权后，原笔套和地面使用同一支笔。旧 GlyphLab 的笔也使用持笔手书写。 |
| 首章铜叶 | 圆盘解开且章节允许收取或入匣时可拿，入匣完成后退出候选。两个固定圆盘仍通过原把手旋转。 |
| 工坊主榫 | 屋顶和庭院两场清理完成后可拿，正确入座后退出候选，原机关只结算一次。 |
| 干粮包与薄饼 | 章节奖励接口发放后可取袋。袋子完全打开，另一只空手可取仍可见的一块饼；嘴区按原规则结算。 |
| 松墨锭、冷铸拓片、定律铜橛 | 在相应样板和已配置授权的实例上可从袋口、世界内取回；使用正式物件自己的换槽、释放和插地逻辑。 |
| 老道具样板 | 已登记的旧干粮、墨规笔与拓片沿用原场景陈列。 |

技能投射物、千钧牵引物、吊篮手柄、双盘及固定布景均沿用各自的专属交互。


## 地面接触与回收

二章场景 SHA-256 为 `fd442e951f0c8b14bca022a3085cb0d772d42f7be2c617ba26b848a92d14fed6`，正式工具带 Prefab 为 `3a7677f59b4e18f8baada83af1b3282e91643a6940960003a3d1caa73d99dd53`。墨规笔由石案教学正常解锁。将笔拉离袋口，张手后让刚体在「近岸残段」或「门外崖肩」自然落稳，随后测同一支笔的站姿斜向远取、低视角远取、笔杆远段和中后段近抓、杆身中央近抓。五种情况均能取回、再次在原剧情板落墨，最后按笔尖和鞘口方向归鞘，再次从鞘中远取并归还。通路符持久性和拾取在同一控制器候选 `6d4cf74a` 上测试，合入时对正式通路版本 `3a4d1ee7` 使用局部补丁。正式墨规笔控制器与受测候选逐字节一致。[五次真实落稳拾取](play/pen-ground-fixed.txt) · [改用实体表面前的 3/5 对照](comparisons/pen-ground-before-surface.txt) · [XR 运行时读回](comparisons/pen-xr-readback.txt)

下列高度以对应测试的虚拟实体支撑面为零，单位为毫米。笔和主榫使用章节现场地板；其余注明样板基面。网格数值来自实际顶点，握点为道具原有的取放参照。

| 实物与姿态 | 网格最低点 | 实体碰撞底至顶 | 握点 | 验证 |
|---|---:|---:|---:|---|
| 墨规笔卧倒 | +0.14 | 约 0 至 +16 | +8 | 远端离握点 116.6 毫米、指尖离笔杆表面 7 毫米可近取。 |
| 工坊主榫侧倒 | 0 | 0 至 +120 | +60 | 离开展示承台，真实落至工坊地砖；右手指向并取回同一主榫。 |
| 松墨锭侧卧 | 0 | 0 至 +18 | +9 | 样板水平基面，指尖在顶面上方 12 毫米可近取，约 0.82 米可远取。 |
| 冷铸拓片平放 | 0 | 0 至 +2 | +1 | 同一基面，12 毫米近抓、约 0.82 米远取、加墙遮挡均通过。 |
| 干粮包侧卧 | +0.09 | 0 至 +44.80 | +22.40 | 二章奖励包，以水平校准面测量，可近抓和远取。 |
| 铜橛侧卧 | +0.50 | 0 至 +34 | +17 | 原铜橛样板校准面；竖直插地时碰撞顶 +220、握点 +170 毫米，可远取拔出。 |

包内薄饼没有独立实体碰撞。对实际可见顶点，在距表面 8 毫米近抓和约 0.44 米远取时均只取一块；松手归包，送嘴才结算。正式二章本次实测发放的是干粮包。铜橛实测使用其独立样板。

首章铜叶按剧情悬放，松手两秒后回黄铜架。叶架陈列时相对下方石几的可见底、碰撞底和握点分别为 +49.1、+49.1、+50 毫米；入腕后关闭实物碰撞并登记一次。另测发现存档节点 `opening.court` 的站立胶囊与「双盘指引_接回校规台」碰撞。入腕用例以已领取而未激活的进度进入原 LF02 监听，实际松手入槽、激活及防再取均通过；该检查点的跨房恢复仍有独立阻断。[入腕回归](play/leaf-wrist.txt) · [原检查点阻挡记录](comparisons/court-checkpoint.txt)

本轮通过 Unity 6000.3.10f1 桌面模拟手输入和真实刚体、碰撞与剧情回调进行验证。手势射线与系统指针分开 6 厘米、方向相差 12° 的对照也已验证：食指伸直时标识从指尖出发，未取得有效指轴时退回系统指针位置。此前 Quest 3 的 Steam Link 录屏为问题现场；本轮没有测量玩家房间内的现实地面和 XR tracking origin 的高度差。


## Play 原始记录

| 批次 | 通过断言 | 结束标志 |
|---|---:|---|
| [biscuit-geometry.txt](play/biscuit-geometry.txt) | 5 | PASS END |
| [chapterpen.txt](play/chapterpen.txt) | 17 | PASS END |
| [generic.txt](play/generic.txt) | 47 | PASS END |
| [ink-rubbing-surfaces.txt](play/ink-rubbing-surfaces.txt) | 10 | PASS END |
| [ink.txt](play/ink.txt) | 31 | PASS END |
| [leaf-pickup.txt](play/leaf-pickup.txt) | 11 | PASS END |
| [leaf-wrist.txt](play/leaf-wrist.txt) | 8 | PASS END |
| [pen-ground-fixed.txt](play/pen-ground-fixed.txt) | 18 | PASS END |
| [ration-ground.txt](play/ration-ground.txt) | 6 | PASS END |
| [ration.txt](play/ration.txt) | 12 | PASS END |
| [stake-ground.txt](play/stake-ground.txt) | 8 | PASS END |
| [stake.txt](play/stake.txt) | 16 | PASS END |
| [tenon-floor.txt](play/tenon-floor.txt) | 11 | PASS END |
| [tenon-socket.txt](play/tenon-socket.txt) | 14 | PASS END |

## 文件与重跑

下表文件均在 `Assets/scripts/logic/`。每份正式文件和隔离候选的 SHA-256 可在[发布校验](source-hashes.json)查阅。

| 文件 | 本次改动 |
|---|---|
| `Rpg/PlayerInputController.cs` | 每帧提取双手观测，统一选取后交回原交互所有者。 |
| `Rpg/WristHandChannels.cs` | 读取左右手对应的 Meta 和 OpenXR 射线与捏合通道。 |
| `Rpg/LinearRpgController.cs` | Inspector 远取距离及玩家禁用、销毁时的清理。 |
| `Interaction/Items/DistanceGrabInteractionController.cs` 和原 `.meta` | 从 NeuGameMRTools 恢复原公开开关、组件引用和 GUID，运行时在 Awake 应用初值。 |
| `Interaction/Items/HandPickupQuery.cs` 和 `.meta` | 共用双手近抓和远取选择，实体表面、遮挡、距离、标识及占用检查。 |
| `Interaction/Items/ToolBeltInteraction.cs` | 通用腰带物件、袋口和落地对象的同物拿取、归还与掉落。 |
| `Interaction/Items/TravelRationInteraction.cs` | 干粮包和展开后可见薄饼接入公共选择，沿用原进食与所有权。 |
| `Interaction/Items/TravelRationPouch.cs` | 按被指向的实际薄饼提取对应份数。 |
| `Interaction/Items/Sample/TravelRationDesktopLab.cs` | 保留干粮桌面模拟输入。 |
| `Interaction/Items/Sample/PineInkPrototype.cs` | 以持锭手与实际空闲手读取滑套动作。 |
| `Rpg/Glyphs/GlyphPenController.cs` | 双手取笔后由持笔手驱动笔尖和画板，逐处合入通路符归笔、效果生命周期和接触诊断。 |
| `Rpg/OpeningLeafPuzzle.cs` | 已解开、未归座的实物铜叶接受双手近抓和远取。 |
| `Rpg/WorkshopTenon/WorkshopTenonObjective.cs` | 清两组敌人后可取同一主榫，插座后收回候选。 |
| `Editor/Interaction/HandPickupPlayChecks.cs` 与 `.meta` | 双手、遮挡、距离、金线及归槽后的再次选取。 |
| `Editor/Interaction/RationPickupChecks.cs` 与 `.meta` | 持袋、选饼、误触及结算回归。 |
| `Editor/GlyphHandPickupChecks.cs` 与 `.meta` | 旧画符样板及二章实体笔、剧情板和双手书写的回归。 |


在独立测试副本使用 [runner](replay/run.py) 和 [XR 隔离预检](replay/xr_isolation.py)。五个参数为 `chapterpen ration ink stake generic`。启动器仅接受 D:/Temp 下的副本；使用 --project 传入副本路径，--output 指定输出目录。运行前由文件 ID 解析工程绑定的 Standalone 设置，禁用 XR 启动、清空该目标的 loader，并检查不存在显式启动硬件的运行时代码。每个子进程使用自己的无效 XR runtime 路径，进入 Play 前与运行中再次检查。Play 报告使用注入的手部观测，和用户串流视频分开留证。

## 直接输入的远抓

普通章节先统一采两手，工具带和独立持笔入口也支持直接传入右手观测。两种路径使用同一套 NeuGameMRTools 组件、可见表面选取、射线标记和权限检查。直接右手采样时，左手原有观测和高亮得以保留。

2026-11-27 修复前，直接右手路径在超出近抓范围时无法标出工具带道具或鞘中笔。修复后，在距握点约 0.32 米的桌面样板中，软袋、笔、拓片均可高亮、捏取并归还；独立画符场景拿到同一支笔后可实际落墨。普通 XR 输入此前已使用公共入口。尚无这次头显掉笔时玩法高亮与捏合的同帧数据。[工具带回放](direct-input-1127/belt.txt) · [独立符笔回放](direct-input-1127/pen.txt) · [正式场景持有者清单](direct-input-1127/authored.json)
