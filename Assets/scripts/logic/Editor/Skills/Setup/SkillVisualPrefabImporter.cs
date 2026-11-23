using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    internal sealed class SkillVisualPrefabImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPostprocessPrefab(GameObject root)
        {
            if (root.TryGetComponent<SkillVisualLifetime>(out var visual))
                visual.prefabAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        }
    }
}
