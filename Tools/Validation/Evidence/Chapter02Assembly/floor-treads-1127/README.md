# 二章门外崖肩与 A 岸栈道踏面

2026-11-27 已在正式 Unity 编辑模式增量保存并重开二章，场景 SHA-256 为 `2cb44e6278de75e4eb571290bd9fd2200087dcd065550b8e44811291fa7dcad5`。现场上层板面和物理地板同为 Y=80.00000 m，中层两者同为 Y=0.00000 m。[主编辑器重开回读](live-readback.txt)

| 原处重测 | 修前 | 修后 |
| --- | ---: | ---: |
| 门外崖肩、A 岸的普通石板低于物理行走面 | 15 mm | 0 mm |
| 两处石缝底低于物理行走面 | 20 mm | 5 mm |
| 自然落稳后，笔尾／笔中／笔尖段外形最低点与普通石板间距 | 约 15.71／15.13／15.24 mm | 约 0.71／0.13／0.24 mm |

板面材质为 WorkshopPaving，下面的 FoundationStone 同时构成缝底；两处的盒形支撑面与相邻地面齐平。石缝宽约 24 mm，缝底比板顶低 5 mm。原 20 个指下位置中，两处目标共有 13 点落在普通板面，1 点落在缝内；另外 6 点位于炉房门槛及谷底。独立复核追踪射线上的不透明、朝上踏面和不同细节等级，并在边缘另抽 96 点。[原网格与共享引用](baseline-geometry/mesh-bindings.json) · [边缘样本](baseline-geometry/edges.csv)

## 实际修改

- `Assets/Art/Environment/SuspendedPath/Chapter02EntranceShoulderTread.asset` 及其元数据是二章入口独立使用的门外崖肩显示网格。原网格仍由山景预览场景引用。
- `Assets/Art/Environment/SuspendedPath/CH02BankAWithoutFloorGuide.asset` 更新 A 岸现有场景专用网格。原场景安装器仍能回读 A 岸的六个材质槽和已清空的地面导航线。
- 两份网格仅调整板面与基底顶沿的局部 Y，分别 120 和 282 个顶点抬高 15 mm，并更新受影响的包围盒。索引、法线、纹理坐标、侧栏和物理组件均沿用原数据。[几何差异](mesh-delta.json)
- `Assets/Scene/Chapter02_SuspendedPath.unity` 中只有门外崖肩的 MeshFilter 改用了新的独立网格。主编辑器保存时也将已有公共拾取默认值 `pickupRayReach = 3` 序列化到玩家组件中；它在改踏面前的编辑器现场副本中已存在。逐项对账了 21 份正式拾取源文件、通路符、移动和触地采样源码；山景预览、B 岸、炉房门槛和共享几何包的散列均与修前一致。[正式文件与依赖](published.json) · [磁盘前后差异](scene.diff) · [发布备份](before-and-after.zip)

## 桌面复验

以同一套生产依赖、新档和真实旧档在禁用 XR 的独立工程分别回放修前与候选。用原支笔在两处从手中释放、自然落稳，读取笔身每段顶点与实体，贴近笔杆捏起并归回原鞘。上层经正常章节的已建通路符桥步行到彼岸，中层双向经过停靠吊篮和 A 岸，谷底与门槛亦复测。[同点配对](comparison.json) · [修前落笔](before/pen.csv) · [候选落笔](candidate/pen.csv)

门外崖肩和近岸残段之间（Z=60）、A 岸与换篮地坪之间（X=21.60）均双向走过，物理步高为 0。相邻地面保持旧铺装，实测边界两侧可见高度差分别为 15 mm 和 10 mm；中层所取 A 岸点位于缝内。停靠吊篮与固定栈道之间测得的最大步高约 0.013 mm。[候选逐点表](candidate/surfaces.csv) · [实际行走与拾取断言](candidate/checks.txt)

修前 4523 次、候选 4168 次 XR 监测均记录为空闲。Quest 串流使用的真实地板标定仍须真人触地测量。本次修复覆盖二章两块铺装。[修前 XR 状态](before/xr-lifecycle.txt) · [候选 XR 状态](candidate/xr-lifecycle.txt)


## 正式场景同字节复跑

主工程重开后将正式场景及已发布网格按 SHA 同步到关闭 XR 的隔离副本，完成 82 项通过记录，Unity 退出码 0。317 项生产依赖及夹具与候选一致，运行前后场景 SHA 均为 `2cb44e62…cad5`。第 0 至 2257 帧的 4639 次检查中，活动加载器、显示及输入子系统均为零。主编辑器全程在编辑模式。[正式点位与踏面](release/surfaces.csv) · [正式原笔数据](release/pen.csv) · [正式接缝与读档断言](release/checks.txt) · [XR 检查](release/xr-lifecycle.txt) · [发布对账](release/release-readback.json) · [退出码](release/exit.json)

