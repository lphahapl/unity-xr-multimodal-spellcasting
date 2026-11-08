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

菜单 **Tools → NarutoVR → 交互 → 验证吸附与序列** 从编辑模式启动独立的 `SnapToolTest` 空场景，在退出后恢复原 Play 启动场景配置。结果写入 `Logs/snap-tool-adaptation-20261104/check.txt`，只把最终 PASS 视为通过；包含合成 Meta 选择事件和真实物理触发，不等于头显手感测试。

## 来源与适配范围

来源：[NeuGameMRTools](https://cnb.cool/NeuGameMR/NeuGameMRTools)，提交 `6e5c58c905ec0ed1931b31c30eb517ddec9e1572`。原临时目录已不存在，本次恢复相同提交到本机诊断目录并保留原始源码。`SnapZone` 和其 Inspector 从上游适配，新增 `SnapItem` 与本项目检查、示例。上游 README 声明 MIT，但该提交无独立 LICENSE 文件；这里保留来源信息，不虚构作者版权文字。

本轮按吸附需求导入模块，没有导入其错误引用命名空间为程序集的 asmdef，没有降级 Meta/OpenXR。代码在项目现有程序集内编译，SnapItem 使用当前安装的 Meta Interaction SDK 公共类型。
