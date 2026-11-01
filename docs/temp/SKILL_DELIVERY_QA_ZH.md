# 技能框架与试炼场景交付验收

日期：2026-10-29。工程：`D:/UnityProjects/NarutoVR`。结果分为编辑器自动检查、画面检查和硬件待验；静态截图不作为帧率或真人识别准确率证据。

## 交付内容

- 原始基础测试场景 `Assets/SealCombat/Scenes/SkillTest.unity`：10 秒骨骼/评分缓存、纯印/纯口令/组合三种模式、8 秒双向匹配、取消、自身护盾和持握火球。
- 三个独立试炼场景：`FireballRange` 火球靶场、`ShieldTrial` 护盾试炼、`ComboSanctum` 组合施法试炼，均在 `Assets/SealCombat/Scenes`。
- 四个实际特效预置体：火球、护盾、命中爆散、地面阵法，位于 `Assets/SealCombat/Prefabs`；含流动材质、旋转符环、受限粒子和飞行尾迹。玩法与表现分离。
- 中文头显 HUD、世界说明和自定义 Inspector 字段。三个新场景将 HUD Canvas 排序提高到 100，修正世界说明牌盖住实时文字的问题。
- 本机 sherpa-onnx 中文关键词服务、合成及真人录音测试记录；玩家数据设计文档。

## 已验证

| 范围 | 结果与证据 |
|---|---|
| 技能匹配核心 | 47 项检查通过；`Logs/showcase-build-result.json` |
| 三场景绑定 | 3 场景检查通过：输入、三个配方、中文字体、特效资源、试炼组件与训练靶；同上 |
| 效果运行 | 20 项分组检查通过：真实预置体生成、尾迹切换、持握槽、当前手锚点、取消、护盾、初始重叠、一次命中、薄墙阻挡、高速球扫掠和清理；`Logs/showcase-effects-result.json` |
| 停用爆散模板 | 模板根物体及生命周期组件原先停用时，也会恢复显示与自动结束；包含在 20 项内 |
| 护盾试炼 | 实际运行 `SkillScenario.Update`，四次 30 点伤害得到 100→70→40→10→0，未吸收 20，完成后不再攻击；`Logs/showcase-shield-trial-result.json` |
| 画面 | 检查三张场景构图、四张实际特效预览及修复后的中文 HUD；`Logs/showcase-hud-preview.png` 与 `Logs/preview-*.png`、`Logs/vfx-*.png` |
| 语音模型 | 前阶段固定合成音/静音/负例等 6 项通过；`Tools/SkillVoice/logs/fixtures-report.json`。真实 WAV 回放曾通过 Unity 接收器激活护盾；不等于真人麦克风测试 |
| 真人录音 | 三份授权录音已离线检查，原件哈希不变；`Tools/SkillVoice/logs/user-audio-baseline.json`。结果见 [真人录音基线](PLAYER_AUDIO_BASELINE_ZH.md) |

效果检查使用隔离的临时玩家和模拟骨骼，不会冒充实际头显输入。护盾试炼使用场景中实际保存的测试参数，检查完成清理临时对象。

跨帧检查初次失败时，编辑器处于后台且 `runInBackground=false`，没有稳定推进游戏帧。测试脚本改为仅在自动检查期间临时允许后台运行，结束或程序集重载时恢复原值；之后 20 项通过。没有为测试永久修改 ProjectSettings 或放宽游戏碰撞判定。

第一次火球场景预览因着色器首次编译缺少部分发光几何，二次渲染正常。独立特效预览采用同步着色器编译并恢复原设置。预览不额外添加 Bloom、HDR 或 ACES 来美化结果。

## 保留与备份

- 框架开发前完整可恢复源码、配置、文档及录制备份：`D:/UnityProjects/NarutoVR_Backups/before-skill-framework-20261029-201814`。可重建的 Library/Temp 不包含在内。
- 增加场景和特效前的增量备份：`D:/UnityProjects/NarutoVR_Backups/before-skill-showcase-20261029-211158`，保存当时 SealCombat、文档与语音脚本。
- 本次终检 SHA-256 对比：原 12 个手印 SO、`Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/ProjectSettings.asset` 与框架前备份一致；基础 `SkillTest.unity` 及三个技能 SO 与特效前备份一致。
- 场景生成器和预置体生成器仅创建缺失资产，不覆盖已有自定义布局或现有视觉资源。

## 实际限制与下一步

1. 本轮进入播放时 SteamVR 的 `xrCreateInstance` 返回 `XR_ERROR_RUNTIME_FAILURE`，未建立有效头显会话。自动检查在编辑器运行完成；真人手感、麦克风路由、双眼画面、性能和持续运行需要连接头显后验收。未改动当前 OpenXR/SDK 配置来绕过这个状态。
2. 关键词检测不理解否定句。“不要释放火球术”真实录音也命中“火球术”；不能声称已经解决语义误触发。直接口令、显式“取消施法”和组合手印仍是当前交互约定。
3. “领域展开，诛伏赐死”仅扩展词表离线实验命中，没有绑定游戏领域效果。阵法当前只负责视觉，不限制移动或改变环境遮罩。
4. 当前护盾只吸收数值并返回余量，没有玩家 HP 承接该余量。能量、解锁、装备、版本存档属于 [玩家数据设计](PLAYER_DATA_DESIGN_ZH.md)，尚未实现。

使用入口见 [三个试炼场景说明](SKILL_SHOWCASES_ZH.md)、[基础技能使用说明](SKILL_TEST_GUIDE_ZH.md) 和 [中文语音接入](SKILL_VOICE_SETUP_ZH.md)。交付时停止播放，打开火球靶场，不在后台持续启动麦克风服务。

本阶段沿用已获准的内置协作代理分担场景、视觉及文档；父代理完成集成、实际运行检查与画面验收。未再次调用此前被拒绝的外部 Antigravity 代码访问。
