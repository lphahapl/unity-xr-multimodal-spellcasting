#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEngine;
using Object = UnityEngine.Object;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>检查效果配置校验及执行结果，使用隔离实例验证扩展效果的注册和清理。</summary>
    public static class SkillEffectConfigurationChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Real executor and projectile paths, isolated from live headset state and scene targets.</summary>
        public static int Run()
        {
            if (!Application.isPlaying || Time.timeScale <= 0) throw new InvalidOperationException("效果配置检查需要未暂停的播放模式。");
            using var f = new Fixture();
            int passed = 0;

            var shieldA = f.Asset<ShieldEffectDefinition>(); shieldA.capacity = 250;
            var shieldB = f.Asset<ShieldEffectDefinition>(); shieldB.capacity = 75;
            var shieldSkill = f.Skill(shieldA);
            Require(f.Effects.TryCast(shieldSkill), "护盾资产激活失败");
            Near(250, f.Effects.ShieldPoints, "护盾应从技能效果资产读取容量");
            Near(250, f.Effects.ShieldCapacity, "HUD上限应读取当前护盾配置");
            shieldA.capacity = 600;
            Near(0, f.Effects.AbsorbPracticeDamage(30), "原有护盾应正常吸收伤害");
            Near(220, f.Effects.ShieldPoints, "运行中修改SO不得修改现有护盾");
            shieldSkill.effectDefinition = shieldB;
            Require(f.Effects.TryCast(shieldSkill), "第二种护盾资产激活失败");
            Near(75, f.Effects.ShieldPoints, "不同护盾应按各自容量刷新，不相加");
            Near(100, f.Effects.shieldCapacity, "技能不得修改旧场景默认护盾字段");
            passed++;

            var fireA = f.Asset<ProjectileEffectDefinition>(); fireA.damage = 13; fireA.speed = 6; fireA.radius = .08f;
            var fireB = f.Asset<ProjectileEffectDefinition>(); fireB.damage = 41; fireB.speed = 17; fireB.radius = .11f;
            var fireSkill = f.Skill(fireA); fireSkill.effectKind = SkillEffectKind.SelfBuff;
            f.RefreshHands(); Require(f.Effects.TryCast(fireSkill), "效果资产应优先于旧枚举");
            GameObject first = f.Held(); Near(.16f, first.transform.localScale.x, "持握球尺寸来自该技能配置");
            fireA.damage = 900; fireA.speed = 90; fireA.radius = .9f; fireA.heldOffset = Vector3.one * 10;
            Invoke(f.Effects, "UpdateHeldPosition", new Pose(f.RightWorld, Quaternion.identity));
            Near(f.RightWorld + new Vector3(0,.05f,.08f), first.transform.position, "运行中修改SO不得移动既有持握球");
            Invoke(f.Effects, "Launch", Vector3.forward);
            var firstProjectile = first.GetComponent<SkillProjectile>();
            Near(13, firstProjectile.damage, "发射时应保留激活时伤害");
            Near(6, firstProjectile.speed, "发射时应保留激活时速度");
            Near(.08f, firstProjectile.radius, "发射时应保留激活时半径");
            passed++;

            fireSkill.effectDefinition = fireB;
            f.RefreshHands(); Require(f.Effects.TryCast(fireSkill), "上一球飞出后第二技能应能生成");
            var second = f.Held(); Invoke(f.Effects, "Launch", Vector3.forward);
            var secondProjectile = second.GetComponent<SkillProjectile>();
            Near(41, secondProjectile.damage, "第二技能应使用自己的伤害");
            Near(17, secondProjectile.speed, "第二技能应使用自己的速度");
            Near(13, firstProjectile.damage, "第二技能不得改写已飞出球的伤害");
            Near(25, f.Effects.projectileDamage, "SO不得改写旧场景默认伤害");
            Near(9, f.Effects.projectileSpeed, "SO不得改写旧场景默认速度");
            passed++;

            int before = f.Effects.ActivationCount;
            fireB.speed = float.NaN; Require(!f.Effects.TryCast(fireSkill), "非有限配置必须拒绝");
            shieldB.capacity = -1; Require(!f.Effects.TryCast(shieldSkill), "负护盾容量必须拒绝");
            Require(f.Effects.ActivationCount == before, "无效配置不得计入成功激活");
            passed++;

            var old = new ProbeLifetime(); var latest = new ProbeLifetime();
            Require(f.Effects.RegisterEffectLifetime(old), "扩展效果应可注册");
            Require(!f.Effects.RegisterEffectLifetime(old), "同一扩展句柄不可重复注册");
            Require(f.Effects.RegisterEffectLifetime(latest), "第二个扩展效果应可注册");
            Require(!f.Effects.RegisterEffectLifetime(new ProbeLifetime { IsAlive = false }), "失效句柄不可注册");
            Invoke(f.Effects, "LateUpdate");
            Require(old.Ticks == 1 && latest.Ticks == 1, "扩展句柄每帧只应更新一次");
            fireB.speed = 17; f.RefreshHands(); Require(f.Effects.TryCast(fireSkill), "取消顺序测试持握生成失败"); f.Held();
            Require(f.Effects.CancelLatest() && !f.Effects.HasHeldObject, "持握应先于扩展效果取消");
            Require(old.Cancels == 0 && latest.Cancels == 0, "收起持握物不应同时取消扩展效果");
            Require(f.Effects.CancelLatest(), "取消应处理最新扩展效果");
            Require(latest.Cancels == 1 && old.Cancels == 0, "扩展效果取消顺序错误");
            Near(75, f.Effects.ShieldPoints, "取消扩展效果不得同时取消护盾");
            f.Effects.ClearAll(); f.Effects.ClearAll();
            Require(old.Cancels == 1 && latest.Cancels == 1, "多次清理不得重复调用句柄取消");
            passed++;

            var expiring = new ProbeLifetime { EndOnTick = true };
            Require(f.Effects.RegisterEffectLifetime(expiring), "自然结束效果注册失败");
            Invoke(f.Effects, "LateUpdate"); Invoke(f.Effects, "LateUpdate"); f.Effects.ClearAll();
            Require(expiring.Ticks == 1 && expiring.Cancels == 1, "自然结束应清理一次且停止Tick");
            passed++;

            var failing = new ProbeLifetime { ThrowOnTick = true };
            Require(f.Effects.RegisterEffectLifetime(failing), "异常效果注册失败");
            Invoke(f.Effects, "LateUpdate"); f.Effects.ClearAll();
            Require(failing.Cancels == 1, "扩展Tick异常必须清理一次");
            passed++;

            var cancelledDuringCleanup = new ProbeLifetime(); var reentry = new ProbeLifetime();
            bool admitted = true;
            cancelledDuringCleanup.OnCancel = () => admitted = f.Effects.RegisterEffectLifetime(reentry);
            Require(f.Effects.RegisterEffectLifetime(cancelledDuringCleanup), "清理重入测试注册失败");
            f.Effects.ClearAll();
            Require(!admitted && reentry.Cancels == 0, "清理中必须拒绝新注册且不接管被拒句柄");
            passed++;

            var custom = f.Asset<SkillEffectConfigurationProbe>();
            f.Effects.RegisterExecutor<SkillEffectConfigurationProbe>((config, skill) =>
                new PreparedSkillCast(() => f.Effects.RegisterEffectLifetime(config.Handle) && !config.RejectAfterRegistration, null, null));
            var customSkill = f.Skill(custom);
            custom.Handle = new ProbeLifetime();
            before = f.Effects.ActivationCount;
            Require(f.Effects.TryCast(customSkill), "新增效果类型应通过多态资产激活");
            Require(f.Effects.ActivationCount == before + 1, "自定义效果应计入一次激活");
            f.Effects.CancelLatest();
            Require(custom.Handle.Cancels == 1, "自定义资产应接入统一取消");
            custom.Handle = new ProbeLifetime(); custom.RejectAfterRegistration = true;
            before = f.Effects.ActivationCount;
            Require(!f.Effects.TryCast(customSkill), "自定义效果拒绝结果必须保留");
            Require(custom.Handle.Cancels == 1 && f.Effects.ActivationCount == before, "失败的自定义激活必须撤销本次新句柄且不计数");
            passed++;

            var pauseHandle = new ProbeLifetime(); Require(f.Effects.RegisterEffectLifetime(pauseHandle), "暂停测试注册失败");
            Invoke(f.Effects,"OnApplicationPause",true);
            Require(pauseHandle.Cancels == 1,"暂停必须清理扩展效果");
            var disableHandle = new ProbeLifetime(); Require(f.Effects.RegisterEffectLifetime(disableHandle), "禁用测试注册失败");
            f.Effects.enabled = false;
            Require(disableHandle.Cancels == 1,"禁用必须清理扩展效果");
            f.Effects.enabled = true; f.Effects.ClearAll();
            Require(pauseHandle.Cancels == 1 && disableHandle.Cancels == 1,"生命周期重复清理不能重复调用Cancel");
            passed++;

            shieldB.capacity = 75;
            Action<string> brokenListener = _ => throw new InvalidOperationException("预期的UI监听异常验收");
            before = f.Effects.ActivationCount; f.Effects.Message += brokenListener;
            try { Require(f.Effects.TryCast(shieldSkill),"UI监听异常不应使已提交的护盾激活失败"); }
            finally { f.Effects.Message -= brokenListener; }
            Require(f.Effects.ActivationCount == before + 1 && f.Effects.ShieldPoints == 75,"UI监听异常不应破坏效果数值及成功计数");
            passed++;
            return passed + CheckTransaction();
        }

        static int CheckTransaction()
        {
            using var f = new Fixture();
            var player = new Rpg.RpgPlayerState(100,100,0,0);
            f.Effects.GameClock = () => 20;
            Invoke(f.Effects,"BindVitals",player);
            var definition = f.Asset<SkillEffectConfigurationProbe>();
            var skill = f.Skill(definition);
            skill.cooldownGroup = "transaction"; skill.cooldownSeconds = 3;
            Func<PreparedSkillCast> prepare = null;
            f.Effects.RegisterExecutor<SkillEffectConfigurationProbe>((_,__) => prepare());
            int releases = 0, notices = 0;
            player.SetShield(75,50);
            bool priorShieldVisible = false;
            prepare = () => new PreparedSkillCast(() =>
            {
                player.SetShield(5,21);
                priorShieldVisible = player.ShieldPoints==75 && player.ShieldExpiresAt==50;
                return false;
            },
                () => notices++, () => releases++);
            SkillCastResult Attempt(string id, float cost=9) => (SkillCastResult)typeof(SkillController).GetMethod("CommitCast",Private)
                .Invoke(f.Effects,new object[] { skill,player,id,cost,20d,null });
            bool Cast(string id) => Attempt(id).Confirmed;
            Require(!Cast("abort") && priorShieldVisible && player.ShieldPoints == 75 && player.ShieldExpiresAt == 50 &&
                player.Mana == 100 && player.GetCooldown("transaction",20)==0 && releases==1 && notices==0,
                "准备被拒必须完整恢复旧盾、能量与冷却，并且只释放本次准备");
            prepare = () => throw new InvalidOperationException("预期准备失败");
            Require(!Cast("throw") && player.Mana == 100 && f.Effects.ActivationCount==0,"准备抛错不能扣费或确认");
            prepare = () => new PreparedSkillCast(() =>
            {
                player.SetShield(5,21);
                f.Effects.ClearAll();
                return true;
            }, () => notices++, null);
            Require(!Cast("aborted scene") && player.ShieldPoints==0 && player.Mana==100 && notices==0,
                "准备中退场应取消未确认施法，撤销预留不能复活已清理的旧盾");
            var pending = new ProbeLifetime();
            bool hiddenHand = false;
            prepare = () => new PreparedSkillCast(() =>
            {
                bool registered = f.Effects.TryRegisterRightHandEffect(pending);
                Invoke(f.Effects, "LateUpdate");
                hiddenHand = registered && !f.Effects.HasOccupiedHand && pending.Ticks==0;
                return false;
            }, () => notices++, null);
            Require(Attempt("unarmed").Failure==SkillCastFailure.Invalidated && hiddenHand && pending.Cancels==1,
                "待提交的右手不能对外可见或参加更新，失败只释放一次");
            Require(Attempt("no mana",101).Failure==SkillCastFailure.InsufficientMana && player.Mana==100,
                "能量不足应带回稳定原因码");
            bool observedCommit = false, duplicateAccepted = false;
            prepare = () => new PreparedSkillCast(() => true, () =>
            {
                notices++;
                observedCommit = f.Effects.IsPublishingCast && player.Mana == 91 && player.GetCooldown("transaction",20)==3;
                duplicateAccepted = Cast("nested");
                throw new InvalidOperationException("预期呈现失败");
            }, () => releases++);
            Require(Cast("confirm") && observedCommit && !duplicateAccepted && notices==1 && f.Effects.ActivationCount==1 &&
                releases==1 && player.Mana==91 && !f.Effects.IsCastingTransaction,
                "发布前必须已经结算；重入与呈现故障不能重复执行或退款");
            skill.skillId="sibling";
            Require(Attempt("shared").Failure==SkillCastFailure.Cooldown && releases==2 && notices==1 && player.Mana==91,
                "同组的另一技能不得绕过冷却；等待提交的实例应释放");

            var fire = f.Skill(f.Asset<ProjectileEffectDefinition>());
            f.RefreshHands();
            Require(f.Effects.TryCast(fire),"发射事件检查无法创建火球");
            var stages = new List<ProjectileStage>();
            f.Effects.ProjectileStageChanged += (stage,_) => stages.Add(stage);
            bool registered = false;
            f.Effects.ProjectileLaunched += () => throw new InvalidOperationException("预期监听失败");
            Action cancelAtLaunch = () => { registered = f.Effects.ActiveProjectileCount==1; f.Effects.ClearAll(); };
            f.Effects.ProjectileLaunched += cancelAtLaunch;
            var held=f.Held();
            using (var target = new OverlapTarget(held.transform.position))
            {
                Invoke(f.Effects,"Launch",Vector3.forward);
                Require(registered && f.Effects.ActiveProjectileCount==0 && stages.Count==1 &&
                    stages[0]==ProjectileStage.Launched && target.Target.HitCount==0,
                    "先登记飞行再发布；监听者撤回飞行后初始重叠不能造成命中");
            }
            f.Effects.ProjectileLaunched -= cancelAtLaunch;
            int hits = 0;
            f.Effects.ProjectileHitTarget += _ => throw new InvalidOperationException("预期命中监听失败");
            f.Effects.ProjectileHitTarget += _ => hits++;
            f.RefreshHands();
            Require(f.Effects.TryCast(fire),"发射点重叠检查无法创建火球");
            stages.Clear();
            using (var target = new OverlapTarget(f.Held().transform.position))
            {
                Invoke(f.Effects,"Launch",Vector3.forward);
                Require(hits==1 && target.Target.HitCount==1 && f.Effects.ActiveProjectileCount==0 && stages.Count==2 &&
                    stages[0]==ProjectileStage.Launched && stages[1]==ProjectileStage.Impact,
                    "重叠发射应按发射、命中顺序结算一次；单个监听异常不能阻断后续通知");
            }
            var sword=UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/雷光剑配置/雷光剑.asset");
            Require(sword && sword.effectDefinition is ThunderSwordEffectDefinition,"正式雷光剑资源缺失");
            f.Effects.WristPoseOverride=_ => new Pose(f.RightWorld,Quaternion.identity);
            Require(f.Effects.TryCast(sword) && f.Effects.HasRightHeldObject && !f.Effects.TryCast(fire),
                "正式雷光剑应占用同一个右手槽，持剑时不能再出火球");
            var captured=(ThunderSwordEffectDefinition)Get(Get(f.Effects,"rightHandEffect"),"settings");
            var source=(ThunderSwordEffectDefinition)sword.effectDefinition;
            Require(captured != source && captured.directDamage==source.directDamage && captured.wavePrefab==source.wavePrefab,
                "每次雷光剑应冻结当前伤害和行为参数，继续共享只读预置体");
            Require(f.Effects.CancelUnreleased() && !f.Effects.HasOccupiedHand &&
                player.GetCooldown(string.IsNullOrEmpty(sword.cooldownGroup)?sword.skillId:sword.cooldownGroup,20)>0,
                "收剑应释放右手并保留已提交的冷却");
            return 12;
        }

        sealed class OverlapTarget : IDisposable
        {
            public readonly SkillTrainingTarget Target;
            public OverlapTarget(Vector3 point)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = point;
                Target = go.AddComponent<SkillTrainingTarget>();
                Physics.SyncTransforms();
            }
            public void Dispose() => Object.DestroyImmediate(Target.gameObject);
        }

        private static object Get(object target,string field) => target.GetType().GetField(field,Private).GetValue(target);
        private static void Invoke(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static void Require(bool value,string message) { if(!value)throw new Exception(message); }
        private static void Near(float expected,float value,string message) => Require(Mathf.Abs(expected-value)<.001f,$"{message}：预期{expected}，实际{value}");
        private static void Near(Vector3 expected,Vector3 value,string message) => Require(Vector3.Distance(expected,value)<.001f,$"{message}：预期{expected}，实际{value}");

        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> owned = new();
            private readonly GameObject root;
            private readonly Lab lab;
            public readonly SkillController Effects;
            public Vector3 RightWorld => root.transform.position + new Vector3(.25f,1.2f,.45f);
            public Fixture()
            {
                root = Own(new GameObject("效果配置验收_独立玩家")); root.transform.position = new Vector3(300,100,100);
                lab = root.AddComponent<Lab>(); lab.recordingOrigin = root.transform;
                Effects = root.AddComponent<SkillController>(); Effects.lab = lab; Effects.head = root.transform; Effects.playerRoot = root.transform;
            }
            private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
            public T Asset<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
            public SkillDefinition Skill(SkillEffectDefinition definition)
            {
                var skill = Asset<SkillDefinition>(); skill.effectDefinition = definition; skill.skillId = "configuration-check"; skill.displayName = "配置检查"; skill.durationSeconds = 10; skill.cooldownSeconds = 0; return skill;
            }
            public GameObject Held() => Own((GameObject)UnityEditor.EditorUtility.EntityIdToObject(Effects.HeldProjectileInstanceId));
            public void RefreshHands()
            {
                double now = Time.realtimeSinceStartupAsDouble;
                var frame = new SealFrame { time = now };
                foreach (var hand in new[]{frame.left,frame.right})
                {
                    hand.connected = hand.tracked = hand.fresh = true; hand.observedAt = now;
                    int index = (int)HandJointId.HandWristRoot;
                    hand.jointValid[index] = true; hand.joints[index] = new Pose(new Vector3(.25f,1.2f,.45f),Quaternion.identity);
                }
                typeof(Lab).GetProperty("Frame").SetValue(lab,frame);
            }
            public void Dispose()
            {
                if (Effects)
                {
                    var visual = (GameObject)Get(Effects,"buffObject"); if(visual)owned.Add(visual);
                    Effects.ClearAll();
                }
                for(int i=owned.Count-1;i>=0;i--)if(owned[i])Object.DestroyImmediate(owned[i]);
                Physics.SyncTransforms();
            }
        }

        public sealed class ProbeLifetime : ISkillEffectLifetime
        {
            public bool IsAlive { get; set; } = true;
            public int Ticks, Cancels;
            public bool EndOnTick, ThrowOnTick;
            public Action OnCancel;
            public void Tick(double now) { Ticks++; if(ThrowOnTick)throw new InvalidOperationException("预期的异常清理验收"); if(EndOnTick)IsAlive=false; }
            public void Cancel() { Cancels++; IsAlive=false; OnCancel?.Invoke(); }
        }
    }

    // Transient test-only effect proves a third type needs no matcher, receiver or executor switch case.
    public sealed class SkillEffectConfigurationProbe : SkillEffectDefinition
    {
        public SkillEffectConfigurationChecks.ProbeLifetime Handle;
        public bool RejectAfterRegistration;
    }
}
#endif
