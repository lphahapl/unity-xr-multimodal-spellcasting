# 文档索引

当前操作、长期设计、外部调研与审阅历史分开维护。项目展示入口为[根 README](../README.md)；所有模型变更遵循[模型交火规则](模型交火/README.md)。

## 当前操作与开发指南

| 要做的事 | 入口 |
|---|---|
| 接通头显 / 安装环境 / 排障 | [Steam Link](guides/STEAM_LINK_DEVELOPMENT_ZH.md) · [Unity/Quest 环境](guides/QUEST_SETUP.md) · [排障](guides/TRACKING_TROUBLESHOOTING_ZH.md) |
| 开始 RPG 与操作 | [场景、资源和存档](guides/LINEAR_RPG_QUICKSTART_ZH.md) · [战斗/探索移动](guides/ENCOUNTER_LOCOMOTION_ZH.md) · [腕饰与教学卡](guides/WRIST_MENU_INTERACTION_DESIGN_ZH.md) |
| 火球与施法诊断 | [参数/算法](guides/FIREBALL_TUNING_ZH.md) · [施法链路](guides/SKILL_CAST_TRACE_ZH.md) |
| 配置技能与展示 | [配置与扩展](guides/CONFIGURATION_GUIDE_ZH.md) · [旧四场景](guides/SKILL_SHOWCASES_ZH.md) · [中文 UI](guides/CHINESE_UI_ZH.md) |
| 手势采样与识别 | [实验台](guides/SEAL_LAB.md) · [录制标定](guides/SEAL_LAB_QUICKSTART_ZH.md) · [识别测试](guides/RECOGNITION_TEST_ZH.md) · [官方基础手形](guides/OFFICIAL_HAND_POSES_ZH.md) |
| 中文口令 | [接入配置](guides/SKILL_VOICE_SETUP_ZH.md) · [测试录音](guides/AUDIO_TEST_RECORDING_ZH.md) |
| P1 反馈试炼 | [启动和操作](guides/P1_TRIAL_ZH.md) · [敌人战斗](guides/ENEMY_COMBAT_ZH.md) · [敌人加载](guides/ENEMY_SPAWNING_ZH.md) · [旁白](guides/NARRATION_FLOW_ZH.md) · [吸附工具](guides/SNAP_TOOL_ZH.md) |
| 检查与验收 | [命令、报告快照与真机边界](guides/VALIDATION_ZH.md) |

当前 RPG 使用战斗头向/手指融合选点、空手胸前快推后撤和探索水平头向行走；火球按头向瞄准，手部动作触发发射。旧四个展示场景维持自己的前推/取消行为。参数以实际场景引用资产为准。

## 架构与设计

- [技能框架](architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) · [扩展接口](architecture/EXTENSION_INTERFACES_ZH.md) · [玩家数据](architecture/PLAYER_DATA_DESIGN_ZH.md) · [线性 RPG 范围](architecture/LINEAR_RPG_DESIGN_ZH.md)。
- [画符设计](architecture/GLYPH_DESIGN_ZH.md)：战斗外有效抓笔进入，期间不结印/攻击，P2；运行时尚未实现。
- [Quest/PICO 兼容](architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md) · [同房多人](architecture/MULTIPLAYER_LAN_PLAN_ZH.md) · [观众镜头](architecture/CAMERA_PRESENTATION_DESIGN_ZH.md)：设计契约，不是跨设备/联网已完成声明。

- [手部与准望匣资产交付要求](architecture/HAND_AND_WRIST_ASSET_DESIGN_ZH.md)：SDK骨架/蒙皮、可见前臂140—160mm建议、一体双绑带组件与真机验收边界；未导入美术手模或实现前臂IK。
- [残机偶模型与机制设计](architecture/MONSTER_DESIGN_CANJIOU_ZH.md)：基础人形小怪（失控工奴）世界观、3D模型规范（栎木青铜/无面单目/外露发条弱点）、VR战斗行为树（快推后撤闪避1.2s蓄力、发条卡死3.5s瘫痪窗口与火核暴击）。

## 世界观与专业需求

- [世界观设定](world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md) · [世界观白皮书](world/考工律度_世界观设定白皮书.md)。二者作为现有设定来源保留，后续差异须在世界观交接中裁决；本次没有擅自统一设定。
- [设定与机制全案（Word）](考工律度_世界观设定与机制全案.docx)。
- 美术概念示意（评审用）：[一体佩戴组件与短前臂新图](visuals/20261102_zhunwang_hand_leaf_schematic/07_v3_worn_design_inscriptions.png) · [比例核对与实际出图词](visuals/20261102_zhunwang_hand_leaf_schematic/07_v3_worn_design_generation.txt) · [比例修改前V3备份](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v3_before_proportion_20261102.txt) · [现行 V3.6 共用提示词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts.txt) · [左右手分开·16张单手视图提示词](visuals/20261102_zhunwang_hand_leaf_schematic/08_hand_turnaround_8views_prompts.txt) · [16张单手原图与建模说明](visuals/20261102_single_hand_16views/README.md) · [原图压缩包](visuals/20261102_single_hand_16views.zip) · [原图外形基准](visuals/20261102_zhunwang_hand_leaf_schematic/06_original_shape_reference.png)。V3明确长八角轮廓、中央实体圆度盘、侧面朱砂按扣，以及五种铜叶外露面的小篆感名称/差异纹样；07已另存为带连接耳、双绑带与搭扣的一体组件/腕后150mm模型方案；图中185mm为项目参照，不是Quest统一手尺寸，图示比例与字形仍需建模复核。历史试稿：[V2佩戴尺寸图](visuals/20261102_zhunwang_hand_leaf_schematic/05_v2_worn_dimensions.png) · [该图实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/05_v2_worn_dimensions_generation.txt) · [V2榫卯结构图](visuals/20261102_zhunwang_hand_leaf_schematic/04_v2_tenon_pull_ring.png) · [结构图实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/04_v2_tenon_pull_ring_generation.txt) · [V2修订前完整词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v2_before_inscriptions_20261102.txt)。用户已允许继续保留金属主体80×48×40mm、含环95mm及5.5mm层距建议；不将它们当作含绕臂绑带的整套三维包络或工程验收。首轮历史稿：[佩戴与菜单](visuals/20261102_zhunwang_hand_leaf_schematic/01_zhunwang_worn_menu.png) · [铜叶与导轨](visuals/20261102_zhunwang_hand_leaf_schematic/02_copper_leaf_rail.png) · [手部](visuals/20261102_zhunwang_hand_leaf_schematic/03_hands_reference.png) · [首轮实际提示词](visuals/20261102_zhunwang_hand_leaf_schematic/prompts_v1_generated_20261102.txt)。均非工程验收依据。
- [程序需求](程序需求/考工律度_程序架构与交互底层技术需求规范.docx) · [策划需求](策划需求/考工律度_核心玩法机制与关卡首领数值策划案.docx) · [美术需求](美术需求/考工律度_美术资产与视效制作需求规范.docx) · [音频需求](音频音乐需求/考工律度_音频音效与离线语音工程需求规范.docx) · [跨专业交接](交接文档/考工律度_XR项目研发全景与跨专业交接指南.docx)。这些非 Markdown 资料本次保持原样，涉及变更同样要记录具体位置与内容。

## 调研与参考

- [画符/移动来源](research/ROGUELITE_GLYPH_LOCOMOTION_SOURCES_ZH.md) · [设备来源](research/QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md) · [多人来源](research/MULTIPLAYER_SOURCES_ZH.md)。
- [语音方案比较](research/VOICE_GESTURE_RESEARCH_ZH.md) · [玩家音效](research/PLAYER_AUDIO_BASELINE_ZH.md) · [教学 UI](research/TUTORIAL_UI_RESEARCH_ZH.md) · [工具审阅](research/NEUGAMEMRTOOLS_REVIEW_ZH.md) · [历史跨系统审计](research/UI_PLATFORM_MULTIPLAYER_AUDIT_ZH.md)。
- [十二印配置参考表](reference/NarutoVR_12Seals_FeatureConfig.xlsx)：录制时期参考，运行时 SO 和实际标定优先。`visuals/` 保留现有舒适性示意图。

现行文档直接维护上述文件。`scratch/build_full_lore_docs.py`、`scratch/build_revised_world_docs.py` 是带内置历史正文的一次性生成稿，仍输出旧根目录 Markdown；`Tools/make_seal_excel.py` 也仍使用旧表格输出位置。它们本次保持原样；复用时先核对现行正文并调整到 `world/` 或 `reference/`，生成后逐项记录差异。

## 模型交接与维护

- [当前功能、195 个脚本的来源和实测结果](research/DISK_RECOVERY_RESULT_20261107_ZH.md) · [工程目录](guides/PROJECT_LAYOUT_ZH.md) · [技能类图](architecture/SKILL_SYSTEM_CLASS_DIAGRAM_ZH.md)。

- [11 月 7 日磁盘回退与恢复缺口核查](research/DISK_ROLLBACK_AUDIT_20261107_ZH.md)：故障前工程与 DS 恢复包的差异、可按哈希找回的旁白源码、可用美术和音频副本，以及仍需重建的场景与引用。

[规则和模板](模型交火/README.md) · [代码审阅交接](模型交火/代码审阅交接.md) · [需求变更交接](模型交火/需求变更交接.md) · [世界观变更交接](模型交火/世界观变更交接.md)。三个交接文件只在末尾追加，历史结论通过新记录纠正。现行指南正常修订，修改/移动/删除的文件逐项写入交接，保持索引与链接同步。
