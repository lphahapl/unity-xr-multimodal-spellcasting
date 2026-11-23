# 结印做对的瞬间，玩家怎样知道

2026-11-21 核查。首章裸手 VR 结印的音效与手部特效调研。下文的铜扣、印纹、时长均为本项目的样片提案。

## 四种值得借用的做法

| 玩家心里的问题 | 游戏实例 | 移到结印上怎么做 |
| --- | --- | --- |
| 刚才的动作认到了吗？ | 《Waltz of the Wizard》里，玩家打响指时听到一声脆响，同时看到光迸开。Meta 对开发团队的案例记录明确写到声音和光这两种反馈。[手势案例](https://developers.meta.com/vr/blog/how-hand-interactions-are-opening-new-possibilities-vr-developers/) · [PlayStation 手部追踪演示](https://www.youtube.com/watch?v=WXtR7_4IdaU) | 双手姿势稳定通过时，两截细铜线在手印外缘合成一条完整刻线，接合的瞬间响一下。玩家盯着敌人时也能凭声音换印。 |
| 我可以结束保持了吗？ | 《Returnal》的副射击用上行声提示充能进度，充满后给一声可单独辨认的提示和一次手柄脉冲。作者是 Housemarque 的 UI/UX 总监。[开发者讲解，Audio and haptics 段](https://blog.playstation.com/2021/05/11/unpacking-returnals-ux-design-gameplay-first-ui-retro-futuristic-tech-and-accessibility/) | 在需要教人保持的环节，可让一股轻气声逐渐收拢，两侧细线同步靠拢；只有保持判定通过才落定。丢失追踪或提前散手时，让气声散掉。裸手版本靠光和收尾音即可读懂。 |
| 这个印推进了几步？ | 《超级马力欧 RPG》每次成功的动作指令都会积累行动槽，槽满后才开放三人合体技。单步输入和整套能力可用有各自的可见结果。[任天堂动作指令说明与演示](https://www.nintendo.com/au/games/nintendo-switch/super-mario-rpg/) | 练习指定印序时，每个正确步骤补齐一笔印纹；也可给同一种敲击声换一个音高。只有当前配方确实前进才累加。多个术共用起手印时，先用通用确认。 |
| 整件事已经办成了吗？ | 《塞尔达传说：黄昏公主》开发访谈里，团队把解谜成功时听见熟悉短音句的快感，单独列为谜题体验的一部分。[任天堂开发者访谈](https://iwataasks.nintendo.com/interviews/wii/twilight_princess/0/0/) | 教学首次通过、永久习得术式，或机关真正开启时，给一个完整的原创铜铃乐句，印纹完整停留一拍。频繁的单印可以只用其中一下短击。 |

玩法上最接近本项目的是《Drakheir》。开发者用源自动画手印的组合来施法，试玩时发现同一个三角印有人做得小而棱角分明，有人做得宽而慢。这个案例支持把确认系在识别器收下动作的时刻。其公开说明没有逐印音效细节，具体的铜声与印纹设计取自本项目材质设定。[Meta 对开发团队的采访](https://developers.meta.com/vr/blog/how-hand-interactions-are-opening-new-possibilities-vr-developers/) · [项目的留音青铜铎设定](../world/KAO_GONG_LU_DU_WORLD_SETTING_ZH.md)

## 同一个印，先比较两种手感

- **铜扣合槽。** 双手停稳，手印外缘两道铜刻线合上；合缝的那一拍，双手之间出一声近、干、短的“嗒”，后面带一点铜质余响。第一版可从总长 0.10–0.18 秒试起。它适合反复听，也能给下一印留出安静的间隔。建议作为战斗首选。
- **聚气落定。** 玩家维持手形时，两手之间的细光往一处收，气声随之抬高；识别通过的一刻，气声截住，落一个清楚的“叮”。手指轮廓保持可见。适合第一次教玩家“还要稳住多久”，需要可靠的候选保持进度；单独的成印事件只足以驱动最后一下。

两版用同一个印、同一个音量和相同识别门槛试。一个简单的验收问题是，玩家眼睛盯着靶子，听到哪一下会放心换手形？再关掉声音重做一次，确认仅看刻线合拢也能读到同一个时刻。连续快速做三印后，下一句口令仍应听得清。保持原印时只响一次；做出有效但顺序不对的印，可以收到识别声，教学进度应保留匹配器返回的实际结果。这些是待头显对照的观察项。

## 对应当前工程

现有输入组件公开了“稳定成印时（印名）”事件，以及携带印名、采样时间和来源的全局通知。同一个有效保持只通知一次，中间印、当前配方以外的有效印也可能通知。因此它适合驱动第一种铜扣反馈。要表达“指定步骤正确”，还须核对任务目标或配方进度。结印实验台能显示候选和保持进度；正式战斗若采用连续聚气，需要确认其进度来源与实际战斗判定一致。[输入组件](../../Assets/scripts/logic/Skills/SkillInputController.cs) · [匹配器](../../Assets/scripts/logic/Skills/Core/SkillMatchEngine.cs) · [现行结印指南](../guides/SKILL_SHOWCASES_ZH.md) · [实验台显示](../../Assets/scripts/ui/SealLab/SealLabView.cs)

以先印后咒的火球为例。律印稳定，先响一次铜扣。随后口令匹配且施法条件满足，火球在右手成形，由技能的成形声与持握表现接手。如果先念咒、最后一个印触发成形，确认与成形可能落在同一帧；样片可以只保留铜扣的起音，让火核声接它的尾巴。火球已成形、火球已投出、永久学会术式，这三个节点各有自己的含义。[火球输入与持握流程](../guides/SKILL_SHOWCASES_ZH.md) · [技能音效配置](../../Assets/scripts/data/Skills/Audio/SkillAudioProfile.cs) · [成形声音入口](../../Assets/scripts/logic/Skills/Effects/SkillController.Audio.cs)
