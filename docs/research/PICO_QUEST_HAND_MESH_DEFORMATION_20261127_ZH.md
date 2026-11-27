# PICO 与 Quest 手模变形和遮挡核查

2026-11-27，针对 PICO 测试中的指节拉扯、包裹手指与握拳跑步现象。以下手模方案为调研结论，待同动作真机对照。

## 已核实的做法

- PICO 的 Integration SDK 在 `PXR_Hand` 中更新根部位置和旋转，以父骨空间的关节旋转驱动各指节，并可通过整手缩放适配手掌大小。这样能保留模型设定的指节相对长度。当前工程使用 OpenXR，官方也提供基于 Unity XR Hands 的可视化接法。可用同一关节数据对照这两种显示方式，先确认变形出在追踪骨架还是皮肤。 [PICO Integration SDK 源码](https://github.com/Pico-Developer/PICO-Unity-Integration-SDK/blob/e0740bf309b26a0b9adae33c506514569bfe5d38/Runtime/Scripts/Hand/PXR_Hand.cs) · [PICO OpenXR 手部追踪文档](https://developer.picoxr.com/document/unity-openxr/hand-tracking/)
- Meta 的自定义手模流程保留 SDK 的手部骨架与骨骼对应，按参考姿态对齐网格后再调整权重。Ultraleap 也要求按关节轴校正模型，并逐指检查变形；指尖定位点无需承担蒙皮权重。建议用同一段张手、半握和紧握画面同时显示关节点与网格，检验掌骨轴向、骨段长度及关节附近的权重。 [Meta 手模绑定](https://developers.meta.com/vr/documentation/unity/unity-isdk-customize-hand-model/) · [Ultraleap 绑定流程](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/rigged-hand-support.html)
- OpenXR 允许关节位姿保持有效，同时清除主动追踪位，运行时可继续输出推算结果。遮挡时把显示用的推算姿态视为确定的手指弯曲，会给律印提供不可靠的数据。现行 `SealXRHandsSource` 通过 `Pose` 和 `TryGetPose` 判定可读关节，再从这些关节估算手指置信度。下一次应先同步采集两端原始 `VALID` 与 `TRACKED`、每指判定和丢失前后帧，确认差异来自哪里。短时保持或混合可以先用于显示，手印仍使用独立的追踪置信度。 [OpenXR 手关节定位](https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrHandJointLocationsEXT.html) · [OpenXR 追踪位定义](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrSpaceLocationFlagBits.html)

## PICO 实测日志

- 「火球术」五次进入识别回调，分别为 22:19:08、22:19:57、22:20:00、22:25:05 和 22:25:07。日志均记录「口令不在当前目录」。前面三次的可用技能记录有龙火、衡印和飞炎，需另查当时步骤的术法授权。
- 律印在 22:20:00 出现 80.7% 的匹配，超过 75% 门槛。22:24:07 有龙火通过并释放的记录。部分动作能通过，遮挡时的误判仍待采集原始关节置信度。
- 探索移动的点指转握拳要求腕位、四指闭合和默认约 0.18 秒的采样衔接。中间帧未确认会清掉点指记录。这是可复测的代码路径，本轮真机日志缺少跑步决策与逐指原始数据，尚无法确认现场卡在哪个条件。

可对照 `Logs/PicoLive1127/clip-222400.mp4` 与 `Logs/PicoLive1127/complete-casts.jsonl`。工程入口见 `SealXRHandsSource` 和 `RpgLocomotionController.Exploration`。与本轮飞炎和龙火的特效调整分别验收。
