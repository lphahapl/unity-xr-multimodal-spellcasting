using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>关卡调用 Show 展开固定地点的模板板。成功信号每次展开最多发出一次。</summary>
    public sealed class GlyphStoryPanel : MonoBehaviour
    {
        public GlyphBoard board;
        public GlyphTemplate template;
        public UnityEvent onCompleted = new();
        static readonly List<GlyphStoryPanel> waiting = new();
        public bool IsWaiting => waiting.Contains(this) && board && board.IsOpen;
        public bool IsResolving => IsWaiting && board.IsShowingSuccess;

        public void Show()
        {
            if (IsWaiting || !isActiveAndEnabled || !board || !template) return;
            board.Open(new[] { template }, true);
            if (!waiting.Contains(this)) waiting.Add(this);
        }

        public void ResetDrawing()
        {
            if (IsWaiting) board.Open(new[] { template }, true);
        }

        public void Cancel()
        {
            if (waiting.Remove(this) && board) board.Close();
        }

        public void Complete()
        {
            if (!IsWaiting) return;
            Cancel();
            onCompleted.Invoke();
        }

        public static GlyphStoryPanel FindNear(Vector3 worldPosition, float distance)
        {
            GlyphStoryPanel best = null;
            float bestDistance = distance * distance;
            for (int i = waiting.Count - 1; i >= 0; --i)
            {
                var panel = waiting[i];
                if (!panel || !panel.IsWaiting) { waiting.RemoveAt(i); continue; }
                if (panel.IsResolving) continue;
                float separation = (panel.board.transform.position - worldPosition).sqrMagnitude;
                if (separation <= bestDistance) { best = panel; bestDistance = separation; }
            }
            return best;
        }

        void OnDisable() => Cancel();
        void OnDestroy() => Cancel();
    }
}
