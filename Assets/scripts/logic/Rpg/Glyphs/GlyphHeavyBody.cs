using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class GlyphHeavyBody : MonoBehaviour
    {
        [InspectorName("允许施力的局部轴"), Tooltip("零向量允许自由推拉；非零向量限制符力方向，保留刚体的重力和碰撞。")]
        public Vector3 localAxis;
        Rigidbody body;
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public bool CanManipulate => isActiveAndEnabled && Body && !Body.isKinematic;
        /// <summary>仅在千钧符确实向刚体施加非零冲量时通知当前道具。</summary>
        public event System.Action<Vector3> GlyphImpulseApplied;

        public void ApplyGlyphImpulse(Vector3 impulse)
        {
            if (!CanManipulate || impulse == Vector3.zero) return;
            if (localAxis.sqrMagnitude > 0.000001f)
                impulse = Vector3.Project(impulse, transform.TransformDirection(localAxis));
            if (impulse == Vector3.zero) return;
            Body.AddForce(impulse, ForceMode.Impulse);
            GlyphImpulseApplied?.Invoke(impulse);
        }
    }
}
