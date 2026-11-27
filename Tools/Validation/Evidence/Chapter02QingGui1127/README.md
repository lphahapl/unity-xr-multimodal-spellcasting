# 二章清圭主榫 R03 A 接入验收

在 [正式二章](../../../../Assets/Scene/Chapter02_SuspendedPath.unity) 清完③工坊屋顶与院内的守敌，靠近或用射线捏取台上的玉榫，带回④衡梁中控，方榫对正孔口后松手。到 [全道具场](../../../../Assets/Samples/ToolBeltItems/AllItemsLab.unity) 可直接在玩家前方五座石台的最右一座试拿。两场共用同一个交互预制件。

| 桌面验证 | 实测 |
| --- | --- |
| [③工坊至④中控](chapter-play.txt) | 41 项。清场前锁定、清场后两种抓取、掉落回台、错误姿态拒收、接受时回原尺寸、中断重抓、插底并供电。 |
| [重新启动 Unity 读盘](chapter-cold.txt) | 8 项。插到底的状态和供电一并恢复。 |
| [共享样件](sample-play.txt) | 12 项。两个场景的物件引用和三件材质，样板中通过实际工具带交互拿起，手持尺寸 1.4 倍。 |
| [美术与着色器](shader-and-art.txt) | 三件网格，40,024 三角形，玉体厚度与体密度均有绑定。Android Vulkan 两种立体渲染模式下，两个着色器各四个 Pass 共 16 种组合均能编译。 |
| [取用台实测](pedestal-survey.json) | 玉底世界高度 0.650 m，双支脚顶面约 0.6499 m。玉件与台框同向。④中控插入后底端低于孔口 104.06 mm。 |

[③台上实拍](pedestal.png) · [④插底实拍](console.png) · [全道具样台](sample.png) · [正光](front.png) · [侧逆光](back.png) · [同机位图像对比](material-comparison.txt)

[导入来源](art-manifest.json)列出美术资产与正式 GUID。[场景修改范围](scene-delta.json)比对了发布前后的每个 YAML 对象，只有③榫件实例及其复位点的朝向、高度变化。编辑器版本为 Unity 6000.3.10f1。关卡实测在禁用 XR 的桌面 Play 执行。模型仅含 LOD0，头显手感、玉质实机观感及帧耗仍待测。

[院内实际掉落](courtyard-floor.txt)在工坊院坪自由释放，碰到地面后等待自动回台并再抓取成功。[闭锁侧视](locked-side.png)可检查台框、双支脚与锁销。闭锁时两侧销与玉件的最短距离均为 39.22 mm。

章节沿用原有存档行为。方孔接受四个相隔 90° 的方向，读档后按孔底默认方向显示已插好的主榫。
