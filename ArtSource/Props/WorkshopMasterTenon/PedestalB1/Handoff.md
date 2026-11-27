# T-28｜③取用台 B1 材质试装与接线

B1 已完成青铜材质、方彝式分层与正面兽面纹。2026-11-26 通过的 [繁复青铜纹样变体](OrnamentTrial/Handoff.md) 是二章③取用台的接线版本。下文的基础 B1 用于核对托点和接线。[纹样选稿](../../../../docs/research/T28_BRONZE_MOTIFS_20261125_ZH.md) · [工作台材质试装](http://127.0.0.1:4190/rig/?collection=t28-pedestal-b1&model=pedestal-b1-bronze)

## 二章场景接线

1. 使用 [已通过的繁复纹样 Prefab](../../../../Assets/Art/Props/WorkshopPedestalB1/OrnamentTrial/WorkshopPedestal_B1_OrnamentTrial.prefab)。场景唯一写入人停用③原承座 “主榫与承座” 的 MeshRenderer，保留原组件中的两个 BoxCollider。
2. B1 根位置贴合原承座，比例为 1。正面沿局部 +Z 面向院落。试装世界 Y 轴旋转为 317.08°。Prefab 的 Holder 用于对位，原 WorkshopTenonObjective.holder 继续作为实际游戏托点。两者世界坐标均为 (29.900000, 0.653000, 44.500000)。
3. WorkshopTenonObjective.closedJaws 指向新 Prefab 的 LockedPins，openJaws 指向 OpenPins。将旧视觉组停用。两组敌人清场后，现有逻辑切换视觉组，双销每边后退 86 mm。工作台模型用插值演示这段行程。
4. 原主榫与④中控引用继续由现有场景驱动。凹槽底部仍使用白盒盒碰撞，待正式主榫交付后复核细部配合。截图中可见的浅蓝方柱是二章快照中的白盒主榫。

## 验证

- 台体 9,572 三角面，双销各 432，每态合计 10,436。台体与销各一个材质槽，分别使用 2K 和 512 规格的颜色、切线法线和金属／光滑度烘焙。Android 使用 ASTC 6×6。[烘焙报告](BakeReport.json) · [几何 QA](GeometryQA.json) · [网格视图](Wire.png)
- Unity 6000.3.10f1 在正式场景的独立副本通过 40 项 PlayMode 检查，覆盖单组清场仍锁定、两组清场可取用、取走、复位和原④入槽流程。正式场景 SHA 保留快照值。[试装日志](formal-play.txt) · [托点对位](B1Import.txt) · [锁定实拍](UnityLocked.jpg) · [开放实拍](UnityOpen.jpg)
- Chrome 调试页实测旋转、批注入口与两个销的 86 mm 位移。真机灯光与正式主榫装配待联调。[浏览器检查](B1-BrowserChecks.json) · [交付散列](ReleaseManifest.json)
