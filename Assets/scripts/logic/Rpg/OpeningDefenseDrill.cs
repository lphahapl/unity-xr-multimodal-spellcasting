using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    public sealed class OpeningDefenseDrill : MonoBehaviour
    {
        public LinearRpgController rpg;
        public Transform muzzle, bolt, dangerBand;
        [Min(.1f)] public float flightSeconds = 1;
        [Min(.01f)] public float damage = 5;
        [Min(.1f)] public float escapeDistance = .35f;
        Action passed;
        bool shield, flying;
        float elapsed;
        Vector3 origin, aim, floor, forward;
        int shot;
        readonly string session = Guid.NewGuid().ToString("N");

        public void BeginShield(Action success)
        {
            StopDrill(); passed = success; shield = true;
        }
        public void BeginRetreat(Action success)
        {
            StopDrill(); passed = success;
            floor = rpg.locomotion.PlayerFloorPosition;
            forward = Vector3.ProjectOnPlane(rpg.locomotion.head.forward, Vector3.up).normalized;
            dangerBand.SetPositionAndRotation(floor + Vector3.up * .018f, Quaternion.LookRotation(forward));
            dangerBand.gameObject.SetActive(true);
            rpg.locomotion.Dodged += OnDodged;
        }
        void OnDodged(Vector3 delta)
        {
            Vector3 current = rpg.locomotion.PlayerFloorPosition;
            if (Vector3.Dot(delta, forward) < -.02f && Vector3.Dot(floor - current, forward) >= escapeDistance)
                Succeed();
        }
        void Update()
        {
            if (passed == null || !shield || !rpg.CanAct) return;
            var effects = rpg.skills.effects;
            if (effects.ShieldPoints < damage) { flying = false; bolt.gameObject.SetActive(false); return; }
            if (!flying)
            {
                origin = muzzle.position;
                aim = rpg.locomotion.head.position - Vector3.up * .26f;
                elapsed = 0; flying = true;
                bolt.gameObject.SetActive(true);
            }
            elapsed += Time.deltaTime;
            bolt.position = Vector3.Lerp(origin, aim, elapsed / flightSeconds);
            if (elapsed < flightSeconds) return;
            flying = false;
            bolt.gameObject.SetActive(false);
            if (Vector3.Distance(aim, rpg.locomotion.head.position - Vector3.up * .26f) > .38f) return;
            float before = effects.ShieldPoints;
            if (rpg.ReceiveDamage("opening.drill." + session + "." + (++shot), damage) && effects.ShieldPoints < before)
                Succeed();
        }
        void Succeed() { var callback = passed; StopDrill(); callback?.Invoke(); }
        public void StopDrill()
        {
            passed = null; flying = shield = false;
            if (bolt) bolt.gameObject.SetActive(false);
            if (dangerBand) dangerBand.gameObject.SetActive(false);
            if (rpg && rpg.locomotion) rpg.locomotion.Dodged -= OnDodged;
        }
        void OnDisable() => StopDrill();
    }
}
