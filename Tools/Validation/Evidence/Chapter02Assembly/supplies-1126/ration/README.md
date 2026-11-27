# 二章中央袋和干粮

角色使用正式二章的官方双手、五口工具带和墨规笔。干粮使用已批的 TravelRationPouch 和单饼 Prefab。默认新游戏和无干粮字段的旧档都从空袋口开始。章节奖励、拾取点或测试入口可以在获得时调用 ChapterRationSupply.TryGrant()；Inspector 事件可绑定 GrantFromReward()。中央软袋已占用时发放失败并保留原物。

## 合并给二章场景负责人

- [清单](manifest.json) 列有 15 个源文件／元数据的目标散列及既有文件基线。可按文稿差异合并 [代码补丁](ration-files.patch)，或按清单逐项比对 [原始文件包](ration-files.zip)。共用的 GameItemBase 和 ToolBeltInteraction 已并入同期定律铜橛的既有 ReleasedInWorld 调用，后续有并行变化时请按语义合并。
- [场景补丁](chapter-scene.patch) 适用正式场景 SHA-256 以 cf4b60b2 开头的基线，落盘目标以 5fde4659 开头。改动已有玩家组件列表和 HMD 的子节点覆盖，共两处，另增九条序列化记录。沿用原场景 .meta。新组件绑定原玩家、现有五口中名为 SoftPouch 的真实中央开口与原头部；笔及其授权流程沿用已有接线。
- 对已推进的二章，先在隔离副本打开新基线，使用批处理入口 NarutoVR.SealCombat.Editor.ChapterRationInstaller.Install 生成这几处接线，再由场景负责人合并最小差异。原版代码包不含整份正式场景。开发中的编辑器场景由原负责人保存。
- 复测顺序为在同一个隔离工程分别以独立 Unity 进程运行 NarutoVR.SealCombat.Editor.ChapterSupplyChecks.Fresh 和 .Cold。使用 -batchmode、-force-d3d11 和 -executeMethod，由检查器退出进程。Cold 从 Fresh 写的专用 QA 存档启动。原样板可继续单独运行。

## 已验行为

[首次 Play 的 36 项断言](play-fresh.txt) 覆盖左右手互换、四块独立饼每块恰好回复 15 气血、松手还饼、短接触和失追、最后吸入口中时中断、空袋归中央口，以及双向笔／袋抢占。持袋或持饼使用现有工具带的排他输入权；物件松手沿已有放入／回原口规则处理。带上的干粮或正由玩家持有的干粮沿用正式检查点对随身物品的落位处理。

检查点记录是否持袋、已消耗的四位掩码和真实槽号或落地位置。存档剩两块后吃完，再恢复，生命和原来两块饼一起恢复。[独立进程冷读的四项断言](play-cold.txt) 再验证这份磁盘存档。测试使用 Unity 6000.3.10f1 和桌面模拟手部观测。Quest／PICO 的嘴部高度、捏取范围及双手遮挡仍待真机验收。
