# 共用印序时，玩家怎样选择释放短术

核查日期为 2026-11-21。本文记录外部案例和当时提出的 Quest 交互方案。纯结印术共用起手的试练已接入独立训练场，当前默认末印额外保持 0.6 秒。手感待头显实测。[训练场入口与操作](../guides/WRIST_MENU_INTERACTION_DESIGN_ZH.md) · [现行配方校验](../../Assets/scripts/logic/Skills/Core/SkillConfigurationValidator.cs)

## 三种确认方式

| 参照 | 实际行为 | 可以借用的部分 |
|---|---|---|
| 《Arx Fatalis》 | 点火术是 Aam → Yok。火球术在后面加 Taar。绘制期间每个识别到的符文出现在右上角，玩家松开 Ctrl 才释放所画的术。 | 输入终点由玩家决定，走到短术终点后仍能继续。两张配方和提交动作可以分别核对。[游戏手册，第 56–57 页](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1700/manuals/Arx_Manual_US_cropped.pdf?t=1590704297) · [Arx Libertatis 配方源码](https://github.com/arx/ArxLibertatis/blob/5b95e4c5ca9d583f1b11c085326979772645e0f3/src/game/magic/SpellRecognition.cpp#L524-L527) |
| Oculus Rift + Leap Motion 的裸手施法研究原型 | 手朝前握拳后，掌中蓝球逐渐变大。球长到上限，再张手才发出闪电。其他术也有明确的结束动作。 | 把备妥状态显示给玩家，再用可识别动作提交。若采用统一收招动作，需要先排除六印之间的过渡姿势及现有交互手势。[原论文第 III 节、图 1 和图 3](https://www.sbgames.org/proceedings2020/JogosSaudeFull/209323.pdf) |
| Unity Input System 的 Hold | 输入开始时可显示进度，持续达到门槛时触发，提前退出可以取消。 | 让玩家用“多稳住一下”表达释放短术的意图。此文档描述输入机制，阈值仍须针对双手结印测量。[Unity Interactions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Interactions.html#hold) |

另一个近似案例是 Quest 游戏《Drakheir》。开发者在 Meta 的访谈中提到，玩家摆同一手印的幅度与速度不同，团队放宽识别范围，并移除了容易相互误判的手势。访谈讨论的是手形误认，没有给出长短印序的仲裁时间。视觉相似和共享前缀是两类问题，分别需要调整识别模板和施法确认规则。[Meta 开发者访谈](https://developers.meta.com/vr/blog/how-hand-interactions-are-opening-new-possibilities-vr-developers/)

## 本项目建议

QMK 的 Tap Dance 给出了按键序列的另一种实现。每次敲击更新计数并重置计时，到期或被其他按键打断时结束本轮，再按状态选择动作。这个方法依赖离散按键事件。对于光学追踪，玩家已经开始换印和手被遮挡都可能暂时收不到下一个成印事件，因此建议额外检查末印是否仍在保持。[QMK Tap Dance](https://docs.qmk.fm/features/tap_dance)

假设甲术是「律→矩」，乙术是「律→矩→衡」。两者都可用时，结到矩印先发出正常的成印提示。甲术显示「稳住释放」，乙术同时显示「2/3 · 下一印 衡」。提示可以接到计划中的前三名候选上。关闭候选提示时仍可凭手边的蓄势反馈判断何时出手。

| 玩家动作 | 建议结果 |
|---|---|
| 当前可用列表中只有甲术，没有更长的分支 | 矩印确认当场释放甲术。 |
| 维持矩印，双手追踪持续有效 | 自矩印确认时开始额外计时，达到门槛只释放甲术一次。正式提交时再检查度力、冷却和其他释放条件。 |
| 在门槛前开始换印 | 手形离开矩印就停止累积短术时间。玩家可以在整串窗口内完成转手，确认衡印后按乙术配方处理。 |
| 确认错误的下一印、主动取消或追踪丢失 | 放弃这次短术待确认状态。同一帧既有换印证据又跨过时间门槛时，先处理最新骨骼再决定能否提交，避免迟发甲术。 |

第一轮可以对照**额外保持 0.4 秒和 0.6 秒**。两档都是本项目的试调值。现有律印的 0.07 秒用于确认手形，首章的 0.12 秒用于检测骨骼断帧，火球术的 8 秒和龙火之术的 4 秒是技能组合窗口。短术保持应独立配置。[律印模板](../../Assets/ScriptableObjects/手印识别配置/律印.asset) · [首章匹配设置](../../Assets/ScriptableObjects/技能目录配置/首章技能资源清单.asset) · [火球术](../../Assets/ScriptableObjects/技能定义配置/火球术.asset) · [龙火之术](../../Assets/ScriptableObjects/技能定义配置/龙火之术.asset)

在 Quest 上记录长术被短术抢发的次数、短术从末印确认到释放的延迟，以及刻意遮挡双手时的误放次数。让初学者和熟练者都尝试慢速换印，特别观察初学者停在矩印看右上角下一印时是否会误放甲术。如果阅读提示需要的时间已让短术显得迟钝，优先对照明确的收招手势。需要即时响应的急救或格挡技能，可以在配方设计时给它独立的印序。
