using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // An optional scene component can choose a phase within one objective (carrying an item or riding a lift).
    internal interface IQuestTrailSource { QuestVisualHints CurrentTrail { get; } }

    [DefaultExecutionOrder(365), DisallowMultipleComponent]
    public sealed class CourtyardTaskGuide : MonoBehaviour
    {
        public RpgChapterFlow chapter;
        [InspectorName("探索时显示寻路光点")] public bool showTrail = true;
        public Material guideMaterial;
        [InspectorName("光点离地高度（米）"), Range(.04f, .35f)] public float trailHeight = .17f;
        [InspectorName("卡住时示范手势")] public bool showGestures = true;
        public GameObject rightHandModel;
        [InspectorName("默认捏合手形")] public AnimationClip pinchPose;
        public Material handMaterial;

        QuestTrailDots trail;
        IQuestTrailSource routeSource;
        QuestGestureHand hand;
        QuestVisualHints activeRoute;
        QuestGestureHint activeGesture;
        float routeWait, gestureWait, nextDraw;
        LinearRpgController owner;

        void OnEnable()
        {
            routeSource = GetComponent<IQuestTrailSource>();
            owner = chapter ? chapter.Owner : null;
            if (owner) owner.CheckpointRestored += Clear;
        }

        void LateUpdate()
        {
            var player = chapter ? chapter.Owner : null;
            if (!player || !player.IsReady || !chapter.isActiveAndEnabled || player.chapter != chapter ||
                !player.CanAct || player.UiInputOwned || player.CampaignComplete || !player.locomotion ||
                player.locomotion.EncounterCombat)
            {
                Clear();
                return;
            }

            var currentTrail = routeSource != null ? routeSource.CurrentTrail : chapter.TrailHints;
            UpdateTrail(showTrail ? currentTrail : null, player.locomotion);
            var movement = player.locomotion;
            QuestGestureHint candidate = null;
            if (showGestures && !chapter.Speaking && movement.head && rightHandModel && handMaterial &&
                chapter.GestureHints is { } hints)
            {
                foreach (var hint in hints)
                    if (hint != null && hint.Available(movement.PlayerFloorPosition)) { candidate = hint; break; }
            }
            UpdateHand(candidate, movement.head);
        }

        void UpdateTrail(QuestVisualHints hints, RpgLocomotionController movement)
        {
            if (activeRoute != hints)
            {
                activeRoute = hints;
                routeWait = nextDraw = 0;
                trail?.Hide();
            }
            if (hints == null || !hints.route || !hints.route.isActiveAndEnabled || !guideMaterial)
            {
                routeWait = 0;
                trail?.Hide();
                return;
            }
            routeWait += Time.deltaTime;
            if (routeWait < hints.trailDelay || Time.time < nextDraw) return;
            nextDraw = Time.time + .075f;
            int end = hints.route.EndIndex(hints.destination);
            if (end < 0) { trail?.Hide(); return; }
            trail ??= new QuestTrailDots(transform, guideMaterial);
            trail.Draw(hints.route.points, end, movement, trailHeight, routeSource != null);
        }

        void UpdateHand(QuestGestureHint hint, Transform head)
        {
            if (activeGesture != hint)
            {
                activeGesture = hint;
                gestureWait = 0;
                hand?.Hide();
            }
            if (hint == null) return;
            gestureWait += Time.deltaTime;
            float elapsed = gestureWait - hint.delay;
            float duration = Mathf.Max(.01f, hint.duration);
            float phase = Mathf.Repeat(Mathf.Max(0, elapsed), duration + Mathf.Max(0, hint.repeatInterval));
            if (elapsed < 0 || phase >= duration) { hand?.Hide(); return; }
            hand ??= new QuestGestureHand(transform, rightHandModel, pinchPose, handMaterial);
            hand.Show(hint, head, phase / duration);
        }

        void Clear()
        {
            activeRoute = null;
            activeGesture = null;
            routeWait = gestureWait = nextDraw = 0;
            trail?.Hide();
            hand?.Hide();
        }

        void OnDisable()
        {
            if (owner) owner.CheckpointRestored -= Clear;
            Clear();
        }
        void OnDestroy() { trail?.Dispose(); hand?.Dispose(); }
    }
}
