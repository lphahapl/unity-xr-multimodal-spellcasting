# 中文口令：PC 与 Quest 离线 sherpa-onnx KWS

当前接入状态（2026-11-08）：`SkillVoiceReceiver` 保留同一个业务门面。Android 非 Editor 自动使用设备内 sherpa-onnx 2.0.1 C API；Windows/PC 编辑器默认仍使用 Python＋回环 UDP。编辑器只有显式勾选 `useNativeInEditor` 才在 Play 中使用本机原生库和麦克风；普通编辑模式不会启动采集。P1 APK 已构建并以独立包名安装到 Quest 3，用户已进入训练场；截至本次 520.6 秒现场记录，收到「雷光剑」6 次、「火球术」4 次，成功召剑 4 次、生成火球 3 次并发射 1 次。首次授权、两次失焦释放及恢复识别已记录。独立 .NET 26 项、最终 Unity 132 项、Quest ARM64 C API 合成音 7 项和 APK 审核 23 项通过。取消口令现场验证及系统权限异常、噪声/准确率和长时性能仍待验，分层证据见下文。

在 Windows 编辑器进入 Play 时，自动启动器按场景里绑定到语音接收器的 `SkillTestController.catalog` 生成本次词表，写入 `Library/SkillVoice/play-config.json` 并交给本机服务。P1 使用「护体咒」「火球术」「金刚护体」「雷光剑」「取消施法」。修改目录后重新进入 Play 即可加载。退出播放会停止该编辑器启动的服务。

手动启动服务时可以给 `Start.ps1` 传 `-Config 'Assets/Configuration/LinearRpg/skill-voice-config.json'` 以使用随工程提供的 P1 词表。手动服务由启动者负责停止和重启，Unity 会使用已经在运行的服务。手动命令默认读 `Tools/SkillVoice/skill-voice-config.json`，也可通过 Unity 菜单导出当前场景目录覆盖该文件。[配置指南](CONFIGURATION_GUIDE_ZH.md)

本功能用中文关键词识别模型匹配场景实际绑定的 `SkillCatalog`，包含全部需要语音的技能及取消口令；纯手势技能不写入语音词表。不是录一段声音后做波形模板比对，也不是通用语音转写。PC 的 Python 进程采集选定麦克风，离线推理，只把口令事件发到 Unity 的 `127.0.0.1`；Quest APK 在头显内完成采集和推理，不依赖电脑 Python 服务。两条语音路径均不上传音频、不自动保存麦克风录音。Steam Link 仍需要 Windows 能收到头显的麦克风输入。

KWS 不理解整句意图或否定语义：「不要释放火球术」仍可能触发「火球术」。静音、普通负例和合成音检查通过不能当作真人或语义准确率。本次用户录音已实际出现否定句触发，详见 `docs/research/PLAYER_AUDIO_BASELINE_ZH.md`；长句扩展仅在独立实验词表中验证，没有接入新的游戏效果。

“完整口令”表示关键词本身完整命中，不代表整段话只能有这几个字。KWS 不理解否定或谈论意图，例如“不要释放火球术”仍可能命中“火球术”。真实玩家录音要另外检查这类样本；本轮合成音负例通过不能据此宣称已解决语义误触发。

## PC 安装与启动

在项目根目录 PowerShell 执行。依赖只装进 `Tools/SkillVoice/.venv`；模型和原始许可证保存在 `Tools/SkillVoice/models`。

```powershell
& './Tools/SkillVoice/Setup.ps1' -Python 'C:/Users/鲁迅/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --list-devices
& './Tools/SkillVoice/Start.ps1' -Device 61
```

`61` 是开发时枚举到的 `麦克风 (Steam Streaming Microphone Wave), Windows WDM-KS`，**设备 ID 会变化，先枚举再选**。当时 Windows 默认输入是 ATK N9 PRO，不能把默认设备等同于 Steam Link。列表中另有叫 Steam Streaming Microphone 的输出设备，不要选输入通道为 0 的条目。

如果指定设备拒绝 16 kHz，可传 `-SampleRate 48000`；sherpa 在内部转换采样率。不要为了让状态变绿随便改成电脑麦克风后宣称已测试头显。`Start.ps1` 隐藏窗口启动，并写 `Tools/SkillVoice/logs/service.jsonl` 与 `service.stderr.log`。进程存在不等于就绪：日志必须出现 `kind=ready`，Unity 必须收到心跳并完成时钟同步。

```powershell
Get-Content './Tools/SkillVoice/logs/service.jsonl' -Wait
& './Tools/SkillVoice/Stop.ps1'
```

服务退出、丢帧、设备失败时，Unity 最迟在 3 秒心跳超时后显示未启动/断开，不会回退成假识别。音频未接好时可以继续做结印测试。

## Quest 独立 APK：接入、构建与验收

### 随包模型与运行路径

- `Assets/Plugins/Android/arm64-v8a` 提供 Android ARM64 原生推理库；`SherpaKeywordEngine` 通过 Cdecl P/Invoke 调用 `sherpa-onnx-c-api`，路径与中文关键词显式编码为 UTF-8。原生对象在同一个 worker 线程创建、推理和释放，结果经主线程交回 `SkillVoiceReceiver`。
- `Assets/StreamingAssets/SkillVoice/model-manifest.json` 随 APK 包含四个模型文件的长度和 SHA-256；原版 chunk-8 int8 encoder/joiner、fp32 decoder 与 tokens 合计 5,449,043 字节。首次启动从 APK 本地资源读取并复制到应用的 `Application.persistentDataPath/SkillVoice/<version>` 私有持久目录，已有缓存也逐项验证。使用 `UnityWebRequest` 读取 Android `jar:` 资源不代表联网下载；无首次运行网络依赖。
- `NativeVoiceConfiguration.BuildKeywordText` 从实际场景的 `SkillTestController.catalog` 生成词表，并校验每个拼音 token 是否在随包 `tokens.txt` 中。取消口令同样来自该 Catalog。不要把历史三口令测试词表或某个 JSON 当作 APK 的最终目录；新增技能需配置 `phraseTokens`，取消需配置 `cancelTokens`。
- 当前推理参数与 PC 基线一致：CPU 2 线程、特征采样率 16 kHz、阈值 0.25、keyword score 1、trailing blanks 1、max active paths 4。Unity 请求 16 kHz 环形麦克风，按设备返回的实际采样率读取、混合为单声道并交给引擎重采样。

### 权限、停止与重试

Android Manifest 声明 `RECORD_AUDIO`，运行时先请求麦克风权限；拒绝、权限撤回、模型校验失败、原生库加载失败、采样中断或解码积压都会显示未就绪/失败原因，不回退到 PC、云端或模拟识别。权限被永久拒绝时需在系统应用权限中允许。原生采集发生故障时，腕匣「咒语」卡页脚显示中文提示与「重试麦克风」，沿用射线轻捏确认；正常状态、其他卡和 PC 服务隐藏该按钮。它调用接收器公开的 `RetryNativeMicrophone()`，该接口仅在原生后端、Play 且组件启用时生效。

暂停、失焦、禁用或销毁组件会释放麦克风并使当前识别代次失效。正常恢复创建新流；发生过运行故障时仍等待主动重试、新 Catalog 或已恢复的权限，不用反复启停掩盖故障。权限弹窗导致的暂停保留本次授权回调。更换 Catalog 和重启同样丢弃旧队列/旧词尾；主线程门面继续拒收旧代次、白名单外、重复、过期或未来时间结果。检测到超过 250 ms 的采集停顿、采样位置不动或时间轴不连续会停止当前流，避免把旧音频重标成新口令。

词尾能量估计不是事件唯一 ID。无间隔连读的真实模型测试中，「火球术」和紧随的「取消施法」可能共享同一个词尾估计。原生门面和匹配器允许同词尾的不同合法口令，按归一化口令拒绝同词重放；真正更早的时间、取消/重置屏障前的旧输入仍拒收，不通过改大时间戳制造新口令。同帧激活与冷却规则保持原样。

### 本地检查与构建命令

在未打开的同版本隔离工程执行，避免抢占正在编辑的正式工程。下面的 `$voiceValidationProject` 需指向已经准备好的隔离副本；其 `Assets`、`Packages`、`ProjectSettings` 应包含待验版本。WAV fixture 可以通过环境变量指向主工程，测试不会打开麦克风或进入 Play，也不会保存/重载场景。

```powershell
$voiceUnity = 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe'
$voiceValidationProject = 'D:/UnityProjects/NarutoVR-VoiceValidation'
$env:NARUTOVR_VOICE_FIXTURE_ROOT = 'D:/UnityProjects/NarutoVR/tools/SkillVoice/fixtures'
& $voiceUnity -batchmode -nographics -projectPath $voiceValidationProject -executeMethod NarutoVR.SealCombat.Editor.NativeVoiceChecks.ExecuteBatch -logFile "$voiceValidationProject/Logs/native-voice-checks.log"
& $voiceUnity -batchmode -nographics -quit -projectPath $voiceValidationProject -executeMethod NarutoVR.SealCombat.Editor.QuestVoiceBuild.ValidateP1 -logFile "$voiceValidationProject/Logs/quest-voice-preflight.log"
& $voiceUnity -batchmode -nographics -quit -buildTarget Android -projectPath $voiceValidationProject -executeMethod NarutoVR.SealCombat.Editor.QuestVoiceBuild.RunP1 -logFile "$voiceValidationProject/Logs/quest-voice-build.log"
```

`NativeVoiceChecks.Run()` 是编辑模式入口，批处理使用 `ExecuteBatch()` 返回退出码。它复用音频、诊断和时钟回归检查，覆盖首章实际 Catalog、token 负例、模型清单与哈希、门面的代次/白名单/时效/遥测门控，并通过 Unity Mono 调用真实 C API 推理合成 WAV。门面输入与音频 duck 门控检查明确为 synthetic，不能证明真人输入或物理混音效果。报告写在隔离工程 `Logs/quest-native-voice-20261108/unity-native-checks.json`。

同一入口也运行腕匣交互和视图生命周期检查，包含重试按钮的状态切换；这些是不启用麦克风的模拟状态检查。若命令运行器缺少 `ALLUSERSPROFILE` 导致 UPM 报 `path ... undefined`，按现有 `Tools/Start-Unity.ps1` 的做法，仅为启动子进程补齐 CommonApplicationData。Android 构建使用 ASCII 路径的 `GRADLE_USER_HOME`，避免本机中文用户目录影响原生构建命令。

用户本次选用 P1：编辑器菜单 `Tools/NarutoVR/Validate P1 Quest Voice Build` / `Build P1 Quest Voice APK` 对应 `ValidateP1()` / `RunP1()`，输出 `Builds/Quest/Kaogong-P1-Quest-Voice.apk`。预检核对 ARM64/IL2CPP、最低 API 32、Manifest 权限、原生库导入、模型哈希、P1 实际目录与雷光剑练习授权。要求编辑器空闲且无未保存场景；不保存场景或改写 Build Settings 场景列表。

P1 Development APK 使用独立应用 ID `com.kaogonglvdu.p1.voicecheck`，与头显上使用 Unity 模板包名的「EP07气象与天文」并排安装。工具仅在 P1 构建/预检期间临时设置该 ID，`finally` 恢复原设置，报告记录实际测试 ID。原 `Validate()` / `Run()` 首章入口保留，使用工程当前 ID；当前模板 ID 与另一应用冲突，不能直接拿该包覆盖安装。正式发布的应用 ID、版本和签名另行确定。

报告为 `Logs/quest-native-voice-20261108/build-build-*.json`；成功后仍检查 APK 实际包含 ARM64 `.so`、四个模型和录音权限。以报告的 `scene`、`applicationIdentifier` 和 APK 哈希识别本次包，不把先前构建候选当最终版本。

独立 .NET 引擎回归可在主工程复现（此检查不替代 Unity Mono/IL2CPP）：

```powershell
dotnet run --project Logs/quest-native-voice-20261108/engine-tests/EngineTests.csproj -- D:/UnityProjects/NarutoVR
```

### 连接头显后的真实验证

使用 Unity 自带 SDK 的 ADB，先用 `devices` 确认真实设备序列号。安装成功的 APK 后在头显内处理权限；以下命令不代替用户点击授权，也不预先替用户授予录音权限。应用 ID 从本次构建报告读取，避免依赖开发时的包名。

```powershell
$voiceAdb = 'D:/unity_hub/6000.3.10f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
& $voiceAdb devices
$questSerial = '替换为已连接头显序列号'
$voiceReportFile = Get-ChildItem -LiteralPath "$voiceValidationProject/Logs/quest-native-voice-20261108" -Filter 'build-build-*.json' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$voiceBuild = Get-Content -LiteralPath $voiceReportFile.FullName -Raw | ConvertFrom-Json
if (-not $voiceBuild.passed) { throw '本次 APK 构建尚未通过' }
& $voiceAdb -s $questSerial install -r $voiceBuild.outputPath
& $voiceAdb -s $questSerial shell monkey -p $voiceBuild.applicationIdentifier -c android.intent.category.LAUNCHER 1
& $voiceAdb -s $questSerial logcat -v threadtime 'Unity:I' '*:S' | Tee-Object -FilePath "$voiceValidationProject/Logs/quest-native-voice-20261108/headset-logcat.txt"
```

真机逐项记录首次授权/拒绝后重试、系统撤权再允许、摘戴/暂停恢复、前后台切换、无网络冷启动、每个当前 Catalog 口令与取消、连读/重复/否定句/环境噪声，以及语音与 BGM 同播。检查暂停前的口令不会在恢复后释放技能；观察实时音频帧数/RMS、词尾估计延迟和状态原因，同时记录机型、系统、APK 哈希、CPU/帧时间与持续运行情况。KWS 否定语义限制仍存在。

### 本次分层验收记录（2026-11-08）

最终 P1 包已安装到 **Quest 3 / Android 12 / SDK 32**，应用 ID 为 `com.kaogonglvdu.p1.voicecheck`。主工程交付文件为 `Builds/Quest/Kaogong-P1-Quest-Voice.apk`，149,881,612 字节，SHA-256：`4e7ba58acabe2cf59d5dc0293c051b90008db8195607b00a1ad3dcf14aee38db`。这组信息对应用户本次实际进入的 P1 测试包。

以下报告均位于 `Logs/quest-native-voice-20261108/`，各层分别记录，不能互相替代：

| 验证层 | 已确认结果 | 证据文件 |
|---|---|---|
| 独立 .NET 引擎 | 26 项通过；真实 Windows DLL、中文路径、合成 WAV 与解码边界 | `engine-tests-result.json` |
| Unity Mono 编辑器 | 最终 132 项通过，场景未改变；包含原有 8 项＋新增 6 项时钟检查、真实 C API/WAV、连读同词尾、腕匣和重试 UI 状态；不启用麦克风 | `build-native-checks-r5.json` |
| Quest ARM64 独立 C API | 当前 P1 五口令合成 WAV 等 7/7 项通过，14 项设备文件哈希核对通过；使用真实设备原生库，但不是麦克风真人测试 | `quest-arm64-smoke/five-keywords/result.json` |
| Android 构建及 APK 内容 | 最终 P1 构建成功；APK 23 项审核通过。APK 内 ONNX Runtime 库经过 NDK strip，已用相同过程重现并逐字节匹配，不把 strip 后的哈希变化误认为换库 | `build-p1-final-result.json`、`apk-audit.json` |
| P1 真人现场链路 | 用户确认进入场景；截至 520.6 秒收到火球 4 次、雷光剑 6 次，技能激活接受为火球 3 次、雷光剑 4 次；火球发射 1 次。Android 16,000 Hz 麦克风采样持续更新 | `device-summary.json`、`device-live/20261108-131117-435f92db7f3e4c169db760f9b5f760fe.jsonl` |
| 现场权限与焦点生命周期 | 首次权限提示→原生启动→识别中，`RECORD_AUDIO` 已授予；两次失焦均释放麦克风并恢复识别 | `device-summary.json` 中 `nativeStatusChanges` |

现场首次火球未出对应腕匣主入口已打开、输入被 UI 占用；这次阻止来自原有玩法门控。其余 3 次火球激活中，2 次因手部追踪丢失消散，1 次完成发射；6 次雷光剑口令中，4 次成功召剑、2 次被冷却阻止。排查“识别了但没放出来”时，应同时查看 `voice_received`、技能激活、UI 占用、追踪与冷却记录。

两次现场焦点恢复记录分别为运行时 58.986 秒失焦释放→69.008 秒恢复识别，以及 207.581 秒失焦释放→211.594 秒恢复识别；这是完整离开/返回过程的日志位置，不是单独初始化耗时。最终现场汇总 `device-summary.json` 截至运行时 520.620 秒，共 1,439 条记录。本次 10 个已匹配样本的词尾能量估计至接收为 **315.16–500.74 ms**，未经过人工词尾标注，不能据此推导代表性准确率、延迟分位数或长时性能。

**尚未验收：** 用户现场念「取消施法」、系统拒绝/撤回权限及恢复的完整测试、断网冷启动、长时间运行性能、背景噪声/否定句和多说话人准确率。合成音中的取消及同词尾回归已经通过，但现场取消仍独立标为待验；现有 KWS 否定语义限制保持不变。

## PC 三层测试

1. `--self-test`：加载真实模型、校验拼音 token、推理两秒静音不应触发，并用独立临时回环端口验证 UTF-8、四时间戳握手和事件延迟字段。不会向正式 Unity 端口注入模拟口令。
2. `--wav`：用真实 KWS 推理 PCM 16-bit WAV。默认快速离线只输出音频时间，不发送 Unity 事件；加 `--realtime` 才按实际时钟重放并发送事件，Unity 明示「WAV 回放测试」。
3. 麦克风实测：运行服务，在语音测试模式逐个说口令，再在组合模式先结印后说口令。需要头显用户完成真人、噪声、远近、误触发等验收。

```powershell
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --self-test
& './Tools/SkillVoice/Generate-TtsFixtures.ps1'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --wav './Tools/SkillVoice/fixtures/shield.wav' --expect '护体咒'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --wav './Tools/SkillVoice/fixtures/fireball.wav' --expect '火球术'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --wav './Tools/SkillVoice/fixtures/cancel.wav' --expect '取消施法'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --wav './Tools/SkillVoice/fixtures/negative.wav'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --wav './Tools/SkillVoice/fixtures/shield.wav' --realtime
```

合成音由 Windows `System.Speech` 的中文 Huihui 语音直接生成 WAV，不播放扬声器；来源记录在 `fixtures/SOURCE.json`。合成音命中只能说明当前模型、词表、推理链路可用，不能报告成真人准确率。

### 仅接线模拟

无需模型或麦克风，显式命令产生标注 `protocol-test` / `synthetic-test` 的事件。Unity 同时显示「模拟接线测试（未运行语音识别）」。模拟时间是人为设置的，绝不能用于真实延迟统计。

```powershell
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --test-event all
# 组合测试时可只发一个口令；命令启动后 1.5 秒发出：
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/skill_voice.py' --test-event '护体咒'
```

同一端口只允许一个服务；先停止连续麦克风服务再运行 WAV 或模拟。正式使用时可在 `SkillVoiceReceiver` Inspector 取消「允许显式模拟接线测试」和「允许 WAV 回放测试」。

## Unity 接口与组合时限

`NarutoVR.SealCombat.SkillVoiceReceiver` 是 PC UDP 与 Android 原生后端共用的门面；原生路径依赖随包的 sherpa-onnx/ONNX Runtime 原生库。事件 `Action<string,double> PhraseReceived` 在主线程发出，第二参数是处于 `Time.realtimeSinceStartupAsDouble` 时间域的**词尾能量估计时间**；业务层必须使用相同的不缩放时钟做组合窗判断，不能混用 `Time.time`。PC 路径每帧最多处理 64 个 UDP 包，不在网络线程调用 Unity API；仅绑定 IPv4 loopback，校验来源端口、会话、递增序号、口令白名单和消息年龄。原生路径直接递交带采集时间和识别代次的结果，不构造伪 UDP 包；两者均拒收过期、重复与不在当前目录的口令。

PC 后端默认 Unity 接收 `127.0.0.1:47831`，Python 监听 `127.0.0.1:47832`。端口被占用时会明确报错，可在 Inspector 与 Python `--port` / `--unity-port` 同步更换。该协议用于本机可信开发环境，没有加密或身份认证；回环限制不会防止本机其他进程伪造数据。Android 原生后端不监听这些端口。

### PC 时间是如何得到的

- Python 用单调高精度 `time.perf_counter()`。麦克风回调使用 PortAudio `inputBufferAdcTime` 与 `currentTime` 的差值映射采集起点。驱动不给有效 ADC 时间时，退回回调时间减帧长，并在 `capture_basis` 标记 `callback-time-estimate-no-valid-adc`。
- 每个输入块 20 ms，通过 RMS 阈值估计有声帧。KWS 命中后等待默认 120 ms 静音，将最后有声帧末尾作为词尾估计；连续有声最多再等 0.8 秒。`--energy-threshold` 默认 0.012，可按麦克风增益调节。这是简单能量分段，环境噪声或连续讲话会移动估计，**不是人工标注词尾或精确强制对齐**。
- Unity 发 `client_send=t1`，服务回 `server_recv=t2, server_send=t3`，Unity 收到时记 `t4`。映射偏移为 `((t1+t4)-(t2+t3))/2`，回环 RTT 为 `(t4-t1)-(t3-t2)`。拒绝 RTT 大于 250 ms 的样本，优先低抖动样本，定期刷新；会话变化必须重新握手。
- `LastLatencyMilliseconds = Unity接收时间 - 映射后词尾估计`，包含后续静音、推理、排队与 Unity 帧等待。`ClockUncertaintyMilliseconds = RTT/2` 只说明握手部分，不包含 ADC、能量分段或 Steam Link 传输误差；不能把它当总精度。
- sherpa 原始 token 时间保留在日志，仅供诊断；Python 2.0.1 的结果对象不暴露段起点，不能据此当跨进程词尾时钟。每次命中按官方示例 `reset_stream`，保留已有声学上下文。

对延迟敏感的组合逻辑应使用采集结束估计时间与结印结束时间比较，并记录实际接收延迟；不能在接收时一律填当前时间来假装零延迟。若边缘时限误判，先检查日志中的采集时间来源和噪声分段，再调整游戏允许时间。

### Android 原生时间的区别

Unity `Microphone` 没有提供 PortAudio 那样的硬件 ADC 时间戳。原生路径用麦克风环形缓冲位置与 Unity 不缩放时钟建立采样时间轴，之后按实际采样数推进，标为 `unity-microphone-ring-position-estimate`。worker 排队保留原采集时间，不以解码/出队时刻替换它；仍使用 20 ms RMS 帧、0.012 阈值、120 ms 静音或最多 0.8 秒等待和同口令 0.6 秒防抖。正常命中只 reset 搜索并保留声学上下文；生命周期重启则销毁旧流，清空待发结果。

原生 `LastLatencyMilliseconds` 同样是 Unity 收到事件减词尾能量估计，包含静音等待、推理、排队和主线程等待。`ClockUncertaintyMilliseconds = 0` 表示无需跨进程握手，不代表采集误差为零；启动时的环形位置估计、设备缓冲和能量分段误差尚需真机测量，不能直接与 PC 的 RTT/2 当成同一种精度。

## 选型、来源与限制

采用 [k2-fsa/sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) 的关键词识别接口，官方模型为 `sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20`，使用 `chunk-8`、int8 encoder/joiner 与 fp32 decoder。该模型可通过拼音词表定制中文口令；历史三口令测试文件只是 fixture，当前启动词表由实际 Catalog 及其发音配置生成，不依赖网络文本转拼音服务。模型来源：[官方 KWS 模型文档](https://k2-fsa.github.io/sherpa/onnx/kws/pretrained_models/index.html)、[GitHub release](https://github.com/k2-fsa/sherpa-onnx/releases/tag/kws-models)。API 对照：[官方麦克风示例](https://github.com/k2-fsa/sherpa-onnx/blob/master/python-api-examples/keyword-spotter-from-microphone.py)、[Python KeywordSpotter](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/python/sherpa_onnx/keyword_spotter.py)。

固定依赖版本在 `requirements.txt`。下载器只从官方 GitHub 下载模型数据和许可证，限定压缩包与解压总量分别不超过 150 MiB，检查路径穿越与软链接；不执行远程安装脚本。原始压缩包保留，SHA-256、来源和运行库 Apache-2.0 许可证记录在 `models/SOURCE.json` / `SHERPA_ONNX_LICENSE.txt`。下载后强制核对 [GitHub 官方资产列表](https://github.com/k2-fsa/sherpa-onnx/releases/expanded_assets/kws-models) 公布的 SHA-256 `68447f4fbc67e70eee3a93961f36e81e98f47aef73ce7e7ca00885c6cd3616a6`，这属于内容校验，不是签名来源证明。模型与运行库的权利条款需分别看原始来源，不能仅凭运行库许可证承诺模型的所有商用权利。

需要验收的真实边界：头显麦克风路由、真人不同音色与口音、环境噪声误触发、Steam Link 额外传输延迟、组合时限附近的漏判。测试中暂不启动云端 ASR，也不悄悄改用键盘模拟成功。

## 历史 PC 实测记录（2026-10-29）

- 官方 32,885,699 字节模型包完整下载，上述官方 SHA-256 校验通过；许可证原文已保存。
- Unity 接收类使用本机 Unity 6000.3.10f1 的 Roslyn、CoreModule、JSONSerializeModule 和 netstandard 编译通过，零警告；该检查本身不等于完整场景 Play 验收。
- `--self-test` 通过：真实模型加载、拼音词表、两秒静音无误触发、UTF-8 回环和时钟握手；一次握手 RTT 约 0.71 ms，仅为该次样本。
- `verify_fixtures.py` 的 6 项全部通过：三个口令各自命中、负例句子不命中、三秒静音不命中、连续「护体咒→火球术→取消施法→护体咒」全部命中。报告：`Tools/SkillVoice/logs/fixtures-report.json`。连续流保持官方 `reset_stream`；没有为让测试通过而降低 0.25 识别阈值。
- `verify_realtime.py` 运行真实 WAV 推理，经真正回环 UDP 发到测试接收器；故意设置接收器时钟偏移 123 秒，测回 123.0000154 秒，口令末有声帧估计至接收约 386 ms，服务侧约 385.8 ms。该数值包含模型与分段等待，只是单次合成音样本，**不是头显真人延迟或 Unity 场景性能承诺**。报告：`Tools/SkillVoice/logs/realtime-report.json`。
- Steam 输入设备 61 的回调接口在 16 kHz 单声道下打开成功；先验收 1 秒得到 51 个 320 样本音频块，无丢帧，内容全零；再带真实模型连续运行 3 秒正常就绪退出。未收到真人口令，不能证明头显音频已正确路由。阻塞接口报 WDM-KS 不支持，正式工具已采用可用的回调方式。

复核命令：

```powershell
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/verify_fixtures.py'
& './Tools/SkillVoice/.venv/Scripts/python.exe' -X utf8 './Tools/SkillVoice/verify_realtime.py'
```

两个脚本都不会生成真人准确率报告；实时测试使用临时端口，不占正式 Unity 服务端口。结束后不留下持续采集进程。
