using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Telegraphed training attack. This deliberately is not an enemy AI or network damage authority.</summary>
    public sealed class RpgTrainingHazard : MonoBehaviour
    {
        [Header("一、训练攻击")]
        [InspectorName("线性关卡控制器")] public LinearRpgController controller;
        [InspectorName("攻击间隔（游戏秒）"), Min(2)] public float interval = 6;
        [InspectorName("红圈预警时长（秒）"), Min(.5f)] public float warningSeconds = 1.5f;
        [InspectorName("攻击范围半径（米）"), Min(.1f)] public float radius = .65f;
        [InspectorName("每次伤害"), Range(1, 1000)] public float damage = 25;
        [Header("二、预警表现")]
        [InspectorName("预警材质（可留空）")] public Material warningMaterial;
        [InspectorName("预警颜色")] public Color warningColor = new(1, .12f, .03f, 1);

        public bool WarningActive => warning;
        public float WarningRemaining => warning && controller ? (float)Math.Max(0, strikesAt - controller.GameTime) : 0;
        double nextAttack, strikesAt;
        bool warning;
        int stage = -1;
        Vector3 center;
        LineRenderer ring;
        Material ownedMaterial;
        readonly string sourceId = Guid.NewGuid().ToString("N");
        long sequence;

        void Start()
        {
            var go = new GameObject("训练攻击_红圈预警"); go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>(); ring.loop = true; ring.useWorldSpace = true;
            ring.positionCount = 64; ring.widthMultiplier = .045f;
            ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
            if (warningMaterial) ring.sharedMaterial = warningMaterial;
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader)
                {
                    ownedMaterial = new Material(shader);
                    if (ownedMaterial.HasProperty("_BaseColor")) ownedMaterial.SetColor("_BaseColor", warningColor);
                    if (ownedMaterial.HasProperty("_Color")) ownedMaterial.SetColor("_Color", warningColor);
                    ring.sharedMaterial = ownedMaterial;
                }
            }
            ring.enabled = false;
        }

        void Update()
        {
            if (!controller || !ring) return;
            double now = controller.GameTime;
            if (!controller.CanAct || !controller.HazardsEnabled || stage != controller.StageIndex)
            {
                stage = controller.StageIndex; warning = false; ring.enabled = false;
                nextAttack = now + Math.Max(2, interval); return;
            }
            if (!warning && now >= nextAttack)
            {
                var nav = controller.locomotion;
                center = new Vector3(nav.head.position.x, nav.rigRoot.position.y + nav.rigFloorOffset + .055f, nav.head.position.z);
                warning = true; strikesAt = now + Math.Max(.5f, warningSeconds); ring.enabled = true;
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / ring.positionCount;
                    ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                }
            }
            if (!warning || now < strikesAt) return;
            warning = false; ring.enabled = false; nextAttack = now + Math.Max(2, interval);
            Vector3 delta = controller.locomotion.head.position - center; delta.y = 0;
            if (delta.sqrMagnitude <= radius * radius) controller.ReceiveDamage(sourceId + ":" + (++sequence), damage);
        }
        void OnDisable() { warning = false; if (ring) ring.enabled = false; }
        void OnDestroy() { if (ownedMaterial) Destroy(ownedMaterial); }
    }
}
