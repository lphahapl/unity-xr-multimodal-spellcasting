# 六印成印视听 V1

2026-11-25，任务 T01-SIX-SEAL-FEEDBACK-20261125。本轮补齐规、矩、准、绳、衡，全部是可批注首版；律印样板保持原文件。正式资源目录：[SealConfirmation](../../../Assets/Art/VFX/Skills/SealConfirmation)。

声音版本说明：本页的声音设计、试听和Unity证据保留为V1历史记录。用户退回偏电子的V2，随后用真实拟音制作V3；当前为[V3.1按各印特点分配力度](../../Audio/SealConfirmationV3_1/README.md)，不要求低于或等响于律印。[V3](../../Audio/SealConfirmationV3/README.md)与[V2](../../Audio/SealConfirmationV2)保留对照。字影、Prefab、GUID和识别沿用本页版本。V1六个原始WAV保存在[对照归档](../../Audio/SealConfirmationV1/Wav)；用下面的归档复现命令重生成，不覆盖当前游戏音频。

| 印 | 字影 | 确认音 | Prefab | 当前校准 |
|---|---|---|---|---|
| 规 | GuiGlyph.png | GuiChime.wav | GuiSealFlash.prefab | 1，双手 |
| 矩 | JuGlyph.png | JuChime.wav | JuSealFlash.prefab | 1，双手 |
| 律 | LvGlyph.png，原文件 | LvChime.wav，原文件 | LvSealFlash.prefab，原文件 | 1，双手 |
| 准 | ZhunGlyph.png | ZhunChime.wav | ZhunSealFlash.prefab | 0，待真人采样 |
| 绳 | ShengGlyph.png | ShengChime.wav | ShengSealFlash.prefab | 0，待真人采样 |
| 衡 | HengGlyph.png | HengChime.wav | HengSealFlash.prefab | 1，单左手 |

五个新 Prefab 均已绑定各自 SealProfile.confirmationPrefab；不改识别阈值、规则、保持时间、handMode 或 calibrated。所有字影继承原样板 0.8 秒生命周期、0.115→0.205 米扩散、峰值 alpha 0.5、空间音量 0.62、单次播放。纹理为 480×576 RGBA，单 Sprite，PPU 576。规、矩、准、绳沿用共享材质的 0.08 米深度偏移；衡单独使用 HengSealGlyph.mat 的 0.12 米偏移。衡的左腕根位置和逐眼投影轮廓保持原逻辑，只有深度测试值前移。原律印共享材质和 Shader 未改。

## 预览与复现

- [六印 Unity 视听合版](../../../docs/visuals/20261125_six_seal_confirmation/SixSealPreview.mp4)：规、矩、衡、律、准、绳，每印约 4 秒；采用实际 Unity 帧和 AudioRenderer 输出。
- [规矩衡先行视听版](../../../docs/visuals/20261125_six_seal_confirmation/GuiJuHengPreview.mp4)，含修正后的左腕准望匣对照段。
- [六音对比 WAV](../../Audio/SealConfirmationV1/comparison_all.wav)，依次律、规、矩、衡、准、绳，每隔 1.5 秒。音频是原创模态合成，不是真实器乐录音。
- [规矩衡先行对比 WAV](../../Audio/SealConfirmationV1/comparison_first.wav)，依次律、规、矩、衡。
- [实际纹理小尺寸对照](../../../Logs/t01-seal-feedback-20261124/Glyph_Readability.png)，大图和 96/64/48 像素高，原贴图完整 alpha。运行时还会乘以 Prefab 的 alpha 0.5 和淡出包络；该静态图不代替 Unity 实际渲染。
- Unity Play 中右键单份 SealProfile，选择 **NarutoVR → 预览成印反馈**。这是直接模板预览，不会使准、绳获得标定。

在工程根目录执行：

```powershell
python -X utf8 ArtSource/VFX/SealConfirmationV1/build_glyphs.py
python -X utf8 ArtSource/Audio/SealConfirmationV1/reproduce_archived_v1.py
python -X utf8 ArtSource/VFX/SealConfirmationV1/deploy_prefabs.py
python -X utf8 ArtSource/VFX/SealConfirmationV1/verify_assets.py
```

字形由五份同名 SVG 的原创中心线路径构成，路径数据同时保存在 glyph-design.json；渲染器沿用原律印制作脚本的金色、暗边、体积亮部与微光算法。本轮没有使用字体轮廓、外部字帖或图像生成模型，也不将原创篆意造型宣称为考据篆书。修改路径后，运行脚本会同步 SVG 和 PNG；部署脚本保留已存在 GUID，保护律印及识别配置其他字段。

## 验证边界

[asset-checks.json](../../../Logs/t01-seal-feedback-20261124/asset-checks.json) 记录静态资产完整性：五配置只变反馈引用，律印/共享材质/共享 Shader 和首章场景保留，GUID 唯一且引用完整，导入设置与原样板一致，音频短于生命周期，无削波和端点突跳。音频逐项数据见 [qa_all.json](../../Audio/SealConfirmationV1/qa_all.json)；重复生成校验见 [reproducibility_check.json](../../Audio/SealConfirmationV1/reproducibility_check.json)。

Unity 6000.3.10f1 的独立物理工程使用原 SkillMatchEngine → SkillInputController.ForwardSeal → SkillController.PlaySealConfirmation → SealGlyphPulse 链路，规、矩、律、衡均验证持续持印一次、退出无额外事件、重结第二次，实际 AudioSource 正在播放且 Unity AudioRenderer 混音非零。准、绳为零识别事件，只测试直接模板预览。输入来自合成特征和腕部 Pose override，未运行头显追踪；规、矩、律另将历史录制手帧经当前特征提取、评分与 matcher 连续回放 6 秒，各仅一次确认。详见 [Unity 原始结果](../../../Logs/t01-seal-feedback-20261124/unity/result.txt)。

六印最终输出数值复核见 [Unity 混音 QA](../../Audio/SealConfirmationV1/unity-output-qa.json)：按非零 PCM 音段与时间线确认事件对照，检查无重复音头、削波和尾部突断；不冒充真人主观听音。合版仅增加说明字幕，未替换实际音轨。六个独立短片也在同一视频目录，命名为 `{Gui,Ju,Heng,Lv,Zhun,Sheng}SealPreview.mp4`。

衡印原共享 0.08 米深度偏移在测试正视角会让准望匣挡住上部字影。以强字影区域且与“只有腕匣”图完全相同为判据，硬遮挡为 3157/8774 像素（35.98%）。独立材质调整至 0.12 米后，四种合成腕角的该判据均为零，根锚点仍是原左腕；暖金字叠铜色处仍有低对比像素。此结果仅说明测试姿态下硬遮挡已消除，不表示所有真实腕角或头显双眼均已通过。

小尺寸人眼随机识字、声音随机盲听和 Quest 实机仍待用户验收；不能用纹理不同或波形互相关低来替代这些结论。需要重点检查规/衡、律/绳声音混淆，衡印左腕与准望匣的遮挡，以及左右眼下近手深度关系。准、绳完整手形识别和六印过渡混淆尚未完成。
