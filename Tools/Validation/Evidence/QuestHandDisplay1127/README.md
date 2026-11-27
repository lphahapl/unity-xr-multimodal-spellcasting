# Quest 手模形变与同帧试用

2026-11-27。Unity 6000.3.10f1，Meta Interaction 207.0.1，XR Hands 1.7.2，OpenXR 1.15.1。正式首章《初醒》《庭院》和二章《断衡悬道》继续使用 Meta 官方手。本页记录隔离工程里的显示候选和实际验证范围。

## 用户指出的左手指根

用户确认使用 Steam Link。用户裁图配准到 `quest3-1127-middle-wrist-A.png` 的左手，灰度相关 0.99996。按图中位置投影实际蒙皮面，食指根折角约 90.2% 受食指骨影响，紧邻掌缘的凹口约 90.4% 受腕骨影响；下方圆凸约 97.6% 受拇指骨影响。食指掌骨到近节的空间夹角仅 6.87°。同一姿态两骨和四骨的外沿在折角、鼓包各连续 11 行像素都相同，凹口最多差 1 像素。截图中的问题应先查掌骨、食指近节与腕之间的显示绑定和权重过渡。[权重定位图](surface-weights.png) · [逐点数据](surface-measurements.json) · [表面拉伸和骨轴](deformation.json)

已验证的 B 采用 Unity 官方样例网格和独立显示绑定。固定相机在原始 800 × 500 回放图测量食指外侧深凹，以纵向 225 至 330 像素的外沿两端连线为基准，A 的最大内凹为 37.0 像素，B 为 9.7 像素。八组双手姿态的 B 指尖骨标记最大偏差 0.254 mm。骨标记和指腹表面分别测量。上述数字评价同姿态的当前隔离候选，佩戴者尚未验收。[原手近景](original-detail.png) · [候选近景](candidate-detail.png) · [截图配准与轮廓口径](screen-registration.json)

检查过 11 月 26 日四段 Quest 系统录屏，`quest-195336.mp4` 的 01:38.8 可见正式二章左手和腕匣。11 月 27 日 04:35 的道具测试录屏在 01:24 只画出关节线。均未找到与当晚 19:24 关节采样同步的视频。[二章实机原帧](D:/Temp/QuestHandReview1127/video/quest-195336-98.8.jpg) · [试吃实机原帧](D:/Temp/QuestHandReview1127/frames/quest1127-video-01m24s.jpg) · [录屏核查](video-review.json)

## 两个实际工程作对照

- PICO 当前三个构建场景的采样链是 PICO OpenXR → Unity XR Hands 1.7.2 → Meta 的 `FromUnityXRHandDataSource` → `IHand`。可见的左右手来自 Meta Interaction 207.0.1 的 `UnityXRHandVisualLeft/Right` Prefab，内含 `OpenXRLeftHand.fbx`、`OpenXRRightHand.fbx`。包清单及锁文件未列 Unity XR Interaction Toolkit。PICO 官方 `PXR_Hand` 是移植会话列出的可比较做法。检查均为只读。[PICO 二章引用](D:/UnityProjects/NarutoVR-PICO/Assets/Scene/Chapter02_SuspendedPath.unity:23541) · [PICO 构建核验](D:/UnityProjects/NarutoVR-PICO/Logs/PicoChapters/android-final.log:529)
- aiwo 的仓根构建项可按 GUID 找到 `s1-e01-space-journey`，而有 Quest 3 安装凭证的恢复工程启用 `ep07-meteorology-and-astronomy`。安装了 Meta Interaction 85 的缓存和 NarutoVR 的 207 缓存，左右 OpenXR 官方 FBX 各自 SHA-256 完全相同。EP07 的渲染器使用恢复的序列化 Mesh，显示源经过原生 OVRHand、LastKnownGoodHand 和 SyntheticHand，另有异常腕姿/可见性回退。尚无相同 Steam Link 输入下，两套完整运行链的现场对照。单独复制包里的手模型不能验证两项目的显示差异。[aiwo 原生链核查](aiwo-chain.md) · [aiwo 构建记录](D:/UnityProjects/mr-aiwo-education-science/Recovery/PlayTest/TrackedHands/device-install.txt)
- 外部参考包括实际出现指关节硬折的 OpenXR 开源手、Ultraleap 的逐指轴向及权重对照，以及《Disassembly VR》的掌骨握持演示。优先修食指根的局部权重过渡，再用对掌姿态单独检验拇指轮廓。该路线目前是调研建议。[实例与图示](../../../../docs/research/QUEST_HAND_KNUCKLE_SKINNING_20261127_ZH.md)

## 怎样看

用 Unity Hub 打开 [隔离工程](D:/Temp/Naruto-ChapterEntry-QA-1126/Project)。选择下列一个入口。

| 条件 | 操作 |
| --- | --- |
| 不戴头显，查看同一姿态 | 选 `Tools > Quest Hand Trial > Open offline recorded frames`，进入 Play。用 Previous / Next 切换 8 个姿态，A / B 切换两套手，Recorded pair 恢复实录双手相对位置。[回放场景](D:/Temp/Naruto-ChapterEntry-QA-1126/Project/Assets/QuestHandTrial/QuestHandRecordedFrames.unity) |
| 戴 Quest 经电脑运行 | 接入 Quest 的 PC OpenXR 运行时，选 `Tools > Quest Hand Trial > Open live chapter (PC OpenXR)`，进入 Play。在 Hierarchy 搜索“Quest 手部显示”，保持一个手势，在 Inspector 的显示方式里切换 CurrentMeta 和 UnityFixedLength。[二章试用场景](D:/Temp/Naruto-ChapterEntry-QA-1126/Project/Assets/QuestHandTrial/Chapter02_QuestHandDisplayAB.unity) |
| 单独看官方手的蒙皮精度 | 在现场场景的 CurrentMeta 模式，Play 中选 `Tools > Quest Hands > Official Meta mesh`，轮流选 two weights 和 four weights。该菜单只修改当次 Play 的两只官方手。Restore project default 恢复本次比较。 |
| 记录变形前后 | 保持能复现的手势，选 `Tools > Quest Hands > Record 10 seconds of raw and gameplay joints`。文件位于**当前隔离工程**的 `Logs/QuestHandCapture`。每次采样区分 Dynamic 和 BeforeRender，保留关节有效位、运行时、设备名、追踪与录制原点、玩法版本和时间戳。原点相同且同侧 Dynamic 观测时差不超过 3 ms 时才直接统计双层位置与旋转差。 |

头显试用可以依次做四个动作：缓慢握拳后逐指打开；拇食指从明显张开到碰合并拿笔；结规印、矩印或律印；最后翻腕看准望匣。留意手背和指根是否凹折、指腹是否接触笔杆，以及翻腕时实体扣是否仍在相同位置。回放只提供已经录到的动作。

## 三层检查

| 层 | 已核实的事实 | 仍需判断的部分 |
| --- | --- | --- |
| OpenXR 与游戏输入 | 11 月 27 日的录制可与 SteamVR 的 Quest 3 和正式工程进程对应。4.97 秒、210 帧都有左右手各 26 个有效关节。左手在动，右手 210 帧的完整姿态相同，数据版本持续增加。旧接地记录另有每手食指尖的原始、玩法、显示三层高度。 | 双手录制写入的是 Meta IHand 转换后的姿态。旧接地记录只有高度且采样相位不锁定，不能从它们反推出其余关节的原始位置或旋转。尚待重新采集变形当时的原始帧。[逐段来源](frame-provenance.json) · [食指高度统计](index-tip-height.csv) |
| 骨骼与驱动 | Unity 实际展开三场景，六个官方实例的 156 个关节引用和父子关系正确。每侧一个显示驱动。识印、捏合、物品和腕匣使用玩法追踪源。八组完整双手姿态分别驱动 A 和 B，A 保留官方显示，B 独立驱动 Unity XR Hands 官方样例网格。B 对齐腕部、掌心及指根，保留中间指节长度，并按追踪比例校准末节纵向尺寸。 | 即使骨节端点对齐，两个网格的指腹厚度仍有差异。射线、握点和识印沿用原玩法输入。显示候选只在隔离试用。[三场景装配](chapter-audit.json) · [逐姿态读数](pose-metrics.csv) · [指尖与表面](fingertip-surface.csv) |
| 蒙皮 | 官方手每侧 1,360 个顶点，640 个有三至四骨权重。默认 Android 每顶点两骨，默认 PC 四骨。在同一官方网格和同一录制姿势上只改变权重数，Quest 3 三帧中左手最远顶点相差 7.27–7.49 mm，右手 8.30 mm。Unity 两次 BakeMesh 与独立逐顶点蒙皮计算互核，16 组结果每个顶点的位移差都小于 0.05 mm。 | 两骨限制能解释部分轮廓差异。尚未测得用户那次变形的运行质量级别，或证明它能解释串流观察。未修改项目全局质量。[位移图](skin-comparison.png) · [逐帧与分位数](skin-influences.csv) |

三份旧接地记录中，二章一段的食指原始/显示高度绝对差，中位数约为左 0.15、右 0.44 毫米，95 分位约为左 11、右 13 毫米，个别时刻差值明显放大。记录的是编辑器探针在同一次轮询读到的多层数据，没保存每一层的采样相位。由此无法把瞬时差值归为传感器误差、时序差或网格形变。另一份没有三层同时有效的记录在统计中保留空值。

## 行为回归和待测

使用六段连续原录，共 1,433 帧。按当前正式六枚印的配置运行实际识印入口，Quest 3 串流段在第 6 帧触发衡印，三段历史姿势分别触发规、矩、律。卷指段 189 帧中有 187 帧进入右手握拳状态。1,433 帧均更新了腕匣实体挂点。该项检查的是程序按玩法手腕计算的位置与旋转，不能代替佩戴者观察腕匣是否贴手。[行为回归](interaction-results.txt)

六段自然录制均缺少完整的张开、捏合、拿取动作。在受控回归中，把物体放在指尖处，先送入一帧人工张开条件，再送入未改动的 29.838 mm 闭合实录帧。实际查询命中并高亮，实际拿取组件取得物品和输入权。戴 Quest 连续拿笔、指腹会合、翻腕贴合及局部失追的视觉表现仍待复测。尚无本轮原生 Android APK 佩戴结果。

隔离工程可直接试用。[试用文件压缩包](isolated-trial.zip)按项目相对路径保存两个试用场景、独立显示驱动、回放、诊断工具和测试资料，供重建同版本的隔离工程。[验证文件清单](manifest.sha256)
