# 四向交互与投掷验收 · 2026-10-30

本轮把左食指四向选点加上路径/落点/驻留进度，右掌四向闪避独立出来，并增加结印保护。用户随后选择“握拳或捏住后甩手松开发射”，RPG火球已切换到腕骨局部持握＋真实手轨迹投掷。

操作与参数：[四向交互与骨骼投掷](DIRECTIONAL_INTERACTION_AND_THROW_ZH.md)。

- 备份：`D:\UnityProjects\NarutoVR_Backups\before-direction-feedback-20261030-212723`。包含修改前SealCombat资产/代码、文档及语音工具入口。
- Unity编辑模式220组检查通过，其中新增15组四向冲突、15组纯投掷、1组官方握拳到真实输入适配检查。
- Unity真实Play：37组效果检查＋25组RPG集成检查通过，总计282组Unity检查。原点/手形/静止松手/握持上甩方向、路径/进度显示、音频调用、MP/CD、取消、伤害和检查点均在对应实际路径检查。
- Python语音配置16项检查通过。Steam Streaming Microphone已启用；本机服务就绪，实际收到“火球术”口令。没有保存本次麦克风原始音频。
- 38份非RPG技能配置和旧场景与备份逐文件哈希一致；`Assets/SealLab`、Packages、ProjectSettings未产生本轮修改。
- 内置执行代理新增15组FSM行为回归；父代理阅读、集成并实际运行通过。second/Sol High文本审查在推理前失败，无新完成回执，未把它计作已完成审查。

## 自动测试过程中的问题

最初Play中的合成骨骼每秒只有约6次观测，最大间隔0.25～0.29秒，超过真实追踪保护阈值。固定合成手坐标、改为游戏Update驱动后仍存在限帧。临时停止当前Play的XR子系统后，真实游戏生命周期检查全部通过。测试结束恢复子系统，不改持久XR配置；不能因此宣称真实串流性能通过。

另外修正了测试驱动放在Editor程序集不能挂载的问题；官方Closed→Neutral→Open需要逐步确认，测试也按该机制等待，未改SDK阈值。

## 证据

- [编辑模式结果](../Logs/direction-feedback/editor-suite.json)
- [播放模式结果](../Logs/direction-feedback/play-check.json)
- [旧配置完整性](../Logs/direction-feedback/source-integrity.json)
- [路径与进度截图](../Logs/direction-feedback/path-preview.png)：验收相机从外部看地面，包含世界面板背面，不代表头显第一视角构图。
- 控制台退出Play时错误0，警告2；本套效果检查包含两次故障注入警告。可见日志与计数已归档后清理，最终状态另存 `Logs/direction-feedback/final-state.json`。

## 待真机确认

戴好头显进入Steam Link，先各方向独立测试10次导航和闪避，再在子/丑/寅印之间切换，观察保护是否误阻断或漏拦截。之后练习握拳、捏合两种投掷，比较实际出手方向与飞行方向。测试点包括实际提示音可闻、手部高速运动的丢追踪、松拳延迟、腕骨偏移和使用舒适性。

没有测定真机准确率、P95延迟或帧率，也未构建新APK。不要将自动测试数量当作设备识别成功率。
