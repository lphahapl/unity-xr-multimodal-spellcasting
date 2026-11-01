# 局域网多人：官方资料核查

核查日期：2026-10-29。目标工程：Unity 6000.3.10f1、Quest VR/MR；首轮研究面向 2 人同一局域网、各自物理活动区、共享虚拟场景合作打靶/PvE，后续再验证 4 人。本文件只记录选型证据及试验边界；未安装网络 SDK，未修改运行代码，未进行 Unity 编译或 Quest 真机测试。

**后续方向更新：** 用户已将未来多人目标暂定为同房间 PvE，当前只预留接口，先完成 Quest 单机各系统。上面的物理分区是原始调研背景，现行优先级与空间对齐约束以 [多人方案](MULTIPLAYER_LAN_PLAN_ZH.md) 和 [Quest/PICO 兼容设计](QUEST_PICO_COMPATIBILITY_PLAN_ZH.md) 为准；LAN 直连证据不能代替同房坐标对齐验收。

**结论供架构决策参考**

建议先验证 **Netcode for GameObjects（NGO）＋Unity Transport（UTP）、Host 权威、客户端手输 Host 的 LAN IP**。官方存在明确的 IP/端口直连入口，Boss Room 也明确允许不接 UGS；2–4 人是本项目的验证目标，并非已测得的性能结论。Mirror 是需要现成 LAN 发现时的候选，FishNet 是后续需要完整预测机制时的候选；普通 Photon Fusion 2 云工作流不满足“断外网仍能新建/加入房间”的首版目标。[NGO NetworkManager 2.7.0](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/components/core/networkmanager.html)、[Boss Room](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop)、[Fusion 2 连接过程](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/connection-process)。

**四个候选的事实与边界**

| 候选 | 官方已确认事实 | 对本项目的意义与限制 |
| --- | --- | --- |
| NGO＋UTP | NGO 2.7.0 文档支持 Unity 6.0+、Mono/IL2CPP、Windows、Android 及这些系统上的 XR；`SetConnectionData` 接收 IP/端口，Host 可监听 `0.0.0.0`。 | 与现有 GameObject/XR 工程方向一致，可把首轮连接范围收敛到 LAN 直连。此版本的客户端 anticipation 没有完整回滚重演；不能把 `NetworkTransform` 当成完整预测或命中回溯。见下方版本及预测说明。 |
| Mirror＋KCP | 官方支持 Host/Server 与 LAN 客户端；KCP 为默认 Transport，文档列为纯 C#、除 WebGL 外可用，默认 UDP 7777；内置 Network Discovery 使用 LAN UDP 广播；核心仓库 MIT。 | 可做 Windows/Android LAN 候选，自动发现有现成组件。仍需验证 Quest 的网络/广播行为；README 的 Unity 6 表述并未验证本工程的精确包组合。不要把宣传中的延迟百分比当成本项目测试结果。 |
| FishNet＋Tugboat | 默认 Tugboat 基于 LiteNetLib，支持可靠/不可靠消息，官方表明确列 Windows、Android；可设置服务器 LAN IP 和端口。核心无 CCU 上限或 CCU 付费墙，支持自托管及 Host；客户端预测属于免费功能。 | 基础 LAN 联机不需要为 Pro 付费。Pro 包含 Lag Compensation、NetworkTransform 外推等选定功能；“预测免费”与“命中回溯补偿免费”不能混为一谈。具体版本、许可及项目接入成本须在实际试验前核对。 |
| Photon Fusion 2 | 标准 Client/Server 建房及加入先经过 Photon Cloud，再尝试直连，失败可中继；Shared Mode 连接云端。官方有 VR Shared 示例。 | 直连成功不等于可完全离线建房。适合学习 VR 同步及评估未来互联网联机，但不作为当前纯 LAN 首选。本轮未评估独立产品或企业自托管协议。 |

上述表格的对应原始资料：

- NGO：[2.7.0 平台与 Unity 要求](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/index.html)、[IP 连接配置](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/components/core/networkmanager.html)、[Client anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/advanced-topics/client-anticipation.html)。
- Mirror：[Host 与 LAN](https://mirror-networking.gitbook.io/docs/manual/general)、[KCP Transport](https://mirror-networking.gitbook.io/docs/manual/transports/kcp-transport)、[Network Discovery](https://mirror-networking.gitbook.io/docs/manual/components/network-discovery)、[官方仓库及 MIT](https://github.com/MirrorNetworking/Mirror)。
- FishNet：[核心能力/无 CCU 付费墙](https://fish-networking.gitbook.io/docs)、[Tugboat 平台与配置](https://fish-networking.gitbook.io/docs/fishnet-building-blocks/transports/tugboat)、[LAN IP 连接教程](https://fish-networking.gitbook.io/docs/tutorials/getting-started/connecting-to-remote-devices)、[免费预测与 Pro 补偿](https://fish-networking.gitbook.io/docs/overview/readme/features)、[Pro 功能边界](https://fish-networking.gitbook.io/docs/overview/readme/pro-projects-and-support)。付费教学项目与 Pro 是独立类别；价格不在本报告中固定。
- Fusion：[Client/Server 建连步骤](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/connection-process)、[拓扑区别](https://doc.photonengine.com/fusion/v2/fusion-choose)、[VR Shared](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-vr-shared)。

**NGO 直连和版本：实际接入前必须确定的内容**

官方 `NetworkManager` 文档把 IP 直连和 Relay 分开：前者通过 `UnityTransport.SetConnectionData` 配置，后者需要 Relay 专用配置。Host 可监听所有本地接口，远端客户端填写 Host 的真实局域网地址；`127.0.0.1` 仅指本机，`0.0.0.0` 是监听地址，不是远端加入地址。结合 Boss Room 的“可不使用 UGS”说明，可据此选择不调用 Authentication/Lobby/Relay 的 NGO＋UTP 直连路径。该判断针对网络连接路径，整款应用的离线能力仍须以断 WAN 冷启动测试确认。[连接文档](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/components/core/networkmanager.html)、[UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/api/Unity.Netcode.Transports.UTP.UnityTransport.html)、[Boss Room README](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop)。

本报告使用 **NGO 2.7.0 文档作为可追溯的能力基线，并未锁定安装版本**。核查当日 Unity 6000.3 包列表转到新版站点，列出 NGO 2.13.3 为 `pre-release`；不能从网页的 `latest`、包编号或“Unity 6 支持”推断此工程已稳定兼容。进入独立试验工程时，需在 6000.3.10f1 的 Package Manager 中核实 released 状态、NGO/UTP 依赖组合和 changelog，完成 Windows/Android 构建后再锁定 manifest 与 lock 文件。[Unity 6000.3 包列表](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.netcode.gameobjects.html)。

NGO 2.7.0 的 anticipation 将即时视觉值与权威值分开，提供纠正/平滑，但官方明确没有完整客户端预测和 reconciliation 的回滚重演循环。**本项目推论**：首版优先本地头手实时跟踪、远端姿态插值、施法即时表现、Host 确认技能与伤害，暂不把精确 PvP 命中回溯或网络刚体对抗作为验收目标。若之后换用其他版本，应重新确认预测能力，不能将此结论无条件外推到未来版本。[Client anticipation 2.7.0](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.7/manual/advanced-topics/client-anticipation.html)。

Fusion 2.0.5+ 的断云恢复说明允许已建立直接连接的玩家在特定断云情形继续游戏，同时可能禁止新玩家加入；这不能证明“无外网冷启动建房”可用。Shared Mode 不适用该 Client/Server 恢复机制。[Connection Lost & Quick Reconnect](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/lost-connection-handling)。

**可阅读的官方示例：4 个 GitHub 仓库**

| 官方仓库 | 建议借鉴 | 必须保留的边界 |
| --- | --- | --- |
| [Unity Boss Room](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) | Host 权威合作玩法、技能/动画延迟掩盖、连接状态机；README 定位了 `ConnectionMethodIP` 与 `ConnectionMethodRelay`。 | 是非 VR 的教学 RPG；README 明确可不用 UGS，但不能整套搬入本项目。示例人数不能当作 Quest 性能证明。 |
| [Unity Bitesize Samples](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.bitesize) | Client Driven、NetworkObject 生成/父子关系、局部网络功能，适合逐项阅读。 | 不同子示例的依赖不同；README 仍含 2022 LTS/桌面平台说明。需选定子目录及提交后审查，不承诺直接适配 Unity 6000.3/Quest。 |
| [Meta Unity-SharedSpatialAnchors](https://github.com/oculus-samples/Unity-SharedSpatialAnchors) | 同房 MR 的共享锚点、加载/定位、玩家原点对齐；包含 PUN 用户共享和 PUN-free 群组共址场景。 | 属于空间对齐扩展，首版异地活动区 VR 不需要；PUN 场景需 Photon 配置，PUN-free 不自动意味着所有锚点服务完全离线。README 示例编辑器为 6000.0.66f2，非本工程验证。 |
| [Meta Unity-SharedSpaces](https://github.com/oculus-samples/Unity-SharedSpaces) | 观察平台社交信息、消息 Transport、NGO 对象复制三层的分工。 | 其传输使用 Photon Realtime，并要求 Photon App ID；不是 UTP 纯 LAN 示例。README 为 Unity 6000.0.59f2+，仍须核验具体依赖。 |

Unity 的 **VR Multiplayer Template** 另有[官方快速入门](https://docs.unity3d.com/Packages/com.unity.template.vr-multiplayer@2.0/manual/index.html)，本次核查页面版本为 2.0.6。它提供头手跟踪、玩家 Avatar、网络交互，适合作为 XR 结构参考；默认入口要求 Connect to Unity Cloud，XRI Network Game Manager 连接 Authentication、Lobby、Relay、Vivox。**不能把此模板描述成开箱纯 LAN**。本次未核实到对应的 Unity 官方 GitHub 模板仓库，因此不将第三方搬运仓库计作官方示例。

Photon 的[官方 VR Shared 文档及下载](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-vr-shared)展示了本地 HardwareRig 与 NetworkRig 分离、头手状态同步、插值和抓取；运行要求 Fusion App ID。抓取的状态权转移与 Shared 拓扑绑定，迁移到 Host 权威时需要重新设计。该页面属于厂商文档示例，未计入上表 GitHub 仓库数量。

**同房 MR 与网络传输分开判断**

共享虚拟场景时，可为每个玩家分配游戏内出生点。若未来两台 Quest 在同一真实房间协作，还需要让两台设备的追踪空间映射到同一世界原点。Meta 共享锚点示例把“连接玩家、共享锚点、对齐原点、同步立方体”分成可辨识步骤；其中蓝牙 `OVRColocationSession` 发现和不可变会话数据也不能替代持续的技能/血量/姿态网络消息。**本项目推论**：将空间对齐保留为独立适配层，当前 LAN 会话和技能同步不应依赖锚点初始化。[Meta 示例说明](https://github.com/oculus-samples/Unity-SharedSpatialAnchors)。

**后续独立原型的验收清单（均未实测）**

1. 记录 Unity、NGO、UTP、OpenXR/XRI 精确版本；Windows 与 Android IL2CPP 均能构建，先通过 2 个实例的基本连接。
2. 同一路由 LAN、断开 WAN 后，从应用完全关闭状态重新 Host/Join；不能用“先联网连好后断网”替代此项。
3. Windows Host ↔ Quest Client、Quest Host ↔ Quest Client 分别测；检查防火墙、路由器客户端隔离、IP/端口配置及 Quest 休眠恢复。广播发现只在后续引入时测试。
4. 两人同时施法：本地反馈及时、远端效果一致、技能事件不重复结算、伤害只由权威端确认；加入/退出及 Host 离开有明确结果。
5. 记录 RTT/抖动/丢包、上行带宽、主线程/GC/Quest 帧时，再从 2 人扩到 4 人。手部姿态与技能事件分别测，不能用编辑器流畅代替头显验收。
6. 同房 MR 启动时再单列锚点权限、空间对齐误差、重新居中、追踪丢失及有/无外网的锚点可用性，不将这些结果混入基础 LAN 传输结论。

资料均来自厂商官方文档或其官方 GitHub 组织。未执行样例项目、未对任何候选做性能排名；以上“建议/推论/验收清单”属于本项目分析，官方来源仅支撑相邻事实。
