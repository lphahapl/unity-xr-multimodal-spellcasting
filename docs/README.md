# 文档索引

当前操作、长期设计、外部调研与审阅历史分开维护。项目展示入口为[根 README](../README.md)；所有模型变更遵循[模型交火规则](模型交火/README.md)。

## 当前操作与开发指南

| 要做的事 | 入口 |
|---|---|
| 试玩《考工律度》正式开场、配置教学与首谜 | [首章场景与接线](guides/CHAPTER01_AWAKENING_ZH.md) |
| 接通头显 / 安装环境 / 排障 | [Steam Link](guides/STEAM_LINK_DEVELOPMENT_ZH.md) · [Unity/Quest 环境](guides/QUEST_SETUP.md) · [排障](guides/TRACKING_TROUBLESHOOTING_ZH.md) |
| 开始 RPG 与操作 | [场景、资源和存档](guides/LINEAR_RPG_QUICKSTART_ZH.md) · [战斗/探索移动](guides/ENCOUNTER_LOCOMOTION_ZH.md) · [腕饰与教学卡](guides/WRIST_MENU_INTERACTION_DESIGN_ZH.md) |
| 火球与施法诊断 | [参数/算法](guides/FIREBALL_TUNING_ZH.md) · [施法链路](guides/SKILL_CAST_TRACE_ZH.md) |
| 配置技能与展示 | [配置与扩展](guides/CONFIGURATION_GUIDE_ZH.md) · [雷光剑与可选近战判定](guides/CONFIGURATION_GUIDE_ZH.md#雷光剑与可选近战判定) · [旧四场景](guides/SKILL_SHOWCASES_ZH.md) · [中文 UI](guides/CHINESE_UI_ZH.md) |
| 手势采样与识别 | [实验台](guides/SEAL_LAB.md) · [录制标定](guides/SEAL_LAB_QUICKSTART_ZH.md) · [识别测试](guides/RECOGNITION_TEST_ZH.md) · [官方基础手形](guides/OFFICIAL_HAND_POSES_ZH.md) |
| 制作玩家双手、试戴左腕准望匣 | [成品 Prefab、Blender 源文件、实绘预览与首章安装](guides/PLAYER_HANDS_ZH.md) |
| 勘律使沈商的外观候选 | [全身主设](visuals/20261121_shen_shang_character/01_沈商_全身主设.png) · [服装三视图](visuals/20261121_shen_shang_character/02_沈商_服装三视图.png) · [工坊校尺](visuals/20261121_shen_shang_character/03_沈商_工坊校尺.png) |
| 沈商玩家造型与模型 | [整身 Blender、面部还原对照、T 姿 FBX 与历史模型](guides/SHEN_SHANG_PLAYER_MODEL_ZH.md) |
| 中文口令与 Quest APK | [PC/Quest 离线接入、原生检查、构建与权限重试](guides/SKILL_VOICE_SETUP_ZH.md) · [测试录音](guides/AUDIO_TEST_RECORDING_ZH.md) |
| P1 反馈试炼 | [启动和操作](guides/P1_TRIAL_ZH.md) · [敌人战斗](guides/ENEMY_COMBAT_ZH.md) · [敌人加载](guides/ENEMY_SPAWNING_ZH.md) · [旁白](guides/NARRATION_FLOW_ZH.md) · [吸附工具](guides/SNAP_TOOL_ZH.md) |
| 常态音乐制作与循环验收 | [《一寸山河》v11复现与实测](guides/MUSIC_PRODUCTION_RUNTIME_ZH.md) |
| 检查与验收 | [命令、报告快照与真机边界](guides/VALIDATION_ZH.md) |

当前 RPG 使用战斗头向/手指融合选点、空手胸前快推后撤和探索食指行走／握拳快跑、选点自动行走或传送；火球按头向瞄准，手部动作触发发射。旧四个展示场景维持自己的前推/取消行为。参数以实际场景引用资产为准。

语音状态（2026-11-08）：Android 原生离线后端已随 P1 APK 构建，以独立包名 `com.kaogonglvdu.p1.voicecheck` 安装到 Quest 3；PC 编辑器默认仍走 UDP。独立 .NET 26 项、最终 Unity 132 项、Quest C API 五词 7/7＋14 项哈希、APK 23 项检查通过。用户已进入 P1；截至 520.6 秒现场记录，收到雷光剑 6 次、火球术 4 次，成功召剑 4 次、生成火球 3 次并发射 1 次；首次授权和两次失焦释放/恢复已验。现场取消、权限拒绝/撤回、断网冷启动、噪声/准确率和长时性能仍待验；[语音指南](guides/SKILL_VOICE_SETUP_ZH.md)记录最终 APK 哈希、现场日志和验证命令。

## 架构与设计

- [全局目标模块图、核心类图与资源装配](architecture/TARGET_ARCHITECTURE_20261110_ZH.md)（2026-11-10）。首章技能和敌人池共用加载器，图中列出玩家、输入、施法实例、目标与存档的实际持有关系。

- [全模块 MVC 重构方案与实施进度](architecture/FULL_MODULE_MVC_REFACTOR_PROPOSAL_20261109_ZH.md)涵盖玩家模型、实体反馈、施法和效果生命周期。首章按稳定 ID 恢复和装配的操作见[配置指南](guides/CONFIGURATION_GUIDE_ZH.md#首章技能资源清单)。248 文件清单记录重构前的盘点。 11 月 11 日的[逐项实施记录](architecture/FULL_MODULE_MVC_REFACTOR_PROPOSAL_20261109_ZH.md#12-按实体收拢职责的实施顺序)列出 11 项职责归属、隔离回归结果和头显待验范围。

- [首章外景材质、灯光与模型](guides/CHAPTER01_EXTERIOR_VISUALS_ZH.md)：全景、工官屋舍与山坡、道路和传送门、天仪完整环、LOD与轻量水道；以页内发布状态和实测范围为准。
- [首章外景模型拆分与制作计划](architecture/CHAPTER01_EXTERIOR_ASSET_PLAN_ZH.md)：A01–A08模型、两套地表材质、世界观约束、分工及验收状态。
- [首章六间房逐房间图册](visuals/20261118_chapter01_multiview/逐房间图册.md) 收录同场景实拍、重排机位、四向特写、陈设模型和可拖入的天仪阁资产。

- [青灰薄云天空](guides/THIN_CLOUD_SKY_ZH.md)：独立动态天空材质、中文参数、静态/动态预览与 Quest/PICO/MR 接入边界；保留首章已有天空素材及用户艺术参数，Quest 3含天空的外景图形基线已测，完整玩法、PICO及天空单独成本A/B仍待验证。

- [教学、成长、物品与章节职责](architecture/PROGRESSION_RESPONSIBILITIES_ZH.md)：独立教学目录、技能解锁、物品奖励、商店、效果等级及 v2 存档。

- [章节流程与目标配置](guides/CHAPTER_FLOW_ZH.md)说明单目标、AND / OR 组合、试炼间段入口、清房句和训练场接线。[首章章节模块图与类图](architecture/FULL_MODULE_MVC_REFACTOR_PROPOSAL_20261109_ZH.md#首批实施的章节模块图) · [旁白的监听与重听](guides/NARRATION_FLOW_ZH.md)

- 六工矩印中的规、矩、律已标定，准、绳、衡待采样。左手护盾使用单手衡印。[六种基础手印设计](architecture/SIX_SEAL_DESIGN_ZH.md) · [录制单手衡印](guides/SEAL_LAB_QUICKSTART_ZH.md#录制单手衡印)
- 正式首章与首枚铜叶：[交接入口](architecture/开场教学与首枚铜叶/00_交接说明.md) · [室内教学](architecture/开场教学与首枚铜叶/01_室内教学流程.md) · [试炼室实景与交互](architecture/开场教学与首枚铜叶/08_试炼室制作与交互.md) · [窗庭远山扩建与实景](architecture/开场教学与首枚铜叶/07_校规庭与量水道制作单.md) · [外景路线与失度出图提示词](architecture/开场教学与首枚铜叶/04_室外场景与线性路线.md) · [同心合矩与方圆规印](architecture/开场教学与首枚铜叶/05_首枚铜叶解谜.md) · [外景三大重排方案比选](architecture/开场教学与首枚铜叶/09_外景三大重排方案比选.md)。
- 第二章《断衡悬道》已建[独立 Unity 场景](../Assets/Scene/Chapter02_SuspendedPath.unity)，分区入口及实景截图见[美术场景搭建记录](architecture/第二章_断衡悬道/05_美术场景搭建.md)。白盒和成品交付使用 [V2.4 场景距离规格](architecture/第二章_断衡悬道/04_场景距离规格.md) · [可编辑规格书](architecture/第二章_断衡悬道/断衡悬道_V2.4_场景距离规格.docx) · [衡梁吊索与动线图](architecture/第二章_断衡悬道/断衡悬道_V2.4_衡梁吊索与动线.pdf)。早期参考保存在 [流程与玩法设计](architecture/第二章_断衡悬道/01_第二章流程与玩法设计.md)、[第一幕台本](architecture/第二章_断衡悬道/02_第一幕台本与对白表.csv)、[出图指南](architecture/第二章_断衡悬道/03_第二章关卡动线与出图指南.md) 和 [核心概念图集](visuals/20261121_chapter02_level_flow/README.md)。
- [技能框架](architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) · [扩展接口](architecture/EXTENSION_INTERFACES_ZH.md) · [玩家数据](architecture/PLAYER_DATA_DESIGN_ZH.md) · [线性 RPG 范围](architecture/LINEAR_RPG_DESIGN_ZH.md)。
- [墨规笔试验场操作与画符设计](architecture/GLYPH_DESIGN_ZH.md#运行试验场) 更新于 2026-11-22。独立 GlyphLab 场景已接入主动千钧符和剧情方直之矩，可练习停笔辨符、连线推拉镇石和描符开栅。
- [Quest/PICO 兼容](architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md) · [同房多人](architecture/MULTIPLAYER_LAN_PLAN_ZH.md) · [观众镜头](architecture/CAMERA_PRESENTATION_DESIGN_ZH.md)：保留原设计日期与契约；Quest 文档已标注原生语音后续接入，其余接口/跨平台/联网计划不因此视为已完成。

- [手部与准望匣资产交付要求](architecture/HAND_AND_WRIST_ASSET_DESIGN_ZH.md)：SDK骨架/蒙皮、可见前臂140—160mm建议、一体双绑带组件与真机验收边界；未导入美术手模或实现前臂IK。
- [残机偶模型与机制设计](architecture/MONSTER_DESIGN_CANJIOU_ZH.md)：基础人形小怪（失控工奴）世界观、3D模型规范（栎木青铜/无面单目/外露发条弱点）、VR战斗行为树（快推后撤闪避1.2s蓄力、发条卡死3.5s瘫痪窗口与火核暴击）。
- [掷火残机模型与机制设计](architecture/MONSTER_DESIGN_ZHIHUO_ZH.md)：第二章司衡外库远程高台小怪（高空安防与熔铸机偶）世界观、3D模型规范（饕餮火铳/单目游标镜/背心发条300%暴击弱点/三爪沉箱地锚）、VR战斗机制（1.5s视听蓄力预警、6.0m/s弧线火核、定规障弹反/火球空中对轰与高低立体压制）。
- [掷火残机三视图与图鉴指南](visuals/20261121_zhihuo_enemy_turnaround/README.md)：3D 角色制作标准三视图定稿（Front/Side/Back，1376×768）、各部位骨骼拓扑与自发光通道分配。

## 世界观与专业需求

- [世界观设定](world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md) · [世界观白皮书](world/考工律度_世界观设定白皮书.md)：已完成 V1.6 大圆通修订，正式裁决并统一六工矩手印（规矩律准绳衡）、五正度铜叶（规矩律准绳）、天宪雷度尺（雷光剑）、留音青铜铎（申屠律识冷铸）与亚稳定工坊（定界铜基石）的世界观闭环。
- [音画规范与三版战斗曲方案](architecture/AUDIO_VFX_SPECIFICATION_ZH.md)：方案 A（紧绷机巧型）已接首章/P1，B/C保留候选；记录实际素材与尚未实现的破韧音乐联动。
- [设定与机制全案（Word）](考工律度_世界观设定与机制全案.docx)。
- 美术概念示意（评审用）：[一体佩戴组件与短前臂新图](visuals/20261102_zhunwang_hand_leaf_schematic/07_v3_worn_design_inscriptions.png) · [比例核对与实际出图词](visuals/20261102_zhunwang_hand_leaf_schematic/07_v3_worn_design_generation.txt) · [比例修改前V3备份](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v3_before_proportion_20261102.txt) · [现行 V3.6 共用提示词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts.txt) · [左右手分开·16张单手视图提示词](visuals/20261102_zhunwang_hand_leaf_schematic/08_hand_turnaround_8views_prompts.txt) · [16张单手原图与建模说明](visuals/20261102_single_hand_16views/README.md) · [原图压缩包](visuals/20261102_single_hand_16views.zip) · [原图外形基准](visuals/20261102_zhunwang_hand_leaf_schematic/06_original_shape_reference.png)。V3明确长八角轮廓、中央实体圆度盘、侧面朱砂按扣，以及五种铜叶外露面的小篆感名称/差异纹样；07已另存为带连接耳、双绑带与搭扣的一体组件/腕后150mm模型方案；图中185mm为项目参照，不是Quest统一手尺寸，图示比例与字形仍需建模复核。历史试稿：[V2佩戴尺寸图](visuals/20261102_zhunwang_hand_leaf_schematic/05_v2_worn_dimensions.png) · [该图实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/05_v2_worn_dimensions_generation.txt) · [V2榫卯结构图](visuals/20261102_zhunwang_hand_leaf_schematic/04_v2_tenon_pull_ring.png) · [结构图实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/04_v2_tenon_pull_ring_generation.txt) · [V2修订前完整词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v2_before_inscriptions_20261102.txt)。用户已允许继续保留金属主体80×48×40mm、含环95mm及5.5mm层距建议；不将它们当作含绕臂绑带的整套三维包络或工程验收。首轮历史稿：[佩戴与菜单](visuals/20261102_zhunwang_hand_leaf_schematic/01_zhunwang_worn_menu.png) · [铜叶与导轨](visuals/20261102_zhunwang_hand_leaf_schematic/02_copper_leaf_rail.png) · [手部](visuals/20261102_zhunwang_hand_leaf_schematic/03_hands_reference.png) · [首轮实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v1_generated_20261102.txt)。均非工程验收依据。
- [程序需求](程序需求/考工律度_程序架构与交互底层技术需求规范.docx) · [策划需求](策划需求/考工律度_核心玩法机制与关卡首领数值策划案.docx) · [美术需求](美术需求/考工律度_美术资产与视效制作需求规范.docx) · [音频需求](音频音乐需求/考工律度_音频音效与离线语音工程需求规范.docx) · [跨专业交接](交接文档/考工律度_XR项目研发全景与跨专业交接指南.docx)。这些非 Markdown 资料本次保持原样，涉及变更同样要记录具体位置与内容。

## 调研与参考

- [游戏画符识别调研](research/游戏画符识别_20261122.md) 核对 Arx Libertatis 的方向识别、Spell Caster 的单笔模板和《Magic Cat Academy》的目标提示，并列出墨规笔的重描、补笔与缺边判例。

- [共享工作空间](../SharedWorkSpace/README.md)，含骨骼与动画、策划案、3D 场景模型及白盒编辑、Chrome 检索与本地文件格式。
- [Quest 追踪手与沈商全身动画](research/QUEST_CHARACTER_HANDS_20261121_ZH.md)，含 Mixamo 指骨验收、腕部 IK、第一人称与观众相机接线，以及浏览器骨骼工作台的人物集合、版本与批注。

- [VR 裸手追踪、结印与工具笔尖的稳定性](research/HAND_STABILITY_20261121_ZH.md)，含开发者实装案例、庭院输入接线、历史骨骼录制回放与 Quest 对照方法。

- [PICO 与 Quest 手模变形和遮挡核查](research/PICO_QUEST_HAND_MESH_DEFORMATION_20261127_ZH.md)，含官方手模绑定方式、关节置信度对照和 PICO 现场日志。

- [长短术共用印序时如何确认释放](research/SEAL_SHORT_CAST_20261121_ZH.md)，含《Arx Fatalis》的实际配方、裸手 VR 收招案例和 Quest 对照试验建议。

- [单印确认、充能完成与条件解锁的视听反馈](research/SEAL_MATCH_FEEDBACK_20261121_ZH.md)，含裸手 VR 游戏案例、铜扣与聚气两种成印样片提案。

- [首章院落的 VR 任务引导与试玩设置](research/VR_QUEST_GUIDANCE_20261119_ZH.md) · [折廊光点、铜耳手势的桌面 Play 视频](visuals/20261119_courtyard_guidance/首章_光点寻路与手势示范.mp4)。

- [战斗 VR 的技能列表与冷却显示](research/VR_COMBAT_SKILL_COOLDOWNS_20261119_ZH.md)，含五款游戏的参照及真机对照提案。工程已接入的右手快览、准望匣状态页与跟随开关见[查看当前术法状态](guides/WRIST_MENU_INTERACTION_DESIGN_ZH.md#查看当前术法状态)。

- [技能配方、命中效果与视听表现的职责](research/SKILL_EFFECT_OWNERSHIP_20261109_ZH.md)。对照 Epic 的火球示例和 Unity Boss Room，说明本项目两种护盾配方为何共享效果、收剑后剑波由谁管理，以及技能重构应验证的行为。

- [第二章 40 米高差的 Quest / PICO 性能评估](research/CHAPTER02_VERTICAL_VISIBILITY_QUEST_PICO_20261122_ZH.md)，含断桥、双战区和吊篮的可见范围、平台帧预算与白盒对照方案。
- [第二章实测高程、水力侵蚀与远山分层](research/CHAPTER02_MOUNTAIN_TERRAIN_20261123_ZH.md) · [断衡悬道群山样件与 Unity 对照](visuals/20261123_chapter02_mountain/README.md)。
- [Quest/PICO外景性能与LOD核查（2026-11-09）](research/QUEST_PICO_EXTERIOR_PERFORMANCE_20261109_ZH.md)：官方资料、Android实际配置、PC画面差异及仍需完成的真机测量。
- [首章山松低开销风动方案（2026-11-14）](research/CHAPTER01_PINE_WIND_20261114_ZH.md)：样板树、顶点风动、LOD 与阴影接线及 Quest/PICO 对照验收。
- [首章草地的密度、风幅与三个预制体样板（2026-11-18）](research/CHAPTER01_GRASS_REFERENCES_20261118_ZH.md)，含移动端与 Quest 游戏参照、同机位风动对照和 Unity 重建入口。
- [首章花草树木选材与 Quest 风动](research/CHAPTER01_FOLIAGE_ASSETS_AND_WIND_20261123_ZH.md)，含 13 个 CC0 模型或模型组、实物选材图、现有着色器接法与头显对照测量方法。
- [中国古代毛笔全用途历史资料与实物考证指南（2026-11-21）](research/ANCIENT_CHINESE_BRUSHES_RESEARCH_ZH.md)：涵盖仰韶彩陶、战国楚笔、云梦秦笔、东汉白马作簪笔、唐代正仓院鸡距笔至明清宫廷笔，系统整理工匠界笔/墨规笔、朝堂簪笔、写经笔、题匾抓笔斗笔、御批朱笔等全用途分类与实物馆藏考据，供【墨规笔】建模与世界观设定参考。
- [中国古代毛笔全用途考古实物图录调研报告（附真实文物图片，2026-11-21）](research/中国古代毛笔全用途考古实物图录调研报告.md) · [实物高清图册目录](visuals/20261121_ancient_brushes/)：包含西汉马王堆毛笔与竹笔套（湖南省博）、战国楚笔与竹筒（北京印刷博/长沙博）、汉代刀笔与簪发具组合（湖南省博）、晚明剔红雕漆龙纹毛笔（大都会艺术博）等真实高清出土文物摄影图录。
- [VR 游戏字幕的摆放与阅读](research/VR_CAPTIONS_PLACEMENT_ZH.md)，含已发售游戏、研究边界和首章验收用例。

- [画符/移动来源](research/ROGUELITE_GLYPH_LOCOMOTION_SOURCES_ZH.md) · [设备来源](research/QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md) · [多人来源](research/MULTIPLAYER_SOURCES_ZH.md)。
- [语音方案比较](research/VOICE_GESTURE_RESEARCH_ZH.md) · [玩家音效](research/PLAYER_AUDIO_BASELINE_ZH.md) · [教学 UI](research/TUTORIAL_UI_RESEARCH_ZH.md) · [工具审阅](research/NEUGAMEMRTOOLS_REVIEW_ZH.md) · [历史跨系统审计](research/UI_PLATFORM_MULTIPLAYER_AUDIT_ZH.md)。
- [六工矩印配置参考表](reference/六工矩印_识别特征配置.xlsx)：录制时期参考，运行时 SO 和实际标定优先。`visuals/` 保留现有舒适性示意图。

世界观正文在 [world/](world/) 下直接维护。六工矩印参考表由 `Tools/make_seal_excel.py` 输出到 `docs/reference/`，已调权重的工作簿位于项目根目录。

## 首章山间校院

- [美术资产表与复用审查](美术需求/首章场景美术资产需求与缺口审查.xlsx) 收录首章所需资源、原工程中的对应资产、缺口与模型交付证据。
- [山间校院场景、实拍与流程验证](visuals/20261119_courtyard_scene/场景验收.md) 提供独立 Unity 场景入口、初稿对照、玩家视角和测试记录。
- [七区陈设定稿与物件特写](visuals/20261119_courtyard_furnishing_concepts/七区陈设.md) 收录每间房的定稿、陈设用途与选稿对照。[查看七区定稿总图](visuals/20261119_courtyard_furnishing_concepts/七区陈设定稿.jpg)。

## 模型交接与维护

- [当前功能、195 个脚本的来源和实测结果](research/DISK_RECOVERY_RESULT_20261107_ZH.md) · [工程目录](guides/PROJECT_LAYOUT_ZH.md) · [技能与玩家模块图、输入输出及调用类图](architecture/SKILL_SYSTEM_CLASS_DIAGRAM_ZH.md)。

- [11 月 7 日磁盘回退与恢复缺口核查](research/DISK_ROLLBACK_AUDIT_20261107_ZH.md)：故障前工程与 DS 恢复包的差异、可按哈希找回的旁白源码、可用美术和音频副本，以及仍需重建的场景与引用。

[规则和模板](模型交火/README.md) · [代码审阅交接](模型交火/代码审阅交接.md) · [需求变更交接](模型交火/需求变更交接.md) · [世界观变更交接](模型交火/世界观变更交接.md)。三个交接文件只在末尾追加，历史结论通过新记录纠正。现行指南正常修订，修改/移动/删除的文件逐项写入交接，保持索引与链接同步。
