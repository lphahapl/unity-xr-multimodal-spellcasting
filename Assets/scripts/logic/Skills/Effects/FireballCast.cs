using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillController
    {
        FireballCast currentFireball;
        HandThrowSettings lastThrowSettings;
        float lastProjectileSpeed;
        bool lastProjectileLeftHand;
        GameObject heldObject => currentFireball?.Visual;
        bool heldInLeftHand => currentFireball?.LeftHand ?? lastProjectileLeftHand;
        double heldExpiresAt => currentFireball?.ExpiresAt ?? 0;
        ProjectileParameters heldParameters => currentFireball?.Parameters ?? default;

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
                var cast = new FireballCast(this, ball, parameters, skill);
                return new PreparedSkillCast(cast.Arm, cast.Publish, cast.Discard);
            }
            catch { FreePrepared(ball); throw; }
        }

        private void Launch(Vector3 direction)
        {
            LastLaunchDirection = direction.normalized;
            LastLaunchPosition = heldObject.transform.position;
            var parameters = currentFireball.Parameters;
            GameObject ball = currentFireball.ReleaseVisual();
            StopHeldAudio(ball.transform);
            var projectileAudio = parameters.Audio;
            currentFireball = null;
            ball.name = "技能火球_飞行";
            var projectile = ball.AddComponent<SkillProjectile>();
            projectile.speed = parameters.Speed;
            projectile.radius = parameters.Radius;
            projectile.damage = parameters.Damage;
            projectile.AttackerTarget = head;
            projectile.poiseDamage = parameters.PoiseDamage;
            projectile.lifetime = parameters.FlightLifetime;
            projectile.impactPrefab = parameters.Impact;
            if (vfxPool && vfxPool.Handles(parameters.Impact))
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
            if (parameters.SurfaceMark) projectile.MarkSurface = hit => StampSurface(hit, parameters.SurfaceMark, direction);
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

        private void UpdateHeld(double now) => currentFireball?.Tick(now);

        void PublishThrowObservation(HandThrowObservation observation)
        {
            LastThrowObservation = observation;
            HasThrowObservation = true;
            if (ThrowObserved == null) return;
            foreach (Action<HandThrowObservation> listener in ThrowObserved.GetInvocationList())
                try { listener(observation); }
                catch (Exception error) { Debug.LogWarning("投掷诊断监听失败：" + error.Message, this); }
        }

        sealed class FireballCast
        {
            readonly SkillController owner;
            public readonly ProjectileParameters Parameters;
            public readonly bool LeftHand;
            public readonly SkillDefinition Source;
            readonly string displayName;
            public GameObject Visual { get; private set; }
            public double ExpiresAt { get; private set; }
            private double lastObserved = double.NegativeInfinity;
            private double previewWristStamp = double.NegativeInfinity;
            private Vector3 previewWrist;
            private Vector3 lastHandPosition, lastHeadPosition;
            private bool hasPreviousSample;
            private HandThrowState throwState;
            private HandThrowIntentState throwIntent;
            private string shotId;
            private string heldSource;
            private bool throwInputValid, releasePoseValid, hasSafeHeldPoint;
            private Pose releasePose;
            private Vector3 safeHeldPoint;
            private long releasePoseCandidate;
            private string throwBlockReason;
            private double armAt, lostAt = -1;
            private readonly List<PushSample> pushSamples = new();

            Pose wrist;
            double observed;

            public FireballCast(SkillController owner, GameObject visual, ProjectileParameters parameters, SkillDefinition skill)
            {
                this.owner = owner;
                Visual = visual;
                Parameters = parameters;
                LeftHand = skill.leftHand;
                Source = skill;
                displayName = skill.displayName;
            }

            public bool Arm()
            {
                if (!Visual || owner.HasOccupiedHand || !owner.TryGetWrist(LeftHand, out wrist, out observed)) return false;
                ExpiresAt = owner.Now + Parameters.HeldLifetime;
                return true;
            }

            public void Publish()
            {
                owner.currentFireball = this;
                owner.lastProjectileLeftHand = LeftHand;
                owner.lastThrowSettings = Parameters.Throwing;
                owner.lastProjectileSpeed = Parameters.Speed;
                try { Hold(wrist, observed); }
                catch
                {
                    if (owner.currentFireball == this) owner.ClearHeld();
                    else Discard();
                    throw;
                }
            }

            public GameObject ReleaseVisual()
            {
                var ball = Visual;
                Visual = null;
                return ball;
            }

            public void Discard() => FreePrepared(ReleaseVisual());
            public string ThrowStatus => Visual && throwState != null ? throwBlockReason ?? (throwIntent != null ? throwIntent.Score.reason : throwState.Status) : string.Empty;
            public ThrowActionScore CurrentThrowScore => throwIntent != null ? throwIntent.Score : default;
            public bool ThrowInputValid => Visual && throwState != null && throwInputValid && !owner.ThrowInputSuppressed && !owner.PushInputSuppressed &&
                owner.HandNow >= owner.LastThrowObservation.observed && owner.HandNow - owner.LastThrowObservation.observed <= Parameters.Throwing.sampleMaxAge;
            public bool ThrowArmed => ThrowInputValid && (throwState.Armed || (throwIntent?.Armed ?? false));
            public bool ReleasePending => ThrowInputValid && throwState.ReleasePending && !(throwIntent?.ContradictsWindup(throwState.Estimate.velocity)??false);
            public float ThrowGripProgress => ThrowInputValid ? throwState.GripProgress : 0;
            public float ThrowReleaseProgress => ThrowInputValid ? throwState.ReleaseProgress : 0;
            public Vector3 ThrowPreviewOrigin => releasePoseValid ? releasePose.position : owner.HeldThrowPosition;

            void Hold(Pose wrist, double observed)
            {
                var ball = Visual;
                var parameters = Parameters;
                ball.transform.SetParent(null, true);
                owner.SetActivationAudioAnchor(Visual.transform);
                throwState = parameters.ReleaseMode == ProjectileReleaseMode.GripThrow ? new HandThrowState(parameters.Throwing) : null;
                Visual.name = "技能火球_持握";
                Visual.transform.localScale = Vector3.one * parameters.Radius * 2;
                DisableVisualColliders(Visual);

                if (Visual.TryGetComponent<SkillVisualMotion>(out var motion)) motion.SetFlying(false);
                throwIntent = throwState != null && parameters.Throwing.assisted.enabled ? new HandThrowIntentState(parameters.Throwing,LeftHand) : null;
                shotId = Guid.NewGuid().ToString("N");
                heldSource=owner.ThrowSource;
                lostAt = -1;
                ResetMotion(owner.Now);
                UpdateHeldPosition(wrist);
                previewWrist=wrist.position; previewWristStamp=observed;
                if (throwState != null) { safeHeldPoint = Visual.transform.position; hasSafeHeldPoint = owner.ThrowPathClear(safeHeldPoint,safeHeldPoint); }
                ball.SetActive(true);
                if (!ball || owner.currentFireball != this || !Visual) return;
                owner.PublishProjectileStage(ProjectileStage.Formed, ball.transform.position);
                if (owner.currentFireball != this || !Visual) return;
                ObserveThrow("spawn", "持球已生成，等待准备及握持", wrist, default, 0, false);
                if (owner.currentFireball != this || !Visual) return;
                owner.Report($"{displayName}已在{(LeftHand ? "左" : "右")}手生成，" +
                    (throwState != null ? "抬起持球手，看向目标；握住后甩手松开、后拉前送或伸手保持发射" : "看向目标，准备后向前轻推发射"));
            }

            public void Tick(double now)
            {
                previewWristStamp=double.NegativeInfinity;
                if (now >= ExpiresAt)
                {
                    ObserveThrow("expired", "火球持握超时", default, default, 0, false);
                    if (owner.currentFireball == this)
                    {
                        owner.ClearHeld();
                        owner.Report("火球持握超时，已消散");
                    }
                    return;
                }
                if (!owner.head || !owner.TryGetWrist(LeftHand, out Pose wrist, out double observed))
                {
                    throwInputValid = false; releasePoseValid = false;
                    if (lostAt < 0) { lostAt = now; ResetMotion(now); owner.Report("手部追踪暂失，保留火球并暂停投掷"); }
                    if (!Visual || owner.currentFireball != this) return;
                    if (now - lostAt >= .18) Visual.SetActive(false);
                    ObserveThrow("tracking_lost","持球手追踪缺失或过期",default,default,0,false);
                    return;
                }
                if (lostAt >= 0) { lostAt = -1; ResetMotion(now); owner.Report("手部追踪恢复，请重新握住再投掷"); }
                if (!Visual || owner.currentFireball != this) return;
                Visual.SetActive(true);
                if (!Visual || owner.currentFireball != this) return;
                UpdateHeldPosition(wrist);
                if(heldSource!=owner.ThrowSource)
                {
                    heldSource=owner.ThrowSource;ResetMotion(now);
                    ObserveThrow("source_changed","手部来源已改变，请重新握住",wrist,default,observed,false);return;
                }
                if (owner.PushInputSuppressed || (throwState != null && owner.ThrowInputSuppressed))
                {
                    ResetMotion(now);
                    ObserveThrow("suppressed","界面、角色状态或其他输入仲裁阻止投掷",wrist,default,observed,false);
                    return;
                }
                previewWrist=wrist.position; previewWristStamp=observed;
                if (throwState == null) { TrackPush(wrist.position,observed,now); return; }
                ThrowGripSample grip = owner.ThrowGripProvider != null ? owner.ThrowGripProvider(LeftHand,Parameters.Throwing) : default;
                if (now < armAt) { ObserveThrow("warming_up","生成或恢复追踪后的准备时间",wrist,grip,observed,true); return; }
                if (!double.IsFinite(grip.observed) || Math.Abs(grip.observed-observed) > .01)
                {
                    ResetMotion(now);
                    ObserveThrow("sample_skew","手形与腕骨不在同一观测帧",wrist,grip,observed,true); return;
                }
                double handNow=owner.HandNow;
                Vector3 velocity=throwState.Process(wrist.position,owner.head.position,grip,handNow);
                AssistedThrowCandidate candidate=throwIntent?.Process(wrist.position,owner.head.position,owner.head.forward,grip,handNow) ?? default;
                bool backwards = throwIntent != null && throwIntent.ContradictsWindup(throwState.Estimate.velocity);
                if (velocity.sqrMagnitude>.0001f && throwIntent != null && throwIntent.ContradictsWindup(velocity)) velocity=Vector3.zero;
                bool pending=throwState.ReleasePending && !backwards;
                Vector3 currentCenter=Visual.transform.position;
                throwInputValid=(grip.fistValid||grip.pinchValid) && observed<=handNow && handNow-observed<=Parameters.Throwing.sampleMaxAge;
                throwBlockReason=null;
                Physics.SyncTransforms();
                // 已确认的物理松手优先；尚未确认的松手候选允许辅助动作继续竞争。
                bool assisted = velocity.sqrMagnitude<=.0001f && candidate.kind!=AssistedThrowKind.None;
                string mode=assisted?candidate.kind.ToString():"GripRelease";
                if (assisted)
                {
                    if (!owner.ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                    { RejectThrow("辅助发射路径或出手点被阻挡",wrist,grip,observed); return; }
                    releasePose=new Pose(currentCenter,wrist.rotation); releasePoseValid=true;
                    velocity=candidate.direction * Parameters.Speed;
                }
                else if (pending && throwState.Estimate.accepted && (!releasePoseValid || releasePoseCandidate!=throwState.CandidateId))
                {
                    if (!owner.ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
                    { RejectThrow("释放位置或甩手路径被阻挡，请重新握住",wrist,grip,observed); return; }
                    releasePose=new Pose(currentCenter,wrist.rotation); releasePoseValid=true; releasePoseCandidate=throwState.CandidateId;
                }
                if (velocity.sqrMagnitude>.0001f)
                {
                    if (!releasePoseValid || !owner.ThrowPathClear(releasePose.position,releasePose.position))
                    { RejectThrow("释放点被障碍占据，未发射",wrist,grip,observed); return; }
                    if (!owner.TryResolveAim(releasePose.position, out var aim))
                    { RejectThrow("头向瞄准点无效或位于出手点之后，未发射",wrist,grip,observed); return; }
                    Visual.transform.SetPositionAndRotation(aim.origin,releasePose.rotation);
                    ObserveThrow("launched",assisted?(candidate.kind==AssistedThrowKind.ReachHold?"伸手保持发射":"后拉前送评分发射"):"握持松手投掷发射",wrist,grip,observed,true,aim.direction*Parameters.Speed,mode);
                    if (owner.currentFireball == this && Visual) owner.Launch(aim.direction);
                    return;
                }
                if (pending && releasePoseValid) Visual.transform.SetPositionAndRotation(releasePose.position,releasePose.rotation);
                else
                {
                    releasePoseValid=false;
                    if (!owner.ThrowPathClear(hasSafeHeldPoint?safeHeldPoint:currentCenter,currentCenter))
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
                if (owner.currentFireball != this) return;
                bool hadSafePoint=hasSafeHeldPoint;
                ResetMotion(owner.Now); hasSafeHeldPoint=hadSafePoint; throwBlockReason=reason;
            }

            private void ObserveThrow(string kind,string reason,Pose wrist,ThrowGripSample grip,double observed,bool hasSample,Vector3 velocity=default,string mode="")
            {
                if (throwState==null) return;
                if(kind=="launched")owner.LastReleaseMode=ReleaseModeLabel(mode);
                bool gripTimeValid=double.IsFinite(grip.observed);
                bool headValid=owner.head&&NarutoVR.SealLab.SealLab.FinitePose(new Pose(owner.head.position,owner.head.rotation));
                if(!gripTimeValid)grip.observed=0;
                var observation=new HandThrowObservation
                {
                    schemaVersion=2,shotId=shotId,sequence=++owner.observationSequence,eventType=kind,releaseMode=mode,
                    source=owner.ThrowSource,
                    hasSample=hasSample,suppressed=owner.ThrowInputSuppressed||owner.PushInputSuppressed,
                    gripTimestampValid=hasSample&&gripTimeValid,headPoseValid=headValid,
                    observed=hasSample&&double.IsFinite(grip.observed)?grip.observed:0,wristObserved=double.IsFinite(observed)?observed:0,
                    now=double.IsFinite(owner.HandNow)?owner.HandNow:0,hand=hasSample?wrist.position:Vector3.zero,head=headValid?owner.head.position:Vector3.zero,headForward=headValid?owner.head.forward:Vector3.zero,
                    grip=grip,score=CurrentThrowScore,estimate=throwState.PreviewEstimate.samples>0&&!throwState.ReleasePending?throwState.PreviewEstimate:throwState.Estimate,
                    status=reason,armed=throwState.Armed||(throwIntent?.Armed??false),launchVelocity=velocity,candidateId=throwState.CandidateId,
                    pending=throwState.ReleasePending,inputValid=throwInputValid,gripProgress=throwState.GripProgress,releaseProgress=throwState.ReleaseProgress,
                    releaseOrigin=releasePoseValid?releasePose.position:owner.HeldThrowPosition,
                    storageRemainingSeconds=owner.HeldRemainingSeconds,projectileSpeed=Parameters.Speed
                };
                if (kind=="launched"||kind=="rejected"||kind=="expired"||kind=="tracking_lost"||kind=="sample_skew")
                    owner.LastThrowOutcome=reason+"；动作分 "+(observation.score.available?$"{observation.score.total:0.0}/{observation.score.threshold:0}":"未计算");
                owner.PublishThrowObservation(observation);
            }

            private void UpdateHeldPosition(Pose wrist)
            {
                Vector3 offset = Parameters.Offset;
                if (LeftHand) offset.x = -offset.x;
                Quaternion basis = Parameters.ReleaseMode == ProjectileReleaseMode.GripThrow ? wrist.rotation : owner.head.rotation;
                Visual.transform.SetPositionAndRotation(wrist.position + basis * offset, wrist.rotation);
            }

            public void ResetMotion(double now)
            {
                throwInputValid = releasePoseValid = hasSafeHeldPoint = false; throwBlockReason = null;
                pushSamples.Clear();
                lastObserved = double.NegativeInfinity;
                hasPreviousSample = false;
                throwState?.Reset();
                throwIntent?.Reset();
                armAt = now + Parameters.ArmDelay;
            }

            private void TrackPush(Vector3 handPosition, double observed, double now)
            {
                // 重复读取的骨骼快照不累计为新的手部运动。
                if (observed <= lastObserved) return;
                Vector3 headPosition = owner.head.position;
                if (hasPreviousSample && ((handPosition - lastHandPosition).sqrMagnitude > 0.35f * 0.35f ||
                    (headPosition - lastHeadPosition).sqrMagnitude > 0.35f * 0.35f)) ResetMotion(now);
                lastObserved = observed;
                lastHandPosition = handPosition;
                lastHeadPosition = headPosition;
                hasPreviousSample = true;
                if (now < armAt) return;

                float minSeconds = Parameters.PushMinimum;
                float maxSeconds = Parameters.PushMaximum;
                while (pushSamples.Count > 0 && observed - pushSamples[0].Time > maxSeconds)
                    pushSamples.RemoveAt(0);
                foreach (PushSample start in pushSamples)
                {
                    if (!IsForwardPush(start.Hand, handPosition, start.Head, headPosition, start.Forward,
                        (float)(observed - start.Time), Parameters.PushDistance, minSeconds, maxSeconds)) continue;
                    if (owner.TryResolveAim(Visual.transform.position, out var aim)) owner.Launch(aim.direction);
                    return;
                }
                pushSamples.Add(new PushSample(handPosition, headPosition, owner.head.forward, observed));
            }

            public bool TryGetThrowPreview(out FireballAim aim)
            {
                aim=default;
                if (!Visual || !Visual.activeSelf || !owner.head || owner.PushInputSuppressed || owner.ThrowInputSuppressed ||
                    Time.timeScale<=0 || !double.IsFinite(previewWristStamp)) return false;
                double age=owner.HandNow-previewWristStamp;
                if(!double.IsFinite(age) || age<-.01 || age>Parameters.Throwing.sampleMaxAge ||
                    previewWrist.y<owner.head.position.y-Mathf.Clamp(owner.aimWristDrop,.15f,.65f)) return false;
                return owner.TryResolveAim(ThrowPreviewOrigin,out aim);
            }

            public void Cancel()
            {
                if (!Visual) return;
                ObserveThrow("cancelled", "持球结束或取消", default, default, 0, false);
                if (!Visual) return;
                owner.StopHeldAudio(Visual.transform);
                if (!owner.clearing) owner.PlayAudio(Parameters.Audio, SkillAudioCue.Ended, Visual.transform.position);
                Discard();
            }
        }
    }
}
