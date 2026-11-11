# 验证入口与证据边界

文档整理时间：2026-11-01T21:00:59+08:00。本次仅整理文档，没有重跑 Unity 或头显测试。

## 现有检查怎么运行

- Unity：`Tools → NarutoVR → 线性RPG → 运行完整编辑模式回归`，以及 `运行独立存档播放检查`。
- 后台请求：先读 `Tools/Request-RpgCheck.ps1` 当前参数；例如 `./Tools/Request-RpgCheck.ps1 full-editor-check`。编辑器需处于可处理请求的状态。
- 配置：`Tools → NarutoVR → 技能配置 → 验证当前场景 / 运行架构检查`；修改资产后核对场景实际引用。
- 语音契约：`Tools/SkillVoice/.venv/Scripts/python.exe -X utf8 -m unittest discover -s Tools/SkillVoice -p test_configuration.py -v`；真实模型合成音回归使用 `Tools/SkillVoice/verify_fixtures.py`，不代表真人麦克风准确率。

## 整理时读取的报告快照

| 范围 | 报告时间（UTC） | 结果 | 边界 |
|---|---|---|---|
| 编辑模式 | 2026-11-01T12:24:48.5959713Z | passed=true，531 项 | hardwareTested=false |
| Play Mode | 2026-11-01T12:25:46.0724498Z | passed=true，集成 35 项，效果 37 项 | hardwareTested=false |

来源为本地 `Logs/official-hand-poses/editor-suite.json` 与 `Logs/linear-rpg/play-check.json`。这是既有报告快照，没有与后续工作区绑定为新的通过证明；仓库不分发这些日志。后续验收必须记录代码提交或文件哈希、场景/资产、运行环境、命令和新报告时间。

Play 检查包含真实 Unity 生命周期、Physics 与明确注入的合成骨骼/调试命令，使用独立 probe 存档；部分效果通过反射或模拟发射验证。曾临时停 XR 子系统以隔离未佩戴限帧，结束恢复，不代表在实际串流帧率下完成。验收还需检查退出 Play 后控制台与清理状态。

## 必须单列的真机验收

当前完整动作的 Quest 手感、追踪遮挡恢复、自然动作误触发、麦克风误报漏报、滑退舒适度及长期性能仍需专项记录。十二个手印资产不代表十二印完成标定；PICO、MR、多人和 Android 语音也不能由桌面回归推定完成。

本轮文档链接检查与整理清单记于[需求变更交接](../模型交火/需求变更交接.md)。2026-10-29/30 的首次交付与旧四向验收摘要见[代码审阅交接](../模型交火/代码审阅交接.md)，不继续用历史数量充当最新结果。

## 2026-11-10 全模块桌面验收

在 Windows 上用 Unity 6000.3.10f1 与临时目录下的独立项目运行。下面三个入口分别测试首章、P1 和训练场，输出 JSON 和 Editor 日志到副本的 Logs 目录。首章读取 12 个交付场景，再以各阶段的 v2 检查点模拟读档。在 LF02 注入合成手势，经正式三点吸附和物品模块验证一次激活与写档。P1 同时测试真实房间和可控的延迟加载。训练场在进入 Play 前设置唯一存档，退出后检查验收副本与编辑器起始场景。

```powershell
python Tools/Validation/run_skill_assembly.py --unity "D:/unity_hub/6000.3.10f1/Editor/Unity.exe" --clone "$env:TEMP/NarutoVR-MvcValidation" --baseline-ref b0afcda --phases mvc-opening mvc-p1 mvc-training
```

每场记录预热后 90 个无图形 Editor 帧的中位数、P95 与当时的已保留内存。该采样用于比较本机桌面运行。头显的渲染帧时、切场峰值、首次施法帧耗时、手势和空间听感需在 Quest/PICO 另行测量。

2026-11-10 的三场景基线、最终采样及专项报告位置已收在[本机验收汇总](../../Logs/mvc-integration-20261110/acceptance-summary.json)。Windows 的实际资源包另有[逐 ID 读回记录](../../Logs/mvc-integration-20261110/packed-game-assets.txt)，包含每个技能使用的效果、加载器类型及包的 SHA-256。日志及构建产物位于 Git 忽略目录。

用同一个命令将 `--phases` 后的参数换为 `checkpoint-recovery progression`，可以重跑存档恢复与成长。前者从三个同步通知内尝试保存和恢复，检查主档和备份的实际写入次数，并覆盖移动监听者抛错、写盘过程中到达新快照，以及未知版本存档下临时角色的首次死亡与复活。该批次分别通过 21 项和 23 项。[独立存档报告](../../Logs/mvc-player-20261110/checkpoint-green/checkpoint-recovery.txt)

### 残机偶受击、死亡和动画

在同一独立工程使用 `--phases enemy-hit enemy-animation mvc-p1 fight-entry`。四个入口分别覆盖首章真实火球的声画与伤害、首章及 P1 的骨骼动作、P1 双敌房间的动画清理与租约复用，以及首章击败后的旁白和奖励。动画检查会在同帧暂停、恢复和连续追转时记录 Animator 状态、骨骼姿势与导航位移。死亡检查还包含独立怪销毁、旧预置房间重置、慢放、屏幕外播放和外部禁用。头显触觉和耳机中的空间听感需单独核验。
