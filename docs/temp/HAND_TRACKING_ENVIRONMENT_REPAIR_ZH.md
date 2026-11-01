# Quest 手追踪环境诊断与原生对照

日期：2026-10-28。范围仅为 NarutoVR 和当前 Quest/Link 连接，没有访问教育工程。

## 已确认的环境

- Unity 6000.3.10f1；Meta Core / Interaction / Interaction OVR 均为 207.0.1；OpenXR 1.15.1；XR Hands 1.7.2。
- 编辑器与 Android 都使用 OpenXR Loader，启用了手追踪、Meta Hand Tracking Aim、Meta XR Feature。当前 Windows 活动运行时为 Meta。
- Android ARM64 / IL2CPP / Vulkan，最低 Android API 32；Unity 自带 SDK / NDK / JDK 已安装。
- Link PC 208.0.0.84.535。ADB 读取的系统界面组件为 74.0.0.771.346，串流客户端为 74.0.0.760.346；整机增量构建号为 50709660195100510，固件构建日期为 2025-03-12。这是版本差距的证据，还不是已确认的故障根因。
- 新的 Link 服务日志确认 Unity 进程获授 ExperimentalOpenXRExtensions。Unity 的运行时报告确认 XR_EXT_hand_tracking 和 XR_FB_hand_tracking_aim 可用且已启用。

## 已执行与结果

1. 复核前一次 30 秒诊断：30 次采样，28 次头显/VR/输入焦点同时有效，双手原生状态与 IHand 同时有效次数为 0。
2. 直接 Windows 服务重启因当前账户无法打开服务控制句柄而失败，没有提权绕过；改用 Link 自带“重启 Meta Horizon Link”。后台服务 PID 由 3976 变为 29208，确认实际重启。
3. 用户重新进入 Link 后，运行同一实验台采样 30 秒：30 次采样，28 次焦点有效，手追踪 enabled 为 0，双手原生有效为 0，双手 IHand 有效为 0。服务重启没有修复。
4. 原生首包在 Gradle 的 `configureCMakeDebug[arm64-v8a]` / Prefab 步骤失败。命令脚本对照：UTF-8 中文依赖路径 + LF 换行失败；同一命令改 CRLF 成功；保留 LF 而将依赖复制到英文路径也成功。不是 SDK 缺失，而是本机生成批处理与路径的兼容问题。
5. 复制公共依赖缓存到工程内 `.build-cache/gradle`，跳过锁文件且保留原缓存。构建辅助工具在 BuildPlayer 前使用 Unity 6.3 的 `AndroidExternalToolsSettings.Gradle.userHomePath` 临时指定此目录，并在 finally 恢复原偏好。实际 Gradle 命令出现 `-g D:/UnityProjects/NarutoVR/.build-cache/gradle`。
6. 期间重试遇到编辑器没有重编译已变更的脚本；停止重复排队，正常关闭后冷启动，确认编辑器程序集重新生成，才重新请求构建。未强制终止 Unity。
7. 2026-10-28 21:34 构建成功：0 错误、4 警告，APK 67,767,251 字节。APK 内确认包名、入口与 HAND_TRACKING 权限正确。ADB 检查独立包名未占用后安装成功并启动。
8. 原生自动记录 90 次状态；25 次 VR/输入焦点同时有效，这些采样均显示原生手追踪 enabled。双手原生状态和实际 IHand 同时有效 5 次，最长连续 5 秒。其余失去输入焦点或手不被追踪的采样不能作为稳定追踪时长。结论是基础手追踪链路已接通，尚未做复杂结印或长时间稳定性评估。
9. 用户随后明确反馈原生运行“正常”。原生成功与 Link 失败形成同一工程的对照；Link 的具体故障根因尚未确诊，版本差距是下一项验证方向。

基线与重启后状态文件位于 `Logs/LinkDiagnostics/EnvironmentFix/`。这些文件只有有效性/焦点等状态，不记录骨骼坐标或设备标识。

## 原生对照包

- 构建同一份已保存的 `Assets/SealLab/Scenes/SealLab.unity`。
- 输出 `Builds/Quest/NarutoVR-HandsProbe.apk`，独立包名 `com.narutovr.handprobe`，不覆盖设备上已有的默认模板包名应用。
- 构建时临时设置 Android 包名，并在 finally 恢复；不改 XR 功能、SDK 版本或保存用户场景。
- 仅这个独立包名的 Android Development Build 会自动采样 90 秒状态，保存到应用自己的 `files/hand-probe.json`；普通包及编辑器不启动该采样器。
- 判定应看原生 HandState 和实际 IHand 的有效性，不能把“打包成功”当作“真机手追踪成功”。
- 再次构建这个独立测试包使用 `Tools > NarutoVR > Build Quest Hand Probe`，该工具自动使用英文缓存。普通 Build 窗口不经过该辅助工具时，仍需核对 External Tools 的 Gradle User Home；启动器的进程环境变量不能保证覆盖已设置的 Unity 偏好。
- 构建后原 Android 包名已恢复。Unity 构建流程自动更新了 preloadedAssets 和 Android batching 的序列化条目，没有覆盖用户场景。

真机状态报告与汇总：`Logs/LinkDiagnostics/EnvironmentFix/native-hand-probe.json`、`native-hand-summary.json`。`native-hands-running.png` 是采样结束后、输入焦点无效时的画面，显示 N/A，不能用它替代已经记录到的有效采样。用户已另行确认戴上操作时显示正常。

## 尚未确定

Link 日志存在配置注册项缺失与部分串流错误，但不足以证明哪一条导致手追踪失败，未据此修改注册表。Link 显示 NVIDIA 驱动卡顿提示，目前也没有证据把它与无骨骼数据直接关联，未安装或回退驱动。

本轮没有重装 SDK、删除缓存、恢复出厂、更新固件或加入测试频道。原生测试结果将决定后续修复方向。

## 分工与验收

父代理完成诊断、Link 操作、构建方案、代码修正及真机验收。Antigravity / Gemini 3.8 Flash High 子任务 `68429515-d5f0-4930-88c6-0319abc4894c` 只返回构建辅助代码提案，没有读写工程或执行构建；父代理接受提案后修正了请求标记失败处理和独立包名恢复逻辑，再落盘执行。子代理没有声称完成编译或硬件验收。
