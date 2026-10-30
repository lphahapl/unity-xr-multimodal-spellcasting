# Link 手部追踪：关闭 Unity 手部特性的对照测试

日期：2026-10-28。用户选择先进行第2项对照；本次没有修改或启动教育对照工程。

## 实验配置

- A组：Standalone 的 Hand Tracking Subsystem、Meta Hand Tracking Aim 均开启。
- B组：只关闭上述两个 Standalone 特性，保持 Meta XR Feature、Meta XR Foveation、Oculus Touch Controller Profile 开启。
- Meta SDK 保持207.0.1，XR Hands包保持安装，手部模式保持ControllersAndHands；未修改Android平台特性。
- 修改前正常保存并退出Unity，修改后重新打开。用户确认戴好Quest并进入Link，放下控制器后进行采样。
- `SealTrackingDiagnostics` 每秒采集状态，持续30次；文件不包含关节姿态、设备序列号或账号资料。

## 结果

| 指标 | A组（已有诊断） | B组（本轮） |
| --- | ---: | ---: |
| 状态采样次数 | 30 | 30 |
| VR及输入焦点均正常 | 30 | 28 |
| GetHandTrackingEnabled返回true | 0 | 0 |
| Meta原生左手有效追踪 | 0 | 0 |
| Meta原生右手有效追踪 | 0 | 0 |
| 最后一次XRHandSubsystem数量 | 1 | 0 |

B组中XRHandSubsystem数量为0，说明本轮确实移除了该子系统；Meta原生手部接口仍未恢复数据。因此，本轮不支持“只需关闭这两项即可恢复追踪”的判断。此结果不能排除在其他运行时问题解决后，还存在被掩盖的兼容性问题。

A组使用本次修改前已保存的诊断，未在本轮重新采集；不能当成严格同步的交叉试验。若后续某项修改恢复追踪，应再恢复原条件复测，以确认因果关系。

SDK实现中GetHandTrackingEnabled返回false也可能表示原生调用失败，不能仅凭该布尔值认定头显设置关闭。应结合GetHandState返回值及追踪状态判断。

## 控制变量与还原

实验前后检查了manifest、packages-lock、OculusProjectConfig、OculusRuntimeSettings和SealLab场景的SHA256，均未变化。B组测试完成后已恢复两个特性；整个OpenXR配置文件与实验前备份的SHA256完全一致。

原始配置、两组状态和比较结果保存在：

`Logs/HandTrackingAB/20261028-203257/`

- `OpenXR.before.asset`：原配置。
- `A-existing-diagnostics.json`：修改前已有诊断。
- `B-disabled-diagnostics.json`：本轮B组诊断。
- `experiment.json`：范围及控制文件哈希。
- `result.json`：汇总结果及控制变量检查。

本轮由父代理通过现成的本地文件及窗口工具完成配置和真机状态检查，未调用外部子代理。
