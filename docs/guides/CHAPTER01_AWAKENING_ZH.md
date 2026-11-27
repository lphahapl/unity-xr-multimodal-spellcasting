# 首章 · 苏醒

打开 [Chapter01_Awakening.unity](../../Assets/Scene/Chapter01_Awakening.unity) 后进入 Play。首次从试炼室醒来，后续启动读取 `Application.persistentDataPath/kaogong-chapter01-v2.json`。P1 练习场使用自己的场景与存档。v2 使用新文件名从新角色开始，原文件未删除，不做自动迁移。

体验“醒来”镜头，请从庭院场景的新局开场进入。申屠说“沈商，醒醒”时，视线从醒榻枕边缓缓抬起，再向左转向室内。第二次闭眼时恢复头部追踪，睁眼后即可自由环顾。已有后续进度时，可在隔离试玩副本中用新存档回看。[庭院场景](../../Assets/Scene/Chapter01_Courtyard.unity) · [桌面实录对照](../visuals/20261127_wake_camera/醒来镜头_前后对照.mp4)

开场会显示“正在按 ID 装配玩家技能”。技能就绪且角色成功抵达检查点后，即可结印、说口令或使用模拟施法。若技能加载失败，桌面调试面板显示具体原因和“重新加载技能”按钮。[首章技能资源清单](CONFIGURATION_GUIDE_ZH.md#首章技能资源清单)

首次落地若显示“等待落点可用”，检查该节点的地面层、地面高度及玩家身体所需的净空。首章会话保留已加载的技能和待恢复的检查点，游玩未暂停时每隔一秒重新尝试。排除障碍后，头显玩家会自动抵达存档节点，桌面也可点“重试检查点落地”立即尝试。游玩中手动恢复若遇到同类阻挡，玩家保留当前的位置和进度，排除障碍后再次恢复即可。

## 关卡路线

抬起左腕，右食指按朱砂扣打开教学。在腕匣选择探索移动。可用左食指指向地面测标记号（`SurveyCompass` 材质的白石/测标，即原“白石”）行走，也可指向其附近地面，驻留后传送或自动走到测标上。在测标记号上停稳半秒完成教学。腕匣打开时可再次按扣收起菜单。按顺序命中近靶和侧靶，等度力恢复，再练「衡印」护盾承伤与快推后撤。打倒试炼间的一具残机偶后，外门开放。

完成后撤教学会打开校靶内闸。站在门外仍可停留，玩家脚底跨入试炼间后加载残机偶，敌人出现并经过一帧显示后开始 `FT01` 的警告。紧接的 `FT02` 等本句音频结束和房间清空，再推进到外门。入口判定使用试炼间原有的区域盒，手、头和飞入室内的投射物均无法代替玩家脚底通过。

出门后沿脚边的白石/测标嵌线直走，前方方框下是校规庭双盘台，左侧石刻给出图形线索，右侧叶匣装着方圆规印铜叶（`leaf.fangyuan`）。右手轻捏铜耳，绕盘心移动指尖，然后松开。外盘转动会带动内方盘。横槽对上两端，方孔对准方销，两盘在容差内松手稳定后开盖。右手捏取铜叶，送到左腕指定导轨入口松手。吸附完成后，把左腕靠近门旁校验座。校验门在台后偏右。过闸后沿右侧步道走到测候台。左侧石渠中的水顺坡向下，水尺旁较宽的水股从高处落入低渠。若运行的是拓展庭院场景 [Chapter01_Courtyard.unity](../../Assets/Scene/Chapter01_Courtyard.unity)，场景还额外挂载了 `CourtyardTaskGuide` 与 `QuestGuideRoute` 任务指引路线。 [路线平面](../visuals/20261108_compact_court/00_路线平面.png)

外盘初始 45°，内盘初始 20°。只把外盘转回水平，内方孔仍差 25°。转盘和取叶都要求右指尖在把手或铜叶中心 5.5 厘米内捏取。腕匣面板支持射线选项；庭内机关要用手靠近。

右手捏住铜叶，把导向头送到左腕匣的侧槽后松手。也可以沿导轨推到中段或槽底再松手。整段 7 厘米导轨的侧向容差都是 4.2 厘米。两手短暂遮挡时，腕匣最多保留最后一次有效位置 0.15 秒。松手后，铜叶按三个随腕移动的目标完成吸附，入口、中段、槽底的时长依次是 0.10、0.18、0.18 秒。最后一段完成才登记入匣。随后把腕槽带到校验座 14 厘米内即可开门。[SnapZone 配置](SNAP_TOOL_ZH.md) · [开场交付说明](../architecture/开场教学与首枚铜叶/00_交接说明.md)

## 当前外景与窗户

校规庭已扩为 17.9 × 10 米，东侧可以停步观景。苏醒室和实战间共十扇木格窗，窗外能看见工官校器坊、石基与山脊；主线、谜台和校验顺序保留。打开首章场景即可看到 `首章外景拓展 · 窗庭远山` Prefab。若编辑器仍持有改前场景，请先保留自己的未保存工作，再重新加载首章文件。[扩建实景与验证边界](../architecture/开场教学与首枚铜叶/07_校规庭与量水道制作单.md#当前扩建版窗庭远山)

外景细化已接入 `首章外景细化`：直坡工官建筑、校器台、层岩、山松、连续谷地及远山，具有实际LOD、太阳阴影与沿渠水声。三座校器坊和侧置门庭已接通步道与承托；测候天仪只转动完整内环。

需要注意 **Build 中两个首章场景的实际差异**：
- **[Chapter01_Awakening.unity](../../Assets/Scene/Chapter01_Awakening.unity)**（Build 序号 0）：使用 `青灰薄云.mat` 天空、雾效范围 `45–185`、`OpeningGrass` 12 处；无 `StoryCameraSequence` 开场镜头、`CourtyardTaskGuide`、`DenseMeadow` 与 `FurnishingApproved7`；门庭内 `EnvironmentPolish.prefab` 的 `SceneTravelGate` 保持 `destinationScene` 留空且 `loadEnabled = 0`（当前不传送）。
- **[Chapter01_Courtyard.unity](../../Assets/Scene/Chapter01_Courtyard.unity)**（Build 序号 2）：使用 `MountainCloudSky.mat` 天空、雾效范围 `63–245`、包含 `OpeningGrass` × 13 + `DenseMeadow` × 1450 + `FlowerBank` + `ShortVerge`、`FurnishingApproved7` + `山间校院 · 场景陈设`、`CourtyardTaskGuide` + `QuestGuideRoute`，以及绑定至 `WK01` 的 `StoryCameraSequence`（"醒来"）；其 `SceneTravelGate` 已在场景实例上覆写为 `destinationScene = Assets/Scene/Chapter02_SuspendedPath.unity`、`loadEnabled = 1`（配 `SceneLoadingOverlay.prefab`），可直接跨章载入第二章。[全景、模型预算与配置](CHAPTER01_EXTERIOR_VISUALS_ZH.md)

## 在 Inspector 中配置

RPG 的「章节流程（必填）」绑定 `OpeningTrialChapter`。教学内容由腕匣的 `TeachingCatalog` 提供，物品奖励由 `RpgItemInventory` 提供，各阶段可用 `trialSkills` 临时借出术式。训练靶生命在 `TrainingTargetGroup` 配置；商店有独立组件和商品目录。见[职责划分](../architecture/PROGRESSION_RESPONSIBILITIES_ZH.md)与[章节流程接入](CHAPTER_FLOW_ZH.md)。

| 对象 | 接线与生效条件 |
|---|---|
| 第一章根物体 · OpeningTrialChapter | `sections` 对应 RPG 的阶段与检查点。每组选择对应的阶段资产，按稳定阶段 ID 接入路线，再逐句绑定字幕、独立台词编号、旁白和操作。命中事件按阶段监听，其他操作按当前句注册，在通过、恢复或禁用时解除。 |
| 双靶与试炼间 | 两句分别引用自己的靶。仅玩家施法效果回报该靶命中时计入。本段教学已开始时，提前命中指定靶会保留到对应操作句，仍需等旁白结束；进入下一阶段、恢复检查点或禁用章节时清空，不跨段计数。战斗组件在实战句开启，异步生成单只敌人。 |
| OpeningDefenseDrill | 慢弹指向发射时的玩家身位，盾值足够才发射并结算。后撤需要实际向后移出训练带。 |
| 校规庭 · OpeningLeafPuzzle | 双盘独立把手、奖励匣、唯一叶片、左腕槽和校验座均为场景引用。外盘每 180 度、内盘每 90 度校准，默认容差 5 度，稳定 0.6 秒。掉落两秒回到支座。 |
| [首章阶段配置](../../Assets/ScriptableObjects/关卡阶段配置/首章) | 基础术式仍按原阶段的 `trialSkills` 借用，室外解谜不另增基础术式授权；校验成功后按原奖励写入角色进度。当前另为雷光剑提供全部 20 段的临时测试授权，见下节。 |
| [正式旁白](../../Assets/Resources/Narration/Chapter01) | 音频名相对 `Resources/Narration`。纯叙述等音频结束，动作句等音频和指定成功事件都结束。操作或解谜卡住会按阶段播放提示，打开腕匣并选择「结印」可重听当前引导。 |

外门、奖励匣和校验门从阶段存档重建。已取未入匣时，唯一叶片回到已开的支座。入匣后随腕显示，校验门仍等玩家在校验座出示腕匣。阶段 ID 同时用于存档，发布后的调整应保留已发布 ID 或提供迁移。

## 雷光剑临时测试授权与永久解锁

首章 `OpeningTrialChapter.sections` 的全部 20 段 `trialSkills` 含 `thunder.sword`；而当前两个首章场景中 `OpeningTrialChapter.initialDiscoveries` 实际共配置 **6 项** ID（`fireball`、`shield.left`、`buff.voice`、`thunder.sword`、`breath.burst`、`breath.stream`）。「雷光剑」配方由首章清单按地址解析。

按照设计规范，玩家应在各阶段领取临时试用，永久解锁由成长流程记录（且存档文件为 `kaogong-chapter01-v2.json`）。需要配置永久获取时，从相应 `RpgStageDefinition` 的 `Unlock Skill Ids`（Inspector「解锁技能标识」）配置首次通关奖励，或从 [首章成长物品](../../Assets/ScriptableObjects/成长物品配置/首章成长物品.asset) 的 `unlockSkillIds` 配置物品激活奖励。物品独占技能不能同时配为初始或阶段奖励；`initialDiscoveries` 只表示了解；已有存档会载入自身进度，已领取的阶段奖励或已激活物品不会因为随后修改列表而自动补发技能，须另行设计明确的迁移方案。

> **当前序列化资产实况（截至 `e7d45dd`）**：两个首章场景当前均绑定 [默认玩家.asset](../../Assets/ScriptableObjects/玩家定义配置/默认玩家.asset)，其 `startingSkills` 目前实际已预置 `breath.stream`（龙火之术）、`thunder.sword`（雷光剑）与 `breath.burst`（飞炎术）；此外 [GATE_CHECK.asset](../../Assets/ScriptableObjects/关卡阶段配置/首章/GATE_CHECK.asset) 的 `unlockSkillIds` 当前配置为 `[fireball, buff.zi]`，[首章成长物品.asset](../../Assets/ScriptableObjects/成长物品配置/首章成长物品.asset)（`leaf.fangyuan`）的 `unlockSkillIds` 当前配置为 `[buff.voice]`。因此新开存档时玩家初始即可施放上述 3 项预设起始术式，这与最初“空初始技能、仅靠阶段/物品解锁”的设计草案存在差异，后续如需调整请在上述 `.asset` 中统一收口。
>
> 另外，两个首章场景中 `PlayerHands` 均采用 Meta 官方 XR 手部视觉方案，并未挂载腕表实体（`WristMenuController.playerWatchMount` 当前为空引用）；`RpgLocomotionController.exploreSpeed` 实测为 `1.25`。

授权验证与操作手感分别验收；桌面合成输入通过不等于头显真人操作已通过。

## 腕匣教学与铜叶

苏醒时可查两课。「火球术」需结律印并念“火球术”。「衡印」可以用左手结印或直接念“衡印”发动，任选其一。腕匣可随时查已开放的课，室内具体能施哪一术由当前试炼段控制。

室内固定教学板在打近靶和侧靶时显示律印，在护盾教学时显示**衡印**（左手单手 `shield.left` / 语音 `buff.voice`，对应 `ROOM_SHIELD.asset` 与 `SH01` 台词）。两块板共用 `Assets/Materials/Chapter01/SealInstructionAtlas.png` 的原始手部照片和文字。图集开启 mipmap，使用三线性过滤、4 倍各向异性过滤和 −0.25 的 Mip Bias，以便玩家从侧面走近时辨认笔画及手指轮廓。[教学板同机位对照](../visuals/20261112_model_display/室内手诀教学板.jpg)

庭院首次捏起方圆规印后，腕匣登记铜叶待入匣。沿腕槽完成三点吸附时永久解锁「衡印」的语音施法，可直接念出口令开盾。向校验座出示左腕会开启通往下一段的闸门。可在[首章成长物品](../../Assets/ScriptableObjects/成长物品配置/首章成长物品.asset)配置物品 ID、收集时了解的技能与激活时解锁的技能；教学名称、配方分组和动作提示另在[首章术式教学](../../Assets/ScriptableObjects/教学目录/首章术式教学.asset)配置。首章场景当前投放一枚方圆规印。

铜叶的入匣参数在场景 `OpeningLeafPuzzle` 的「铜叶入匣手感」中：导向头本地坐标为 `(0, 0.07, 0)`，骨骼捏合松开距离为 0.035 米；腕部 UI 的松开阈值保持原值。释放时检查叶尖或最后握持位置，入槽后保留 0.25 秒接触记忆，允许手指自然张开时退让最多 0.10 米。右手短暂失追最多 0.35 秒只冻结铜叶，超时释放；真正的 UI 占用仍终止抓取。首章腕槽关闭触发器自动接管，只在本驱动确认松手且已开放插叶时启动三点吸附。捏住不吸、拿远不吸，掉落两秒召回规则保留。隔离 Play 的 14 项回归已通过，真人手感仍需头显复测。

## 行走和受击反馈

首章探索步速在「移动与导航」物体的 `RpgLocomotionController` 上调整「探索步速（米／秒）」。当前为 1.25 米／秒，渠边平路 10 米的桌面实测时间为 8.00 秒。脚步按实际路程触发。斜碰墙角时可沿边走，松指或正面顶墙即停。战斗时看向地面刻线并自然伸出左食指，同点驻留约 0.32 秒确认传送。[行走配置](ENCOUNTER_LOCOMOTION_ZH.md) · [战斗落点实拍](../visuals/20261108_trial_interior/Unity_战斗落点.jpg)

护盾全额承伤时沿用盾的反馈。玩家实际气血减少时，双眼边缘浮起朱砂色，约 0.58 秒消退，中央仍能瞄准。要调整强度，在[默认玩家](../../Assets/ScriptableObjects/玩家定义配置/默认玩家.asset)的「受击反馈」调整「视口边缘提示强度」；设为 0 可关闭。[同机位受击前后](../visuals/20261108_trial_interior/Unity_受击前后.jpg)

## 修改对白与字幕

在对应阶段的 `NarrationController → 逐句配置` 中展开一句，就能修改「说话者」和「台词正文」。[四列总表](../architecture/开场教学与首枚铜叶/03_Unity旁白导入.csv)可用于核对主线，给某段控制器重新导入时请选择对应的[分组表](../architecture/开场教学与首枚铜叶/ImportGroups)。读表规则见[旁白导入指南](NARRATION_FLOW_ZH.md)。首章 29 句主线从控制器读取正文，7 句卡住提示配置在相应阶段的 `hints`。

卡住提示按当前阶段的 `hints` 顺序播放。满足等待条件累计 18 秒后尝试第一条，后续每次累计 25 秒。某条找不到音频时，Console 会给出阶段和条目序号，并在同一次尝试里继续找下一条。禁用提示音源会暂停等待计时。

试玩使用一个世界空间字幕板。走动、转头时，一行小字保持在视口下方。每页保留说话人，开场可见「申屠：沈商，醒醒。」等台词。长句优先在标点处分开，依据当前音频的播放进度换页。音频结束后清空，即使这一步还在等待玩家完成动作也一样。
