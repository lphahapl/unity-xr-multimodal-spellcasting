using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [Serializable]
    public sealed class ChapterStageVisibility
    {
        [InspectorName("场景物体")] public GameObject sceneObject;
        [InspectorName("显示该物体的阶段 ID")] public string[] stageIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ChapterTargetHighlight
    {
        [InspectorName("目标物体")] public GameObject target;
        [InspectorName("瞄准标记（可选）")] public GameObject marker;
        [InspectorName("要换材质的渲染器（可选）")] public Renderer surface;
        [InspectorName("高亮材质")] public Material activeMaterial;
        [InspectorName("常态材质")] public Material restingMaterial;
    }

    /// <summary>根据阶段 ID 切换场景提示，根据当前目标切换标记及材质。</summary>
    [DisallowMultipleComponent, AddComponentMenu("NarutoVR/章节/章节场景提示")]
    public sealed class ChapterSceneGuidance : MonoBehaviour
    {
        [InspectorName("按阶段显示的物体")] public ChapterStageVisibility[] stageVisibility = Array.Empty<ChapterStageVisibility>();
        [InspectorName("可高亮的目标")] public ChapterTargetHighlight[] targetHighlights = Array.Empty<ChapterTargetHighlight>();

        string requestedStage;
        GameObject requestedTarget;
        bool hasRequest;

        public void Show(string stageId, GameObject target = null)
        {
            hasRequest = true;
            requestedStage = stageId;
            requestedTarget = target;
            if (isActiveAndEnabled) Apply(stageId, target);
        }

        /// <summary>切换句内目标，保留已应用的阶段物体状态。</summary>
        internal void SetFocus(GameObject target)
        {
            if (requestedStage == null) return;
            requestedTarget = target;
            if (isActiveAndEnabled) ApplyFocus(target);
        }

        void OnEnable()
        {
            if (Application.isPlaying && hasRequest) Apply(requestedStage, requestedTarget);
        }

        void OnDisable()
        {
            if (Application.isPlaying && hasRequest) Apply(null, null);
        }

        void Apply(string stageId, GameObject target)
        {
            foreach (var cue in stageVisibility)
            {
                if (cue?.sceneObject)
                    cue.sceneObject.SetActive(stageId != null && Array.IndexOf(cue.stageIds, stageId) >= 0);
            }
            ApplyFocus(stageId == null ? null : target);
        }

        void ApplyFocus(GameObject target)
        {
            foreach (var cue in targetHighlights)
            {
                if (cue == null) continue;
                bool focused = target && cue.target == target;
                if (cue.marker) cue.marker.SetActive(focused);
                if (cue.surface) cue.surface.sharedMaterial = focused ? cue.activeMaterial : cue.restingMaterial;
            }
        }

        internal void ValidateConfiguration(RpgChapterFlow chapter, RpgStageDefinition[] stages)
        {
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in stages) knownIds.Add(stage.stageId);
            var controlledObjects = new HashSet<GameObject>();
            foreach (var cue in stageVisibility)
            {
                if (cue == null || !cue.sceneObject || cue.stageIds == null || cue.stageIds.Length == 0)
                    throw new InvalidOperationException("章节场景提示缺少显示物体或阶段 ID");
                ValidateVisibilityOwner(cue.sceneObject);
                foreach (string id in cue.stageIds)
                    if (!knownIds.Contains(id)) throw new InvalidOperationException($"{cue.sceneObject.name} 的提示引用了未知阶段：{id}");
            }
            var targets = new HashSet<GameObject>();
            var surfaces = new HashSet<Renderer>();
            foreach (var cue in targetHighlights)
            {
                if (cue == null || !cue.target || !targets.Add(cue.target) || (!cue.marker && !cue.surface))
                    throw new InvalidOperationException("章节提示目标缺失、重复或未指定标记与渲染器");
                if (cue.marker) ValidateVisibilityOwner(cue.marker);
                if (cue.surface && (!cue.activeMaterial || !cue.restingMaterial || !surfaces.Add(cue.surface)))
                    throw new InvalidOperationException($"{cue.target.name} 的高亮材质不完整或渲染器被重复绑定");
            }

            void ValidateVisibilityOwner(GameObject output)
            {
                if (!controlledObjects.Add(output) || transform.IsChildOf(output.transform) ||
                    chapter.transform.IsChildOf(output.transform) || chapter.Owner.transform.IsChildOf(output.transform))
                    throw new InvalidOperationException($"{output.name} 的提示显隐绑定重复或会禁用章节自身");
            }
        }

        internal bool ControlsVisibility(GameObject output) =>
            Array.Exists(stageVisibility, cue => cue != null && cue.sceneObject == output) ||
            Array.Exists(targetHighlights, cue => cue != null && cue.marker == output);
    }
}
