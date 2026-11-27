# T-25 · A4 十字铜带镇石正式接入

玩家俯看图与三维模型分别于 2026-11-25 21:40:14、21:40:39（北京时间）获得用户“通过”。主工程使用同一个镇石 Prefab 完成搬运、掉落、符力移动和篮槽锁定。[两处原批注记录](approval.json) · [工作台正式四向](http://127.0.0.1:4190/plans/ch02-t25-ballast.html?v=a4&view=production-four) · [顶视](Art/04-top.png)

## 主工程入口

| 环节 | 实际对象与行为 |
|---|---|
| 工奴出生 | 敌人目录的 `stone-carrier` 租约取得 [StoneCarrierProfile](../../../../Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset)。它仍引用 [BallastStone.prefab](../../../../Assets/Art/Enemies/CanJiOu/BallastStone.prefab) 的原 `BallastStone` 组件。 |
| 搬运 | 工奴进入专用 Animator 状态 `Base Layer.StoneCarry`，继续播放 [CanJiOu_WalkCarryingPerson.fbx](../../../../Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx)。[StoneCarrier](../../../../Assets/scripts/logic/Enemies/StoneCarrier.cs) 根据该剪辑的当前相位取[石根的局部位置和旋转](../../../../Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset)。87 项序列化采样与批准的姿态表逐项相同。[序列化核对](serialized-poses.json) |
| 工奴死亡 | 原 [BallastStone](../../../../Assets/scripts/logic/Enemies/BallastStone.cs) 脱离工奴，在其死亡的位置落下，启用原碰撞盒和刚体。 |
| 千钧符与 A 篮 | 玩家清场并登篮后，原 [BallastBasket](../../../../Assets/scripts/logic/Enemies/BallastBasket.cs) 开放对当轮石块的操控。实际施力并进入捕获半径后，石根转为运动学刚体，对准原槽心；随后触发一次 `onDocked`。 |

模型、材质和纹理位于 [BallastStoneA4](../../../../Assets/Art/Enemies/CanJiOu/BallastStoneA4/)。Unity 6000.3.10f1 在独立工程重新导入后通过 41 项检查。可见网格共 21,456 个三角形，使用 3 个 URP 材质、9 张贴图。原 Prefab 的 GUID、根节点、5 kg 刚体、1.05 m 盒碰撞体及脚本引用逐字段核对通过，替换范围限于显示子物体。[正式四向图](Art/four-cardinal.png) · [导入记录](Art/production-checks.txt) · [31 项资产散列](Art/production-integrity.json) · [根节点对照](root-contract.json)

## 运行时的手与石

工奴沿用原角色和原搬运动画的身前双手承托动作。在暂停、恢复和半速播放时，石根跟随同一动作相位。受击过渡暂停持姿写入；死亡后石块脱手，下一次租约从首帧创建新石。普通敌人、工奴死亡和同一工奴的对象池复用均已在 Play 回归。[实际 Animator 与生命周期报告](Motion/README.md) · [运行断言](Motion/play-checks.txt)

在独立 Unity Play 中从真实生成入口取得工奴，按原搬运剪辑的 30 fps 逐帧读取 Animator、石根及角色蒙皮。全段 87 帧，每帧 5,301 个角色顶点。三角面相交检测、闭合壳包容检测均为零帧。第 6 帧整身到石体最近，净空 **3.775 mm**。第 55 帧右手长件到石体最近，净空 **39.285 mm**。虽然其包围盒有 80 帧重叠，实际可见网格保持分离。左、右掌离石最远分别为 **8.405 mm** 和 **9.399 mm**，均在第 67 帧。[逐帧距离](RuntimeClearance/default/frames.csv) · [输入哈希与结果](RuntimeClearance/default/summary.json) · [复测工具](RuntimeClearance/verify_runtime.py)

审批时全身最小净空为 4.006 mm。正式 Play 蒙皮与审批捕获的最大顶点差为 38.52 mm，出现在第 51 帧、由髋部和两侧大腿共同影响的第 609 号顶点。本轮编辑器的构建目标为 Android，实际使用 `Mobile / TwoBones`。用生产网格的逆绑定矩阵在第 0、51 帧复算，前两项权重重现了当前蒙皮，所有权重重现了审批时的棚拍。本轮净空使用同一出生实例的 A4 模型、实际石根及角色蒙皮重算。切换 Renderer 的质量提示后，两份额外的 BakeMesh 输出仍与默认输出逐字节相同。数值覆盖原动作的离散采样帧。头显中的近距材质、原关卡照明和真实笔势仍需在设备上体验。

## 二章场景负责人接线

正式场景的原 `StoneCarrierPost` 已引用“示例落石”、巡逻点、A 篮、乘员盒与镇石锁定中心。场景提交者可将 [T25AltarRoomInstaller.cs](SceneHandoff/T25AltarRoomInstaller.cs) 放入其工作副本的 Editor 目录，打开 `Chapter02_SuspendedPath`，在编辑模式选择“Tools → NarutoVR → 第二章断衡悬道 → 接入已批准的祭坛工奴房间”。操作支持 Undo，由场景负责人审阅并保存。

该操作在祭坛接敌线后补齐工奴的房间登记，复用已有玩家、敌人管理器、出生点与篮槽，停用旧工奴和其身上的镇石占位。已有有效接线会复用。若另一位场景编辑者已改变接敌线、使用了出生点或留下多个候选房间，安装器会指出对应问题。实物由租约生成，场景中无需手放第二块 A4。升降机关应消费原篮的 `onDocked` 事件。

### 场景副本 Play 回读

从正式场景 `e82f24bdef5532e0a39fc9551860a00f7de6aa9c7a15277f842b1d1c8a33681f` 建立独立副本，保存补丁、重开并运行。玩家脚底进祭坛后，真实房间自动生出工奴。一次普通受击触发撤离，工奴在示例落石附近死亡。同一块石与同一刚体（本次实例 ID 分别为 −6296、−6292）落地，清场后登篮才开放符力。桌面模拟笔尖与手部输入，经过正式千钧符的锁定、施力和碰撞进入槽口。8 个显示网格在就位前后的标识全部一致，`onDocked` 只触发一次。[补丁和复现步骤](SceneHandoff/README.md)

原槽心世界坐标为 **(−9.121380, 80.245000, 21.000010) m**。以原槽局部轴读取四面实体内壁，从锁定后的可见网格量到 −X／+X／−Z／+Z 的净空依次为 **119.1983／119.2021／119.1926／110.0078 mm**，石底与槽底高度差 **0.0027 mm**。正式场景源文件在本轮交接时保留工坊两处遭遇，祭坛登记与占位停用由场景唯一写入人保存。原篮的持久监听数为零，本次实测止于锁槽事件。

[场景 Play 时序与 151 条物理采样](SceneHandoff/结果.md) · [单次事件与实例标识](SceneHandoff/运行证据/play-assertions.txt) · [本次逐项文件清单](files.md)
