# 沈商全身玩家模型

## 工作台存档

打开沈商集合，默认显示 **08 · 自然神态 I**，即修改外眼角和下颌之前保存的整身。在顶部「版本」下拉框切换，复制当前地址可让其他人直接打开同一版。[进入沈商工作台](http://127.0.0.1:4190/rig/?collection=shen)

| Blender 存档 | 工作台里能看到的内容 |
|---|---|
| [原生首稿](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-native) | 首稿头颈及自身的眉发、发髻、木簪和短须 |
| [01 轮廓初修](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-01) | 同一检查点的头颈、眼球和配套眉发，保留早期素色皮肤 |
| [02 五官比例中间稿](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-02) | 整身 A 姿，包含同期单独保存的头与发型 |
| [03 眼鼻修订](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-03) | 头颈、眼球、睫毛、睑缘和内眦 |
| [04 眼皮与嘴唇](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-04) | 头颈与眼部，含当期唇纹与下睑细节 |
| [05 衣领与面颊调整前 F](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-05) | 开始下一轮面颊、眉眼和衣领调整之前的整身 |
| [06 面颊与衣领 G](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-06) | 修改额头和后颈之前的整身 |
| [07 额头、面颊与颈部 H2](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-07) | 额头、面颊、颈部与领根贴合后的整身 |
| [08 自然神态 I](http://127.0.0.1:4190/rig/?collection=shen&model=shen-archive-08) | 默认审阅版，整身 A 姿 |
| [眼皮与下颌修订](http://127.0.0.1:4190/rig/?collection=shen&model=shen-eyelids-jaw) · [头身比例修订](http://127.0.0.1:4190/rig/?collection=shen&model=shen-proportions) | 08 之后的两份独立整身 |
| [面部对稿 R1](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r1) | 从眼皮与下颌修订稿继续制作的整身候选，对照设定调整眼睑、颧颊与下颌轮廓 |
| [面部对稿 R2](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r2) | 补了额前碎发与短须。两侧眉中段的毛根被皮肤遮住，保留为修眉前的对照。 |
| [面部对稿 R3](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r3) | 恢复连续的浓眉，重做上眼窝与颧下转折。Gemini 认可断眉已修复，仍指出眉眼紧绷、额前发束齐整。 |
| [面部对稿 R4](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r4) | 眼裂宽高、颧点、发际按三视图配准。外审仍指出鼻梁橘斑和眉头紧张，工作台另修正了眼球颜色导出。 |
| [面部对稿 R5](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r5) | 把参考发束和多视肌理转入真实模型，鼻梁色差改善。双眉的根部和尾部过实，侧面眉弓前凸，此版留作对照。 |
| [面部对稿 R6](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r6) | 眉座、眼眦深度与瞳色对齐。Gemini 认为单看脸已具备很强的同一人辨识度，仍建议改前发际和颧颊阴影。 |
| [面部对稿 R7](http://127.0.0.1:4190/rig/?collection=shen&model=shen-face-match-r7) | 额前散发按原设重做，保留清瘦脸型并柔化颧下转折。Gemini 通过整张脸和含发型、颈肩的外观候选。 |

02 单独保存的头部 3 件与眉发 56 件，与同目录整身中的部件逐点一致。H2 主工程与 07、自然神态研究源与 08 的 Blender 文件分别具有相同的 SHA-256，每组共用一个工作台入口。

早期全身制作路线也可按制作节点回看。

| 路线 | 工作台版本 |
|---|---|
| 单视图生成与本地绑定 | [单图原始高模](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-single-high) · [本地绑定整身与腕匣](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-local-rig) |
| 三视图生成与第一次 Adobe 绑定 | [三视图原始高模](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-multi-high) · [首次上传的静态网格](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-first-input) · [首次回件，41 骨](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-first-return) |
| 换用张开的 V2 双手并再次绑定 | [第二次上传的静态网格](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-open-input) · [第二次回件，65 骨](http://127.0.0.1:4190/rig/?collection=shen&model=shen-old-full-return) |

旧版 Mixamo 链接沿用同一份 65 骨 FBX，并保留早期预览用的统一贴图。第二次回件的历史条目按身体与双手的材质槽展示。[旧版 Mixamo 入口](http://127.0.0.1:4190/rig/?collection=shen&model=shen)

第一人称双手保留独立版本，可分别查看掌侧、指环和 OpenXR 骨骼。[左手 V2](http://127.0.0.1:4190/rig/?collection=shen&model=hand-left-v2) · [右手 V2](http://127.0.0.1:4190/rig/?collection=shen&model=hand-right-v2) · [左手 V3](http://127.0.0.1:4190/rig/?collection=shen&model=hand-left-v3) · [右手 V3](http://127.0.0.1:4190/rig/?collection=shen&model=hand-right-v3)

历史归档的 23 项各有工作台实载截图。随后单独加入的 R1 可从上表打开。[历史版本轮览](../visuals/20261122_shen_shang_workbench_archive/BrowserVersions.jpg) · [逐版文件、时间和验证记录](../visuals/20261122_shen_shang_workbench_archive/ArchiveAudit.json)

## 逐轮面部审阅

R7 为通过整体审阅的候选。Gemini 在双瞳对齐正脸、35° 转面与纯侧脸中都辨认为同一人物。设定的沉静眼神、额角碎发、颧颊至下颌的自然转折均已在整身模型上实现。作者可在工作台决定是否定稿。 [整头三视图](../visuals/20261123_shen_shang_holistic/R7_Whole.jpg) · [细节对照](../visuals/20261123_shen_shang_holistic/R7_Detail.jpg) · [可编辑工程](../../ArtSource/Characters/ShenShangRebuild/PortraitStudies/09_FaceReferenceMatch_R7/ShenShang_FaceMatch_R7.blend) · [Gemini 复审原文](../visuals/20261123_shen_shang_holistic/R7_Gemini.json)

工作台 R7 和[轻量整身模型](../visuals/20261123_shen_shang_holistic/R7_DisplayModel.zip)均已修复放大时额前的极淡 V 线。Gemini 收到校正版后维持原审结论。[正脸同尺度对照](../visuals/20261123_shen_shang_holistic/R7_Portrait.jpg) · [显示校正实测](../visuals/20261123_shen_shang_holistic/R7_DisplayHairline.jpg)

## 面部对稿 R1

对照图从上至下为正面、35° 与左侧，从左至右为设定、眼皮与下颌稿、R1。观察中段面颊过渡到下颌的宽度、上睑包覆和嘴部侧面轮廓。整身 Blender 源可继续编辑。[三角度同标尺对照](../visuals/20261123_shen_shang_face_match_r1/FaceCompare_Registered.jpg) · [可编辑工程](../../ArtSource/Characters/ShenShangRebuild/PortraitStudies/03_FaceReferenceMatch_R1/ShenShang_FaceMatch_R1.blend) · [工作台与模型验收](../visuals/20261123_shen_shang_face_match_r1/Review.json)

Gemini 收到了完整 R1 压缩包，会话反馈只能读取 ZIP 元数据，美术意见依据原设和已配准的三角度图。相较 J 稿，它建议保留下颌与咬肌的收束，继续核对眉弓至上睑的走势、35° 颧下至口角的转面，以及侧面的鼻尖、嘴唇和下巴。[Gemini 审稿会话](https://gemini.google.com/app/6a49eff0f573b63d)

主审从对照图确认了下颌收形的改善，也看到眉弓与上睑仍有差距。面颊后续以保住原设的饱满承托为约束，鼻尖前突量和唇厚留待同标尺侧面配准后判断。R1 按这次送审的形态存档。网格、贴图和工作台的技术结果来自本机冷开检查。

## 整身比例审稿

把定稿 A 姿与模型按发髻顶、鞋底对齐时，两者下巴、肩线与腰带落在相近高度。本次收窄颧颊、下颌两侧和外衣肋腰，降低胸前领口。整个模型从鞋底到发髻约 1.739 米。工作台可在「全身 · 头身比例修订」与「全身 · 眼皮与下颌修订」之间切换。

[打开比例修订版工作台](http://127.0.0.1:4190/rig/?collection=shen&model=shen-proportions) · [定稿与前后版本等高对照](../visuals/20261122_shen_shang_proportions/FrontComparison.jpg) · [整身三视图](../../SharedWorkSpace/assets/shen-proportions/Turnaround.jpg) · [可编辑 Blender 工程](../../ArtSource/Characters/ShenShangRebuild/ProportionStudies/01_HeadAndTorso/ShenShang_Proportions.blend)

## 眼皮与下颌线审稿

打开[工作台里的整身 A 姿模型](http://127.0.0.1:4190/rig/?collection=shen&model=shen-eyelids-jaw)，拖动空白处可环绕观察。点击「全身三视图」查看同一 Blender 工程等比例渲染的[正面、左侧、背面原图](../../SharedWorkSpace/assets/shen-eyelids-jaw/Turnaround.jpg)。这个工作台版本用于查看静态外观。

[自然度版完整存档](../../ArtSource/Characters/ShenShangRebuild/Checkpoints/08_NaturalRest_I/ShenShang_NaturalRest_I.blend)保存上轮用户所见的整身。[新版 Blender 工程](../../ArtSource/Characters/ShenShangRebuild/PortraitStudies/02_EyelidsAndJaw/ShenShang_EyelidsAndJaw.blend)单独可编辑。眼皮在外眦更早合拢，角后的窄沟接回皮肤，上睑覆盖和下睑承托参照原稿。下颌体到耳下升支的转折重新塑形，耳前肤色中原有的局部深斑随之减弱。

[用户圈选的外眼角特写](../visuals/20261122_shen_shang_eyelids_jaw/OuterCorner.jpg) · [双眼与眼皮](../visuals/20261122_shen_shang_eyelids_jaw/Eyelids.jpg) · [35° 与正侧面下颌线](../visuals/20261122_shen_shang_eyelids_jaw/Jaw.jpg) · [完整面部对照](../visuals/20261122_shen_shang_eyelids_jaw/Portrait.jpg) · [检查记录](../visuals/20261122_shen_shang_eyelids_jaw/Review.json)。

## 面部自然度审稿

[H2 完整存档](../../ArtSource/Characters/ShenShangRebuild/Checkpoints/07_ForeheadCheekNeck_H2/ShenShang_Rebuilt_H2.blend)是用户要求保存的额、颊、颈版本。[独立自然度新稿](../../ArtSource/Characters/ShenShangRebuild/PortraitStudies/01_NaturalRest/ShenShang_NaturalRest.blend)保留同一人物外轮廓，调整了上睑、眼角和鼻唇，重梳眉毛，在上唇与下巴加入可编辑的短胡茬。皮肤底色、粗糙度及细纹随部位变化。新稿默认相机对准 35° 面部，按 F12 可渲染。

[35° 和正面同光对照](../visuals/20261122_shen_shang_natural_face/Portrait.jpg) · [另一组窗光下的近景](../visuals/20261122_shen_shang_natural_face/Window.jpg) · [Blender 检查记录](../visuals/20261122_shen_shang_natural_face/Review.json)。以下整身制作源与无骨 T 姿对应 H2。

## 当前 Blender 重建审阅版

[沈商整身 A 姿工程](../../ArtSource/Characters/ShenShangRebuild/ShenShang_Rebuilt.blend)包含可编辑的头脸、眉发、衣装、双手及独立的左腕准望匣。依照用户圈出的转面调整了远侧颊线、眉上到发际的额面，以及下颌至前后颈根的过渡。后领、内领和上衣肩背共同贴合新的颈根。头部的面颊、额头和脖子可分别用形态键调节。

[设定、修订前和修订后的 35° 与侧面对照](../visuals/20261122_shen_shang_forehead_cheek_neck/Portrait.jpg) · [远侧半脸放大](../visuals/20261122_shen_shang_forehead_cheek_neck/FarCheek.jpg) · [正面](../visuals/20261122_shen_shang_forehead_cheek_neck/Front.jpg) · [整身回读](../visuals/20261122_shen_shang_forehead_cheek_neck/ColdOpen.json) · [轮廓和衣领复核](../visuals/20261122_shen_shang_forehead_cheek_neck/SurfaceQA.json)。对照中两次模型实渲使用相同的正交相机、灯光和裁切。35° 设定稿按两眼位置水平配准。

可打开[本轮修订前的整身](../../ArtSource/Characters/ShenShangRebuild/Checkpoints/06_BeforeForeheadAndNeck/ShenShang_Rebuilt_G.blend)，或回看上一轮的[眉眼与衣领对照](../visuals/20261122_shen_shang_cheek_collar/EyeAndCollar.jpg)与[更早的整身源](../../ArtSource/Characters/ShenShangRebuild/Checkpoints/05_BeforeCheekAndCollar/ShenShang_Rebuilt_F.blend)。头脸采用 MakeHuman HM08 的 CC0 解剖拓扑并在 Blender 中雕形。[头部比例与测量](../../ArtSource/Characters/ShenShangRebuild/Head/Head_SourceAndMeasurements.json)

双手沿用定稿 V2，每手 26 根追踪骨骼，准望匣挂在左腕。[无骨骼 T 姿 FBX](../../ArtSource/Characters/ShenShangRebuild/Export/ShenShang_TPose.fbx)供后续 Mixamo 标点。[FBX 本地回读](../../ArtSource/Characters/ShenShangRebuild/Export/TPose_QA.json)

## 沈商重建用三视图

本轮以原全身主设、工坊校尺近景和定稿双手为依据，重画同一人物的[全身 A 姿三视图](../visuals/20261122_shen_shang_turnaround/Body_APose.png)、[自然站姿服装图](../visuals/20261122_shen_shang_turnaround/Body.png)和[头脸近景](../visuals/20261122_shen_shang_turnaround/Head.png)。头脸图依次为正面、左侧和四分之三。用户已确认这组三视图用于重建，指环位置以[定稿手部](../visuals/protagonist_hands_model_sheet.jpg)为准。

打开 [Blender 正交底图](../../ArtSource/Characters/ShenShangRebuild/ShenShang_OrthographicReferences.blend)，场景选择器可切换全身和头脸。小键盘 1 看正面，3 看角色左侧，Ctrl + 1 看全身背面。全身从鞋底到发髻按 1.74 米配准。文件内嵌五张参考底图，用来比对重建中的轮廓。

## 历史原生头脸首审

上一版头脸以原全身主设和服装三视图为参照，从 Blender 新建颅骨、颈部和五官。工坊近景用于核对眉毛、颧面和皮肤质感。打开[头脸审阅场景](../../ArtSource/Characters/ShenShangNative/ShenShang_HeadReview.blend)可查看独立的头脸与梳发集合。正面和四分之三使用同一光照和正交画幅，另有一张左侧图用来核对眉弓、鼻根和下颌的轮廓。

[原设定与首稿对照](../visuals/20261121_shen_shang_native_head/DesignComparison.jpg) · [正面](../visuals/20261121_shen_shang_native_head/Front.png) · [四分之三](../visuals/20261121_shen_shang_native_head/ThreeQuarter.png) · [左侧](../visuals/20261121_shen_shang_native_head/LeftProfile.png)

用户审阅后指出头脸还原度不足，要求先重做三视图，再重建模型。此首稿存档供对照。以下两版为先前已交付的历史角色。

## 混元三视图与 Mixamo 版本

在 Unity 中把 [ShenShangMixamoPlayer.prefab](../../Assets/Art/Characters/ShenShangMixamo/ShenShangMixamoPlayer.prefab) 拖入场景。要播动作库里的动作，把 Humanoid 动画放进 Animator Controller，再将 Controller 赋给角色的 Animator。需要在其他工程使用骨架时，可以从 [Adobe 原始 T 姿势 FBX](../../ArtSource/Characters/ShenShang/MultiView/ShenShang_Mixamo_Final.fbx) 开始，Unity 工程中的 Prefab 已经装配了项目材质。

混元 3.1 使用设定稿中分开的正面、左侧和背面各一张，共生成一次。第一次 Mixamo 回件每手只有拇指和食指。第二次上传时使用张开双臂的静态网格，将定稿 V2 手形接到袖口并张开五指。Adobe 重新完成了从躯干、双腕到十指的绑定。[三视图生成记录](../../ArtSource/Characters/ShenShang/MultiView/production.json) · [Mixamo 标点](../visuals/20261121_shen_shang_mixamo/MixamoMarkers.jpg)

最终 Adobe FBX 有 65 个关节。Unity 映射了 52 个 Humanoid 槽位，左右各 15 个指节。十指分别弯曲时，手部网格的最大位移为 5.0 至 13.34 厘米，腕外 25 厘米区域在测试精度下保持静止。项目跑步动作完整播放了 0.733 秒。模型共 33,094 个三角面。单个蒙皮渲染器有三个材质槽，分别是身体、左手和右手。头发、衣袖和准望匣都属于身体网格。第一人称视角如需单独隐藏头部或腕匣，还须在显示层按区域处理。

新版修过眼部颜色与法线。正面和转面都能看到虹膜、上眼睑以及鼻梁处的连续高光。T 姿势时宽袖张开，放臂后收回。跑步大跨步时前下摆仍会拉伸。桌面测试使用独立 Unity 6000.3.10f1 工程与移动 URP。要对接 Quest 的握拳、结印或手腕目标，可以直接读取验收导出的腕骨局部旋转、各指节层级和绑定矩阵。[眼部修整对照](../visuals/20261121_shen_shang_mixamo/FaceComparison.jpg) · [Unity 跑步预览](../visuals/20261121_shen_shang_mixamo/UnityRun.png) · [手腕和指骨数据](../visuals/20261121_shen_shang_mixamo/UnityHandRig.json) · [逐指实测](../visuals/20261121_shen_shang_mixamo/UnityAcceptance.txt)

## Blender 绑定版本

把 [ShenShangPlayer.prefab](../../Assets/Art/Characters/ShenShang/ShenShangPlayer.prefab) 从 Project 拖入 Hierarchy，脚底就是放置基准。全身高 1.74 米，四枚指环随手指弯曲，准望匣随左前臂移动。要试自己的动作，可把带 Humanoid 动画的 Animator Controller 拖到角色的 Animator 上。[静止姿态](../visuals/20261121_shen_shang_player/UnityNeutral.png) · [跑步姿态](../visuals/20261121_shen_shang_player/UnityRun.png)

## 眼部修订

沿用沈商参考图，局部重绘眉眼，把瞳孔、眼睑和眉形对齐。对照图左右是修改前和修改后，上下为同灯光下的正面和约 32° 侧面。最终 Prefab 的近景使用移动端配置的 2K 贴图。眼睛为贴图表现，近看仍有低模轮廓。[修前修后](../visuals/20261121_shen_shang_player/FaceComparison.png) · [Unity 正面](../visuals/20261121_shen_shang_player/UnityFaceFront.png) · [Unity 侧面](../visuals/20261121_shen_shang_player/UnityFace.png)

## 骨骼和实测范围

骨架在 Blender 中完成绑定，源文件包含姿态测试。Unity 6000.3.10f1 隔离工程以最终资产验出有效 Humanoid Avatar，映射 52 个槽位，两手各有 15 根指骨。十指逐根弯曲通过，项目跑步动画重定向后左右脚位移分别为 0.336 米和 0.409 米。静止、抬腕、压腕三个姿态共检查 18 个截面，袖口与准望匣腕带最小净隙 0.63 毫米。指环与皮肤接合处有 17 对被遮住的背向面。[动作测试视频](../visuals/20261121_shen_shang_player/Blender_RigCheck.mp4) · [骨骼验收](../visuals/20261121_shen_shang_player/UnityAcceptance.txt) · [跑步实测](../visuals/20261121_shen_shang_player/UnityRetarget.txt) · [腕带实测](../visuals/20261121_shen_shang_player/UnityWristFit.txt)

## Quest 手部映射进度

Quest 的腕部和指节位姿可以通过 XR Hands 驱动角色。项目已有手部采样入口，首章继续使用定稿的第一人称 V2 双手。全身 Prefab 目前支持 Humanoid 动画；接入实时跟手还需校准 XR 关节和角色骨骼的静置方向，用腕部目标驱动手臂 IK，再用头显实测握拳、结印和抬腕。手部数据本身不提供肩肘位姿。[项目采样入口](../../Assets/scripts/logic/SealLab/SealXRHandsSource.cs) · [Unity 手部关节数据](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/manual/hand-data/xr-hand-data-model.html)

[Blender 源文件](../../ArtSource/Characters/ShenShang/ShenShang.blend) · [美术资源表](../../ArtSource/Characters/ShenShang/asset-register.csv)
