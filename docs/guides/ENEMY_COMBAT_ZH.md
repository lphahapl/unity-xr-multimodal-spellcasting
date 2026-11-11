# 敌人攻击、削韧、弱点与玩家实体

当前入口是 `CanJiOu_Enemy.prefab`，搭配 [导航与动画说明](ENEMY_NAVIGATION_ZH.md)。本轮参考本机 `D:/unity_training/Practice/Assets/Scripts/Data/WeaponData.cs`、`Logic/WeaponObj.cs`、`Logic/EnemyObj.cs`、`Logic/EnemyAI.cs`、`Logic/EventCenter.cs` 的数据和事件分工；没有修改 Practice，也没有整套搬入它的武器、任务或 Root Motion 系统。

## Inspector 配置位置

P1 的残机偶从 [基础预制体](../../Assets/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab) 继承招式。在 Project 中打开它，选根物体，展开 **EnemyObj → 每招攻击数据 → Element 0（挥砍）或 Element 1（转圈）→ 单次伤害**。房间使用的 `CanJiOu_Room` 变体继承这两个值。

| 配置位置 | 调整内容 | P1 当前值 |
|---|---|---|
| 敌人根物体 / EnemyController | 发动攻击距离、朝向误差、攻击冷却、独立动作总数 | 1.6 米、15°、1.5 秒、2 招 |
| 敌人根物体 / EnemyObj / 每招攻击数据 / 0 | 挥砍伤害、扇形半径、总角、生效窗口 | 8、2.1 米、100°、动画 0.55–0.70 |
| 同上 / 1 | 转圈伤害、圆形半径、生效窗口 | 6、2.3 米、动画 0.55–0.80 |
| EnemyObj / 战斗数值资产 | 生命、韧性、恢复参数 | [CanJiOu_Combat.asset](../../Assets/ScriptableObjects/敌人战斗配置/CanJiOu_Combat.asset) |
| 同一 SO | 最大生命 / 韧性、恢复等待 / 速率、瘫痪后恢复比例 | 50 / 100、4 秒 / 每秒 15、100% |
| 头骨下的“后脑弱点” / EnemyHitZone | 削韧倍率、生命伤害倍率、额外固定伤害 | 3、2、0 |
| [火球术投射物](../../Assets/ScriptableObjects/投射物效果配置/火球术投射物.asset) | 命中伤害、基础削韧值 | 25、20 |
| RPG 会话与玩家 / LinearRpgController / 玩家基础定义 → [默认玩家](../../Assets/ScriptableObjects/玩家定义配置/默认玩家.asset) | 生命上限、能量上限、每秒恢复能量、恢复等待秒数 | 100、100、5、2 |
| 敌人根物体 / EnemyPoiseBar | 头顶名牌的位置、条宽、血条高度、生命与韧性颜色 | 血条宽 55 厘米、高 4.5 厘米。下方韧性条同宽，高约 1.5 厘米 |
| “近距探测范围” / SphereCollider | 近距兜底半径与中心 | 半径 2.5 米，中心上移 1 米 |
| 同物体 / EnemyDetectionSensor | 近距发现 | 玩家碰撞体进入即发现，背面和墙体均不阻止近身警觉 |
| EnemyController / 视野 | 巡逻发现距离、水平总角 | 基础预制体 8 米，[P1 房间变体](../../Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab) 15 米。两者均为 120°（正前方左右各 60°），实体遮挡会阻断视线 |
| EnemyController | 受攻击后强制锁定秒数 | 5 秒 |

攻击窗口使用动画归一化进度，`0.5` 表示动画播放一半，不是 0.5 秒。时间随动画速度变化；有效窗口结束不得超过现有攻击退出点 0.98。每次攻击开始冻结本招配置，因此 Play 中调整攻击数据从下一招生效。生命/韧性 SO 在生成或 `ResetForSpawn()` 时建立独立快照，调参后需重新生成。

头顶名牌从上到下为敌人名称、朱砂色生命条、金色细韧性条。名字取自[敌人定义的「显示名称」](../../Assets/ScriptableObjects/敌人定义配置/残机偶.asset)。直接摆放的预制体通过 EnemyObj 的「敌人身份」引用同一份定义。残机偶的血条中心位于头部挂点上方 29 厘米，站立和坐地时都跟随头部。画布整体朝向玩家眼位，绕到侧后方仍能正读。韧性清空时露出紫色底条，恢复时重新填充。敌人死亡后隐藏名牌，再次出生时按新状态显示。

[受击后的双条](../visuals/20261108_enemy_nameplate/残机偶_受击.png) · [破韧坐地后的名牌](../visuals/20261108_enemy_nameplate/残机偶_破韧.png)

`attackCount` 是独立动作总数，必须同时存在相应 Animator 动作入口和 `AttackData`。数量不足或数值无效会拒绝行动/攻击并显示原因。老 `contactNormalizedTime` 只用于首次迁移默认两招，隐藏后不再控制当前生效时间。

## 判定、扣血和瘫痪

1. 攻击动画进入生效窗口后，每次更新按 XZ 扇形/圆形和高度范围判断玩家代表点。中心偏移按敌人水平朝向旋转，米制范围不跟随模型缩放；每招可调中心与上下高度。
2. 同一攻击的唯一 ID 和角色集合保证每位玩家只结算一次；头、手多个 Collider 不会多扣。低帧率一次跨过整段窗口会补判一次当前位置，不回放历史路径。窗外不再命中新进入的人。
3. 命中走 `RpgDamageReceiver → LinearRpgController.ReceiveDamage → PlayerObj`，先去重，再吸收护盾，再扣剩余生命。反馈、倒下后的技能取消和检查点逻辑仍由关卡控制器处理。
4. 玩家火球通过已有球形扫掠结算敌人生命/韧性；只在首个有效碰撞处结算一次。身体/头部是普通 Collider，后脑是带 `EnemyHitZone` 的 Trigger；其他无关 Trigger 不挡火球，实体墙仍先挡住。
5. 削韧归零的本次命中立即触发 `Seated`，取消攻击并停导航；瘫痪期间保持零韧性，不反复重播、不自然恢复。动画结束后按 SO 比例回韧。死亡优先，不能靠回韧复活。
6. 非瘫痪状态下，每次有效削韧重置恢复等待，之后按速率渐回；不会固定周期突然回满。生命伤害为零的削韧技能也可走同一接口。

弱点生命伤害按 `本发伤害 × 生命倍率 + 额外固定伤害` 结算，弱点削韧按 `基础削韧 × 削韧倍率` 结算。对满血普通残机偶，未升级火球命中身体两次击杀，直接命中后脑弱点一次击杀。火球每级升级加成基础伤害的 10%。弱点区域须属于受击敌人。当前受击接口将单次 HP 伤害限制为 10000、削韧为 100000。

普通火球命中后，观察弹着火花、命中音源和头顶双条。正式首章的满血残机偶生命从 50 降至 25，韧性从 100 降至 80，当前攻击可以继续。一次命中打空韧性会切到坐地反应并中止攻击。致死优先进入 `Dead`，击败当帧生效，倒地动作播完后清理。[生成与回收](ENEMY_SPAWNING_ZH.md)

### 玩家挨打时看哪里

存活玩家损失气血时，双眼视野的侧缘和角部浮起朱砂色，约 0.58 秒淡出，中央准星保持清楚。密集命中会合并，最多每 0.72 秒开始一次提示。护盾承担全部伤害时，只显示原有护盾反馈。暂停、倒下或恢复检查点都会清除残留的视口颜色。

在玩家的「LinearRpgController → 玩家基础定义」打开所用资产，调节「受击反馈 → 视口边缘提示强度」，范围为 0 到 1。设为 0 可关闭视口提示。首章与 P1 试炼使用[默认玩家](../../Assets/ScriptableObjects/玩家定义配置/默认玩家.asset)。

后脑挂在 `mixamorig:Head` 子节点，随动画移动；Collider 没有 Renderer，无需透明材质。选中区域开启 Scene Gizmos 可看紫色球。初始大小与位置需结合实际动作和 VR 瞄准手感微调；不要用覆盖整颗头的大弱点球，否则正面也会命中它。

## 玩家实体与受击

在 **Create → NarutoVR → 线性RPG → 玩家定义** 创建 `RpgPlayerDefinition`，在场景会话的“玩家基础定义”指定它。场景中每个 `PlayerObj` 创建自己的 `PlayerModel`。装备、腕匣和 HUD 可通过只读接口取得数据。

敌人通过 `RpgDamageReceiver` 将带唯一命中标识的伤害交给当前玩家。通过暂停与存活检查后，同一份生命状态先做命中去重，再吸收护盾、扣除剩余伤害。恢复检查点保留本会话命中记录。玩家实体报告实际吸收和生命损失，遮罩与触觉据此更新。

## 近距与受击锁定

普通残机偶有三种发现玩家的途径。Inspector 的“最近发现来源”可用来区分它们。

- **近身**。身体 Collider 和半径 2.5 米的探测用 SphereCollider 分开。可识别的玩家碰撞体进入 Trigger 时立即发现，即使玩家在背后或隔着薄墙。没有身体 Collider 的 XR 玩家也可以凭已注册的玩家代表点进入球内被发现。同轮生成中，多处碰撞体接触只触发一次首次发现事件。
- **巡逻视野**。普通残机偶待机或巡逻时，可以自行扫描已注册的玩家。P1 房间的视野最远 15 米，水平总角 120°，需要从敌人眼部到玩家代表点视线畅通。巡逻点留空只影响巡逻走位，仍可发现玩家。
- **受击**。有效的非致命玩家攻击携带攻击者代表点。残机偶在下一个 AI Tick 锁定该玩家，受击或坐地动画继续播放。默认强制锁定 5 秒，锁定期满后重新按距离和遮挡判断是否丢失。

`EnemyObj.ReceiveHit(hitId, damage, poiseDamage, zone, attacker)` 和 `EnemyHitZone.TryApplyHit(...)` 接收攻击者代表点。火球传入施法玩家的头显代表点，后续技能也应传入自己的施法者。敌人用头部检查视线，用玩家脚下位置计算追击路径与近战楼层高差。挥砍和转圈仍分别检查攻击范围、朝向、冷却和遮挡。

召唤敌人有指定目标就直接追，没有目标时待机。带攻击者信息的有效命中同样可以使它取得目标。

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
