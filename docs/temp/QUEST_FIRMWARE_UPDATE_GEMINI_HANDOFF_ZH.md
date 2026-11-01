# Gemini 接手提示词：Quest 3 官方固件升级与手追踪复验

请接手这台 Quest 3 的系统固件升级，并在升级后复验 Quest Link 手部追踪。请先核实现场状态，然后实际推进，不要只给教程。本文是截至 2026-10-28 的交接记录；设备连接、网页、进程和版本均可能变化，需要重新读取。

## 1. 用户目标与授权

- 用户此前明确要求“帮我 adb 更新”，现在说“已经唤醒”，并让我写此文供你协助升级。
- 用户已唤醒头显，USB 保持连接；先用 ADB 确认连接与电量。
- 目标是通过官方渠道升级 Quest 系统，优先正常系统更新或官方 USB 更新工具；如能取得适用的官方签名 OTA ZIP，可用 ADB sideload。
- 保留设备数据。不要恢复出厂、解锁 bootloader、root、刷 userdebug、加入 PTC 测试频道或使用其他型号固件。
- 只处理 NarutoVR 和此 Quest。不要修改 `D:\UnityProjects\mr-aiwo-education-science`。
- Codex 已暂停升级操作，避免双方同时控制设备。请同样避免其他工具同时操作同一头显或浏览器。

## 2. 设备实测信息

最近一次 ADB 读取：

| 项目 | 结果 |
|---|---|
| 型号 | Quest 3 |
| ro.product.device / name | eureka / eureka |
| ro.build.version.incremental | 50709660195100510 |
| ro.build.date | Wed Mar 12 17:01:49 PDT 2025 |
| 当前 slot | _b |
| Android / API | Android 12 / API 32（前次读取） |
| com.oculus.systemux | 74.0.0.771.346 |
| com.oculus.xrstreamingclient | 74.0.0.760.346 |
| com.oculus.updater | 74.0.0.1228.447 |
| 电量 | 73%，USB 供电、充电中（升级前请重读） |
| /data 剩余空间 | 约 431 GB |
| sys.boot_completed | 1 |

原始基线文件：`D:\UnityProjects\NarutoVR\Logs\FirmwareUpdate\before-update.json`。

**目前没有下载固件 ZIP、没有执行 sideload，也没有进入 bootloader / recovery。** 请重新检查，防止用户在交接期间自行更新。

## 3. 使用这份 ADB，不要使用 vivo 调试工具附带的 ADB

PowerShell：

```powershell
$questAdb = 'D:\unity_hub\6000.3.10f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
& $questAdb get-state
& $questAdb shell getprop ro.product.model
& $questAdb shell getprop ro.product.device
& $questAdb shell getprop ro.build.version.incremental
& $questAdb shell getprop ro.build.date
& $questAdb shell getprop sys.boot_completed
& $questAdb shell dumpsys battery
& $questAdb shell df -h /data
& $questAdb shell dumpsys package com.oculus.systemux | Select-String 'versionName='
& $questAdb shell dumpsys package com.oculus.xrstreamingclient | Select-String 'versionName='
```

不要同时开启 vivo 调试；不要无故替换 ADB 驱动。若有多个 Android 设备，用实际发现的设备标识限定目标，不要把序列号写进公开报告。

## 4. 官方升级入口与已经做过的尝试

官方工具：

https://www.meta.com/help/quest/software_update/?view=prepare

上次该页面已成功加载，标题 **Meta Quest Software Update**，显示 Quest 2 / Quest 3S / Quest 3 / Quest Pro 的选择按钮和 Get started 按钮。尚未选择设备或启动更新。请选择 **Quest 3**。

页面当时说明：电脑至少需要 2 GB 空间、准备 USB-C 数据线；通过该工具更新不会影响应用进度及设置。以你操作时的官方最新说明为准。

此前 Chrome 远程调试连接不可用；Codex 改用内置浏览器后成功打开此页。这个浏览器会话不一定能被你接管，也未验证它是否支持 WebUSB。你可以在自己可控、支持 WebUSB 的 Chrome / Edge 中打开官方入口，实际验证能力，不要沿用 Codex 内部 tab ID。

如使用浏览器自动化，按本机协作规则先检查用户已开启的 Chrome 远程调试端点；不可用时说明实际限制并使用可行的官方方式。需要用户选择 USB 设备、操作物理按键或处理账户验证时，再给出明确的一步指引。

ADB 已执行过：

```powershell
& $questAdb shell am start -a android.settings.SYSTEM_UPDATE_SETTINGS
```

命令成功启动到 `com.oculus.vrshell/.intents.AndroidIntentsRelayActivity`。但当时头显可能休眠，截图返回退出码 1 / 空文件，**没有获得有效更新页面截图，也没有点击更新**。用户现已唤醒，可以重试读取屏幕状态。不要把空图解释为更新失败或设备损坏。

本地 `Logs\FirmwareUpdate\native-update-settings.png` 是 0 字节，不是有效截图。

## 5. 升级执行要求

1. 先重新读设备信息、电量和更新状态，记录基线。优先检查系统本身是否提供稳定版更新；若可直接更新，通常无需手动寻找 ZIP。
2. 如走官方 USB 更新工具，按官网当前流程准备，并验证实际浏览器 USB 支持。准备好工具后再让设备进入对应更新模式，避免过早重启导致 ADB 丢失。
3. 如必须 ADB sideload，先取得来源可验证、型号适用的官方签名 OTA。确认它是可用于当前设备/版本的完整包或符合前置条件的增量包，再考虑安装。记录 URL、包大小和 SHA-256；若官方没有提供哈希，不要声称已有官方哈希认证。
4. **不要凭搜索结果里最大的版本号选包。** 曾发现第三方归档含 v2.9 / 209 的 PTC 包；那不能证明它是这台设备应安装的稳定版。当前尚未取得官方 OTA ZIP。
5. 不要下载运行第三方“一键刷机”二进制。不要使用 Quest Pro / Quest 3S / Quest 2 包。OTA ZIP 也不是 APK，`adb install` 不能升级系统固件。
6. 未确认命令适配前，不要盲用网上的 `adb reboot sideload-auto-reboot`。此前没有执行或验证这条命令。若需进入 USB Update Menu，请核对官方流程；菜单中选择 Sideload Update，绝不能选择 Factory Reset。
7. 如采用 `adb sideload <已验证的包>`，保留安装输出并等待头显完成重启；不要仅凭电脑端进度条判断成功。
8. 故障时保留日志，不反复刷不同包碰运气，也不直接恢复出厂。说明准确阻塞点，继续完成不受影响的检查。

升级证据、下载文件和报告可放在工程 `Logs\FirmwareUpdate\` 或新建的 `Downloads\Firmware\` 中。不要删除项目、缓存或已有数据来“重装环境”。不要求修改 Unity SDK 或工程渲染配置。

## 6. 为什么要升级：已经确认的诊断边界

工程路径：`D:\UnityProjects\NarutoVR`。

- Unity：6000.3.10f1。
- Meta XR Core / Interaction / OVR：207.0.1。
- Unity OpenXR：1.15.1；XR Hands：1.7.2。
- PC Meta Horizon Link：最近记录为 208.0.0.84.535，请重查当前值。
- Windows 活动 Meta OpenXR runtime：`D:\Meta Horizon\Support\oculus-runtime\oculus_openxr_64.json`。
- Developer Runtime Features 已打开；Unknown Sources 已打开。
- Quest 原生主页可以用手；同一 SealLab 的 Android 原生测试包也能显示有效手追踪，用户已确认“正常”。
- Link 中 Unity 可以显示文字、进入 VR，但采样时原生手状态和实际 IHand 无效。一次 Link 服务正常重启、一次禁用部分 Unity 手部扩展的 A/B 对照都没有恢复，配置已恢复。
- OpenXR 手追踪扩展可用/启用，并不等于该时刻运行时实际传入有效手部数据。
- Link 桌面环境没有显示手模型，本身不足以证明应用手部数据坏了；验收应看应用内真实 IHand / 原生状态。

**目前没有找到 Meta 官方关于“头显 v74 + PC Link 208 一定不兼容”的精确版本矩阵。升级是缩小版本差距、排除旧系统因素，不能预先保证解决。不要把猜测写成已确认根因。**

官方参考（需要你重新核实当前内容）：

- Link 软件要求：https://www.meta.com/help/quest/140991407990979/
- Unity Link 开发说明：https://developers.meta.com/vr/documentation/unity/unity-link/

工程已有详细记录：

- `docs\HAND_TRACKING_ENVIRONMENT_REPAIR_ZH.md`
- `docs\LINK_HAND_TRACKING_CASES_SOURCE_NOTES.md`
- `docs\HAND_TRACKING_AB_TEST_ZH.md`

## 7. 升级后的验收

### A. 系统升级是否真实完成

重新读取 build incremental、build date、systemux 和 xrstreamingclient 版本，与基线比较；保存到 `Logs\FirmwareUpdate\after-update.json`。如系统要求分阶段更新，可按官方正常流程继续，不用把没有升到搜索结果最高版本当作失败。

### B. 先复验原生手追踪

现有测试 APK：

`D:\UnityProjects\NarutoVR\Builds\Quest\NarutoVR-HandsProbe.apk`

包名：`com.narutovr.handprobe`

Activity：`com.unity3d.player.UnityPlayerGameActivity`

已安装过，通常只需启动：

```powershell
& $questAdb shell am start -n com.narutovr.handprobe/com.unity3d.player.UnityPlayerGameActivity
```

让用户退出 Link，在原生应用里伸出双手。此包仅记录有限时长状态诊断，启动后约 90 秒、每秒一次，报告位于：

`/sdcard/Android/data/com.narutovr.handprobe/files/hand-probe.json`

确认报告是本次生成，必要时拉取到新的文件名，避免把历史结果当本次结果。只需安装缺失的测试包，不必重建工程。

### C. 再复验 Link 的真实应用手部数据

让用户进入 Link、放下控制器、将双手置于头显前。Unity 使用现有 SealLab 场景，先核对是否正在播放以及 VR / 输入焦点，不要改场景接线。

工程现有编辑器诊断工具：`Assets\SealLab\Editor\SealTrackingDiagnostics.cs`。

- 菜单：`Tools / NarutoVR / Capture Tracking Diagnostics`。
- 已进入 Play 时也可使用工程已有请求文件机制 `Logs\seal-tracking-diagnostics.request`；先读脚本确认触发方式。
- 采样约 30 秒，报告 `Logs\seal-tracking-diagnostics.json`。
- 重点看 VR / input focus、nativeEnabled、GetHandState、实际 IHand 是否有效；不要只看“扩展已启用”或系统菜单图标。

原生测试和 Link 测试按顺序进行，不要同时运行并争抢输入焦点。若需要用户戴好、进入 Link 或放下控制器，用简短明确的问题协调。

## 8. 你需要交付的结果

请写一份简短 Markdown 报告到 `Logs\FirmwareUpdate\upgrade-result.md`：

1. 实际使用的官方更新方式；如用 ZIP，列来源、适配依据与 SHA-256。
2. 更新前后真实版本、安装结果与证据文件。
3. 原生与 Link 两组手追踪复验结果，注明采样时是否有输入焦点。
4. 若 Link 仍失败，明确“固件已更新但问题仍存在”，提出下一项能区分原因的检查，不要无证据重装 SDK。
5. 若未能更新，准确说明卡在哪一步、已完成什么、接下来是否需要用户操作。

请不要声称已经更新，直到设备重启后的版本与官方工具/设备状态足以确认。
