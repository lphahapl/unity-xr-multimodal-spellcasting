# 二章腕部状态与准望匣

供二章场景负责人合并。[场景补丁](chapter-wrist.patch) 的基线是二章入口负责人已关闭头部旧靶场 Canvas 的候选 `8bd10b60`，应用后为 `cdadb030`。请先保留该入口改动。差异仅涉及玩家的 `wristMenu` 引用、场景根列表和六条新增序列化记录。沿用玩家原有的官方左右手，石案的铜扣继续服务石案。运行脚本、手模、Prefab 和场景 .meta 均无改动。[目录文件指纹与对象清单](receipt.json)

右手抬至视线内、掌心朝向自己，稳定约 0.18 秒出现术法快览，放下即收起。快览沿用玩家在首章设置的开关。想持续查看，用右食指按左腕准望匣的朱砂扣，选「术法状态」。放下手腕只收起主菜单；状态卡可切换「跟随 / 固定」，点「关闭」才收起。

在二章隔离副本用 Unity 6000.3.10f1 执行了 21 项 Play 断言，涵盖新档、左右腕两条入口、状态卡固定和跟随、实际墨规笔抓取和战斗手势仲裁。输入为合成手部观测，尚待头显佩戴体验。[逐项读回](ChapterWristAcceptance.txt)

若后续二章已合入其他改动，可在副本从正式首章重新生成该增量。命令为 `python assemble.py <工程根目录> <现有二章副本.unity> <新的输出.unity>`。脚本校验已有引用、ID 冲突与本地依赖，只新建输出文件。生成后在副本比较对象级差异并实际运行测试。项目已存在腕菜单时直接核查已有接线。[装配脚本](assemble.py)

复测时将测试脚本放入隔离副本的 `Assets/scripts/logic/Editor/Interaction/`，使用独立 Unity 编辑器以 `-batchmode -force-d3d11 -executeMethod NarutoVR.SealCombat.Editor.ChapterWristAcceptance.Run` 启动，不加 `-quit`。检查默认开启的右腕速览，沿用本地开关且不写入偏好，保存使用随机 QA 文件名。[复测源文件](ChapterWristAcceptance.cs)

## 二章入口复验

同一腕部候选又运行了场景负责人的 Chapter02FreshEntryChecks.Run，22 项通过。独立新档走到石案，取笔、按石案铜扣、绘制「方直之矩」，模板匹配与修桥均只结算一次；过桥后抵达祭坛，正常触发首次战斗。[二章入口与断桥 Play 日志](fresh-entry.txt) · [新档出生相机实拍](01-fresh-entry.png)
