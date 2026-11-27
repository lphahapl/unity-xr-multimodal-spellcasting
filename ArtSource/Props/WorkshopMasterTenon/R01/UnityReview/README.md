# 主榫 R01 独立 Unity 样板

当前状态：首版可批注样板。Unity 6000.3.10f1 / D3D11 桌面接口回归通过，原生进程退出码 0。未改正式 Assets、章节场景、清场解锁或杂物目录。

打开已准备好的物理隔离工程 `D:/Temp/NarutoVR-T01Seal-20261124`，然后打开 `Assets/Review/WorkshopMasterTenonR01/WorkshopMasterTenonR01_Lab.unity` 并 Play。

`WorkshopMasterTenonR01_UnityReview.unitypackage` 和 `Assets/Review/WorkshopMasterTenonR01` 是同一套交付；需要本项目当前 `NarutoVR.Game.Runtime` / `NarutoVR.Game.Editor` 与 URP、Input System 依赖。不是不带项目代码的通用空白 Unity 工程。包内参考中控的 GUID 已独立重映射，FBX 与纹理字节来自正式资源，不会覆盖正式资源。

## 桌面操作

1. `1` 拿起③取用位主榫。
2. `2` 移到近手检查位；旁边的灰色手是本项目手模的 1:1 静态尺寸参照，并非真人追踪或握姿验证。
3. `3` 松手，重力和碰撞恢复；`4` 再拿起。
4. `5` 移到④正式中控槽上方，`6` 松手并尝试插入。先在口上方回正，再沿真实孔道下插。
5. 完成后 `1` 可从槽里重新拿出；`R` 回到独立展示位。

持物时方向键平移，Page Up / Page Down 升降，Q / E 绕竖轴旋转。也可点击 Game 视图左上面板按钮。两个台位仅用于独立样板，间距不代表第二章地图距离；③展示座用 0.85 × 0.85 × 0.65 m 尺寸代理，④保留正式 TenonConsole / ConsoleSocket 几何。

## 实现边界与装配值

- 主榫底端原点，Unity +Y 朝上，120 × 120 × 350 mm，根缩放为 1；720 / 256 / 96 三角形，单材质 2K PBR。
- 真实抓取底层调用 `GameItemBase.Take / MoveHeld / Drop`。独立样板控制器不接入玩家通用 `ToolBeltInteraction.items`。
- 专用准入派生自当前 `SnapZone`，只接收被指定的主榫；插值、所有权、完成和释放事件仍由原 `SnapZone` 执行。没有调用仅服务工具带的 `GameItemBase.TryDock`。
- 原 `ToolBeltSlot` 本身仍按尺寸收纳；实测现有五槽都因尺寸拒收主榫。独立身份限制来自样板注册入口和专用插槽，不声称已给全项目杂物槽新增 purpose 规则。
- 正式槽局部位置为 (-0.12, 0.92, 0) m。当前 LOD0 实际轴向孔宽扫描约 153 mm；资料中的约 146 mm 为名义参照，两者不混写，也没有扩大实际模型。
- 样板上口参考 y=1.028 m；台身在主榫投影范围最高 y=0.919940 m；座端 y=0.923940 m，保留 4 mm 台身余量，样板插深约 104.06 mm。该插深是本次独立装配选择，不是已定稿章节尺寸。
- 0 / 90 / 180 / 270° 接受；明显错转 45°、横放、30 mm 偏位、持物、外物拒绝。微偏先在孔上方回正。
- 逐段预检及实际播放逐帧同时检查真实 MeshCollider 与同一 6056 个三角面的 OBB-SAT，不关闭碰撞体来穿模。路径每段 33 个采样姿态；回归全程逐帧复查。
- `Physics.ComputePenetration` 单独对 45° 深入薄壁凹网格返回 0，独立三角 SAT 检出约 8.353 mm；过深例双方均约 8 mm。这个局限在日志保留，没有把单一返回 0 当成通过。
- 主榫 Rigidbody 使用默认 `Interpolation=None`，匹配现有直接 Transform 驱动。前一轮误设 Interpolate 会在吸附完成后回退到前一物理姿态约 1.418 mm；已修正样板配置，原运行时代码未改。

## 验证与未测项

拿取 → 移动 → 释放后物理落台 → 重新拿取 → 正式槽插接；四个正交向和再次插入共五次，有且仅有五次完成事件；持槽重复请求不重发，插接途中不能重新抢取，完成后能再次取出。所有测试插接帧最大检测穿透为 0。

本样板目前只由键盘、面板和合成接口输入驱动，**尚未绑定 Meta/XR 抓取事件，尚未接或验证真实头显抓取入口**。不是已经接好后仅欠真人测试。独立 Prefab 保留握点、入槽端、碰撞和刚体数据，便于后续接入。

`Images` 是 Unity 实际渲染，`Insertion_ActualUnity.mp4` 是实际回归插接帧。近手图只用于尺寸和外观比较。未执行 Quest 实机、Meta 真人抓取、人体工学、艺术定稿验收；未实现清场解锁。

完整依据：`D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-r01-20261125/unity/` 下的 `result.txt`、`process-start.json`、`process-exit.json`、`timeline.tsv`、`collision-oracle-diagnostics.txt`、`console-fit-analysis.json`、`captured-input-sha256.json`。早先失败记录保留在 `attempt1-physics-oracle` 和 `attempt2-rigidbody-interpolation`。

补充发布图见 `NeutralLighting`：同机位中性照明，冻贴图和全部 PBR 通道不变，用于与原偏暖照明比较。`Proposal` 是另外搭建的双压挡闭合/退开机械构想，不包含在已测抓插样板包中，也不代表玩法/美术已批准。
