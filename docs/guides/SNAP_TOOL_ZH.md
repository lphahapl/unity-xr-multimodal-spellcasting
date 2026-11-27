# 单点与列表序列吸附

本项目已按需接入 NeuGameMRTools 的吸附模块，保留 `SnapZone` 名称、原单点序列化字段、脚本 GUID 与动画曲线算法，增加顺序路径、Meta 松手接管和状态恢复。未导入整套抓取向导、计数器或分组显示管理器，也未修改现有 SDK 包。当前吸附点序列与结印技能序列是两个独立功能。

## 最快试用

1. 将 `Assets/Prefabs/Interaction/SnapSequenceDemo.prefab` 拖进需要的场景。
2. 展开实例，选择“吸附区域”。在 Inspector 将“示例道具”拖到“测试道具”。
3. 进入 Play，点“播放吸附路径”：道具按三个点依次移动并旋转。默认第一、三段各 0.5 秒，第二段单独设置为 1 秒，总计 2 秒。
4. 点击“停止并释放（当前位置）”可中途取消，或在完成后解除区域对道具的接管。

示例是纯吸附配置，尚未绑定头显抓取交互；实际拿取需要道具已有可用的 Meta Grab / HandGrab 接线。示例位置可以直接在 Scene 中调整，选中区域会显示路径线。

## 自己配置

推荐层级如下；区域与道具分别独立，不能互相作为父子：

```text
道具根节点               ← SnapItem、Rigidbody、已有 Meta Grabbable
  模型与碰撞体           ← Collider 可以放在子节点，共用根 Rigidbody
  抓取交互组件

吸附区域                 ← SnapZone、BoxCollider（Is Trigger）
  SnapTarget             ← 单点可选
  Point1                 ← 序列目标点，也可以放在区域外的独立层级
  Point2
  Point3
```

- Hierarchy 右键：**NeuGameMRTools → 交互 → 创建吸附区域**。
- 选中道具根节点：**Tools → NarutoVR → 交互 → 为选中道具添加吸附标记**。缺少 Rigidbody 时添加静态持有用的 kinematic Rigidbody，已有 Rigidbody 保持原配置。碰撞形状仍由模型自身配置。
- 道具仅支持根节点的单个 Rigidbody，不支持多刚体关节链、布娃娃。区域需要 Trigger；自动触发忽略道具的 Trigger Collider，使用实体 Collider。
- 默认自动触发要求 `SnapItem`，避免误吸附玩家、手或敌人。名称筛选匹配道具根名称，采用包含匹配；空字符串表示不限名称。取消标记要求属于兼容选项，此时仍推荐带 SnapItem 以处理 Meta 驱动。
- 默认等待 Meta 抓取松手：持有道具进入区域后先等待，松手且仍在区域内才开始。先离区再松手不会开始。显式 `SnapObject` 不绕过松手及占用检查。
- 道具上的 `SnapItem` 可填写额外需要暂停的 `Behaviour`，例如与吸附争夺 Transform 的 Animator/移动脚本。Meta Grabbable 和交互组件会自动收集，无需手填。

## 时间、顺序与曲线

| 配置 | 行为 |
|---|---|
| 单点吸附 | 沿用原 `_snapTarget` 和 `_snapRotationTarget`；未指定时分别使用区域自身的位置与旋转。 |
| 列表序列吸附 | 从物体当前位置开始，按 List 上下顺序到达每个目标；目标不得留空。 |
| 默认每段时长 | 对列表中未覆盖的每段生效，也用于单点模式。没有原版 3 秒上限；必须为非负有限值。 |
| 单独设置时长 | 当前列表元素的“本段时长”覆盖默认值，表示到达这个点的耗时。 |
| 时长为 0 | 立即到达，不额外等待一帧；仍按顺序触发到点事件。 |
| 旋转参考为空 | 列表模式使用该目标点的旋转。 |
| 动画曲线 | 每段重新从 0 到 1 求值，同一条曲线控制该段位置及旋转。 |

每段仍为原版算法：

```csharp
float t = Mathf.Clamp01(elapsed / duration);
float curvedT = snapCurve.Evaluate(t);
position = Vector3.Lerp(startPosition, targetPosition, curvedT);
rotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);
```

`Lerp/Slerp` 保留原版对插值因子的钳制；曲线超过 0～1 不产生越界弹跳。段末精确落到目标，不依赖曲线末端是否恰好为 1。空曲线回退线性。每段重新应用 EaseInOut 会在节点处减速后再起步，这是逐点吸附，不是连续切线的样条飞行。

开始时冻结全部目标点、时长与曲线。默认使用世界空间。对腕匣等移动部件，勾选「吸附过程中跟随区域移动」，路径会以区域的局部空间保存和播放。道具抵达后的佩戴显示由业务组件接管。目标点必须独立于被吸附道具。

使用缩放后的游戏时间，暂停时停止推进。长帧剩余时间会继续推进后续段，避免每段固定多等一帧。序列最大 256 点，用于限制零秒回调链的单次工作量。该路径直接控制 Transform，不做避障或路径寻路。

## 状态与事件

- “开始吸附”：道具成功获得所有权后触发一次；等待松手期间不会提前触发。
- “到达路径点”：每个点触发一次，参数为道具和 **从 0 开始**的列表索引；单点为 0。
- “整条吸附完成”：最后一个点到达后才触发一次。完成后 `IsSnapping=false`，`SnappedObject` 仍指向该道具，区域保持占用。
- “释放 / 取消吸附”：`ReleaseObject()` 或组件停用时触发；沿路径主动移出触发区域不会取消。此处比上游“任一碰撞体离区立即清空”的行为更明确。

占用期间不能被另一个道具替换，也不能被另一个区域接管。开始时暂停物理/抓取驱动；完成时可禁用碰撞体，直至显式释放。释放时恢复原来的启用状态、重力和 kinematic 设置，原来禁用的组件保持禁用；不恢复启动前的投掷速度。区域内释放后须离区再进，防止自动反复吸附；代码显式重新调用允许重试。

停止或组件停用会在当前位置释放，取消尚未发送的路径/完成事件。事件回调可以释放或启动新道具，旧任务不会继续覆盖新任务状态。`HideSelf()` 仅隐藏触发器和区域 Renderer，已开始路径继续；若要取消，调用 `ReleaseObject()`。

```csharp
using NeuGameMRTools.Runtime.Components;

// 显式调用以 SnapItem 解析道具根节点，不使用自动触发的名称筛选。
if (!zone.TrySnapObject(item))
    Debug.Log(zone.LastFailure);

zone.ReleaseObject();
```

## 源码与验证入口

- [SnapZone.cs](../../Assets/scripts/logic/Interaction/Snapping/SnapZone.cs)：路径、插值、所有权、状态恢复与回调。
- [SnapItem.cs](../../Assets/scripts/logic/Interaction/Snapping/SnapItem.cs)：根节点标记及当前 Meta SDK 抓取适配。
- [SnapZoneEditor.cs](../../Assets/scripts/logic/Editor/Interaction/SnapZoneEditor.cs)：中文配置、可撤销路径点创建和 Play 测试。
- [SnapZoneChecks.cs](../../Assets/scripts/logic/Editor/Interaction/SnapZoneChecks.cs)：隔离 Play 检查与示例生成。

菜单 **Tools → NarutoVR → 交互 → 验证吸附与序列** 从编辑模式启动按需重建独立的 `SnapToolTest` 空场景（不保留已生成的闲置场景），在退出后恢复原 Play 启动场景配置。结果写入 `Logs/snap-tool-adaptation-20261104/check.txt`，只把最终 PASS 视为通过；包含合成 Meta 选择事件和真实物理触发，不等于头显手感测试。

## 派生吸附区与反馈

`SnapZone` 可直接用于普通单点或顺序路径。自动触发、`TrySnapObject(GameObject)`、保留给 UnityEvent 的 `SnapObject(GameObject)` 和 `TrySnapObjectAtRotation(GameObject, Quaternion)` 共用同一套接管与释放流程。定向入口接收世界旋转。自动触发还按 Inspector 的名称与触发器规则选候选物。

生产派生例见 [ToolBeltSlot.cs](../../Assets/scripts/logic/Interaction/Items/ToolBeltSlot.cs)。新区域继承 `SnapZone`，按需重写这一方法。示例中的判断仅用于说明扩展点：

```csharp
protected override bool TryPrepareSnap(GameObject candidate, Quaternion? requestedWorldRotation,
    ref Pose targetPose, out string reason)
{
    reason = null;
    if (!candidate.TryGetComponent<NarutoVR.SealCombat.Items.GameItemBase>(out var item))
    {
        reason = "这类物件不适用";
        return false;
    }
    targetPose.rotation = requestedWorldRotation ?? item.transform.rotation;
    return true;
}
```

`candidate` 是已解析的道具根，`targetPose` 是末段的世界位置和旋转；顺序路径的中间点保留原有配置。钩子只计算准入和位姿。工具带在这里检查笔套身份及开口宽、厚、深，并计算落座位置。拒绝原因从 `LastFailure` 读取。基类校验位姿、占用、路径和刚体，统一提交物理、反馈与释放。监听者通过当前 `LandingPose` 读取实际终点；勾选“跟随区域移动”时，该终点随带平移、旋转。工具带以这一姿态保持物件并记录下次归还方向。

| 阶段 | C# 实例事件 | Inspector 原有事件及语义 |
| --- | --- | --- |
| 接管 | `OnSnapStarted(GameObject)` | 开始吸附。道具已经交由区域控制。如果驱动在物理接管阶段同步中断，入口返回 false，恢复原状态，两种渠道均不发布生命周期消息。 |
| 经过点 | `OnPointReached(GameObject, int)` | 到达路径点，序号从 0 开始。在此取消，后续点与整条完成不会发生。 |
| 完成 | `OnSnapCompleted(GameObject)` | 整条吸附完成，各发送一次。物件仍归该区域，之后可显式释放。 |
| 释放 | `OnSnapReleased(GameObject, bool completed)` | 释放 / 取消吸附。物理和原驱动已恢复。完成后释放为 true，开始后的中断为 false；对空闲区域重复释放无反馈。 |

音画可直接订阅所需阶段并在停用时退订。原 UnityEvent 和原 C# 订阅保持各自一次，回调内可以取消旧吸附、启动下一次。通过 `SnapZone` 类型引用操作工具带也遵循派生准入。拾取时若释放监听者已经为同一物件建立另一次成功接管，新的槽位继续持有它。

升级历史场景前关闭其编辑或保留未保存的修改。文本场景曾同时挂有独立 `SnapZone` 与 `ToolBeltSlot`。用 [迁移脚本](../../Tools/migrate_tool_belt_slots.py) 先打印变更数量，例如 `python Tools/migrate_tool_belt_slots.py Assets/Samples/ToolBeltItems/ItemInteractionLab.unity`，确认后在同一命令末尾加 `--write`。脚本合并同一物体上的一对组件，保留原吸附区的场景文件 ID、路点与序列化事件，并转接本场景中旧辅助组件的引用。项目内五个旧实例均在该样板，迁移后再执行显示零对。普通腕匣、章节与顺序演示 Prefab 沿用既有脚本 GUID。脚本只处理场景本地组件；预制体覆写或一对组件启用状态不同会停止，需单独核对引用。

## 工具带取放与进食样板

打开 [ItemInteractionLab.unity](../../Assets/Samples/ToolBeltItems/ItemInteractionLab.unity) 并进入 Play。玩家右手侧的笔套专收墨规笔，也是笔坠出世界后的默认归位点。其余四个开口都是通用槽。开场时粟饼在中央软袋，薄铜片在扁袋。空槽按松手时的方向检查物件宽、厚和插入长度，墨规笔也可以竖放进够深的方匣或窄鞘。

要调槽位尺寸，选中该开口的“吸附终点”，在 [ToolBeltSlot](../../Assets/scripts/logic/Interaction/Items/ToolBeltSlot.cs) 上设置 Width（X 向开口）、Thickness（Z 向开口）和 Depth（沿 −Y 的可用深度），单位为开口局部米。各部件网格的局部包围盒随实际姿态投影到这三个方向；有专门收拢外形的道具也可配置 Stow Envelope。仅专属笔套勾选 Pen Holster。Display Kind 指定首次陈列的类型；物件勾选 Start At Display Slot 后使用对应的空槽，已指定 Home 的沿用该槽。未配置陈列的地面物件保持原位。

[共享道具配置](../../Assets/scripts/data/Items/GameItemConfigSO.cs) 的 Kind 有六个值：通用（0，兼容旧数据）、墨规笔、行旅麦枣干粮包、醒神松墨锭、冷铸青铜拓片、定律铜橛。样板配置在同一场景目录下。类型用于首摆和笔套识别。四个通用槽只检查实际占用与外形。

在[统一道具试验场](ALL_ITEMS_LAB_ZH.md)可以试手持尺寸、地面拾回和归槽。普通工具带道具取出时按配置中的「取用尺寸倍率」等比放大，墨规笔单独配置。松手落地维持取用尺寸。槽口按收纳外形判断能否放下，吸附完成后恢复收纳尺寸。

要换槽，将道具按放入姿态送到另一个合法空口再松手。工具带 Inspector 的 Placement Reach 起测 **0.045 米**，按握点和入槽端中更靠近开口的一点判定。明确换槽优先于丢弃判定。下次从新口取出时，该口及放好的朝向成为归还依据。

普通松手时看握点与玩家当前头部在水平方向上的距离。Discard Reach 起测 **0.60 米**。小于这个半径时，道具会按取出时的朝向尝试回来源槽一次。在身体旁抬手、放低手，或玩家实际走动后松手，都使用当时的身体位置。距离达到半径就直接落地。来源槽被占、空间不足或吸附失败时也会落地。落地后捡回同一件道具仍记得其真实来源。从未从槽口取出过的道具，普通松手原地落下。短暂失追保留手持，恢复后重新捏住再松开才释放。

右手在粟饼旁捏取，将进食点朝向嘴部，连续停留 0.4 秒吃一口。把手移出嘴部半径，再送回，可以吃下一口。前两口减少可见的饼块，末口沿嘴部路径吸入后消失。每口回复 15 气血和 20 度力，上限按玩家配置结算。两项均已满时保留口粮。参数、反馈音与手柄脉冲在 [TravelRation.asset](../../Assets/Samples/ToolBeltItems/TravelRation.asset) 配置，数值在关联的两个效果资产上配置。

桌面在 Game 窗口按 **F8** 切换模拟右手。**1** 对准软袋、**2** 笔套、**3** 方匣、**4** 扁袋、**5** 窄鞘。按住**空格**取物，选目标后松开空格放下。**R** 俯仰 90°，**Q** 转向 90°。例如从笔套取笔，按一次 R 调成笔尖向下，再对准空方匣松手。**M** 送到嘴边，**N** 将手放在本次来源槽前方 0.14 米，**G** 拉远，**L** 切换失追，**K** 对准笔当前位置。远拉丢下的笔可在地上捡回。样板的 WorldFallBoundary 固定在世界 Y = −12 米，只有关键物真的掉到这条界线下方才尝试回默认槽。

自动复测入口是 [ItemInteractionLabChecks.RunBatch](../../Assets/scripts/logic/Editor/Interaction/ItemInteractionLabChecks.cs)。在隔离工程运行 Unity 时传入 `-batchmode -executeMethod NarutoVR.SealCombat.Editor.ItemInteractionLabChecks.RunBatch`，由检查脚本进入和退出 Play。结果在该工程的 `Logs/item-interaction-lab.txt`，末行为 `PASS END` 表示全部判定通过。

## 来源与适配范围

来源：[NeuGameMRTools](https://cnb.cool/NeuGameMR/NeuGameMRTools)，提交 `6e5c58c905ec0ed1931b31c30eb517ddec9e1572`。原临时目录已不存在，本次恢复相同提交到本机诊断目录并保留原始源码。`SnapZone` 和其 Inspector 从上游适配，新增 `SnapItem` 与本项目检查、示例。上游 README 声明 MIT，但该提交无独立 LICENSE 文件；这里保留来源信息，不虚构作者版权文字。

本轮按吸附需求导入模块，没有导入其错误引用命名空间为程序集的 asmdef，没有降级 Meta/OpenXR。代码在项目现有程序集内编译，SnapItem 使用当前安装的 Meta Interaction SDK 公共类型。

## 定律铜橛隔离样板

在[铜橛样板](../../Assets/Samples/ToolBeltItems/CooldownStakeLab.unity)从第 5 槽取出铜橛，锥尖朝下贴近脚前地面，松手。四面纱屏以铜橛为中心展开，方形边长 3 米、高 2.3 米，持续 12 个游戏秒。站在屏内向外射击可穿过；从屏外射入，火核、火球、雷光剑波或龙火会在入屏处受阻。拔起铜橛可立即收屏。原冷却加速场已由纱屏替代。

纱面用浅色经纬和较深的缝边呈现透光织物，能看清屏外的敌人。织法与透光层次参照马王堆出土的曲裾素纱单衣，四面屏是本项目的机关造型。[上海美术馆展览说明](https://www.artmuseumonline.org/art/art/visitGuide/xwgg/2026/07/04/2c906d9b9e0127fe019f3bdb331225cb.html) · [馆方实物原图](https://www.artmuseumonline.org/art/upload/image/2026/07/07/52027eb933db48878212eb52b86214ae.jpeg)

桌面按 F8 开启模拟。按 5 对准窄鞘，按住空格取出，按 G 对准脚前，松开空格插地。左右箭头横移两米，可进出纱屏。按 K 对准地上的铜橛，按住空格拔起，再按 5 并松手回鞘。全道具样板也陈列了相同的铜橛。[全道具样板位置](ALL_ITEMS_LAB_ZH.md)

[铜橛 Prefab](../../Assets/Samples/ToolBeltItems/CooldownStake.prefab) 保留握点、锥尖与齿环。插地与四面屏材质、高度在 [CooldownStake](../../Assets/scripts/logic/Interaction/Items/CooldownStake.cs) 配置。正式二章开局会把一根铜橛收在工具带窄鞘。旧存档没有铜橛时补发一根；已有铜橛按存档恢复原有位置与状态，插地的纱屏继续倒计时。[供给接线包](../../Tools/Validation/Evidence/Chapter02Assembly/supplies-1126/stake/README.md) · [本轮验收](../../Tools/Validation/Evidence/Chapter02Comfort1127/README.md)

2026 年 11 月的冷却场灰盒保留了插地、出入圈与四术冷却计时记录，供追查旧版行为。[旧版石台实拍](../visuals/定律铜橛_插地灰盒.jpg) · [旧版计时回归](../../Tools/Validation/Evidence/CooldownStake.txt)

## 双手拾取与地面道具

玩家可以用任意一只空手接触道具的实体表面后捏合，也可以先张手指向道具。出现细金线和末端小圈后捏合，原道具会移到该手的握持位置。远取距离在玩家 `LinearRpgController` Inspector 的「道具远取距离（米）」调整，初始值为 3 米。腰带近抓沿用各交互组件的 `reach`，正式工具带为 0.09 米，以握点、袋口或实际碰撞表面计距。薄长工具可以从杆身侧面拿起。

`PlayerInputController` 从左右手观测驱动公共候选查询。新道具沿用对应所有者的 `CollectPickups` 与 `TryPickup`，在可拿阶段提供目标。工具带将合法入袋的物件作为袋口握点提供，`SnapZone` 继续负责入槽、占位和释放。射线会检查遮挡。需要关闭某件道具的抓取时，可使用原 `DistanceGrabInteractionController.DisableGrab()`。开包后，另一只空手可以选中包内仍可见的薄饼。

二章的石案教学开放实体墨规笔。故意丢笔后，可指向地面笔杆取回，同一支笔可继续书写。归笔时将笔尖按原可收纳方向放到袋口。铜叶、工坊主榫和补给分别沿用它们的解谜、房间清理和发放条件。[双手拾取复测与逐类记录](../../Tools/Validation/Evidence/HandPickups1126/README.md)
