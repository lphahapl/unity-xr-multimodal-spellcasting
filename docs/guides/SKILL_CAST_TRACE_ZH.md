# 通用施法调试链条

进入 Play 后 SkillInputController 默认自动记录，无需点录制。Inspector「四、施法链条日志」可关闭记录或调整状态采样间隔。所有技能复用同一入口，包括只有口令、只有印、两者结合；不修改技能判定规则。

编辑器日志：`Logs/skill-casts/*.jsonl`；设备包：`Application.persistentDataPath/Diagnostics/skill-casts`。文件名前缀为 UTC，正文 utc 为 UTC，time 是运行时单调时钟。每个控制器启用周期独立文件，session 区分会话，sequence 排序，epoch 表示输入重置代际，attempt 仅关联同步施法请求及结果。异步投掷用 payload 中的 shotId 关联，不能把所有护盾/火球消息都归给最后一个技能。

## 排查顺序

1. `voice_health`：服务、麦克风选择、采样与能量、服务命中、游戏接收状态；没有录制音频原文文件，日志包含识别口令文本。
2. `voice_transport`：接收器是否因时钟、服务会话、序号、口令年龄或允许列表拒收。
3. `voice_received / voice_queued / voice_processing / voice_rejected`：口令在游戏内是否排队、过期或被输入仲裁拦截。
4. `match_snapshot`：各配方分数、质量、歧义、保持时间、骨骼采样间隔、输入占用、待匹配技能和截止时间。手印先于口令也会记录。这里的时间门槛与模板得分是独立条件。
5. `activation_request → activation_accepted / activation_rejected / activation_exception`：对应技能ID与attempt；RPG返回MP、CD、解锁、导航/UI占用或效果拒绝状态。
6. `effect_message`：生成、消散、护盾生效等。不能单靠匹配器“成功”判断特效已生成。
7. `throw_spawn / throw_frame / throw_launched / throw_rejected / throw_cancelled / throw_expired`：payload保留现有投掷观察对象、评分、握持和释放方式。原有详细投掷录制继续保留。
8. `status / input_reset / session_end`：取消、超时、暂停、退出等终止原因。

## P1 房间与伤害

开启施法链条日志后，房间和玩家会在同一个 JSONL 中记录以下事件。按 `sequence` 与进出房间、投掷记录对齐即可查看同一轮战斗。

| 事件 | 可核对的内容 |
|---|---|
| `enemy_detected` | 该敌人本轮首次发现的目标、发现来源、敌人实例和房间租约 |
| `enemy_damage` | 实际接受的命中、攻击者、身体或弱点、HP 前后值、削韧、死亡状态和命中 ID |
| `player_damage` | 玩家接受的伤害、来源招式、原始伤害、护盾吸收和 HP 前后值 |
| `player_damage_rejected` | 该次伤害尝试被拒的原因及命中 ID，可用于排查重复命中 |

只有 `throw_launched` 时只能确认火球已投出。确认房间敌人扣血要找到对应的 `enemy_damage`，训练靶扣血在靶自身的命中记录中查看。

快速查看最新日志：`powershell -File Tools/Read-SkillCastTrace.ps1`。指定历史文件加 `-Path`。脚本只读取，不改变游戏状态。

## 性能与边界

匹配快照默认0.5秒、语音健康2秒、连续投掷0.25秒；施法与取消等关键事件不节流。缓冲写入，每秒刷新，关闭时最终刷新。每文件上限32MB，超过停止该文件记录并警告；IO失败不传播给技能，不改变成功/失败。崩溃可能损失尚未刷新的最后一段。日志不包含完整骨骼，故不能直接重放所有动作；当前匹配门槛是可读快照，不是完整事件溯源。

## 本次故障与后续工作

2026-11-01检查到口令已接收、寅印约95分，但实时骨骼间隔0.27–0.34秒超过0.12秒上限，导致0.07秒保持不断重置。Profiler当时正在录制，已分配内存约24GB；已关闭并清理录制，但不能据此认定低帧率根因已解决。下次佩戴头显、关闭SteamVR主面板后对照新的链条日志。没有为了通过测试放宽印模板或把采样断档当作连续保持。

架构保持：纯 SkillMatchEngine 负责匹配，Controller 负责生命周期与串联，RPG负责MP/CD和权限，Effects负责效果，SkillCastTrace仅负责写盘。后续若需要多人审计/跨设备回放，再引入明确的结构化技能事件接口与请求ID传播；调试文件不能作为联机权威数据。

验证：新增JSONL中文转义、序号/关联、关闭刷新、独立会话、IO失败隔离检查，纳入完整编辑模式回归。真机声音、姿势与释放手感仍需下次实际测试。
