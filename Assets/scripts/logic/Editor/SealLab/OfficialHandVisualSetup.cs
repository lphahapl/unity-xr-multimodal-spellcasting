using System;
using System.Linq;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.Editor
{
    public static class OfficialHandVisualSetup
    {
        const string Root = "Packages/com.meta.xr.sdk.interaction/Runtime/UnityXR/Prefabs/Hands/";
        public static void Install(Lab lab)
        {
            var input = lab.xrHandsSource;
            if (!input || !input.IsConfigured) throw new InvalidOperationException("Missing XR Hands source");
            foreach (bool left in new[]{true, false})
            {
                var source = left ? input.leftHand : input.rightHand;
                string path = Root + (left ? "UnityXRHandVisualLeft.prefab" : "UnityXRHandVisualRight.prefab");
                var visual = input.GetComponentsInChildren<HandVisual>(true).SingleOrDefault(v =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(v.gameObject) == path);
                if (!visual)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, input.transform);
                    visual = instance.GetComponent<HandVisual>();
                }
                visual.InjectHand(source);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            }
            var view = lab.GetComponent<NarutoVR.SealLab.SealLabView>();
            if (view) view.showBoneLines = false;
        }
    }
}
