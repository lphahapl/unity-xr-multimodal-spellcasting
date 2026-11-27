# 清圭 R02 / RevisionB 独立 Unity 样板（最终实测）

本目录最终可用版为 RevisionB，模型冻结时间2026-11-26T03:41:42+08:00。本目录是实际桌面 Unity 材质与装配样板；正式章节、主工程生产 Assets 与 R01 均未替换。最终 RevisionB 的模型、截图、视频、量规与握点数据在 `RevisionB/`，原冻结 R02 的中间证据保留。

## 打开与实际依赖

- Unity 6000.3.10f1 / URP 17.3，项目使用 Linear 色彩空间。桌面采集设备为 D3D11 / RTX 4050 Laptop。
- 将交付包导入当前项目的独立审阅分支，打开其独立 Lab 场景。场景控制器复用当前 `GameItemBase.Take / MoveHeld / Drop` 与真实 `SnapItem / SnapZone` 链；键盘/面板是桌面测试入口。按1拿取、2近手、3松手、4重拿、5移到孔口、6松手插入、R复位；方向键/PageUp/PageDown平移，Q/E旋转。
- **尚未绑定或验证 Meta/XR 头显抓取事件入口**。现有 Grip / InsertionTip / 碰撞体 / 独立 purpose 限定是后续系统接入数据。没有加入玩家通用杂物槽。
- 材质需要静态反射环境。包内 `Generated/CapturedFrontalStudio.cubemap` 来自实际场景软箱、条灯、地面经 ReflectionProbe 渲染后的读回，不是手画环境图。Lab 自动绑定它，不要求每帧更新探针。
- **只把 Prefab 拖到当前生产 Mobile 管线，不保证与样板一致。** 生产 `Mobile_Renderer` 的 `shadowTransparentReceive=false`；该配置会让 URP 清空透明队列的阴影参数。样板独立克隆 `QingGuiR02_DesktopReviewRP` 和 `QingGuiR02_DesktopReviewRenderer`，显式启用透明接收阴影。`QingGuiR02ReviewQuality` 在 Lab 运行期间切换到独立 RP，销毁时恢复前值。生产 Renderer 未改。
- 独立 RP 使用 2048 主光 Soft Medium 阴影、2.5 m 距离、单 cascade、4× MSAA；不开 Camera Opaque Texture。正式系统需单独评估此阴影预算及场景透明排序。

## 材质构成与可调范围

玉仍使用标准 URP PBR：真实环境反射、玉区域 clearcoat、实测厚度吸收、浅层散射和轻微内部密度层次。主预设 `Jade60`，克制预设 `Jade50`，可调上限 .65。金铜保持不透明。

每个 LOD 为 3 个 Renderer / 3 材质：原 Body 的金铜部分（queue 3001）、共享同一 Body mesh 的玉层（queue 3000）、独立实体 GoldWire（queue 3002）。玉层透明混合且写深度，晚绘金铜保持遮挡；背面金丝不会透过前表面玉层形成重复彩色轮廓。Body 保留实体投影，玉层不重复投影。

- Thickness：线性 R×0.12 m，来自模型真实射线厚度，不使用 AO 冒充厚度。JadeMask 玉1、金铜0。
- 吸收 (22,24,30)/m、透射强度 .55、浅散射 .25、内部颜色权重 .08、clearcoat mask .60 / smoothness .90。这些是视觉拟合参数，不是玉石实验测量常数。
- 内部密度使用物体空间 32³ R8 场，.85–1.15 弱变化；并非体积光追或真实内部结构扫描。
- GoldWire 使用真实金属 PBR、smoothness .68 和局部法线导数驱动的 specular AA，降低毫米圆丝的高光闪粒。不是通过删丝或关闭深度遮挡隐藏问题。
- 不用自发光伪造透光，不做屏幕折射，不申请全屏颜色复制。局部半透会混合背景，不能把该背景贡献等同于玉的自发光。交叉透明物体排序、XR 双眼和 Quest 实机尚未验证。

## 有限验证与边界

实际桌面链覆盖拿取、移动、松手、重新拿取、0/90/180/270 四向插入、一次完成和退出重插。使用正式 Console 的实际网格与 153 mm 孔，不用自造宽方洞。样板插深104.06 mm，底端 seatY=.9239401，底部台身余量4 mm。

106 mm 方插端旋转45°的对角149.9 mm仍可能进入153 mm孔；拒绝45°属于准线/方位规则，不能称其一定物理卡孔。120 mm禁用量规单独作为保守路径检查，不参与落桌接触。游戏接触体按导入三角面各高度区间的外包生成，保留圆角附近保守空隙；不会把肩部称作跨153 mm孔的实体挡停。

光学测试分别验证真实模型与2/8/25 mm已知厚度试片。保持原阴影，外遮挡能阻断入射透射；主灯关、金属掩码与黑背景全光源/环境/反射关是不同负例。实测证据保留HDR线性数据，照片以4×MSAA正常曝光展示。弱微米级投影并不作为独立成功门槛。

Android/Vulkan验证是材质 asset bundle 的真实着色器编译，覆盖玉opaque、玉transparent及Gold。不是已安装APK、头显实渲染、XR入口或Quest GPU性能测试。Android纹理候选为Base/Normal/MetallicSmoothness/AO ASTC6×6，Thickness/JadeMask R8；桌面样板使用未压缩纹理，平台差异须在设备上检查。

## 图像版本

`MaterialAB` / `Polish` 历史对照使用R01几何，不能当作R02成品。`FinalLook` / `FinalLookAA` 是修复前技术历史。`FinalLookReady` 是实体R02中间图，但当时透明接收阴影尚未开启；新修正图和最终B图另存，不覆盖历史。最终B背侧20–22通过临时持物yaw180迎向同一工作室灯与反射环境；Prefab、底端原点、插接坐标没有改变。最初背光偏暗的三张已归档到Logs/revision-b/rear-backlit-history。

真实本体正面为Unity +Z，相机从−X看到右侧。顺光/侧光/背光是光照方向，并不等于本体正侧背。静态手网格只提供1:1尺度参考，不代表真人握姿通过。

最终文件名、哈希、实测尺寸/握点、图元成本及各进程退出码由本目录实际 manifest 和 Logs 中本次运行结果给出；不能将源三角数当作双Body实际前向提交数。共享项目三份交接由父代理统一追加。

## 最终入口、证据和数值

- 打开包：`RevisionB/QingGuiR02B_UnityReview.unitypackage`。
- 场景：`Assets/Review/QingGuiR02/RevisionB/QingGuiR02B_Lab.unity`；物件：同目录 `QingGuiR02B.prefab`。可直接检查的物理文件复制在本目录 `Source/Assets/Review/QingGuiR02/`。
- `Source/ProductionInputs` 是实际使用的3份项目脚本比对快照；当前项目已有这些类，不要重复导入。B使用 GameItemBase SHA `b92701987ff2d0683ee1bd107970fc3fae58267bc199085e4bdf417a667675fb`，SnapZone/SnapItem与主项目逐字一致。原R02历史用旧 GameItemBase；必要同步及旧副本已单列。
- B首轮完整回归 PID71464 于2026-11-26T03:54:04+08:00 原生退出0；后续只做背侧摄影，PID81720原生退出0。全部命令与退出记录在 `Logs/workshop-master-tenon-qinggui-r02-20261126/unity/` 对应目录。
- 单根LODGroup，3组LOD各含Body/Jade/Gold三个Renderer；修正了FBX自动LODGroup重复管理同一Renderer的问题。
- 导入实际外廓：X110.027805 × Y350.000203 × Z106.796980 mm；底端Y0，根缩放1。230mm握位实测挂点为 (1.249997,230.000004,-0.114098) mm。
- 9206个导入LOD0顶点均被15段外包接触盒包含（20µm浮点容差）；120mm量规始终禁用为游戏接触体。
- 五次有意插入完成；重复请求不多发；全部插接期间实体和保守量规对正式台身的最大几何穿入均0。45°由方位规则拒绝。所有5种现有杂物槽均通过原槽尺寸检查拒绝本物件。
- 局部玉层实际背景响应最大线性delta .265625；铜背景delta0，金不透背景，背面金丝彩色幽影0。黑背景下所有光、环境SH、反射关后为0。
- B本体透射隔离测量：背光亮度和10837.4709；外挡后0，主光关后0。13331个完全金属像素最大透射1.9431e-5，低于.0002容差；玉厚度百分位约42.30/45.18/73.83/105.88/119.94mm。金丝投影差最大.02636719；保留Soft Medium实际投影。
- 三个最终Shader均有实际Vulkan编译输入及成功bundle，错误列表为空。不是Quest实测。

| LOD | 源三角数 | 实际前向提交三角 | Renderer/材质 |
|---|---:|---:|---:|
| 0 | 15,834 | 24,672 | 3 / 3 |
| 1 | 6,650 | 8,650 | 3 / 3 |
| 2 | 3,444 | 4,144 | 3 / 3 |

以上不含阴影、额外相机等额外通道。Body/jade共享同一mesh资产但会重复提交几何。不能用源15,834代称最终24,672。独立RP保留renderScale .8；照片读回1280×960，4×MSAA，诊断遮罩另用无AA。

## 观看顺序

1. `RevisionB/FinalLook/02_R02_Front35_Default60.png`：本版正常35°、主.60。
2. `03_R02_Front35_Restrained50.png`：同机位同光.50备选。
3. `07_R02_Crown_Transition_Close.png`：正面孔下铜玉终止边；`22_RevisionB_RearShoulder_CopperLip_Gap_Close.png`：用户指出同侧的背面近景。
4. `20_RevisionB_RearShoulder_NormalHandScale.png`：同侧正常持物尺度；`21_R02Original_Left_RevisionB_Right_RearShoulder.png`：旧R02左、B右，同光同曝光。
5. `RevisionB/FinalLook/RevisionB_MovingLight_AndTurn.mp4`：6秒/30fps，前3秒固定物件移动光，后3秒固定光下背侧小角转动。实际Unity渲染帧编码，无后期重绘。
6. `RevisionB/Images/02_NearHand_Front45_1to1.png`、`08_Inserted_Front45.png`：尺度与实际孔；`RevisionB/RevisionB_ActualSocket_Insert.mp4`：2.5秒实际原槽插入。

`RevisionB/MaterialModelShadowReady`和FinalLook的10–19是技术诊断图，不作为常态材质美图。源模型还有母模/薄边LOD/2K细节的已接受限制，见Model/RevisionB/model-report.json；源/GLB精确QA由模型侧记录，不冒称本Unity测试重新完成Blender QA。

R01保护范围428/428文件保持；原R02和B冻结核心各25/25字节保持。该结论只指明确保护清单，不声称整个共享工程未变化。未接正式章节、清场锁或Meta/XR入口；工作台和三份共享交接由父代理统一发布。

## 最后阴影修正与重现

背侧极近景的规律斜纹由自阴影偏移不足造成。单变量shadowStrength0对照消纹；depthBias .2仍有残留，.5为本次比较中消纹的最小有效档。最终主光为depthBias .5 / normalBias .05 / Soft Medium，透明接收与真实外物遮挡均保留，shadowStrength=1。Shader和所有模型/贴图字节没有改变。最终光学/重摄影进程PID83128于2026-11-26T04:36:33+08:00原生退出0；外挡诊断明确读回相同.5/.05并归零。

`FinalLook`与6秒影片已重采为此最终档；`Images`与2.5秒原孔视频保留03:54那次完整拿插回归的原始帧，当时depthBias .02，几何/材质相同。不得把两组拍摄时刻混为同一运行。最初暗图、斜纹原图、单变量对照和相机复用夹具错误exit3均留在Logs，不隐藏失败。

独立目录重现顺序：先运行Logs下`run-revision-b.ps1`（冻结SHA核对、完整拿插、Vulkan），再运行`run-revision-b-shadow-final-r2.ps1`（最终同机位摄影、同光背侧与外挡读回）。它们只允许专属Temp工程。正常手动体验直接打开B Lab场景即可。

模型/材质/Soft Medium路线已由父代理目检接受；当前极近2K铜界细毛边是已说明的采样限制，不继续修改冻结模型。此目录不记录用户或Quest实机通过。
