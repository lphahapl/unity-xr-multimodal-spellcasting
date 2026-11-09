# 《一寸山河》制作与循环验收

2026-11-08恢复并重新验收。正式文件为[112秒v11](../../Assets/Audio/kaogong_yicun_shanhe_ambient_loop.wav)，与故障前v11逐字节一致，SHA256：`4d893f2962f3fa5e4e29e2679aff39d90a5ca2005f3022972660daf1549314a9`。原录音、切点与必要源码均已恢复；没有换用合成乐器。

## 当前音乐与工程引用

60BPM、4/4、28小节。古筝、竹笛与轻中提琴，约−28.991 LUFS，峰值−12.823dBFS。保留8秒前奏和笛子完整主题两次陈述。40–56秒保留弦乐退出，但以弱古筝连接过长空隙；53.35/54.25/55.15秒的E–G–A引向56.25秒笛子C；去掉约78秒的同音重叠，G–A引向88.25秒再现，笛子再入起音较轻。

首章和P1的`explorationMusic`已指向正式文件，音乐音量仍为0.28；探索不再使用恢复期的空槽静音。正式文件保留当前GUID `6b5271a650bc6bd42bba69f84849a867`，导入为立体声44.1kHz PCM/Streaming、关闭归一化、后台加载。8秒前奏属于整曲，每轮都会重播。战斗音乐现已按用户选择接入A，见下节。

## 战斗曲A接入（2026-11-08）

用户选择[方案A](../../Assets/Audio/kaogong_combat_opt_a_taut_clockwork.wav)，首章和P1的`combatMusic`均已绑定GUID `7da01d4da958b704ea5438a032e74fe3`。母带未改：37.5秒、44.1kHz双声道PCM16、−22.994LUFS；SHA256 `2d4a31fa3ed9d57199dffdf491ee82662e92f125205d6476855066106175f8d5`。导入改为PCM/Streaming、关闭归一化、后台加载。音量0.28与1.2秒交叉淡化保持原配置；两条音乐从场景启动开始循环，进入战斗时混入A，并非每次从头播放。

Unity6000.3.10f1逐场景读回引用通过，仅替换两场景各一条战斗引用，导航和探索引用保留。已有交叉淡化/语音避让13项检查通过；独立空场景从零播放A两轮，捕获76.757秒、3598个音频回调、两次自然回绕，PCM比对未见额外静音或接缝时间跳变。[接入验收](../../Logs/combat-a-integration-20261108/integration-summary.json) · [波形比对](../../Logs/combat-a-integration-20261108/capture-qa.json)。未测正式场景完整遭遇流程、Quest及物理混播听感。

A的28–31.5秒并无历史说明所称静音，文件最长全零休止为32.400–32.812秒的约412ms；此次保留原编曲。破韧音乐联动尚未实现，详见[音画规范](../architecture/AUDIO_VFX_SPECIFICATION_ZH.md#21-破韧留白属于尚未实现的设计)。B/C保留候选，未接线。

## 复现

原Python3.13解释器已丢失；旧venv保留，新隔离解释器为`C:/Users/鲁迅/.codex/tools/music-production/.venv-rebuild-311/Scripts/python.exe`（Python3.11.9）。Pedalboard0.9.26、NumPy2.4.6、SciPy1.17.1、SoundFile0.14.0、pyloudnorm0.2.0、Mido1.3.3，安装使用本机配置的PyPI镜像。实际依赖见[freeze](../../Logs/yicun-shanhe-continuity-v11-rebuild-20261108/freeze-311.txt)。

```powershell
& 'C:/Users/鲁迅/.codex/tools/music-production/.venv-rebuild-311/Scripts/python.exe' -X utf8 'scratch/render_yicun_shanhe_continuity_v11.py'
```

渲染写到`Logs/yicun-shanhe-continuity-v11-rebuild-20261108/`，不自动发布Assets；重跑前先另存需要保留的同名产物。v7只恢复了v11所需音源/音符函数和旧v6乐谱依赖，不承诺旧v7独立入口可运行。原音源哈希、干轨重构与周期混响10项检查见[重建验收](../../Logs/yicun-shanhe-continuity-v11-rebuild-20261108/rebuild-qa.json)。谱面与奏法以脚本/JSON为准；MIDI不包含全部实录采样奏法。

## Unity完整测试

在已打开本项目的编辑器中运行，每次用新的ID：

```powershell
python -X utf8 scratch/run_bgm_unity_probe.py run --id my-bgm-check --sha256 4d893f2962f3fa5e4e29e2679aff39d90a5ca2005f3022972660daf1549314a9 --duration 112 --full-loops 2 --wait-seconds 10
python -X utf8 scratch/run_bgm_unity_probe.py poll --id my-bgm-check --wait-seconds 30
```

CLI等待超时只返回pending，不停止Unity。默认锁定程序集重载，独立空启动场景保留原编辑场景及未保存状态；测试从零自然播放两轮，不跳播。结束后需同时检查`passed`、`sceneRestored`和`locksReleased`。本次接线使用的一次性Editor工具已归档到`Logs/bgm-resume-20261108/`；普通run不保存场景。接线时发现共用导航数据的两个场景不可同时加载保存，P1导航引用已逐字段还原，未留下延迟接线请求。

本次两轮捕获226.645秒、10624个音频回调，通过[运行与PCM验收](../../Logs/bgm-resume-20261108/unity-playback-summary.json)。3秒时主动申请脚本重载，保护开启后仍完成两轮；关闭保护的负例准确记录重载中断。原测试的单次DSP快照、静态截止时间跨重载归零都不能作为音频掉线证据。

捕获校验工具为[verify_bgm_capture.py](../../scratch/verify_bgm_capture.py)：全采样率相关搜索、独立时间锚、两个接缝、额外静音与回调序列。正例及两种50ms掉音负例见[合成回归](../../Logs/bgm-capture-verifier-20261108/test-01/validation.json)。重采样后不要求两轮逐样本相等；保留差异指标，并核时间连续性，不以更改对齐掩盖真实插入静音。

这是桌面音频线程和正式资源接线验收，不是物理设备录音；Quest、旁白/SFX混播和主观听感尚未验收。故障前第三次提前退出的发起者仍无法追溯，没有仅凭XR初始化报错作归因。11月5日旧日志已丢失，不作为本次通过证据。
