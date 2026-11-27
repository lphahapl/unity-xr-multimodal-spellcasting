# Quest 手模的食指根折角与拇指外凸

2026-11-27 核查。与本项目最接近的公开实例，是一套在 Godot 中实跑的 OpenXR 手。作者在自动赋权后也看到了指关节硬折，并给出回到 Blender 检查骨位和局部权重的做法。对我们截图里的左手，值得先查**食指近节到手掌的过渡**，再单独查**拇指内收时虎口和大鱼际的轮廓**。下列实例均来自作者或厂商，所用绑定各有差异。

## 三个可以对照的实例

| 实例 | 作者实际展示的内容 | 与本项目的关系 |
| --- | --- | --- |
| Blender → Godot / OpenXR | ClonedPuppy 公开了手部录像、骨架和可运行工程。示例手完全自动赋权，作者在 Godot 中发现部分指关节显得硬而有棱角。其工作流允许把骨轴切回 Blender 编辑状态，调整骨位、刷权重，再导出检查。作者也提到镜像后的拇指和小指常需单独调整。 | 三例中与食指折角最接近。可以照着做同姿、同机位复核。文中给出了返修入口，没有展示手工返修后同姿对照。[作者工程、视频和步骤](https://github.com/ClonedPuppy/Blender_OpenXR_Hand_Rigging#blender-openxr-hand-rigging) |
| Ultraleap 的 Unity 追踪手 | 打开页面中的 **Rigging Hand Assets from Scratch**。在 **Joint Orientation**，同一食指分别演示轴向正确和错误时怎样弯曲。在 **Weight Painting/Skinning**，另以腕部为例演示权重错给邻段的结果，以及重新分配后旋腕时的表现。 | 可依此分别检查关节转向和表面跟随。本项目可借用逐关节旋转、观察被带动顶点的方法。腕部例子的权重数值和 Ultraleap 的拇指关节约定只适用于它的示例绑定。[厂商教程与对照动画](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/rigged-hand-support.html) |
| 《Disassembly VR》的握持 | 开发者展示了掌握、双指拿取和单手转握，并说明为接 SteamVR 的骨骼输入专门做了掌骨绑定。Valve Index 控制器的掌骨姿态来自估算，慢慢握紧时，在 SteamVR 手模上仍能看到掌骨抬起。 | 这给虎口复核提供了动作参照。让拇指从张开、对掌走到持物，同时从掌侧和正面观察掌骨、拇指根与虎口。案例使用 Valve Index 控制器，未直接测量 Quest 光学手追或用户这帧的大鱼际形状。[开发者演示与绑定说明](https://www.reddit.com/r/virtualreality/comments/irceho/wip_full_body_physics_high_fidelity_hands_in/) |

## 用现有截图决定先试哪里

- **食指根。** 截图尖角处的表面权重约 90% 跟随食指近节，旁边内凹处约 90% 跟随腕。该帧食指掌骨与近节的三维夹角只有 6.87°。建议固定原始骨骼帧，从手背和侧面查看两点之间的网格与逐排权重，再比较局部调整前后的轮廓。两个像素的权重不能单独证明整片皮肤应当怎样分配。[同帧表面定位及热图](D:/Temp/QuestHandReview1127/silhouette/report.md)
- **拇指根。** 鼓包可见表面约 57% 跟随拇掌骨、41% 跟随拇近节。同一骨间折转在正面投影约 65°，侧面约 17°。建议保留对掌运动，先比较张手、半握、捏合时两视角的体积。如果只有内收时外凸，可再试仅随该姿势起效的局部形状修正，并检查张手是否仍自然、捏合时两指腹是否仍能会合。这个方案是依据本帧提出的试验，还缺同姿真实手部参照。[正侧视和两骨／四骨对照](D:/Temp/QuestHandReview1127/silhouette/report.md)

三例提供的是显示手的绑定与检查方法，均未把上述外观归为 Meta SDK 已知问题。对本帧，单独切换每顶点两骨或四骨，指根尖角和拇指鼓包周围连续 11 行像素的最外缘均相同。复核优先级应放在局部表面，玩法追踪仍可提供相同输入用于对照。[项目手模、驱动和录制范围](../../Tools/Validation/Evidence/QuestHandDisplay1127/README.md) · [轮廓实测](D:/Temp/QuestHandReview1127/silhouette/report.md)
