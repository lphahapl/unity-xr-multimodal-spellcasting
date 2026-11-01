# 扩展与调试接口：保持现有结果的结构重构

本轮约束：不改结印阈值、技能组合窗口、动作优先级、投掷算法、MP/CD、移动和UI操作规则。没有放宽采样断档门槛。修改前文件保存在 `Logs/architecture-refactor-backup`。

## 职责与调用方向

`SkillTestController → ISkillActivationPolicy → LinearRpgController原有TryActivate → Player/SkillEffects`。

以前技能主控为了读取诊断状态，直接查找RPG组件，通用输入层反向依赖具体玩法。现在通过策略接口取得执行结果与说明。RPG显式实现接口，仍调用完全相同的私有TryActivate，没有复制MP/CD或效果判断。

### ISkillActivationPolicy

- `TryActivate(SkillDefinition)`：同步执行原有准入及效果流程，返回成功/拒绝。不是匹配器，不处理声音或骨骼。
- `ActivationStatus`：读取上次结果供日志解释；读取异常只影响诊断，不更改已执行技能的结果。
- `OwnsCooldowns`：是否由策略拥有冷却。当前RPG为true；匹配器不重复计算CD。
- 在 `SkillTestController.Start` 前绑定 `ActivationPolicy`。不要运行中热换冷却所有者；当前没有实现冷却状态迁移协议。
- 旧 `ActivationOverride` 保留，显式设置时优先于策略；旧演示场景不绑定策略时仍直接调用Effects。RPG初始化清除旧回调并绑定自己，维持原来初始化覆盖回调的行为。
- 这是本机主线程接口，不是联网RPC。未来网络权威应实现独立策略，不能把DebugCast直接暴露给客户端请求。

### 已有输入与效果扩展口

- `ReceiveVoice(phrase, endedAt)`：走真实的队列、时钟与输入仲裁；endedAt必须使用项目单调输入时钟。
- `ScheduleVoice(phrase)`：免手操作的延迟模拟口令。
- `Cancel()`：执行现有取消策略。
- `InputClock`：确定性测试时钟。不要用它伪造真机连续骨骼。
- `SkillEffects.ThrowGripProvider / WristPoseOverride`：手形与腕骨的替代数据输入，仍经过原有投掷检查。
- `SkillEffects.ThrowObserved`：订阅投掷诊断；`Message`：效果消息。它们不替代游戏权威状态。
- `TraceCast(...) / CastTracePath`：扩展诊断事件与当前日志路径。

### 腕部输入观察

`WristMenuController.LatestInput`返回最近一次实际消费的 `WristMenuInput` 值拷贝。修改返回值不会写回控制器。编辑器 `live-input` 与 `InteractionInputTrace` 已改用它，移除了按字符串反射私有input字段的生产调试依赖。此接口是观察口，不会推进状态机，也不是注入输入的方法；原有Tick仍负责唯一更新。

## 验证与已知问题

`Tools/Request-RpgCheck.ps1 -Action architecture-interface-check` 运行接口等价、日志、口令时钟与核心匹配检查，不保存或切换当前场景。完整回归也包含新增接口检查。

真实输入低频造成保持计时清零的问题仍待下一次佩戴头显采样。它是运行性能/采样问题，不应借结构重构悄悄改变时序要求。控制台和施法JSONL现已显示间隔、保持与拦截原因。

后续审查清单（不在本轮改变行为）：

- 将越来越多的诊断拼接从SkillTestController移入专用观察器；保持关键事件顺序及异步shotId归属。
- 真机记录正常与故障帧率，区分编辑器负担、应用失焦节流、串流与XR采样停顿。
- 联机前定义可序列化的施法请求/结果与幂等ID，避免直接复用本机bool回调作为网络协议。
- 只在确实存在第二实现或测试需求时增加接口，避免为每个类制造空泛抽象。
