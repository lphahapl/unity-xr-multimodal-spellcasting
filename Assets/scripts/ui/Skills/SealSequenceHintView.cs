using System;
using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>只读玩家的已确认印序，把候选交给头部提示预制体。</summary>
    internal sealed class SealSequenceHintView : IDisposable
    {
        readonly SkillInputController source;
        readonly List<SkillSealHint> hints = new(3);
        SealHintPanel panel;
        double nextRefresh, awakeUntil = double.NegativeInfinity;

        public SealSequenceHintView(SkillInputController source)
        {
            this.source = source;
            source.SealConfirmed += Wake;
        }

        void Wake(string seal, double observed)
        {
            if (!source.SealHintsEnabled) return;
            awakeUntil = observed + source.Engine.HistoryLimitSeconds;
            nextRefresh = double.NegativeInfinity;
        }

        public void Refresh(double now)
        {
            var head = source.effects ? source.effects.head : null;
            if (!head) { Clear(); return; }
            if (panel && panel.transform.parent != head) panel.transform.SetParent(head, false);

            if (!source.SealHintsEnabled || now > awakeUntil)
            {
                if (panel && panel.gameObject.activeSelf) panel.Retract(now);
            }
            else if (now >= nextRefresh)
            {
                nextRefresh = now + .05;
                source.Engine.GetSealHints(hints);
                if (!panel && hints.Count > 0)
                {
                    var prefab = source.sealHintsPrefab;
                    if (!prefab)
                    {
                        var asset = Resources.Load<GameObject>("UI/SealHintPanel");
                        if (asset) prefab = asset.GetComponent<SealHintPanel>();
                    }
                    if (!prefab)
                    {
                        Debug.LogError("结印候选面板缺少预制体 UI/SealHintPanel", source);
                        Dismiss();
                        return;
                    }
                    panel = UnityEngine.Object.Instantiate(prefab, head, false);
                    panel.name = "结印候选";
                }
                if (panel && (hints.Count != 0 || panel.gameObject.activeSelf)) panel.Present(hints, now);
            }
            if (panel && panel.gameObject.activeSelf) panel.Animate(now);
        }

        public void Dismiss() => awakeUntil = double.NegativeInfinity;

        public void Clear()
        {
            Dismiss();
            if (panel) panel.HideImmediately();
        }

        public void Dispose()
        {
            source.SealConfirmed -= Wake;
            if (panel) UnityEngine.Object.Destroy(panel.gameObject);
        }
    }
}
