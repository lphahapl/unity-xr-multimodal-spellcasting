# 本机音乐制作环境

2026-11-05安装并测通。[music-production技能](../skills/music-production/SKILL.md)负责用途、编曲和验收；[Pedalboard](https://github.com/spotify/pedalboard)负责本轮离线共享混响。当前采样仍由项目已有实录音源渲染，未安装新的古筝/笛子音色库或VST宿主插件。Unity只播放导出的音频，不承担Pedalboard计算。

## 已安装与已验证

- 隔离Python：`C:/Users/鲁迅/.codex/tools/music-production/.venv/Scripts/python.exe`，Python3.13.5，不继承全局site-packages。
- 本机环境清单：`C:/Users/鲁迅/.codex/tools/music-production/runtime.json`，记录已测能力和解释器位置，技能选择渲染路线时优先复用。
- Pedalboard0.9.26、NumPy2.5.4、SciPy1.18.1、SoundFile0.14.0、pyloudnorm0.2.0、Mido1.3.3；完整依赖见冻结清单（历史路径：`../../Logs/yicun-shanhe-pedalboard-v9/tool-setup/freeze.txt`）。
- 官方PyPI wheel安装；安装记录（历史路径：`../../Logs/yicun-shanhe-pedalboard-v9/tool-setup/setup-summary.json`）保留初次临时文件占用失败与成功重试。全局Python未修改，`pip check`通过。
- [真实分轨片段测试](../../scratch/test_yicun_pedalboard.py)13项通过，包括格式、非静音、参数生效、尾音、重复渲染，以及124个不等长块与整段结果相同。测试用混响参数不是正式曲目的参数。

## 复现当前v10

在项目根目录执行：

```powershell
& 'C:/Users/鲁迅/.codex/tools/music-production/.venv/Scripts/python.exe' -X utf8 'scratch/render_yicun_shanhe_phrased_v10.py'
```

输出到`Logs/yicun-shanhe-phrased-v10/`，会重写该目录的同名候选及报告，先另存需要保留的候选。脚本不自动覆盖Unity正式音频。v9及工具安装测试保留在原目录。

v10针对用户“笛子离散、没有主题”的试听反馈重写乐句：8.25秒与16.25秒完整陈述同一个六音主题，再模进、换尾与再现。`scratch/yicun_dizi_phrases_v10.py`将实录持音按24ms等功率交接连接，整句连续力度、句首起音和句尾收气，句间至少约0.65秒换气。它是持音采样编程连接，并非录制了真实连奏过渡；JSON保存逐句表达控制点，MIDI保留音高、名义时值和力度，不能单独复现全部奏法。

产物包括112秒WAV、三个同起点/长度的dry分轨、共享混响return、可编辑MIDI、源音符/音源映射/参数/hash报告、开头32秒和跨接缝20秒试听。MIDI不放入GM替代program；loop marker在112秒，持音可延伸到marker之后。JSON与脚本保存真实采样映射和尾部，直接用通用MIDI播放器不等于复现原音色。

正式混响为短而轻的共享空间：room_size0.36、damping0.64，湿声通道105Hz高通/6200Hz低通；干声保留现有实录处理。输出以`(channels, frames)`连续float32传入Pedalboard；同一流的后续块/轮保留状态。连续三轮渲染时第三轮与第二轮逐样本差为0，取稳定一轮保留跨缝混响，未给首尾机械清零。

固定音源校准保留减法编曲与乐句动态，只在最终混音施加一次整体增益。母带目标约-29LUFS用于当前轻声常态场景，不作为其他作品的统一标准。真实音源、代码和输出hash见render.json（历史路径：`../../Logs/yicun-shanhe-phrased-v10/render.json`）。

## 验收与发布

用已安装skill的只读分析器检查候选，`--out`使用未存在的新文件：

```powershell
& 'C:/Users/鲁迅/.codex/tools/music-production/.venv/Scripts/python.exe' -X utf8 'C:/Users/鲁迅/.codex/skills/music-production/scripts/analyze_audio.py' 'Logs/yicun-shanhe-phrased-v10/yicun_shanhe_phrased_v10.wav' --loop --sections 'Logs/yicun-shanhe-phrased-v10/sections.json' --out 'Logs/yicun-shanhe-phrased-v10/new-audio-review.json'
```

谱面检查实际note尾部、换气与主题；信号检查分轨相加、削波、动态和接缝；听感另行试听。v10句内短交接是有意奏法，需查电平与音高平台，不要求每音之间静音。固定音源清理不等于所有录音纯净：G4古筝仍保留有限处理后的自然共振，需要结合整曲听辨。

发布前保存旧WAV和.meta，核验正式文件与候选hash相同，保留GUID和PCM/Streaming导入设置。P1播放器使用完整AudioClip的loop，前奏每轮都会经过；一次性前奏需要另行改变播放结构。目标设备的听感、性能及与旁白/SFX混播不能由桌面波形检查代替。
