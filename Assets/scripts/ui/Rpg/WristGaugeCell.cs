using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>一枚常驻刻格。数值立即更新，显示层从当前进度补色或消散。</summary>
    public sealed class WristGaugeCell : MonoBehaviour
    {
        [SerializeField] Image inlay;
        [SerializeField] Image glint;
        [SerializeField] MeshRenderer insert;
        [SerializeField] Vector3 fillAxis = Vector3.right;
        [SerializeField] Color enamel = Color.white;

        static readonly int Enamel = Shader.PropertyToID("_Enamel");
        static readonly int CellU = Shader.PropertyToID("_CellU");
        static readonly int CellV = Shader.PropertyToID("_CellV");
        static readonly int State = Shader.PropertyToID("_CellState");
        const float GainSeconds = .34f;
        const float LossSeconds = .42f;

        MaterialPropertyBlock properties;
        float startTime, origin, displayed;
        int direction;
        bool visible = true;

        public float Fill { get; private set; }
        public bool IsAnimating => direction != 0;
        public MeshRenderer Insert => insert;

        public void Bind(Image colorInlay, Image highlight)
        {
            inlay = colorInlay;
            glint = highlight;
            SetFill(0, false, 0);
        }

        public void SetFill(float amount, bool animate, float delay)
        {
            if (!float.IsFinite(amount)) return;
            float previous = Fill;
            Fill = Mathf.Clamp01(amount);
            if (!animate || !visible || !isActiveAndEnabled)
            {
                Finish();
                return;
            }
            if (Mathf.Approximately(previous, Fill)) return;

            int nextDirection = Fill > previous ? 1 : -1;
            if (direction != nextDirection)
            {
                origin = displayed;
                startTime = Time.unscaledTime + Mathf.Max(0, delay);
                direction = nextDirection;
            }
            Tick(Time.unscaledTime);
        }

        // The row owns the update loop. Continuous regeneration extends this target without
        // restarting the sweep; a reversal starts from the last visible edge.
        internal void Tick(float now)
        {
            if (direction == 0) return;
            float duration = direction > 0 ? GainSeconds : LossSeconds;
            float phase = Mathf.Clamp01((now - startTime) / duration);
            if (phase >= 1)
            {
                Finish();
                return;
            }
            float eased = Mathf.SmoothStep(0, 1, phase);
            displayed = Mathf.Lerp(origin, Fill, eased);
            float pulse = Mathf.Sin(phase * Mathf.PI);
            Draw(displayed, direction * pulse);
        }

        internal void SetVisible(bool show)
        {
            visible = show;
            Finish();
            if (insert) insert.enabled = show && isActiveAndEnabled;
        }

        void Finish()
        {
            direction = 0;
            displayed = Fill;
            Draw(Fill, 0);
        }

        static Vector4 Coordinate(Vector3 axis, Bounds bounds)
        {
            axis.Normalize();
            Vector3 size = bounds.size;
            float span = Mathf.Abs(axis.x) * size.x + Mathf.Abs(axis.y) * size.y + Mathf.Abs(axis.z) * size.z;
            axis /= Mathf.Max(.000001f, span);
            return new Vector4(axis.x, axis.y, axis.z, .5f - Vector3.Dot(axis, bounds.center));
        }

        void Draw(float edge, float signedPulse)
        {
            if (insert)
            {
                if (properties == null)
                {
                    properties = new MaterialPropertyBlock();
                    insert.GetPropertyBlock(properties);
                    var bounds = insert.localBounds;
                    Vector3 right = fillAxis.normalized;
                    // The broad face is the longest transverse mesh axis; the thin bevel is the other.
                    Vector3 up = Vector3.ProjectOnPlane(Vector3.up, right) * bounds.size.y;
                    Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, right) * bounds.size.z;
                    Vector3 side = Vector3.ProjectOnPlane(Vector3.right, right) * bounds.size.x;
                    Vector3 across = up.sqrMagnitude > forward.sqrMagnitude ? up : forward;
                    if (side.sqrMagnitude > across.sqrMagnitude) across = side;
                    properties.SetVector(CellU, Coordinate(right, bounds));
                    properties.SetVector(CellV, Coordinate(across, bounds));
                    properties.SetColor(Enamel, enamel);
                }
                properties.SetVector(State, new Vector4(edge, Fill, signedPulse, 0));
                insert.SetPropertyBlock(properties);
            }

            if (inlay) inlay.rectTransform.anchorMax = new Vector2(edge, 1);
            if (!glint) return;
            glint.enabled = Mathf.Abs(signedPulse) > .01f;
            if (!glint.enabled) return;
            float halfWidth = signedPulse > 0 ? .12f : .19f;
            glint.rectTransform.anchorMin = new Vector2(Mathf.Max(0, edge - halfWidth), 0);
            glint.rectTransform.anchorMax = new Vector2(Mathf.Min(1, edge + halfWidth), 1);
            glint.color = signedPulse > 0 ? new Color(1, .9f, .66f) : new Color(.93f, .48f, .25f);
            glint.canvasRenderer.SetAlpha(Mathf.Abs(signedPulse) * .75f);
        }

        void OnDisable()
        {
            Finish();
            if (insert) insert.enabled = false;
        }

        void OnEnable()
        {
            if (insert) insert.enabled = visible;
            Finish();
        }
    }
}
