# 音乐工作流工具核查（2026-11-04）

需求：查GitHub上更合适的音乐工作流工具，优化已经安装的`music-production`。结论是保留现有音乐决策入口，借鉴成熟流程，再按实际音源选择执行后端。当前古筝、竹笛、轻弦乐BGM的音色问题不能由换一份提示词或安装MIDI库直接解决。

## 选择与取舍

| 候选 | 已核实的价值 | 本次决定 |
|---|---|---|
| [sb-dev/music-production-skills](https://github.com/sb-dev/music-production-skills) | 创作、制作、评价分工；先验证小片段，保留已接受的决定，按失败层定点修复；MIT | 吸收工作流原则。没有引入其Replicate执行依赖或阶段审批制度；现有用户授权继续有效 |
| [sirruf/music-gen-skill](https://github.com/sirruf/music-gen-skill) | Mido→FluidSynth/TiMidity→SoundFont，本地可编辑源与依赖探测；MIT | 借鉴明确的MIDI/音源接口。Bash路径、固定44.1k与仅检查输出存在不足以直接覆盖Windows游戏交付 |
| [glincker/ableton-skills](https://github.com/glincker/ableton-skills) | 现有会话读取、快照、可撤销MIDI编辑；MIT | 需要另装Ableton与兼容MCP；保留操作思路，不沿用统一流行曲式、EQ或响度规则 |
| [cclawton/music21-mcp](https://github.com/cclawton/music21-mcp) | 符号MIDI解析、移调与模式搜索；MIT | 有具体符号处理需求再接。其曲式实现使用简单音高窗口，不作为音乐结构或听感的自动验收 |
| [Mido](https://github.com/mido/mido)、[pretty_midi](https://github.com/craffel/pretty-midi) | MIDI消息或音符级编辑；均MIT；后者默认`synthesize()`使用正弦波 | 需要可编辑MIDI时选一种适合现有工程的接口；不将program名称当真实音色 |
| [music21](https://github.com/cuthbertLab/music21) | 符号乐谱、和声与MusicXML分析；BSD-3-Clause，曲库另有条款 | 作为可选谱面辅助；不能替代音源、渲染或试听 |
| [FluidSynth](https://github.com/FluidSynth/fluidsynth) | SoundFont合成及MIDI音频渲染；LGPL-2.1；有Windows发布 | 有合适SF2/SF3库时可用。引擎许可证不提供音色库授权，不把GM通用音色冒充民族乐器 |
| [DawDreamer](https://github.com/DBraun/DawDreamer)、[Pedalboard](https://github.com/spotify/pedalboard) | Python宿主/效果处理；支持插件乐器，前者侧重路由图与自动化；均GPL-3.0 | 已有VST音源时评估。先核版本和Windows加载，再做目标音源片段；不同时强装两者，不将它们视为内置专业音源 |
| [librosa](https://github.com/librosa/librosa)、[pyloudnorm](https://github.com/csteinmetz1/pyloudnorm) | 音频特征分析、响度测量；分别ISC、MIT | 保留现有pyloudnorm测量；需要谱面之外的信号定位再引入librosa。节拍/调性估计不是音乐质量评分 |
| [FFmpeg](https://github.com/FFmpeg/FFmpeg) | `ebur128`提供综合、瞬时/短时响度、LRA及可选true peak；授权依构建组件而异 | 补可选只读复核命令；本机未测通，未声称已接入或已运行 |
| [sfizz](https://github.com/sfztools/sfizz)、[sfizz-ui](https://github.com/sfztools/sfizz-ui) | SFZ引擎与插件；BSD-2-Clause；仓库页面均显示2026-06-21归档 | 保留固定版本候选，撤除“活跃维护默认后端”的可能误判；依然需要真实采样与兼容宿主 |

这是对当前任务的选型判断，不是全网工具排名。许可证栏记录仓库声明；音源、插件、模型与生成内容的权利分别核查。没有复制上游技能或执行安装脚本。

## 本机能力与本次实施

当前探测的Python为`D:/py/python.exe`（3.13.5）：NumPy 2.3.4、SoundFile 0.14.0、SciPy 1.17.1、pyloudnorm 0.2.0可发现；Mido、pretty_midi、music21、librosa、Pedalboard、DawDreamer未在该解释器发现。`Get-Command`未在当前PATH找到ffmpeg、ffprobe或reaper，这不是全盘安装检查。

本次更新并同步用户级skill：

- 主入口增加工具选择条件、片段先行、按根因所在层修复，并保留已接受主题和编制。
- 新增[工具与渲染流程](../skills/music-production/references/toolchain.md)：候选选择、能力状态、MIDI时间/音色映射、表情控制、分轨/效果尾部、复现信息及同响度对照。
- [音频复查](../skills/music-production/references/audio-review.md)补FFmpeg可选测量路径，区分LRA、true peak与现有脚本的窗口极差/4倍峰值估计。
- 沿用现有只读分析脚本；不新增依赖、不重写BGM、不改变Unity播放器。

对本项目下一次实际制作，优先使用已有可编辑源和采样；需要进一步提升音色时，先核可用的古筝/竹笛音源与演奏技法，再决定是否接入插件或SFZ宿主。仅有草稿目标时可先交MIDI和标明替代音色的试听。

## 核查与限制

GitHub网页、README、实际skill/脚本与官方文档用于能力判断；GitHub匿名API返回403限额，未用失败结果推断维护状态。sfizz归档由仓库页面核实。FFmpeg网页抓取超时后读取官方仓库`doc/filters.texi`的ebur128章节；命令是官方语法的只读用法，尚未在本机运行。

两名内置协作代理分别核查工作流与采样/宿主，父代理复核并取舍；第二账号额度查询因本机接口未启动失败，未调用外部账号。详细来源、静态审阅、环境和交付验证保存在`Logs/music-production-tools/`；未安装候选宿主、未加载真实VST、未进行新音频试听，不宣称这些能力已经部署完成。
