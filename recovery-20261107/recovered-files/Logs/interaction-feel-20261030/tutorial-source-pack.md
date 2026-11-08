# 教学 UI 第一方证据包（交第二账号继续方案审查）

核实时间：2026-10-30。此次只做网页取证和本地代码只读检查，没有实现教学 UI、修改运行时代码或操作桌面。下列事实可以引用；“可参考方向”是设计推断，不代表这些作品公开了相同算法，也不代表本项目已经实现。

## 1. 真实 VR 作品

### Waltz of the Wizard：可查询的魔法 Codex 与口令示例

- 来源：开发团队 Aldin 在 Steam 的官方公告页，文章 **Skully’s Fantastic Fails out now!**，2025-10-27。
- 直达来源：[Waltz of the Wizard 官方公告](https://steamcommunity.com/app/1094390/announcements/)。该动态聚合页当前包含文章；浏览器页内搜索文章标题或 Codex 可定位。
- 已核实事实：更新重做游戏内菜单，Codex 包含地图、魔法语音指令列表，以及对 Skully 可说话语的示例；同文还介绍 Tasks 任务入口。
- 可参考方向：将技能配方与咒语示例整理为随时可打开的术式图鉴，解决玩家记不住全部口令的问题。
- 局限：这份证据**没有证明它使用腕部召唤菜单**，也没有提供咒语示范录音、中文语音识别或准确率数据。不能据此声称它已有我们设想的腕表教学 UI。

### The Wizards – Dark Times：幽灵手动作教学

- 来源：Carbon Studio 团队的 Steam 官方公告 **Patch 1.25 Now Live!**，2024-04-25。
- 直达来源：[The Wizards – Dark Times 官方更新](https://steamcommunity.com/app/1103860/allnews/)。页内搜索 Patch 1.25 或 hand spirits 可定位。
- 已核实事实：更新明确提到多人模式的小型教程通过显示手部幽灵演示如何为法术附魔，并优化其动态表现；还增加出口标识帮助玩家定位。
- 可参考方向：结印教程可用独立的半透明示范手演示姿态/过程，而不是仅提供一张静态正视图。示范手必须与玩家实测骨骼分色。
- 局限：该说明没有给出示范手骨骼资源、识别器或裸手追踪准确率；手势作品可作为教学表达参考，不能当成 Quest 手追踪可靠性的验证。

### Unplugged：裸手游戏中的角色导师

- 来源：Meta Quest 官方博客，2021-10-21。
- 直达来源：[Become a Rock God in Unplugged](https://www.meta.com/blog/become-a-rock-god-unplugged-oculus-quest-platform/)。
- 已核实事实：Quest 版利用手部追踪游玩空气吉他，不要求手柄；Satchel 作为玩家的游戏内导师。页面包含官方演示视频入口。
- 可参考方向：可把复杂动作拆成导师/语音引导的一步一步练习，而非一次堆满文字。
- 局限：这篇文字没有证明具体腕部菜单、评分可视化、中文口令或语音回声抑制方案。本次未逐帧评测视频，不应编造屏幕细节。

以上三个案例支持“图鉴查询、示范手、分步导师”三种教学手段。它们不是三个已经证明采用腕部菜单的案例。

## 2. 官方 UI / 手部交互依据

### Meta：腕部可作入口，复杂面板不要持续跟着手腕

- [Hands UI best practices](https://developers.meta.com/vr/design/hands-ui-best-practices/)，页面标注更新 2026-09-24。
- 已核实：官方建议非惯用手掌心朝上后食指捏合作为游戏内选项入口；不建议交互菜单持续跟随手腕，因为移动目标难点击且会误触。允许从腕部召出，但出现后宜固定在世界空间；复杂内容放在玩家前方。
- 已核实：裸手没有手柄震动，选择成功需要明确的声音与视觉反馈；要提供 hover / pressed 状态。
- 局限：这是通用设计建议，不是本项目输入冲突已解决的证据。左右手已有移动、闪避、结印和投掷，仍须独立仲裁。

### Unity：Hand Menu 是可复用机制，不是必须引入的新框架

- [Unity XRI 2.5 Hand Menu](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@2.5/manual/hand-menu.html)。
- 已核实：提供手/控制器跟随、根据掌心方向显示菜单、左/右/任一手选择，以及跟随距离、速度等配置。
- 局限：本次核实的是 **2.5 文档**，不是项目现装版本。`Packages/manifest.json` 当前没有 `com.unity.xr.interaction.toolkit`。不要仅为了该样例直接混入一套 XRI 交互链；可借鉴检测与显示职责拆分。
- 注意：组件能跟随手腕，不等于复杂界面应该永远跟随。与 Meta 建议结合时，应区分“入口锚点”与“正式阅读面板”。

### Microsoft：手菜单适合短操作，额外朝向/注视条件可降低误开

- [Hand menu — Mixed Reality design](https://learn.microsoft.com/en-us/windows/mixed-reality/design/hand-menu)。
- 已核实：建议手菜单按钮少、交互短；长时间复杂交互可改世界固定菜单。仅掌心向上容易误开，可增加手型和注视条件；文中明确包括头部注视。还讨论双手重叠导致追踪困难。
- 局限：是 HoloLens/MRTK 设计经验，不是 Quest 专有性能结论；不要把该文中的腕部系统键和厘米阈值直接当成 Quest 硬规则。

### Unity：头部朝向可作没有眼动输入时的备选

- [XRI Examples — Gaze](https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/Gaze.md?plain=1)。
- 已核实：官方示例支持 eye gaze 与 head tracking fallback，并有 coaching cards / dwell 等示例。
- 局限：本项目当前讨论中的“视线”应写成头部朝向或头部射线，不能冒充实测眼动焦点。这里没有声称 Quest 3 当前提供眼动数据。

## 3. 本地代码事实，防止第二账号把提案写成已实现

- 当前 `Assets/SealCombat/Runtime/Core/SkillDefinition.cs` 的触发模式只有 `GestureOnly`、`VoiceOnly`、`VoiceAndGesture`。`sealName` 是单印，`SupportedSealCount = 1`。教学卡应引用同一技能 ID / 配方，而不是另存一份容易失同步的规则。
- 本轮 `rg` 搜索 `Assets/SealCombat/Runtime` 和 `Assets/SealLab/Runtime` 未找到 Glyph / 画符运行时；`docs/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md` 也明确画符尚未完成。**画符教学只能写可插拔预留/演示预览，不得写成可识别练习已存在。**
- `Assets/SealCombat/Runtime/SkillTestController.cs` 已有 `ActivationOverride`、`GestureInputBlocked`、`VoiceInputBlocked`。`CancelInputOnly()` 清除计划口令、延迟语音队列，调用识别引擎 Cancel 并重置取消手势；注释明确它不会取消已经提交的效果。
- `SkillTestController.ProcessVoice()` 会先处理取消口令，再检查 `VoiceInputBlocked`；因此仅设置该回调**不能保证屏蔽所有演示音频引发的事件**，尤其是取消口令。
- `Assets/SealCombat/Runtime/Rpg/LinearRpgController.cs` 已将阻断回调用于 `!CanAct || locomotion.NavigationActive`，同时接管激活/取消。新 UI 不应直接覆写这些回调、破坏已有 RPG 仲裁。
- 当前包：Meta Core / Interaction / Interaction OVR `207.0.1`，XR Hands `1.7.2`，OpenXR `1.15.1`，UGUI `2.0.0`。这些是本地 manifest 事实，不是联网确认的推荐版本。

## 4. 给第二账号的边界与需设计项

当前输入约束：左食指用于移动选点、右掌移动用于闪避、右手握持松开用于火球投掷、双手靠近有结印保护、放下双手用于取消。新增入口不能把常见战斗姿势直接当全局菜单键。用户本轮只要求先调研确认，尚未要求实施教学系统。

请在方案中明确区分：

1. 腕部入口、眼前召唤后世界固定的阅读面板、固定教学台各自用途。
2. 浏览与练习分别由谁消费输入；退出后怎样避免残留手势立即闪避/施法。
3. 咒语示范播放期间及尾音怎样阻断匹配、处理延迟结果；不能只依赖耳机“不漏音”。
4. 真实识别分数/追踪有效性与教学进度分别展示；缺少可见骨骼不能简单写成玩家做错。
5. 画符只预留教学适配接口；当前不编造识别结果。
6. 建议的距离、停留时间、姿态阈值写成待真机标定的初值，不作为来源已经验证的项目结论。

未做：没有下载第三方游戏资源、没有新增 SDK、没有搭场景、没有运行玩家实验。本证据包交第二账号用于方案和文档，不能替代用户真机可用性测试。
