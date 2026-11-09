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
| 中文口令与 Quest APK | [PC/Quest 离线接入、原生检查、构建与权限重试](guides/SKILL_VOICE_SETUP_ZH.md) · [测试录音](guides/AUDIO_TEST_RECORDING_ZH.md) |
| P1 反馈试炼 | [启动和操作](guides/P1_TRIAL_ZH.md) · [敌人战斗](guides/ENEMY_COMBAT_ZH.md) · [敌人加载](guides/ENEMY_SPAWNING_ZH.md) · [旁白](guides/NARRATION_FLOW_ZH.md) · [吸附工具](guides/SNAP_TOOL_ZH.md) |
| 常态音乐制作与循环验收 | [《一寸山河》v11复现与实测](guides/MUSIC_PRODUCTION_RUNTIME_ZH.md) |
| 检查与验收 | [命令、报告快照与真机边界](guides/VALIDATION_ZH.md) |

当前 RPG 使用战斗头向/手指融合选点、空手胸前快推后撤和探索水平头向行走；火球按头向瞄准，手部动作触发发射。旧四个展示场景维持自己的前推/取消行为。参数以实际场景引用资产为准。

语音状态（2026-11-08）：Android 原生离线后端已随 P1 APK 构建，以独立包名 `com.kaogonglvdu.p1.voicecheck` 安装到 Quest 3；PC 编辑器默认仍走 UDP。独立 .NET 26 项、最终 Unity 132 项、Quest C API 五词 7/7＋14 项哈希、APK 23 项检查通过。用户已进入 P1；截至 520.6 秒现场记录，收到雷光剑 6 次、火球术 4 次，成功召剑 4 次、生成火球 3 次并发射 1 次；首次授权和两次失焦释放/恢复已验。现场取消、权限拒绝/撤回、断网冷启动、噪声/准确率和长时性能仍待验；[语音指南](guides/SKILL_VOICE_SETUP_ZH.md)记录最终 APK 哈希、现场日志和验证命令。

## 架构与设计

- [首章外景材质、灯光与模型](guides/CHAPTER01_EXTERIOR_VISUALS_ZH.md)：全景、工官屋舍与山坡、道路和传送门、天仪完整环、LOD与轻量水道；以页内发布状态和实测范围为准。
- [首章外景模型拆分与制作计划](architecture/CHAPTER01_EXTERIOR_ASSET_PLAN_ZH.md)：A01–A08模型、两套地表材质、世界观约束、分工及验收状态。

- [青灰薄云天空](guides/THIN_CLOUD_SKY_ZH.md)：独立动态天空材质、中文参数、静态/动态预览与 Quest/PICO/MR 接入边界；保留首章已有天空素材及用户艺术参数，Quest 3含天空的外景图形基线已测，完整玩法、PICO及天空单独成本A/B仍待验证。

- [教学、成长、物品与章节职责](architecture/PROGRESSION_RESPONSIBILITIES_ZH.md)：独立教学目录、技能解锁、物品奖励、商店、效果等级及 v2 存档。

- [章节流程接入与首章解耦](guides/CHAPTER_FLOW_ZH.md)：通用章节契约、可组合目标、显式训练场流程、现有场景直接接线及中文注释入口。

- [六种基础手印设计](architecture/SIX_SEAL_DESIGN_ZH.md)：从十二地支筛选子、丑、寅、申、午、戌，提议改称规、矩、律、准、绳、衡；含动作、首章衔接和完整改名范围，运行时尚未迁移。
- 正式首章与首枚铜叶：[交接入口](architecture/开场教学与首枚铜叶/00_交接说明.md) · [室内教学](architecture/开场教学与首枚铜叶/01_室内教学流程.md) · [试炼室实景与交互](architecture/开场教学与首枚铜叶/08_试炼室制作与交互.md) · [窗庭远山扩建与实景](architecture/开场教学与首枚铜叶/07_校规庭与量水道制作单.md) · [外景路线与失度出图提示词](architecture/开场教学与首枚铜叶/04_室外场景与线性路线.md) · [同心合矩与方圆规印](architecture/开场教学与首枚铜叶/05_首枚铜叶解谜.md)。
- [技能框架](architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) · [扩展接口](architecture/EXTENSION_INTERFACES_ZH.md) · [玩家数据](architecture/PLAYER_DATA_DESIGN_ZH.md) · [线性 RPG 范围](architecture/LINEAR_RPG_DESIGN_ZH.md)。
- [画符设计](architecture/GLYPH_DESIGN_ZH.md)：战斗外有效抓笔进入，期间不结印/攻击，P2；运行时尚未实现。
- [Quest/PICO 兼容](architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md) · [同房多人](architecture/MULTIPLAYER_LAN_PLAN_ZH.md) · [观众镜头](architecture/CAMERA_PRESENTATION_DESIGN_ZH.md)：保留原设计日期与契约；Quest 文档已标注原生语音后续接入，其余接口/跨平台/联网计划不因此视为已完成。

- [手部与准望匣资产交付要求](architecture/HAND_AND_WRIST_ASSET_DESIGN_ZH.md)：SDK骨架/蒙皮、可见前臂140—160mm建议、一体双绑带组件与真机验收边界；未导入美术手模或实现前臂IK。
- [残机偶模型与机制设计](architecture/MONSTER_DESIGN_CANJIOU_ZH.md)：基础人形小怪（失控工奴）世界观、3D模型规范（栎木青铜/无面单目/外露发条弱点）、VR战斗行为树（快推后撤闪避1.2s蓄力、发条卡死3.5s瘫痪窗口与火核暴击）。

## 世界观与专业需求

- [世界观设定](world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md) · [世界观白皮书](world/考工律度_世界观设定白皮书.md)：已完成 V1.6 大圆通修订，正式裁决并统一六工矩手印（规矩律准绳衡）、五正度铜叶（规矩律准绳）、天宪雷度尺（雷光剑）、留音青铜铎（申屠律识冷铸）与亚稳定工坊（定界铜基石）的世界观闭环。
- [音画规范与三版战斗曲方案](architecture/AUDIO_VFX_SPECIFICATION_ZH.md)：方案 A（紧绷机巧型）已接首章/P1，B/C保留候选；记录实际素材与尚未实现的破韧音乐联动。
- [设定与机制全案（Word）](考工律度_世界观设定与机制全案.docx)。
- 美术概念示意（评审用）：[一体佩戴组件与短前臂新图](visuals/20261102_zhunwang_hand_leaf_schematic/07_v3_worn_design_inscriptions.png) · [比例核对与实际出图词](visuals/20261102_zhunwang_hand_leaf_schematic/07_v3_worn_design_generation.txt) · [比例修改前V3备份](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v3_before_proportion_20261102.txt) · [现行 V3.6 共用提示词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts.txt) · [左右手分开·16张单手视图提示词](visuals/20261102_zhunwang_hand_leaf_schematic/08_hand_turnaround_8views_prompts.txt) · [16张单手原图与建模说明](visuals/20261102_single_hand_16views/README.md) · [原图压缩包](visuals/20261102_single_hand_16views.zip) · [原图外形基准](visuals/20261102_zhunwang_hand_leaf_schematic/06_original_shape_reference.png)。V3明确长八角轮廓、中央实体圆度盘、侧面朱砂按扣，以及五种铜叶外露面的小篆感名称/差异纹样；07已另存为带连接耳、双绑带与搭扣的一体组件/腕后150mm模型方案；图中185mm为项目参照，不是Quest统一手尺寸，图示比例与字形仍需建模复核。历史试稿：[V2佩戴尺寸图](visuals/20261102_zhunwang_hand_leaf_schematic/05_v2_worn_dimensions.png) · [该图实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/05_v2_worn_dimensions_generation.txt) · [V2榫卯结构图](visuals/20261102_zhunwang_hand_leaf_schematic/04_v2_tenon_pull_ring.png) · [结构图实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/04_v2_tenon_pull_ring_generation.txt) · [V2修订前完整词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v2_before_inscriptions_20261102.txt)。用户已允许继续保留金属主体80×48×40mm、含环95mm及5.5mm层距建议；不将它们当作含绕臂绑带的整套三维包络或工程验收。首轮历史稿：[佩戴与菜单](visuals/20261102_zhunwang_hand_leaf_schematic/01_zhunwang_worn_menu.png) · [铜叶与导轨](visuals/20261102_zhunwang_hand_leaf_schematic/02_copper_leaf_rail.png) · [手部](visuals/20261102_zhunwang_hand_leaf_schematic/03_hands_reference.png) · [首轮实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v1_generated_20261102.txt)。均非工程验收依据。
- [程序需求](程序需求/考工律度_程序架构与交互底层技术需求规范.docx) · [策划需求](策划需求/考工律度_核心玩法机制与关卡首领数值策划案.docx) · [美术需求](美术需求/考工律度_美术资产与视效制作需求规范.docx) · [音频需求](音频音乐需求/考工律度_音频音效与离线语音工程需求规范.docx) · [跨专业交接](交接文档/考工律度_XR项目研发全景与跨专业交接指南.docx)。这些非 Markdown 资料本次保持原样，涉及变更同样要记录具体位置与内容。

## 调研与参考

- [Quest/PICO外景性能与LOD核查（2026-11-09）](research/QUEST_PICO_EXTERIOR_PERFORMANCE_20261109_ZH.md)：官方资料、Android实际配置、PC画面差异及仍需完成的真机测量。
- [VR 游戏字幕的摆放与阅读](research/VR_CAPTIONS_PLACEMENT_ZH.md)，含已发售游戏、研究边界和首章验收用例。

- [画符/移动来源](research/ROGUELITE_GLYPH_LOCOMOTION_SOURCES_ZH.md) · [设备来源](research/QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md) · [多人来源](research/MULTIPLAYER_SOURCES_ZH.md)。
- [语音方案比较](research/VOICE_GESTURE_RESEARCH_ZH.md) · [玩家音效](research/PLAYER_AUDIO_BASELINE_ZH.md) · [教学 UI](research/TUTORIAL_UI_RESEARCH_ZH.md) · [工具审阅](research/NEUGAMEMRTOOLS_REVIEW_ZH.md) · [历史跨系统审计](research/UI_PLATFORM_MULTIPLAYER_AUDIT_ZH.md)。
- [十二印配置参考表](reference/NarutoVR_12Seals_FeatureConfig.xlsx)：录制时期参考，运行时 SO 和实际标定优先。`visuals/` 保留现有舒适性示意图。

现行文档直接维护上述文件。`scratch/build_full_lore_docs.py`、`scratch/build_revised_world_docs.py` 是带内置历史正文的一次性生成稿，仍输出旧根目录 Markdown；`Tools/make_seal_excel.py` 也仍使用旧表格输出位置。它们本次保持原样；复用时先核对现行正文并调整到 `world/` 或 `reference/`，生成后逐项记录差异。

## 模型交接与维护

- [当前功能、195 个脚本的来源和实测结果](research/DISK_RECOVERY_RESULT_20261107_ZH.md) · [工程目录](guides/PROJECT_LAYOUT_ZH.md) · [技能与玩家模块图、输入输出及调用类图](architecture/SKILL_SYSTEM_CLASS_DIAGRAM_ZH.md)。

- [11 月 7 日磁盘回退与恢复缺口核查](research/DISK_ROLLBACK_AUDIT_20261107_ZH.md)：故障前工程与 DS 恢复包的差异、可按哈希找回的旁白源码、可用美术和音频副本，以及仍需重建的场景与引用。

[规则和模板](模型交火/README.md) · [代码审阅交接](模型交火/代码审阅交接.md) · [需求变更交接](模型交火/需求变更交接.md) · [世界观变更交接](模型交火/世界观变更交接.md)。三个交接文件只在末尾追加，历史结论通过新记录纠正。现行指南正常修订，修改/移动/删除的文件逐项写入交接，保持索引与链接同步。
