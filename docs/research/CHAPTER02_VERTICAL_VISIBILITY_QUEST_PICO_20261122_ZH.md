# 《断衡悬道》40 米高差的 Quest / PICO 性能评估

> 后续节奏修订已把①②实走面统一到 +80 m，40 m 纵深保留为崖景。以下 40 m / 12 m 对照是当时的性能研究方案；当前白盒与成品按 [V2.4 场景距离规格](../architecture/第二章_断衡悬道/04_场景距离规格.md) 放样。

核查于 2026-11-22。建议首轮白盒保留①出关门径台到②祭坛的 **40 m 高差**，优先控制断桥、祭坛边沿和吊篮中段的可见内容。官方验收看应用帧率、显示刷新率和掉帧。查阅的规范没有按米限制关卡尺寸。28 × 22 m 的祭坛与 36 × 28 m 的工坊可以保留战斗走位空间。[Meta 性能验收条款](https://developers.meta.com/vr/resources/vrc-quest-performance-1/) · [PICO XR Check](https://developer.picoxr.com/document/distribute/functional-requirement-checklist-for-official-apps/)

## 40 米对这张图意味着什么

①位于 +120 m，②位于 +80 m，断桥净缺口 4 m。其后 A 篮从 +80 m 降到④的 0 m，B 篮从 0 m 降到 −80 m。三个区段应分别预算。仅把同一片简模崖壁拉高，顶点数、材质数和纹理张数可以完全相同。真正会变的有两件事：高处可能同时看到更多物体，长路可能需要常驻更多独有资产。[本轮白盒尺寸图册](D:/UserProfile/Downloads/断衡悬道_动线与白盒尺寸图册.pdf) · [Unity 可见性与剔除](https://docs.unity3d.com/6000.0/Documentation/Manual/OcclusionCulling.html)

**如果 40 m 全由步行下坡消化，路径长度先于性能成为设计问题。** 按直线恒坡估算，40 ÷ tan(15°) ≈ 149 m，10° 时约 227 m，均为水平展开长度，尚未计入折返。若目标是“修完断桥很快进入背石机偶战”，值得并排试玩另一个版本：②仍在 +80 m，将①暂放 +92 m，实走高差 12 m，崖下远景继续提供纵深。这个备选只改①→②；两段吊篮行程独立。坡角只是计算示例。

## 两个平台各按什么验

| 设备 | 官方依据 | 本章首轮测试 |
|---|---|---|
| Quest 3 | Meta 正式 VRC 将允许的刷新率与应用渲染率分别规定；通常要求至少 60 fps，符合条件的 AppSW 段另有口径。 | 独立运行，先按原生 72 Hz / 72 fps，帧周期 13.89 ms。用 OVR Metrics Tool 看 App FPS、App GPU Time、Stale Frames，同时用 Unity Profiler 查主线程和渲染线程。[Meta VRC](https://developers.meta.com/vr/resources/vrc-quest-performance-1/) · [OVR Metrics 指标](https://developers.meta.com/vr/documentation/unity/ts-ovrstats/) |
| PICO 4 普通版 | XR2 Gen 1，官方规格列 72/90 Hz。PXRC 对正式游戏、交互应用给出 72 fps，AppSW 有单列规则。 | 作为 PICO 首测机，先验原生 72 Hz / 72 fps。用 PICO Developer Center 的设备性能监控或 Metrics HUD 记录应用帧率、显示刷新率、眼缓冲大小、掉帧、内存及温度。[普通版规格](https://www.picoxr.com/global/products/pico4/specs) · [PICO XR Check](https://developer.picoxr.com/document/distribute/functional-requirement-checklist-for-official-apps/) · [设备性能监测](https://developer.picoxr.com/document/unity/monitor-device-performance/) |
| PICO 4 Ultra 消费版 | XR2 Gen 2，消费版规格列 90 Hz。 | 读取设备实开刷新率。以原生 90 Hz 验证时，每帧 11.11 ms。普通版和 Ultra 分开出报告，保留各自眼缓冲与渲染设置。[Ultra 官方规格](https://www.picoxr.com/global/products/pico4-ultra/specs) |

建议以原生刷新率为这轮共同口径，关闭补帧或明确记录其状态。按 20% 余量设内部美术预警线：72 Hz 时 CPU 关键线程和 GPU 的 P95 分别争取 ≤11.1 ms；90 Hz 时分别争取 ≤8.9 ms。CPU 与 GPU 可并行，时间分别看。最终还要核对应用实际帧率、P99 与连续掉帧，尤其是载入和切换那几帧。这些时间目标是本项目提案。[Meta 测试与性能分析](https://developers.meta.com/vr/documentation/unity/unity-perf/) · [PICO XR Profiling Toolkit](https://developer.picoxr.com/document/unity/xr-profiling-toolkit/)

## 白盒第一轮怎么摆

| 玩家位置 | 建议先做的改动 | 应保留的线索 |
|---|---|---|
| ①门口、断桥与②祭坛 | 在修桥后下行的弯道，用一段实体崖肩挡住③院内下半部。远处只画屋顶和塔、梁的简模；工台、车、料架、屋内火光靠近③才显示。①到②维持一条清楚的路。 | 从桥头看得到对岸和下一步的落脚处；进入②后能辨认背负镇石的机偶、护卫与 A 篮入口。 |
| ②战斗和 A 篮牵石 | 全精度留给当前战区。错开的三栋工坊按栋或屋段做 LOD；小道具可分组剔除。③未进入交战时，避免远处机偶和粒子持续完整模拟。 | 当前敌人、掉落的同一块镇石、篮槽与战斗地面保持有效；玩家回头或换站位仍能继续牵石。 |
| A/B 吊篮中段，回望工坊或谷底 | 这是重点压测机位：两层地面、长梁与工坊可能同时进视野。巨衡和谷底轮廓常显；在进篮前异步预取目标层，分散实例化和激活。旧区只在遮挡成立或已换上远景代理后降档。有多区驻留压力时再拆加法场景或 Addressables 分组。 | 玩家转头时看得到出发地的轮廓与目的地。共用的梁、两篮、地面碰撞及解谜状态独立保留；加载不能打断篮体运动。 |
| ③近战与④中控 | 只对需要出招预判的近处机偶保留动态阴影。先用烘焙明暗表现工坊，给远房去掉小件投影。崖壁沿视线分块，复用岩材、木材和贴图；用远景材质表现谷气，再对比半透明雾片、炉烟、符线的像素开销。 | 院心 18 × 14 m 净空、主榫、返回④的路线均保留。冻结远区前保存清场和取榫状态。 |

Unity 的 LOD 根据物体在屏幕中的相对高度切换。远处的大梁与小箱子应分别设阈值。内置遮挡剔除有 CPU 查询和数据驻留成本；崖肩、门洞、实墙形成有效遮挡后再局部烘焙并实测。敞开的峡谷优先用简模与分组显示，不把薄雾或调短整台相机的远裁剪当作遮挡。[Unity LOD Group](https://docs.unity3d.com/cn/6000.0/Manual/class-LODGroup.html) · [Unity 遮挡剔除](https://docs.unity3d.com/6000.0/Documentation/Manual/OcclusionCulling.html)

## 项目现状与真机验证

Android 默认档位是 Mobile，使用 Forward、4× MSAA、渲染比例 0.8，HDR、全局深度和不透明纹理请求为关。主光阴影已开，1024、单级、距离 50 m。移动 Renderer 当前有启用的 Decal Renderer Feature，最大绘制距离 **40 m**；这个参数只管贴花。若断桥或祭坛远距读不清，先检查关键提示是否依赖贴花。主光阴影先比当前 50 m 与 25 m 对照，并检查战斗预警可读性。若仍然受 GPU 像素耗时限制，再分机测试固定注视点渲染；Quest/PICO 均需读回实际状态并检查周边视野的符纹、文字与索线。[Android 画质](../../ProjectSettings/QualitySettings.asset) · [移动 URP](../../Assets/Settings/Mobile_RPAsset.asset) · [移动 Renderer](../../Assets/Settings/Mobile_Renderer.asset) · [Unity 6.3 独立头显 URP 建议](https://docs.unity3d.com/6000.3/Documentation/Manual/xr-untethered-device-optimization.html)

工程依赖包含 Unity OpenXR 和 Meta XR；直接依赖清单中未见 PICO 专用 SDK。在 PICO 上先验证独立包启动、双眼、手部输入、画符和牵石，再验证性能。首次室外历史报告里，Quest 3 首章庭院静态 App GPU 样本 P95 为 5.33 ms；该轮隔离了部分业务逻辑，不能拿这个数抵扣第二章战斗预算。本次核对到的是 GlyphLab 试验场与第二章设计稿，工程中尚未找到正式悬道场景，**第二章两平台都还没有可签收的实测帧时间**。[包清单](../../Packages/manifest.json) · [现有试验场](../../Assets/Scene/GlyphLab.unity) · [历史测量条件](QUEST_PICO_EXTERIOR_PERFORMANCE_20261109_ZH.md)

白盒具备后用同一版本、设备档位、眼缓冲、敌人数和特效依次比较 **A：40 m 全部细节可见；B：40 m 加遮挡与远景 LOD；C：12 m 实走高差，沿用 B 的资产与规则**。先比较 A/B 看可见性收益，再比较 B/C 看压缩高差对帧时间和入战时间的影响。每机重复走断桥俯视、背石机偶交战、篮内牵石、吊篮中段转头、工坊清场五段，另做持续循环，记录冷启动和热态。比较原生应用 FPS、CPU 主/渲染线程和 GPU 帧时、丢帧、两区同时驻留的峰值内存与从修桥完成到首次接敌的秒数。B 稳帧、入战节奏也合适就保留 40 m；若 B 仍慢，先按 CPU、GPU 或载入尖峰定位，若主要问题是步行空档则优先选 C。[Meta 设备端采样](https://developers.meta.com/vr/documentation/unity/ts-ovr-best-practices/) · [PICO 设备端采样](https://developer.picoxr.com/document/unity/monitor-device-performance/)
