# Quest 与 PICO 手模变形、关节遮挡与律印识别技术报告

2026-11-27。核查 Quest 主工程 `main` 和 PICO 移植工程 `codex/pico-port`。结论使用正式场景引用、两个工程的运行时源码、已有 Quest 回放与 PICO 原生实测。候选验证范围见末节。

## 结论

**两平台可分别挂手模，关节输入、识印和交互继续各走现在的入口。律印先修两端共用的遮挡处理，保留「双食指并拢向上，其余手指合扣」的手势。** PICO 已成功识别并放出龙火，当前问题是某些遮挡姿势不稳定。换成 PICO 手模可以改善显示，识印仍需单独解决缺失关节。

| 环节 | Quest | PICO |
| --- | --- | --- |
| 设备输入 | Quest 原生为 Android OpenXR，开启 Meta 的手追功能。现有变形截图的对照数据来自 Quest 3 经 Steam Link 的 PC OpenXR，须与原生数据分开评估。 | PICO 4 Ultra Enterprise 原生运行时，启用 PICO Support 与 XR Hands，原生日志为 `OpenXR / XR Hands / Pico XRRuntime()`。Meta Hand Tracking Aim 在 PICO Android 配置中关闭。 |
| 当前可见手 | 三个章场景均引用 Meta Interaction 207.0.1 的 `UnityXRHandVisualLeft/Right`，内含官方 OpenXR FBX。 | 三个章场景引用同一对 Meta Prefab。显示也经过 `FromUnityXRHandDataSource → IHand → HandVisual`。 |
| 可独立切换的显示 | 继续用当前 Meta 基线对照 Quest 已做的 `UnityFixedLength` 候选。如局部折角残留，用同骨同帧检查掌骨、食指近节、腕骨权重过渡及拇指轮廓。 | 先用 PICO 官方文档推荐的 Unity XR Hands `HandVisualizer` 作原始关节参照，再试已有固定骨长显示驱动。要用 PICO 自带 `HandLeft/Right` 时，参考 `PXR_Hand` 的骨架、局部旋转和整手缩放方式，通过当前 XR Hands 做骨骼映射。常规验收仍走现有 OpenXR Loader。 |
| 玩法 | 捏合、远抓、腕匣与结印从实际追踪与交互通道取数据。保持原追踪源与活动场景只有一套交互驱动。 | `WristHandChannels` 优先使用关节几何的射线与捏合，现有 EXT 设备只按有效状态补充。手模分支不得改变这个优先级，也不得向识印回写渲染姿态。 |

[Quest 三场景与回放证据](../../Tools/Validation/Evidence/QuestHandDisplay1127/README.md) · [PICO 二章 Prefab 引用](D:/UnityProjects/NarutoVR-PICO/Assets/Scene/Chapter02_SuspendedPath.unity:23541) · [PICO OpenXR 官方手追接法](https://developer.picoxr.com/document/unity-openxr/hand-tracking/) · [PICO 官方手模驱动源码](https://github.com/Pico-Developer/PICO-Unity-Integration-SDK/blob/e0740bf309b26a0b9adae33c506514569bfe5d38/Runtime/Scripts/Hand/PXR_Hand.cs)

## 变形从哪里来

**Quest 已有量测显示皮肤绑定本身会放大局部折角。** 用户指出的左手画面中，食指根尖角附近约 90% 跟食指近节，紧邻掌缘凹口约 90% 跟腕骨。对应食指掌骨与近节约 6.87°，这一帧单独改每顶点两骨或四骨，尖角和鼓包的最外缘几乎相同。把同一录制姿态送入 Unity 样例网格与固定骨长显示的联合候选，特定凹口由 37.0 像素降至 9.7 像素。这个 A/B 同时替换了网格和驱动，用来判断显示有改善，尚无法分别归因。原生 Quest APK 未做同动作对照。 [Quest 权重证据及原图](../../Tools/Validation/Evidence/QuestHandDisplay1127/README.md) · [Ultraleap 逐关节检查轴向和权重](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/rigged-hand-support.html)

**PICO 遮挡可以导致输入缺帧，现用驱动还可能把骨长波动带进网格。** Meta 桥接每帧根据食指近节至中节的距离估算整手比例，并将捕捉到的局部位置与旋转写给蒙皮骨架。骨段长度波动、父子关节有效性不一致，或追踪重新恢复，都可能表现为指根拉长、折回或跳变。Unity XR Hands 的 `TryGetPose` 在失败时返回 `false`，此时出参是 `Pose.identity`。现装 Meta 桥接只在返回 `true` 时覆盖该关节的缓存，因此**仅丢单个关节的 Pose 位不会在这段代码直接把指节写回默认姿势**。可能是旧的遮挡前姿势留在缓存，周围关节继续移动。若运行时给出有效的推算姿态，桥接也可能采纳它。须用同一帧的原始关节、桥接关节与渲染骨骼确认这两种现象。 [Meta 桥接关节写入](../../Library/PackageCache/com.meta.xr.sdk.interaction@61177ebcfd9a/Runtime/UnityXR/Scripts/FromUnityXRHandDataSource.cs#L303) · [Unity XRHandJoint API](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/api/UnityEngine.XR.Hands.XRHandJoint.html#UnityEngine_XR_Hands_XRHandJoint_TryGetPose_UnityEngine_Pose__) · [OpenXR VALID 与 TRACKED 定义](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrSpaceLocationFlagBits.html)

## 律印为什么会卡在遮挡

1. `SealXRHandsSource` 用关节的 Pose 位、`TryGetPose` 和数值合法性判断是否可用。指节缺失会降低该指的置信度。整手置信度随后合并所有手指，`SealFeatureExtractor` 又要求两只整手均高置信才承认全部 `Both.*` 特征。因此，即使双腕和食指仍然可见，丢一根原本不参与律印评分的小指，也可能连带丢掉全部双手特征。
2. 调整前两端的 `律印.asset` SHA-256 均为 `a4227064fac716e7d6d00dae44fb232d736dc225fa0bbe0563c144d528e2d92b`。正权重合计 16.5，其中双手腕距、掌角、食指距离/方向和中指尖距离合计 7.5。只让这些双手特征失效，其余已满分，有效覆盖与最高分也只剩 `9 / 16.5 = 54.5%`，过不了 80% 覆盖门。只降低得分阈值无法通过。模板基础分数门槛为 80%，龙火的口令配方用折减后的 75%，保持要求为 70 ms。
3. PICO 原生记录中，22:20:00 的样本是 80.7%，质量和消歧均通过，22:24:07.453 已有龙火正式施放记录。它证明现有手势能成功。该日志只有汇总分与门控。Quest 会话对其按 session 和帧去重，578 条带律印门控的快照中，454 条显示双手有效，454 条质量均通过，107 条同时过分数、质量、时效和消歧。这个采样没有抓到双手仍有效却因局部缺失拒识的现场帧，也未标玩家逐次意图。可排查代码路径，现场哪一节丢失仍待同步采集。 [快照计数](D:/Temp/QuestPicoHands1127/pico-gate-counts.json) · [Quest 章节认证链](D:/Temp/QuestPicoHands1127/quest-seal.md)

[关节有效性](../../Assets/scripts/logic/SealLab/SealXRHandsSource.cs#L70) · [双手特征](../../Assets/scripts/logic/SealLab/SealFeatureExtractor.cs#L20) · [评分分母与覆盖](../../Assets/scripts/data/SealLab/SealProfile.cs#L115) · [技能阈值和消歧](../../Assets/scripts/logic/Skills/Core/SkillMatchEngine.cs#L887) · [PICO 原生事件记录](D:/UnityProjects/NarutoVR-PICO/Logs/PicoLive1127/complete-casts.jsonl)

## 已有实现怎样处理遮挡

| 实现 | 做法 | 本项目用法 |
| --- | --- | --- |
| Unity XR Hands | 逐关节查询成功再更新本地位姿数组。失败的关节不会用失败出参覆写。HandVisualizer 可单独显示关节点，适合判断原始关节是否已跳动。 | 用作原始骨架参照。正式的显示驱动遇父关节缺失时保留整段最近可信的局部关系，避免子节点临时采用错误的相对基准。 [XRHandSkeletonDriver](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.7/api/UnityEngine.XR.Hands.XRHandSkeletonDriver.html) |
| Meta Interaction SDK | `LastKnownGoodHand` 用上次可信姿态维持连续显示，`SyntheticHand` 可通过过渡修改可见手；SDK 的架构示例允许射线与显示用不同数据层。项目安装的 207 版会把回退输出标为 `SyntheticPose`，并对下游提供有效/高置信标志。 | 借鉴最近可信和渐变策略，回退仅在显示分支使用。识印保留真实追踪状态，避免回退帧触发新技能或拾取。 [Meta 输入处理](https://developers.meta.com/vr/documentation/unity/unity-isdk-input-processing/) |
| Ultraleap | 跟踪器将当前可见信息、历史观测和人体骨架约束一起使用，推断被挡住的关节。 | 在可见手上保留骨长和局部旋转连续性。对识别来说，完全被挡住的手指缺少新证据，不能当成确定弯曲。 [Ultraleap 跟踪与插值](https://docs.ultraleap.com/api-reference/tracking-api/leapc-guide/leap-concepts.html) |

## 对 Gemini 调研的复核

Gemini 对单指丢点牵连整手、着色网格以单节食指估整手比例，以及把显示和识别独立接线的判断，有源码和权重复算支撑。它指出的点指转握拳还包含独立的移动状态机、整手质量和 0.18 秒衔接条件。这能给跑步排查提供明确的复现实验。 [Gemini 移植调研](QUEST_TO_PICO_INDUSTRY_PORTING_PRACTICES_20261127_ZH.md) · [Quest 协作只读回执](D:/Temp/QuestPicoHands1127/quest-review.md)

- 射线与捏合的标准接口可以复用。本项目的 PICO 分支已在关节几何可用时优先使用几何，EXT 是补充。Gemini 的通用建议不能直接反推本工程当前顺序。 [PICO 实际分支](D:/UnityProjects/NarutoVR-PICO/Assets/scripts/logic/Rpg/WristHandChannels.cs:30)
- PICO 官方支持 60 Hz 高频手追，并提示有性能开销。当前已安装批次 APK 的清单读回 `Hand_Tracking_HighFrequency=0`。适合单独对照快速开合手，局部完全遮挡仍需要应用层判断。 [PICO 官方高频说明](https://developer.picoxr.com/document/unity-openxr/hand-tracking/#high-frequency-hand-tracking-60hz) · [现有 APK 清单](D:/UnityProjects/NarutoVR-PICO/Logs/PicoHeng1127/apk-manifest.txt:141)
- Gemini 列出的 150–300 ms 可作为显示用短保持的试验区间。施法需要另设时限和新鲜样本规则。文中通用距离/视场数值以及所列商业游戏共同采用五项做法的说法，在已列来源中缺少逐项原始证据，不能作为 PICO 工程验收参数。
- 有实测的双手 VR 研究中也出现相互挡手，使用者微调手的位置后恢复检测。它支持给玩家轻量的角度提示。它也记录了不同手势姿态相似导致的误操作，支持本工程同时用规印、衡印和自然动作做负例。 [双手 VR 手势实测，§5.4.1、§6.2](https://arxiv.org/html/2303.07995#S6.SS2)

## 实施和验证顺序

1. **先把缺失定位到具体关节。** 复用 Quest 隔离项目的 10 秒追踪采集和 `SealRecording` 回放，PICO 做独立录制入口。两端原生同人、同光、同动作，区分 `Dynamic` 与 `BeforeRender`。记录腕、掌、食指三段、中指及所有实际依赖关节的有效位、`TryGetPose` 结果、原始位置与旋转、桥接后姿态、可见骨骼、成印得分与拒绝原因。Unity 的 Pose 位表达位姿可用性。如果 Unity 封装未暴露原生位置和旋转的 `TRACKED` 位，在独立诊断中经验证的 OpenXR 扩展边界补采。记录从无遮挡找到手势、右手遮左手、左右互换、遮挡后马上松开四种路径。
2. **两端共用按特征判断置信度。** 腕距只依赖两个腕，掌角看构成掌方向的点，两食指朝向和距离由实际用到的食指关节决定。双腕新鲜、可见食指或足以证明其方向的近节、双手会合关系和竞争印间隔构成律印的必要证据。看得见的卷曲证据继续加分。被包住的非关键手指因遮挡读不到时标为未知，有可信数据且明显不符合时可直接否决；避免把未知作为命中，或把整手置信度放开。
3. **用可见的合手过程补证。** 两食指在合手前已经确认、且当前双腕及其余锚点仍符合时，可在短暂局部丢帧期间保留候选，先用约 80–120 ms 做回放对照。待新鲜关键证据到来，继续现有确认流程。关键点持续缺失、双手已分开或证据冲突时过期或取消，整手丢失时不累计保持时间。完整遮挡下若所有能区分律与规的特征都不可见，优先给玩家一个小幅转腕或略错开手指的提示，仍沿用同一印。只有两端实录仍表明难稳定时，再评估供两平台共用的等价动作。
4. **显示候选无须等识印。** Quest 复用同帧 A/B，先测现有 B 的手指轮廓和指腹接触。PICO 从官方 OpenXR 基线和固定骨长候选出发。按全手多段稳定观测缓慢校正一次比例，低置信时仅在显示层短时保留最近可信姿态，恢复时平滑接回。腕匣依然从玩法腕位取样，模型指腹与实际捏合点须用拿笔和铜叶对照，防止看起来碰到了却抓不到。
5. **按平台分开标定，使用同一组正反例验收。** 当前 PICO 只覆盖三印。后续用同一标签和不同录制段验证两个平台的参数，先只调单项特征和覆盖，保留竞争印差值，再决定是否需要维护另外一份模板。 Quest 原生和 PICO 原生各录无遮挡律印、单侧遮挡律印、规印、衡印、空握拳、正常近抓与远抓、翻腕看匣。回放对照当前版和候选的首判成功率、判定延迟中位数和 P95、规/衡/日常动作误触率、丢帧位置及同帧骨段长度漂移。另查短时失追不会造成施法误取消、自动丢物或腕匣跳位。初测可用至少 5 名用户、每端每人 20 次律印和每端至少 200 次负例，先与基线比较并保留原始次数和分母。Steam Link 单列结果。

## PICO 已实现及回放结果

PICO 移植分支已接入 `8ed772e`。只在 Android 启用 PICO OpenXR 时应用三印参数，正式 Quest 独立保留基线。当前以两套入口共用的特征评分实行局部可用。律印的单侧食指尖读不到时，可检查该侧食指近节至远节的弯曲，以及两侧仍可见的近段方向。保留双腕、左右食指和跨手关系的独立证据组。可见的原证据优先，原证据与目标冲突时不会转用替代特征。未知关节不生成有效值。已建立的候选可以在较窄分差内续持，候选建立和保持均以新鲜观测为准。

| 印 | PICO 最低有效覆盖 | 规则原容差倍数 | 续持比进入低的分差 |
| --- | ---: | ---: | ---: |
| 律 | 58% | 1.09 | 4.5 个百分点 |
| 规 | 68% | 1.10 | 3.5 个百分点 |
| 衡 | 65% | 1.09 | 4 个百分点 |

Unity 6000.3.10f1 隔离验证通过 61 项 SealLab、87 项技能核心及 34 项 PICO 合成回归。Quest 会话提供了 18 段压力片，另补左右食指分别丢点两段，合计 20 段 960 帧。对每段执行原算法和 PICO 算法。人工移除一侧食指尖，律印旧覆盖约 36.4%，PICO 覆盖约 61.0%，实际章节匹配回调由零次变为一次龙火。人工移除两侧食指尖、整只所需手失追和重复旧帧时，均未产生龙火。规印缺无名指尖时能成印，食指曲直与律印的反例区分通过。左手衡印在缺右手时能发动一次，相关回放也核查了缺小指和相邻特征。 [逐段对照](D:/UnityProjects/NarutoVR-PICO/Logs/PicoSealOcclusion1127/masked-results.csv) · [断言回执](D:/UnityProjects/NarutoVR-PICO/Logs/PicoSealOcclusion1127/validation-summary.txt) · [PICO 源码与文件散列](D:/UnityProjects/NarutoVR-PICO/Logs/PicoSealOcclusion1127/source-sha256.txt)

回放适合证明掩码和计时修复，输入来自历史录制、Quest Steam Link 与人工缺失。章节匹配回调的龙火测试允许嘴部距离条件通过，尚无本次遮挡动作下的 PICO 原生逐关节记录。新参数仍需在 PICO 上校准，平台专用显示手、跑步和新 APK 另行验证。
