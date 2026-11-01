# 技能架构重构验收（2026-10-29）

本轮把技能输入、配置、效果执行和界面分开，并完成已有四个技能场景的迁移。配置入口和新增技能教程见 [README](../README.md)。

## 结果与配置位置

- `SkillCatalog` 管理本场技能、缓存/消歧规则和取消口令；`SkillDefinition` 管理单个技能的输入配方。
- `ShieldEffectDefinition` 和 `ProjectileEffectDefinition` 管理效果参数与视觉；每次激活复制数值，已有火球不会被后来施法或资产修改改写。
- `SkillTestController` 处理输入生命周期；`SkillMatchEngine` 保留匹配、取消、冷却与证据消费；`SkillTestView` 根据生效配方显示中文面板和调试按钮。
- Unity 导出统一语音 JSON；Python 按配置建立关键词，不再把三个词写死在接收白名单和模拟入口。
- 不同机制可以继承效果定义并注册独立的生命周期对象。当前没有引入额外 DI/ECS 框架，也尚未实现通用多效果事务、玩家存档或成长系统。

三个展示场景引用 `Assets/SealCombat/Configuration/试炼技能目录.asset`，默认技能副本在 `Configuration/默认技能/`。基础 `SkillTest` 在 `Configuration/迁移/<场景GUID>/` 使用独立目录，保留旧基础视觉。旧 `Profiles` 资产保留兼容，日常应从实际目录引用进入配置。

## 本轮通过的检查

| 检查 | 结果 | 证据 |
|---|---|---|
| 原有匹配核心回归 | 47 项通过 | `Logs/architecture-final-editor-result.json` |
| 编辑态配置/迁移契约 | 17 组通过（配置10、迁移7） | 同上 |
| 三个展示场景接线 | 3 个通过；基础场景另由创建检查通过 | 同上及编辑器 build 结果 |
| 运行态配置/生命周期 | 17 组通过（效果配置11、控制器6） | `Logs/architecture-final-runtime-result.json` |
| 既有效果与展示预置体 | 20 项通过 | 同上 |
| 重复迁移 | 更新0场景，4个场景文件哈希均不变 | `Logs/architecture-final-idempotence-result.json` |
| 语音配置契约 | Python 16 项通过 | `test_configuration.py` 可重跑 |
| 当前导出配置 | 3个口令与收发端口检查通过 | `skill_voice.py --check-config` |
| 实际模型与 Unity 连接 | 合成录音 `shield.wav` → KWS“护体咒” → UDP → 当前目录 → 护盾激活通过 | `Logs/architecture-voice-integration-result.json` |
| 中文界面 | 游戏相机渲染并检查显示正常 | `Logs/skill-scene-preview.png` |
| 粒子速度告警 | 修复地面阵法1个模块；重新播放及20项效果检查通过，新增日志中告警0条 | `Logs/architecture-particle-runtime-result.json`、`Logs/architecture-final-vfx-result.json` |
| 既有模板与工程关键配置 | 12个手印模板、manifest、锁文件、PlayerSettings共15文件与备份一致 | `Logs/architecture-protected-files.json` |

语音代理还验证了原6段固定音频模型回归及第四个关键词的配置/模型加载；以上 Unity 集成检查使用真实模型推理和 UDP，没有通过直接注入字符串替代该链路。`shield.wav` 是合成测试音，不能据此推断真人麦克风准确率。

迁移检查覆盖：旧场景护盾200、投射物伤害40及运动/基础材质保留；旧共享技能不被改写；用户显式效果资产保留；空 View 恢复旧文字/字体；快捷键按稳定技能 ID 重接；再次迁移不覆盖新调参；丢失目录引用后稳定复用原资产。另验证 Unity 复制时未使用输入字段的 null/空串等价，而实际不同口令不会被误合并。

运行边界检查覆盖：自定义取消、过期口令、第四个技能自动出现并激活、禁用清空模拟输入、重新启用后拒绝禁用期间结束的迟到口令，以及配置快照和效果实例隔离。Esc 被保留给取消，重复快捷键会报告中文配置错误。

粒子告警来自地面阵法的速度模块：Y轴使用双常量，X/Z轴仍为单常量。生成脚本已统一模式；现有阵法仅将两个零速度轴等值转换，保持速度与其它视觉参数。另三个特效预置体与备份完全一致。

## 使用与验证边界

本轮没有佩戴头显验收；检查记录的 `hardwareTested` 均为 `false`。自动播放时 SteamVR/OpenXR 没有成功建立头显实例，不能把这里的通过项理解为新的真机追踪、双眼画面或帧率测试。自动检查曾临时开启后台运行，结束后恢复原值。

真实麦克风、快速结印与发射体验仍需要连接头显验证。当前 KWS 不理解否定句，“不要释放火球术”仍可能命中“火球术”；本轮没有宣称修复这一既有限制。领域、限制移动、玩家HP/能量/成长/存档也未在此次重构中实现。

## 备份与复查

改动前快照：`D:/UnityProjects/NarutoVR_Backups/before-architecture-refactor-20261029-215802`，包括本轮涉及的技能、手印代码及资产、工程配置、文档和语音脚本。路径另存于 `Logs/architecture-backup-path.txt`。

Unity 编辑模式可运行“技能配置 → 运行架构检查”和“运行旧场景迁移检查”。自动检查使用独立临时资源；迁移检查清理仅限带唯一 GUID 的自建测试目录。正常迁移不会覆盖已经配置好的技能目录。实际场景本轮的一次性迁移修正有独立完成标记，后续不作为每次启动逻辑执行。
