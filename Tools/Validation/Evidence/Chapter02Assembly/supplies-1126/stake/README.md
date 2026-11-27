# 二章定律铜橛接入候选

供二章场景单写负责人 01a1f895 接收。初验基线为已发布干粮的 `5fde4659`，当前接收基线为已发布吊篮的 `5dd20772`。两版共用 [scene.patch](scene.patch)。补丁只在原玩家上增加领取组件和一条指向原窄鞘的序列化引用。本包的正式章节发放时机与最终模型均待定。

## 装配与领取

1. 在独立工程打开 [二章场景](../../../../../../Assets/Scene/Chapter02_SuspendedPath.unity)，执行 **NarutoVR → 第二章 → 接入定律铜橛领取入口**。工具只装配当前打开的二章，保存交给场景负责人。已有相同接线时可以重复执行。使用补丁的接收方可以在仓库根目录执行 `git -c core.autocrlf=false apply --check Tools/Validation/Evidence/Chapter02Assembly/supplies-1126/stake/scene.patch`，确认后以 `--whitespace=nowarn` 替换 `--check` 应用，保留正式文件的 LF 换行。
2. 在原 `LinearRpgController` 对象上核对 `ChapterStakeSupply`。四个字段依次为原统一 `ToolBeltInteraction`、`TallSleeve` 的 `ToolBeltSlot`、`ChapterStakeGraybox` 和原笔使用的世界防坠下界。该组件在新局及无铜橛记录的旧档中均不创建道具。
3. 领取节点确定后，UnityEvent 可绑定 `ChapterStakeSupply.GrantFromReward()`。需要处理领取失败的奖励脚本应调用 `TryGrant()`，仅在返回 true 时结算奖励。窄鞘被占、尚未就绪或已领取时返回 false。成功后只有一个实例加入原带的 items，右手捏取与笔、干粮共用原输入所有权。
4. 锥尖向下贴近石地松开，冰蓝刻度出现，齿环开始旋转。离场恢复正常冷却，拔起可立即收场，靠近窄鞘按鞘口朝向松手即可回收。此变体的插入段直径 8 mm、长 55 mm，适配实测 13 × 16 × 57 mm 开口。宽杆、齿环和顶盘留在口外。正式美术应重新测量实际入鞘几何和碰撞体。
5. 默认半径 1.5 m、持续 12 游戏秒、恢复倍率 2，参数在灰盒 Prefab 的 CooldownStake 上配置。四术以现有技能 SO 的冷却组结算。安装没有授予技能或改变章节事件。

## 检查点语义

同一玩家依次采集各装备模块。干粮记录保留份数和实际口袋，铜橛单独记录所有权、所在口袋或世界姿态，以及是否插地和力场剩余游戏秒。持有从槽口拔出的铜橛时保存，读回到其来源槽；地上拾起且无来源槽的铜橛按世界姿态落下。插地读回后重新检查玩家是否在圈内，耗尽的铜橛保持插地并可取回。归鞘读回时场纹关闭。

## 重放

在私有工程副本从 5fde4659 开始。将 [ChapterStakeChecks.cs](../../../../../../Tools/Validation/ChapterStakeChecks.cs) 放入游戏 Editor 程序集，以 Unity 6000.3.10f1 启动三个独立 batchmode 进程，分别调用 `ChapterStakeChecks.Fresh`、`ChapterStakeChecks.Cold`、`ChapterStakeChecks.ColdField`。三次使用专用 QA 存档。第一进程装配并保存副本，后两进程从磁盘加载。在已应用补丁的 5dd20772 副本，将第一项换为 `FreshInstalled`，直接验证磁盘上的装配结果。桌面测试向现有手部输入入口送合成观测。二章测量四个正式技能定义经过玩家预约与提交的冷却；四术效果从施法入口生成的独立验证另见[原灰盒记录](../../../CooldownStake.txt)。Quest 佩戴操作、正式灰盒替换与发放节点由后续验收。

## 副本实测

| 场景基线 | 铜橛回放 | 结果 |
| --- | --- | --- |
| 已发布干粮 5fde4659 | [新局和两种冷读](5fde4659-play.txt) | 新局空鞘，显式领取成功；已插地与已归鞘均能重开 |
| 已发布吊篮 5dd20772 | [新版玩家与同一补丁](5dd20772-play.txt) | 两件共用腰带和存档；四项冷却场内 0.18 秒恢复 0.36 秒，离场 0.16 秒恢复 0.16 秒 |

[实际磁盘快照节选](checkpoint-excerpts.json)中，插地存档保留 11.62 秒场时，回收存档记录第 5 槽、场时为零。两份存档都保留中央袋的四份干粮。[文件散列与场景差异](receipt.json)记录了接收基线和候选。场景补丁增加两个序列化记录，修改玩家对象的一项组件引用，删除零项。

同一吊篮候选随后跑过[干粮新局 36 项、冷读 4 项](5dd20772-ration-regression.txt)，包括实笔与干粮双向互斥。按保留 LF 的方式应用补丁后，候选散列前缀为 `f85438c3`，独立进程再次[冷读插地存档](lf-cold-field.txt)通过。
