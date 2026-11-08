# 敌人导航、攻击与首次发现事件

本轮实现普通敌人场景巡逻、发现玩家后追击、独立动作随机攻击，以及召唤敌人直接追击、无目标待机。使用 Unity AI Navigation / NavMeshAgent，与 Meta、OpenXR、PICO 的手部输入无直接依赖。

当前已接逐招 AttackData 判定、玩家护盾/扣血、敌人生命/韧性、火球后脑弱点和 Seated 瘫痪；详见[战斗配置指南](ENEMY_COMBAT_ZH.md)。世界观中的蓄力危险区、特定秒数的发条瘫痪、远程变种和完整近战武器系统尚未实现。

## 场景接线

1. 将 [残机偶导航敌人预制体](../../Assets/SealCombat/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab) 放到地面。原 `CanJiOu_AutoLOD.prefab` 仍是美术资产，没有自动加 AI。
2. 地面使用 `NavMeshSurface` 烘焙导航网格，Agent Type 与敌人 NavMeshAgent 一致。敌人脚底应在导航面上。AI 不会通过修改 Transform 强行穿越不可达路径。
3. 在 `EnemyController` 的“玩家目标”填玩家代表点。单人 XR 可以先用头显 Camera Transform；水平距离用于攻击，垂直距离受“目标投影到导航面的最大距离”限制。后续多人由目标选择器调用 `SetTarget`，不在 AI 内搜索或绑定具体头显品牌。
4. 普通敌人关闭“召唤敌人”，在场景建空物体并按顺序填入“巡逻点”。到点停留后循环；空引用跳过，全部为空时待机。
5. 召唤敌人开启“召唤敌人”。有目标立即追击，不要求首次视野条件；没有目标、目标销毁或目标物体禁用时待机。巡逻点即使误填也不会使用。
6. 首次发现事件在“五、事件”中绑定。可拖对象、选择公开方法，例如旁白播放或 `EnemyController.SetSuspended(true)`。如果事件用于停下播放演出，演出结束需自行调用 `SetSuspended(false)`。

敌人根物体现在有两个自研组件。`EnemyController` 保留行为参数、玩家目标、巡逻点和事件；`EnemyObj` 持有 Animator 引用并执行导航、转向、视线和动画操作。同一个根物体只保留一套，`EnemyObj` 不自行更新 AI。旧 `controller.animator` 代码入口继续可用，旧场景的序列化引用在初始化时兼容迁移。读取属性不会自动添加组件；编辑器可通过“仅迁移 EnemyObj 组件”显式迁移标准 prefab。

此次没有保存或替换用户正在编辑的场景，需将上述新 prefab 接到希望使用的场景。

## 状态与事件契约

| 项目 | 行为 |
|---|---|
| 首次发现 | 先设置 `HasEverDetectedPlayer=true`，再触发 `onFirstPlayerDetected`，每轮生成一次 |
| 当前目标 | `HasTarget` 表示当前已发现有效目标，与首次发现记录独立 |
| 普通敌人发现 | 默认 8 米、前方 120 度、无遮挡；已追击时不受首次视野角限制 |
| 普通敌人丢失 | 距离超过 12 米或遮挡超过 2 秒后失去目标，恢复场景巡逻；未做最后已知位置搜索 |
| 召唤敌人 | 由生成器注入目标直接追击；目标有效时不因视野或距离主动放弃；不可达时停止并周期重试 |
| 开始攻击 | 条件满足时随机一次，编号范围 `[0, attackCount)`；0 默认挥砍，1 触发 `attack1` 转圈 |
| 接触事件 | 每招 AttackData 的生效窗口首次经过时发一次通知，不等同实际命中；Entity 按扇形/圆形及高度、遮挡独立判定并对每位玩家每招只扣一次血，勿重复绑定扣血 |
| 结束攻击 | 动画进度达到 0.98、取消或超时时复位；`onAttackFinished` 同时包含正常结束与取消，不用于无条件扣血 |
| 冷却 | 从结束/取消开始，默认 1.5 秒；即使设为 0 也等待退出旧攻击动画再选下一招 |
| 池化 | `OnEnable` / `InitializeSpawn` 重置本轮状态，重新允许首次事件；仅 `SetTarget` 不重置首次记录 |
| 暂停 | 调用 `SetSuspended(true)` 取消攻击、停止导航；恢复调用 `SetSuspended(false)` |
| Hit / Seated | 通过 `NotifyHit()` / `EnterSeated()` 打断攻击并停止导航；实际动画退出后恢复行动，默认 8 秒超时兜底 |
| Dead | 通过 `Die()` 停止导航、取消攻击并持续播放/停留死亡态，只有重新生成才解除；不自动扣血或销毁对象 |

所有时间随 `Time.deltaTime`，游戏暂停不会用现实时间跳过攻击。可以关闭“自动更新”，由外部统一调度 `Tick(deltaTime)`，不要同时调用两套更新。

## 生成器与扩展接口

```csharp
var enemy = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
enemy.InitializeSpawn(playerTarget, summoned: true);

enemy.SetTarget(otherPlayerTarget); // 换目标，不重置首次事件
enemy.SetTarget(null);              // 召唤敌人待机
enemy.SetSuspended(true);           // 演出、死亡或外部控制
enemy.CancelAttack();               // 受击等逻辑主动中止当前攻击
enemy.NotifyHit();                  // 播放 Hit，播完恢复行动
enemy.EnterSeated();                // 播放 Seated，播完恢复行动
enemy.Die();                        // 进入 Dead，直到重新生成
```

如果使用对象池，先放到有效 NavMesh，再激活并调用 `InitializeSpawn`。不同 Agent Type 的召唤点由生成器按其导航网格选择。首次事件回调允许禁用敌人、暂停或换目标，旧更新不会继续对已变化的生命周期发动攻击。

敌人伤害已走 `RpgDamageReceiver → LinearRpgController.ReceiveDamage → PlayerObj` 的护盾和去重入口。命中 ID 含敌人运行实例标识和递增攻击序号，池化重生不复用旧 ID。不要直接修改玩家 HP，也不要把开始攻击事件等同于命中。

上述三个反应方法为 `void`，可直接绑定 UnityEvent。程序若需要知道本次请求是否生效，可调用 `TryNotifyHit()`、`TryEnterSeated()`、`TryDie()`。优先级为死亡高于坐地、高于受击；同级请求不会重复排队，受击不能打断坐地，死亡拒绝其余反应。旧 Animator 若没有这三种状态与参数，基础巡逻/攻击仍可运行，反应接口返回失败；正式战斗 prefab 必须提供这些状态，缺少反应时削空/死亡将暂停行动。敌人 HP 归零已调用 Die，韧性归零调用 Seated 并在动画结束回韧。

职责拆分后，路径缓存、NavMeshPath、射线缓冲、Animator 参数名及根运动控制集中在 `EnemyObj`；目标选择、首次发现记录、攻击随机、冷却、反应优先级、暂停和回调重入保护仍由 `EnemyController` 维护。行为数值和事件不迁走，保留既有序列化路径和绑定。

## Animator 分工

- `isFindPlayer` 是当前目标状态；`angle` 是敌人 forward 到目标方向的 XZ 平面有符号角，左负右正。
- 新增 `motionState` 整数，0 待机、1 巡逻走路、2 追击跑步、3 近身转向。脚本决定行为，Animator 表现动画。
- 非攻击状态先检查 `isAttacking`，其他移动连线要求 `isAttacking=false`，消除 Run 抢占攻击。
- 攻击子状态机保留默认挥砍和 `attack1` 条件转圈，标签为 `EnemyAttack`。退出由脚本完成/取消标志驱动，不再等待 Idle 的 Exit Time。
- 新增状态使用用户的 `dead` Bool、`Hit` Trigger、`isSeated` Trigger。Any State 的死亡入口优先且不允许自重入；Hit/Seated 入口要求 `dead=false`，动画在 0.98 进度后退出到待机；死亡态保留无普通出口。
- 导航负责位移，脚本负责水平转向。`applyRootMotion=false`、`agent.updateRotation=false`。目前仍复用用户动作素材，脚底滑动和转身动作观感需现场调速验证。
- 当前资产只有两种独立攻击。增加第三种时须同时加动作、`attack2` Trigger、Attack Entry 条件和 EnemyObj 第三项 AttackData，并将总数设为 3；只改总数不会凭空生成动画。运行时基础验证能拦缺参数/数据，编辑器回归核对现有两招的实际入口。
- 攻击总数和 Animator Controller 请退出 Play 后修改，再开始新一轮；生成时会重新验证配置。移动速度、攻击距离、冷却等数值可在 Play 中调试。
- 关闭 Root Motion 后，转向/转圈素材是否仍有期望的视觉旋转需要 Game/头显验收；动画入口通过不等于视觉表现已验收。若素材主要通过根旋转表现转圈，应制作原地版本或隔离模型子节点旋转，不同时恢复导航根节点的动画位移。

“修复动画并生成敌人预制体”是旧两招结构的配置工具。检测到新增根状态时会拒绝重建；当前应使用“增量适配 Hit Seated Dead”，只调整这三个状态的入口和恢复出口，保留移动/攻击结构和所有剪辑。仅迁移组件时使用“仅迁移 EnemyObj 组件”。原始 controller 本机备份位于 `Logs/enemy-fsm-20261104/before/`；本轮拆分前和用户新增状态版本在 `Logs/enemy-obj-refactor-20261104/before/`。

## 调试与验证

Inspector 第六组显示行为、首次发现、当前目标、角度、攻击编号与拦截说明。未烘焙、目标不可达、未填巡逻点和动画错误会停下并给出原因。

编辑器菜单 `Tools → NarutoVR → 敌人 → 验证导航与攻击状态`，或 `Tools/Request-RpgCheck.ps1 -Action enemy-check`，临时指定 `EnemyNavigationTest.unity` 空场景进入 Play，再创建隔离场景和临时 NavMesh，结束后退出并恢复原 Play 启动场景设置。不保存用户当前场景。最终结果在 `Logs/enemy-fsm-20261104/check.txt`；`Logs/linear-rpg/result.json` 仅确认检查已启动，不是通过结论。以最终报告时间和 PASSED/FAILED 为准。此检查不代表头显、真机帧率或完整战斗验收。
