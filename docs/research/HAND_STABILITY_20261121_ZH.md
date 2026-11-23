# VR 裸手追踪、结印与工具笔尖的稳定性调研

核查于 2026-11-21，工程使用 Unity 6000.3.10f1、XR Hands 1.7.2、Meta Interaction SDK 207.0.1。本次只做源码核查和历史录制回放。对画面里的手，首要核对实际显示的是原始骨骼线还是玩家手模。对吐息，最值得先试的是稳定同一只手选中的印顶关节。对结印与画符，则应分别验证手势状态和笔尖轨迹。[项目依赖](../../Packages/manifest.json) · [Unity 版本](../../ProjectSettings/ProjectVersion.txt)

## 公开实现

核查日期为 2026-11-21。以下三款作品由开发团队公开说明了具体做法。观察其他游戏的视频可以发现抖动或延迟，单靠画面无法确定滤波器、阈值或输入设备。

| 作品或工具 | 开发者公开的做法 | 对应问题 |
|---|---|---|
| 《First Steps》 | 手整体几乎静止时保持前一姿态，小幅运动按变化量平滑。质量降低时从最近可靠的线速度和角速度作衰减外推，恢复后用约 0.1 秒接回实测姿态。指骨另用逐指置信度和角速度判断，团队在高置信数据中也观察到抖动。 | 可见的手需要兼顾稳和跟手。0.1 秒是这个作品的恢复过渡时长。双手互挡时能看见平顺的手，与系统此刻能识别结印是两件事。[Meta 的 First Steps 实装复盘，2021-04-28](https://developers.meta.com/horizon/blog/adding-hand-tracking-to-first-steps/) |
| 《Hand Physics Lab》 | Holonautic 团队说明，失去追踪时锁定虚拟物理手，追踪恢复后逐步跟回真实手位。如果失追时手正在桌面内部，直接跳到新位置可能导致剧烈碰撞。 | 持物、碰撞和接触恢复时，连续的运动比补齐每个姿态更重要。[Holonautic 开发者访谈](https://developers.meta.com/vr/discover/success-stories/hand-physics-lab-holonautic/) |
| 《Maestro》 | Double Jack 把延长音手势 Fermata 的收尾设计成在头前稳住拳头，让摄像头在动作变慢时可靠确认末姿。 | 识别动作可以针对明确停稳的时刻判定。开发者公开的是手势设计，该案例没有给出滤波系数或确认时长。[Meta 与 Double Jack 的 GDC 2026 分享](https://developers.meta.com/vr/blog/gdc-2026-day-1-hands-agents-performance/) |
| Unity XRI 官方裸手示例、Ultraleap | Unity 将点按附着点、直接抓取附着点、远距射线原点分别接到稳定化姿态。Ultraleap 的 Pinch Detector 对拇指与指定指尖之间的距离设置两个阈值，文档默认捏下 25 毫米、松开 30 毫米，中间带沿用先前状态。 | 前者处理连续空间坐标，后者处理离散状态的抖动。Ultraleap 的距离是其组件默认值，本工程的律印仍需按自己的样本标定。[Unity XRI 2.6 手部交互示例](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit%402.6/manual/samples-hands-interaction-demo.html) · [Ultraleap Pinch Detector](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/pinch-detector.html) |

同一个《First Steps》里，松手判定另有一套规则：高置信、连续 75 毫秒未捏握、抓起与放下阈值不同；恢复追踪后等 15 毫秒才重新检查松手。这解释了为什么手模的平滑时间不能直接拿来当结印确认时间。Microsoft MRTK 也提供只稳定射线原点和方向的 `StabilizedRay`，两者可以分别调节。Meta 的远指指南指出，捏下本身就会让手轻移，SDK Ray Interactor 专门处理这种瞄点偏移。[First Steps 的 Drop 小节](https://developers.meta.com/vr/blog/adding-hand-tracking-to-first-steps/) · [Microsoft MRTK StabilizedRay](https://learn.microsoft.com/en-us/dotnet/api/microsoft.mixedreality.toolkit.utilities.stabilizedray?view=mixed-reality-toolkit-unity-2020-dotnet-2.8.0) · [Meta 远指指南](https://developers.meta.com/vr/design/indirect_interactions_bp/)

## 当前工程的四条输入路径

| 玩家看到的问题 | 源码与正式庭院场景记录 |
|---|---|
| 手放着没动，线条仍抖或忽隐忽现 | 磁盘上的庭院启用了 `SealLabView.showBoneLines`。线条逐点读取原始关节，一根手指的链上有无效点时整条隐藏。识别端读取直接的 Unity XR Hands 数据，使用 Dynamic 关节更新和每手独立时间戳，采样上限 60 Hz；只更新腕根不会刷新手指的观测时间。实际头显运行包的显示配置仍需现场核对。[庭院场景](../../Assets/Scene/Chapter01_Courtyard.unity) · [绘线与显隐](../../Assets/scripts/ui/SealLab/SealLabView.cs) · [追踪源](../../Assets/scripts/logic/SealLab/SealXRHandsSource.cs) |
| 保持同一手印，识别状态来回跳 | 律印配置的分数阈值为 0.80、最低覆盖率 0.80、保持 70 毫秒；首章目录的歧义分差为 0.05。共用前缀的纯结印分支另有 150 毫秒追踪宽限、50 毫秒换印去抖。要先查评分、覆盖率、次优候选与无效区间，再决定是否调整状态判定。[律印](../../Assets/ScriptableObjects/手印识别配置/律印.asset) · [首章目录](../../Assets/ScriptableObjects/技能目录配置/首章技能资源清单.asset) · [匹配器](../../Assets/scripts/logic/Skills/Core/SkillMatchEngine.cs) |
| 喷火时瞄点突然偏向另一侧 | 调研时，每手从 26 个骨点中逐帧重选最高点。随后飞炎、龙火采用下表的 4 毫米门限，同一只手里两个关节高度接近时继续跟随旧关节。两手之间用 8 毫米高差带混合。龙火失追有 100 毫秒恢复窗口，期间停止火流、伤害和耗用。[吐息瞄准与持续判定](../../Assets/scripts/logic/Skills/Effects/SkillController.Breath.cs) |
| 远指或画线颤动 | 导航已有局部平滑，正式庭院配置的静止与快速移动时间常数分别为 35 和 8 毫秒，失效或跳变时重置。画符仍在设计阶段，计划采墨规笔笔尖、投影到固定画板。两者有各自的坐标和误差尺度。[导航平滑](../../Assets/scripts/logic/Locomotion/NavigationInputSmoother.cs) · [画符设计](../architecture/GLYPH_DESIGN_ZH.md) |

本机 SDK 已有 `HandFilter`。207.0.1 的 OpenXR 实现按配置频率过滤腕根位姿和各关节局部旋转，再按骨链重建。参数为空时直通。庭院的直接识别输入未发现接入该组件。若试用于玩家手模，应单设显示分支，并继续用带原始有效性和时间戳的观测判定施法。这样遮挡恢复时，画面可以平滑接回，而重复帧或外推姿态不会累积成印时间。[Meta v207 HandFilter](https://developers.meta.com/horizon/reference/interaction/v207/class_oculus_interaction_input_filter_hand_filter/) · [输入与合成姿态架构](https://developers.meta.com/vr/documentation/unity/unity-isdk-input-processing/) · [本地采样器](../../Assets/scripts/logic/SealLab/SealLab.cs)

## 同一批历史手部录制的离线对照

盘点本机 SealRecordings 的 14 段 JSONL，10 段无印名，4 段有标签。两段「子」缺少合格右手数据，其余「丑」「寅」各一段，共 629 个有效观测。记录来源为 SteamVR/OpenXR，未记录头显型号。选用新鲜、高置信、腕与右食指尖有效的样本，按观测版本和时间去重；无效或间隔超过 100 毫秒则断段，每段开头 250 毫秒用于预热。最终 607 个相邻区间，观测间隔中位 27.36 毫秒。下表 P95 表示 95% 的观测值不超过该数。

**单手印顶。** 以录制原点的 +Y 为上，逐帧比较右手有效关节。严格取最高时，607 个区间里更换关节 37 次，其中「丑」36 次、「寅」1 次。换点的区间内，最高点相邻位移中位数为 **86.6 毫米**，新关节相对旧关节的同帧高度优势 P95 仅 **1.02 毫米**。许多关节高度接近，横向却相隔数厘米。这个机制足以使单手选点跳变；两段录制均缺少同步头部姿态与玩家瞄准意图，无法由此推出嘴到双手最终瞄点的角误差或误射率。

试算一种有记忆的选点规则：旧关节有效时，只有新关节高出指定距离才更换；旧关节失效就立即重新选择。保留的是关节身份，它的位置仍逐帧更新。

| 更换所需高差 | 更换关节次数 | 相邻位移超过 40 毫米的次数 | 保留关节低于当帧最高点的高度差 P95 |
|---:|---:|---:|---:|
| 0 毫米，原规则 | 37 | 36 | 0 |
| 1 毫米 | 20 | 19 | 0.39 毫米 |
| 2 毫米 | 13 | 12 | 1.09 毫米 |
| 4 毫米 | 3 | 2 | 2.49 毫米 |

**连续坐标。** 同批样本另外比较右食指尖相对手腕的三维位置。固定低通截止频率取 4 Hz。速度自适应组采用 1€ 思路，最低截止 4 Hz、速度系数 20 Hz/(m/s)、导数截止 1 Hz，以实际观测间隔更新，按三维平滑速度的模长调节截止频率。这里使用独立回放实现，参数仅供这次对照。[1€ 论文与作者调参方法](https://gery.casiez.net/1euro/)

| 输出 | 相邻观测位移 P95 | 输出相对同帧原始观测的位置偏差 P95 |
|---|---:|---:|
| 原始点 | 3.37 毫米 | 0 |
| 固定低通 | 2.43 毫米 | 3.72 毫米 |
| 速度自适应低通 | 2.59 毫米 | 3.15 毫米 |

固定低通在这组数据上压小了更多相邻位移，自适应组更贴近同帧原始点。「丑」的单步中位位移在自适应组从 0.62 升至 0.74 毫米。原始点也含噪声和真实动作，两个指标只能描述输出的平滑程度与偏离量。录制缺少物理真值、真实笔尖和独立 Quest 上的显示时间，不能用来测定位精度、画符准确率或手到画面的延迟。

本机复核可运行 `python D:/Temp/NarutoVR-HandStability-20261121/replay.py`。脚本读取原录制，只写统计值、参数及文件指纹。本次汇总 `results.json` 的 SHA-256 为 `072643c7f774a126de802a216c257f58afca9a811b015c840c0fd848f5bba29d`。

## 建议的试验顺序

1. **先定位画面问题。** 在同一份 Quest 构建、同一姿势下，分别显示原始调试线、玩家手模与稳定化手模，记录实际源版本、采样间隔、无效关节数和恢复瞬间。单独观察是整手位移、局部关节震颤，还是手指线消失。显示可尝试 SDK 低平滑，恢复过渡仅影响外观。[手模安装与真机验收](../guides/PLAYER_HANDS_ZH.md)
2. **吐息先验证 4 毫米换点门限。** 玩家希望优先稳住火线，当前取离线对照中的 4 毫米。后续有头显时，把手举在嘴前保持、缓慢横扫，再故意抬高另一指节，记录固定靶面上的落点散布和有意换指的响应时间。若换点稳定后仍有连续颤动，再单独试最终方向的轻量平滑，可见火流与伤害沿用同一方向。[当前施法规则](../guides/CONFIGURATION_GUIDE_ZH.md)
3. **结印看误触与漏判。** 让不同玩家依次做保持律印、相似印切换、双手互挡再恢复及散手。同步记录正确标签、首二候选分差、覆盖率和触发时间，统计错误触发、漏触发、一次保持中的状态翻转及确认延迟。保留现有规则作为对照，一次只变一项。补充记录快速甩手、松开与闪避，确认显示平滑不会改变攻击时机。
4. **墨规笔先在画板局部验证。** 用同一批带时间戳的笔尖轨迹回放原始、固定低通和 1€ 三组。分别画慢直线、90° 折角和小圆，测直线横向偏差、拐角被抹圆的程度、抬笔后多画的距离和模板拒识率。落笔/离板单独设接触与离开门限，失追中断当前笔画。若刚性握笔时握点到笔尖相距 10 厘米，1° 转角误差就约为 1.75 毫米，测试时应同时记录握点旋转和笔尖位置。[画符采样与分笔设计](../architecture/GLYPH_DESIGN_ZH.md)
