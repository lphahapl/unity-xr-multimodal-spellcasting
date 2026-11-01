# 跨工程 Meta / Unity 启动崩溃检索

日期：2026-10-28。父代理检索并与后台研究代理交叉核对；不把社区帖子当作官方已确认修复。

## 已确认的本机根因与修复

原 ADB 服务来自 vivo 办公套件：`D:/互联/pcsuite/adb_41/adb.exe`。Meta 原生编辑器工具执行 `adb devices -l` 前，在命令路径的字符编码转换中抛出 `std::range_error("bad conversion")`。崩溃转储中的 `Json::operator` 是相邻导出符号，不足以指认 JSON 配置损坏。

父代理结合原生代理的只读取证，核对了异常类型、异常文本、实际 ADB 命令和转换位置。命令转换先于 `CreateProcessW`；不是整个系统环境变量块转换失败。随后仅停止旧 ADB 服务、改由 Unity 自带 SDK 的纯英文路径启动 ADB。旧教育工程在原 SDK85 和已恢复的用户配置下成功打开，见 `Logs/education-correct-adb.log`。NarutoVR恢复配套207.0.1后编译、配置校验及交互式冷启动均通过，原生插件1.207.0，验收见 `Logs/quest-gui-acceptance.json`。

没有证据将 Hub3.22.4 更新确定为根因。用户之后会避免 vivo 调试与 Unity 同时运行；`Tools/Use-UnityAdb.ps1` 还会检查服务端实际可执行路径并使用 Unity SDK ADB。没有卸载、迁移 vivo 软件或修改全局 PATH。

## 直接资料

1. [Meta 官方社区：Crash in Project with Meta XR SDKs](https://communityforums.atmeta.com/unity-development-123/crash-in-project-with-meta-xr-sdks-168052)：2025-05-13 已有安装 Meta SDK 后 Unity 6 导入启动崩溃的第一手报告，换编辑器和官方样例仍失败。出现 UnityPluginLoad / ovrp_GetVersion / Telemetry / UPSTContentManager。没有回复或官方修复结论，帖子未展示与本机完全一致的原生 JSON 堆栈。
2. [Unity Hub 官方发行说明](https://unity.com/unity-hub/release-notes)：3.22.4 发布于 2026-10-22；条目涉及项目操作、安装校验、Windows 更新、Electron 和 TLS 证书。未找到其与 OVRPlugin / preferenceUpdateFeatures 的直接关联记录。
3. [2026-04-01 的第一手复现记录](https://qiita.com/mio2024/items/4cbbcc1c00afb0dd996e)：包含与本机高度相符的 ovrp_GetVersion / GetMachineID / WithMachineIdUrlParameter / UPSTContentManager 托管调用链。作者称 SDK、工程、Link 重装无效，清理 Local / LocalLow / Roaming 中 Unity 用户目录后恢复；缓存根因是作者推测，且警告会丢失登录信息。不能据此确认具体损坏文件，也不照搬为删除全部 AppData 的建议。

## 本机证据

- NarutoVR 使用 Unity 6000.3.10f1；SDK 207、203 在交互启动时均于 OVRPlugin / JSON / DeveloperTelemetry::preferenceUpdateFeatures 处失败。DX11、DX12 都复现。
- SDK203 无图形和有图形的 batchmode 编译、配置检查与退出码 0 均通过；这不能代替交互式编辑器验收。
- 未修改的 mr-aiwo-education-science 工程使用同一 Unity 版本、Meta SDK85、OpenXR1.16.1，也出现同一原生崩溃链。
- 用户报告今晚 Hub 自动更新；本机 Hub 为 3.22.4。该时间线是线索，不能等同于因果证明。
- 用户授权的 Meta tools_consent.txt 单文件重建对照无效，原文件已恢复，备份保留。没有选择或修改用户的数据收集同意选项。
- NarutoVR SDK83.0.4 配套安装及 Android 批处理校验已通过，但随后交互式启动也在同一个原生位置失败。回退版本没有解决共享故障。

## 定位根因之前的推断

共享环境比单个新项目损坏更符合当时证据。用户状态损坏只是曾经的诊断假设，下面两项隔离均未支持它；最终根因见上文。
未找到官方公开确认的准确文件、补丁版本或一步修复，也没有证据证明卸载 Hub 或重装整个项目能解决。
旧项目 SDK 版本保持不变，没有大范围删除用户配置、许可证或凭据。

## 已授权并完成的可恢复隔离对照

一次只隔离一处，改名备份，启动旧项目检验；两项都复现同一崩溃，均已恢复：

1. `C:/Users/鲁迅/AppData/Roaming/Unity/Editor-5.x/Preferences`：编辑器布局、预设等共享设置。
2. `C:/Users/鲁迅/AppData/LocalLow/Unity/Editor`：共享编辑器用户状态与缓存。

备份分别为 `Preferences.codex-backup-20261028-014208` 和 `Editor.codex-backup-20261028-014327`；恢复状态记录在 `Logs/user-state-isolation-stage1.json` 和 `stage2.json`。Meta同意配置原文件也已恢复并核对哈希，备份保留。没有自动选择登录或隐私选项；保留工程、Local/Unity/cache、Local/Unity/licenses、Local/Unity/config及UnityHub账号目录。临时路径映射已移除。
