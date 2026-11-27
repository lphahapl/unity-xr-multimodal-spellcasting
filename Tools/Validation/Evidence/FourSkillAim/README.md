# 四术准心桌面 Play 记录

2026-11-26，Windows，Unity 6000.3.10f1，URP，D3D11，单目眼位相机 960 × 600。隔离工程使用项目中的技能定义、预制体和玩家施法执行器。手部与头部使用带时间戳的模拟姿态，口令通过游戏的语音接收入口注入。全部 50 条检查通过。[原始输出](checks.txt)

| 技能和场景 | 同一帧只切换准心渲染器所得差异像素 | 原图 |
|---|---:|---|
| 火球术抬腕对靶 | 38 | [持球与预测线](fireball.png) |
| 雷光剑对靶 | 44 | [剑波预览](thunder.sword.png) |
| 龙火之术沿印顶方向 | 78 / 72 | [空场](breath.stream-clear.png) · [撞墙](breath.stream.png) |
| 飞炎术发射 | 34 / 32 | [空场](breath.burst-clear.png) · [撞墙](breath.burst.png) |

## 验证内容

- 用四份正式技能定义构建玩家运行时目录，逐一核对每个玩家副本的瞄准引用。玩家飞炎引用玩家版效果，掷火残机火核保持对另一份共享飞行效果的引用。
- 将测试执行器的旧视线距离设为 2 米，火球和剑波瞄准配置副本设为 9 米。两术均瞄准 5 米处的靶。修改各自开关、丢失和恢复持术手、收招时，准心按对应状态变化。另用左腕火球和实际横挥剑波的发射方向与靶位进行核对，误差均小于 1°。
- 龙火在认印并收到口令后出现预瞄，等待贴嘴期间松开双手即收起。喷射时调整一侧主导指节，火流和准心一起转向。单侧缺失可用印顶时准心熄灭。
- 飞炎从嘴部沿已确定的发射方向飞行。出手后转头，标记仍指向原弹道，达到配置的提示时间即收起。两种吐息的空场、实体遮挡位置均与攻击扫掠一致。另跑原有投掷预览和两种口部施法回归。
- 存图取自运行中的眼位相机。对照帧复用同一相机与世界状态，关闭对应的终点标记，按落点周围 109 × 109 像素内 RGB 绝对差之和大于 32 计数。方向线和武器保留在两帧中。
- Quest 上待核对双目融合、远端清晰度、实手遮挡和正式关卡背景上的对比度。

## 重放

在同版本工程副本，把 [FourSkillAimPlay.cs](../../FourSkillAimPlay.cs) 复制到 `Assets/scripts/logic/Editor/`。关闭该副本的编辑器，然后从 PowerShell 执行以下命令。将两个路径设为本机的 Unity 编辑器和工程副本。

```powershell
$editor = 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe'
$project = 'D:/Temp/NarutoVR-ParryQA-20261124'
& $editor -batchmode -force-d3d11 -projectPath $project -executeMethod NarutoVR.SealCombat.Editor.FourSkillAimPlay.Run -logFile "$project/Logs/four-aim.log"
```

测试进入空场景 Play 并自行退出。输出在副本的 `Logs/FourSkillAim/`；以 `checks.txt` 末行 `FOUR_AIM_PLAY_PASS` 和进程退出码 0 为通过条件。
