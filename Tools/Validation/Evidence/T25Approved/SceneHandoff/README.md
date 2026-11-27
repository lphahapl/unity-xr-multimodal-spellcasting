# 第二章祭坛 · A4 镇石场景交接

本包供《断衡悬道》场景负责人接入工奴房间。模型、持姿、工奴与千钧符均由已有运行时代码提供。此处交付一份可撤销的手动安装器，以及只在专属隔离副本运行的验收器。

## 正式场景负责人操作

1. 在自己的 Unity 工程中将本目录的 `T25AltarRoomInstaller.cs` 放入 `Assets/Editor`，待编译结束，打开 `Assets/Scene/Chapter02_SuspendedPath.unity`。场景改动由一位负责人保存。
2. 在编辑模式运行菜单 **Tools → NarutoVR → 第二章断衡悬道 → 接入已批准的祭坛工奴房间**。核对 Hierarchy 与 Inspector，确认后保存场景。安装器会将整次变更放入一个 Undo 组。再次运行应提示“无需改动”。
3. 检查 `02 · 祭坛战场  +80 m/首次接敌线`。正式快照中该点为空，补丁在其下创建 `祭坛工奴 · 接敌区域`，挂一个触发盒及 `CombatRoomEncounter`。已有合适的房间时复用原实例，补全空缺的引用。发现多个候选、房间停用、出生点另有用途或范围与约定冲突时会报错，保留场景供负责人核对。
4. 检查房间绑定。玩家和敌人服务均来自 `二章 · 玩家与工坊校准`，出生列表中 `stone-carrier` 引用坛下原 `背石起始位`，`isSummoned` 关闭。旧 `背石工奴 · 机体占位` 及其子 `背载镇石 · 含铜箍` 应随占位根一起停用。新石由工奴的 `StoneCarrierProfile` 创建并交给同一个 A 篮。
5. 进入 Play 测试。玩家脚底跨过接敌线后应出现一个背石工奴。普通命中触发撤离，击败后同一块石落地。清场后登上 A 篮，从真机使用千钧符将镇石牵入原篮槽，观察是否记一次锁定。乘员盒和真实手势操作都需要真机核实体验。

## 现场引用及范围

- 玩家使用正式 `LinearRpgController.locomotion` 的 `head`、`rigRoot` 和 `PlayerFloorPosition`。脚底点由 `CombatRoomEncounter.Update` 读取。玩家对象已开启 `roomControlsEncounter`，不需要添加第二份玩家、相机或管理器。
- 新触发盒相对接敌线中心为 `(9.5, 1, 0)`，尺寸为 `(19, 3, 22) m`。对应原祭坛局部地面 X 范围为 9 至 28 米，Z 范围为 0 至 22 米。范围从线后延伸至整片接战地面，容得下玩家进场和工奴退向落石点。
- `背石起始位` 上已有 `StoneCarrierPost` 并绑定原篮。它的两个巡逻点按序为 `示例落石`、`背石起始位`。A 篮下的 `A 配重槽 · 净口 1.25/示例槽心/镇石锁定中心` 是锁定锚点。乘员盒在 `A · 镇石配重吊篮` 根上，局部中心 `(1.6, 1.25, 0)`、尺寸 `(3.2, 2.5, 3) m`。
- 工坊的 `WorkshopTenonObjective.roof` 与 `.courtyard` 分别引用屋顶与院场的既有遭遇，供工坊目标启停。这两个引用保留。祭坛房间由自身范围响应在场玩家。正式章节对祭坛物资运输另有剧情门槛时，场景负责人需单独接入相应目标事件。
- 本轮验收截止于 A 篮锁定。原 `BallastBasket.onDocked` 没有持久监听；需要吊篮升降或章节推进时，应由相应系统接收这一事件。测试中的临时监听只计数。

## 复现实测

时序、输入边界及锁槽测量见 [运输验收记录](结果.md)。同次实测的原状、重开后的接线、运行断言、轨迹和输入指纹位于 [运行证据](运行证据)。本机还保留 `Logs/CH02-T25-A4-production/transit/final-delivery/play.log` 完整控制台。

在专用副本 Unity 已退出时，从工程根在 PowerShell 中运行下列命令。选择新的报告目录保存每次证据，`T25ApprovedTransit.Run` 会校验副本路径，只在该副本写入改过的场景。QA 为本轮创建唯一存档名。

```powershell
$qa = 'D:/Temp/NarutoVR-CarrierQA-20261125'
$report = Join-Path (Get-Location) ('Logs/CH02-T25-A4-production/transit/run-' + (Get-Date -Format yyyyMMdd-HHmmss))
python Tools/Validation/Evidence/T25Approved/SceneHandoff/prepare_snapshot.py $qa $report
if ($LASTEXITCODE -ne 0) { throw '生产与副本输入核对失败' }
$env:T25_TRANSIT_REPORT = $report
$unity = 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe'
$arguments = @('-batchmode', '-nographics', '-projectPath', $qa, '-executeMethod', 'T25ApprovedTransit.Run', '-logFile', (Join-Path $report 'play.log'), '-job-worker-count', '4')
$run = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($run.ExitCode -ne 0) { throw "Unity 验收失败，退出码 $($run.ExitCode)" }
```
