using UnityEngine;
using System.Collections.Generic;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent, RequireComponent(typeof(Collider))]
    public sealed class PassageGlyphAnchor : MonoBehaviour
    {
        [Tooltip("同一岸的所有标记引用同一节点，两处不同岸之间才能搭路。")]
        public Transform bank;
        [Tooltip("桥面顶部的接岸位置。")]
        public Transform landing;
        public bool Available => isActiveAndEnabled && bank && landing && bank.gameObject.activeInHierarchy;
        readonly Dictionary<PassageGlyphAnchor, GameObject> roads = new();
        internal bool HasRoad
        {
            get { foreach (var road in roads.Values) if (road) return true; return false; }
        }
        internal bool CanLinkTo(PassageGlyphAnchor other, float span)
        {
            if (!Available || !other || !other.Available || bank == other.bank || Connected(other)) return false;
            var delta = other.landing.position - landing.position;
            return delta.sqrMagnitude <= span * span && delta.x * delta.x + delta.z * delta.z >= .01f;
        }
        internal PassageGlyphRoad RoadTo(PassageGlyphAnchor other)
        {
            return roads.TryGetValue(other, out var road) && road ? road.GetComponent<PassageGlyphRoad>() : null;
        }

        internal void Register(PassageGlyphAnchor other, GameObject road) => roads[other] = road;
        internal bool Connected(PassageGlyphAnchor other) => roads.TryGetValue(other, out var road) && road;
    }
}
