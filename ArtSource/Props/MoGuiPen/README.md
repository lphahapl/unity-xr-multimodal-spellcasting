# T-21 沈商墨规笔

依据用户通过的方案 3。交付收合状态，保留朱砂笔锋、铜箍、木杆、尺纹、深靛缠线和有孔尾环。定稿中的开盖视图用于交代尾帽结构。持握位置按现有画符程序定在前段铜箍附近。[定稿三视图](../../../docs/visuals/沈商_墨规笔_方案3_三视图.jpg) · [Unity 对照图](Verification/MoGuiPen-Unity.jpg)

## 接入

- 在玩家层级实例化 [MoGuiPen Prefab](../../../Assets/Art/Props/MoGuiPen/MoGuiPen.prefab)，将 Prefab 根节点拖入 `GlyphPenController.penRoot`，把直属子节点 `Nib` 拖入 `nib`。
- Prefab 根和 `Grip` 的位置重合。笔尖在根本地 (0, 0, 0.108) 米，本地 +Z 是书写方向。Prefab 根保持米制 1 倍缩放。控制器直接驱动笔根，回落由已有 `holster` 或默认的上腹笔套处理。
- 近档长 243.8 毫米、2,228 三角面，远档 802 面。两档共用一张 2048×1024 PBR 图集，Android ASTC 6×6。笔保持无碰撞体，可沿用现有笔尖采样。
- 在正式 Unity 版本的隔离副本中，实体笔尖与 `Nib` 在两档和三种朝向下均重合至 0.1 毫米以内。GlyphLab 桌面回放通过真实 Prefab 取笔、千钧符、板面接触、指物、断线、移动仲裁和剧情方矩。正式章节布场、头显握持和画符手感待测。[Unity 回读](Verification/Unity-Readback.txt)

[可编辑 Blender 文件](Modeling/MoGuiPen.blend) · [Blender 几何和回读检查](Modeling/Blender-QA.json) · [Unity 游戏网格](../../../Assets/Art/Props/MoGuiPen/Geometry/MoGuiPen.fbx)
