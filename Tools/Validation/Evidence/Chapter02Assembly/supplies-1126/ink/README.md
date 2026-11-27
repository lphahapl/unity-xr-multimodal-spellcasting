# 醒神松墨锭与冷铸青铜拓片灰盒

[独立样板和源文件](ink-files.zip) · [清单和散列](manifest.json) · [实际 Play 记录](play-ink.txt)

可直接在主工程打开[松墨锭与拓片样板](../../../../../../Assets/Samples/ToolBeltItems/InkRubbingLab.unity)并进入 Play。样板使用原五口玩家、ToolBeltSlot 和标准道具抓取。2026-11-26 在 Unity 6000.3.10f1 隔离复跑了 12 项桌面检查，再按清单将 8 个文件接入主工程。松墨锭和拓片的正式外观、贴图及工作台提交仍归原道具美术会话。

## 松墨锭

右手握墨锭，左手捏住盖并沿锭身推开。露出末段后，左手松开盖，重新捏住末段，沿预分线向侧面掰。小幅弯折可见墨段绕刻线转动；中途失追或松开会复位。沿锭身直抽和绕锭轴拧动都不会折断。滑程起测值为 31 毫米，弯折角起测值为 32 度，在场景的 PineInkPrototype 上可调。

灰盒有两段，露墨和折段默认只展示机械状态。testRecoveryOnBreak 是独立样板的调试开关，默认关闭；测试开启时用已有 RestoreMana.asset 每折下一段结算一次。嗅闻与折段选哪一个作为正式回复时机，等用户裁定。桌面试演用 F8 切换模拟右手，数字 3 定位墨锭，空格捏取；持物时 U 切换盖、B 演示折段。

## 冷铸拓片

使用普通 GameItemBase 和薄板收纳外廓。桌面用 F8、数字 4 和空格取片，保持捏合时可用 R 或 Q 旋转，靠近能容纳该姿态的空口后松手即可换槽。测试回读的是实际新槽的 LastDock。特殊用途待定。

独立批处理入口为 NarutoVR.SealCombat.Editor.ChapterSupplyChecks.Ink。它先由 InkRubbingLabBuilder 从现有 ItemInteractionLab 重建独立样板，再进入 Play，验证闭盖、推盖、直抽、折段、失追、单次结算和拓片换槽。12 项通过。样板验证使用合成手部观测，尚待戴头显校对操作范围。

另在隔离 Unity 中直接打开与主工程逐字节相同的已保存场景，跳过重建，12 项桌面检查全部通过。[已保存场景回放](saved-scene-play.txt)
