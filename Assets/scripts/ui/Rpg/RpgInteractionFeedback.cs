using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Read-only presentation of movement intent and settled actions; never moves the rig or spends resources.</summary>
    [DefaultExecutionOrder(600)]
    public sealed class RpgInteractionFeedback : MonoBehaviour
    {
        [Header("一、交互来源")]
        [InspectorName("线性关卡控制器")] public LinearRpgController controller;
        [Header("二、路径与落点提示")]
        [InspectorName("启用地面特效")] public bool showVisuals = true;
        [InspectorName("路径与当前位置颜色")] public Color pathColor = new(.05f, .85f, 1f);
        [InspectorName("悬停进度颜色")] public Color progressColor = new(1f, .75f, .12f);
        [InspectorName("落点圈半径（米）"), Min(.1f)] public float landingRadius = .32f;
        [InspectorName("地面提示高度（米）"), Range(.01f,.15f)] public float floorLift = .055f;
        [Header("二（补充）、手边投掷提示")]
        [InspectorName("显示握持进度圈")] public bool showThrowGrip = true;
        [InspectorName("显示火球瞄准线与准星"), Tooltip("抬起持球手显示真实弹道。转头选择目标；遇到墙体时，准星停在火球首先碰到的位置。")]
        public bool showThrowDirection = true;
        [InspectorName("握持圈半径（米）"), Range(.03f,.15f)] public float throwRingRadius = .085f;
        [InspectorName("调试轨迹对照线长度（米）"), Range(.1f,.6f)] public float throwDirectionLength = .45f;
        [InspectorName("握持确认颜色")] public Color gripReadyColor = new(.15f,1f,.6f);
        [InspectorName("松手确认颜色")] public Color releaseColor = new(1f,.5f,.1f);
        [Header("三、操作提示音（可换成正式音效）")]
        [InspectorName("启用提示音")] public bool playSounds = true;
        [InspectorName("提示音量"), Range(0,1)] public float volume = .18f;
        [InspectorName("选中或准备音效")] public AudioClip selectionClip;
        [InspectorName("移动成功音效")] public AudioClip movementClip;
        [InspectorName("技能成功音效")] public AudioClip skillClip;
        [InspectorName("取消音效")] public AudioClip cancelClip;
        [InspectorName("拒绝或受击音效")] public AudioClip blockedClip;
        [InspectorName("同类提示最小间隔（秒）"), Min(.05f)] public float minimumCueInterval = .16f;
        [InspectorName("启用握住确认音")] public bool playGripCue = true;
        [InspectorName("启用驻留即将完成音")] public bool playDwellCue = true;
        [InspectorName("驻留提示进度"), Range(.5f,.95f)] public float dwellCueThreshold = .75f;
        [InspectorName("启用投掷拒绝音")] public bool playThrowRejectedCue = true;

        public string LastFeedback { get; private set; } = "左食指伸直后移动选方向，保持至金圈填满；右掌独立短划闪避";
        public bool PathVisible => openingMarks?.HasFocus == true || (path && path.enabled);
        public float VisibleDwellProgress { get; private set; }
        public int CueCount { get; private set; }
        public int SoundCount { get; private set; }
        public Vector3 DisplayedTarget { get; private set; }
        public bool GripVisualVisible => throwGrip && throwGrip.enabled;
        [InspectorName("调试：显示前送原始轨迹对照线")] public bool showRawWindupDirection;
        public bool ThrowDirectionVisible => throwDirection && throwDirection.enabled;
        public int GripCueCount { get; private set; }
        public int DwellCueCount { get; private set; }
        public int ThrowRejectedCueCount { get; private set; }

        RpgLocomotionController movement;
        OpeningNavigationMarks openingMarks;
        SkillEffects effects;
        LineRenderer path, landing, progress, feet, pulse, flow, throwGrip, throwDirection, aimReticle, rawWindupLine;
        Material pathMaterial, progressMaterial, pulseMaterial, throwMaterial;
        Transform visuals;
        AudioSource speaker;
        readonly AudioClip[] generated = new AudioClip[5];
        readonly double[] lastCue = new double[Enum.GetValues(typeof(RpgActionCue)).Length];
        bool wasNavigating, wasProtected, wasWarning;
        bool wasThrowArmed, wasReleasePending, dwellCueSent;
        string lastThrowRejection;
        NavigationNode dwellCueTarget;
        float previousDwell;
        int launches, hits;
        double pulseUntil;
        Vector3 pulseCenter;
        RpgTrainingHazard hazard;

        void OnEnable()
        {
            if (!controller) return;
            movement = controller.locomotion; effects = controller.skills ? controller.skills.effects : null;
            if (!movement) return;
            movement.Feedback += Present;
            controller.Feedback += Present;
            launches = effects ? effects.LaunchCount : 0; hits = effects ? effects.HitCount : 0;
            hazard = controller.GetComponent<RpgTrainingHazard>();
            for (int i = 0; i < lastCue.Length; i++) lastCue[i] = double.NegativeInfinity;
            EnsurePresentation();
            wasNavigating = wasProtected = wasWarning = false;
            wasThrowArmed = effects && effects.ThrowArmed;
            wasReleasePending = effects && effects.ReleasePending;
            lastThrowRejection = null; dwellCueTarget = null; dwellCueSent = false; previousDwell = 0;
        }

        void EnsurePresentation()
        {
            if (!speaker)
            {
                speaker = gameObject.AddComponent<AudioSource>(); speaker.playOnAwake = false;
                speaker.spatialBlend = 0; speaker.loop = false;
                // Short nonverbal signals, generated once; no microphone access and no keyword playback.
                generated[0] = Tone("选向", 620, 920, .09f);
                generated[1] = Tone("移步", 260, 780, .16f);
                generated[2] = Tone("施法", 480, 1200, .2f);
                generated[3] = Tone("取消", 600, 220, .14f);
                generated[4] = Tone("不可用", 180, 130, .1f);
            }
            if (visuals) { visuals.gameObject.SetActive(true); return; }
            visuals = new GameObject("交互提示_运行时").transform;
            visuals.SetParent(transform, false);
            pathMaterial = MakeMaterial(pathColor); progressMaterial = MakeMaterial(progressColor);
            pulseMaterial = MakeMaterial(pathColor);
            throwMaterial = MakeMaterial(pathColor);
            path = Line("选点路径", pathMaterial, .018f);
            landing = Line("目标落点", pathMaterial, .025f);
            progress = Line("悬停进度", progressMaterial, .045f);
            feet = Line("玩家当前位置", pathMaterial, .014f);
            pulse = Line("操作反馈扩散", pulseMaterial, .04f);
            flow = Line("路径流光", progressMaterial, .045f);
            throwGrip = Line("手边握持进度", throwMaterial, .007f);
            throwDirection = Line("火球瞄准线", throwMaterial, .009f);
            aimReticle = Line("火球落点准星", throwMaterial, .009f);
            rawWindupLine = Line("前送原轨迹（细线）", throwMaterial, .003f);
            if (controller.openingChapter)
                openingMarks = new OpeningNavigationMarks(visuals,movement,
                    MakeMaterial(new Color(.27f,.24f,.19f)),MakeMaterial(new Color(.78f,.75f,.63f)),
                    MakeMaterial(new Color(1f,.67f,.30f)));
        }

        void LateUpdate()
        {
            if (!controller || !movement || !visuals) return;
            bool active = controller.CanAct;
            bool chapterWalking = controller.openingChapter && movement.UsesEncounterModes && !movement.EncounterCombat;
            if (active && !chapterWalking && movement.NavigationActive && !wasNavigating)
                Present(RpgActionCue.NavigationReady);
            if (active && movement.SealProtectionActive && !wasProtected)
                LastFeedback = "结印保护中：分开双手并稳定后，重新张掌划动";
            wasNavigating = movement.NavigationActive; wasProtected = movement.SealProtectionActive;
            if (effects)
            {
                UpdateThrowCues(active);
                if (active && effects.LaunchCount > launches) Present(RpgActionCue.Launched);
                if (active && effects.HitCount > hits) Present(RpgActionCue.Hit);
                launches = effects.LaunchCount; hits = effects.HitCount;
            }
            UpdateDwellCue(active);
            bool warning = active && hazard && hazard.WarningActive;
            if (warning && !wasWarning) { Present(RpgActionCue.Blocked); LastFeedback = "攻击预警：离开地面红圈，或准备护盾"; }
            wasWarning = warning;
            RefreshVisuals(active);
        }

        void UpdateThrowCues(bool active)
        {
            bool armed = effects.HasHeldObject && effects.ThrowArmed;
            bool pending = effects.HasHeldObject && effects.ReleasePending;
            if (active && effects.ThrowInputValid && armed && !wasThrowArmed && playGripCue)
                Present(RpgActionCue.ThrowGripReady);
            if (active && wasReleasePending && !pending && effects.HasHeldObject && !armed &&
                effects.LaunchCount == launches && effects.HasThrowObservation && playThrowRejectedCue)
            {
                var observation = effects.LastThrowObservation;
                bool regripped = (observation.grip.fistValid && observation.grip.fistClosed) ||
                    (observation.grip.pinchValid && observation.grip.pinchClosed);
                if (!regripped && !string.IsNullOrEmpty(observation.status) && observation.status != lastThrowRejection)
                {
                    lastThrowRejection = observation.status;
                    Present(RpgActionCue.ThrowRejected);
                    LastFeedback = "未发射：" + observation.status;
                }
            }
            if (!effects.HasHeldObject) lastThrowRejection = null;
            wasThrowArmed = armed; wasReleasePending = pending;
        }

        void UpdateDwellCue(bool active)
        {
            var target = movement.CandidateNode;
            float dwell = Mathf.Clamp01(movement.DwellProgress);
            if (!active || !movement.NavigationActive || !target || target != dwellCueTarget || dwell + .001f < previousDwell)
                dwellCueSent = false;
            if (active && movement.NavigationActive && target && !dwellCueSent && dwell >= dwellCueThreshold)
            {
                dwellCueSent = true;
                if (playDwellCue) Present(RpgActionCue.DwellNearComplete);
            }
            dwellCueTarget = target; previousDwell = dwell;
        }

        void RefreshVisuals(bool active)
        {
            bool visible = showVisuals && active;
            if (openingMarks != null)
            {
                openingMarks.Refresh(visible && movement.EncounterCombat && !movement.EncounterSlideActive &&
                    !movement.SealProtectionActive && !controller.UiInputOwned);
                VisibleDwellProgress = openingMarks.HasFocus ? Mathf.Clamp01(movement.DwellProgress) : 0;
                if (openingMarks.HasFocus) DisplayedTarget = movement.CandidateNode.floorPosition;
                RefreshPulse(visible);
                RefreshThrowVisuals(active);
                return;
            }
            Vector3 origin = movement.PlayerFloorPosition + Vector3.up * floorLift;
            feet.enabled = visible;
            if (visible) Ring(feet, origin, .22f, 1);
            bool candidate = visible && movement.NavigationActive && movement.CandidateNode;
            path.enabled = landing.enabled = progress.enabled = flow.enabled = candidate;
            VisibleDwellProgress = candidate ? Mathf.Clamp01(movement.DwellProgress) : 0;
            if (candidate)
            {
                DisplayedTarget = movement.CandidateNode.floorPosition;
                Vector3 destination = DisplayedTarget + Vector3.up * floorLift;
                path.positionCount = 25;
                for (int i = 0; i < path.positionCount; i++) path.SetPosition(i, PathPoint(origin, destination, i / 24f));
                Ring(landing, destination, landingRadius, 1);
                Ring(progress, destination + Vector3.up * .012f, landingRadius + .055f, VisibleDwellProgress);
                progress.enabled = VisibleDwellProgress > .001f;
                float phase = Mathf.Repeat(Time.unscaledTime * .7f, 1);
                flow.positionCount = 2;
                flow.SetPosition(0, PathPoint(origin, destination, phase));
                flow.SetPosition(1, PathPoint(origin, destination, Mathf.Min(1, phase + .075f)));
            }
            RefreshPulse(visible);
            RefreshThrowVisuals(active);
        }

        void RefreshPulse(bool visible)
        {
            pulse.enabled = visible && Time.realtimeSinceStartupAsDouble < pulseUntil;
            if (pulse.enabled)
            {
                float elapsed = 1 - (float)((pulseUntil - Time.realtimeSinceStartupAsDouble) / .45);
                Ring(pulse, pulseCenter, .2f + elapsed * .45f, 1);
                pulse.widthMultiplier = .045f * (1 - elapsed);
            }
        }

        void RefreshThrowVisuals(bool active)
        {
            bool valid = active && effects && effects.HasHeldObject && effects.ThrowInputValid;
            throwGrip.enabled = valid && showThrowGrip;
            FireballAim aim=default;
            throwDirection.enabled = aimReticle.enabled = active && showThrowDirection &&
                effects && effects.TryGetThrowPreview(out aim);
            rawWindupLine.enabled = throwDirection.enabled && showRawWindupDirection && effects.WindupPreview;
            if(rawWindupLine.enabled){
                var origin=effects.ThrowPreviewOrigin;
                rawWindupLine.positionCount=2;
                rawWindupLine.SetPosition(0,origin);
                rawWindupLine.SetPosition(1,origin+effects.CurrentThrowScore.rawWindupDirection*Mathf.Clamp(throwDirectionLength,.1f,.6f));
            }
            if (!valid && !throwDirection.enabled) return;
            Color color = aim.blocked ? new Color(1f,.32f,.1f) : effects.ReleasePending ? releaseColor : effects.ThrowArmed ? gripReadyColor : pathColor;
            SetColor(throwMaterial, color);
            if (throwGrip.enabled)
            {
                float amount = effects.CurrentThrowScore.holdProgress>0 ? effects.CurrentThrowScore.holdProgress : effects.ReleasePending ? effects.ThrowReleaseProgress : effects.ThrowArmed ? 1 : effects.ThrowGripProgress;
                Vector3 center = effects.HeldThrowPosition;
                if (!RpgGestureState.Finite(center)) { throwGrip.enabled = throwDirection.enabled = false; return; }
                Quaternion face = movement.head ? movement.head.rotation : Quaternion.identity;
                float radius = float.IsFinite(throwRingRadius) ? Mathf.Clamp(throwRingRadius,.03f,.15f) : .085f;
                HandRing(throwGrip, center, face, radius, Mathf.Max(.025f,Mathf.Clamp01(amount)));
            }
            if (throwDirection.enabled)
            {
                throwDirection.positionCount = 2;
                throwDirection.SetPosition(0, aim.origin); throwDirection.SetPosition(1, aim.endpoint);
                float distance=Vector3.Distance(effects.head.position,aim.endpoint);
                float radius=Mathf.Clamp(distance*.008f,.025f,.22f);
                aimReticle.widthMultiplier=Mathf.Clamp(radius*.1f,.003f,.022f);
                HandRing(aimReticle,aim.endpoint,effects.head.rotation,radius,1);
            }
        }

        public void Present(RpgActionCue cue)
        {
            if (!isActiveAndEnabled || !movement || !speaker) return;
            if (cue == RpgActionCue.Damage && controller && controller.Entity && controller.Entity.LastSettledDamage.HealthLost <= 0) return;
            double now = Time.realtimeSinceStartupAsDouble;
            int key = (int)cue;
            if (now - lastCue[key] < minimumCueInterval) return;
            lastCue[key] = now; CueCount++;
            int tone = 0;
            Color color = pathColor;
            switch (cue)
            {
                case RpgActionCue.NavigationReady:
                    LastFeedback = openingMarks != null ? "左食指指向地面尺刻，铜色开口围住当前落点" : "导航已准备：左食指前后左右移动选择落点";
                    break;
                case RpgActionCue.TargetSelected: LastFeedback = "落点已选中：保持方向至金色进度圈填满"; break;
                case RpgActionCue.Moved:
                    LastFeedback = openingMarks != null ? "已到达落点，放松左食指后可再次选择" : "已到达落点：左食指回到起点后可再次选择";
                    tone = 1; break;
                case RpgActionCue.Dodged: LastFeedback = "闪避成功：右手回位后可再次划动（仍需冷却结束）"; tone = 1; break;
                case RpgActionCue.Cancelled: LastFeedback = "已取消待施法与未发射物"; tone = 3; color = new Color(.65f,.45f,1); break;
                case RpgActionCue.Blocked: LastFeedback = "操作未通过：查看移动或最近操作中的原因"; tone = 4; color = new Color(1,.25f,.08f); break;
                case RpgActionCue.Damage: LastFeedback = "受到攻击：检查生命与护盾"; tone = 4; color = Color.red; break;
                case RpgActionCue.Launched: LastFeedback = "火球已发射"; tone = 2; color = progressColor; break;
                case RpgActionCue.Hit: LastFeedback = "命中目标"; tone = 2; color = progressColor; break;
                case RpgActionCue.ThrowGripReady: LastFeedback = "握持已确认：可甩手松开、后拉前送或伸手保持；绿色仅表示已握住"; color = gripReadyColor; GripCueCount++; break;
                case RpgActionCue.ThrowRejected: LastFeedback = "本次未发射：查看投掷拒绝原因，重新握住后再试"; tone = 4; color = releaseColor; ThrowRejectedCueCount++; break;
                case RpgActionCue.DwellNearComplete: LastFeedback = "落点即将确认：保持方向，或左手回中立撤销"; color = progressColor; DwellCueCount++; break;
                default: LastFeedback = cue == RpgActionCue.Progress ? "进度操作成功" : "技能已生效或准备完成"; tone = 2; color = progressColor; break;
            }
            var clip = tone switch { 0 => selectionClip, 1 => movementClip, 2 => skillClip, 3 => cancelClip, _ => blockedClip };
            if (playSounds && volume > 0)
            {
                var sound = clip ? clip : generated[tone];
                if (!controller.TryGetComponent<P1AudioManager>(out var audio) || !audio.TryPlayAction(cue, sound, volume))
                    speaker.PlayOneShot(sound, volume);
                SoundCount++;
            }
            if (openingMarks != null && (cue == RpgActionCue.NavigationReady || cue == RpgActionCue.TargetSelected ||
                cue == RpgActionCue.Moved || cue == RpgActionCue.DwellNearComplete)) return;
            pulseCenter = movement.PlayerFloorPosition + Vector3.up * floorLift;
            pulseUntil = now + .45;
            SetColor(pulseMaterial, color);
        }

        LineRenderer Line(string label, Material material, float width)
        {
            var go = new GameObject(label); go.transform.SetParent(visuals, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.sharedMaterial = material;
            line.widthMultiplier = width; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
            return line;
        }
        static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (!shader) throw new InvalidOperationException("交互提示缺少Unlit着色器");
            var material = new Material(shader); SetColor(material, color); return material;
        }
        static void SetColor(Material material, Color color)
        {
            if (!material) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
        static Vector3 PathPoint(Vector3 from, Vector3 to, float fraction) =>
            Vector3.Lerp(from, to, fraction) + Vector3.up * (Mathf.Sin(fraction * Mathf.PI) * .14f);
        static void Ring(LineRenderer line, Vector3 center, float radius, float fraction)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(64 * fraction));
            line.loop = false; line.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * fraction * Mathf.PI * 2;
                line.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius);
            }
        }
        static void HandRing(LineRenderer line, Vector3 center, Quaternion rotation, float radius, float fraction)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(40 * fraction));
            line.loop = false; line.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * fraction * Mathf.PI * 2;
                line.SetPosition(i, center + rotation * new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0) * radius);
            }
        }
        static AudioClip Tone(string label, float start, float end, float duration)
        {
            const int rate = 22050;
            var data = new float[Mathf.CeilToInt(duration * rate)];
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                phase += Mathf.Lerp(start, end, t) / rate * Math.PI * 2;
                data[i] = (float)Math.Sin(phase) * Mathf.Sin(Mathf.PI * t) * .45f;
            }
            var clip = AudioClip.Create("交互_" + label, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnDisable()
        {
            openingMarks?.Refresh(false);
            if (movement) movement.Feedback -= Present;
            if (controller) controller.Feedback -= Present;
            if (visuals) visuals.gameObject.SetActive(false);
            if (throwGrip) throwGrip.enabled = false;
            if (throwDirection) throwDirection.enabled = false;
            if (aimReticle) aimReticle.enabled = false;
            if (rawWindupLine) rawWindupLine.enabled = false;
            if (speaker) speaker.Stop();
            VisibleDwellProgress = 0;
        }
        void OnDestroy()
        {
            openingMarks?.Dispose();
            if (visuals) Destroy(visuals.gameObject);
            if (speaker) Destroy(speaker);
            foreach (var clip in generated) if (clip) Destroy(clip);
            if (pathMaterial) Destroy(pathMaterial); if (progressMaterial) Destroy(progressMaterial); if (pulseMaterial) Destroy(pulseMaterial);
            if (throwMaterial) Destroy(throwMaterial);
        }
    }
}
