# 2026-10-31 交互修复与测试

本轮先定位到三处实际问题：入口只在打开瞬间定位；轻捏前半段射线丢失立即清空高亮目标；移动必须先发生食指位移，射线只是加分项。

## 操作

- 抬左腕，右指点腕饰开关入口；放腕收起入口，腕饰随实际追踪手腕。结印、咒语速查默认保留，画符卡随腕关闭，可在各面板 policy 配置。
- 子面板顶部“跟随 / 固定”切换玩家 Rig 坐标跟随与固定世界位置，页脚显示当前模式。捏住顶部“捏住拖动”条，移动右手调整位置，松开结束；关闭与翻页也可近按。
- UI 射线优先用官方 IHand pointer pose，无可用 pointer pose 时用手腕→食指根部作为稳定回退。轻捏前短时间保留稳定目标，仍校验新鲜度、移动幅度、遮挡；持续捏住不会连点。官方使用独立 pointer pose 和选中目标捕获，没有依据要求两条射线。
- 移动：左食指伸直指向节点，准备0.12秒、驻留0.32秒，路径与落点提示保留；到达后先解除指向再重备，防止连续跳点。节点需真正处于射线容差内，保留碰撞、可达性、结印和 UI 占用保护。
- 电脑右侧新增“麦克风输入与口令命中”，显示输入设备、后端、样本新鲜度、RMS/Peak、有声帧、服务命中和游戏接收。能量不是识别置信度，当前 sherpa KWS API 未提供概率。火球仍需“火球术＋寅印”，仅念咒不会完成组合技能。
- 电脑按钮“关闭所有腕部教学界面”用于恢复现场；世界诊断面板可独立开关。

## 数据与验证边界

音频遥测每0.5秒更新、每5秒记录标量到 Tools/SkillVoice/logs/service.jsonl，不保存原始声音。输入为0应先查静音/串流通路，不通过降低识别门槛掩盖。

本轮修改前备份：Logs/interaction-repair-20261031（parent-backup、navigation-backup、voice-backup）。UI原始磁盘场景没有序列化多份腕部生成根；需同时区别运行时保留子面板、两个诊断世界面板及护盾加持腕饰，不能仅凭截图删除。

回归输出：Logs/official-hand-poses/editor-suite.json；Play检查：Logs/linear-rpg/play-check.json。报告中的 hardwareTested=false 表示合成轨迹和编辑器检查，不代替头显手感验收。

验证完成：本轮编辑模式444项通过，Python单元21项通过，Play集成检查于2026-10-31 20:08完成通过。首次45秒总时限在第34步后超时，保留全部断言、将总时限调为90秒后重跑通过。以上均非真人硬件精度测量。当前Steam输入遥测仍为零能量，待真人说话确认音频路由。

21:05补充：真人反馈完全看不到射线，原可视逻辑仅在命中按钮时显示，已增加面板打开时的未命中搜索线，范围由2米调为3米；空处仍不能点击。新增实际controller未命中仍可见检查。语音原进程已退出，恢复为Steam WASAPI/48kHz；待真人有声验证。

语音生命周期补充：Windows Unity编辑器的SkillVoiceAutoStart在Play时自动启动Tools/SkillVoice/Start.ps1；用名称选择Steam WASAPI输入及其原生采样率，退出后限速重试，退出Play/编辑器时仅停止具有本次owner token的服务。手动启动的服务不会被编辑器关闭。此方案只用于Windows开发，不打入Android/PICO/Quest原生包；独立设备语音仍需平台适配。SkillVoiceReceiver.autoStartInEditor可关闭，Debug显示启动管理状态。

后续真人回归修正：实际采样发现SDK pointer pose位于地面原点而手在1.8米处，现仅接受距手腕30厘米内的有效Aim，否则回退骨骼。已打开UI不再受双手靠近结印门控阻挡。主入口放腕增加相对打开高度下降18厘米、保持原关闭确认时间的条件。结印得分/门槛/覆盖率/每条特征值与权重恢复为独立Debug分区。接收器重复启用先释放本身socket，失败每2秒重试，销毁显式释放。编辑模式450项、Python24项通过。Android原生语音尚未接入，官方有SherpaOnnxKws Android示例，可后续改用应用内识别消除外部Python/UDP依赖：https://github.com/k2-fsa/sherpa-onnx/tree/master/android/SherpaOnnxKws 。

语音生命周期实测：进入Play自动拉起并完成心跳；人为停止具有owner token的服务后自动拉起新进程，后续日志再次命中两次火球术；停止Play后47831/47832均释放，再次启动供真人测试。新的真人输入采样已显示rayOrigin在右手附近，而非地面零点。

施法门控诊断补充：真人日志中火球术口令已接收、寅单帧89.7分覆盖100%仍超时，不能据此认定所有技能条件满足。新增匹配器实际输入间隔/占用/质量/竞争印分差/持续保持进度，并在结印Debug显示；record-input可保存30秒连续交互输入到Logs/interaction-input。点腕明确接触入口取消视线锥限制、允许更靠近头部，保留抬腕、接触、主动靠近、重备与追踪门控。编辑模式451项通过。

21:54 连续实测：`Logs/interaction-input/20261031-214216.jsonl` 的104个采样间隔全部超过技能0.12秒上限，平均0.289秒，腕部稳定状态同样反复重置。后续 `20261031-215027.jsonl` 捕获寅82.6～88.6分、质量/歧义均通过、无输入占用，但间隔0.229～0.346秒，保持仍为0；这是本轮未释放的直接拦截原因。语音Python识别到口令但Unity在RTT超过0.25秒时不接受同步，后续已出现成功同步及游戏接收口令，不能归因为麦克风未工作。

性能对照：停止Profiler录制并清理采样缓存，重新Play后Unity私有内存从约21GB降至约6GB，但仍有长采样间隔。Steam `driver_vrlink.txt` 于21:53有连续Bad link event / THROTTLE EVENT，单次传输延迟300.2ms；无线传输异常有证据，尚不能认定它是唯一性能原因。用户要求改为Steam Link USB有线对照，明确不使用Meta Quest Link。电脑SteamVR 2.18.4，OpenXR仍指向SteamVR；Valve 2.18公告已列USB串流支持，需头显最新Steam Link。检查时ADB设备列表为空、未出现USB/NCM网卡，等待插线后验证。不得把“USB已插入”直接当成“流量走USB”。

USB对照续记：用户两次确认插线/重插后，Windows PresentOnly设备列表仍没有Quest/Meta USB设备，ADB及mDNS均为空，且无NCM/USB网卡。当前故障在USB数据连接建立之前，尚未进入IP/SteamVR配置阶段，不能通过修改IPv4地址或重装SDK处理。Valve官方FAQ原文核实：USB要求Horizon OS v2.5+；头显启动Steam Link后自动创建USB NCM。IPv4兼容方式还要求v2.7+，只在已出现网卡而IPv6 link-local不工作时考虑，不应先改现有网卡。
来源：https://help.steampowered.com/en/faqs/view/0E2C-406B-9135-38A4
