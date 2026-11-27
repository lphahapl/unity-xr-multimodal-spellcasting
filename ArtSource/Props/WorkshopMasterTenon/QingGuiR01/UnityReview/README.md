# 清圭 R01 独立 Unity 样板

冻结模型已完成 Unity 桌面实际抓插接口、原槽几何与材质光照验证；2026-11-26 完成。正式章节和生产 Assets 未改，R01/R02/R03 保留。美术批注与 Quest 实机验收仍待进行。

## 看图与运行

- 正确 C4 正 / 右侧 / 背三视：`FacingReview/01_C4_Front_Orthographic.png`、`02_C4_Right_Orthographic.png`、`03_C4_Back_Orthographic.png`。同姿态、同灯、同正交尺度（size .215m，1280×960）。
- 正45近手：`FacingReview/04_C4_Front45_NearHand.png`。灰手是项目 V3 手模型的 1:1 静态比例参考，不是已经追踪或实际握住。
- 正面金线与孔：`FacingReview/05_C4_Front_GoldLines_Grip.png`、`06_C4_CrownHole_Close.png`。
- 正面装配：`FacingReview/07_C4_Front45_SeatedPose.png`、`08_C4_Front_SeatedPose.png`。按已通过的零度落座姿态复位拍摄，没有旋转物件来修正朝向。
- 实插录像：`QingGuiR01_Insertion_Unity.mp4`，75 帧 / 30 fps / 2.5 秒。它是整轮真实接口测试中从**本体背面**记录的插入动作，不是正面材质展示。
- 可导入包：`QingGuiR01_UnityReview.unitypackage`；同内容源码/Prefab/场景在 `Assets/Review/QingGuiR01/`。

直接打开独立工程 `D:/Temp/NarutoVR-QingGuiR01-20261125`，进入 `Assets/Review/QingGuiR01/QingGuiR01_Lab.unity` 后播放。按 1 拿取、2 近手、3 松手、4 再拿、5 移到原槽上方、6 松手插接、R 复位；方向键/PageUp/PageDown 移动，Q/E 转动。也有屏幕按钮。

Unity 6000.3.10f1、URP 17.3.0 与 NarutoVR 当前交互依赖。导出包复用现有 GameItemBase / SnapItem / SnapZone 的 GUID，并非可独立导入任意空白项目的通用交互 SDK。主工程没有被自动导入该包。

## 朝向和握点

C4 本体正面是相机位于 Unity **+Z** 看向 -Z；高冠在画面右侧，有多条长金线。本体右侧相机位于 Unity -X。早期 `Images/` 沿旧相机约定命名，部分 Front 实际为背面，保留作为原始回归记录，发布审稿优先用 `FacingReview/`。

实际导入 LOD0 在根局部 Y=230 mm 的截面为 X `[-41.00001,+43.99997] mm`，Z `[-23.00002,+22.58588] mm`。最终 Grip 为 `(+1.49998,230,-0.20707) mm`；相对最初源坐标建议 `(-1.5,230,0)` 的修正合长 3.00712 mm。父代理已接受该实测锚点，最后摄影回读 old=new、改变量 0。Prefab 与原完整回归版本的唯一内容差异是 Grip 的这一行局部位置。

修正后真实 GameItemBase.Take / MoveHeld 的握点目标复核通过。**尚未绑定或验证 Meta/XR 真实抓取入口**；这不是只差真人试戴。无手型舒适度或双销兼容结论。

## 原槽与交互结果

实际导入外包约 `111×106×350 mm`，底端原点保持。15 段从实际三角形截取的外包 BoxCollider 覆盖 2,848 个 Unity LOD0 顶点；圆角、孔和斜冠的空白盒体空间是保守碰撞近似，不是精确手指碰撞。120×120×350 mm 量规单独禁用，不充当游戏实体碰撞。

对原 ConsoleSocket_LOD0 在顶部下 5 / 15 / 35 / 55 / 75 / 95 mm 水平发射 ±X / ±Z 射线，六层孔宽均约 153 mm；使用原 ConsoleBody 与 Socket 的 6,056 个三角形，没有替代方洞。样板插深 104.06 mm，底端 Y=.9239401 m，台身最高处 Y=.9199401 m，保留 4 mm 空隙。这是样板装配值，不是原设新增尺寸。

通过真实 GameItemBase.Take → MoveHeld → Drop → 再拿与原 SnapZone 对齐/插接：四个 90° 方向及取回重插共 5 次完成，保持已插状态不重复完成；持有、异物、45°索引不符、30 mm 偏位及横轴均拒绝。全部插接过程实体接触体和独立量规的最大检测交叠均为 0。

106 mm 方端的 45° 对角约 149.9 mm，小于孔宽，所以 45° 拒绝来自方位规则，不能称为必然物理卡住。正控制：120 mm 量规的 45° 交叠约 8.353 mm；实际物体过深 12 mm 检出约 1.975 mm 交叠，偏心 50 mm 检出约 26.502 mm。原孔全路径检查未以关闭碰撞换取成功。

物件不注册到本 lab 的通用工具带列表；原五种工具槽尺寸实际拒绝它，独立槽只接收指定物件。本轮未实现正式章节的清场、解锁或新夹销系统。

## 材质与验收证据

详见 `MATERIAL_VALIDATION.md`。使用真实厚度图和 JadeMask 的不透明主方向光透射近似，保留标准 PBR、法线、阴影与金属响应；不是自发光、折射或真实体积/屏幕空间 SSS。材质回读、完全金属控制、关灯、外部遮挡及不同厚度控制均通过；实际金属过滤边缘最大残量 2.98e-6，关灯和外遮挡为精确 0。

`MaterialModel/` 的 Front / Side / Back 只指顺光 / 侧光 / 背光，不是物件正侧背。浮点 HDR 统计保留；预览 PNG 对极小金属镜面高光会限幅。源厚度射线的 6 次未命中保守 120 mm 与 44,903 次长路径归一化截断均保留记录。

完整回归 PID 60884、首次朝向/握点补测 PID 42176、最终纯摄影 PID 47016 均原生退出 0。最终 Shader 与 Android/Vulkan 编译快照字节一致；该编译不是 APK、双眼渲染或 Quest GPU 性能证明。当前 2K 纹理为桌面未压缩审稿导入，正式 Quest 压缩/内存仍需工程集成评估。

证据根：`D:/UnityProjects/NarutoVR/Logs/workshop-master-tenon-qinggui-r01-20261125/unity/`。`full-regression-captured-input-sha256.json` 保存最初整轮输入；`captured-input-sha256.json` 保存最终交付与增补关系；`orientation-supplement/`、`orientation-final/` 保留精确握点、启动命令和 OS 退出码。模型核心 14 项、主交互代码/meta 10 项、原槽 28 个文件的来源一致性均单独记录。
