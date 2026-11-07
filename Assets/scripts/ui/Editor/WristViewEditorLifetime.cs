#if UNITY_EDITOR
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    internal static class WristViewEditorLifetime
    {
        static WristViewEditorLifetime() => AssemblyReloadEvents.beforeAssemblyReload += ReleaseBeforeReload;
        internal static void ReleaseBeforeReload()
        {
            foreach(var view in Resources.FindObjectsOfTypeAll<WristMenuView>())
            {
                if(!view || EditorUtility.IsPersistent(view) || !view.gameObject.scene.IsValid()) continue;
                ReleaseView(view);
            }
        }
        internal static void ReleaseView(WristMenuView view)
        {
            if(view.TryGetComponent<WristMenuController>(out var controller)) controller.ResetForWorldChange();
            view.ReleaseVisuals(true);
        }
    }
}
#endif
