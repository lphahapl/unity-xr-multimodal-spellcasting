# ②祭坛 · R02 背石与三敌验收

2026-11-26，Unity 6000.3.10f1，桌面 Play。**R02 通过本次祭坛三敌与同石入 A 篮验收。** 工奴、普通近战和掷火残机都由正式房间生成。工奴死亡时松开背上的 A4 镇石。玩家需要同时满足三敌已清场、人在 A 篮两个条件，才能用千钧符牵引原石。

## 本次使用的版本

| 输入 | 已核对的值 |
| --- | --- |
| 正式场景与敌人组独立副本 | `Chapter02_SuspendedPath.unity`，SHA-256 `c56d7380bef12e6104f59a28f1926009e74b425db6c9c62621ee414c142022d2` |
| 生产背负动作 | `CanJiOu_BackCarryWalk.fbx`，SHA-256 `bc9b990a9546daadaa00189dc6ca265c61aa7dc1dcb18b8f91db393a4035671c` |
| A4 镇石持姿 | SHA-256 `e9cc0679a55a4540d30b1f8b713c28f5373bdda286d0166c34f1e1be2e9899f4` |
| 子剪辑 | Animator 的 `StoneCarry` 和工奴配置的持姿共用 GUID `fd6ab94ab06e1b042ae628900e686368`、本地文件 ID `-203655887218126122`。发布组在租约生成的工奴上读到实际播放的 `CanJiOu_BackCarryWalk`，一个周期 1.6 秒。 |

编辑器进入 Play 前和最后一局退出后，主工程、副本与发布清单的 10 项资源全部一致。两份完整输入快照 SHA-256 相同。场景含官方左右手，三局均记录原场景未加补丁。[进入 Play 前](before-play-r02.json) · [结束后](after-play-r02.json) · [发布清单](../../Chapter02Assembly/back-carry-walk/R02/runtime-sha256.json)

## 敌人组本轮实跑

三局各使用独立新档。探针推进断桥画成回调并移动现有玩家 Rig。两局击杀测试走正式受击入口；第三局由敌人 AI 自主行动。

| 用例 | 现场结果 |
| --- | --- |
| [工奴 → 近战 → 掷火](Encounter/carrier-first/assertions.txt) | 原石 / 刚体 `-6632 / -6628`。第一亡掉石；仍有两敌或一敌时，提前登篮均不能牵引。第三亡只清场一次。 |
| [掷火 → 近战 → 工奴](Encounter/carrier-last/assertions.txt) | 原石 / 刚体 `-6234 / -6230`。前两亡仍由工奴背石，第三亡才释放同一刚体并触发唯一清场。 |
| [三敌同时行动](Encounter/live/assertions.txt) | 固定玩家 10.262 秒后倒地。工奴持石巡逻，距起点最远 5.479 米；近战 3 次命中，掷火残机生成 2 个独立火核、命中 2 次。原玩家、头部及两只官方手的引用持续一致。 |

两种击杀顺序都从接敌线外 2 米开始取样，预展示距离配置为 32 米。线外三只可见，攻击尚未开放；过线后，同三只敌人变为可受击。零亡和一亡时退出后重入，各局均累计创建三只、从池再次取出零只，一亡后的近战保留 49 HP。三局进程退出码均为 0。各用例目录内有场景准备记录、断言、轨迹和退出码。

## 同石入篮与下降

采用动画发布组这一版的完整桌面 Play 及其原始 CSV。该局工奴先死，另外两只随后由同一房间受击结算。掉落的 A4 实例 `-6260`、刚体 `-6256` 从工奴背部一路保持到原篮槽。掉石首个物理步位移 3.9 毫米。清场后玩家登篮，两次千钧符牵引完成入槽，`onDocked` 触发一次。镇石和 8 个渲染网格由 A 篮运至中层，下降约 80 米。玩家步行离开后，镇石仍留在原槽。Unity 实例 ID 的比较均限于产生它的同一局。[运输断言](../../Chapter02Assembly/back-carry-walk/R02/a-ride.txt) · [运输轨迹](../../Chapter02Assembly/back-carry-walk/R02/a-ride-route.csv) · [R02 工奴侧面](../../Chapter02Assembly/back-carry-walk/R02/side.png) · [R02 入槽实拍](../../Chapter02Assembly/back-carry-walk/R02/stone-docked.png)

运输 CSV 的 51 条牵引采样都记录了同一对石 / 刚体 ID。740 条乘梯采样中，原槽的首末采样高度分别为 80.2401 米与 0.2451 米。归档的运输断言与 CSV 均已对照发布组独立副本的原始输出逐字节核对。

本次覆盖桌面模拟入场、正式受击、AI 与运输。头显中实际画符、观察步态和乘梯舒适度仍待真机体验。隔离副本退出时找不到语音服务的停止脚本，语音输入未纳入本轮。

## 复核入口

在仓库根目录运行 `python -X utf8 Tools/Validation/Evidence/Chapter02AltarTrio/R02Unified/verify_inputs.py <新的输出文件> --published-r02`，可重验生产与独立副本的哈希、正式场景、官方手、动作及持姿引用。[三敌 Play 入口](../Encounter/run_checks.ps1)在专用副本中串行运行三局。[预备记录](preparation-c56d.json)保存 R02 发布前的输入。
