using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillController
    {

        FireballCast currentFireball;
        GameObject heldObject => currentFireball?.Visual;
        bool heldInLeftHand => currentFireball?.LeftHand ?? false;
        double heldExpiresAt => currentFireball?.ExpiresAt ?? 0;
        ProjectileParameters heldParameters => currentFireball?.Parameters ?? default;

        sealed class FireballCast
        {
            public readonly ProjectileParameters Parameters;
            public readonly bool LeftHand;
            public readonly string DisplayName;
            public GameObject Visual { get; private set; }
            public double ExpiresAt { get; private set; }
            Pose wrist;
            double observed;

            public FireballCast(GameObject visual, ProjectileParameters parameters, bool leftHand, string displayName)
            {
                Visual = visual;
                Parameters = parameters;
                LeftHand = leftHand;
                DisplayName = displayName;
            }

            public bool Arm(SkillController owner)
            {
                if (!Visual || owner.HasOccupiedHand || !owner.TryGetWrist(LeftHand, out wrist, out observed)) return false;
                ExpiresAt = owner.Now + Parameters.HeldLifetime;
                return true;
            }

            public void Publish(SkillController owner)
            {
                try { owner.HoldFireball(this, wrist, observed); }
                catch { if (owner.currentFireball == this) owner.ClearHeld(); else Discard(); throw; }
            }

            public GameObject ReleaseVisual()
            {
                var ball = Visual;
                Visual = null;
                return ball;
            }

            public void Discard() => FreePrepared(ReleaseVisual());
        }

        /// <summary>投射物配置的执行入口，先冻结参数，再占用持握槽并创建手持实例。</summary>
        PreparedSkillCast PrepareFireball(ProjectileEffectDefinition definition, SkillDefinition skill)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0 || !skill || clearing || releasingEffects > 0) return null;
            if (HasOccupiedHand) { Report("手部技能槽已占用，请先释放当前持物"); return null; }
            if (!head || !TryGetWrist(skill.leftHand, out _, out _))
            { Report($"{(skill.leftHand ? "左" : "右")}手当前追踪无效，无法生成火球"); return null; }

            var parameters = new ProjectileParameters(definition);
            if (parameters.ReleaseMode == ProjectileReleaseMode.GripThrow && ThrowGripProvider == null)
            { Report("投掷模式未连接握拳/捏合输入，无法生成"); return null; }
            GameObject ball = null;
            try
            {
                if (parameters.Visual) ball = Instantiate(parameters.Visual, StagingRoot);
                else
                {
                    Material material = GetEffectMaterial(true, parameters.Material);
                    if (!material) { Report("火球材质不可用，生成失败"); return null; }
                    ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ball.SetActive(false);
                    ball.transform.SetParent(StagingRoot, false);
                    Collider sphereCollider = ball.GetComponent<Collider>();
                    sphereCollider.enabled = false;
                    if (Application.isPlaying) Destroy(sphereCollider); else DestroyImmediate(sphereCollider);
                    var renderer = ball.GetComponent<Renderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                ball.SetActive(false);
                var cast = new FireballCast(ball, parameters, skill.leftHand, skill.displayName);
                return new PreparedSkillCast(() => cast.Arm(this), () => cast.Publish(this), cast.Discard);
            }
            catch { FreePrepared(ball); throw; }
        }

        void HoldFireball(FireballCast cast, Pose wrist, double observed)
        {
            currentFireball = cast;
            var ball = cast.Visual;
            var parameters = cast.Parameters;
            ball.transform.SetParent(null, true);
            SetActivationAudioAnchor(heldObject.transform);
            throwState = parameters.ReleaseMode == ProjectileReleaseMode.GripThrow ? new HandThrowState(parameters.Throwing) : null;
            heldObject.name = "技能火球_持握";
            heldObject.transform.localScale = Vector3.one * parameters.Radius * 2;
            DisableVisualColliders(heldObject);

            if (heldObject.TryGetComponent<SkillVisualMotion>(out var motion)) motion.SetFlying(false);
            throwIntent = throwState != null && parameters.Throwing.assisted.enabled ? new HandThrowIntentState(parameters.Throwing,heldInLeftHand) : null;
            shotId = Guid.NewGuid().ToString("N");
            heldSource=ThrowSource;
            lostAt = -1;
            ResetMotion(Now);
            UpdateHeldPosition(wrist);
            previewWrist=wrist.position; previewWristStamp=observed;
            if (throwState != null) { safeHeldPoint = heldObject.transform.position; hasSafeHeldPoint = ThrowPathClear(safeHeldPoint,safeHeldPoint); }
            ball.SetActive(true);
            if (!ball || heldObject != ball) return;
            PublishProjectileStage(ProjectileStage.Formed, ball.transform.position);
            if (heldObject != ball) return;
            ObserveThrow("spawn", "持球已生成，等待准备及握持", wrist, default, 0, false);
            if (heldObject != ball) return;
            Report($"{cast.DisplayName}已在{(heldInLeftHand ? "左" : "右")}手生成，" +
                (throwState != null ? "抬起持球手，看向目标；握住后甩手松开、后拉前送或伸手保持发射" : "看向目标，准备后向前轻推发射"));
        }

        private void UpdateHeld(double now)
        {
            previewWristStamp=double.NegativeInfinity;
            if (now >= heldExpiresAt) { ObserveThrow("expired","火球持握超时",default,default,0,false); ClearHeld(); Report("火球持握超时，已消散"); return; }
            if (!head || !TryGetWrist(heldInLeftHand, out Pose wrist, out double observed))
            {
                throwInputValid = false; releasePoseValid = false;
                if (lostAt < 0) { lostAt = now; ResetMotion(now); Report("手部追踪暂失，保留火球并暂停投掷"); }
                if (now - lostAt >= .18) heldObject.SetActive(false);
                ObserveThrow("tracking_lost","持球手追踪缺失或过期",default,default,0,false);
                return;
            }
            if (lostAt >= 0) { lostAt = -1; ResetMotion(now); Report("手部追踪恢复，请重新握住再投掷"); }
            heldObject.SetActive(true);
            UpdateHeldPosition(wrist);
            if(heldSource!=ThrowSource)
            {
                heldSource=ThrowSource;ResetMotion(now);
                ObserveThrow("source_changed","手部来源已改变，请重新握住",wrist,default,observed,false);return;
            }
            if (PushInputSuppressed || (throwState != null && ThrowInputSuppressed))
            {
                ResetMotion(now);
                ObserveThrow("suppressed","界面、角色状态或其他输入仲裁阻止投掷",wrist,default,observed,false);
                return;
            }
            previewWrist=wrist.position; previewWristStamp=observed;
            if (throwState == null) { TrackPush(wrist.position,observed,now); return; }
            ThrowGripSample grip = ThrowGripProvider != null ? ThrowGripProvider(heldInLeftHand,heldParameters.Throwing) : default;
            if (now < armAt) { ObserveThrow("warming_up","生成或恢复追踪后的准备时间",wrist,grip,observed,true); return; }
            if (!double.IsFinite(grip.observed) || Math.Abs(grip.observed-observed) > .01)
            {
                ResetMotion(now);
                ObserveThrow("sample_skew","手形与腕骨不在同一观测帧",wrist,grip,observed,true); return;
            }
            double handNow=HandNow;
            Vector3 velocity=throwState.Process(wrist.position,head.position,grip,handNow);
            AssistedThrowCandidate candidate=throwIntent?.Process(wrist.position,head.position,head.forward,grip,handNow) ?? default;
            bool backwards = throwIntent != null && throwIntent.ContradictsWindup(throwState.Estimate.velocity);
            if (velocity.sqrMagnitude>.0001f && throwIntent != null && throwIntent.ContradictsWindup(velocity)) velocity=Vector3.zero;
            bool pending=throwState.ReleasePending && !backwards;
            Vector3 currentCenter=heldObject.transform.position;
            throwInputValid=(grip.fistValid||grip.pinchValid) && observed<=handNow && handNow-observed<=heldParameters.Throwing.sampleMaxAge;
            throwBlockReason=null;
            Physics.SyncTransforms();
            // 已确认的物理松手优先；尚未确认的松手候选允许辅助动作继续竞争。
            bool assisted = velocity.sqrMagnitude<=.0001f && candidate.kind!=AssistedThrowKind.None;
            string mode=assisted?candidate.kind.ToString():"GripRelease";
            if (assisted)
            {
                if (!ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                { RejectThrow("辅助发射路径或出手点被阻挡",wrist,grip,observed); return; }
                releasePose=new Pose(currentCenter,wrist.rotation); releasePoseValid=true;
                velocity=candidate.direction * heldParameters.Speed;
            }
            else if (pending && throwState.Estimate.accepted && (!releasePoseValid || releasePoseCandidate!=throwState.CandidateId))
            {
                if (!ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                { RejectThrow("释放位置或甩手路径被阻挡，请重新握住",wrist,grip,observed); return; }
                releasePose=new Pose(currentCenter,wrist.rotation); releasePoseValid=true; releasePoseCandidate=throwState.CandidateId;
            }
            if (velocity.sqrMagnitude>.0001f)
            {
                if (!releasePoseValid || !ThrowPathClear(releasePose.position,releasePose.position))
                { RejectThrow("释放点被障碍占据，未发射",wrist,grip,observed); return; }
                if (!TryResolveAim(releasePose.position, out var aim))
                { RejectThrow("头向瞄准点无效或位于出手点之后，未发射",wrist,grip,observed); return; }
                heldObject.transform.SetPositionAndRotation(aim.origin,releasePose.rotation);
                ObserveThrow("launched",assisted?(candidate.kind==AssistedThrowKind.ReachHold?"伸手保持发射":"后拉前送评分发射"):"握持松手投掷发射",wrist,grip,observed,true,aim.direction*heldParameters.Speed,mode);
                Launch(aim.direction); return;
            }
            if (pending && releasePoseValid) heldObject.transform.SetPositionAndRotation(releasePose.position,releasePose.rotation);
            else
            {
                releasePoseValid=false;
                if (!ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                { RejectThrow("持球位置或移动路径被阻挡，请移回可用空间",wrist,grip,observed); return; }
                safeHeldPoint=currentCenter; hasSafeHeldPoint=true;
            }
            string reason=throwIntent != null ? throwIntent.Score.reason+"；松手："+throwState.Status : throwState.Status;
            if (backwards) reason="后拉阶段：不采用朝后的松手轨迹；"+reason;
            ObserveThrow("frame",reason,wrist,grip,observed,true);
        }

        private void RejectThrow(string reason,Pose wrist,ThrowGripSample grip,double observed)
        {
            // 重置前保存本次候选和评分；同一个动作只尝试一次发射方式。
            ObserveThrow("rejected",reason,wrist,grip,observed,true);
            bool hadSafePoint=hasSafeHeldPoint;
            ResetMotion(Now); hasSafeHeldPoint=hadSafePoint; throwBlockReason=reason;
        }

        private void ObserveThrow(string kind,string reason,Pose wrist,ThrowGripSample grip,double observed,bool hasSample,Vector3 velocity=default,string mode="")
        {
            if (throwState==null) return;
            if(kind=="launched")LastReleaseMode=ReleaseModeLabel(mode);
            bool gripTimeValid=double.IsFinite(grip.observed);
            bool headValid=head&&NarutoVR.SealLab.SealLab.FinitePose(new Pose(head.position,head.rotation));
            if(!gripTimeValid)grip.observed=0;
            var observation=new HandThrowObservation
            {
                schemaVersion=2,shotId=shotId,sequence=++observationSequence,eventType=kind,releaseMode=mode,
                source=ThrowSource,
                hasSample=hasSample,suppressed=ThrowInputSuppressed||PushInputSuppressed,
                gripTimestampValid=hasSample&&gripTimeValid,headPoseValid=headValid,
                observed=hasSample&&double.IsFinite(grip.observed)?grip.observed:0,wristObserved=double.IsFinite(observed)?observed:0,
                now=double.IsFinite(HandNow)?HandNow:0,hand=hasSample?wrist.position:Vector3.zero,head=headValid?head.position:Vector3.zero,headForward=headValid?head.forward:Vector3.zero,
                grip=grip,score=CurrentThrowScore,estimate=throwState.PreviewEstimate.samples>0&&!throwState.ReleasePending?throwState.PreviewEstimate:throwState.Estimate,
                status=reason,armed=throwState.Armed||(throwIntent?.Armed??false),launchVelocity=velocity,candidateId=throwState.CandidateId,
                pending=throwState.ReleasePending,inputValid=throwInputValid,gripProgress=throwState.GripProgress,releaseProgress=throwState.ReleaseProgress,
                releaseOrigin=releasePoseValid?releasePose.position:HeldThrowPosition,
                storageRemainingSeconds=HeldRemainingSeconds,projectileSpeed=heldParameters.Speed
            };
            LastThrowObservation=observation; HasThrowObservation=true;
            if (kind=="launched"||kind=="rejected"||kind=="expired"||kind=="tracking_lost"||kind=="sample_skew")
                LastThrowOutcome=reason+"；动作分 "+(observation.score.available?$"{observation.score.total:0.0}/{observation.score.threshold:0}":"未计算");
            if (ThrowObserved!=null) foreach(Action<HandThrowObservation> listener in ThrowObserved.GetInvocationList())
                try { listener(observation); } catch(Exception error) { Debug.LogWarning("投掷诊断监听失败："+error.Message,this); }
        }

        private void UpdateHeldPosition(Pose wrist)
        {
            Vector3 offset = heldParameters.Offset;
            if (heldInLeftHand) offset.x = -offset.x;
            Quaternion basis = heldParameters.ReleaseMode == ProjectileReleaseMode.GripThrow ? wrist.rotation : head.rotation;
            heldObject.transform.SetPositionAndRotation(wrist.position + basis * offset, wrist.rotation);
        }

        private void Launch(Vector3 direction)
        {
            LastLaunchDirection = direction.normalized;
            LastLaunchPosition = heldObject.transform.position;
            throwInputValid = releasePoseValid = false;
            GameObject ball = currentFireball.ReleaseVisual();
            StopHeldAudio(ball.transform);
            var projectileAudio = heldParameters.Audio;
            pushSamples.Clear();
            lostAt = -1;
            ball.name = "技能火球_飞行";
            var projectile = ball.AddComponent<SkillProjectile>();
            projectile.speed = heldParameters.Speed;
            projectile.radius = heldParameters.Radius;
            projectile.damage = heldParameters.Damage;
            projectile.AttackerTarget = head;
            projectile.poiseDamage = heldParameters.PoiseDamage;
            projectile.lifetime = heldParameters.FlightLifetime;
            projectile.impactPrefab = heldParameters.Impact;
            if (vfxPool && vfxPool.Handles(heldParameters.Impact))
            {
                projectile.ImpactPool = vfxPool;
                projectile.ImpactOwner = this;
            }
            else projectile.ImpactCreated = OnImpactCreated;
            projectile.SurfaceHit = point =>
            {
                LastImpactAudioHandled = PlayAudio(projectileAudio, SkillAudioCue.Impact, point);
                PublishProjectileStage(ProjectileStage.Impact, point);
            };
            projectile.GameClock = GameClock;
            if (ball.TryGetComponent<SkillVisualMotion>(out var motion)) motion.SetFlying(true);
            projectile.ArmFlight(direction, playerRoot ? playerRoot : head.root, hitLayers, playerLayers,
                OnTargetHit, OnProjectileFinished);
            projectiles.Add(projectile);
            ++LaunchCount;
            LastLaunchAudioHandled = PlayAudio(projectileAudio, SkillAudioCue.Released, LastLaunchPosition);
            PublishProjectileStage(ProjectileStage.Launched, LastLaunchPosition);
            if (ProjectileLaunched != null)
                foreach (Action listener in ProjectileLaunched.GetInvocationList())
                    try { listener(); } catch (Exception e) { Debug.LogWarning("火球发射监听失败：" + e.Message, this); }
            Report("火球已发射");
            if (projectile) projectile.CheckLaunchOverlap();
        }
    }
}
