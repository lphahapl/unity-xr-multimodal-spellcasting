# 敌人攻击、削韧、弱点与玩家实体

当前入口是 `CanJiOu_Enemy.prefab`，搭配 [导航与动画说明](ENEMY_NAVIGATION_ZH.md)。本轮参考本机 `D:/unity_training/Practice/Assets/Scripts/Data/WeaponData.cs`、`Logic/WeaponObj.cs`、`Logic/EnemyObj.cs`、`Logic/EnemyAI.cs`、`Logic/EventCenter.cs` 的数据和事件分工；没有修改 Practice，也没有整套搬入它的武器、任务或 Root Motion 系统。

## Inspector 配置位置

| 配置位置 | 调整内容 | 默认值 |
|---|---|---|
| 敌人根物体 / EnemyController | 发动攻击距离、朝向误差、攻击冷却、独立动作总数 | 1.6 米、15°、1.5 秒、2 招 |
| 敌人根物体 / EnemyObj / 每招攻击数据 / 0 | 挥砍伤害、扇形半径、总角、生效窗口 | 35、2.1 米、100°、动画 0.55–0.70 |
| 同上 / 1 | 转圈伤害、圆形半径、生效窗口 | 25、2.3 米、动画 0.55–0.80 |
| EnemyObj / 战斗数值资产 | 生命、韧性、恢复参数 | [CanJiOu_Combat.asset](../../Assets/SealCombat/Configuration/Enemies/CanJiOu_Combat.asset) |
| 同一 SO | 最大生命 / 韧性、恢复等待 / 速率、瘫痪后恢复比例 | 150 / 100、4 秒 / 每秒 15、100% |
| 头骨下的“后脑弱点” / EnemyHitZone | 削韧倍率、生命伤害倍率、额外固定伤害 | 3、1、0 |
| 技能 ProjectileEffectDefinition | 基础伤害、基础削韧 | 原技能伤害保留，削韧默认 20 |
| 敌人根物体 / EnemyPoiseBar | 韧性条位置、宽高、颜色 | 根物体上方 2.15 米、橙色；瘫痪底条紫色 |
| “近距探测范围” / SphereCollider | 近距兜底半径与中心 | 半径 2.5 米，中心上移 1 米 |
| 同物体 / EnemyDetectionSensor | 近距是否忽略墙体 | 默认不忽略 |
| EnemyController | 受攻击后强制锁定秒数 | 5 秒 |

攻击窗口使用动画归一化进度，`0.5` 表示动画播放一半，不是 0.5 秒。时间随动画速度变化；有效窗口结束不得超过现有攻击退出点 0.98。每次攻击开始冻结本招配置，因此 Play 中调整攻击数据从下一招生效。生命/韧性 SO 在生成或 `ResetForSpawn()` 时建立独立快照，调参后需重新生成。

`attackCount` 是独立动作总数，必须同时存在相应 Animator 动作入口和 `AttackData`。数量不足或数值无效会拒绝行动/攻击并显示原因。老 `contactNormalizedTime` 只用于首次迁移默认两招，隐藏后不再控制当前生效时间。

## 判定、扣血和瘫痪

1. 攻击动画进入生效窗口后，每次更新按 XZ 扇形/圆形和高度范围判断玩家代表点。中心偏移按敌人水平朝向旋转，米制范围不跟随模型缩放；每招可调中心与上下高度。
2. 同一攻击的唯一 ID 和角色集合保证每位玩家只结算一次；头、手多个 Collider 不会多扣。低帧率一次跨过整段窗口会补判一次当前位置，不回放历史路径。窗外不再命中新进入的人。
3. 命中走 `RpgDamageReceiver → LinearRpgController.ReceiveDamage → PlayerObj`，先去重，再吸收护盾，再扣剩余生命。反馈、倒下后的技能取消和检查点逻辑仍由关卡控制器处理。
4. 玩家火球通过已有球形扫掠结算敌人生命/韧性；只在首个有效碰撞处结算一次。身体/头部是普通 Collider，后脑是带 `EnemyHitZone` 的 Trigger；其他无关 Trigger 不挡火球，实体墙仍先挡住。
5. 削韧归零的本次命中立即触发 `Seated`，取消攻击并停导航；瘫痪期间保持零韧性，不反复重播、不自然恢复。动画结束后按 SO 比例回韧。死亡优先，不能靠回韧复活。
6. 非瘫痪状态下，每次有效削韧重置恢复等待，之后按速率渐回；不会固定周期突然回满。生命伤害为零的削韧技能也可走同一接口。

弱点生命伤害 = `基础伤害 × 生命倍率 + 额外固定伤害`；弱点削韧 = `基础削韧 × 削韧倍率`。默认弱点不额外增加 HP 伤害。弱点区域必须属于本敌人；传别人的区域不会获得加成。当前受击接口将单次 HP 伤害限制为 10000、削韧为 100000。

后脑挂在 `mixamorig:Head` 子节点，随动画移动；Collider 没有 Renderer，无需透明材质。选中区域开启 Scene Gizmos 可看紫色球。初始大小与位置需结合实际动作和 VR 瞄准手感微调；不要用覆盖整颗头的大弱点球，否则正面也会命中它。

## PlayerObj 和 PlayerSO

- `PlayerSO` 可从 `Create → NarutoVR → 线性RPG → 玩家配置 PlayerSO` 创建，继承已有 `RpgPlayerDefinition` 字段：HP/MP 上限、MP 恢复速率、恢复等待。
- 在 `LinearRpgController` 的“玩家基础定义”绑定配置。原 `RpgPlayerDefinition` 资产继续有效，不强制重命名或迁移旧场景。
- `PlayerObj` 持有唯一 `RpgPlayerState` 及会话命中账本；不同玩家使用同一 SO 也不会共享当前血量、MP 或 CD。关卡的 `Player` 属性读取同一状态。
- 外部战斗入口继续用 `LinearRpgController.ReceiveDamage(hitId, amount)` 或 `RpgDamageReceiver`，保留暂停、存活准入和死亡反馈。`PlayerObj.TryReceiveDamage` 是实体底层接口，不能绕过关卡准入调用来处理正常战斗。
- 旧场景运行时自动补齐 PlayerObj 与 RpgDamageReceiver；未批量保存业务场景。恢复检查点不会清命中账本，防止旧攻击重放。

## 近距与受击锁定

普通敌人远距离仍由指定目标、8 米/120°视野和视线检测发现。新增近距 Trigger 与身体 Collider 分离：近距离 360°兜底，可从已注册角色自动选目标，XR 玩家不必有实体 Collider。使用角色代表点（默认头显位置）判断，不会因为一只手离开 Trigger 就丢掉整个人。离开近距后仍遵守原距离/遮挡宽限，不是永久透视追踪。EnemyController 的“最近发现来源”显示召唤目标、视野追踪、近距兜底或受击锁定。

`EnemyObj.ReceiveHit(hitId, damage, poiseDamage, zone, attacker)` 和 `EnemyHitZone.TryApplyHit(...)` 支持传攻击者代表点。已接受且未致死的玩家攻击排队到安全 Tick 边界锁定攻击者，暂时绕过发现视野/距离/遮挡；发动攻击仍要求攻击距离、朝向、冷却和视线。锁定结束恢复正常丢失规则。火球已传入头显代表点；未来近战或远程玩家适配也要传自己的攻击者，不能用全局主摄像机代替。

召唤敌人依旧有目标就直接追，无目标待机，不参与近距自动选人；受到有效玩家攻击时可以通过带攻击者的受击接口取得目标。远距离自动搜索多个玩家、仇恨值、联机权威判定尚未实现。

## 事件与扩展

原 Inspector `onFirstPlayerDetected`、`onAttackStarted`、`onAttackContact`、`onAttackFinished` 保留，桥接到 `EnemyEvents`。`onAttackContact` 现在表示进入攻击生效窗口，不表示一定打中，请勿再绑定额外扣血。

```csharp
using System;
using NarutoVR.SealCombat.Events;

IDisposable subscription;
void OnEnable() => subscription = EventCenter.Subscribe(EnemyEvents.PoiseBroken,
    notice => ShowBrokenFeedback(notice.Enemy), owner: this);
void OnDisable() => subscription?.Dispose();
```

事件键集中使用 `static readonly EventKey<T>`；同名新建 key 是不同事件。支持首次发现、攻击开始/窗口/结束、玩家伤害已接受、敌人生命韧性改变/韧性归零/死亡。通知只观察事实，不负责再次执行伤害；死亡/瘫痪先提交状态机再发通知。回调导致重生、销毁或追加伤害后，不会继续用旧流程暂停/回收新生命。

同一 key/delegate 重复订阅共享一个 token；任一 Dispose 即取消唯一监听。生命周期仍须主动退订，owner 仅兜底过滤被销毁对象。监听异常隔离，订阅变更/重入按快照派发。仅主线程使用，联网线程必须先回到主线程。

## 与 Practice 的取舍

- 保留“配置数据与运行状态分开、区域判定、类型化事件键”的思路。
- 不复制“先广播受击再扣韧性”的顺序，避免本次刚削空却没触发瘫痪。
- 不复制从生成时持续倒计时、周期性回满韧性的代码；改为最后一次有效削韧后的延迟恢复。
- 不复制每次判定清 HashSet 的方式；本项目要求整招窗口内每位玩家只受一次伤害。
- Practice 当前 EnemyAI 只有一个探测 Box Trigger，另一个 Collider 是身体用途；不将其误称为双探测范围。不复制任意 Collider Exit 就清玩家的逻辑。
- 不直接照搬它的 Root Motion 导航、全局任务依赖和 `Attacker.GetComponent<PlayerObj>()` 假设；XR 身体、头部、系统对象可能分属不同层级。

## 验证入口与边界

`Tools → NarutoVR → 敌人 → 验证导航与攻击状态`，或 `Tools/Request-RpgCheck.ps1 -Action enemy-check`。最终结果看 `Logs/enemy-fsm-20261104/check.txt`，请求成功不代表测试通过。包括实际 Animator、导航、攻击窗口、护盾、玩家实体、削韧恢复、弱点球形扫掠、事件重入和探测检查。测试不读写正式存档，不代表头显手感、Android 构建或完整关卡验收。

配置/迁移标准敌人使用 `enemy-migrate`，只改标准 prefab/必要配置资产，保留已有事件和动画引用；重复运行不覆盖已调好的弱点或探测器。创建备份与本轮报告位于 `Logs/enemy-attack-20261104/`。原始美术 prefab 和正在编辑的业务场景不会自动替换。
