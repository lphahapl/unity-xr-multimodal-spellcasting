from pathlib import Path
from datetime import datetime, timezone, timedelta
import hashlib
import json
import re
import shutil
import subprocess
from urllib.parse import unquote, urlsplit
import posixpath

ROOT = Path(r'D:\UnityProjects\NarutoVR').resolve()
DOCS = ROOT / 'docs'
NOW = datetime.now(timezone(timedelta(hours=8))).isoformat(timespec='seconds')
TASK = 'DOCS-' + datetime.now().strftime('%Y%m%d-%H%M%S')
BACKUP = ROOT / 'Logs' / TASK
MODEL = 'GPT-6 / Codex（运行时未提供具体子版本）'
original = {p.relative_to(ROOT).as_posix(): p.read_bytes() for p in DOCS.rglob('*.md')}
original['README.md'] = (ROOT / 'README.md').read_bytes()
texts = {p: b.decode('utf-8-sig').replace('\r\n', '\n') for p, b in original.items()}

def safe(path):
    target = (ROOT / path).resolve()
    if not target.is_relative_to(ROOT):
        raise RuntimeError(f'Outside workspace: {target}')
    return target

def sha(data):
    return hashlib.sha256(data).hexdigest()

groups = {
    'guides': '''ENCOUNTER_LOCOMOTION_ZH FIREBALL_TUNING_ZH SKILL_CAST_TRACE_ZH CONFIGURATION_GUIDE_ZH LINEAR_RPG_QUICKSTART_ZH OFFICIAL_HAND_POSES_ZH STEAM_LINK_DEVELOPMENT_ZH QUEST_SETUP SEAL_LAB_QUICKSTART_ZH SEAL_LAB RECOGNITION_TEST_ZH SKILL_VOICE_SETUP_ZH AUDIO_TEST_RECORDING_ZH SKILL_SHOWCASES_ZH CHINESE_UI_ZH WRIST_MENU_INTERACTION_DESIGN_ZH'''.split(),
    'architecture': '''EXTENSION_INTERFACES_ZH SKILL_FRAMEWORK_AND_INTERACTION_ZH PLAYER_DATA_DESIGN_ZH LINEAR_RPG_DESIGN_ZH GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH QUEST_PICO_COMPATIBILITY_PLAN_ZH MULTIPLAYER_LAN_PLAN_ZH CAMERA_PRESENTATION_DESIGN_ZH'''.split(),
    'research': '''ROGUELITE_GLYPH_LOCOMOTION_SOURCES_ZH QUEST_PICO_COMPATIBILITY_SOURCES_ZH MULTIPLAYER_SOURCES_ZH PLAYER_AUDIO_BASELINE_ZH NEUGAMEMRTOOLS_REVIEW_ZH TUTORIAL_UI_RESEARCH_ZH VOICE_GESTURE_RESEARCH_ZH UI_PLATFORM_MULTIPLAYER_AUDIT_ZH'''.split(),
    'world': '''KAO_GONG_LU_DU_WORLD_SETTING_ZH 考工律度_世界观设定白皮书'''.split(),
}
by_name = {Path(p).stem: p for p in original if p.startswith('docs/')}
mapping = {}
notes = {}
for group, names in groups.items():
    for name in names:
        old = by_name[name]
        new_name = 'GLYPH_DESIGN_ZH' if name == 'GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH' else name
        mapping[old] = f'docs/{group}/{new_name}.md'
        notes[old] = {'guides': '迁入操作指南；修复相对链接。', 'architecture': '迁入架构/设计；标明提案与运行实现边界并修复链接。', 'research': '迁入调研资料；注明历史核对范围并修复链接。', 'world': '迁入世界观目录；保留正文设定，仅修复路径引用。'}[group]

retire_specs = {
    'KAO_GONG_LU_DU_V1_REVIEW_ZH': ('docs/模型交火/世界观变更交接.md', '独立旧版审阅停止作为当前入口；原文迁入世界观历史记录。'),
    'KAO_GONG_LU_DU_V15_REVIEW_ZH': ('docs/模型交火/世界观变更交接.md', '独立旧版审阅停止作为当前入口；原文迁入世界观历史记录。'),
    'KAO_GONG_LU_DU_TECH_CONFLICT_REVIEW_20261101_ZH': ('docs/模型交火/代码审阅交接.md', '阶段性冲突清单迁入代码审阅历史；问题状态须新记录复核。'),
    'INTERACTION_REPAIR_20261031_ZH': ('docs/guides/TRACKING_TROUBLESHOOTING_ZH.md', '过程记录被现行移动、腕部和排障指南替代；独有诊断边界已提炼。'),
    'INTERACTION_REPAIR_20261101_ZH': ('docs/guides/WRIST_MENU_INTERACTION_DESIGN_ZH.md', '旧修复轮次含过时输入来源描述；合并子面板、跟随、三维拖动与遮挡规则。'),
    'TECHNICAL_DEVELOPMENT_INDEX_ZH': ('docs/README.md', '旧索引含过时操作，替换为分类总索引。'),
    'WORLD_BUILDING_AND_ART_DIRECTION_BRIEF_ZH': ('docs/模型交火/需求变更交接.md', '发给外部 AI 的早期候选提示词已完成用途；采用方向和未实现边界写入交接。'),
    'SKILL_TEST_GUIDE_ZH': ('docs/guides/CONFIGURATION_GUIDE_ZH.md', '基础测试接线与四场景说明已由配置指南和展示指南覆盖。'),
    'ARCHITECTURE_REFACTOR_QA_ZH': ('docs/guides/VALIDATION_ZH.md', '旧阶段验收被统一验证说明替代；历史覆盖与局限迁入代码交接。'),
    'SKILL_DELIVERY_QA_ZH': ('docs/guides/VALIDATION_ZH.md', '初次交付验收含当时未实现 RPG 的旧结论，历史结果仅保留摘要。'),
    'LINEAR_RPG_QA_2026-10-30_ZH': ('docs/guides/VALIDATION_ZH.md', '首次 RPG 验收被后续检查扩充，保留历史摘要而不继续维护旧结果入口。'),
    'DIRECTIONAL_INTERACTION_QA_2026-10-30_ZH': ('docs/guides/VALIDATION_ZH.md', '旧四向交互验收不代表当前模式，保留测试层级与隔离方式摘要。'),
    'HAND_TRACKING_AB_TEST_ZH': ('docs/guides/TRACKING_TROUBLESHOOTING_ZH.md', 'Meta Link A/B 未解决故障，配置已恢复；保留结论，不保留旧操作任务。'),
    'HAND_TRACKING_ENVIRONMENT_REPAIR_ZH': ('docs/guides/TRACKING_TROUBLESHOOTING_ZH.md', '旧环境修复过程已结束；保留原生探针成功边界、构建与 ADB 注意点。'),
    'LINK_HAND_TRACKING_CASES_SOURCE_NOTES': ('docs/guides/TRACKING_TROUBLESHOOTING_ZH.md', '旧 Link 病例搜索不构成现行兼容矩阵；保留诊断原则。'),
    'QUEST_FIRMWARE_UPDATE_GEMINI_HANDOFF_ZH': ('docs/guides/TRACKING_TROUBLESHOOTING_ZH.md', '一次性固件升级委托已过时；不继承旧会话授权，保留复验原则。'),
    'shared-editor-crash-research': ('docs/guides/QUEST_SETUP.md', '跨工程崩溃调查结论已被环境指南与排障指南覆盖。'),
    'DIRECTIONAL_INTERACTION_AND_THROW_ZH': ('docs/guides/ENCOUNTER_LOCOMOTION_ZH.md', '旧四向闪避/轨迹瞄准操作已被战斗探索移动与当前火球指南替代。'),
    'THROW_DIRECTION_AND_NAV_SCORING_ZH': ('docs/guides/FIREBALL_TUNING_ZH.md', '旧投掷方向与导航权重方案已替代；保留鲁棒采样算法及追踪边界。'),
    'INTERACTION_FEEL_AND_DEBUG_ZH': ('docs/guides/ENCOUNTER_LOCOMOTION_ZH.md', '分散的手感补丁说明合并到现行移动、火球与腕部指南。'),
    'SCORED_THROW_DIAGNOSTICS_ZH': ('docs/guides/FIREBALL_TUNING_ZH.md', '旧门槛与投掷方向描述过时；自动诊断与动作评分含义已合并。'),
}
retired = {by_name[name]: spec for name, spec in retire_specs.items()}
assert set(mapping) | set(retired) == set(original) - {'README.md'}, 'Unclassified Markdown'
destinations = set(mapping.values())
assert len(destinations) == len(mapping)

def section(text, start, end, replacement):
    a = text.index(start)
    b = text.index(end, a + len(start))
    return text[:a] + replacement.rstrip() + '\n\n' + text[b:]

def banner(text, message):
    heading, rest = text.split('\n', 1)
    return heading + '\n\n> ' + message + '\n' + rest

def current_ops():
    return '''## 当前操作（LinearRpgTest）

- **战斗移动**：左食指指向可达节点，头向/手指/距离初始权重为 60%/30%/10%；有效准备 0.12 秒、同点驻留 0.32 秒后传送。路径、地面、占位和碰撞仍须通过。
- **战斗后撤**：空手右手从胸前快速前推，沿起手时头显水平前向的反方向滑退，初值 0.8 米/0.25 秒。持球、UI 操作或结印占用时不启动。没有通用无敌帧。
- **探索行走**：左食指持续伸直，沿水平头向行走，默认 0.95 米/秒；收指、放腕、失联或 UI 操作即停。探索不响应闪避，切换模式后需有效收指重备。
- **火球**：寅印与新口令“火球术”组合，先后不限，默认窗口 8 秒。成功生成扣 MP/CD；手槽最多一发。头显朝向决定目标，从手边发射，三种手部动作只触发发射；同印持续有效 1 秒可配新口令重备，持球/失联/UI 占用时不累计。
- **腕饰**：抬左腕，右食指从外靠近点腕饰开关小菜单，退开后再点可关闭；放腕也关闭入口。主菜单支持近按，独立子卡只用射线高亮后轻捏确认，松开重备。结印/咒语卡默认放腕保留，画符预览默认随腕收起。
- **取消与占用**：可靠双腕同时低位保持约 0.4 秒取消待施法和未发射物，保留已生效护盾、飞行物和已提交 MP/CD；追踪丢失不等于取消或松手。靠近/悬停不占战斗输入，真正菜单操作或拖动才占用；导航证据不同时用于新结印。

参数及优先级见[战斗/探索移动](ENCOUNTER_LOCOMOTION_ZH.md)、[火球调参](FIREBALL_TUNING_ZH.md)和[腕饰操作](WRIST_MENU_INTERACTION_DESIGN_ZH.md)。旧四个展示场景保留自己的前推与取消逻辑，见[展示指南](SKILL_SHOWCASES_ZH.md)。这些桌面实现仍需本轮头显手感验收。
'''

out = {}
for old, new in mapping.items():
    value = texts[old]
    name = Path(old).stem
    if '/architecture/' in new:
        value = banner(value, '文档定位：架构基线与设计约束；文内早期方案不是自动实施指令。当前操作以[移动指南](ENCOUNTER_LOCOMOTION_ZH.md)、[火球指南](FIREBALL_TUNING_ZH.md)和[腕饰指南](WRIST_MENU_INTERACTION_DESIGN_ZH.md)为准；功能是否完成须核对源码、场景与带版本的验收。')
    if '/research/' in new:
        value = banner(value, '文档定位：资料核查/历史调研，核对日期见正文，候选方案和外部版本信息需要使用时重新核实。本文不充当当前操作或已完成功能清单；现行入口见[文档索引](TECHNICAL_DEVELOPMENT_INDEX_ZH.md)。')
    if name == 'GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH':
        value = value.split('## 8.')[0]
        value = value.replace('第 8 节保留原审阅；', '原追加审阅已整体迁入模型交火的代码审阅交接；')
        value = value.replace('其他 AI 可在本文末尾追加审阅', '其他 AI 应在模型交火的代码审阅交接追加审阅')
        value += '\n## 审阅记录入口\n\n历史追加内容已原文迁入[代码审阅交接](../模型交火/代码审阅交接.md)。后续继续在该文件末尾追加；本设计主文维护现行方案，不混入每轮讨论。\n'
        notes[old] = '更名为画符设计；保留战斗外拿笔、P2 优先级和固定源码结论；原追加区完整迁入代码交接。'
    if name == 'CONFIGURATION_GUIDE_ZH':
        value = section(value, '## 线性 RPG 首个切片', '## 配置分层', '## 线性 RPG 首个切片\n\n打开 **Tools → NarutoVR → 线性RPG → 创建或打开测试场景**。已接入 HP/MP/CD、三阶段固定奖励、火球升级、隐藏盾和版本化检查点；完整剧情、头显商店、画符和敌人 AI 仍为后续。\n\n' + current_ops() + '\n独立存档、词表与检查入口见[运行指南](LINEAR_RPG_QUICKSTART_ZH.md)和[验证说明](VALIDATION_ZH.md)。')
        value = value.replace('以这里及当前 Inspector 为准。', '以正在运行的目录/效果资产及本节指定的现行操作指南为准。')
        value += '\n## 同印重新就绪例外\n\n组合技能可以配置同印重新就绪时间，当前 RPG 为 1 秒；需要新口令，并受手槽、追踪和 UI 占用门控。不要把“持续同印绝不再施法”的早期说明套用于已启用此选项的 RPG 目录。\n'
        notes[old] = '重写 RPG 操作段，移除四向导航/旧闪避规则；补同印重备例外，保留资产扩展与语音接线。'
    if name == 'LINEAR_RPG_QUICKSTART_ZH':
        value = section(value, '## 操作 A', '## 角色、购买与存档', current_ops())
        value = value.replace('更新：2026-10-30。', '操作核对：2026-11-01。')
        notes[old] = '重写操作为战斗选点、快推后撤、探索行走、头向火球与子卡射线；保留关卡、资源和存档说明。'
    if name == 'LINEAR_RPG_DESIGN_ZH':
        value = section(value, '## 2. 已确定的操作 A', '## 3. 数据归属', '## 2. 当前交互契约\n\n' + current_ops() + '\n操作主文由上述三份指南统一维护。本文件维护关卡、资源与数据边界；未来画符取消笔画的规则只在画符实现后生效。')
        value = value.replace('更新：2026-10-30。', '交互契约核对：2026-11-01。')
        notes[old] = '删除失效操作 A 和“腕部 UI 未实现”段，改为指向当前交互契约；保留线性成长和数据设计。'
    if name == 'OFFICIAL_HAND_POSES_ZH':
        value = section(value, '## 下一次 Quest 串流', '面板状态的含义：', '## 下一次 Quest 串流\n\n先在 Steam Link 路线确认骨骼持续有效与左右官方手形，再按[移动指南](ENCOUNTER_LOCOMOTION_ZH.md)测试战斗选点、胸前前推后撤和探索收指停步；按[火球指南](FIREBALL_TUNING_ZH.md)测试握持与松手。手形通过只是动作的一个门槛，不保证移动、施法或发射被接受。\n\n')
        value = value.split('## 本轮验收结果')[0] + '\n## 验证口径\n\n当前检查入口、报告时间和真机边界见[验证说明](VALIDATION_ZH.md)。旧轮次数量不再作为最新通过数维护。\n'
        value = value.replace('更新：2026-10-30。', '操作核对：2026-11-01。')
        notes[old] = '保留 SDK Curl/阈值/有效性解释，替换旧移动和轨迹瞄准步骤，移除旧轮次数量入口。'
    if name == 'SKILL_FRAMEWORK_AND_INTERACTION_ZH':
        start = value.index('> **2026-10-30 当前规则优先入口：**')
        end = value.index('日期：2026-10-29。', start)
        value = value[:start] + '> 当前 RPG 操作统一见[移动指南](ENCOUNTER_LOCOMOTION_ZH.md)、[火球指南](FIREBALL_TUNING_ZH.md)和[腕饰指南](WRIST_MENU_INTERACTION_DESIGN_ZH.md)。腕饰与输入仲裁已接入；多印、画符、领域及通用多效果事务仍未完成。下文保留技能架构与早期设计，不能将旧场景取消/前推规则套用于 LinearRpgTest。\n\n' + value[end:]
        notes[old] = '修正顶部“旧四向、手轨迹瞄准、腕饰尚未接入”状态；保留技能架构基线。'
    if name == 'SEAL_LAB_QUICKSTART_ZH':
        value = section(value, '## 第一次连Quest调试', '## 录制：先从一个印开始', '## 第一次连 Quest 调试\n\n按[Steam Link 开发指南](STEAM_LINK_DEVELOPMENT_ZH.md)连接 Quest → SteamVR OpenXR → Unity。打开 Steam Link 专用实验场景，放下控制器，核对真实骨骼、逐关节有效性和样本时间，再进行录制。系统轻捏可用不能证明应用收到骨骼。环境故障见[追踪排障](TRACKING_TROUBLESHOOTING_ZH.md)。\n\n')
        notes[old] = '保留采样/标定流程，移除过时的首次 Meta Link 连接步骤并指向 Steam Link。'
    if name == 'VOICE_GESTURE_RESEARCH_ZH':
        # Preserve reusable source comparison; discard superseded implementation plan.
        value = value.split('## 建议的联合判定')[0]
        value += '\n## 已采用的路线与边界\n\n当前采用 PC sherpa-onnx KWS 和单印三模式匹配，默认组合窗 8 秒、缓存上限 10 秒/640 帧。KWS 不理解否定语义；独立 Android 语音尚未接通。被替代的统一就绪、双腕推手确认和语音自动降低手印阈值方案不再作为需求。现行接线见[语音指南](SKILL_VOICE_SETUP_ZH.md)，匹配架构见[技能框架](SKILL_FRAMEWORK_AND_INTERACTION_ZH.md)。\n'
        notes[old] = '保留 KWS/ASR/声学匹配的来源比较，删除已被三模式框架替代的旧就绪与推手提案。'
    if name == 'UI_PLATFORM_MULTIPLAYER_AUDIT_ZH':
        value = re.sub(r'> 2026-10-31 实施更新：.*', '> 该审计反映 2026-10-30 的问题；腕饰现已接入。当前子卡仅射线轻捏，UI 的靠近/悬停不占输入；操作以[腕饰指南](WRIST_MENU_INTERACTION_DESIGN_ZH.md)为准。MR、PICO、网络权威与完整课程仍需独立实施与验收。', value)
        notes[old] = '作为历史架构审计迁入 research；更正顶部当前 UI 边界，旧表不充当现行状态清单。'
    out[new] = value

out['docs/guides/WRIST_MENU_INTERACTION_DESIGN_ZH.md'] = '''# 腕饰菜单与独立教学卡：当前操作

操作核对：2026-11-01。已接入 `LinearRpgTest`；文字速查已实现，画符识别、动作示范与完整课程尚未完成。默认入口为 `TapWrist`；`RaiseFist` 仅留作配置对照。旧演进过程归入模型交火，不再作为默认操作。

## 打开、确认与关闭

1. 左腕抬到胸前；不要求握拳。右食指从外侧主动靠近腕饰并点入，打开小菜单。退到接触区外再点一次关闭；持续接触不连开关。放低左腕也收起入口。
2. 主菜单按钮支持近距离点按及射线轻捏；独立子卡**只使用射线轻捏**。按钮高亮后捏下确认，松开才能再次操作；已捏住再进入按钮不会点击。
3. 结印/咒语卡默认放腕保留，画符预览默认随腕收起；`closeWithWrist` 可分别配置。小菜单关闭、独立卡关闭和取消施法是不同动作。
4. 指向顶部拖动条，捏住后移动手或转动射线，以捕获距离和偏移更新三维位置；拖动期间卡片朝向头显。松开结束拖动。
5. 跟随开启：位置随玩家 Rig，朝向持续看向头显。跟随关闭且未拖动：保持当时世界位置和旋转。传送或追踪坐标系重建按重置策略处理；探索连续行走不应每帧关闭卡片。

## 输入归属与追踪恢复

仅靠近腕饰或悬停卡片只高亮，不清结印、不丢口令、不打断导航。确认开主菜单、按按钮或拖动才占用对应输入；已有手持物优先。只读卡可保留并对照施法；普通菜单不暂停全局时钟。退出实际交互时清旧点击/动作历史并重新准备，不能把收手当新发射或后撤。

射线方向和轻捏选择分别接线：优先使用有效、通过手边距离校验的 Meta/FB Aim 或标准 EXT 输入；不具备可信系统输入时，射线回退到腕根至食指/中指指根中点方向，轻捏回退到拇食指尖距离与迟滞。EXT 的 pinchReady/数值必须有效，当前 0.75 按下、0.35 松开，中间保持。实体点腕仍需真实食指尖。代码有适配路径不等于已完成 PICO 真机验收。

拖动丢样时冻结世界位置，宽限内恢复持续捏合后重算偏移；默认宽限 0.30 秒。有效松手即结束，即使当帧射线无效；超时、动作占用、关闭或空间重置也结束。点击在来源切换/丢样后等待张开重备，不能补发旧点击。

## 配置、实现与验证

在场景“左腕术式菜单与教学卡”的 `WristMenuController` 调接触区、腕骨偏移、关闭策略与拖动宽限；`WristMenuView` 管表现。默认点腕参考：主动靠近 2 厘米、接触半径 5.5 厘米、确认 0.025 秒、退开 10 厘米重备。骨骼轻捏回退参考为 3 厘米按下、4.5 厘米松开。这些是可调配置，不是准确率结论；运行时 Inspector 与实际输入来源优先。

`WristTapSwitch` 负责开关边沿；`WristRayPinchState` 负责轻捏和高亮缓存；RPG 主控统一仲裁；`WristMenuController/View` 管面板。Hierarchy 的“腕部界面 · 自有世界根”是临时 UI，自建资源在禁用、销毁或脚本重载时清理，不应保存到场景或手动复制。

电脑 Debug 查看射线来源、捏合来源、目标、确认与拖动状态。按[验证说明](VALIDATION_ZH.md)运行自动检查，再在头显各做 10 次开关、翻页、三轴拖动、短遮挡恢复、长遮挡、跟随切换；检查不误施法、不误后撤、保留卡可关闭。合成测试不能代替真人手感。

平台、共享空间与网络权威仍属后续设计，参见[兼容方案](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md)、[多人方案](MULTIPLAYER_LAN_PLAN_ZH.md)。教学内容从稳定技能 ID/目录读取，个人 UI 不写入战斗存档；示范音隔离需在真正接入播放功能时另验收。
'''
notes[by_name['WRIST_MENU_INTERACTION_DESIGN_ZH']] = '删除旧抬拳入口演进叙述；合并最新点腕、子卡射线、三维拖动、跟随、系统输入/骨骼回退与遮挡恢复规则。'

out['docs/guides/FIREBALL_TUNING_ZH.md'] += '''
## 动作评分与自动诊断

动作分是若干真实运动特征的加权证据，不是正确概率；高分仍须通过握持确认、追踪、新鲜度、时序、位移/速度、输入占用和碰撞等硬门槛。生成时已经远伸不能仅等待保持计时就发射，必须观察到本次主动伸手。左手失联不应独自阻断合法右手发射；右手失联不当作松手或可靠低位。

进入 Play 后可自动写 `Logs/throw-captures/throw-score-*.jsonl`，默认上限 15 分钟/64 MB，可停止并新建文件。该日志与通用[施法链路](SKILL_CAST_TRACE_ZH.md)互补：先看生成/握持/输入占用，再看候选方式、冻结释放点、拒绝原因及实际发射事件。不要把未通过候选的分数当作已发射。调试记录是结构化运动/状态数据，不是原始麦克风录音。

当前相关实现包括 `HandThrowState`、`HandThrowIntentState`、`SkillEffects` 与 `ThrowDiagnosticRecorder`；测试覆盖见 `ScoredThrowChecks` 和 `HandThrowChecks`。旧四向移动和“按甩手轨迹决定弹道”的说明已退出当前 RPG 操作入口，旧展示场景保留自己序列化的行为。辅助前送默认分数 62、后拉/前送 5/9 厘米、伸手保持 0.45 秒；效果资产是参数单一入口。
'''
notes[by_name['FIREBALL_TUNING_ZH']] = '保留最新参数和算法；吸收旧评分文档的硬门槛、自动录制、拒绝原因与旧场景边界。'

out['docs/guides/TRACKING_TROUBLESHOOTING_ZH.md'] = '''# 手追踪、语音与环境排障

整理：2026-11-01。本文提炼旧故障记录中的有效结论，不宣称这些故障目前仍存在。当前 PC 路线见[Steam Link 指南](STEAM_LINK_DEVELOPMENT_ZH.md)，首次环境见[Quest/Unity 配置](QUEST_SETUP.md)。

## 先分辨失败在哪一层

1. 核对实际 OpenXR Runtime、输入/VR 焦点、左右真实骨骼和样本时间。系统菜单能轻捏、扩展已启用、桌面显示手模型，均不能单独证明 Unity 得到有效骨骼；反之系统页面没手模型也不能单独判定应用失效。
2. 骨骼有效后再看关节质量、官方手形和模板评分，再看菜单/导航占用、MP/CD、手槽及效果碰撞。[施法链路](SKILL_CAST_TRACE_ZH.md)与[火球诊断](FIREBALL_TUNING_ZH.md)保留各层拒绝原因。
3. 手姿不连续时先记录真实观测间隔。曾采到 0.229～0.346 秒间隔，大于当时技能最大间隔；未佩戴限帧、编辑器负载和无线链路均需分开对照。减少内存或通过合成测试，不能证明真机低帧率已解决。

## 已完成的历史对照与适用范围

- 原生 Quest 手探针 `com.narutovr.handprobe` 曾取得有效手数据并由用户观察正常；这是原生追踪链的有限采样，不是完整技能/语音/性能 APK 验收。报告需看本次时间和焦点，不复用旧文件充当本次成功。
- 旧 Meta Link 中手数据无效；服务重启、部分扩展 A/B 未恢复，测试后配置已还原。没有证据证明某两个固定版本必然不兼容，也不能保证固件升级必修复。当前改走 Steam Link，不继续执行旧固件升级委托。
- USB 排障先确认设备枚举、驱动、ADB 或实际网络接口。接线/ADB 安装不等于已建立视频串流；需要支持的 USB 网络或串流方案及独立验证，不能跳过物理链路直接设置 IP。
- 原生探针构建曾受中文用户路径和批处理换行影响，使用项目英文 `.build-cache/gradle` 与正确换行解决。构建前核对现有工具，不重装 SDK 作为默认诊断动作。

## Windows 工具链与语音

Meta 工具曾调用位于中文用户路径的第三方 ADB，在创建进程前触发 `std::range_error`；对照使用 Unity Android SDK 的 ADB 后恢复。可核对 `Tools/Use-UnityAdb.ps1`，先确认实际执行路径，不把独立进程路径问题归因于所有 Meta SDK。旧同意配置重建对照无效且已还原，不再保留为排障步骤。

PC 编辑器语音可按场景设置自动启动；只停止本会话创建且持有所有权标记的进程，不停止用户手动开的服务。看心跳、接收时钟、输入设备、词表和音量/削波等遥测；标量遥测不是原始音频录制。换场景词表后重新导出并重启服务，见[语音设置](SKILL_VOICE_SETUP_ZH.md)。

## 复验与记录

先记录环境、代码/配置基线和真实故障，再做单变量对照，结束恢复配置。既有菜单 `Tools / NarutoVR / Capture Tracking Diagnostics` 可采应用追踪状态；使用请求文件前读当前脚本。固件/驱动/平台版本使用时重新核实官方信息；旧文档中的授权不延伸为后续系统改动权限。

历史结果摘要见[代码审阅交接](../模型交火/代码审阅交接.md)，新的诊断、修改和结论按[交接规则](../模型交火/README.md)追加。
'''

quest = out['docs/guides/QUEST_SETUP.md']
quest = re.sub(r'经用户明确许可，曾将 .*?(?=后续已创建独立)', '', quest, flags=re.S)
quest = quest.replace('Quest 真机启动、手部追踪数据与 APK 构建尚未验证。', '原生 Quest 手探针后来已构建并观察到有效追踪；这不代表完整技能、原生语音和性能已经验证。PC 当前使用 Steam Link，现行入口见[Steam Link 指南](STEAM_LINK_DEVELOPMENT_ZH.md)，历史环境故障结论见[排障指南](TRACKING_TROUBLESHOOTING_ZH.md)。')
out['docs/guides/QUEST_SETUP.md'] = banner(quest, '本文保留 Unity/Meta 安装与工具链配置依据；PC 连接使用 Steam Link。历史安装结果不代表未来版本仍相同，依赖以项目 manifest/lock 为准。')
notes[by_name['QUEST_SETUP']] = '保留安装/ADB 工具链依据，修正原生探针验证边界，移除无效同意配置排障过程，补当前连接入口。'

editor = json.loads((ROOT / 'Logs/official-hand-poses/editor-suite.json').read_text(encoding='utf-8-sig'))
play = json.loads((ROOT / 'Logs/linear-rpg/play-check.json').read_text(encoding='utf-8-sig'))
out['docs/guides/VALIDATION_ZH.md'] = f'''# 验证入口与证据边界

文档整理时间：{NOW}。本次仅整理文档，没有重跑 Unity 或头显测试。

## 现有检查怎么运行

- Unity：`Tools → NarutoVR → 线性RPG → 运行完整编辑模式回归`，以及 `运行独立存档播放检查`。
- 后台请求：先读 `Tools/Request-RpgCheck.ps1` 当前参数；例如 `./Tools/Request-RpgCheck.ps1 full-editor-check`。编辑器需处于可处理请求的状态。
- 配置：`Tools → NarutoVR → 技能配置 → 验证当前场景 / 运行架构检查`；修改资产后核对场景实际引用。
- 语音契约：`Tools/SkillVoice/.venv/Scripts/python.exe -X utf8 -m unittest discover -s Tools/SkillVoice -p test_configuration.py -v`；真实模型合成音回归使用 `Tools/SkillVoice/verify_fixtures.py`，不代表真人麦克风准确率。

## 整理时读取的报告快照

| 范围 | 报告时间（UTC） | 结果 | 边界 |
|---|---|---|---|
| 编辑模式 | {editor['utc']} | passed={str(editor['passed']).lower()}，{editor['total']} 项 | hardwareTested={str(editor['hardwareTested']).lower()} |
| Play Mode | {play['utc']} | passed={str(play['passed']).lower()}，集成 {play['integrationChecks']} 项，效果 {play['effectChecks']} 项 | hardwareTested={str(play['hardwareTested']).lower()} |

来源为本地 `Logs/official-hand-poses/editor-suite.json` 与 `Logs/linear-rpg/play-check.json`。这是既有报告快照，没有与后续工作区绑定为新的通过证明；仓库不分发这些日志。后续验收必须记录代码提交或文件哈希、场景/资产、运行环境、命令和新报告时间。

Play 检查包含真实 Unity 生命周期、Physics 与明确注入的合成骨骼/调试命令，使用独立 probe 存档；部分效果通过反射或模拟发射验证。曾临时停 XR 子系统以隔离未佩戴限帧，结束恢复，不代表在实际串流帧率下完成。验收还需检查退出 Play 后控制台与清理状态。

## 必须单列的真机验收

当前完整动作的 Quest 手感、追踪遮挡恢复、自然动作误触发、麦克风误报漏报、滑退舒适度及长期性能仍需专项记录。十二个手印资产不代表十二印完成标定；PICO、MR、多人和 Android 语音也不能由桌面回归推定完成。

本轮文档链接检查与整理清单记于[需求变更交接](../模型交火/需求变更交接.md)。2026-10-29/30 的首次交付与旧四向验收摘要见[代码审阅交接](../模型交火/代码审阅交接.md)，不继续用历史数量充当最新结果。
'''

out['docs/README.md'] = '''# 文档索引

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
| 检查与验收 | [命令、报告快照与真机边界](guides/VALIDATION_ZH.md) |

当前 RPG 使用战斗头向/手指融合选点、空手胸前快推后撤和探索水平头向行走；火球按头向瞄准，手部动作触发发射。旧四个展示场景维持自己的前推/取消行为。参数以实际场景引用资产为准。

## 架构与设计

- [技能框架](architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md) · [扩展接口](architecture/EXTENSION_INTERFACES_ZH.md) · [玩家数据](architecture/PLAYER_DATA_DESIGN_ZH.md) · [线性 RPG 范围](architecture/LINEAR_RPG_DESIGN_ZH.md)。
- [画符设计](architecture/GLYPH_DESIGN_ZH.md)：战斗外有效抓笔进入，期间不结印/攻击，P2；运行时尚未实现。
- [Quest/PICO 兼容](architecture/QUEST_PICO_COMPATIBILITY_PLAN_ZH.md) · [同房多人](architecture/MULTIPLAYER_LAN_PLAN_ZH.md) · [观众镜头](architecture/CAMERA_PRESENTATION_DESIGN_ZH.md)：设计契约，不是跨设备/联网已完成声明。

## 世界观与专业需求

- [世界观设定](world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md) · [世界观白皮书](world/考工律度_世界观设定白皮书.md)。二者作为现有设定来源保留，后续差异须在世界观交接中裁决；本次没有擅自统一设定。
- [设定与机制全案（Word）](考工律度_世界观设定与机制全案.docx)。
- [程序需求](程序需求/考工律度_程序架构与交互底层技术需求规范.docx) · [策划需求](策划需求/考工律度_核心玩法机制与关卡首领数值策划案.docx) · [美术需求](美术需求/考工律度_美术资产与视效制作需求规范.docx) · [音频需求](音频音乐需求/考工律度_音频音效与离线语音工程需求规范.docx) · [跨专业交接](交接文档/考工律度_XR项目研发全景与跨专业交接指南.docx)。这些非 Markdown 资料本次保持原样，涉及变更同样要记录具体位置与内容。

## 调研与参考

- [画符/移动来源](research/ROGUELITE_GLYPH_LOCOMOTION_SOURCES_ZH.md) · [设备来源](research/QUEST_PICO_COMPATIBILITY_SOURCES_ZH.md) · [多人来源](research/MULTIPLAYER_SOURCES_ZH.md)。
- [语音方案比较](research/VOICE_GESTURE_RESEARCH_ZH.md) · [玩家音效](research/PLAYER_AUDIO_BASELINE_ZH.md) · [教学 UI](research/TUTORIAL_UI_RESEARCH_ZH.md) · [工具审阅](research/NEUGAMEMRTOOLS_REVIEW_ZH.md) · [历史跨系统审计](research/UI_PLATFORM_MULTIPLAYER_AUDIT_ZH.md)。
- [十二印配置参考表](reference/NarutoVR_12Seals_FeatureConfig.xlsx)：录制时期参考，运行时 SO 和实际标定优先。`visuals/` 保留现有舒适性示意图。

## 模型交接与维护

[规则和模板](模型交火/README.md) · [代码审阅交接](模型交火/代码审阅交接.md) · [需求变更交接](模型交火/需求变更交接.md) · [世界观变更交接](模型交火/世界观变更交接.md)。三个交接文件只在末尾追加，历史结论通过新记录纠正。现行指南正常修订，修改/移动/删除的文件逐项写入交接，保持索引与链接同步。
'''

out['docs/模型交火/README.md'] = '''# 模型交火：协作规则与记录模板

适用：Codex、Gemini 及其他模型在本仓库进行代码审阅、需求/设计调整、世界观修改，以及文档新增、修改、移动和删除。开始任务时读本规则、相关现行文档，以及相关交接文件最近记录和被引用的问题；交付前追加交接记录。

## 三个固定交接文件

| 文件 | 写什么 |
|---|---|
| [代码审阅交接](代码审阅交接.md) | 审查范围、问题、证据、修复/复核、测试及未解决项；审阅没有改代码也要记录 |
| [需求变更交接](需求变更交接.md) | 用户需求、技术/交互/数值/范围变化、文档治理；逐项说明这次哪些文件改了什么 |
| [世界观变更交接](世界观变更交接.md) | 世界规则、术语、角色、剧情、美术/音频风格及玩法解释的变化与跨文档影响 |

一次任务跨类时，在相关文件使用同一个任务 ID，并互相引用。共同的完整文件清单可集中在需求交接的一条固定记录，其他交接明确链接该记录；各自仍说明本类别结论。纯检查且无内容变更，写明代码/文档均未修改，不虚构变更。

## 追加历史的规则

- 三个交接文件从建立起只允许在文件末尾追加完整新记录。不得删除、覆盖、重排、改写已有记录，也不回填修改旧记录的状态。初始化前不存在的文件可以新建。
- 修复、关闭、撤回或反驳旧问题时，新建条目引用原任务/问题 ID，写证据、理由和新状态。旧观点仍保留；模型意见本身不等于用户批准。
- 导入历史注明原作者/模型、原时间和导入时间；缺失的模型版本或时分秒写“未记录”，不猜测。导入旧记录不代表现在复现，也不自动恢复旧委托或授权。
- 追加前重新读取文件尾部，核对没有其他模型新增内容；使用追加写入，不用整文件重写。发现同时有人改同一业务文档时先对齐文件范围，保留未认领的修改。

## 每条记录必填内容

1. 唯一任务 ID、记录 ID、模型实际标识、执行入口和带时区时间，例如 `2026-11-01T21:00:00+08:00`。区分“Gemini”入口和具体模型；拿不到具体型号写“未知/未提供”，不能伪造型号或代他人署名。
2. 触发需求、变更前后、原因和状态：提案/待确认/已实现/已验证/撤回等。实施需要用户选择的设计仍保持待确认；区分代码实现、桌面测试和头显实测。
3. **代码/资产清单**：每个路径、具体改动；未改写“无”。**文档清单**：每个新增/修改文件的路径、章节或条目、改了什么。移动写旧路径→新路径；删除写路径、为何过时及替代入口。禁止只写“同步 README”“更新相关文档”。
4. 影响范围与冲突：配置、场景、术语、玩法、程序、美术、音频等受影响处，逐项说明已同步/未同步。保留未解决问题 ID、下一步与所需决定。
5. 验证：实际命令/操作、时间、版本或文件哈希、结果与证据位置；没跑写“未运行”及原因。既有报告只写历史快照，不冒充本次通过，模拟输入不冒充真机。

## 文档同步与完成条件

- 根 `README.md` 展示项目；`docs/README.md` 为唯一总索引；`guides/` 维护当前操作；`architecture/` 放架构与设计，注明尚未实现内容；`research/` 放带核查日期的来源；`world/` 放世界设定。
- 技术事实核对当前源码、实际场景和配置引用，设定决策核对用户最新要求及已接受交接。文档与实现不同，记录差异和下一步，不把某份文档的存在当成实现或批准证据。
- 需求变化核对 README、相关操作/设计文档及专业需求资料；世界观变化核对两份设定、美术/音频/策划/程序需求。未同步的文件明确列出，不能宣称全量一致。
- 普通现行文档可以整理、合并、删除过时内容，但先保留仍有用的结论、修复入链，再在需求交接逐项记录去向。交接三文件的历史例外：永久保留，纠正也只能追加。新日志不要散落在旧设计文末或 `docs/temp`。
- 完成前核对相对链接、目录索引、实际文件变更清单，以及交接所称测试与真实执行是否一致；最终向用户指出改动、验证、待解决项和记录位置。本文是协作约定，不声称工具已经强制阻止覆写。

## 追加模板

```markdown
## <记录 ID>｜<标题>

- 任务 ID：<同任务跨文件共用>
- 模型：<实际标识，未知明确写明>；执行入口：<Codex/Gemini/...>
- 时间：<ISO 8601，含时区>
- 来源与状态：<用户要求/关联记录；提案/已实现/已验证/...>

### 变化与依据

<变更前→变更后、原因；审阅则列问题 ID、严重性、位置、触发条件和证据>

### 文件清单

| 类型 | 路径/章节 | 具体改动与替代去向 |
|---|---|---|
| 代码/资产 | 无或逐项列出 | ... |
| 文档新增/修改/移动/删除 | 逐项列出 | ... |

### 验证、冲突与下一步

<实际检查/结果/证据；未运行项目；未同步文件和未解决问题；关联交接记录>
```
'''

pointer = '''# 项目协作入口

进行代码审阅、需求/交互/数值/范围调整、世界观修改，或新增/修改/移动/删除文档前，先读 [模型交火规则](docs/模型交火/README.md) 和相关交接最近记录。完成后按规则在对应交接文件末尾追加：模型实际标识、带时区时间、依据/状态、逐项文件变更、验证和未解决项。

三个交接文件的既有内容永久保留；纠正或关闭问题通过新记录引用原 ID。当前文档入口见 [docs/README.md](docs/README.md)。
'''
for filename in ['AGENTS.md', 'GEMINI.md']:
    if (ROOT / filename).exists():
        raise RuntimeError(f'Existing instruction file needs merge: {filename}')
    out[filename] = pointer

readme = texts['README.md']
a = readme.index('- 条件满足后激活技能；')
b = readme.index('### 最近一次串流', a)
readme = readme[:a] + '- 条件满足后激活技能；火球生成到手与发射分开。当前 RPG 采用头向瞄准，手部动作触发发射；有效同印可配新口令重新就绪。见[火球配置与算法](docs/FIREBALL_TUNING_ZH.md)。\n- 战斗选点、探索行走与腕部仲裁见[移动指南](docs/ENCOUNTER_LOCOMOTION_ZH.md)；点腕、子卡射线轻捏、三维拖动与跟随见[腕饰指南](docs/WRIST_MENU_INTERACTION_DESIGN_ZH.md)。\n\n' + readme[b:]
readme = readme.replace('2. **子面板**：近距离食指点按钮，或指向按钮看到高亮后轻捏。', '2. **子面板**：指向按钮看到高亮后轻捏；子面板只使用射线轻捏，主菜单保留近按。')
readme = readme.replace('后拉约7厘米再快速前送', '后拉约5厘米再快速前送约9厘米').replace('稳定保持约0.6秒', '稳定保持约0.45秒').replace('前送需动作分72', '前送需动作分62')
readme = re.sub(r'\[架构重构记录\].*?原始日志、录音和本机备份不随仓库上传。', '验证入口、报告时间和覆盖范围见[验证说明](docs/guides/VALIDATION_ZH.md)。既有报告不代表后续版本自动通过；原始日志、录音和本机备份不随仓库上传。', readme)
readme = section(readme, '## 文档导航', '## 第三方资源与许可', '''## 文档导航

- [文档总索引](docs/README.md)：当前操作、架构设计、世界观和资料来源。
- [配置与扩展](docs/CONFIGURATION_GUIDE_ZH.md) · [RPG 场景与存档](docs/LINEAR_RPG_QUICKSTART_ZH.md)。
- [手势录制与标定](docs/SEAL_LAB_QUICKSTART_ZH.md) · [中文口令](docs/SKILL_VOICE_SETUP_ZH.md)。
- [画符设计](docs/architecture/GLYPH_DESIGN_ZH.md)：战斗外抓笔进入，期间不结印/攻击，P2，尚未实现运行时。
- [模型交火规则](docs/模型交火/README.md)：代码审阅、需求变更和世界观变更统一追加交接，逐项记录模型、时间及文件改动。
''')
readme = readme.replace('docs/                        # 操作、架构、设计与历史验收说明', 'docs/                        # guides / architecture / research / world / 模型交火')
readme = re.sub(r'本轮腕饰、射线选点与语音诊断操作：.*?施法故障排查与自动日志：', '追踪、语音与环境故障：[排障指南](docs/guides/TRACKING_TROUBLESHOOTING_ZH.md)。\n\n施法故障排查与自动日志：', readme, flags=re.S)
out['README.md'] = readme

# Resolve moved links by source semantics and by unique basename for the old temp layout.
retired_map = {p: spec[0] for p, spec in retired.items()}
all_map = {**mapping, **retired_map}
base_lookup = {Path(p).name: new for p, new in all_map.items()}
fence = re.compile(r'^\s*(`{3,}|~{3,})')
links = re.compile(r'(!?\[[^\]\n]*\]\()(<[^>]+>|[^\s)]+)([^)\n]*\))')
historical_logs = {'docs/模型交火/代码审阅交接.md', 'docs/模型交火/世界观变更交接.md'}
def rewrite(text, old, new):
    active = None
    lines = []
    for line in text.splitlines(keepends=True):
        mark = fence.match(line)
        if mark:
            char = mark.group(1)[0]
            if active == char:
                active = None
            elif active is None:
                active = char
            lines.append(line)
            continue
        if active:
            lines.append(line)
            continue
        def link(m):
            raw = m.group(2).strip('<>')
            if raw.startswith('#') or re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', raw):
                return m.group(0)
            parsed = urlsplit(raw)
            target_path = unquote(parsed.path).replace('\\', '/')
            candidate = posixpath.normpath(posixpath.join(posixpath.dirname(old), target_path))
            basename = posixpath.basename(target_path)
            target = all_map.get(candidate)
            if not target and basename in base_lookup:
                target = base_lookup[basename]
            if not target:
                if (ROOT / candidate).exists() or candidate in out:
                    target = candidate
                else:
                    # Many old temp docs were authored as though still at docs/.
                    fallback = posixpath.normpath(posixpath.join('docs', target_path))
                    if (ROOT / fallback).exists() or fallback in out:
                        target = fallback
                    elif target_path.startswith(('Assets/', 'Tools/', 'Packages/', 'ProjectSettings/', 'Logs/', 'docs/')) and ((ROOT / target_path).exists() or target_path in out):
                        target = target_path
            if target and (target.startswith('Logs/') or target.startswith('Builds/')):
                label = m.group(1)[m.group(1).find('[')+1:-2]
                return f'{label}（本地证据：`{target}`，不随仓库分发）'
            if not target:
                label = m.group(1)[m.group(1).find('[')+1:-2]
                if 'Logs/' in target_path:
                    evidence = target_path[target_path.index('Logs/'):]
                    return f'{label}（本地证据：`{evidence}`，不随仓库分发）'
                return m.group(0)
            relative = posixpath.relpath(target, posixpath.dirname(new) or '.')
            # Old section anchors cannot survive a replaced document; use its new top-level entry.
            fragment = ('#' + parsed.fragment) if parsed.fragment and candidate not in retired and target == candidate else ''
            return m.group(1) + relative + fragment + m.group(3)
        lines.append(links.sub(link, line))
    value = ''.join(lines)
    # Fix explicitly written repository paths outside provenance logs as well.
    for source, dest in all_map.items():
        value = value.replace('`' + source + '`', '`' + dest + '`')
        value = value.replace('`docs/' + Path(source).name + '`', '`' + dest + '`')
    return value

reverse = {new: old for old, new in mapping.items()}
for p in list(out):
    out[p] = rewrite(out[p], reverse.get(p, p), p)

# Imported reviews are explicit historical snapshots. Preserve glyph append bytes verbatim.
header = lambda title: f'# {title}\n\n只在末尾追加。填写要求见[规则与模板](README.md)。旧记录状态通过新记录纠正，禁止修改历史。\n\n'
code_log = header('代码审阅交接')
glyph_old = original[by_name['GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH']].decode('utf-8-sig')
glyph_tail = glyph_old[glyph_old.index('### 追加记录'):]
code_log += f'''## IMPORT-GLYPH-20261031｜历史画符追加审阅

- 导入任务：{TASK}；导入模型：{MODEL}；入口：Codex；导入时间：{NOW}。
- 原来源：`docs/temp/GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH.md` 的原追加区。
- 原作者：Antigravity 与 Codex（各条原署名保留，具体模型版本未记录）；原日期：2026-10-31，具体时分秒以原文记录为准，缺失不补写。
- 状态：历史审阅原文，未经本次重新复现；不代表全部接受或当前仍未修复。旧“在原设计追加”的指令由新规则替代，以下保留为历史。

<details>
<summary>原追加区全文（迁移保留）</summary>

~~~~~~markdown
'''
code_log += glyph_tail + '\n~~~~~~\n\n</details>\n\n'
tech_old = by_name['KAO_GONG_LU_DU_TECH_CONFLICT_REVIEW_20261101_ZH']
code_log += f'''## IMPORT-CONFLICT-20261101｜技术与设定冲突历史审阅

- 导入任务：{TASK}；导入模型：{MODEL}；入口：Codex；导入时间：{NOW}。
- 原来源：`{tech_old}`；原执行者：Codex（原文未确认具体子版本）；原日期：2026-11-01，具体时间见原文。
- 状态：整理前审阅快照。C03/C04 的旧故障与后续修复须按原说明和新记录看；本次只处理文档，不重跑业务测试或实现剩余需求。

<details>
<summary>原审阅全文（迁移保留）</summary>

~~~~~~markdown
'''
code_log += original[tech_old].decode('utf-8-sig') + '\n~~~~~~\n\n</details>\n\n'
code_log += f'''## {TASK}-CODE｜清理阶段性验收与交互过程文档

- 任务 ID：{TASK}；模型：{MODEL}；执行入口：Codex；时间：{NOW}。
- 来源与状态：用户要求整理文档；已完成文档合并，业务代码未改。

### 历史证据摘要

- 2026-10-29 架构/首次技能交付覆盖匹配核心 47 项、效果 20 项及配置/迁移等检查，并有录音模型→UDP→效果链路；这些数量不能和后续包含同用例的套件相加。旧“RPG/HP 未做”的结论不再当作现状。
- 2026-10-30 初始 RPG 记录及后续四向检查验证了当时合成骨骼、Physics、资源与存档；旧四向轮次 220 项编辑、37 项效果、25 项集成，不覆盖后来模式。未佩戴时临时隔离 XR 的测试不代表串流帧率或舒适性验证。
- 旧 Meta Link 无效手追踪在服务重启和扩展 A/B 后仍存在，配置恢复；原生 Quest 探针曾成功。不能据此证明固定版本必不兼容，固件升级的旧委托不再继续执行。
- 后续点腕、输入所有权、释放候选冻结、子卡仅射线、三维拖动、Rig 跟随/头向旋转与遮挡宽限被合并为当前指南；早期 313/365/457 等通过数仅是阶段记录。

### 文件、验证与待办

- 代码/场景/资产：无修改；现有未提交代码由原任务保留。
- 文档：本次逐项新增、移动、内容变更、删除与替代路径见[需求交接的 {TASK} 记录](需求变更交接.md)。世界观审阅原文见[世界观交接](世界观变更交接.md)。
- 验证：本次运行本地文档路径/引用检查并核对迁移文本；既有 Unity 报告仅作为[验证快照](../guides/VALIDATION_ZH.md)引用，本次未运行 Unity 或头显。
- 未解决：历史技术审阅中的玩法/设定决策和真机风险没有因为文档搬迁而关闭；后续逐个引用原问题 ID 复核并追加状态。
'''

world_log = header('世界观变更交接')
for name in ['KAO_GONG_LU_DU_V1_REVIEW_ZH', 'KAO_GONG_LU_DU_V15_REVIEW_ZH']:
    old = by_name[name]
    world_log += f'''## IMPORT-{name}｜历史设定审阅迁入

- 导入任务：{TASK}；导入模型：{MODEL}；入口：Codex；导入时间：{NOW}。
- 原来源：`{old}`；原执行者：Codex（具体子版本未记录）；原审阅日期/版本见原文，缺失时间不推测。
- 状态：历史快照，Gemini 已有后续修订。下文的问题清单不是本次针对最新版重新作出的判定；后续纠正/关闭引用原编号并新增记录。

<details>
<summary>原审阅全文（迁移保留）</summary>

~~~~~~markdown
'''
    world_log += original[old].decode('utf-8-sig') + '\n~~~~~~\n\n</details>\n\n'
world_log += f'''## {TASK}-WORLD｜整理设定入口，建立持续交接

- 任务 ID：{TASK}；模型：{MODEL}；执行入口：Codex；时间：{NOW}。
- 状态：文档整理已完成；无新增世界观决策。
- 代码/资产：无；Word 专业需求与设定全案保持原文件内容和位置。
- 文档移动：`docs/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md` → `docs/world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md`；`docs/考工律度_世界观设定白皮书.md` → `docs/world/考工律度_世界观设定白皮书.md`。正文设定保留，仅修复路径引用。
- 文档合并：两份旧设定审阅原文迁入本文件；最新技术冲突审阅迁入[代码审阅交接](代码审阅交接.md)。原独立审阅文件删除，具体去向见[需求交接](需求变更交接.md)。
- 需求依据：早期世界构建提示词已经完成用途；已采用的线性轻量 RPG、三种单印输入、战斗外交互画符与当前平台边界作为交接背景，不再把早期肉鸽、多印或完整敌人设想当作现成功能。
- 验证与未决：未重新裁决两份设定及 Word 资料的内容冲突；未来修改须列出受影响的程序/策划/美术/音频文档，并逐项说明同步状态。本文不替用户接受任何设定提案。
'''

# Rebase historical prose links without editing its original technical opinions.
# Imported blocks are fenced to retain exact original text and historical paths.
# New handoff navigation is authored relative to the new ledger directory.
# The mandatory append provenance is byte-preserved as Markdown text (including line endings).
out['docs/模型交火/代码审阅交接.md'] = code_log
out['docs/模型交火/世界观变更交接.md'] = world_log

move_rows = '\n'.join(f'| 移动/修改 | `{old}` → `{new}` | {notes[old]} |' for old, new in sorted(mapping.items()))
delete_rows = '\n'.join(f'| 删除旧文件 | `{old}` | {reason} 替代：`{new}`。 |' for old, (new, reason) in sorted(retired.items()))
new_notes = {
    'docs/README.md': '建立唯一分类总索引，串联现行操作、架构、世界观、调研及既有 Word 需求。',
    'docs/模型交火/README.md': '建立三类追加交接、必填元数据、逐文件变更、状态/验收边界与并行写入约定。',
    'docs/模型交火/代码审阅交接.md': '完整迁入画符原追加区与技术冲突审阅，补旧 QA/追踪过程摘要及本次整理记录。',
    'docs/模型交火/需求变更交接.md': '建立持续需求账本，逐项列本次文件去向与内容变化。',
    'docs/模型交火/世界观变更交接.md': '完整迁入 V1/V1.5 旧审阅，记录设定入口移动及未决边界。',
    'docs/guides/TRACKING_TROUBLESHOOTING_ZH.md': '提炼原生/串流诊断、低帧率、ADB/Gradle、USB 与语音进程所有权结论。',
    'docs/guides/VALIDATION_ZH.md': '集中验证命令、既有报告带时间快照、历史数量与真机局限。',
    'AGENTS.md': 'Codex/通用代理入口指向唯一交接规则，不复制整份规则。',
    'GEMINI.md': 'Gemini 入口指向同一交接规则，要求实际模型与时间、逐项文件变化。',
}
new_rows = '\n'.join(f'| 新增 | `{p}` | {note} |' for p, note in new_notes.items())
req_log = header('需求变更交接') + f'''## {TASK}-REQ｜文档归档治理与模型交火规则

- 任务 ID：{TASK}；模型：{MODEL}；执行入口：Codex；时间：{NOW}。
- 来源：用户要求清理 docs 和项目 Markdown，删除无用/过时资料，建立三类交接；每次追加、保留历史、写模型/时间及具体文档修改。
- 状态：文档整理与规则建立已实现；没有改变游戏需求或业务实现。

### 变更依据与范围

原文档散落 docs 根目录和 temp，多份过程报告的“当前”描述互相冲突。改为 guides/architecture/research/world 分类与唯一索引。仍有独特依据的架构/调研保留并注明范围；被新版覆盖的操作、旧任务提示词和阶段性 QA 合并后删除。三个账本从本次初始化起永久追加。

旧世界构建 brief 已完成外部提案用途：当前方向仍为线性关卡与轻量 RPG，首版全虚拟 VR；战斗外画符 P2，拿笔进入且禁止攻击/结印；PICO/MR/多人、完整剧情/敌人和 Android 语音均不能由设计文档推定完成。本次不重新选择世界设定，也不把技术现状自动变成新增用户需求。

### 完整文件清单

代码/Unity 场景/配置资产：**无修改**；工作区原有未提交实现未触碰。

| 操作 | 文件路径 | 具体变化/原因与替代去向 |
|---|---|---|
| 修改 | `README.md` | 链接全部转向新目录；替换旧诊断/四向入口；当前操作第2项明确子卡仅射线；第4项纠正后拉5厘米/前送9厘米/62分/保持0.45秒；文档导航指向总索引与交接。 |
{new_rows}
{move_rows}
{delete_rows}
| 移动非 Markdown 参考表 | `docs/temp/NarutoVR_12Seals_FeatureConfig.xlsx` → `docs/reference/NarutoVR_12Seals_FeatureConfig.xlsx` | 内容字节保持；移出 temp 并标明 SO/实际标定优先。 |

其他非 Markdown 资料：现有 DOCX 和 visuals 图片保持内容与位置。本地清理前备份：`Logs/{TASK}/backup/`；迁移元数据在同目录上一级 `manifest.json`，均不作为仓库历史入口。

### 验证与交接

- 实际执行：`python -X utf8 Logs/docs-cleanup-work/organize.py`；本地标准库检查所有保留 Markdown 的相对文件链接、源内容快照、目标路径边界、重复目标及迁移清单。结果随本任务后续验收条目记录；不能提前认作通过。
- 业务测试：本次未运行 Unity/头显/语音模型，文档引用既有报告明确带原时间和 hardwareTested=false。
- [代码审阅交接](代码审阅交接.md)保留原追加意见与技术冲突；[世界观交接](世界观变更交接.md)保留旧设定审阅。历史问题未在本次自动关闭，等待具体复核。
- 本次未调用外部子代理：用户此前要求当前会话处理；文档取舍由当前会话完成，文件迁移与引用盘点由本地确定性工具执行。
'''
out['docs/模型交火/需求变更交接.md'] = req_log

# Back up exact original bytes. Verify concurrent modifications before touching a source.
for path, data in original.items():
    if safe(path).read_bytes() != data:
        raise RuntimeError(f'Concurrent change detected, stop before mutation: {path}')
for target in out:
    if safe(target).exists() and target not in original:
        raise RuntimeError(f'Unexpected existing destination: {target}')
for path, data in original.items():
    backup_file = BACKUP / 'backup' / path
    backup_file.parent.mkdir(parents=True, exist_ok=True)
    backup_file.write_bytes(data)

table_old = 'docs/temp/NarutoVR_12Seals_FeatureConfig.xlsx'
table_new = 'docs/reference/NarutoVR_12Seals_FeatureConfig.xlsx'
table_bytes = safe(table_old).read_bytes()
(BACKUP / 'backup' / table_old).parent.mkdir(parents=True, exist_ok=True)
(BACKUP / 'backup' / table_old).write_bytes(table_bytes)
safe(table_new).parent.mkdir(parents=True, exist_ok=True)
safe(table_new).write_bytes(table_bytes)

for path, value in out.items():
    target = safe(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('x' if path not in original else 'w', encoding='utf-8', newline='\n') as stream:
        stream.write(value.rstrip() + '\n')
for old in set(mapping) | set(retired):
    target = safe(old)
    if not target.is_relative_to(DOCS.resolve()) or target.suffix != '.md':
        raise RuntimeError(f'Unsafe deletion target: {target}')
    # Snapshot was checked above; test again just before unlink for concurrent writers.
    if target.read_bytes() != original[old]:
        raise RuntimeError(f'Concurrent change at deletion: {old}; backup retained')
    target.unlink()
assert safe(table_old).read_bytes() == table_bytes
safe(table_old).unlink()
if not any((DOCS / 'temp').iterdir()):
    (DOCS / 'temp').rmdir()

manifest = {
    'task': TASK, 'time': NOW, 'model': MODEL, 'moved': mapping,
    'retired': {k: {'replacement': v[0], 'reason': v[1]} for k, v in retired.items()},
    'created': list(new_notes), 'beforeSha256': {p: sha(b) for p, b in original.items()},
    'afterSha256': {p: sha(safe(p).read_bytes()) for p in out},
    'xlsx': {'from': table_old, 'to': table_new, 'sha256': sha(table_bytes)},
}
(BACKUP / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'task': TASK, 'time': NOW, 'moved_md': len(mapping), 'retired_md': len(retired), 'new_md': len(new_notes), 'backup': str(BACKUP), 'files_after': len(out)}, ensure_ascii=False))
