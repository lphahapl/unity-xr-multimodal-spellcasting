# 六印小篆字源与原轮廓候选 R1

2026-11-25 开始，2026-11-26 00:01 +08:00 接入；T-01；执行为 Codex / GPT-6（具体变体未提供）。状态：**六字经父代理看图确认、独立 Unity 回归后，已替换主工程六张原 PNG，不代表用户最终观感定稿或 Quest 通过**。父代理确认可按官方许可进行保持原轮廓的普通排字，仍不声称获得专属游戏法律批文。五音 V3.1 与律印原声保持。本轮未改变名称、Prefab、既有 GUID、声音、手别或标定。

## 先看

- [六字同屏](Comparison/SixSeal_ChongXi_FirstLook.png)：律、規、矩、準、繩、衡，均来自同一份崇羲篆体。
- [当前字影与候选对照](Comparison/SixSeal_Old_vs_ChongXi.png)：原 Assets 在上，真实字体排字在下，并列 96/64/48 像素高。此为静态检查，不能代表 Quest 双眼可读性。
- [矩／巨／榘关系](Comparison/Ju_JuRadical_JuVariant.png)：保留矩印，另列考据关联，不静默替换。
- [Unity 实际实例同屏](UnityReview/SixSeal_ActualInstances_Labelled.png)、[近腕实际帧合版](UnityReview/SixSeal_ActualUnity_Contact.png)、[实际图像及声音视频](UnityReview/SixSeal_ActualUnity_WithAudio.mp4)：顺序规、矩、衡、律、准、绳，后两项明确标模板。影片只加标签，字形来自真实实例，声音来自 Unity Listener 的实际混音。
- [透明 PNG](Candidates/ChongXi/PNG/) 与 [原轮廓 SVG](Candidates/ChongXi/SVG/)：8 对文件，其中 6 个正式名对应候选、2 个矩字关系参照。全部 480×576；原轮廓只等比缩放、坐标翻转和平移，没有拼字、增删笔画、变形、加粗、描边或美术磨损。
- [Runtime](Runtime/)：六个可供 SpriteFrag 乘色的原 PNG 副本；**每个像素 RGB 均为 255/255/255，alpha 0—255**。黑色只用于对照图展示，透明 PNG 原文件就是白色，Runtime 与候选逐文件字节一致。
- [RuntimeGold](RuntimeGold/)：父代理要求继续原暖金风格，六字普通排字色为 **RGB 246/220/167**，alpha 与白版逐字节一致。选色来自原律印 `alpha>=240` 的 28,166 个像素，取亮度 75 分位最近的真实像素 RGBA 246/220/167/254，仅沿用其 RGB，不把其 alpha 覆盖候选透明度。[采样与六字哈希](RuntimeGold/runtime-gold-manifest.json)。正式核验使用该暖金版。

## 逐字核查

现行命名及实义以 [SIX_SEAL_DESIGN_ZH.md 第 5、13—18 行](../../../docs/architecture/SIX_SEAL_DESIGN_ZH.md) 和六份 `Assets/ScriptableObjects/手印识别配置/*印.asset` 为准。候选的字体与语言映射不会改变配置显示名。

| 正式名称 / 记忆词 | 当前文件及问题 | 本轮候选 | 字源依据与修正建议 |
|---|---|---|---|
| 律印 / 鸣律 | `LvGlyph.png`；原 `D:/Temp/LvConfirm-20261121a/make.py` 明记原创单线路径，以彳和手持笔为意，不是有出处的字体轮廓 | 律 U+5F8B，`uni5F8B` | 用同一崇羲字体的律字；保留彳、聿构造。CNS 也有此码；铜鸣原音不涉及本轮修改。 |
| 规印 / 合规 | `GuiGlyph.png`；V1 脚本写“规”，自绘夫、目及足部，属篆意游戏字 | 規 U+898F，`uni898F` | [中大規条](https://humanum.arts.cuhk.edu.hk/Lexis/lexi-mf/search.php?word=%E8%A6%8F)列夫、見构造；使用本字規而非简体规骨架。 |
| 矩印 / 交矩 | `JuGlyph.png`；V1 自绘矢与现代巨方框；没有原字源 | 矩 U+77E9，`uni77E9` | 崇羲原文件确实收矩；保留矩印。此是该字库的篆体转写候选，不能称它是《说文》矩字头原刻。详见下节。 |
| 准印 / 平准 | `ZhunGlyph.png`；V1 两水短笔与隹的简体准骨架 | 準 U+6E96，`uni6E96` | [中大準条](https://humanum.arts.cuhk.edu.hk/Lexis/lexi-mf/search.php?word=%E6%BA%96)列水与隼声，义为平；使用準，不能将两点水准直接装饰成篆书。 |
| 绳印 / 张绳 | `ShengGlyph.png`；V1 简体纟折线及右侧方形结纹 | 繩 U+7E69，`uni7E69` | [中大繩条](https://humanum.arts.cuhk.edu.hk/Lexis/lexi-mf/search.php?word=%E7%B9%A9)说明糸及蠅省声；使用繩，不保留现代纟骨架。 |
| 衡印 / 镇衡 | `HengGlyph.png`；V1 按现代行、中央格子及下部承托重新绘制 | 衡 U+8861，`uni8861` | [中大衡条](https://humanum.arts.cuhk.edu.hk/Lexis/lexi-mf/search.php?word=%E8%A1%A1)列角、大与行声；候选保留字库原形。仍用当前左手与独立深度，不由字形变更调整手别。 |

V1 的 [README](../SealConfirmationV1/README.md)、`build_glyphs.py` 文件头和 `glyph-design.json` 均明确原创路径。问题是其性质不满足“必须小篆”，不是以前资产自称经过考据。准、绳仍为未标定模板；本轮资产候选不改变 `calibrated: 0`。

## 矩、巨、榘放在同一处的证据

1. **现代正式印名仍为矩印。** `矩印.asset` 的 `confirmationPrefab` 指向 `JuSealFlash.prefab`，候选也保持 `JuGlyph` 文件干。崇羲 `cmap[U+77E9] = uni77E9` 实测存在；并非系统楷书回退或代理自行拼字。
2. [中大《汉语多功能字库》巨条](https://humanum.arts.cuhk.edu.hk/Lexis/lexi-mf/search.php?word=%E5%B7%A8)的《说文》项以“規巨也”释巨，并列榘为或体；形义解释将巨联系到矩尺，说明“规巨”即“规矩”。这里的本义是画方的工具，不是游戏里把矩印解释成“巨大”。
3. 同条列 **榘** 为巨的重文/或体，关联木与矢。崇羲 `cmap[U+6998] = uni6998` 实测存在，已输出独立参照；CNS 文件并无此码，不能以缺字静默补字。
4. [中大矩条](https://humanum.arts.cuhk.edu.hk/Lexis/lexi-mf/search.php?word=%E7%9F%A9)展示金文例及人持曲尺的解释，其小篆图栏为空。**空栏不是“历史上不存在矩的篆写”的证明**；但当前证据也不足以将本轮矩候选冒称为《说文》独立字头摹本。
5. [崇羲官方制作体例](https://xiaoxue.iis.sinica.edu.tw/chongxi/)区分《说文》所收字和采用小篆偏旁转写的未收字。故本轮清楚标“矩：该字库收录的转写篆体”；巨、榘只作旁证。建议父代理审核此路线后继续，不需为追求“全是独立字头”改游戏印名。

## 字库出处、许可和未核项

**选用研究候选：崇羲篆体。** 作者王心怡、谢清俊、庄德明、季旭昇；发布方为中央研究院小学堂。[官方介绍](https://xiaoxue.iis.sinica.edu.tw/chongxi/)、[官方下载页](https://xiaoxue.iis.sinica.edu.tw/chongxi/download.htm)、[许可页](https://xiaoxue.iis.sinica.edu.tw/chongxi/copyright.htm)、[官方排字范例](https://xiaoxue.iis.sinica.edu.tw/chongxi/example.htm)。官方版本页标 2022-06-26 1.00、11596 字；文件内部版本日期为 2022-06-23。网页“字数”、字体 glyph 数和 Unicode 映射数不同，不用数量差异单独断言篡改。

实际下载来自 [RECATOOLS 的公开原文件镜像](https://recatools.com/assets/fonts/chongxi-seal/chongxi_seal.otf)，其 [转换器页面](https://recatools.com/xiaozhuan-converter/?lang=zh-HK)及 [CSS](https://recatools.com/assets/css/oriental-qin.css?v=1786242615)声明所供 OTF 未修改。下载后为 21,817,868 字节；SHA-256：

`b5f52fe62e84b2b18ff4b586d9838d157e05925eca847ecd48c915ba4dcd735a`

**这只是本次下载文件的实测哈希，不是官方公布哈希。** 本机读取小学堂站点返回 IP blocked；未绕过封锁，未取得官方 ZIP 作逐字节比对。Web 读取器可核官方发布/许可页。镜像的“原样”声明和文件元数据相符，但本轮没有独立的官方二进制一致性证明。

父代理提出用 [官方常用字表 PDF](https://xiaoxue.iis.sinica.edu.tw/chongxi/files/chongxi_CommonWords.pdf)只核六字，避免将整包一致性作为唯一手段。此次 Web 打开两次超时，本机该具体 PDF 返回 ASCDC 的 IP 封锁页面，文件未取得。因此未完成 PDF 页码定位、内嵌轮廓或原页图比对；不写成已经比过。若取得官方 PDF，可在保持现有文件不动的情况下续补这一证据。[获取状态](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/official-pdf-attempt.json)。

官方许可 **CC-BY-ND-3.0-TW-or-later**，允许附姓名的重制、散布和传输（含商业利用），禁止改作。官方介绍面向一般使用者并展示排字图片；本轮未找到明确写“游戏贴图”的专门答复。[CC 原条款第 3 条](https://creativecommons.org/licenses/by-nd/3.0/tw/legalcode)允许为其他媒体/格式作技术必要转换。本轮以此为普通排字/图片输出的依据，保留完整未改 OTF、作者、来源和许可，SVG 仅忠实抽出轮廓，PNG 仅正常抗锯齿排字。**父代理在正式接入前已复核官方许可并确认本轮原轮廓普通排字路线；这不是权利人针对本游戏的专属许可批文。** 本轮没有改 OTF、字体子集化、人工修笔、金属做旧、光晕或描边；未来若要改轮廓或做依赖改作的样式，另找允许改作字源或取得授权。

建议随交付保留的署名：

> 崇羲篆體 — 王心怡、謝清俊、莊德明、季旭昇；中央研究院小學堂，https://xiaoxue.iis.sinica.edu.tw/chongxi/ 。CC BY-ND 3.0 TW or later，https://xiaoxue.iis.sinica.edu.tw/chongxi/copyright.htm 。使用原字型排字，未修改字型或笔画轮廓。

**未采用到发布候选：CNS《全字库说文解字》。** `Source/ebas927.ttf` 为官方 [下载页](https://www.cns11643.gov.tw/downloadList.jsp?ID=2&ID2=20)文件，SHA `e8501dc9747190936a222576c9b0b6f918ff335afa5696c3fda3acba1a9574e2`。含律、規、準、繩、衡、巨，缺矩、榘。其[通用授权页](https://www.cns11643.gov.tw/pageView.jsp?ID=59)虽写相关字体，但“授权下载”具体仅列正宋/正楷，未明确涵盖说文字体。本轮将它标为研究参照，不将 OGDL/OFL 通用页面套用成该文件的发布许可证，不输出它的游戏贴图。`cns-license.html` 和 `ogdl-1.0.html` 仅是调查材料。

小学堂[查询字形图片 CC0 声明](https://xiaoxue.iis.sinica.edu.tw/License/License)只覆盖其查询界面所得图片和属性；不能扩大成所有下载字型都 CC0。本轮没有取得六字对应查询图片，也没有借它冲销崇羲 ND 条款。

## 实际工具、文件与验证

- Python 3、fontTools 4.68、Pillow、numpy；`TTFont.getBestCmap` 明确匹配 Unicode，`SVGPathPen` 取真实原轮廓，Pillow FreeType 普通排字。没有图像生成模型，无浏览器 UI、Blender、Unity或全局字体安装。
- [build_candidates.py](build_candidates.py)：只在本目录及专属日志写文件。复现：`python -X utf8 ArtSource/VFX/SealConfirmationXiaozhuanR1/build_candidates.py`。
- [candidate-manifest.json](candidate-manifest.json)：8 字逐项码位、glyph 名、字体边界、唯一等比变换、轮廓路径哈希、SVG/PNG SHA、alpha 边界；6 个当前 PNG 在 `OriginalSnapshots/` 只读复制备查。
- [candidate-checks.json](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/candidate-checks.json)：8 字全部真实收录、480×576、外缘透明；48 项正式成印文件和六份配置哈希前后保持。原音频同样在受保护范围。
- [independent-outline-checks.json](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/independent-outline-checks.json)：第二次独立从原字体提取路径，比对 8 份 SVG 的 path 完全一致；PNG 的尺寸、模式、码位、文件散列和 48 项受保护文件再次检查通过。[Runtime 清单](Runtime/runtime-manifest.json)逐文件记录与候选字节一致、白 RGB 和 alpha 保留。
- 人眼看过初览、矩关系页与旧/新页、Unity 实例同屏及衡带腕匣图，字形未裁边；细笔和密部仍须真实尺度和头显验收。后续追加 Unity 授权的实测见下节；未盲测、未录头显。
- 字源阶段只新增本候选目录与专属 Logs。后续追加授权已替换主项目六张 PNG，并新增同目录 `THIRD_PARTY_GLYPHS.md`；主材质、设计正文、三交接未修改，父代理负责整合时的文档和交接。

## 下步边界

父代理已通过六字方向，矩继续用崇羲原矩字而不换巨；普通排字的许可路线亦已明确。主工程的 Sprite/材质 RGB 都是白色，旧暖金来自纹理，因此不能假定白 PNG 会自动变金。父代理明确选用 RuntimeGold 的普通排字着色，不改 Shader 或 Prefab。授权由本工作子代理使用旧 T-01 隔离工程核验，主交接由父代理完成。准/绳依旧只做模板预览，不声明真人识别通过。官方 PDF 六字比对受下载阻点影响，保持未核明示。

## 本轮 Unity 回归准备

父代理追加授权后使用 `D:/Temp/NarutoVR-T01Seal-20261124`，不启动正式工程；运行前该副本无占用进程、无 UnityLockfile。主工程与其他隔离工程的 Unity 进程不受本轮控制。

[prepare_unity_review.py](prepare_unity_review.py)先备份主/副本六张旧 PNG 到 `Backup/MainV1/`、`Backup/IsolatedV1/`，不覆盖已有备份；把 RuntimeGold 六图复制到副本，保留 meta。原 `T01SealAudioV31Acceptance.cs` 克隆为独立类 `T01SealXiaozhuanAcceptance.cs`，独立 SessionKey 和日志；只跑六个阶段、持印一次/退出无事件/重入一次及直接模板预览，跳过原 Rapid/Stress。最后实例化六个真实 Prefab 做同龄脉冲同屏，而非后期贴字。

运行前 [46 项比对](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/input-chain-comparison.json)确认副本与主工程的实际 matcher、控制器、pulse、六份配置、Prefab、WAV、材质、Shader 与 meta 一致。执行命令与 PID 在 [process.json](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/process.json)，本轮输出独立在 `Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/`。此段是准备事实，结果以该目录 `result.txt` 和最终部署报告为准，不能将启动成功写成播放通过。

## 本轮实测、部署与边界

[实际结果](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/result.txt)和 [原时间线](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/timeline.tsv)记录：规、矩、律、衡各经原 matcher → ForwardSeal → PlaySealConfirmation → SealGlyphPulse，持印仅 1 次，退出无追加，重入后累计 2 事件/2 实例；每次真实 AudioSource 播放。准、绳 `calibrated=false`，0 识别事件，直接 Prefab 预览各一次。历史手帧额外通过当前评分与 6 秒持续持印单确认；此为回放，不是本次真人新采样。

六项实际 Listener PCM 峰值 0.1126—0.3022、削波样本 0，音量及原声音保持。六个阶段共 720 帧原图和各自原始混音 WAV 保存在专属日志。视频由这些帧和这些混音制作，每项 4 秒，只在下方新增说明带，修剪的是音频捕获量化多出的约 10.7 毫秒末端静音；没有用源 WAV 替换实际混音。原始 WAV 不作裁剪，仍可重查。[媒体/声音 QA](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/unity/media-qa.json)。

衡印沿用真实左腕锚点与原 0.12 m 材质深度、实际准望匣 Prefab，仅核一个合成正视腕姿。图像差值代理检查在 `delta>20` 的 6,498 字影像素中找到 2 个弱抗锯齿边缘与仅腕匣近似同色，不能据此证明几何遮挡；更强字影核心的同判据候选为 0。**不声称所有角度或 Quest 双眼无遮挡**。

Unity 6000.3.10f1 本轮测试明确输出完成并请求退出 0，PID 72340 随后不再运行；启动命令未保留原进程句柄获取 OS exit code，故不冒称捕获到 OS 退出码 0。日志有 `UnityEditor.Search.SearchDatabase` 启动索引 `ArgumentOutOfRangeException`，其堆栈无本测试或运行链代码；还有 Package Manager path 警告。六项测试在这些编辑器告警后继续完成，测试没有生成 `failure.txt`，不宣称日志零错误，也不越界修其他编辑器缓存。

[deployment.json](../../../Logs/seal-confirmation-xiaozhuan-r1-20261125/deployment.json)记录主工程六图替换前后 SHA-256，六图均与已测 RuntimeGold 字节一致；余下 42 项受保护文件（包括原 meta、Prefab、WAV 与六份识别配置）不变。新增署名文件 `Assets/Art/VFX/Skills/SealConfirmation/THIRD_PARTY_GLYPHS.md`，Unity 可能自动为该新文档创建 meta；既有 GUID 没有重建。正式场景、主榫、正式 Unity 执行和三交接没有由本子代理改动。
