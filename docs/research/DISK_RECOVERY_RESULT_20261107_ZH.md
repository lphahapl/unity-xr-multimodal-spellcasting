# NarutoVR 恢复验收与现有功能清单（2026-11-07）

本轮从 11 月 2 日 Git 基线 `96f0a20`、第一/第二账号的本地工程记录、DS 包和 Downloads 原件重建。**没有找回故障前的整份 P1 场景或 Git 提交**。以下区分“现有功能”“本轮在 Unity 中验证”“故障前最终源码逐字节一致”。本轮所有 Play 测试均在桌面 Unity 6000.3.10f1 运行，无 Quest 头显连接。

| 模块 | 现在工程里有什么 | 可复查的依据与边界 |
|---|---|---|
| Quest 手部、结印与口令 | 使用 Meta 官方手；保留手形识别、单印/口令/组合配方、火球与护盾、抬腕菜单、移动和检查点；恢复技能目录、中文注释和类图。 | [类图与代码入口](../architecture/SKILL_SYSTEM_CLASS_DIAGRAM_ZH.md)；代码与场景在位，不把本轮桌面检查算作实机麦克风、手势或定位验收。 |
| P1 反馈试炼 | 单练习靶、8 米通廊、14×16 米战斗室、70 个连通导航点；探索/战斗切换，独立存档，视觉、音效与触觉服务。 | [P1 场景](../../Assets/Scene/KaogongP1Trial.unity)、[房间与池化 Play 记录](20261107-recovery/room-play.txt)。实测打靶不提前触发战斗、入房生成、退房停止受击、重进保血、恢复检查点和清房；未还原无记录的手摆坐标、台词和事件。 |
| 残机偶 | 4,058 / 2,250 三角面的双 LOD，共用蒙皮骨架；保留两种跑步、行走、待机、左右转、两种攻击、受击、瘫倒、坐地，共 11 段原 FBX。以 Addressables 2.9.0 的 `enemies/canjiou` 加载；含敌人目录、预热回收、导航、房间状态、弱点与削韧。 | [11 份原件逐文件 SHA-256](20261107-recovery/animation-files.tsv)、[11 段骨骼采样及控制器引用](20261107-recovery/assets-check.txt)。使用真实房间变体，每段采 9 个时点；10 段由 Animator 引用，带根位移跑步为备用，追击用原地跑。片段非静止不等于逐帧穿模、脚滑、头显动画观感全部验收。 |
| NeuGameMRTools 吸附改进版 | 在本地历史中找回改进源码及原脚本 GUID，无需拉上游覆盖。松手后按 Inspector 的 List 顺序平移、旋转；每段可覆盖耗时；支持 0 秒段、中途释放、到点事件和一次性完成事件。 | [吸附指南](../guides/SNAP_TOOL_ZH.md)、[示例 Prefab](../../Assets/Prefabs/Interaction/SnapSequenceDemo.prefab)、[31 项 Play 测试](20261107-recovery/snap-play.txt)。示例三个点耗时 0.5 / 1.0 / 0.5 秒。恢复的是项目使用的吸附模块，不包含上游所有工具，也未证明物理铜叶已绑定。 |
| 旁白与等待玩家操作 | 三种推进模式（音频、操作、两者）、逐句事件、句后延迟、CSV/XLSX 导入；交互成功时能发布当前步骤。P1 的“对话”保持历史所见空句表。 | [旁白指南](../guides/NARRATION_FLOW_ZH.md)、[本轮事件实测](20261107-recovery/dialogue-check.txt)。开始前的操作不缓存，未来/过期 ID 不推进，完成整段只通知一次。无参发布完成的是此刻的当前句，不能自动分辨哪个物体属于未来哪一步。 |
| 准望匣与铜叶 | 恢复 Downloads 最终 WristFit FBX、BLEND、源贴图、材质及分件 Prefab。匣身/绑带同属佩戴组件；朱砂扣帽独立网格。新脚本接 P1 右指输入，仅沿扣帽轴移动。五条实模槽的 25×3 mm 探测截面净深约 73.66 mm。复原已存多视图，以及同倍率铜叶八视图。 | [佩戴 Prefab](../../Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.prefab)、[源件测量](../../ArtSource/Zhunwang/WristFit/report.json)、[铜叶八视图](../../ArtSource/Zhunwang/CopperLeafViews/Views)。纵长 95.00 mm；官方左腕腕骨截面 52.63×36.49 mm，近/远腕带最小孔径 59.55×43.90 / 62.42×46.15 mm。Unity 测得扣帽行程 1.500 mm、匣身不动且只产生一次按下/释放。P1 保存的是场景实例，尚未以真机确认实际绑腕定位、前臂贴合或扣帽开菜单；未宣称五叶的榫锁已可玩。 |
| 音频 | P1 的音效、切换及反馈逻辑在位。探索音乐置空并禁用它的程序回退；战斗保留原有回退音。 | 《一寸山河》未找到匹配历史 SHA-256 的 v11，遵照用户决定不接 v10；《断榫破律》候选亦未冒认为旧成品。[音画规范恢复说明](../architecture/AUDIO_VFX_SPECIFICATION_ZH.md)。 |
| 目录、场景与资产引用 | 按新要求整理为 `Assets/scripts/{data,logic,ui}`、`Assets/Scene`，44 个 SO 放入 12 个以“配置”结尾的类型文件夹；P1 以及吸附、敌人导航、BGM 隔离场景齐备。 | 330 个已映射资产、14 个场景、44 个 SO（含教程说明），Unity 扫描 29,882 个序列化对象引用、0 个丢失脚本、0 个报告的失效引用。[机器报告](20261107-recovery/layout.json)。这不能查出人为留空的 UnityEvent 或场景摆放差异。 |

- [当前目录、全部配置类型与数量](../guides/PROJECT_LAYOUT_ZH.md) · [敌人动画、攻击和真实帧导航](20261107-recovery/enemy-play.txt) · [技能/迁移等 83 项编辑模式检查](20261107-recovery/skill-edit.json)。

## 哪些源码和以前一致

统计对象为 `Assets/scripts` 下 **195** 个 C#（193 个项目脚本和 2 个已归类的 Unity 教程脚本），含 Editor；不计 Packages/Library、模型、场景和文档。[逐文件清单（TSV，可用 Excel 打开）](20261107-recovery/source-provenance.tsv) 含当前路径、SHA-256、证据出处以及与 11 月 2 日基线的比较。每个文件只落入一类，依次优先 A、B、C；E 为本次工具或新实现。

| 类别 | 数量 | 能证明什么 |
|---|---:|---|
| A · 最终哈希吻合 | 5 | `NarrationController.cs`、`NarrationControllerEditor.cs`、`NarrationEvents.cs`、`EventCenter.cs`、`NarrationTable.cs`。当前原始字节与旧验收中的 SHA-256 完全一致，[五个哈希及旧记录行号](DISK_ROLLBACK_AUDIT_20261107_ZH.md)。 |
| B · 历史源码快照吻合 | 42 | 如 `SnapZone.cs`、`SnapItem.cs`、`EnemyManager.cs`、`EnemyObj.cs`、`GameManager.cs`、`CombatRoomEncounter.cs`。与一次成功写入的完整正文或从已知起点重放的补丁快照逐行一致，仅忽略 BOM、换行格式和文件末尾换行；没有故障前最终文件散列。 |
| C · 只能确认早期 Git 基线一致 | 73 | 与 11 月 2 日提交的同名原文件核对；是否字节一致或只有换行差异见清单。不能排除 11 月 2 日之后还有未找回的编辑。 |
| D · 已恢复/适配，不能证明全文一致 | 72 | 包含场景/路径调整、中文注释、修复、缺少完整历史文本等情况。如 `EnemyController.cs`、`P1RoomLayoutSetup.cs`、`P1AudioManager.cs`；其中探索音乐留空属于明确的新要求。 |
| E · 本次新增或改编 | 3 | `ZhunwangPressButton.cs` 为新的可按压组件；`DiskRecoveryChecks.cs` 为本轮验收入口；`ProjectLayoutOnce.cs` 从旧布局工具提取只读检查，移除了原有迁移动作。 |

另列 [工具与制作脚本核对表](20261107-recovery/tool-source.tsv)：Tools、scratch、ArtSource、docs/skills 合计 51 份已纳入版本的脚本，未混入上述 C# 数字。

逐字符相同、资产 GUID 相同、运行结果相同是三种不同证据。11 个动画 FBX 能逐字节核对原件；敌人 Animator、Prefab、房间布局、NavMesh 和接线由历史步骤重建，不能据此推出其序列化文件与故障前完全一致。

## 剩余边界与复查入口

- 原 P1 完整 YAML、全部旧 GUID、未保存的 Inspector 编辑和人工试玩手感仍无独立原件。不要用本轮运行次数置换旧工程的 162 项旁白、其他旧测试或真机验收。
- 音画规范原文、旧制作环境、历史交接全文只恢复到可举证部分；[旧交接标题定位](20261107-recovery/historical-record-index.tsv) 是可定位的样本，不是完整账本。旧音频署名/制作脚本按历史资料保留，不代表成品已挂入 P1。
- [原缺口 R01–R09](DISK_ROLLBACK_AUDIT_20261107_ZH.md) · [本轮逐路径变更清单](20261107-recovery/changed-files.tsv)。原始会话未复制进版本库。
