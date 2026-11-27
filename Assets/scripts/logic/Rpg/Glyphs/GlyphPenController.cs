using System;
using NarutoVR.SealCombat.Items;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>从工具带取笔、占用玩家输入并驱动两种画板。效果的可变状态只属于本次匹配。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(LinearRpgController))]
    public sealed class GlyphPenController : MonoBehaviour, IHandPickupSource
    {
        [InspectorName("主动面板模板")] public GlyphTemplate[] activeTemplates = Array.Empty<GlyphTemplate>();
        [InspectorName("主动面板，可留空使用灰盒")] public GlyphBoard activeBoard;
        [InspectorName("笔杆根节点")] public Transform penRoot;
        [InspectorName("笔尖采样点，本地 +Z 朝前")] public Transform nib;
        [InspectorName("上腹笔套抓取点，可绑定角色骨骼")] public Transform holster;
        [InspectorName("笔套相对眼位（右、上、前）")] public Vector3 beltOffset = new(.13f, -.39f, .16f);
        [InspectorName("取笔距离（米）"), Min(.01f)] public float pickupRadius = .095f;
        [InspectorName("放下持笔手：低于头部（米）"), Min(.15f)] public float lowerBelowHead = .54f;
        [InspectorName("失追退出间隔（秒）"), Min(.15f)] public float trackingTimeout = 1.2f;
        [InspectorName("已牵引：收笔低于头部（米）"), Min(.15f)] public float tetherReleaseBelowHead = .65f;
        [InspectorName("已牵引：低手收笔保持（秒）"), Min(.05f)] public float tetherReleaseHold = .45f;
        [InspectorName("已牵引：完全丢手后收笔（秒）"), Min(.15f)] public float tetherTrackingTimeout = 6f;
        [InspectorName("主动板相对眼位（右、上、前）")] public Vector3 boardOffset = new(0, -.18f, .42f);
        [InspectorName("主动板停笔提交间隔（秒）"), Min(.1f)] public float idleSeconds = .9f;

        public bool IsHeld => claim != 0 || beltSession;
        public bool BlocksCombat => IsHeld || releasedFrame == Time.frameCount;
        internal bool CanYieldDrawingToDodge => player.locomotion.UsesEncounterModes && drawing && drawing.HasUnsubmittedInk &&
            effect == null && OwnsPen;
        public string Status { get; private set; } = "轻捏上腹右侧的墨规笔";
        LinearRpgController player;
        GlyphBoard drawing;
        GlyphBoard contactBoard;
        Vector3 previousRawTip;
        bool hasPreviousTip, readyFromFront, touchingSurface;
        GlyphStoryPanel story;
        GlyphTemplate matchedTemplate;
        bool AllowsEmptyHand => matchedTemplate && matchedTemplate.effect && matchedTemplate.effect.AllowsEmptyHand;
        GlyphEffectInstance effect;
        GlyphEffectContext context;
        Transform sleeve, actorRoot;
        Transform observedNib;
        GlyphNibDragVisual nibDrag;
        Material previewMaterial;
        long claim;
        ToolBeltInteraction carriedBelt;
        GameItemBase carriedPen;
        bool beltSession, heldInLeftHand, pendingPickup, waitingForLift;
        bool OwnsPen => claim != 0 ? player.Input.OwnsInteraction(this, claim) :
            beltSession && carriedBelt && carriedBelt.OwnsHeldItem(carriedPen);

        internal void BindBeltPen(ToolBeltInteraction belt, GameItemBase item)
        {
            if (!belt || !item || item.Kind != GameItemKind.GlyphPen || !item.grip || !item.insertionTip)
                throw new ArgumentException("工具带墨规笔须有有效握点与笔尖");
            Finish();
            if (carriedBelt && carriedBelt != belt) carriedBelt.DrivenByGlyph = false;
            carriedBelt = belt;
            carriedPen = item;
            penRoot = item.transform;
            nib = item.insertionTip;
            if (sleeve) sleeve.gameObject.SetActive(false);
            carriedBelt.DrivenByGlyph = isActiveAndEnabled;
        }
        bool pinchArmed;
        int releasedFrame = -1;
        double lastTracked;
        double lowSince = double.NaN;
        Vector3 gripFromWrist;
        bool usingWrist;
        Quaternion bodyYaw = Quaternion.identity;

        void OnEnable()
        {
            player = GetComponent<LinearRpgController>();
            player.GlyphPen = this;
            HandPickupQuery.Register(this);
            if (carriedBelt) carriedBelt.DrivenByGlyph = true;
            if (player.locomotion) player.locomotion.MovementStarting += OnMovementStarting;
        }

        bool CanTakeStandalone => isActiveAndEnabled && !carriedBelt && !IsHeld && player && player.CanAct &&
            penRoot && penRoot.gameObject.activeInHierarchy && !player.skills.effects.HasOccupiedHand &&
            !player.locomotion.EncounterCombat && !player.locomotion.NavigationActive && !player.locomotion.EncounterSlideActive;

        public void CollectPickups(HandPickupQuery query)
        {
            if (query.Player == player && CanTakeStandalone)
                query.Offer(this, penRoot, penRoot.position, pickupRadius);
        }

        public bool TryPickup(Transform target, bool left, PlayerHandObservation hand)
        {
            if (target != penRoot || !CanTakeStandalone || !hand.gripPoseValid ||
                !player.Input.TryAcquireInteraction(this, out claim)) return false;
            heldInLeftHand = left;
            pendingPickup = true;
            lastTracked = hand.rightObserved;
            waitingForLift = hand.headPosition.y - hand.rightWrist.y >= lowerBelowHead;
            player.skills.InvalidateEarlierVoice();
            player.skills.effects.ResetPushInput();
            Status = "已取出墨规笔";
            return true;
        }

        void OnMovementStarting()
        {
            if (!AllowsEmptyHand && effect?.IsTethered != true) Finish();
        }

        void EnsureTools()
        {
            if (actorRoot) return;
            actorRoot = new GameObject("墨规笔工具").transform;
            actorRoot.SetParent(transform, false);
            if (!penRoot)
            {
                penRoot = new GameObject("墨规笔").transform;
                penRoot.SetParent(actorRoot, false);
            }
            if (!nib)
            {
                Stick(penRoot, "玄铜笔杆", .009f, .115f, .025f, new Color(.39f,.23f,.1f));
                Stick(penRoot, "朱砂笔锋", .005f, .026f, .095f, new Color(.86f,.15f,.07f));
                nib = new GameObject("笔尖采样点").transform;
                nib.SetParent(penRoot, false);
                nib.localPosition = Vector3.forward * .108f;
            }
            if (!holster && !carriedBelt)
            {
                sleeve = new GameObject("上腹右侧笔套").transform;
                sleeve.SetParent(actorRoot, false);
                Stick(sleeve, "护锋套", .016f, .09f, .052f, new Color(.10f,.16f,.19f));
            }
        }

        void Stick(Transform parent, string label, float radius, float length, float center, Color color)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = label;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = Vector3.forward * center;
            obj.transform.localRotation = Quaternion.Euler(90,0,0);
            obj.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);
            var col = obj.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);
            var renderer = obj.GetComponent<Renderer>();
            if (!previewMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader) previewMaterial = new Material(shader);
            }
            if (previewMaterial) renderer.sharedMaterial = previewMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor",color);
            block.SetColor("_Color",color);
            renderer.SetPropertyBlock(block);
        }

        // Called before wrist and locomotion arbitration; matching runs after movement.
        internal void Observe(PlayerHandObservation hand, double now)
        {
            if (carriedBelt) carriedBelt.StepFromGlyph(hand, now);
            if (carriedBelt && carriedBelt.OwnsHeldItem(carriedPen))
                heldInLeftHand = carriedBelt.HeldInLeftHand;
            if (heldInLeftHand) hand = player.Input.LeftPickupHand;
            bool inCombat = player.locomotion.EncounterCombat;
            if (!isActiveAndEnabled || !player.CanAct || inCombat && !CanYieldDrawingToDodge)
            {
                Finish();
                return;
            }
            EnsureTools();
            Vector3 heading = Vector3.ProjectOnPlane(hand.headForward, Vector3.up);
            bool bodyTracked = PlayerHandObservation.Finite(hand.headPosition) &&
                PlayerHandObservation.Finite(hand.headForward) && hand.headForward.sqrMagnitude > .01f;
            if (bodyTracked && heading.sqrMagnitude > .01f) bodyYaw = Quaternion.LookRotation(heading);
            if (sleeve && bodyTracked)
                sleeve.SetPositionAndRotation(hand.headPosition + bodyYaw * beltOffset, bodyYaw * Quaternion.Euler(52,-12,0));
            var rest = holster ? holster : sleeve;
            bool startedBeltHold = false;
            if (carriedBelt && claim == 0)
            {
                if (!carriedBelt.OwnsHeldItem(carriedPen))
                {
                    if (!beltSession || !AllowsEmptyHand || carriedBelt.Held ||
                        !hand.pinchValid || hand.pinchTransition || !hand.PinchUp(.045f) ||
                        !PlayerHandObservation.Fresh(hand.rightValid, hand.rightObserved, now, player.handSampleMaxAge) ||
                        !player.Input.TryAcquireInteraction(this, out claim)) { Finish(); return; }
                    beltSession = false;
                }
                else if (!beltSession)
                {
                    beltSession = startedBeltHold = true;
                    lastTracked = hand.rightObserved;
                    player.skills.effects.ResetPushInput();
                    Status = "已取出墨规笔";
                }
            }
            else if (!IsHeld) penRoot.SetPositionAndRotation(rest.position, rest.rotation);
            if (IsHeld && !OwnsPen) { Finish(); return; }
            bool tethered = effect?.IsTethered == true;
            bool wristTracked = PlayerHandObservation.Fresh(hand.rightValid,hand.rightObserved,now,player.handSampleMaxAge) &&
                PlayerHandObservation.Finite(hand.rightWrist);
            bool wristOnly = tethered && wristTracked && !hand.gripPoseValid;
            bool tracked = wristTracked && (hand.gripPoseValid || wristOnly);
            if (IsHeld && !AllowsEmptyHand && (tethered || !tracked) && now - lastTracked >= (tethered ? tetherTrackingTimeout : trackingTimeout))
            {
                Finish();
                return;
            }
            if (!tracked)
            {
                contactBoard = null;
                touchingSurface = false;
                pinchArmed = false;
                lowSince = double.NaN;
                if (tethered && bodyTracked && context.bodyTracked)
                {
                    // 手暂时被遮住，笔模型与牵引线起点一起跟随脚步平移。
                    context.hand += Vector3.ProjectOnPlane(hand.headPosition - context.body, Vector3.up);
                    if (!carriedBelt) penRoot.position = context.hand;
                }
                context.tracked = false;
                context.bodyTracked = bodyTracked;
                context.body = hand.headPosition;
                context.observed = now;
                context.rebaseHand = false;
                return;
            }
            if (hand.PinchUp(.040f)) pinchArmed = true;
            bool pressed = pinchArmed && hand.PinchDown(.027f);
            if (pressed) pinchArmed = false;
            if (!IsHeld)
            {
                player.Input.Pickups.EvaluateDirectRight(hand, now);
                if (!IsHeld) return;
            }
            bool startedPickup = pendingPickup;
            pendingPickup = false;
            if (bodyTracked && hand.headPosition.y - hand.rightWrist.y < lowerBelowHead) waitingForLift = false;
            if (!waitingForLift && !carriedBelt && !AllowsEmptyHand && bodyTracked &&
                ShouldLowerPen(hand.headPosition.y - hand.rightWrist.y, hand.rightObserved, tethered))
            {
                Finish();
                return;
            }
            if (!bodyTracked) lowSince = double.NaN;
            lastTracked = hand.rightObserved;
            if (!wristOnly) gripFromWrist = hand.gripPosition - hand.rightWrist;
            Vector3 grip = wristOnly ? hand.rightWrist + gripFromWrist : hand.gripPosition;
            if (!carriedBelt) penRoot.SetPositionAndRotation(grip, wristOnly ? penRoot.rotation : hand.gripRotation);
            context = new GlyphEffectContext {
                owner = player.locomotion.rigRoot, pen = penRoot, holdingBeltPen = beltSession, tracked = true, hand = grip, tip = nib.position,
                bodyTracked = bodyTracked, rebaseHand = wristOnly != usingWrist,
                wrist = hand.rightWrist, gripRotation = wristOnly ? penRoot.rotation : hand.gripRotation,
                rightPointing = hand.rightPointing, rightOpen = hand.rightOpen, rightFist = hand.rightClosed,
                indexRayValid = hand.rightIndexRayValid, indexRay = new Ray(hand.rightTip, hand.rightIndexDirection),
                // The tracked head translates with the player even inside a stationary XR origin.
                forward = nib.forward, body = hand.headPosition, observed = hand.rightObserved
            };
            usingWrist = wristOnly;

            if (effect != null) return;
            if (inCombat)
            {
                // Keep the draft while the right wrist is being evaluated for a combat push.
                contactBoard = null;
                touchingSurface = false;
                return;
            }
            SelectBoard(hand, startedPickup || pressed || startedBeltHold);
            if (drawing)
            {
                ConstrainToBoard(nib.position);
                context.tip = nib.position;
            }
            else contactBoard = null;
        }

        bool ShouldLowerPen(float depth, double observed, bool tethered)
        {
            if (!tethered) return depth >= lowerBelowHead;
            if (depth < tetherReleaseBelowHead)
            {
                lowSince = double.NaN;
                return false;
            }
            if (double.IsNaN(lowSince) || observed < lastTracked || observed - lastTracked > player.handSampleMaxAge)
                lowSince = observed;
            return observed - lowSince >= tetherReleaseHold;
        }

        void SelectBoard(PlayerHandObservation hand, bool pressed)
        {
            if ((story && !story.IsWaiting) || (drawing && !drawing.IsOpen))
            {
                drawing = null;
                story = null;
                matchedTemplate = null;
            }
            if (drawing && (drawing != activeBoard || drawing.PointCount > 0)) return;
            var nearby = GlyphStoryPanel.FindNear(nib.position, .8f);
            // After pressing a story board's button, the held nib starts back at the belt.
            if (!nearby) nearby = GlyphStoryPanel.FindNear(hand.headPosition, 1.2f);
            if (drawing && !nearby) return;
            if (drawing) drawing.Close();
            contactBoard = null; // 同一个板对象重新展开也需要新的一次正面接近。
            story = nearby;
            if (story)
            {
                drawing = story.board;
                Status = "照剧情画板给出的图样绘制";
            }
            else if (pressed && activeTemplates != null && activeTemplates.Length > 0)
            {
                if (!activeBoard)
                {
                    var panel = new GameObject("主动符板");
                    panel.transform.SetParent(actorRoot,false);
                    activeBoard = panel.AddComponent<GlyphBoard>();
                }
                activeBoard.transform.SetPositionAndRotation(hand.headPosition + bodyYaw * boardOffset,bodyYaw);
                activeBoard.idleSeconds = idleSeconds;
                activeBoard.Open(activeTemplates,false);
                drawing = activeBoard;
                Status = "笔尖接触板面绘制，停笔后匹配";
            }
        }

        // 板面局部 -Z 面向书写者。用原始笔尖的轨迹确认它从正面进入矩形，
        // 接触后只修正笔模型的位置，原始手追握点和朝向始终保留。
        void ConstrainToBoard(Vector3 rawWorldTip)
        {
            if (drawing != contactBoard)
            {
                contactBoard = drawing;
                hasPreviousTip = readyFromFront = touchingSurface = false;
            }
            Vector3 local = drawing.transform.InverseTransformPoint(rawWorldTip);
            float contact = drawing.contactDistance;
            bool inside = drawing.ContainsSurface(local);
            if (!inside || local.z < -Mathf.Max(contact, drawing.releaseDistance))
                touchingSurface = false;

            if (!touchingSurface && inside && readyFromFront && hasPreviousTip)
            {
                // 低帧率下手部一帧跨过板面时，检测线段真正穿过平面的位置。
                float entryZ = local.z >= 0 ? 0 : -contact;
                if (previousRawTip.z < entryZ && local.z >= entryZ)
                {
                    float t = (entryZ - previousRawTip.z) / (local.z - previousRawTip.z);
                    touchingSurface = drawing.ContainsSurface(Vector3.Lerp(previousRawTip, local, t));
                }
                else if (previousRawTip.z < 0 && local.z >= -contact && local.z > previousRawTip.z &&
                         drawing.ContainsSurface(previousRawTip))
                    touchingSurface = true; // 初次观测已在正面容差内，继续向前轻触即可落笔。
            }
            if (!touchingSurface)
            {
                if (local.z < -contact || (!hasPreviousTip && inside && local.z < 0)) readyFromFront = true;
                else if (!inside || local.z >= 0) readyFromFront = false;
            }
#if UNITY_EDITOR
            drawing.contactLog.Observe(local.z, inside, touchingSurface, Time.realtimeSinceStartupAsDouble);
#endif
            if (touchingSurface)
            {
                var onPlane = drawing.transform.TransformPoint(new Vector3(local.x, local.y, 0));
                penRoot.position += onPlane - rawWorldTip;
            }
            previousRawTip = local;
            hasPreviousTip = true;
        }

        internal void Advance(double now)
        {
            if (!IsHeld) return;
            if (!OwnsPen || !player.CanAct) { Finish(); return; }
            ObserveNibVisual(now);
            if (effect != null)
            {
                effect.Tick(context,Time.unscaledDeltaTime);
                Status = effect.Status;
                if (effect.IsComplete) Finish();
                return;
            }
            if (!drawing) return;
            if (player.locomotion.EncounterCombat && drawing.HasUnsubmittedInk)
            {
                // Suspend both ink and the idle-submit clock until exploration resumes.
                drawing.Sample(default, false, now);
                return;
            }
            if (story && story.IsResolving)
            {
                if (drawing.AdvanceStorySuccess(now))
                {
                    var resolved = story;
                    drawing = null;
                    story = null;
                    if (matchedTemplate && matchedTemplate.effect) effect = matchedTemplate.effect.Begin(context);
                    resolved.Complete();
                }
                return;
            }
            drawing.Sample(context.tip,context.tracked,now,touchingSurface);
            if (!context.tracked || !drawing.TrySubmit(now,out var match)) return;
            if (!match) { Status = drawing.Status; return; }

            matchedTemplate = match;
            Status = "已识别：" + match.Title;
            if (story)
            {
                drawing.BeginStorySuccess(now, match);
                return;
            }
            var completedBoard = drawing;
            completedBoard.Close();
            drawing = null;
            if (match.effect) effect = match.effect.Begin(context);
            match.RaiseCompleted(completedBoard);
        }

        void ObserveNibVisual(double now)
        {
            if (observedNib != nib)
            {
                if (nibDrag) nibDrag.ResetDrag();
                observedNib = nib;
                nibDrag = observedNib ? observedNib.GetComponentInParent<GlyphNibDragVisual>() : null;
            }
            if (!nibDrag) return;
            bool writing = touchingSurface && drawing && drawing.IsOpen && !drawing.IsShowingSuccess &&
                effect == null && !player.locomotion.EncounterCombat;
            nibDrag.Observe(drawing ? drawing.transform : null, context.tip, context.tracked, writing, now);
        }

        void FixedUpdate()
        {
            if (IsHeld && player && player.CanAct && OwnsPen)
                effect?.FixedTick(Time.fixedDeltaTime);
        }

        public void Finish()
        {
            if (!IsHeld) return;
            if (nibDrag) nibDrag.ResetDrag();
            var held = claim;
            claim = 0;
            beltSession = false;
            heldInLeftHand = pendingPickup = waitingForLift = false;
            releasedFrame = Time.frameCount;
            pinchArmed = false;
            lowSince = double.NaN;
            usingWrist = false;
            context = default;
            contactBoard = null;
            touchingSurface = false;
            var oldEffect = effect;
            effect = null;
            try
            {
                oldEffect?.End();
                if (story && story.IsWaiting) story.ResetDrawing();
                else if (drawing) drawing.Close();
            }
            finally
            {
                story = null;
                matchedTemplate = null;
                drawing = null;
                if (player)
                {
                    player.Input.ReleaseInteraction(this, held);
                    if (player.skills)
                    {
                        player.skills.InvalidateEarlierVoice();
                        if (player.skills.effects) player.skills.effects.ResetPushInput();
                    }
                }
                var rest = holster ? holster : sleeve;
                if (!carriedBelt && penRoot && rest) penRoot.SetPositionAndRotation(rest.position, rest.rotation);
                Status = "轻捏上腹右侧的墨规笔";
            }
        }

        void OnApplicationFocus(bool focused) { if (!focused) { pinchArmed = false; Finish(); } }
        void OnDisable()
        {
            HandPickupQuery.Unregister(this);
            Finish();
            pinchArmed = false;
            if (carriedBelt) carriedBelt.DrivenByGlyph = false;
            if (player && player.locomotion) player.locomotion.MovementStarting -= OnMovementStarting;
            if (player && player.GlyphPen == this) player.GlyphPen = null;
        }
        void OnDestroy()
        {
            if (actorRoot) Destroy(actorRoot.gameObject);
            if (previewMaterial) Destroy(previewMaterial);
        }
    }
}
