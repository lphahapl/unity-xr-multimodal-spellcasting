using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace NarutoVR.Editor
{
    internal static class PlayerHandRig
    {
        internal const string Art = "Assets/Art/PlayerHands/";
        internal const string Package = "Packages/com.meta.xr.sdk.interaction/Runtime/";
        internal const string Wrist = "XRHand_Wrist";
        internal static string Master(string side) => Package + "Meshes/OpenXR" + side + "Hand.fbx";
        internal static string Visual(string side) => Package + "UnityXR/Prefabs/Hands/UnityXRHandVisual" + side + ".prefab";
        internal static string Side(GameObject master)
        {
            string path = AssetDatabase.GetAssetPath(master);
            if (path == Master("Left")) return "Left";
            if (path == Master("Right")) return "Right";
            throw new ArgumentException("母版必须是安装包的 OpenXRLeftHand.fbx 或 OpenXRRightHand.fbx。");
        }

        static ModelImporter CustomImporter(GameObject model)
        {
            string path = AssetDatabase.GetAssetPath(model);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                || AssetImporter.GetAtPath(path) is not ModelImporter importer || AssetDatabase.LoadMainAssetAtPath(path) != model)
                throw new ArgumentException("请选择 Assets 下自定义 FBX 的主资产。");
            return importer;
        }

        internal static void Sync(GameObject master, GameObject model)
        {
            Side(master);
            var importer = CustomImporter(model);
            if (!model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any() ||
                !model.GetComponentsInChildren<Transform>(true).Any(t => t.name == Wrist))
                throw new ArgumentException("目标 FBX 缺少腕骨或蒙皮，未更改导入设置。");
            var source = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(master));
            var preset = new Preset(source);
            try
            {
                preset.excludedProperties = new[] { "m_ExternalObjects", "m_UserData", "m_AssetBundleName", "m_AssetBundleVariant" };
                if (!preset.ApplyTo(importer)) throw new InvalidOperationException("无法复制官方 ModelImporter 设置。");
                importer.SaveAndReimport();
            }
            finally { UnityEngine.Object.DestroyImmediate(preset); }
        }

        internal static Dictionary<string,Transform> Joints(GameObject root, List<string> errors)
        {
            var joints = new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("_end", StringComparison.OrdinalIgnoreCase)) errors.Add("意外的末端骨 " + Path(t,root));
                if (t.name.StartsWith("XRHand_", StringComparison.Ordinal) && !joints.TryAdd(t.name,t))
                    errors.Add("关节重名 " + Path(t,root));
            }
            if (joints.Count != 26) errors.Add($"关节数 {joints.Count}，预期 26。");
            return joints;
        }
        static string Path(Transform t, GameObject root) => AnimationUtility.CalculateTransformPath(t,root.transform);
        static bool Close(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i=0; i<16; i++)
                if (!float.IsFinite(b[i]) || Mathf.Abs(a[i]-b[i]) > .0002f * Mathf.Max(1f,Mathf.Abs(a[i]))) return false;
            return true;
        }

        internal static List<string> Compare(GameObject master, GameObject model)
        {
            Side(master);
            var imp = CustomImporter(model);
            var errors = new List<string>();
            var reference = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(master));
            if (imp.globalScale!=reference.globalScale || imp.useFileScale!=reference.useFileScale ||
                imp.useFileUnits!=reference.useFileUnits || imp.animationType!=reference.animationType ||
                imp.avatarSetup!=reference.avatarSetup || imp.bakeAxisConversion!=reference.bakeAxisConversion ||
                imp.preserveHierarchy!=reference.preserveHierarchy || imp.optimizeGameObjects!=reference.optimizeGameObjects ||
                imp.maxBonesPerVertex!=reference.maxBonesPerVertex || imp.skinWeights!=reference.skinWeights)
                errors.Add("ModelImporter 与母版不一致，请先同步。");
            var expected=Joints(master,errors);
            var actual=Joints(model,errors);
            if (!Close(master.transform.localToWorldMatrix,model.transform.localToWorldMatrix))
                errors.Add("FBX 根节点的位置、轴向或缩放不同。");
            foreach (var pair in expected)
            {
                if (!actual.TryGetValue(pair.Key,out var bone)) { errors.Add("缺骨 " + pair.Key); continue; }
                var referenceBone=pair.Value;
                if (Path(referenceBone,master)!=Path(bone,model)) errors.Add("父链不同 " + Path(bone,model) + "，预期 " + Path(referenceBone,master));
                if (!Close(Matrix4x4.Scale(referenceBone.localScale),Matrix4x4.Scale(bone.localScale)))
                    errors.Add("局部缩放不同 " + pair.Key + " " + bone.localScale);
                if (!Close(master.transform.worldToLocalMatrix * referenceBone.localToWorldMatrix,
                           model.transform.worldToLocalMatrix * bone.localToWorldMatrix))
                    errors.Add("静置位置、轴向或祖先缩放不同 " + pair.Key);
            }
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t != model.transform && (t.name.StartsWith("XRHand_",StringComparison.Ordinal) ||
                    actual.TryGetValue(Wrist,out var wrist) && t.IsChildOf(wrist)) && !expected.ContainsKey(t.name))
                    errors.Add("多余骨或腕下节点 " + Path(t,model));
            var original=master.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length!=1 || !skins[0].sharedMesh) { errors.Add("需且仅需一个有网格的 SkinnedMeshRenderer。"); return errors; }
            var skin=skins[0];
            if (!Close(master.transform.worldToLocalMatrix*original.transform.localToWorldMatrix,
                       model.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix))
                errors.Add("网格对象空间或枢轴与官方不同。");
            var referenceBones=original.bones;
            var referenceBinds=original.sharedMesh.bindposes;
            var binds=skin.sharedMesh.bindposes;
            var bones=skin.bones;
            if (bones.Length!=binds.Length || bones.Length==0) errors.Add("蒙皮骨索引数与绑定矩阵数不符。");
            var seen=new HashSet<string>();
            for(int i=0; i<Math.Min(binds.Length,bones.Length); i++)
            {
                var bone=bones[i];
                if (!bone || !actual.TryGetValue(bone.name,out var known) || known!=bone || !seen.Add(bone.name))
                { errors.Add("空、重复或未知的蒙皮骨索引 " + i); continue; }
                int j=Array.FindIndex(referenceBones,t=>t && t.name==bone.name);
                if (j<0 || j>=referenceBinds.Length || !Close(referenceBinds[j],binds[i])) errors.Add("绑定矩阵与母版不同 " + bone.name);
            }
            return errors;
        }
    }
}
