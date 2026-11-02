# 肉鸽施法、画符与手势移动：一手资料核查

> 文档定位：资料核查/历史调研，核对日期见正文，候选方案和外部版本信息需要使用时重新核实。本文不充当当前操作或已完成功能清单；现行入口见[文档索引](../README.md)。

核查日期：**2026-10-30**。本文为设计研究，不表示相关功能已经实现，也没有安装包、运行 Unity 或修改运行资产。

标记规则：**事实**是来源直接支持的内容；**设计推论**是针对本项目的建议；**未核实**不能作为竞品既有功能或平台保证引用。检索使用官方商店、开发者文章、原作者代码和平台文档；未依据宣传画面反推算法。

## 1. 先确认参考游戏是谁

| 用户称呼 | 核查结果 | 开发者、平台与证据边界 |
| --- | --- | --- |
| 德拉克传承 | **相关候选：Drakheir**；当前官方链接的英文商品名为 **Drakheir The First Hand Tracking ARPG**。但未找到一手来源把“德拉克传承”这一精确中文名直接对应到它。 | Meta 官方文章把 Drakheir 归于 **Hengxin Shambala**，并从裸手优先设计讨论它；开发者隐私页主体为 **Shambala virtual reality technology co, ltd**。Meta 官方文章链接到 Quest 商品 ID `8234180549963919`。中文别名和用户具体指的是不是这个产品，仍需参考截图/链接确认。[Meta 开发者案例](https://developers.meta.com/vr/blog/how-hand-interactions-are-opening-new-possibilities-vr-developers/)、[开发者隐私页](https://drakheir.shambalavr.com/privacy.html)、[Meta 商品页](https://www.meta.com/experiences/drakheir-the-first-hand-tracking-arpg/8234180549963919/) |
| 无界术士 | **强匹配：The Wizards**，当前 Steam 商品名 **The Wizards - Enhanced Edition**；具体参考版本尚未确定。 | Steam 标明开发者/发行者 **Carbon Studio**、PC VR 和 tracked controller support。官方港服 PS 商店直接使用中文名“无界术士”，明确为 PS4/PSVR、PS Camera、PS Move 必需，亚洲发行方为 **CIRCLE Entertainment LTD (Hong Kong)**。Carbon 自己的历史页确认 Enhanced Edition 曾发行到 Quest 与 PSVR。不要把开发者与地区发行商混为一谈。[Steam](https://store.steampowered.com/app/586950/The_Wizards__Enhanced_Edition/)、[官方中文 PS 商品页](https://store.playstation.com/zh-hans-hk/product/HP1658-CUSA17326_00-WIZARDSASIACIRCL)、[Carbon 历史页](https://carbonstudio.pl/en/history/) |

**版本与名称边界。** The Wizards、The Wizards - Enhanced Edition、The Wizards - Dark Times 是同一开发者的不同版本/作品；开发者历史页列出这些名称，不能把续作的动作、联机或法术数量移植到原作事实里。这里没有把“无界术士”猜成 Aldin Dynamics 的 **Waltz of the Wizard**；Meta 案例文章将后者与 Drakheir 分列，开发者也不同。[Carbon 历史页](https://carbonstudio.pl/en/history/)、[Meta 案例文章](https://developers.meta.com/vr/blog/how-hand-interactions-are-opening-new-possibilities-vr-developers/)

**可确认的平台范围。** Drakheir 的第一方材料支持 Quest 裸手路线；上市公司公开披露也直接把它称为原生手势追踪游戏。本次没有核到其 Steam 正式商品页、PICO 官方商品页或当前逐型号兼容清单。Meta 商品正文多次超时/无可读内容，因此未核实当前价格、发售状态和设备细表。[恒信东方 2025 年报摘要，2026-04-28 发布](https://disc.static.szse.cn/download/disc/disk03/finalpage/2026-04-28/98031e39-1cb8-47e3-9569-46578d12fde6.PDF)

## 2. 可以借鉴什么，不能推断什么

**Drakheir 的已核事实。** Meta 采访明确描述：开发者受日式动画结印启发，以手势组合施法；测试发现玩家动作幅度/速度不同，于是扩大容差并删除互相重叠的手势。这是“法术动作要可区分、允许玩家变体”的直接设计证据。文中的近似动作比例是开发者的产品描述，**不是本项目可沿用的识别阈值，也不是 Quest 追踪准确率**。[Meta 开发者案例，Drakheir 段](https://developers.meta.com/vr/blog/how-hand-interactions-are-opening-new-possibilities-vr-developers/)

**The Wizards 的已核事实。** 官方中文商店描述动作施法、学习/升级六种元素法术、命运卡牌、竞技场，以及滑动行走/定点传送。开发者回顾明确提到 Vive Wands、后来适配 Oculus Touch，以及从点到点传送转向自由移动并补充舒适性设置。这些足以支持“鲜明的身体动作与即时魔法反馈”这一借鉴方向；该参考不能默认归为原生裸手游戏。[PS 官方商品页](https://store.playstation.com/zh-hans-hk/product/HP1658-CUSA17326_00-WIZARDSASIACIRCL)、[Carbon 开发回顾](https://carbonstudio.pl/en/2026/02/04/how-the-wizards-was-created-carbon-studio-vr-game/)

**设计推论。** 本项目可保留“短姿态识别 → 稳定成立反馈 → 法术准备 → 定向释放”的可读流程，并让高成本画符成为额外操作路线。局内拾物换法术、隐藏法术、局外购买升级、吟唱/结印/画符对 MP/CD/威力的影响，应作为本项目独立规则设计。上述竞品资料没有证明它们恰好采用这一套经济或数值公式。

**未核实。** 两款参考游戏的内部分类器、骨骼权重、时序窗口、MP/CD 公式、画符算法、负例拒识阈值，以及是否使用 `$1/$P/$Q`。看见画面中的轨迹或粒子，不能证明后台识别自由手绘；看见虚拟手，不能证明输入没有控制器。

## 3. 画符：成熟小算法与适用边界

### 3.1 原作者、源码与真实许可证

| 方案 | 原作者与官方入口 | 已核的代码/许可 | 对本项目的判断 |
| --- | --- | --- | --- |
| `$1` | Jacob O. Wobbrock、Andrew D. Wilson、Yang Li；[作者项目页](https://depts.washington.edu/acelab/proj/dollar/index.html)、[论文](https://faculty.washington.edu/wobbrock/pubs/uist-07.01.pdf) | 直接读过官方 [dollar.js](https://depts.washington.edu/acelab/proj/dollar/dollar.js) 头部，以及 [C# ZIP](https://depts.washington.edu/acelab/proj/dollar/dollar.zip) 内 `$1/Recognizer.cs` 的完整许可段；均标 **New BSD License**，含三项条件。 | 适合第一轮少量**单笔**符号验证；不能把多笔符不加处理地串成一条线。 |
| `$P` | Radu-Daniel Vatavu、Lisa Anthony、Jacob O. Wobbrock；[作者项目页](https://depts.washington.edu/acelab/proj/dollar/pdollar.html) | 直接读过官方 [pdollar.js](https://depts.washington.edu/acelab/proj/dollar/pdollar.js) 和 [C# ZIP](https://depts.washington.edu/acelab/proj/dollar/pdollar.zip) 内 `PDollarGestureRecognizer/PointCloudRecognizer.cs` 的许可段：**New BSD License**。 | 面向单笔/多笔最终形状；更宽容的同时会失去笔顺语义。 |
| `$Q` | 同上三位作者；[作者项目页](https://depts.washington.edu/acelab/proj/dollar/qdollar.html)、[论文](https://faculty.washington.edu/wobbrock/pubs/mobilehci-18.pdf) | 直接读过官方 [qdollar.js](https://depts.washington.edu/acelab/proj/dollar/qdollar.js) 和 [C# ZIP](https://depts.washington.edu/acelab/proj/dollar/qdollar.zip) 内 `PDollarGestureRecognizer/QPointCloudRecognizer.cs` 的许可段：**New BSD License**。JS 移植还署名 Nathan Magrofuoco。 | 延续点云路线，以查表、提前终止等减少比较成本；是否值得替换 `$P` 要看本项目实测耗时。 |

**许可摘要。** 已读许可允许有条件地以源码/二进制形式再分发与修改；需保留版权、条件和免责声明，二进制分发也要在材料中附带相关声明，不可擅用机构/作者名背书。此处没有复制整段许可；真正引入时应保留对应文件原文和署名。没有核审所有 ZIP 内附带 DLL、示例素材及第三方依赖，不能把某个源码许可扩大成“整个下载包的所有内容随意用”。

**集成边界。** 官方提供的是 JS/C# 源码和研究/桌面示例，**没有据此核到 Unity UPM 包或保证即装即用**。`$1` C# 示例涉及 Windows Forms、`System.Drawing`、WobbrockLib；移动端 Unity 应抽取/移植核心数学，替换数据类型和示例 I/O，重新验证 Android/IL2CPP。`$P/$Q` 也需要本项目的轨迹采集、门控、模板资产和拒识层。

### 3.2 算法选择表：不能把“方向不敏感”一概而论

| 选择维度 | `$1` | `$P` | `$Q` | 本项目建议（设计推论） |
| --- | --- | --- | --- | --- |
| 输入/时序 | 2D 单笔有序点列；逐位置比较。 | 2D 无序点云，设计上忽略笔画数量、顺序、每笔绘制方向。 | 继承点云与 articulation-invariant 路线。 | 普通战斗符先用单笔；如果法术区别是先横后竖或顺/逆画，不能只用 `$P/$Q` 判形。 |
| 起止门控 | 三者均接收已分段的候选；不负责理解玩家是否开始画符。 | 同左。 | 同左。 | 独立“进入画符模式 → 开笔 → 采样 → 收笔/提交”状态；追踪丢失或全局取消时作废。多笔需保留 stroke ID，抬笔段不连线。 |
| 旋转/缩放 | 默认实现有旋转归一化与非均匀缩放；论文明确指出默认不能区分依赖朝向或长宽比的符。 | 所核官方实现缩放/平移，没有 `$1` 那种旋转归一化步骤。 | 所核官方实现同样没有那一步；另有整数坐标和查表。 | 先规定符的朝向是否有含义；有含义时保持锁定平面坐标，不能无条件启用旋转不变性。容许少量倾斜不等于允许任意旋转。 |
| 镜像与左右手 | 未见自动镜像归一化步骤。 | 未见自动镜像归一化步骤。 | 同左。 | “笔的绘制方向”与“几何镜像/旋转”是不同概念。左右手是否视为同符要明确决定，必要时加入受控镜像模板；镜像后与别符冲突则重新设计符集。 |
| 近似符/负例 | 最近模板结果不是“玩家意图”的证明。 | 点云忽略笔顺可能把同形不同顺序合并。 | 加速没有恢复已丢失的笔顺信息。 | 必须有绝对距离门槛、第一/第二候选间隔、无效输入拒绝；不可每次都强制选一个法术。阈值需由真实玩家正/负样本确定。 |
| 退化轨迹 | 默认非均匀缩放会伤害线状输入；论文专门讨论此限制。 | 零尺寸点云会使官方缩放除数为零。 | 同样要保护零尺寸输入。 | 识别前拦截非有限点、重复点、点数不足、总长度/包围盒过小、长时间停顿、过深离平面、异常跳点；单线符单独处理或首版排除。 |

事实依据：[ `$1` 原论文限制段](https://faculty.washington.edu/wobbrock/pubs/uist-07.01.pdf)、[ `$P` 作者说明](https://depts.washington.edu/acelab/proj/dollar/pdollar.html)、[ `$P` 实现](https://depts.washington.edu/acelab/proj/dollar/pdollar.js)、[ `$Q` 实现](https://depts.washington.edu/acelab/proj/dollar/qdollar.js)。表中拒识、门控与退化防护是本项目需补的工程判断，不是这些算法自带的完整 VR 功能。

**研究结果不能直接变成产品承诺。** `$1` 的原实验使用 Pocket PC 笔输入；`$Q` 作者给出的性能倍数来自其研究条件。两者都不能转换成 Quest 裸手在战斗、遮挡、快速挥手条件下的准确率或帧耗时保证。对小模板集，先确认采样与拒识是否可靠，再决定是否需要更快的分类器。[ `$1` 论文](https://faculty.washington.edu/wobbrock/pubs/uist-07.01.pdf)、[ `$Q` 论文](https://faculty.washington.edu/wobbrock/pubs/mobilehci-18.pdf)

### 3.3 只评估“3D 采集 → 锁定平面 → 2D 识别”

以下为**设计推论**，不是上述作者已经验证的 Quest 实现：

1. 进入画符状态时确定一个舒适的胸前绘制平面，锁定原点 `o`、平面右轴 `u`、上轴 `v` 和法线 `n`。不要每帧随头朝向或正在移动的左手重新定基准，否则同一条轨迹会被坐标系运动扭曲。
2. 用有效追踪的指尖/选定绘制点 `p` 生成 `x=dot(p-o,u)`、`y=dot(p-o,v)`；另存 `z=dot(p-o,n)`、时间、tracking-valid 与 stroke ID。以离平面深度作为质量检查，不把它冒充识别 3D 符的能力。
3. 删除太近的重复点和明确跳点，适度平滑，再按弧长重采样。笔画抬起、追踪丢失、移动动作不能被直线补进符中。太多缺失直接撤销当前笔/整符，并给反馈。
4. 初版符集按**几何差异**选型；用真实 Quest 用户记录相似符、残缺符、随手摆动、移动瞄准、结印、火球轻推和取消动作作为负例。测误触发、拒识、提交延迟及疲劳，再谈“画得更准是否更强”。

**可行性结论。** 对有限、明显不同、以平面形状为主的符号，这是一条可控的原型路线。它会丢弃深度结构，因此不适合把前后绕环等 3D 形状投影后仍承诺可区分。识别分数也没有天然对应“符的艺术完成度”或伤害倍率；若要奖励复杂操作，先奖励完成明确步骤，避免让追踪噪声直接放大伤害差距。

## 4. Quest 输入与后续 PICO 的适配边界

**Quest/XR 事实。** Unity 的 Meta Aim Hand 文档提供 aim pose、`isTracked`、`trackingState`、aim flags 与各指 pinch strength 的 Input System 路径，且要求启用 Hand Tracking feature/OpenXR。系统 aim pose 是运行时计算的瞄准姿态，不能当作“食指骨骼轴原样延长”的保证。[Unity XR Hands 1.6.1](https://docs.unity.cn/Packages/com.unity.xr.hands@1.6/manual/openxr-features/metahandtrackingaim.html)、[Khronos aim state 定义](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrHandTrackingAimStateFB.html)

**状态过滤事实。** OpenXR 的 aim flags 分别含有效瞄准、pinch、系统手势和菜单手势标志。设计推论：检测到系统菜单手势、追踪无效、切换输入模式时应冻结/清除未提交输入，不能继续用上一帧缓存位置确认传送或开笔。[Khronos flag 定义](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrHandTrackingAimFlagBitsFB.html)

**现成样例边界。** XRI Hands Interaction Demo 说明支持手/控制器切换、ray/pinch、poke，并列出 XR Hands、OpenXR 等依赖和 Quest feature 配置；它是起点，不是本项目战斗手势识别器。XRI 3.5 更新说明另称加入手势传送样例，但仍应按项目锁定版本检查实际样例内容，不能只凭“装了 XRI”假定拥有同一套脚本。[Hands Demo 文档](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.5/manual/samples-hands-interaction-demo.html)、[3.5 更新说明](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@3.5/manual/whats-new-3.5.html)

**手追踪的真实限制。** Meta 官方设计资料列出视野/追踪体积、自遮挡、双手互挡、光线和快速动作问题。设计推论：紧贴双手的结印、跨手指尖画符和快速甩手都需单独验收；“手没显示但继续累计组合”应视为输入质量问题。优先让绘制手留在头显可见区，并给可见的丢笔/取消反馈。[Meta Limitations & Mitigations](https://developers.meta.com/vr/design/hands-limitations-mitigations/)

**PICO 已核到的范围。** PICO 官方手追踪概览描述 OpenXR 对齐、版本依赖与 XRI Hands Demo 接入路径；其 OpenXR SDK 更新页还记录了系统版本相关的已知 pinch ray 问题。这说明移植必须按实际 SDK、系统、机型核对，不能默认 Quest 的 MetaAimHand 路径、阈值和手势标志在 PICO 原样成立。没有在本次核到本工程目标 PICO 设备清单或已装包兼容性。[PICO 手追踪概览](https://developer-cn.picoxr.com/en/document/unity/hand-tracking-overview/)、[PICO OpenXR 更新页](https://developer.picoxr.com/document/updates-unity-openxr/)

**设计推论。** 在平台适配层统一输出有效关节、瞄准姿态、pinch、系统交互占用、时间戳及追踪状态；上层只处理“开笔/收笔、方向候选、确认/取消”。PICO 使用其可用数据填充同一契约，再单独采样校准。暂不以“同为 OpenXR”代替设备验收。

## 5. 左手预设点传送与右手闪避

### 5.1 官方能力与可以复用的部分

**XRI 事实。** Teleportation Anchor 对应预设位置/朝向；Area 对应可选表面位置。3.x 通过 locomotion provider、mediator、XR Body Transformer 组织移动；Anchor 的 hit-normal filter 可限制选取法线，不能据此宣称已经完成全身净空或敌人占位检查。[XRI 移动概览](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@3.5/manual/locomotion.html)、[Teleportation Anchor](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.5/manual/teleportation-anchor.html)

**Meta 事实。** Teleport Interaction 支持 hotspot、平面、NavMesh 与物理层表面；手部射线加入肩部稳定以减少远处选点噪声。其候选计算还可阻止“手穿墙后再向另一侧传送”的选点。**这不是左食指前后左右 + 停留确认的现成实现**；输入规则需自定义。[Meta Teleport Interaction](https://developers.meta.com/vr/documentation/unity/unity-isdk-teleport-interaction/)

**设计推论。** 左手方向选择可先离散为四向，采用角度滞回、防抖、稳定驻留进度和离开/取消复位。要先明确“前后左右”基于固定房间、入场时朝向还是角色朝向；不能在玩家缓慢转头时把参照轴悄悄切换。左手用于选点时，其确认事件不应同时被画符或结印消费；右手闪避也应有独立意图门控，避开已有火球轻推触发区。

### 5.2 “夹角小可插值”必须拆成两种语义

| 插值发生在哪 | 可行性与风险 | 设计建议 |
| --- | --- | --- |
| **只插值候选落点**：两个预设方向之间生成一个中间候选，确认后仍瞬移。 | 保留离散位移，但两个合法点之间的线性插值点可能在墙内、坑中、禁区或错误楼层。 | 每个中间候选也重新做合法性验证；不能把“端点合法”推成“中间都合法”。为小夹角候选给清晰落点预览。 |
| **插值移动 XR Origin**：从 A 滑到 B，包括短距离闪避。 | 已成为人为连续移动；小方向夹角并不限制位移距离、速度、视流或转向加速度，因此不是舒适性证明。 | 保留瞬移/眨眼选项；连续模式作为待体验验证的选项，控制距离/速度/加速度和强制转向，并测试眩晕与操作成功率。 |

官方依据：Meta 建议减少加速事件、降低视流、用暗角/时间遮挡，并允许用户选择舒适性方案；并非某种短 Lerp 自动舒适。XRI 官方示例也指出瞬时移动通常更舒适、平滑过渡可能造成不适。[Meta 移动最佳实践](https://developers.meta.com/vr/design/locomotion-best-practices/)、[Meta 用户偏好](https://developers.meta.com/vr/design/locomotion-user-preferences/)、[Unity 官方 LocomotionSetup 示例说明](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/LocomotionSetup.md)

**右手闪避离点的官方相关能力。** Meta FirstPersonLocomotor 说明了 hotspot 与物理/滑动移动结合、离开 hotspot 后切换移动状态，以及阻止穿过碰撞体的离散 stepping。这能说明“预设点与离点移动并存”在架构上有成熟参考；并不证明八方向闪避已被该 SDK 作为同一玩法实现。[Meta Character Controller](https://developers.meta.com/vr/documentation/unity/unity-isdk-character-controller/)

**八方向候选仍待讨论。** 设计推论：它可以是右手请求方向的候选集合，或敌人威胁驱动的安全落点评分集合；这是两种不同控制语义。先明确方向权归玩家还是系统，再确定八向是否必要。候选不足时应拒绝/缩短/提示，不能悄悄选一个与手势相反的点；“离敌最远”也可能进入新危险区。

### 5.3 落点和碰撞校验不可省略

| 校验 | 官方事实依据 | 本项目需补的逻辑（设计推论） |
| --- | --- | --- |
| 地面、楼层、可走区 | `NavMesh.SamplePosition` 找最近导航面，**不考虑障碍**，官方举例可能选到上层楼板；返回 normal 也不可当有效地面法线使用。[Unity API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/ai/navmesh/sampleposition) | 小范围采样、楼层/高度差、地面 raycast/法线、房间允许区和禁传送区一起判断。 |
| 角色落点净空 | `Physics.CheckCapsule` 检查胶囊体积与碰撞体重叠。[Unity API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/physics/checkcapsule) | 用实际玩家高度/半径和正确层遮罩检查墙、低顶、动态物；排除自身碰撞体，适当处理地面接触容差。只检查一个中心点不够。 |
| 闪避沿途碰撞 | `CapsuleCast` 可检测胶囊扫掠路径；官方明确它不能检测初始重叠的碰撞体。[Unity API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/physics/capsulecast) | 连续闪避逐步扫掠/CharacterController 约束，并另查初始/最终重叠；不能直接 Lerp transform 穿墙。 |
| XR 移动约束 | XRI XR Body Transformer 的 collision-constrained manipulator 与 CharacterController 是可选配置。[XRI 文档](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.5/manual/xr-body-transformer.html) | 必须检查真实场景配置；有 XR Origin/Teleportation Provider 本身不等于所有自定义变换都受碰撞保护。 |
| 战斗中的动态占位 | 以上 SDK 不知道本项目敌人硬直、攻击预警、投射物、无敌帧和敌人占位规则。 | 驻留确认时再次查询落点，闪避执行前再次查净空；对敌人/危险区作明确游戏规则判断。多人同房间 PvE 将来另做落点预约与权威校验，本轮仅设计。 |

## 6. 给设计与后续原型的结论

1. **画符可先做受约束的 2D 符号原型。** 少量单笔符从 `$1` 起步；想宽容多笔形状时比较 `$P/$Q`，同时接受它们不能独立表达笔顺/画向法术语义的代价。
2. **先减少符集混淆，再调识别阈值。** 这是 Drakheir 官方案例最直接的启发；把停留、开笔、确认、取消、移动、释放划开输入状态，避免误触发。
3. **预设点瞬移是合适的首个移动验证范围。** 方向选择与落点选择可平滑，但镜头连续插值必须另当一种移动模式验收；八向闪避和威胁评分仍保留为设计候选。
4. **下一轮需要设备证据。** 采集 Quest 真机正/负轨迹与误触发日志，验证遮挡、快速动作、取消、输入冲突、低顶/墙边/跨楼层落点和闪避沿途碰撞。没有完成这些测试之前，不给识别准确率或舒适性保证。

本次未核实事项汇总：精确中文名“德拉克传承”映射、用户所指《无界术士》的版本、Drakheir 当前商店设备细表/Steam/PICO 正式发行情况、竞品内部算法及数值、Unity 项目实际锁定依赖与样例版本、目标 PICO 设备支持矩阵、任何 Quest 真机识别率与舒适性表现。
