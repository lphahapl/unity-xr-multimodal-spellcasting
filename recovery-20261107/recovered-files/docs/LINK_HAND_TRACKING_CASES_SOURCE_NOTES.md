# Quest Link 手部追踪案例与相册截图核对

核对日期：2026-10-28。遵照用户要求，未再访问或启动教育对照工程。本轮只读取公开来源、通过ADB截图及复制用户指定的近期相册截图，没有改变Unity或Link配置。

## 已核对的一手来源

1. [Meta员工公告：Link Launch Issue Resolved](https://communityforums.atmeta.com/pairing-connection-12/meta-quest-link-update-link-launch-issue-resolved-174424)。页面日期为2026-01-20，作者标记Meta Employee。公告明确说明：当时的Dash暂不显示手模型，但射线和交互仍可工作。它是历史版本说明，不能证明当前208版本所有手追踪故障均属正常；能证明“Dash必须先显示手，Unity才可能取得骨骼”不是可靠前提。
2. [开发者第一手案例：Hand tracking not working in Quest Link](https://communityforums.atmeta.com/discussions/dev-quest/hand-tracking-not-working-in-quest-link/1208563)。发帖者报告原生主页手追踪正常，而Link只识别控制器、Unity Editor也受影响，重装PC Link未解决。后续又报告Unity恢复后，Link界面偶尔仍没有手。属于历史用户经历，没有可保证适用于当前版本的修复步骤。
3. [Meta官方：Hand Tracking Overview](https://developers.meta.com/vr/documentation/unity/unity-handtracking-overview/)。页面更新日期为2026-09-14，明确支持Windows上的Quest+Link+Unity Editor手追踪开发。不能将普通PCVR游戏的手追踪支持范围直接套用到编辑器开发。
4. [HTCC作者的Oculus Hand Tracking说明](https://github.com/fredemmott/HTCC/blob/master/docs/hardware/oculus-hand-tracking/README.md)。其工具文档将Link手追踪列为开发功能，要求头显原生手追踪、开发者账号、重启PC客户端及开启Developer Runtime Features，并描述过更新或实验分组造成的不稳定。作者明确测试设备为Quest 2和Quest Pro，不能直接当作Quest 3当前版本的确诊依据。

## 本机截图

- ADB直接截屏第一次捕获Meta加载标志；第二次为黑色。ADB前台应用查询确认当时是头显Link串流客户端。黑色截屏不能证明用户实际看到黑屏，也不能用于判断手模型是否存在。
- 按用户指示复制相册中今天最近两张手动截图，未查看其他日期照片。
- `Logs/LinkDiagnostics/album-link-203741.jpg`：Link灰色Dash应用库，含Unity入口、悬浮菜单和Meta按钮提示，无可见手模型。
- `Logs/LinkDiagnostics/album-link-183003.jpg`：SealLab应用，显示UNAVAILABLE/LOW和N/A，同样有系统按钮提示。
- 系统界面的提示图标不能替代Unity实际骨骼有效性检查。截图可确认显示现象，不能单独说明骨骼是否在系统内部被解算、是否已传至PC，或哪一项设置导致数据失效。

## 当前结论与下一步

Dash看不到手模型有官方历史说明，不能仅凭该现象判定硬件或Unity工程有问题。本机另有已完成的原生接口诊断和A/B测试，确实显示Unity未取得有效手数据，详见`docs/HAND_TRACKING_AB_TEST_ZH.md`；这是独立且尚未解决的问题。

当前证据不足以认定SDK207、Link208或某一特性冲突为确定根因。下一项可验证动作是正常重启Link运行服务并重新连接头显，再用原生状态诊断比较；如仍失败，考虑同一工程的Quest原生APK最小验证，绕开Link传输层。没有执行重装、降级、删除缓存、注册表补丁或来源帖子中的其他命令。

## 执行记录

父代理完成ADB、相册查看、官方公告与原始帖核对。Antigravity/Gemini公开来源提取子任务超时、未产生文档；已确认进程停止、无部分文件并释放范围。未切换其他外部代理，本记录由父代理编写。

## 补充：v74 头显与 Link 208 是否兼容

2026-10-28 后续核对：没有检索到官方对“v74 + Link 208.0.0.84.535”的手追踪兼容承诺或明确不兼容公告，不能仅从版本号差距确诊。

- [Meta 当前 Link 系统要求](https://www.meta.com/help/quest/140991407990979/)的 Software requirements 明确要求 PC Link 应用保持最新，并让头显运行最新操作系统；该页面未给出 v74 与 208 的跨版本兼容矩阵。
- [Meta 官方开发配置文档](https://developers.meta.com/vr/documentation/unity/unity-link/)要求启用 Meta OpenXR runtime、Developer Runtime Features，并在启用功能后重启编辑器；本机这些步骤已完成。
- [开发者原始讨论：Version mismatch v206 / v205](https://communityforums.atmeta.com/questions-and-discussions-26/version-mismatch-v206-v205-179165)：2026-09-29/30 的回复明确涉及头显 207 / PC 208，报告 Link 手方向和右眼异常；另一位开发者报告 SDK 207 原生部署的头部/手部追踪正常。这是开发者经历，不能推断其问题一定由版本错配导致，也不能作为 v74 的确诊证据。
- 本机同一工程：原生 APK 已获取有效双手 IHand，用户确认正常；Link 已完成多次采样却未取得有效双手数据。确定的是当前 Link 手追踪链路没有工作，具体原因仍待验证。

建议保持已验证的 SDK/场景不变，先将 Quest 更新到设备能够获取的最新正式固件，再重复同一状态采样。这是依据官方软件要求做的诊断步骤，不是保证更新必然修复的结论。本轮搜索没有更新任何软件或设备。
