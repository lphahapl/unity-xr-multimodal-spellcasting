# Quest 开发环境

工程：`D:/UnityProjects/NarutoVR`，Unity `6000.3.10f1`。

## 已安装并锁定的依赖

| 包 | 版本 |
| --- | --- |
| Meta XR Core SDK | 207.0.1 |
| Meta XR Interaction SDK Essentials | 207.0.1 |
| Meta XR Interaction SDK OVR Integration | 207.0.1 |
| Unity OpenXR | 1.15.1 |
| XR Hands | 1.7.2 |
| XR Plug-in Management | 4.5.1 |

父代理逐一核对了 manifest、packages-lock 和已解压 package.json，六项版本一致。
UPM 最终解析返回 200；首次 SDK 导入、程序集重载完成，未发现 C# 编译错误。
早期有一次 packages.unity.com ETIMEDOUT，随后解析与下载已完成。
不包含独立 Simulator、MR Utility Kit 或神经网络推理包。

先测试了 Meta SDK203.0.0，后回退83.0.4做兼容性对照，两者均复现同一共享故障。定位并修复ADB路径问题后恢复配套207.0.1。Core207.0.1的最低Unity声明为6000.0.66f2；当前6000.3.10f1满足版本要求。
来源：工程Logs中保存的Meta官方registry版本元数据、[Meta XR 插件配置](https://developers.meta.com/vr/documentation/unity/unity-xr-plugin/)。

## Android 工具链

Unity 自带 AndroidPlayer 中已有 SDK platforms android-34/35/36、adb、NDK 27.2.12479018、OpenJDK 17.0.9。
工具存在和版本已核对，尚不等于 APK 构建或真机验证通过。

## 配置与验证

`Assets/Editor/QuestEnvironmentSetup.cs` 提供两个菜单：

- `Tools > NarutoVR > Configure Quest Environment`：显式配置 Android ARM64、IL2CPP、最低 API 32、Vulkan、Linear，启用 Android/Standalone OpenXR、Meta XR、手部追踪、Aim 和 Touch profile，设置 ControllersAndHands 与 HIGH 快速运动模式。
- `Tools > NarutoVR > Validate Quest Environment`：检查已保存设置，并主动读取原生 OVRPlugin 版本。结果写入 `Logs/quest-environment-validation.json`。

`Logs/quest-setup.request` 是一次性配置请求标记；本次安装通过显式批处理命令完成配置并已清除标记，正常启动不覆盖后续自定义设置。
配置前备份在 `Logs/SetupBackup/`。

双击工程根目录 `Open-NarutoVR.cmd` 启动。脚本先核对ADB服务实际路径，再启动Unity；`Tools/Start-Unity.ps1 -Validate` 可在工程关闭后执行批处理校验，`-Configure` 可重新配置并校验。

原崩溃由vivo办公套件的 `D:/互联/pcsuite/adb_41/adb.exe` 服务触发：Meta原生工具转换非ASCII命令路径时抛出 `std::range_error("bad conversion")`。切换为Unity自带SDK的ADB服务后恢复。用户之后避免vivo调试与Unity同时运行；启动脚本仍保留检查。如需切换ADB，USB调试连接会短暂重连，不会退出vivo主程序。

脚本仅在缺失时为子进程补齐ALLUSERSPROFILE，不修改系统环境变量或用户隐私设置。`-ProjectPath` 可指定同版本的其他工程，仅启动，不运行NarutoVR配置方法。Unity安装位置在脚本中为 `D:/unity_hub/<version>/Editor/Unity.exe`。

## 验收边界

2026-10-28 实测：SDK207.0.1三项依赖与缓存版本一致，编译完成，配置校验通过，原生OVRPlugin为1.207.0。正常重启后NarutoVR成功进入Android平台的SampleScene编辑器窗口，DX12，响应正常；当前冷启动日志没有C#编译错误或原生崩溃。配置结果见 `Logs/quest-environment-validation.json`，交互启动验收记录见 `Logs/quest-gui-acceptance.json`。旧教育项目也已在原SDK85及恢复后的用户设置下成功进入编辑器，见 `Logs/education-correct-adb.log`。

诊断期间SDK203/83的批处理曾经通过，但交互启动失败，因此没有用批处理退出码替代实际窗口验收。最新安装在已打开的编辑器内导入，正常重启加载原生插件；早先 `Logs/quest-setup-current.log` 的退出码1来自工程已打开导致的重复启动，不是本次SDK安装失败。

经用户明确许可，曾将 `C:/Users/鲁迅/AppData/Roaming/MetaXR/device_data/tools_consent.txt` 备份为同目录的 `tools_consent.txt.backup-20261028-012440`，保留原始168字节内容，单独验证重建配置。该对照无效，原文件已恢复，备份保留，没有自动选择数据收集同意选项。跨工程证据和检索结果见 `docs/shared-editor-crash-research.md`。
后续已创建独立 `Assets/SealLab/Scenes/SealLab.unity`：玩家Rig、实际IHand骨骼线条、特征HUD、Editor特征/录制回放窗口与手动加权评分。默认SampleScene保留；十二印模板尚未校准。使用方式、软件测试和真机验证边界见 `docs/SEAL_LAB.md`。
Quest 真机启动、手部追踪数据与 APK 构建尚未验证。

## 协作

Antigravity 调用因认证失败而结束，未执行模型任务。用户授权切换后，DeepSeek Flash 对父代理提供的版本、工具链材料和未验证边界做了文本核对，父代理已验收；实际文件读取、安装与最终验证由父代理执行。
