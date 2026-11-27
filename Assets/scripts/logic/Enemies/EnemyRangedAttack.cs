using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyController))]
    public sealed class EnemyRangedAttack : MonoBehaviour
    {
        public Transform upperArm, forearm, muzzle;
        public EnemyFireCore projectile;
        public LineRenderer warningRing;
        [Min(.01f)] public float speed = 6f;
        [Range(0, 1)] public float lockProgress = .55f;
        [SerializeField, InspectorName("战斗走位")] FireThrowerPositioning positioning = new();
        internal FireThrowerPositioning Positioning => positioning;

        public bool IsConfigured => upperArm && forearm && muzzle && projectile && warningRing
            && forearm.IsChildOf(upperArm) && muzzle.IsChildOf(forearm) && float.IsFinite(speed) && speed > 0;
        EnemyController controller;
        string attackId;
        int attackIndex;
        AttackData attack;
        Transform target;
        Vector3 aimPoint;
        float progress;
        bool locked, pending, fired;

        internal void Prepare(string id, int index, AttackData data)
        {
            Cancel();
            controller = GetComponent<EnemyController>();
            target = controller.target;
            if (!IsConfigured || !isActiveAndEnabled || !target) return;
            attackId = id;
            attackIndex = index;
            attack = data;
            warningRing.transform.localScale = new Vector3(data.radius, 1, data.radius);
            TrackTarget();
            warningRing.enabled = true;
        }

        void TrackTarget()
        {
            var floor = CombatDamageReceiver.GetNavigationPosition(target);
            aimPoint = floor + Vector3.up * .06f;
            warningRing.transform.position = floor + Vector3.up * .02f;
        }

        // Keep the last telegraphed point; sampling the target again here would follow the dodge itself.
        internal void LockAim()
        {
            if (attackId != null) locked = true;
        }

        internal void SetProgress(float value, bool release)
        {
            if (attackId == null) return;
            progress = value;
            if (!locked && target)
            {
                TrackTarget();
                locked = release || progress >= lockProgress;
            }
            pending |= release;
        }

        void LateUpdate()
        {
            if (attackId == null || Time.deltaTime <= 0) return;
            if (!target || !controller.CanReceiveHit || controller.target != target)
            { Cancel(); return; }
            if (!locked) TrackTarget();
            // The hand is part of the skinned barrel. Correct its physical axis after Animator poses the arm.
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .3f));
            if (fired) weight *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(attack.activeEnd, 1, progress));
            AimJoint(upperArm, weight * .70f);
            AimJoint(forearm, weight);
            // A rotating offset barrel moves its own origin. Re-evaluate from the posed muzzle.
            AimJoint(forearm, weight);
            if (!pending || fired) return;
            pending = false;
            fired = true;
            warningRing.enabled = false;
            var core = Instantiate(projectile, muzzle.position, muzzle.rotation);
            SceneManager.MoveGameObjectToScene(core.gameObject, gameObject.scene);
            core.Launch(controller, attackId, attackIndex, attack.damage, speed,
                Vector3.Distance(aimPoint, muzzle.position), attack.radius);
        }

        void AimJoint(Transform joint, float weight)
        {
            Vector3 direction = aimPoint - muzzle.position;
            if (direction.sqrMagnitude < .0001f || weight <= 0) return;
            var correction = Quaternion.FromToRotation(muzzle.forward, direction);
            joint.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight) * joint.rotation;
        }

        internal void Cancel()
        {
            attackId = null;
            target = null;
            attack = null;
            progress = 0;
            pending = locked = fired = false;
            if (warningRing) warningRing.enabled = false;
        }

        void OnDisable() => Cancel();

        void OnDrawGizmosSelected()
        {
            if (!muzzle) return;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(muzzle.position, muzzle.forward * 2);
        }
    }
}

