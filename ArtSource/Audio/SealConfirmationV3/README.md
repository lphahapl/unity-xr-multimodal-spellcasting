# T-01 声音 V3：器物动作与柔和反馈

2026-11-25。用户试听后退回V2，要求除律印外重新换掉偏刺耳、电子感的声音。本版从项目既有录音中取材料接触与摩擦片段，围绕五印的物理含义重新组合。律印铜鸣保持原文件。

历史状态：V3曾在隔离Unity播放与连播通过后替换主工程。用户随后明确响度应按各印特点决定，不要求比律印低，当前生产声音已更新为[V3.1动作力度修订](../SealConfirmationV3_1/README.md)。本目录WAV、配方与测试保留为V3对照；下文较低能量是该历史版本的测量结果，不是当前设计上限。V3当时部署与哈希见[deployment.json](../../../Logs/t01-seal-audio-v3-20261125/deployment.json)。

| 印 | 实义 | 本版动作表达 | 使用的录音类别 |
|---|---|---|---|
| 规 | 围合守界 | 柔软摩擦回拢、末尾收口 | 皮革细尾、抽拉摩擦、轻扣尾部 |
| 矩 | 转折成形 | 短转角、包裹的木面靠合 | 木质材料子层及少量摩擦 |
| 准 | 校齐稳定 | 平缓抽擦逐渐平复、轻定住 | 抽拉/扣合的安静片段 |
| 绳 | 联结约束 | 连续牵拉、纤维感细节束紧 | 箭杆操作/拉弓拟音中的非释放片段 |
| 衡 | 承重镇定 | 低厚承托，轻微余动后落稳 | 木面/掌面材料子层与柔软摩擦 |

这些是对印意的声音表达，不是新录的圆规、纸、绳或秤。未使用完整武器攻击、战斗Impact低频层、振荡器叠加、规则音高滑扫或合成主体。原录音的材质类别依据库目录和作者说明；本轮没有亲自录制器物。

## 试听

- [六印V3对比](candidate_comparison.wav)：**律 → 规 → 矩 → 准 → 绳 → 衡**，每1.2秒一个，保留候选实际文件增益。
- [V2/V3逐印A/B](AB_V2_V3.wav)：同一六印顺序，每印先V2后V3；没有把V3重新抬到V2的响度。
- [0.4秒间隔快速连播](rapid_game_gain.wav)：同一顺序三轮，统一乘Prefab原音量0.62。此文件是本地叠音，实际Unity混音证据另列，不能混称。
- [V3实际Unity视听合版](../../../docs/visuals/20261125_six_seal_audio_v3/SixSealPreview.mp4)：律、规、矩、准、绳、衡；采用原Prefab音量0.62及实际AudioRenderer输出，未重新响度归一。
- [Unity快速连播实际混音](../../../Logs/t01-seal-audio-v3-20261125/unity/Rapid/unity-output.wav)：规、矩、律、衡、准、绳两轮，0.4秒输入槽；各印实际确认时刻包含原保持时间。

五音峰值约−10.00至−11.89 dBFS，文件长0.39–0.51秒。五音前400ms K加权能量跨度约3.83dB（±1.92dB），比律印低约7.39–11.22dB。本轮保留自然轻摩擦，没有为贴合一个数字而强放大细噪声。律印作为保留基准会更响；相近能量与主观等响不同，响度和材质听感仍可继续批注。

## 原件、出处与使用边界

源文件来自已导入项目的 `Assets/Imported/BattleSoundLibrary/Battle 1/`。本地原包元数据明确商品176417、Infinity PBR / Magic Pig Games、版本4.2.0.1；30个候选GUID与2026-11-08导入清单吻合。作者说明其武器拟音由声音设计师录制，魔法音效另外设计。[作者来源说明](https://infinitypbr.com/packs/battle-sound-library)

该产品标注采用Unity标准Asset Store EULA，可在有效授权范围内修改并嵌入游戏，不是可自由分发的原始采样库。[官方商品](https://assetstore.unity.com/packages/audio/sound-fx/weapons/battle-sound-library-magic-melee-human-made-audio-fantasy-rpg-176417) · [许可条款2.2](https://unity.com/legal/as-terms)。本轮按用户明确指定的既有工程素材做本地处理，未核查账户购买凭据，不能把`.meta`中的`Store`当作购买证明；未上传原始采样，也未把它用于音频生成模型或训练。

逐层原文件路径、SHA-256、原采样率、截取起止秒、放置时刻、增益与淡化全部在[source_recipe.json](source_recipe.json)。原件只读，未复制整库；输出只用于该游戏反馈和本地审阅。通常采用12ms淡入、20ms淡出、交叉叠合、重采样及有限瞬态压缩；没有音乐移调或合成补声。复用的是库内作者拟音，不能标成我们新录。

[项目侧素材索引](../../../Logs/t01-seal-audio-v3-20261125/source-discovery-project.json) · [本地包出处索引](../../../Logs/t01-seal-audio-v3-20261125/source-discovery-audio.json) · [来源与使用边界](../../../Logs/t01-seal-audio-v3-20261125/source-policy.json)。

## 复现与验证边界

```powershell
python -X utf8 ArtSource/Audio/SealConfirmationV3/render_candidates.py
```

脚本读取既有项目原件，生成`Wav/`、试听和逐层来源表，默认不覆盖主工程五个WAV。源缺失时应恢复相同授权原件，不从未知下载链接替换。[基本QA](qa.json)验证48kHz单声道PCM16、首尾归零、无削波、尾部静音、9个源文件哈希不变。V1和V2的WAV、制作代码、试听与历史测试均保留。

本轮的波形、原件来源与运行检查不等于真人盲听验收；未做Quest实机听感、空间声像和耳侧长时间舒适性测试。准、绳仍未标定，声音预览不能当作其识别已经通过。

Unity6000.3.10f1独占物理副本运行原matcher→ForwardSeal→PlaySealConfirmation→SealGlyphPulse。规矩律衡持续持印一次、退出重结第二次，准绳零识别事件、仅直接模板预览。Rapid包含8次matcher确认和4次直接预览，12个独立音源，最多3源同时播放，播放游标均到达clip.samples后自然停止；实际混音峰约−10.89dBFS，零削波。OS退出码0。输入为合成特征与腕Pose注入，不能冒充真实手追踪；原手帧历史回放也不算新采样。[运行结果](../../../Logs/t01-seal-audio-v3-20261125/unity/result.txt) · [实际启动与退出](../../../Logs/t01-seal-audio-v3-20261125/unity/process-exit.json)。
