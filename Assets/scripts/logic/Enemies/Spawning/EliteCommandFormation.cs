using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>精英指挥时显示内接勾股三角形，并从圆心连向本次召唤会话仍存活的成员。</summary>
    [DisallowMultipleComponent]
    public sealed class EliteCommandFormation : MonoBehaviour
    {
        const float Radius = 1.6f;
        const int CircleSegments = 36;
        const float GroundOffset = 0.08f;
        const float MemberHeight = 0.9f;
        [SerializeField, InspectorName("显示指挥算阵")] bool showFormation = true;
        readonly List<LineRenderer> links = new();
        Transform pivot;
        LineRenderer circle;
        LineRenderer triangle;
        Material sharedLineMaterial;
        EnemyLease commander;
        IReadOnlyList<EnemyLease> members;
        int visibleLinks;

        /// <summary>控制表现开关。打开时重新核验当前指挥官及其成员的出生身份。</summary>
        public bool ShowFormation
        {
            get => showFormation;
            set { showFormation = value; Refresh(); }
        }

        /// <summary>进入指挥态。传入该精英当前会话独占的成员名单，成员增减在下一帧生效。</summary>
        public void Present(EnemyLease leader, IReadOnlyList<EnemyLease> roster, Material lineMaterial)
        {
            // 已退池的旧会话即使稍晚抵达，也不能在同一物体的新生命上开启算阵。
            if (!IsLiving(leader) || leader.Controller.gameObject != gameObject) return;
            commander = leader;
            members = roster;
            sharedLineMaterial = lineMaterial;
            if (pivot && circle.sharedMaterial != lineMaterial)
            {
                circle.sharedMaterial = triangle.sharedMaterial = lineMaterial;
                foreach (var line in links) line.sharedMaterial = lineMaterial;
            }
            Refresh();
        }

        /// <summary>退出指挥态，解除对该次租约与名单的引用。重新进入由会话再次调用 Present。</summary>
        public void Hide()
        {
            commander = null;
            members = null;
            sharedLineMaterial = null;
            HideRenderers();
        }

        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();
        void OnDisable()
        {
            if (gameObject.activeInHierarchy) HideRenderers();
            else Hide(); // 整个敌人被禁用或退池时，本轮指挥即告结束。
        }

        static bool IsLiving(EnemyLease lease) => lease != null && !lease.Defeated &&
            lease.Owner && lease.Owner.IsCurrent(lease) && !lease.Controller.IsDead;

        void Refresh()
        {
            if (commander == null) return;
            if (!IsLiving(commander)) { Hide(); return; }
            if (!isActiveAndEnabled || !showFormation || !sharedLineMaterial) { HideRenderers(); return; }
            if (!pivot) BuildGeometry();
            var origin = pivot.position;
            int count = 0;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                var child = members[i];
                if (!IsLiving(child) || !child.Controller.IsCommandedBy(commander)) continue;
                if (count == links.Count)
                {
                    var extra = CreateLine("SurveyLink", true, false, new Color(.32f, .88f, .85f));
                    extra.positionCount = 2;
                    links.Add(extra);
                }
                var link = links[count++];
                link.SetPosition(0, origin);
                link.SetPosition(1, child.Controller.transform.position + Vector3.up * MemberHeight);
                if (!link.enabled) link.enabled = true;
            }
            SetVisibleLinkCount(count);
            if (!pivot.gameObject.activeSelf) pivot.gameObject.SetActive(true);
        }

        void HideRenderers()
        {
            SetVisibleLinkCount(0);
            if (pivot && pivot.gameObject.activeSelf) pivot.gameObject.SetActive(false);
        }

        void SetVisibleLinkCount(int count)
        {
            for (int i = count; i < visibleLinks; i++) links[i].enabled = false;
            visibleLinks = count;
        }

        void BuildGeometry()
        {
            pivot = new GameObject("EliteCommandFormationVisual").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.up * GroundOffset;
            var color = new Color(.85f, .68f, .28f);
            circle = CreateLine("OuterCircle", false, true, color);
            circle.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / CircleSegments;
                circle.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0, Mathf.Sin(angle) * Radius));
            }
            // 以直径为斜边，第三点同时满足位于圆上和两直角边之比 3:4。
            triangle = CreateLine("Pythagorean345Triangle", false, true, color);
            triangle.positionCount = 3;
            triangle.SetPosition(0, Vector3.left * Radius);
            triangle.SetPosition(1, Vector3.right * Radius);
            triangle.SetPosition(2, new Vector3(-.28f, 0, .96f) * Radius);
        }

        LineRenderer CreateLine(string label, bool worldSpace, bool loop, Color color)
        {
            var go = new GameObject(label);
            go.layer = gameObject.layer;
            go.transform.SetParent(pivot, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = sharedLineMaterial;
            line.useWorldSpace = worldSpace;
            line.loop = loop;
            line.startWidth = line.endWidth = .022f;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
