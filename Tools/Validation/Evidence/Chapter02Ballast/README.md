# 二章镇石搬运采样

这里的镇石取自祭坛运输实测中的同一实例，包含场景原有的两条木托。文件使用 Unity 局部米制坐标，+Y 向上、+Z 向前。[工奴原动作](../../../../Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx)

| 文件 | 用途 |
|---|---|
| [scene-stone.obj](scene-stone.obj) 和 [scene-stone.json](scene-stone.json) | 石根局部 180 顶点、60 三角面，含原材质分组、场景源网格子资源 ID 和导出变换 |
| [scene-fit-poses.csv](scene-fit-poses.csv) | 第 0～86 帧石根相对角色根的平移、四元数 xyzw 和缩放，原剪辑为 30 fps |
| [scene-fit-clearance.csv](scene-fit-clearance.csv) | 同帧整身、双掌、右长件与石网格的表面间距，单位米。含相交、包围盒、封闭部件包含测试与最近三角面 |

平托时右长件有 61 帧实际穿入。直接沿用运行时三实体灰盒的斜托变换，现场木托有 87 帧与右长件相交。重新按石体、铜箍和木托共五个封闭部件拟合后，右长件与石的最小距离为 50.18 mm，整身最小为 1.17 mm。右长件包围盒在 87 帧全部产生误报。右掌最大空隙 31.82 mm，石芯中心超出双掌前缘约 19～22 cm。姿态处于审稿阶段。[现场侧视与批注](http://127.0.0.1:4190/plans/ch02-t25-ballast.html?v=a2&view=scene-side) · [旧试位的木托交点](http://127.0.0.1:4190/plans/ch02-t25-ballast.html?v=a2&view=scene-hit)

在独立 Unity 工程使用 [采样器](../../Chapter02CarryPoseSampler.cs) 及 [复现脚本](../../Chapter02CarryPoseRun.ps1) 导出角色网格和腕骨。两次独立导出的顶点流 SHA-256 均为 `43E44A97A3BAFAD65CF703B4AAB83A316E799B9C8956C003EA4265CEB14F5A4C`。从工程根使用 [网格检查脚本](../../Chapter02BallastSceneClearance.py) 复测如下。

```powershell
python Tools/Validation/Chapter02BallastSceneClearance.py Logs/CH02-carry-pose-20261125 Tools/Validation/Evidence/Chapter02Ballast/scene-stone.obj Tools/Validation/Evidence/Chapter02Ballast/scene-fit-poses.csv D:/Temp/t25-scene-replay
```
