# 中文口令测试：本机 sherpa-onnx KWS

配置更新：当前默认口令仍为下述三项，但已支持从 Unity `SkillCatalog` 导出 `Tools/SkillVoice/skill-voice-config.json`，新增口令不必改 Python 或接收器源码。最新配置步骤见 [项目 README](../README.md)；变更后需要重新导出、重启服务并重新进入 Play。显式实验词表不会自动修改 Unity 的允许口令。

本功能用中文关键词识别模型匹配「护体咒」「火球术」「取消施法」。不是录一段声音后做波形模板比对，也不是通用语音转写。PC 上的 Python 进程采集选定麦克风，离线推理，只把口令事件发到 Unity 的 `127.0.0.1`。不上传音频，不自动保存麦克风录音。Steam Link 场景需要 Windows 能收到头显的麦克风输入；Quest 独立 APK 不能直接使用这套 PC 回环服务。

KWS 不理解整句意图或否定语义：「不要释放火球术」仍可能触发「火球术」。静音、普通负例和合成音检查通过不能当作真人或语义准确率。本次用户录音已实际出现否定句触发，详见 `docs/PLAYER_AUDIO_BASELINE_ZH.md`；长句扩展仅在独立实验词表中验证，没有接入新的游戏效果。

“完整口令”表示关键词本身完整命中，不代表整段话只能有这几个字。KWS 不理解否定或谈论意图，例如“不要释放火球术”仍可能命中“火球术”。真实玩家录音要另外检查这类样本；本轮合成音负例通过不能据此宣称已解决语义误触发。

## 安装与启动

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

## 三层测试

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

`NarutoVR.SealCombat.SkillVoiceReceiver` 是独立组件，不依赖外部 Unity SDK 或原生 DLL。事件 `Action<string,double> PhraseReceived` 的第二参数是映射到 `Time.realtimeSinceStartupAsDouble` 的**词尾能量估计时间**；业务层必须使用相同的不缩放时钟做组合窗判断，不能混用 `Time.time`。每帧最多处理 64 个 UDP 包，不在网络线程调用 Unity API。仅绑定 IPv4 loopback，来源端口必须是服务端口，校验会话、递增序号、口令白名单和消息年龄，拒绝过期包与重复包。

默认 Unity 接收 `127.0.0.1:47831`，Python 监听 `127.0.0.1:47832`。端口被占用时会明确报错，可在 Inspector 与 Python `--port` / `--unity-port` 同步更换。该协议用于本机可信开发环境，没有加密或身份认证；回环限制不会防止本机其他进程伪造数据。

### 时间是如何得到的

- Python 用单调高精度 `time.perf_counter()`。麦克风回调使用 PortAudio `inputBufferAdcTime` 与 `currentTime` 的差值映射采集起点。驱动不给有效 ADC 时间时，退回回调时间减帧长，并在 `capture_basis` 标记 `callback-time-estimate-no-valid-adc`。
- 每个输入块 20 ms，通过 RMS 阈值估计有声帧。KWS 命中后等待默认 120 ms 静音，将最后有声帧末尾作为词尾估计；连续有声最多再等 0.8 秒。`--energy-threshold` 默认 0.012，可按麦克风增益调节。这是简单能量分段，环境噪声或连续讲话会移动估计，**不是人工标注词尾或精确强制对齐**。
- Unity 发 `client_send=t1`，服务回 `server_recv=t2, server_send=t3`，Unity 收到时记 `t4`。映射偏移为 `((t1+t4)-(t2+t3))/2`，回环 RTT 为 `(t4-t1)-(t3-t2)`。拒绝 RTT 大于 250 ms 的样本，优先低抖动样本，定期刷新；会话变化必须重新握手。
- `LastLatencyMilliseconds = Unity接收时间 - 映射后词尾估计`，包含后续静音、推理、排队与 Unity 帧等待。`ClockUncertaintyMilliseconds = RTT/2` 只说明握手部分，不包含 ADC、能量分段或 Steam Link 传输误差；不能把它当总精度。
- sherpa 原始 token 时间保留在日志，仅供诊断；Python 2.0.1 的结果对象不暴露段起点，不能据此当跨进程词尾时钟。每次命中按官方示例 `reset_stream`，保留已有声学上下文。

对延迟敏感的组合逻辑应使用采集结束估计时间与结印结束时间比较，并记录实际接收延迟；不能在接收时一律填当前时间来假装零延迟。若边缘时限误判，先检查日志中的采集时间来源和噪声分段，再调整游戏允许时间。

## 选型、来源与限制

采用 [k2-fsa/sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) 的关键词识别接口，官方模型为 `sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20`，使用 `chunk-8`、int8 encoder/joiner 与 fp32 decoder。该模型可通过拼音词表定制中文口令；本地 `keywords.txt` 写出三条发音，不依赖网络文本转拼音服务。模型来源：[官方 KWS 模型文档](https://k2-fsa.github.io/sherpa/onnx/kws/pretrained_models/index.html)、[GitHub release](https://github.com/k2-fsa/sherpa-onnx/releases/tag/kws-models)。API 对照：[官方麦克风示例](https://github.com/k2-fsa/sherpa-onnx/blob/master/python-api-examples/keyword-spotter-from-microphone.py)、[Python KeywordSpotter](https://github.com/k2-fsa/sherpa-onnx/blob/master/sherpa-onnx/python/sherpa_onnx/keyword_spotter.py)。

固定依赖版本在 `requirements.txt`。下载器只从官方 GitHub 下载模型数据和许可证，限定压缩包与解压总量分别不超过 150 MiB，检查路径穿越与软链接；不执行远程安装脚本。原始压缩包保留，SHA-256、来源和运行库 Apache-2.0 许可证记录在 `models/SOURCE.json` / `SHERPA_ONNX_LICENSE.txt`。下载后强制核对 [GitHub 官方资产列表](https://github.com/k2-fsa/sherpa-onnx/releases/expanded_assets/kws-models) 公布的 SHA-256 `68447f4fbc67e70eee3a93961f36e81e98f47aef73ce7e7ca00885c6cd3616a6`，这属于内容校验，不是签名来源证明。模型与运行库的权利条款需分别看原始来源，不能仅凭运行库许可证承诺模型的所有商用权利。

需要验收的真实边界：头显麦克风路由、真人不同音色与口音、环境噪声误触发、Steam Link 额外传输延迟、组合时限附近的漏判。测试中暂不启动云端 ASR，也不悄悄改用键盘模拟成功。

## 本次实测记录（2026-10-29）

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
