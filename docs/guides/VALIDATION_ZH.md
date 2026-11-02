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
