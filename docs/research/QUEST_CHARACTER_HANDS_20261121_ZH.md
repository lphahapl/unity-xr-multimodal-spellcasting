# 沈商全身动画与 Quest 玩家手

核查于 2026-11-21。本文记录上一版沈商的 Mixamo 骨架与追踪接线，供验证与复用。当前人物改为在 Blender 中从头建模，先审新脸，再决定全身与送绑。第一人称结印继续使用项目的 V2 双手。全身人物同时读玩家的有效手部观测，以腕部位置驱动手臂，以指节朝向驱动人物蒙皮。两种外观可以使用同一份输入。[工程依赖](../../Packages/manifest.json) · [首章手模与准望匣](../guides/PLAYER_HANDS_ZH.md)

## 骨架各自负责什么

| 来源 | 实际提供 | 沈商怎么用 |
|---|---|---|
| Quest 裸手 / XR Hands | 每手 26 个手部关节，包括掌心、腕、各指节和指尖。逐关节有有效性，位置相对追踪原点。肩和肘在这套数据的范围之外。 | 复用首章已经读取的手源、原点、时间戳与有效性。由 Animator 提供肩部基姿，按人物的上臂和前臂长度求肘部。超出人物臂长时保留骨长。 |
| Meta Interaction SDK 玩家手 | 已有骨架和手部蒙皮，每侧有独立的 26 关节驱动表。 | 头显近距离看 V2 指节、指环和纹路。左手现有准望匣继续承担按钮、注视、读数与菜单。结印使用测量到的关节和质量标记。 |
| Unity Humanoid / Mixamo | 动画库提供可预览、可下载的动作。有效的 Humanoid Avatar 支持跨人形骨架重定向，指骨属于需要单独验收的映射。 | 身体动画先采样，再按左右手独立覆盖上臂、前臂、腕和有效指节。缺单指时沿用其当前动画局部姿态，失去一只手时整条手臂回到动画。 |
| Meta Movement SDK | 官方样例含身体校准、骨架重定向、手 IK 与动画混合。 | 日后如果要跟随真实肩膀、躯干转身或获取设备支持的身体姿态，可独立评估此路径。当前的肘向和身体朝向来自估算。 |

来源见 [Unity XR Hands 数据模型](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/manual/hand-data/xr-hand-data-model.html)、[Adobe 自动绑定要求](https://helpx.adobe.com/creative-cloud/help/mixamo-rigging-animation.html)、[Adobe 动作库](https://helpx.adobe.com/creative-cloud/help/animate-characters-mixamo.html)、[Unity Humanoid 动画重定向](https://docs.unity3d.com/6000.0/Documentation/Manual/Retargeting.html)和 [Meta Movement 样例](https://developers.meta.com/vr/documentation/unity/unity-sample-movement/)。

## 手指、腕部和动画如何交接

- 从 Meta 的左、右手静置 Prefab 分别读取腕、食指根、小指根，计算掌面。对目标 Avatar 使用同一解剖标志，为腕和每根指骨保存各自的轴向补偿。运行时先将采样从 recordingOrigin 还原到世界空间，再施加补偿。这样沈商、Mixamo 和 SDK 手各自的骨骼滚转角都可以保留。[校准工具](../../Assets/scripts/logic/Editor/PlayerHands/TrackedAvatarBuilder.cs)
- 每手驱动腕及五指各三节。拇指的掌骨、近节和远节对应 Humanoid 的三段拇指。其余四指使用近、中、远节。SDK 的掌心、其余掌骨和指尖仍服务于第一人称和识别。蒙皮骨索引、SDK 枚举和 Humanoid 索引分别按本方语义取值。[追踪输出到人物](../../Assets/scripts/logic/Rpg/TrackedAvatarArms.cs) · [原始观测有效性](../../Assets/scripts/logic/SealLab/SealXRHandsSource.cs)
- 腕目标在臂长内时由两段 IK 到达。肘部弯曲方向参考人物身体的外侧、后下方。长短臂玩家、双臂交叉及贴脸结印可能出现虚拟肘与真实肘位置不同；头显手部和准望匣仍跟随测得的腕。全身模型脚底随现有玩家脚下水平位置和楼层移动，身体朝向允许头部有 30° 的自由转动。站姿比例、屈膝和手臂姿态应在头显复核。[人物摆位和相机显隐](../../Assets/scripts/logic/Rpg/TrackedAvatarPresentation.cs)
- 美术首次将新版混元角色交给 Mixamo 时选择了 Standard Skeleton (65)，首次回件每手实际只有拇指和食指链。分开手掌与五指后第二次上传，Adobe 回件有 65 骨、52 个人形映射和双侧各 15 个指节。鞋底至头顶高度 1.740 米。Unity 中的身体 FBX 与第二次 Adobe 下载件 SHA-256 均为 `39d0ff1ddd3098504833d0c81eb3285d4fc2e5e5f09d08c8cf8409203516cd7f`。逐指弯曲及跑步使用了实际蒙皮，追踪接线另行在两份首章场景副本中验收。[人物验收](../visuals/20261121_shen_shang_mixamo/UnityAcceptance.txt) · [Unity 回归夹具](../../Tools/Validation/TrackedAvatarHandsChecks.cs)

## 在 Unity 里接线

1. 复查旧版可使用[沈商跟手变体](../../Assets/Art/Characters/TrackedAvatars/ShenShangMixamoPlayerTracked.prefab)，或在 Project 选完整人物的 Prefab，执行 **Tools → 玩家手 → 从人物 Prefab 生成人物跟手变体**，选新的输出路径。生成工具在预览场景读官方手和人物静置姿态，并将第一人称身体网格存入同目录的 Body.asset。源人物缺手腕或任一指节时，会报告缺失的 Humanoid 槽位。
2. 打开 Chapter01_Awakening 或 Chapter01_Courtyard，在 Project 选跟手变体，执行 **Tools → 玩家手 → 在当前首章装配所选跟手人物与 V2 双手**。安装器复用两路玩家 Hand、现有功能准望匣和采样器，将调试骨骼线关闭。可重复执行或 Undo，由场景作者决定何时保存。
3. 头显看沈商的躯干、衣摆、双袖、V2 双手及左腕功能准望匣。第一人称单独裁去头面、模型手及整段烘在网格里的装饰腕匣，布料连接段根据 V2 小臂和人物原袖截面生成，跟两者共用姿态。头部相对躯干的前移距离预设 16 厘米，可调。要输出观众画面，在 **Tracked Avatar Presentation → 全身观众相机（单目）** 绑定独立 Camera 并输出到窗口或 RenderTexture。该相机显示含原始头、手、装饰腕匣的完整人物；实际读数、按扣和施法仍归本地玩家。两种视角的选择只改变渲染。
4. 需要跑步、待机或演出时，把项目的 Humanoid Animator Controller 赋给人物的 Animator。移动位移归现有玩家移动逻辑；角色 Animator 的 Apply Root Motion 关闭。上机检查左右互挡、合十、逐指握拳、腕内外翻、抬匣按扣和物理走动后瞬移。两种视角分别检查单套手、单个可见准望匣以及腕和袖口之间的间隙。

## 骨骼工作台

双击仓库里的 `SharedWorkSpace/rig/Open.cmd` 启动独立网页。可打开[残机偶](http://127.0.0.1:4190/rig/?model=sample)或[上一版沈商](http://127.0.0.1:4190/rig/?model=shen)。两个版本均附工程里的 FBX、颜色贴图和法线贴图。导入其他 FBX、GLB 或 glTF 时同次选择外部贴图和 BIN。残机偶版本另附可播放的待机动画。

1. 点击「导入模型」或拖入文件。每次导入一个模型，在对话框里明确选择新建人物集合或已有集合，再确认版本名。导入成功后，可从顶部两个下拉框切换人物和版本。导入新版本会保留旧版本。
2. 点击「版本批注」查看当前版本的记录，输入文字并点击「保存批注」追加一条带时间的批注。当前工作台把版本与批注写入 `SharedWorkSpace/config/rig/`，模型和附件写入 `SharedWorkSpace/assets/`。原 Chrome 配置中的历史资料可从「配置与检索 → 旧浏览器资料」迁入。
3. 蓝绿色圆点标出关节，连线表示父子关系。拖腕或踝可牵拉骨链，也可以切到「旋转单骨」。拖空白处环绕，右拖平移，滚轮缩放。密集的指节点可从列表按骨名选中，再点「聚焦关节」。撤销、重做与还原仅改变预览姿态。
4. 选择一根骨后点「权重热图」。暖色表示较高权重，可以检查本应固定的衣料或配件是否随手指移动。右侧的影响顶点来自浏览器渲染网格，UV 接缝和法线接缝会拆点。下方数字单独检查源文件中的全部蒙皮影响，含未加权、权重和误差超过 0.001、超过四骨、无效权重或关节引用。可见姿态按浏览器加载器保留的四个影响绘制。
5. 残机偶有 25 个骨骼节点，1,728 个原始蒙皮顶点，四类计数都为零。上一版沈商有 65 个节点，16,513 个原始蒙皮顶点，其中 1,468 个超过四骨。两份文件中的未加权、权重和异常与无效引用均为零。数值检查不能确定衣袖、表带或指缝在所有姿态下都附着正确。[浏览器实现](../../SharedWorkSpace/rig/app.js) · [沈商工作台](../visuals/20261121_quest_body_hands/ShenWorkbench.png) · [残机偶工作台](../visuals/20261121_quest_body_hands/RigWorkbench.png)

## 桌面验证与上机边界

Unity 6000.3.10f1 的 URP 相机分别渲染了完整观众人物和模拟人眼低头画面。旧的腕后 9 厘米裁口会留下模型烘入的左腕铜壳，俯视时会伸出一块尖片。最终第一人称版保留原袖和躯干的作者权重，并用单独的布料段连接袖口与腕部。双手前伸样例中，身体三角边长的第 99 百分位比率为原身 1.20、第一人称身体 1.21，左右连接段分别为 1.04 和 1.06。这个测试能发现拉长的面，仍需在头显上核对极限屈腕、遮挡以及不同身高时袖口的贴合。[低头画面](../visuals/20261121_quest_body_hands/HeadsetDown.png) · [观众画面](../visuals/20261121_quest_body_hands/Observer.png)

录制回放有 628 个有效单手样本。人物站位固定时 314 个腕目标在臂长内，另 314 个由对应侧臂长截断；人物腕与截断后目标的最大误差为 0.001 毫米内。这只衡量求解器的几何误差。该历史录制来自 SteamVR/OpenXR，未记录头显型号。两章在隔离工程中验证了安装、重复安装、相机显隐和原有腕匣引用，正式场景的保存由作者控制。[完整验收记录](../visuals/20261121_quest_body_hands/DesktopChecks.txt)

2026-11-21 19:59，从隔离 Unity 工程中移出早期工作台原型后复跑，人物接入的 121 项断言通过。现行工作台的骨骼操作和模型版本管理在 Chrome 中独立验证。
