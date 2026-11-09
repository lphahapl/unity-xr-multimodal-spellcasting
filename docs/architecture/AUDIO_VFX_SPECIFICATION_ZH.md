# 《考工律度》音画规范：可证内容恢复节选

原规范全文未找回。此页从历史工程工具的行号读回留存设定，不是新制定的完整版，也不能代替当前场景验收。

- 残机偶的设计区分巡游、接近、规刃疾劈、失手及发条卡死的反馈；旧稿把卡死写作 3.5 秒弱点窗口。这是设计，当前战斗数值以[敌人组件与配置](../guides/ENEMY_COMBAT_ZH.md)为准。
- 常态探索曲《一寸山河》以真实古筝和笛子为主、轻弦乐铺底；112秒v11已于2026-11-08按原始哈希精确恢复并接回首章/P1，桌面两轮及PCM连续性验收通过。
- 房间战斗曲《断榫破律·残机初遇》的旧交接列为 37.5 秒、5/8 拍、24 小节；其他时长或只有同名的 WAV 不自动作为旧母带。残机偶瘫痪与乐段联动不算已交付。

历史定位：第一账号音频会话 <code>01a19bb1</code>，原 JSONL 第 553、5299 行可见原文行号；第二账号迁移会话 <code>01a199d5</code> 第 332 行可见战斗曲交接。历史改稿的行号会变化，不把不同版本拼成一份伪原稿。当前实现与空缺见[恢复验收](../research/DISK_RECOVERY_RESULT_20261107_ZH.md)。

## 首章与 P1 试玩当前播放的音乐

两场景的探索轨现均绑定《一寸山河》v11，关闭「探索音乐未指定时保持静音」。战斗轨已按用户选择绑定方案 A《紧绷机巧型》（37.5秒），进入遭遇战时混入该轨；临时钟列只保留为空槽时的代码备用。两场景的音乐音量均为 0.28，完整交叉渐变为 1.2 秒。环境声、脚步和旁白有各自的播放通道。[首章场景](../../Assets/Scene/Chapter01_Awakening.unity) · [P1 场景](../../Assets/Scene/KaogongP1Trial.unity) · [音乐源选择](../../Assets/scripts/logic/Rpg/P1AudioManager.cs) · [备用钟列生成](../../Assets/scripts/logic/Rpg/P1FallbackAudio.cs)


### 常态曲v11恢复与衔接

古筝E–G–A引出笛子C，补接40–56秒留白，解除约78秒同音叠加并连接88秒再现。保留古筝/笛子/轻弦乐、明亮安静主题、8秒前奏和−29LUFS；两轮实际播放与捕获比对通过，首章/P1保持0.28音量。前奏随整曲循环重复，主观与头显混播另验。格式为PCM/Streaming、关闭归一化；[复现与证据](../guides/MUSIC_PRODUCTION_RUNTIME_ZH.md)。恢复期空槽与v10副本已由本次替换，历史记录保留。

## 2. 战斗曲方案 A 已接入，B/C 保留候选

应用户对战斗曲“沉稳紧张、杜绝轻佻浮躁、基于实战设计”的需求，已完成三款差异化普通战斗曲母带合成，并实体落盘至 `Assets/Audio/` 与 `docs/audio/`。用户于2026-11-08选择A，首章/P1已绑定，导入为PCM/Streaming、关闭归一化。下表配器与B/C参数沿用历史制作说明，不作为真实乐器录音来源或本轮试听验收：

| 方案 | 风格命名 | 核心乐器与织体 | 节奏/时长/BPM | 声学指标 (RMS/Peak/质心) | 文件内休止与核查状态 | 推荐与适用场景 |
|:---|:---|:---|:---|:---|:---|:---|
| **方案 A (已选用)** | **紧绷机巧型** | 古琴泛音+闷音断奏（Ostinato）、悬疑战鼓心跳重低音、紧绷发条齿轮运转、清越小青铜铎 | 5/8 拍<br>37.5 秒循环<br>BPM 96 | Peak: -8.0 dBFS<br>RMS: -25.55 dBFS<br>质心: 4823.8 Hz<br>低频占 34.0% | 28–31.5秒实测未明显压低；最长全零休止为32.400–32.812秒，约412ms | **首章残机偶遭遇战首选**。沉稳压迫，极具呼吸张力与机械紧张感。 |
| **方案 B** | **兵戈疾进型** | 古琴拂弦刮奏、密集双槌战鼓切分滚奏、编磬重击点金、青铜破空刃鸣 | 5/8 拍<br>36.0 秒循环<br>BPM 100 | Peak: -8.0 dBFS<br>RMS: -26.10 dBFS<br>质心: 5261.2 Hz<br>低频占 36.45% | 旧稿标注27.0–30.5秒骤停，本轮未核查 | **多机偶围攻/开阔中庭战**。节律疾驰如飞，刀光剑影迫在眉睫。 |
| **方案 C** | **威严金石型** | 曾侯乙编钟基石重鸣、中军战鼓厚重步伐、古琴大调正调深沉铺底、大厦将倾之势 | 5/8 拍<br>39.13 秒循环<br>BPM 92 | Peak: -8.0 dBFS<br>RMS: -24.90 dBFS<br>质心: 4693.5 Hz<br>低频占 31.97% | 旧稿标注29.3–32.8秒骤停，本轮未核查 | **精英机偶/古殿深处仪式战**。先秦重器威严庄重，宏大气场。 |

### 2.1 破韧留白属于尚未实现的设计
破韧时压低音乐可突出卡壳声和反击窗口，但当前播放器没有把敌人破韧事件接到音乐增益。音乐在场景启动时开始循环，战斗状态只控制交叉淡化；固定文件时间不能自动对齐战斗事件。

A的实际28–31.5秒RMS为−24.254dBFS，比前两秒高约0.77dB，不支持旧稿“28秒静音”的说法。全曲−22.994LUFS、峰值−8.003dBFS；后段短休止保留原编曲，没有为了接入重写母带。[离线核查](../../Logs/combat-a-integration-20261108/offline-qa.json)。

现有语音避让仍由检测到的语音/旁白触发−8dB衰减，不能保证消除识别误判。B/C留白及真实设备语音混播本轮未测；破韧联动不属于此次A接线的已实现内容。

### 2.2 资产与工程引用
- 方案 A：[Assets/Audio/kaogong_combat_opt_a_taut_clockwork.wav](../../Assets/Audio/kaogong_combat_opt_a_taut_clockwork.wav) · [文档副本](../audio/kaogong_combat_opt_a_taut_clockwork.wav)
- 方案 B：[Assets/Audio/kaogong_combat_opt_b_agile_blade.wav](../../Assets/Audio/kaogong_combat_opt_b_agile_blade.wav) · [文档副本](../audio/kaogong_combat_opt_b_agile_blade.wav)
- 方案 C：[Assets/Audio/kaogong_combat_opt_c_solemn_bronze.wav](../../Assets/Audio/kaogong_combat_opt_c_solemn_bronze.wav) · [文档副本](../audio/kaogong_combat_opt_c_solemn_bronze.wav)
- 探索常态曲：[Assets/Audio/kaogong_yicun_shanhe_ambient_loop.wav](../../Assets/Audio/kaogong_yicun_shanhe_ambient_loop.wav) · [文档副本](../audio/kaogong_yicun_shanhe_ambient_loop.wav)
- 试听播放器 Artifact：`guqin_bgm_audition_player.html`


## 技能音效的当前接线（2026-11-08）

技能音效由效果 SO 引用 `SkillAudioProfile`，由玩家上的 `SkillAudioPlayer` 管理空间播放、变奏和清理；`P1AudioManager` 保留关卡音乐、环境声、脚步和混音协调。火球沿用原三组九条音频，雷光剑已接召剑、挥砍、出波、命中和落雷五组，共 11 条从新导入库加工的副本。配置路径和阶段对应见[技能音频接线](../guides/CONFIGURATION_GUIDE_ZH.md#技能音频接线)。原始导入资源不改写，长持握循环暂不启用；最终听感和头显混音仍需真人验收。
