# 中文咒语与结印联合释放：调研与讨论稿

> 文档定位：资料核查/历史调研，核对日期见正文，候选方案和外部版本信息需要使用时重新核实。本文不充当当前操作或已完成功能清单；现行入口见[文档索引](../README.md)。

> 2026-10-29 后续决定：交互与框架以 [技能框架与释放交互确定方案](../architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) 为准。本文的统一就绪/推手确认、短时间窗等是历史提案；新版支持纯结印、纯咒语、组合输入，10 秒缓存与默认 8 秒匹配，满足条件立即激活，火球生成到指定手后再由物体自身处理发射。

核对日期：2026-10-29。本文是安装之前的调研快照，当时尚未安装语音库或修改战斗代码。后续已经接入 PC sherpa-onnx KWS 与三种技能输入模式，当前操作见 [语音设置](../guides/SKILL_VOICE_SETUP_ZH.md)，整体落地状态见 [开发索引](../README.md)。以下仍保留原始候选与讨论，不代表当前配置。公开资料核查由父代理和后台资料代理分别完成，父代理核验来源后汇总。

## 工具选择

首选试验 sherpa-onnx 的中文自定义关键词检测（KWS）。它通过限定关键词解码器检测指定短句，支持自定义关键词而不需要为每个新关键词重新训练。每关键词可设置 boosting score 与 trigger threshold，两者都影响漏检/误触；数值不能直接当作正确概率。

- [sherpa-onnx 官方仓库](https://github.com/k2-fsa/sherpa-onnx)：离线推理，支持 Windows、Android arm64、C#。
- [官方 KWS 说明](https://k2-fsa.github.io/sherpa/onnx/kws/index.html)：自定义关键词与触发参数。
- [官方预训练 KWS 模型](https://k2-fsa.github.io/sherpa/onnx/kws/pretrained_models/index.html)：提供中文 WenetSpeech 3.3M 及中英双语 3M 模型；支持配置拼音关键词。
- [Unity 社区接入工程](https://github.com/xue-fei/sherpa-onnx-unity)：README 列出中文 KWS 模型，记载 Android 构建成功。工程基于 Unity 2021，不能据此宣称本项目 Unity 6 / Quest 已验证可用。
- [官方 C# 麦克风关键词示例](https://github.com/k2-fsa/sherpa-onnx/blob/master/dotnet-examples/keyword-spotting-from-microphone/Program.cs)：可参考接口与流式送音频，不能原样当作 Unity 麦克风模块。

备选：

- [Vosk 官方 Unity 工程](https://github.com/alphacep/vosk-unity-asr)，配 [中文小模型](https://alphacephei.com/vosk/models)。这是 ASR 转文字，中文小模型 42 MB，官方标注适用 Android。固定词表/语法需核对模型词表与动态识别图支持，不能保证任意动漫术名填入就能用；参见 [官方适配说明](https://alphacephei.com/vosk/adaptation)。
- [whisper.unity](https://github.com/Macoron/whisper.unity)：有 UPM 接入、Android ARM64 支持及本地多语言识别。适合通用语音转文字备选；仅几个实时口令时优先验证 KWS。不能把 PC/Mac 演示性能推算到 Quest。

若要求玩家先录几遍自己的咒语、之后按声音相似度匹配，属于另一条路线：VAD 切段 → 特征归一化 → MFCC / 声学嵌入 → DTW 时间对齐 → 距离阈值与第二候选分差。参考 [MFCC+DTW 示例](https://github.com/a37950456/Implementation-of-Dynamic-Time-Warping-DTW-) 仅为算法学习入口，不是成熟 Unity/Quest 插件。需多模板、陌生声音/错误咒语负样本和不同语速测试；直接比较波形或拿动漫原声当玩家声纹模板不适合作为起点。


## 已采用的路线与边界

当前采用 PC sherpa-onnx KWS 和单印三模式匹配，默认组合窗 8 秒、缓存上限 10 秒/640 帧。KWS 不理解否定语义；独立 Android 语音尚未接通。被替代的统一就绪、双腕推手确认和语音自动降低手印阈值方案不再作为需求。现行接线见[语音指南](../guides/SKILL_VOICE_SETUP_ZH.md)，匹配架构见[技能框架](../architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md)。
