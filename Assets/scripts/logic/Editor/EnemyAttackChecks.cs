#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>仅供隔离 Play 检查的角色接收器；每个角色一个接收器，碰撞体数量不影响命中次数。</summary>
    public sealed class EnemyAttackCheckReceiver : CombatDamageReceiver
    {
        public bool allowed = true;
        public readonly List<EnemyAttackHit> hits = new();
        public Action<EnemyAttackHit> afterHit;
        public override Vector3 HitPosition => transform.position;
        public override bool CanReceiveDamage => allowed && isActiveAndEnabled;
        public override bool TryReceiveDamage(EnemyAttackHit hit)
        {
            if (!CanReceiveDamage) return false;
            hits.Add(hit); afterHit?.Invoke(hit); return true;
        }
    }

    /// <summary>几何、动画窗口、命中去重、事件重入和真实 RPG 护盾管线；不写存档或清全局事件。</summary>
    public static class EnemyAttackChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("敌人攻击检查须在隔离 Play 场景运行。");
            int count = 0;
            void Check(bool ok, string reason) { if (!ok) throw new Exception("敌人攻击检查：" + reason); ++count; }
            using var fixture = new AttackFixture();
            var owner = fixture.Owner;
            var data = new AttackData { radius = 2, angle = 90, halfHeight = 1, centerOffset = new Vector3(0, 1, 0),
                activeStart = .55f, activeEnd = .7f, damage = 35 };
            var center = data.Center(owner.transform);
            Check(data.Validate(out _), "合法攻击配置");
            var invalid = data.Snapshot(); invalid.activeEnd = .4f;
            Check(!invalid.Validate(out _), "拒绝逆序时间窗");
            invalid = data.Snapshot(); invalid.radius = float.NaN;
            Check(!invalid.Validate(out _), "拒绝非有限范围");
            Check(data.Contains(center, Vector3.forward, center + Vector3.forward * 2), "扇形前方半径边界包含");
            Check(!data.Contains(center, Vector3.forward, center - Vector3.forward), "扇形背后排除");
            Check(!data.Contains(center, Vector3.forward, center + Vector3.right), "扇形侧方排除");
            Vector3 edge = new Vector3(1, 0, 1).normalized;
            Check(data.Contains(center, Vector3.forward, center + edge), "扇形半角边界包含");
            Vector3 outsideAngle = Quaternion.Euler(0, 46, 0) * Vector3.forward;
            Check(!data.Contains(center, Vector3.forward, center + outsideAngle), "超过扇形半角排除");
            Check(data.Contains(center, Vector3.forward, center + Vector3.forward + Vector3.up), "高度上边界包含");
            Check(!data.Contains(center, Vector3.forward, center + Vector3.forward + Vector3.up * 1.02f), "超过高度排除");
            Check(!data.Contains(center, Vector3.forward, center + Vector3.forward * 2.02f), "超过半径排除");
            var circle = data.Snapshot(); circle.shape = AttackShape.Circle;
            Check(circle.Contains(center, Vector3.forward, center - Vector3.forward * 2) &&
                circle.Contains(center, Vector3.forward, center + Vector3.right * 2), "圆形包含后方与侧方");
            Check(!circle.Contains(center, Vector3.forward, center + Vector3.up * 1.02f), "圆形仍有高度限制");
            owner.transform.rotation = Quaternion.Euler(0, 90, 0); owner.transform.localScale = new Vector3(9, 3, 7);
            var offset = data.Snapshot(); offset.centerOffset = new Vector3(1, 2, 3);
            Check(Vector3.Distance(offset.Center(owner.transform), owner.transform.position + new Vector3(3, 2, -1)) < .003f,
                "局部米偏移随水平朝向旋转，不随模型缩放");
            Check(offset.Contains(offset.Center(owner.transform), owner.transform.forward, offset.Center(owner.transform) + Vector3.right * 2) &&
                !offset.Contains(offset.Center(owner.transform), owner.transform.forward, offset.Center(owner.transform) + Vector3.right * 2.02f),
                "缩放后的攻击半径仍按世界米判断");
            owner.transform.rotation = Quaternion.identity; owner.transform.localScale = Vector3.one;

            CheckWindowAndSnapshot(fixture, data, Check);
            fixture.ClearReceivers();
            CheckEventsAndCancellation(fixture, data, Check);
            fixture.ClearReceivers();
            CheckOcclusionAndSelf(fixture, data, Check);
            fixture.ClearReceivers();
            CheckRpgPipeline(fixture, data, Check);
            return $"敌人攻击范围/窗口/去重/事件/RPG护盾 {count} 项通过（隔离 Play 合成检查）。";
        }

        static void CheckWindowAndSnapshot(AttackFixture f, AttackData data, Action<bool, string> check)
        {
            var execution = new EnemyAttackExecution(f.Owner);
            var first = f.Receiver(Vector3.forward);
            first.gameObject.AddComponent<BoxCollider>(); first.gameObject.AddComponent<SphereCollider>();
            execution.Begin(0, data);
            check(!execution.Evaluate(.54f, false, 1.4f, ~0) && first.hits.Count == 0, "生效窗之前无伤害");
            check(execution.Evaluate(.55f, false, 1.4f, ~0) && first.hits.Count == 1, "起点首次开启并命中一次");
            check(!execution.Evaluate(.6f, false, 1.4f, ~0) && first.hits.Count == 1, "同次攻击多帧与多个碰撞体均不重复扣血");
            var atEnd = f.Receiver(new Vector3(.2f, 0, 1));
            execution.Evaluate(.7f, false, 1.4f, ~0);
            check(atEnd.hits.Count == 1, "生效窗终点仍可命中新进入角色");
            var late = f.Receiver(new Vector3(-.2f, 0, 1));
            execution.Evaluate(.8f, false, 1.4f, ~0);
            check(late.hits.Count == 0, "已经处理终点后不得在窗外伤害新进入角色");
            execution.Evaluate(.6f, false, 1.4f, ~0);
            check(late.hits.Count == 0, "动画进度回退不重新打开已结束窗口");
            string previousId = execution.Id; execution.Begin(1, data);
            execution.Evaluate(.4f, false, 1.4f, ~0); execution.Evaluate(.9f, false, 1.4f, ~0);
            check(execution.Id != previousId && first.hits.Count == 2 && late.hits.Count == 1, "低帧率跨过整个窗口仍命中一次，新攻击允许再伤害");
            execution.Evaluate(1, false, 1.4f, ~0);
            check(first.hits.Count == 2 && late.hits.Count == 1, "跨窗补判之后不继续命中");
            execution.Begin(0, data); execution.End(); execution.Evaluate(.6f, false, 1.4f, ~0);
            check(first.hits.Count == 2, "End 在窗口前中断，不能再伤害");
            var mutable = data.Snapshot(); execution.Begin(0, mutable);
            mutable.damage = 999; mutable.radius = .01f; mutable.activeStart = .9f;
            execution.Evaluate(.6f, false, 1.4f, ~0);
            check(first.hits.Count == 3 && Mathf.Approximately(first.hits[^1].Damage, data.damage), "Begin 冻结伤害、范围与窗口，修改原配置不影响本次攻击");
            execution.Begin(0, data); int before = first.hits.Count;
            execution.Evaluate(float.NaN, false, 1.4f, ~0); execution.Evaluate(-1, false, 1.4f, ~0);
            check(first.hits.Count == before, "非有限或负进度不产生伤害");
            execution.End();
        }

        static void CheckEventsAndCancellation(AttackFixture f, AttackData data, Action<bool, string> check)
        {
            var one = f.Receiver(new Vector3(-.1f, 0, 1));
            var two = f.Receiver(new Vector3(.1f, 0, 1));
            var execution = new EnemyAttackExecution(f.Owner);
            int notifications = 0;
            using (EventCenter.Subscribe(EnemyEvents.DamageAccepted, hit =>
            {
                if (hit.Source != f.Owner) return;
                ++notifications; execution.End(); execution.Evaluate(.6f, false, 1.4f, ~0);
            }))
            {
                execution.Begin(0, data); execution.Evaluate(.6f, false, 1.4f, ~0);
                check(notifications == 1 && one.hits.Count + two.hits.Count == 1, "命中通知中取消和重入，旧攻击不能继续伤害剩余角色");
            }
            one.hits.Clear(); two.hits.Clear(); notifications = 0;
            string replacementId = null;
            using (EventCenter.Subscribe(EnemyEvents.DamageAccepted, hit =>
            {
                if (hit.Source != f.Owner || replacementId != null) return;
                ++notifications; execution.Begin(1, data); replacementId = execution.Id;
                execution.Evaluate(.6f, false, 1.4f, ~0);
            }))
            {
                execution.Begin(0, data); execution.Evaluate(.6f, false, 1.4f, ~0);
                check(notifications == 1 && one.hits.Count + two.hits.Count == 1 && execution.Active,
                    "命中通知中重建攻击时，旧快照立即停止且不能重入新攻击");
                execution.Evaluate(.6f, false, 1.4f, ~0);
                check(one.hits.Count + two.hits.Count == 3 && one.hits[^1].AttackId == replacementId && two.hits[^1].AttackId == replacementId,
                    "下一次更新独立执行新攻击，ID与去重不串代");
            }
            execution.End();
        }

        static void CheckOcclusionAndSelf(AttackFixture f, AttackData data, Action<bool, string> check)
        {
            var victim = f.Receiver(Vector3.forward * 1.5f);
            var self = f.Receiver(Vector3.forward);
            self.transform.SetParent(f.Owner.transform, true);
            var wall = f.Object("攻击检查_遮挡");
            wall.transform.position = f.Origin + new Vector3(0, 1, .7f);
            wall.transform.localScale = new Vector3(4, 4, .1f); wall.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            var execution = new EnemyAttackExecution(f.Owner); execution.Begin(0, data);
            execution.Evaluate(.6f, true, 1.4f, ~0);
            check(victim.hits.Count == 0 && self.hits.Count == 0, "墙体遮挡阻止命中，自身层级不受自身攻击");
            wall.SetActive(false); Physics.SyncTransforms();
            execution.Evaluate(.65f, true, 1.4f, ~0);
            check(victim.hits.Count == 1 && self.hits.Count == 0, "窗口内解除遮挡可命中，仍排除自身角色");
            execution.End();
        }

        static void CheckRpgPipeline(AttackFixture f, AttackData data, Action<bool, string> check)
        {
            using var rpg = new RpgFixture(f.Origin + Vector3.forward);
            check(rpg.Receiver.CanReceiveDamage && rpg.Controller.Player.Health == 100, "真实 RPG fixture 完成初始化且可受伤");
            check(rpg.Effects.TryApplyShield(rpg.Shield, rpg.Buff) && Mathf.Approximately(rpg.Effects.ShieldPoints, 50), "真实护盾容量50激活");
            int notices = 0; EnemyAttackHit accepted = default;
            using var token = EventCenter.Subscribe(EnemyEvents.DamageAccepted, hit =>
            {
                if (hit.Source == f.Owner && hit.Receiver == rpg.Receiver) { ++notices; accepted = hit; }
            });
            var execution = new EnemyAttackExecution(f.Owner);
            execution.Begin(0, data); execution.Evaluate(.6f, false, 1.4f, ~0);
            check(notices == 1 && Mathf.Approximately(rpg.Effects.ShieldPoints, 15) && Mathf.Approximately(rpg.Controller.Player.Health, 100),
                "敌人攻击经 RpgDamageReceiver 与 ReceiveDamage 先扣护盾");
            check(!rpg.Receiver.TryReceiveDamage(accepted) && Mathf.Approximately(rpg.Effects.ShieldPoints, 15),
                "直接重放同一攻击ID被 RPG 去重，护盾不会再次扣除");
            execution.Begin(1, data); execution.Evaluate(.6f, false, 1.4f, ~0);
            check(notices == 2 && Mathf.Approximately(rpg.Effects.ShieldPoints, 0) && Mathf.Approximately(rpg.Controller.Player.Health, 80),
                "新攻击ID消耗剩余护盾，只有溢出的20点扣生命");
            execution.Evaluate(.65f, false, 1.4f, ~0);
            check(notices == 2 && Mathf.Approximately(rpg.Controller.Player.Health, 80), "同轮后续帧不重发事实事件或扣生命");
            execution.End();
            var paused = typeof(LinearRpgController).GetField("paused", BindingFlags.Instance | BindingFlags.NonPublic);
            paused.SetValue(rpg.Controller, true);
            check(!rpg.Receiver.CanReceiveDamage && !rpg.Receiver.TryReceiveDamage(new EnemyAttackHit(f.Owner, rpg.Receiver, "paused-probe", 0, 10, rpg.Receiver.HitPosition)),
                "暂停玩家仍由原 RPG 准入拒绝敌人伤害");
            paused.SetValue(rpg.Controller, false);
            check(rpg.Receiver.TryReceiveDamage(new EnemyAttackHit(f.Owner, rpg.Receiver, "death-probe", 0, 100, rpg.Receiver.HitPosition)) &&
                rpg.Controller.Player.Health == 0 && !rpg.Controller.CanAct && !rpg.Receiver.CanReceiveDamage,
                "致死命中保留原RPG死亡与受击准入关闭流程");
        }

        sealed class AttackFixture : IDisposable
        {
            readonly List<GameObject> objects = new();
            readonly List<EnemyAttackCheckReceiver> receivers = new();
            public readonly Vector3 Origin = new(8000, 0, 8000);
            public EnemyObj Owner { get; }
            public AttackFixture()
            {
                var root = Object("攻击检查_敌人"); root.SetActive(false); root.transform.position = Origin;
                Owner = root.AddComponent<EnemyObj>(); root.GetComponent<NavMeshAgent>().enabled = false;
                root.SetActive(true);
            }
            public GameObject Object(string name)
            { var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(go); return go; }
            public EnemyAttackCheckReceiver Receiver(Vector3 offset)
            {
                var go = Object("攻击检查_角色"); go.transform.position = Origin + Vector3.up + offset;
                var receiver = go.AddComponent<EnemyAttackCheckReceiver>(); receivers.Add(receiver); return receiver;
            }
            public void ClearReceivers()
            {
                foreach (var receiver in receivers) if (receiver) UnityEngine.Object.DestroyImmediate(receiver.gameObject);
                receivers.Clear();
            }
            public void Dispose()
            { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) UnityEngine.Object.DestroyImmediate(objects[i]); }
        }

        sealed class RpgFixture : IDisposable
        {
            readonly GameObject root;
            readonly RpgStageDefinition stage;
            public LinearRpgController Controller { get; }
            public RpgDamageReceiver Receiver { get; }
            public SkillEffects Effects { get; }
            public ShieldEffectDefinition Shield { get; }
            public SkillDefinition Buff { get; }

            public RpgFixture(Vector3 position)
            {
                root = new GameObject("敌人攻击检查_真实RPG") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false); root.transform.position = position;
                try
                {
                    Transform Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go.transform; }
                    var head = Child("检查头部"); head.localPosition = Vector3.up * 1.6f;
                    var lab = root.AddComponent<NarutoVR.SealLab.SealLab>(); lab.enabled = false;
                    var skills = root.AddComponent<SkillTestController>(); skills.enabled = false;
                    Effects = root.AddComponent<SkillEffects>(); Effects.head = head; Effects.playerRoot = root.transform; Effects.lab = lab;
                    var locomotion = root.AddComponent<RpgLocomotionController>(); locomotion.head = head; locomotion.rigRoot = root.transform;
                    var node = Child("检查点").gameObject.AddComponent<NavigationNode>(); node.nodeId = "enemy-damage-check";
                    var target = Child("检查靶").gameObject.AddComponent<SkillTrainingTarget>(); target.resetAfterSeconds = 0;
                    stage = ScriptableObject.CreateInstance<RpgStageDefinition>(); stage.stageId = "enemy-damage-check";
                    skills.effects = Effects; skills.lab = lab;
                    skills.catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>("Assets/ScriptableObjects/技能目录配置/线性RPG技能目录.asset");
                    Controller = root.AddComponent<LinearRpgController>(); Controller.skills = skills; Controller.locomotion = locomotion;
                    Controller.handPoseSettings = AssetDatabase.LoadAssetAtPath<BasicHandPoseSettings>("Assets/ScriptableObjects/基础手形配置/官方基础手形.asset");
                    Controller.stages = new[] { stage }; Controller.targets = new[] { target }; Controller.checkpointNodes = new[] { node };
                    Controller.autoLoad = false; Controller.autoAdvanceStages = false;
                    Controller.saveFileName = "enemy-damage-probe-" + Guid.NewGuid().ToString("N") + ".json";
                    Receiver = root.AddComponent<RpgDamageReceiver>();
                    Shield = ScriptableObject.CreateInstance<ShieldEffectDefinition>(); Shield.capacity = 50;
                    var visual = Child("护盾检查_空视觉").gameObject; visual.SetActive(false); Shield.visualPrefab = visual;
                    Buff = ScriptableObject.CreateInstance<SkillDefinition>(); Buff.displayName = "检查护盾"; Buff.durationSeconds = 30;
                    root.SetActive(true);
                    // Awake 必须真实运行；不跨帧，立即停掉不属于此次伤害检查的语音/手势 Start。
                    skills.enabled = false;
                    if (!Controller.CanAct) throw new Exception("真实 RPG 检查初始化失败：" + Controller.Status);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                if (root) Object.DestroyImmediate(root);
                if (stage) Object.DestroyImmediate(stage);
                if (Shield) Object.DestroyImmediate(Shield);
                if (Buff) Object.DestroyImmediate(Buff);
            }
        }
    }
}
#endif
