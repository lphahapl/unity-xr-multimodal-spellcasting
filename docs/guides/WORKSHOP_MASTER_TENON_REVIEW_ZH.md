# 汉白玉校准主榫 · 独立审稿与装配样板

## 正式接入 · 清圭 R03 A

二章工坊取用台与全道具样板共用原交互 Prefab；在 Hierarchy 搜 `清圭` 可以定位模型子物件。玉芯、铜框和金线沿用 R03 A 的三件网格。模型高 350 mm，握点距底端 230 mm，插入端底心与原榫座轴心对齐。手持放大到 1.4 倍，榫座接住时恢复原尺。③工坊取用台的方榫方向随台框，方榫底面高 0.650 m，落在双支脚上。④衡梁中控在另一处，插入深度约 104 mm。

在二章清完屋顶和工坊院内两组守敌后取榫，带回中控，对正方孔后松手。也可在全道具测试场右侧样台直接试手持外观和远抓。[正式美术 Prefab](../../Assets/Art/Props/WorkshopMasterTenon/QingGuiR03A/QingGuiR03A.prefab) · [本轮验收](../../Tools/Validation/Evidence/Chapter02QingGui1127/README.md)

## 审稿记录 · 清圭 R03 分件与暖白玉首样

[R03 独立审稿页](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r03.html)展示 Revision A 的实际三件灰模、11 张 Unity 首样图和 4 秒固定光转动。[R03 结构旋转](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-qinggui-r03a)是普通网页 PBR，与 Unity 玉石材质分开判断。B 用户未认可，保留其[退修与玉石镶口参照](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r02.html#jade-reference)和全部旧批注；用户于 11 月 26 日暂时通过 R03 A，并要求提供 Unity 透光照片和视频。同机位的 6.5 秒视频、正照、侧逆光、背光和临时关闭透光的对照见[透光实拍](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r03.html#backlight)。

本次仅使用[Revision A 候选](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR03/Model/RevisionA/candidate-ready.json)：JadeCore、BronzeFrame、GoldWire 三个独立闭合网格，补全18条金线，源几何40,024三角形。组装灰模和三件拆分由实际模型输出，拆分图不代表已验拆装玩法。原15条金线和有大三角明暗的首拍保留为失败记录，未上本页。

[Unity 媒体合同](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR03/UnityReview/revision-a-first-look-01/media-contract.json)记录每图/片SHA、实际FBX与新玉芯测厚来源、相机/灯光/曝光。B左、R03右是在新一帧里同时拍摄，使用复制的原章日光、天光与Mobile管线，非拼接工作室历史图。正/右/背是本体朝向，逆光诊断另移灯；06为实际孔下铜帽终止边，11仅为原孔几何落座，不宣称抓取或SnapZone完成。

仍待精修：玉体在普通光中偏均匀，孔帽边缘细分与抗锯齿可见，最终细刻、贴图和LOD未完成。玉料系数为视觉近似；原章灯光复制到独立预览，不含完整地形遮挡。未接XR、未做Quest/双眼/性能实测，该次审稿时尚未替换正式章节。

## 前轮：清圭 R02 RevisionB 退修历史与独立回归

[R02 / RevisionB审稿页](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r02.html)已置顶最终Unity正常材质、同侧肩孔持物尺度、原R02/B同光对照和6秒移动光/小角转动。另有0.50备选、实体金丝及铜唇近景，模型三视/高低模/来源与桌面插接证据折叠展示。[B普通PBR旋转](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-qinggui-r02b) · [B网页微透旋转](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-qinggui-r02b-lookdev)。网页材质不是Unity shader。

[B模型](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR02/Model/RevisionB/README.md)于2026-11-26 03:41:42 +08:00冻结，25项核心SHA一致；铜边增加约0.50mm真实台阶并收窄交界，整理铜面起伏，保留方端接口。三LOD源几何15,834/6,650/3,444三角形，外廓约110.028×106.797×350mm。Unity实际前向提交24,672/8,650/4,144，均3 Renderer/3材质，另计阴影、深度与额外相机。极近距离仍可见孔环分段和2K斜窄界采样细毛边。

[Unity最终冻结清单](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR02/UnityReview/delivery-manifest.json)时间为2026-11-26 04:41:21 +08:00。17图与2段实录逐项按清单复制：FinalLook正常材质和6秒片使用最终Soft Medium阴影修正，消除自阴影斜纹并保留金丝真实投影；Images与2.5秒原孔视频保留同一B的完整物理回归原帧，早于最后光偏移调整，分组记录而非同一拍摄时刻。[独立样板与依赖](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR02/UnityReview/README.md)说明拿取、四向插入与退出重插、五次完成且不多发、最大穿入0，以及最终外挡/关光/金属负例和三个Shader的Vulkan编译结果。

样板依赖独立透明接收阴影Renderer与真实反射环境；不是Prefab拖入生产Mobile Renderer即可等效。Meta/XR抓取入口未接，Quest实机/双眼/GPU性能和交叉透明排序未测；静态手只供尺度。RevisionB尚未获用户审美通过、未替换正式章节，本物件与③取用台/双销的配合由独立任务负责。原R02中间实拍、两版模型文件、R01页面与历史批注继续保留；原MaterialAB/Polish使用R01几何，未冒充B。

## 前轮：清圭 R01 Unity桌面实孔通过，新实模待批注

2026-11-26，[清圭 R01 审稿页](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r01.html)已置顶实际低模与2K PBR的同尺度正、右、背三视，附近景、高低模、线框与LOD对照；[旋转入口](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-qinggui-r01)使用普通PBR GLB。总高350mm、低模外廓约111×106mm，方插端106×106×105mm，握位设计85×46mm；高模1,207,404三角形，LOD0/1/2为4,916/1,800/600，1材质、1UV、2K九图。九图包含原始通道及不同打包格式，非游戏同时采样九张。[冻结模型说明](../../ArtSource/Props/WorkshopMasterTenon/QingGuiR01/Model/README.md)和[冻结清单](../../Logs/workshop-master-tenon-qinggui-r01-20261125/model/candidate-ready.json)保留实测与哈希。

Cycles源材质侧光、普通5W背光、25W压力背光和关灯图分别标注；源SSS/透光、GLB普通PBR与Unity实时厚度近似不能互相替代。C4和AI参考下移保留，AI侧视方端比例仍非量规。Unity实际几何正面为+Z、右侧为−X、背面为−Z；MaterialModel组的Front/Side/Back表示正照/侧照/背照，不能当作几何三视。其中01/02/03是最终完整材质（PBR+厚度近似），04仅透光分量，不是关闭透光前后的A/B。最终纯摄影图已同步到审稿页，保留真实几何朝向；前45°近手与坐实图明确使用C4长金线正面。[原孔插接录像](http://127.0.0.1:4190/plans/media/workshop-master-tenon-qinggui-r01/QingGuiR01_Insertion_Unity.mp4)使用原回归帧和当时机位，不当作新正面摄影角度。

独立Unity四个正交方向加取回重插共5次通过，15段碰撞体覆盖2,848个导入顶点；原孔在6个深度双向测宽约153mm，逐帧实际碰撞与独立120mm保守量规最大穿入均为0。输入为桌面合成控制，未接Meta/XR抓取。握点按导入截面修正为Unity局部(+1.49998,230,−0.20707)mm：旧建议X符号相反，本次调整3.00712mm；独立Take/MoveHeld通过，非真人握持舒适度结论。[插接实测](../../Logs/workshop-master-tenon-qinggui-r01-20261125/unity/result.txt) · [握点依据](../../Logs/workshop-master-tenon-qinggui-r01-20261125/unity/orientation-supplement/grip-imported-section.json) · [实际拿取接口](../../Logs/workshop-master-tenon-qinggui-r01-20261125/unity/orientation-supplement/grip-interface-result.txt)。

最终暖光厚度近似不使用自发光，关灯或外物挡住入射传光时增量为0；真实模型金属区域残余约2.98×10⁻⁶，接近零。[Android/Vulkan Shader编译](../../Logs/workshop-master-tenon-qinggui-r01-20261125/unity/android-vulkan-bounded/result.txt)通过，仅为编译验证，不是Quest运行。顶扣、Meta/XR和Quest观感/性能仍另验。本清圭模型与③取用台/双销配合另验，由独立任务负责。C4批准状态保持，新实模待用户批注，尚未正式替换章节资产。

## 前轮：C4外观已批准，进入清圭 R01 建模

2026-11-25，用户明确“就这样开始建模 先做三视图 要求建模细致 有玉石光泽，透光 青铜和金线有质感”。以C4为已批准的外观依据，进入[清圭 R01 独立建模审稿](http://127.0.0.1:4190/plans/workshop-master-tenon-qinggui-r01.html)。当前页面先建立分区，依次承接AI造型参考、实际模型三视与近手、高低模、Blender材质、Unity实时近似及④实孔验证；尚未发布本版实模或材质验证结果。AI参考图仅用于造型沟通，不作为量规。C4及所有历史稿和批注保留，③取用台另项推进，④接口保持；旧R01–R03工程记录不代表新稿通过。

## 前轮：C4清圭金线加强待批注

2026-11-25，用户追加“增加金线”。[C4金线加强稿与实际提示词](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptC4/README.md)沿用C3轮廓与温玉、青铜材质，明显增加肩部和玉面疏朗金线、铜边金线及下端呼应，置于[对照工作台](http://127.0.0.1:4190/plans/workshop-master-tenon-bronze-concepts.html#tenon)最前。C4待批注，C3保留对照，C2仅方向获认可；没有新模型或插接/头显验证。③取用台B1另审，④接口保持。下文保留各轮历史状态。

## 前轮：C2方向认可，C3铜金精修待批注

2026-11-25，用户明确“c2方向对了用青铜和金线再做美化”。[C3精修稿与实际提示词](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptC3/README.md)沿C2轮廓和玉面留白，增加青铜叠边、内侧浅槽、肩部金线及下端窄连续浅纹。C2原图和历史批注保留；C3置于[对照工作台](http://127.0.0.1:4190/plans/workshop-master-tenon-bronze-concepts.html#tenon)最前。C2获认可的是方向，C3细节待批注，未建模或进行新插接/头显测试。③取用台B1另审、④接口保持。

## 前轮：主榫 C2 清圭与C1新参考

2026-11-25，用户要求新青铜器与古代信物方向；已核大都会汉铜虎符和商至西周虎形车辖的馆藏原图及Public Domain标记，另参考国博鄂君启错金青铜节的文字介绍。C1伏虎符节出图后，用户要求“再来，有点丑 来点文艺气息”。据此新增C2：温玉握面、柔和斜肩、单侧古铜骨线与细金线，减少兽形及粗重分节。清圭为方案名，不改正式物品名称。

[C2当前图稿与提示词](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptC2/README.md) · [C1新参考与历史稿](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptC1/README.md) · [当前图稿工作台](http://127.0.0.1:4190/plans/workshop-master-tenon-bronze-concepts.html#tenon)。C1审美未通过，C2待批注；下部方端仍以106×106×105mm为后续重建约束，多视图不能按像素量取。未制作新实模、未验插接或头显，不继承旧R03结果。③取用台仍是B1独立候选，④接口保持。

## 前轮：青铜主榫 B1 / B2 与③取用台 B1

2026-11-25，按用户“你继续，给我设计稿”分开出两件概念；随后依“主榫在设计一版”新增主榫B2。主榫B1为单鸟纹与玉质拇指位，B2为双夔合抱与窄玉芯准线；③取用台B1采用方彝式分层和低位双销结构提案。主榫与台可分别选择，④中控保持另一处原接口。

[图稿对照工作台](http://127.0.0.1:4190/plans/workshop-master-tenon-bronze-concepts.html) · [概念图、提示词与边界](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptB2/README.md) · [B2主榫](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptB2/Tenon_B2.png) · [③取用台B1](../../ArtSource/Props/WorkshopMasterTenon/BronzeConceptB1/Pedestal_B1.png)。全部是图稿候选，尚无本方向的建模、插拔或头显通过结论。

旧方向R03已留存[模型](../../ArtSource/Props/WorkshopMasterTenon/R03/README.md)与[独立Unity记录](../../ArtSource/Props/WorkshopMasterTenon/R03/UnityReview/README.md)：握位74mm、真实拇指凹面2mm，底端0–127mm保留R02，原孔5次桌面插接通过；现行顶扣兼容与新增肩销小样分别验证。此为既有工程记录，审美未通过，也不能用来证明新青铜图稿已适配。R01/R02原件和批注保留，下文为历史候选。

## R02 · 近手精修候选

2026-11-25。按A1方向另做R02，R01原件、场景包和批注保留。已去除R01的连续竖向流痕及两圈显眼铜带，改为细微等向云絮玉料、窄连续云雷纹带、贯通准线及长短刻度。上段握体和下段插入端通过轮廓区分，仅底端保留极窄暗铜嵌线。第一轮预览准线不够清楚、冠部/肩部高光分层，经加宽准线及修正曲面和法线后重新烘焙；未用更换灯光掩盖。

外廓仍为120×120×350mm。插接端106mm方、105mm长；105–127mm为肩部过渡，上段最大120mm。底端原点和原落座坐标保持；肩起点仅高于样板孔口约0.94mm。**肩线不是跨孔机械止挡**：120mm外廓本身小于当前约153mm孔口，落座深度由专用插接姿态限定。106mm方端在45°时可能穿过孔口，方位拒绝需与实际几何碰撞区分。

[R02源与说明](../../ArtSource/Props/WorkshopMasterTenon/R02/README.md) · [正45实模](../../ArtSource/Props/WorkshopMasterTenon/R02/Previews/01_Front45.png) · [中性侧光](../../ArtSource/Props/WorkshopMasterTenon/R02/Previews/07_SideLightJade.png) · [冻结数据](../../Logs/workshop-master-tenon-r02-20261125/model/candidate-ready.json)。高模361856三角，LOD0/1/2为952/320/112，单材质2K PBR，真实高到低烘焙。高模/三低模QA无错误无告警；FBX冷回读通过，GLB的72个同位置顶点为UV/切线导出分缝。当前运行时仍为不透明PBR玉料近似。

[R02审稿工作台](http://127.0.0.1:4190/plans/workshop-master-tenon-r02.html) · [旋转模型和R02独立批注](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-r02) · [独立样板包与操作](../../ArtSource/Props/WorkshopMasterTenon/R02/UnityReview/README.md) · [Unity实际插入视频](../../ArtSource/Props/WorkshopMasterTenon/R02/UnityReview/Insertion_ActualUnity_R02.mp4)。37个工作台附件与冻结源一致；R01版本及批注保留，页面可同时对照A1、R01和R02。浏览器已实际查看图像、R02三维材质载入及其版本批注。

R02独立Unity6000.3.10f1/D3D11回归通过，PID40132于15:39:43+08:00退出0。[实际结果](../../Logs/workshop-master-tenon-r02-20261125/unity/result.txt)：拿取、落台、重拿、四向及再次插入共五次完成，无重复；正确路径的10段外包接触盒与独立120mm禁用量规均0穿透。551个导入顶点均被接触盒覆盖；圆角外侧有保守空间，不是精确网格碰撞。45°实际接触体检出0，按准线方位规则拒绝；120mm量规45°正控制8.353mm，实际过深/偏心50mm正控制4.975/26.5mm。原孔6056三角和原落座位置沿用。R01的183项基线文件保持不变。包内44项均位于独立R02目录，仍依赖本项目运行时与URP/Input System。

**尚未绑定Meta/XR抓取入口**，未做头显实测，用户审美仍待批注。双销继续保留为原独立提案，本轮未将A1台座当作已制作资产。model-report中的兼容字段`nominal_socket_metres=.153`使用当前孔型测量值，原资料名义值仍为146mm。

## R01 · 首版归档

2026-11-25。按用户本次“参照原设建模”授权完成独立初稿，温白玉身、半哑光、回折云雷纹、度量刻线与暗铜嵌箍。尚待用户审美批注，未替换正式章节或放入玩家杂物集合。

[原设、近景、侧背与实插工作台](http://127.0.0.1:4190/plans/workshop-master-tenon-r01.html) · [旋转模型与逐版批注](http://127.0.0.1:4190/rig/?collection=workshop-master-tenon&model=master-tenon-r01) · [模型源与烘焙说明](../../ArtSource/Props/WorkshopMasterTenon/R01/README.md) · [独立Unity样板操作](../../ArtSource/Props/WorkshopMasterTenon/R01/UnityReview/README.md)。

## 原设与新增提案

V2.1原设及动线修订明确：③工坊清场取得“汉白玉校准主榫”，回④中控插入唯一方形空榫眼；缺榫朱砂红报错，插到位后才转冰蓝并点亮两侧分控。原图“阴刻”二字是工艺占位，按原稿要求换成云雷纹和度量刻线。图2和塔脚玉榫图均属于④，不能当作③取用座近景定稿。

V2.1双战区尺寸原图中，主榫为0.12×0.12×0.35m，③展示座为0.85×0.85×0.65m、周边站立区直径3m。V2.4改为院口到③取榫站位8m，持榫返④合计34.4m；旧图30m院内距离不沿用。[尺寸原图](../../ArtSource/Props/WorkshopMasterTenon/Reference/V21-Zone3-tenon-dimensions.png) · [文档/图像哈希](../../ArtSource/Props/WorkshopMasterTenon/Reference/source-manifest.json) · [动线修订摘录](../../ArtSource/Props/WorkshopMasterTenon/Reference/V21-revised-excerpts.json)。段落按XML全部w:p计数时工艺修正为P57；空段计数差异不作为不同设定。

“检修时主榫存于校准座，两枚铜销防止战斗中误启巨衡；最后守备停机后销退开，露出握位”是本轮新增设计提案，原稿没有锁定原因。独立小样以座上两枚实体挡销的闭合/退开状态展示，不改主榫外廓；当前本体没有凸肩锁槽，因此表述为压挡顶部接触边。没有接清场事件、实际抓取锁定或正式中控启机。新增机制须另行审定。

[A1玉料刻纹与双销候选](../visuals/20261125_ch02_calibration_tenon/汉白玉校准主榫_玉料刻纹与双销_A1.png) 已与R01实际模型放在同一审稿页。A1为暖白云絮玉料、连续窄纹带、长准线、下端收颈及低位双销；冻结R01为齐平双铜带、独立回纹、等截面方柱与顶缘压挡。A1没有尺寸标注，透视不覆盖实测配合。R01已保留，后续R02按本轮批注另行精修；当前页和模型仍明确标记R01待批注。

## 资产与实际验证

| 项目 | 交付与结果 |
|---|---|
| 尺寸与原点 | 120×120×350mm，底端中心；Unity +Y竖直。铜件无外凸肩，全截面不超120mm |
| 高低模 | 高模343616三角；LOD0/1/2为720/256/96三角；每档1网格、1材质、1UV |
| 贴图 | 2K BaseColor/Normal/AO/Roughness/Metallic，另有ORM、Unity MetallicSmoothness；Cycles真实高到低烘焙 |
| 模型QA | 高模与三低模均通过；FBX/GLB空场景回读一致。GLB有62个UV/切线导出拆分同位置顶点，源网格无重复，已接受 |
| 玉材质边界 | 高模有轻微次表面散射；运行时是不透明PBR表面近似，未制作Quest玉石透光Shader |
| 原插孔 | 保留正式TenonConsole/ConsoleSocket几何与相对位置。资料名义146mm，当前实际网格轴向喉口扫描约153mm，未扩大 |
| 样板装配 | 主榫底端y=0.923940m，高于该投影范围台身最高点4mm；插深约104.06mm，是本轮样板选择，原设未规定 |
| 桌面拿插 | 原GameItemBase接口拿起、移动、松手物理落台、重拿；原SnapZone派生准入与完成。四正交向+重插共五次完成，无重复事件 |
| 碰撞 | 正确路径逐帧真实MeshCollider与6056原三角OBB-SAT检查均零穿透；错转45°/过深正控制分别检出约8.353/8mm |
| 身份边界 | 样板注册与专用插槽仅接该主榫，不加入通用杂物列表；五现有工具带槽按原尺寸规则拒收，未给全项目新增purpose规则 |

主榫Prefab保留握点、插入端、BoxCollider、Rigidbody、SnapItem及GameItemBase，方便后续系统接入。**当前仅键盘/面板驱动真实底层接口，尚未绑定或验证Meta/XR头显抓取入口。** 近手图中的静态手模只是1∶1尺寸参照。Quest真实拿取、握姿舒适度、双眼材质和正式清场/中控联调另验。

独立Unity工程：`D:/Temp/NarutoVR-T01Seal-20261124`，场景 `Assets/Review/WorkshopMasterTenonR01/WorkshopMasterTenonR01_Lab.unity`。也可将[独立unitypackage](../../ArtSource/Props/WorkshopMasterTenon/R01/UnityReview/WorkshopMasterTenonR01_UnityReview.unitypackage)导入本项目的另一副本；它依赖项目当前运行时代码/URP/Input System，并非通用空白项目包。包中参考中控GUID独立重映射。`1/2/3/4/5/6/R`操作详见样板README。

[Play结果](../../Logs/workshop-master-tenon-r01-20261125/unity/result.txt) · [原生退出码](../../Logs/workshop-master-tenon-r01-20261125/unity/process-exit.json) · [实际孔道扫描](../../Logs/workshop-master-tenon-r01-20261125/unity/console-fit-analysis.json)。Unity6000.3.10f1/D3D11独立运行通过。早先单一PhysX薄壁检测漏报与样板误开Rigidbody插值导致的1.418mm落座回退均保留失败记录，修正的是样板配置/验收工具，没有修改正式运行时。

[实际插接短视频](../../ArtSource/Props/WorkshopMasterTenon/R01/UnityReview/Insertion_ActualUnity.mp4) · [浏览器同页对照截图](../../Logs/workshop-master-tenon-r01-20261125/workbench-A1-actual.png)。工作台附件当前仅支持模型和图片，视频保留在本地交付中。模型侧冻结README中的“待Unity验证”是当时阶段状态，以本页及独立样板最终结果为准；源README与冻结包未回写。
