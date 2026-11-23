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

        public void ApplyGlyphImpulse(Vector3 impulse)
        {
            if (!CanManipulate || impulse == Vector3.zero) return;
            if (localAxis.sqrMagnitude > 0.000001f)
                impulse = Vector3.Project(impulse, transform.TransformDirection(localAxis));
            Body.AddForce(impulse, ForceMode.Impulse);
        }
    }
}
