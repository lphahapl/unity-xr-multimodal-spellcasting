# ③取用台 B1｜繁复青铜纹样定版

2026-11-26 用户确认「通过」，本版用于二章③取用台。依据 11 月 26 日选稿，应用 D 兽面细底纹、E 侧腹交缠龙纹、F 盘沿错银连纹。四面凸纹与包浆沿 B1 的铜材变化，盘沿细线与铜面齐平，槽口周边留素铜。

[可旋转定版及双销演示](http://127.0.0.1:4190/rig/?collection=t28-pedestal-b1&model=pedestal-b1-ornament-trial) · [二章同机位原版与试贴](B1-Ornament-Review.jpg)

## Unity 正式接线

独立变体 [WorkshopPedestal_B1_OrnamentTrial.prefab](../../../../../Assets/Art/Props/WorkshopPedestalB1/OrnamentTrial/WorkshopPedestal_B1_OrnamentTrial.prefab) 从基础 B1 继承 Mesh、Holder 和 LockedPins/OpenPins。只替换台体材质，共用原双销材质。由二章场景唯一写入人使用本变体替换现有白盒的可见部分，并沿用[二章接线说明](../Handoff.md)。正式场景当前为白盒承座快照，演示中的浅蓝方柱是该快照的功能主榫。

2K Base Color、NormalGL 与金属度／光滑度沿用同一个台体 UV 图集。Unity Android 平台导入使用 ASTC 6×6。Blender 5.2.2 高低模烘焙检查通过，台体为 9,572 三角，双销各 432。记录所列台体和销的低模坐标、拓扑与 UV 哈希在烘焙前后相同。

## 验证

- Unity 6000.3.10f1 在二章隔离副本通过 40 项桌面 PlayMode 检查，检查点包括单组清场仍锁定、两组清场后开销、拿取与放回。对照照片用同一位置、灯光和焦距，只在拍原版时临时换回原材质。见 [原版](UnityBaseline.jpg)、[试贴](UnityTrial.jpg)、[侧腹](UnitySide.jpg)、[取榫后俯视](UnityTaken.jpg)。
- Chrome 调试页读取上架资产，验证旋转、批注及两条销位移轨道，行程每边 86 mm。Quest 双眼和纹样在头显远处的稳定性待实测。
- [烘焙和低模哈希](OrnamentBake.json) · [几何 QA](OrnamentGeometryQA.json) · [贴图读回](AtlasReadback.json) · [Unity 检查](formal-play.txt) · [正式快照和交付散列](OrnamentTrialManifest.json)
