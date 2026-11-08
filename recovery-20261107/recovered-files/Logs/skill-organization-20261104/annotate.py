import json
import re
from pathlib import Path

root = Path('Assets/SealCombat/Runtime')
files = [Path(x) for x in json.loads(Path('Logs/skill-organization-20261104/runtime-files.json').read_text())]

# Each replacement is limited to an existing comment, so serialized names and code stay intact.
translations = {
    'Install before SkillTestController.Start. Execute synchronously on the Unity main thread.': '在 SkillTestController.Start 之前安装，由 Unity 主线程同步调用。',
    'The implementation owns game-specific admission (resources, cooldowns, input ownership)': '实现类负责玩法层准入，包括资源、冷却和输入占用，',
    "and effect execution. False must preserve the implementation's existing rejection semantics.": '并负责调用效果执行器；返回 false 表示按该玩法的规则拒绝本次激活。',
    'Best-effort bounded JSONL diagnostics; failure never changes gameplay.': '将施法事件写入有容量上限的 JSONL 日志；记录失败时继续执行玩法。',
    'One conflict policy shared by editor validation and runtime matching.': '编辑器与运行时共用的配方校验，统一检查技能标识、口令和手印冲突。',
    'A future sequence recipe can replace this single-seal input without changing effect execution.': '当前输入配方仅支持单印；以后扩展印序列时可继续复用效果执行接口。',
    'Single-thread input matcher. Call Tick once before the first input. No effect or Unity scene ownership.': '单线程输入匹配器。首个输入前先调用 Tick 初始化时钟，匹配成功后通过回调申请激活。',
    'The RPG session owns group cooldowns; legacy scenes retain their existing local cooldowns.': '为 true 时由 RPG 会话管理共享冷却组；为 false 时使用匹配器内部的技能冷却。',
    'The recorder and HUD ask for text on demand. Keeping string formatting out of Observe': '日志与界面按需获取诊断文字，',
    'lets the warm, high-frequency skeleton path stay allocation-free.': '避免在每次骨骼 Observe 调用中反复分配格式化字符串。',
    "LateUpdate's Tick can be a few milliseconds later than the lab's sample timestamp.": 'LateUpdate 的 Tick 时间可能比本帧骨骼采样时间晚几毫秒。',
    'Hide the expired intent immediately, but allow that already sampled frame to finish.': '先关闭已过期的等待提示，再给已经采集的合法边界帧一次完成匹配的机会。',
    'An occupied hand cannot bank another held-seal activation. Discard any': '手部被占用时清除本轮持续保持产生的重复证据，',
    'repeat already prepared in this cycle and start again after hand release.': '待占用结束后重新累计保持时间，避免一松手就补发。',
    'A still-unconsumed combination is usable while the player keeps the seal.': '组合技能的证据尚未消费且手印持续合格时，可以滚动更新有效区间。',
    'Roll its interval, retaining the same physical action and allocation. A loss': '滚动时复用同一动作编号及证据对象；一旦质量不合格，',
    'of quality resets Start above, so a fresh full hold is needed before rolling.': '上方逻辑就会清除 Start，必须重新完成一次完整保持。',
    'Only an actual, qualifying sample at the instant of consumption can seed': '只有消费当下仍然合格的新鲜采样，才可作为下一段保持区间的起点。',
    'the new interval. A delayed voice matched against history cannot backdate it.': '迟到口令匹配历史证据时，不能把新一轮保持的起点倒推到过去。',
    'Mirror SealProfile.ValidateRules without allocating its temporary HashSet every sample.': '采用与 SealProfile.ValidateRules 相同的校验规则，复用临时集合以减少逐帧分配。',
    'Only this scratch set is reused; score history never retains mutable profile/rule objects.': '评分历史只存不可变的评分事实，临时集合和可编辑模板不会写入历史缓存。',
    'Immutable facts only: no SealProfile, FeatureRule, RuleScore or caller-owned array references.': '缓存评分所需的数值快照，避免持有调用方会继续修改的模板、规则和数组引用。',
    'Shared label normalization; this is not natural-language intent recognition.': '统一口令标签的格式，供配置校验和完整口令匹配共用。',
    'Read-only input recipe captured when the matcher starts.': '匹配器初始化时生成的只读配方快照，供界面和诊断展示本轮生效参数。',
    'Scores fresh observations and proposes one action. Never spawns or bypasses collision/ownership gates.': '根据新鲜观测对后拉前送和伸手保持评分，输出发射候选，由效果执行器继续检查占用与碰撞。',
    'A far hand at creation cannot pre-qualify hold. Establish a fresh near baseline first.': '生成时手已伸远不能直接满足保持条件，先建立一次新的近身基线。',
    'Physical grip-release edge and recent hand trajectory. No scene objects, aiming rays or effect spawning.': '跟踪握持到松手的边沿及最近手部轨迹，输出速度估计，由效果执行器完成瞄准与生成。',
    'A withdrawn release needs a new confirmed grip; never reuse its old trajectory.': '松手被撤回后必须重新确认握持，并重新采集轨迹。',
    'Freeze at Closed -> Neutral/Open, not at the delayed all-fingers-Open result.': '在握紧转为中间态或张开时冻结松手起点，避免等待完全张开造成延迟。',
    'Exclude the newest tail so opening/prediction noise cannot redirect the throw.': '剔除最新一小段轨迹，降低张指和追踪预测噪声对方向的干扰。',
    'Weighted least squares p(t)=intercept+v*t, with two Huber reweight passes.': '用加权最小二乘拟合 p(t)=截距+速度*t，再用两轮 Huber 重加权减弱异常点影响。',
    'Time is relative to the release edge and positions to the first sample for numeric stability.': '时间相对松手边沿、位置相对首个采样点计算，以改善数值稳定性。',
    'Shared, read-only effect configuration. Mutable effect state belongs to the executor or a registered lifetime.': '共享效果配置的扩展基类；持续时间、生成物等可变状态由执行器或注册的生命周期对象持有。',
    'Return true only after activation succeeds. On false, leave no new effect behind.': '效果成功生效后才返回 true；返回 false 时应清理本次创建的效果资源。',
    "An activated custom effect's owned runtime state. Register once; the executor ticks it and calls Cancel": '自定义效果持有的运行时状态。注册成功后由执行器逐帧 Tick，并在结束、取消或停用时',
    'once when it ends, is explicitly cancelled, or the player pauses/disables. Cancel must release owned resources.': '调用一次 Cancel；Cancel 负责释放该效果拥有的资源。',
    'The head chooses a point; the ball flies from its physical release position toward that point.': '保存头向瞄准点与手部发射点之间的飞行解算结果，包括遮挡信息。',
    "Observation time seam for deterministic replay. Production uses the snapshot's realtime clock.": '观测时钟注入点，用于确定性回放；正常流程使用追踪快照的实时时间。',
    'Future actor adapters can bind this seam; production scenes use the existing tracked source by default.': '可为其他施法者注入腕部位姿；正常场景默认读取现有追踪来源。',
    'Values are captured once at activation. Editing a shared SO or activating a different': '激活时冻结本发投射物的参数。之后编辑共享配置或激活其他技能，',
    "skill cannot change an existing held ball's motion, damage or eventual flight settings.": '都不会改变当前手持物的运动、伤害及发射后的飞行参数。',
    'This invocation may have created a held visual before a later setup step failed.': '后续初始化可能在持握视觉已经创建后失败，需要清理本次生成物。',
    'A custom definition which registers then rejects/throws cannot leave a new lifetime behind.': '自定义效果注册后再失败或抛错时，撤销本次激活新增的生命周期对象。',
    'Definition implementation entry. TryActivate owns successful activation counting.': '护盾配置的执行入口，设置容量与到期时间；成功次数由外层 TryActivate 统一统计。',
    'A purely visual failure must not refund an already committed, working shield.': '护盾数值已经生效后，纯视觉创建失败只记录告警。',
    'Definition implementation entry. Captures all projectile parameters before creating the held instance.': '投射物配置的执行入口，先冻结参数，再占用持握槽并创建手持实例。',
    'A mature physical release wins. An unfinished release must not starve assisted actions.': '已确认的物理松手优先；尚未确认的松手候选允许辅助动作继续竞争。',
    'Preserve the candidate and score before resetting. No other mode retries this action.': '重置前保存本次候选和评分；同一个动作只尝试一次发射方式。',
    'Never treat a reused snapshot as new hand motion.': '重复读取的骨骼快照不累计为新的手部运动。',
    'Both world movement and movement relative to the head must advance forward.': '同时要求手在世界空间和相对头部的空间向前移动，避免转头或身体位移误触发。',
    'The crosshair ray supplies the aim point; the ball flies toward it from the hand.': '头向准星射线给出目标点，投射物从手部实际位置朝该点飞行。',
    'Independent of grip/throw scores: a stationary raised wrist can aim, but cannot fire by aiming alone.': '瞄准预览独立于握持和投掷评分，抬腕静止时也可预览，发射仍需通过动作检查。',
    'A surface between the head and the launch plane must not send a ball backwards or beyond cover.': '头部与发射平面之间存在遮挡时，限制目标点，避免朝后发射或越过掩体。',
    'Physical flight sweeps a sphere, so an off-axis wall can intercept an unobstructed gaze ray.': '实际飞行使用有半径的球形扫掠，因此准星射线旁的墙体也可能挡住投射物。',
    'Transfers lifecycle ownership only when true is returned. The caller owns cleanup after rejection.': '仅在返回 true 时接管生命周期；注册被拒绝时仍由调用方清理资源。',
    'An existing handle cannot be registered twice; registration during cancellation is rejected.': '同一对象只可注册一次；清理期间拒绝新注册。',
    'User effects may register or cancel other effects inside Tick. Snapshot entries, not': '自定义效果可在 Tick 内注册或取消其他效果，因此遍历时使用登记项快照，',
    'runtime objects, and check ownership again before touching each one.': '每次操作前重新确认登记项仍归本执行器管理。',
    'The portion of incoming damage that remains after this shield absorbs it.': '护盾吸收后剩余的伤害，由调用方继续扣除生命值。',
    'Cancel priority: held projectile, newest custom lifetime, then shield. Launched projectiles continue.': '按手持投射物、最新自定义效果、护盾的优先级取消一项；飞行中的投射物继续运行。',
    'Low-hands cancellation preserves shields and already flying projectiles.': '放低双手时取消尚未发射的物品，已生效护盾和飞行中的投射物继续运行。',
    'User-edited visuals must never add damage or interfere with aim/collision queries.': '关闭纯视觉资源中的碰撞器，避免它们影响瞄准射线或额外参与伤害碰撞。',
    'One swept sphere per movement step; no trigger callbacks or persistent damage.': '每个运动步进行球形扫掠，按命中锁存保证单次伤害结算。',
    'SphereCast does not detect a sphere already overlapping a surface at launch.': 'SphereCast 无法覆盖发射时已经重叠的表面，需先做重叠检测。',
    'Recheck overlaps for surfaces moved into the ball between physics updates.': '每步重查重叠，覆盖两次物理更新之间主动移入球体的障碍。',
    'A blocking wall takes precedence over a target if both contain the launch point.': '发射点同时处于墙体和靶子内时，优先由墙体挡住。',
    'Latch before applying damage/callbacks, including targets with several colliders.': '先锁定本次命中，再扣血及回调，避免多碰撞器或重入造成重复伤害。',
    'A reusable, finite-health target. Projectiles own their single-hit guarantee.': '可重置的有限生命靶子；每发只命中一次由投射物负责保证。',
    'True only when a live target actually received damage.': '仅当存活靶子实际承受伤害时返回 true。',
    'Bounded streaming JSONL; observes gameplay without changing its decisions.': '订阅投掷观测并流式写入有上限的 JSONL 日志，供动作诊断使用。',
    'Owns detached, short-lived visuals after their projectile has been destroyed.': '管理脱离投射物后的短时视觉资源，按独立寿命销毁或交由对象池回收。',
    'Visual-only animation. No colliders, input, damage, or material instances.': '驱动旋转、脉冲和环绕等纯视觉动画，玩法碰撞和伤害由效果执行器负责。',
    'Scene-owned, fixed pool for the configured projectile impact. Never grows during a hit.': '场景级固定容量命中特效池；提前创建实例，命中时只租用已有槽位。',
    'A stale lease must never return a slot that has already been rented again.': '租约携带代次，防止旧租约错误回收已经再次出租的槽位。',
    'A moving player or rig must not move a detached world-space impact.': '命中特效挂到世界空间根节点，避免继续跟随玩家或 XR Rig 移动。',
    'Receives local sherpa-onnx events. All Unity API calls and events run on the main thread.': '接收本机 sherpa-onnx 口令事件，校验传输状态后在 Unity 主线程发布。',
    'Second argument is estimated voice end mapped to Time.realtimeSinceStartupAsDouble.': '事件第二个参数是映射到 Unity 实时时钟的口令结束时间。',
    'Recent above-threshold energy on the selected live microphone, measured at the source.': '根据采集端上报的音量及时间，判断所选麦克风最近是否出现过阈值以上的声音。',
    'Source-clock mapping rejects buffered heartbeats that arrive after an editor stall.': '用源时钟映射识别积压心跳，避免编辑器卡顿后把旧数据当作实时输入。',
    'Selected device is not proof of audible input. No model probability is fabricated.': '展示设备、音量和心跳诊断；选中设备本身不能证明已经采到声音。',
    'JsonUtility populates these wire-format fields by reflection.': '这些字段由 JsonUtility 按网络数据包字段名反射填充，修改名称会影响协议。',
    "Copies the active catalog's labels. Unconfigured receivers reject every phrase.": '复制当前技能目录的口令白名单；完成配置前拒绝所有口令。',
    'Bounded polling prevents a noisy local process from blocking a VR frame.': '限制每帧轮询数量，防止本机进程大量发送数据拖慢 VR 帧。',
    'Refresh after long pauses; prefer the lowest-jitter sample within a window.': '长暂停后重新校准时钟，在观测窗口内优先采用抖动最小的样本。',
    'A restarted server must complete a new handshake before its events are accepted.': '服务端重启后必须重新握手，才能接收新会话的口令事件。',
    'Owns input lifecycle and connects matching to effects. Presentation lives in SkillTestView.': '连接骨骼、语音、匹配器与效果执行器，并管理输入生命周期；界面由 SkillTestView 展示。',
    'Serialized compatibility for existing scenes and editor tools; catalog/view now own these settings.': '兼容旧场景与编辑器工具的序列化字段；新配置由 catalog 和 view 持有。',
    'Optional session policy: installed before Start by the RPG adapter. Old scenes need no policy.': 'RPG 适配器可在 Start 前注入会话策略；训练场可直接使用效果执行器。',
    'Preferred extension seam; legacy ActivationOverride retains priority when assigned.': '玩法扩展优先使用此策略接口；已设置的旧式 ActivationOverride 回调仍具有更高优先级。',
    'An exclusive local UI session consumes even cancel phrases. Separate from ordinary navigation gates.': '本地界面独占输入时连取消口令也会拦截，其权限高于普通导航占用。',
    'Monotonic realtime input clock; tests may supply a deterministic clock. Never use paused game time.': '输入使用单调实时时钟，测试可注入固定时钟；暂停游戏时间不影响输入新鲜度判断。',
    'Voice processing and cancellation callbacks can advance the input clock.': '处理语音及取消回调可能推进输入时钟，',
    'Sample Tick time afterwards; an earlier cached value looks like a real rollback.': '因此随后重新取 Tick 时间，避免旧缓存时间被误判为时钟回退。',
    'Invalidate pending evidence without cancelling already committed effects.': '清除尚未提交的匹配证据，已生效效果由各自的生命周期继续管理。',
    'Read-only HUD plus explicit debug commands; does not decide whether a skill matches.': '展示技能状态及调试按钮，按钮将明确命令交回控制器处理。',
    'Training instructions and finite shield demonstrations. This is not a player health system.': '提供训练场说明和有限次数的护盾演示，正式玩家生命由 RPG 运行态管理。',
    'Diagnostics must never interrupt input or effects.': '诊断失败时继续处理输入与效果。',
}

type_summaries = {
    'SkillDefinition': '单个技能的共享配置，描述触发配方、效果资产、资源消耗与冷却；运行时实例另行保存状态。',
    'SkillCatalog': '一个可装备技能集合及其匹配规则，集中校验配方冲突并导出语音口令。',
    'SkillMatchSettings': '匹配器的缓存、时效、保持与消歧参数；启动时复制为本次运行的配置快照。',
    'SkillTriggerMode': '技能配方采用纯结印、纯口令或口令加单印三种触发方式。',
    'SkillEffectKind': '旧配置的效果枚举；没有 effectDefinition 资产时用于兼容护盾和手持火球。',
    'SkillEffects': '持有护盾、手持物、飞行投射物和扩展效果的运行态，负责生成、使用、命中与清理。',
    'ProjectileEffectDefinition': '手持投射物的共享配置，激活时由执行器复制为本发投射物的参数快照。',
    'ShieldEffectDefinition': '自身护盾配置，每次生效刷新容量，到期时间来自对应技能的持续时间。',
    'ProjectileReleaseMode': '选择旧版前推释放或握持投掷输入路径，供配置资产序列化。',
    'HandThrowSettings': '握持确认、松手确认、轨迹拟合与辅助发射参数，创建状态机时复制使用。',
    'ThrowGripSample': '同一次手部观测的握持特征及时间戳，用于检测数据过期、不同步和松手边沿。',
    'HandThrowEstimate': '轨迹拟合结果，包含世界速度、相对头部速度及有效性等诊断数据。',
    'AssistedThrowSettings': '后拉前送与伸手保持的阈值、权重和确认时长，控制辅助动作评分。',
    'AssistedThrowKind': '辅助判定输出的动作类型，None 表示本次观测未形成可释放候选。',
    'ThrowActionScore': '本次动作的分项评分和拒绝原因，供执行器、界面与日志共同读取。',
    'AssistedThrowCandidate': '辅助动作提出的释放候选，记录动作类型和方向，最终准入由执行器决定。',
    'HandThrowObservation': '单次投掷观测的可序列化诊断快照，用于还原准备、松手、拒绝及发射过程。',
}

methods = {
    'SkillDefinition.cs': {'public string ValidateDefinition(': '校验单个技能字段及效果配置，返回首个错误；返回 null 表示合法。'},
    'SkillCatalog.cs': {'public List<string> Validate(': '合并集合配置、取消口令和跨技能冲突检查，供编辑器与运行时使用。'},
    'SkillConfigurationValidator.cs': {'public static string[] GetErrors(': '按输入列表下标返回错误，冲突双方同时标记，避免依赖技能遍历顺序。'},
    'SkillMatchSettings.cs': {'public SkillMatchSettings Snapshot(': '复制标量设置，避免运行中修改共享资产改变本轮匹配规则。'},
    'SkillMatchEngine.cs': {
        'public SkillMatchEngine(': '冻结配方与时序配置，为合法配方建立单印跟踪器；无效配方禁用并给出原因。',
        'public void Tick(': '推进输入实时时钟并清理过期证据；时间回退时重置上下文。',
        'public void Observe(': '接收新骨骼帧及评分，累计连续合格保持区间，并尝试解决当前候选。',
        'public void Voice(': '校验完整口令及结束、到达时间，先查历史手印，再建立等待后续手印的窗口。',
        'public void Cancel(': '设置取消时间屏障并清除证据，使旧口令或旧手印无法在取消后补发。',
        'public void Reset(': '清除上下文并允许时钟重新起算，供启停、暂停恢复及时间回退使用。',
        'private void ResolveCandidates(': '多个不同技能同时满足时拒绝本轮输入；唯一技能才进入激活尝试。',
        'private void Attempt(': '先消费证据，再检查同帧重入和冷却；回调成功后才登记本地冷却。',
        'private void Consume(': '按物理动作编号消费全部关联证据，防止同一次手印被多个配方重复使用。',
        'private bool FindEvidence(': '在同一 Epoch 内查找完整保持区间落入组合窗口的证据，并选择时间最近的一份。',
        'private void ClearContext(': '推进 Epoch 并设置时间屏障，同时清空缓存和等待项，隔离清理前后的输入。',
    },
    'SkillTestController.cs': {
        'bool ActivateTraced(': '给本次激活分配日志编号，按旧回调、玩法策略、直接执行器的顺序选择执行入口。',
        'private void Start(': '完成配置校验、创建匹配器并连接激活回调和语音事件。',
        'private void LateUpdate(': '在骨骼采样之后统一处理语音队列、输入占用、取消姿势和新手印证据。',
        'public void ReceiveVoice(': '语音事件入口，校验输入所有权后立即处理或延迟到本帧 LateUpdate。',
        'bool VoiceGate(': '拒绝独占界面期间或输入时间屏障之前的口令，并记录具体拦截原因。',
        'public void Cancel(': '优先调用玩法注入的取消策略；训练场采用自身的待输入与效果取消流程。',
        'private void ResetInputLifecycle(': '清理排队口令和取消手势状态，使恢复后的输入从新的时间边界开始。',
    },
    'SkillEffects.cs': {
        'public bool TryActivate(': '统一效果激活入口，检查可用性与重入，失败时撤销本次新建的持握物和扩展生命周期。',
        'private void Update(': '推进手持物、护盾及扩展效果的生命周期，处理超时、追踪和释放输入。',
        'public void ClearAll(': '统一释放本执行器拥有的效果、临时视觉和材料，供禁用、销毁及暂停清理使用。',
    },
    'HandThrowState.cs': {
        'public Vector3 Process(': '以采样时间去重和校验轨迹，跟踪握持到松手的转换，确认后输出估计速度。',
        'HandThrowEstimate Fit(': '对松手前的轨迹窗口进行稳健拟合，生成释放速度及质量检查结果。',
    },
    'HandThrowIntentState.cs': {'public AssistedThrowCandidate Process(': '累计新鲜的身体相对运动，评估后拉前送与伸手保持，并提出至多一个候选。'},
    'SkillVoiceReceiver.cs': {'private void Process(': '检查会话握手、源时钟和递增序号，将通过白名单的口令映射为 Unity 输入时间。'},
    'VfxPoolManager.cs': {
        'public void Prewarm(': '按配置容量提前创建视觉实例，复用时恢复颜色、粒子和动画状态。',
        'public bool TryPlayImpact(': '尝试租用空槽并返回带代次的租约；容量用尽时跳过视觉，不改变命中结算。',
        'public void CancelOwned(': '按所属执行器回收效果，避免一个角色的清理误伤其他角色的视觉。',
    },
}

def insert_summary(lines, index, summary):
    start = index
    while start > 0 and lines[start - 1].lstrip().startswith('['):
        start -= 1
    if start > 0 and lines[start - 1].lstrip().startswith('///'):
        return False
    indent = re.match(r'\s*', lines[index])[0]
    lines.insert(start, indent + '/// <summary>' + summary + '</summary>')
    return True

counts = {}
for file in files:
    content = file.read_text(encoding='utf-8-sig')
    count = 0
    for original, translated in translations.items():
        if original in content:
            content = content.replace(original, translated)
            count += 1
    lines = content.splitlines()
    for index in range(len(lines) - 1, -1, -1):
        match = re.search(r'\b(?:class|struct|enum|interface)\s+(\w+)', lines[index])
        if match and match[1] in type_summaries:
            count += insert_summary(lines, index, type_summaries[match[1]])
    for anchor, summary in methods.get(file.name, {}).items():
        indexes = [i for i, line in enumerate(lines) if line.strip().startswith(anchor)]
        if len(indexes) != 1:
            print('REVIEW_ANCHOR', file.name, anchor, len(indexes))
            continue
        count += insert_summary(lines, indexes[0], summary)
    file.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    counts[str(file).replace('\\', '/')] = count

Path('Logs/skill-organization-20261104/comment-counts.json').write_text(json.dumps(counts, indent=2), encoding='utf-8')
print('中文注释处理', len(counts), '个文件，共', sum(counts.values()), '处')
