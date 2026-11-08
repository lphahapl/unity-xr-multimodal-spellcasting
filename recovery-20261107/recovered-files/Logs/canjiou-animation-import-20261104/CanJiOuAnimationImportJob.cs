using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static class CanJiOuAnimationImportJob
{
    private const string Root = "Assets/SealCombat/Art/Enemies/CanJiOu/";
    private const string Output = "Logs/canjiou-animation-import-20261104/";

    [Serializable]
    private class Entry
    {
        public string path;
        public string clip;
        public float seconds;
        public float fps;
        public bool humanoid;
        public bool validAvatar;
        public bool loop;
        public Vector3 averageSpeed;
        public float averageAngularSpeed;
        public int curveCount;
        public Vector3[] hips;
        public float poseChange;
        public string[] warnings;
        public string[] errors;
    }

    [Serializable]
    private class Report
    {
        public string unity;
        public bool targetAvatarValid;
        public List<Entry> animations = new List<Entry>();
    }

    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "CanJiOu_AutoLOD.prefab");
            var animator = target.GetComponentInChildren<Animator>();
            var report = new Report { unity = Application.unityVersion,
                targetAvatarValid = animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman };
            if (!report.targetAvatarValid) throw new Exception("Target Humanoid avatar is invalid.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
            RenderSettings.ambientIntensity = 1;
            var light = new GameObject("PreviewLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);

            foreach (var path in Directory.GetFiles(Root + "Animations", "*.fbx").OrderBy(p => p))
            {
                string assetPath = path.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                bool loop = name.EndsWith("Idle") || name.Contains("Forward") || name.EndsWith("DazedSeated");
                var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var definitions = importer.defaultClipAnimations;
                if (definitions.Length != 1) throw new Exception(assetPath + " unexpected clip count: " + definitions.Length);
                definitions[0].name = name;
                definitions[0].loopTime = loop;
                definitions[0].loopPose = loop;
                importer.clipAnimations = definitions;
                importer.SaveAndReimport();

                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var clip = assets.OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
                var avatar = assets.OfType<Avatar>().FirstOrDefault();
                var entry = new Entry { path = assetPath, clip = clip.name, seconds = clip.length,
                    fps = clip.frameRate, humanoid = clip.humanMotion,
                    validAvatar = avatar && avatar.isValid && avatar.isHuman, loop = clip.isLooping,
                    averageSpeed = clip.averageSpeed, averageAngularSpeed = clip.averageAngularSpeed,
                    curveCount = AnimationUtility.GetCurveBindings(clip).Length,
                    warnings = importer.GetImportWarnings(), errors = importer.GetImportErrors() };
                if (!entry.validAvatar || !entry.humanoid || entry.seconds <= 0 || entry.errors.Length > 0)
                    throw new Exception("Import validation failed: " + assetPath);
                CheckOnTarget(target, clip, entry);
                report.animations.Add(entry);
                File.WriteAllText(Output + "unity-report.json", JsonUtility.ToJson(report, true));
                Debug.Log("CANJIOU_IMPORT_PASS " + name + " " + entry.seconds + "s, poseChange=" + entry.poseChange);
            }
            if (report.animations.Count != 11) throw new Exception("Expected 11 animations.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "complete.txt", "PASS 11 imported Humanoid clips sampled on CanJiOu_AutoLOD.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText(Output + "failed.txt", error.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static void CheckOnTarget(GameObject prefab, AnimationClip clip, Entry entry)
    {
        var instance = Object.Instantiate(prefab);
        var animator = instance.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        var lod = instance.GetComponentInChildren<LODGroup>();
        if (lod) lod.ForceLOD(0);
        foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
        var graph = PlayableGraph.Create("CanJiOuPreview");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable = AnimationClipPlayable.Create(graph, clip);
        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var bones = new[] { HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        var initial = new Vector3[bones.Length];
        entry.hips = new Vector3[3];
        for (int sample = 0; sample < 3; sample++)
        {
            playable.SetTime(clip.length * sample * 0.499f);
            graph.Evaluate(0);
            entry.hips[sample] = hips.position;
            for (int index = 0; index < bones.Length; index++)
            {
                Vector3 position = animator.GetBoneTransform(bones[index]).position;
                if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                    throw new Exception("Invalid bone position in " + clip.name);
                if (sample == 0) initial[index] = position;
                else entry.poseChange = Mathf.Max(entry.poseChange, Vector3.Distance(position, initial[index]));
            }
            Render(instance, clip.name + "_" + sample);
        }
        graph.Destroy();
        Object.DestroyImmediate(instance);
        if (entry.poseChange < 0.001f) throw new Exception("Animation did not move target: " + clip.name);
    }

    private static void Render(GameObject instance, string name)
    {
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 1.25f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        camera.transform.position = new Vector3(2.5f, 1.9f, 4.5f);
        camera.transform.LookAt(new Vector3(0, .85f, 0));
        var texture = RenderTexture.GetTemporary(420, 480, 24);
        var previous = RenderTexture.active;
        var pixels = new Texture2D(420, 480, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 420, 480), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Output + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(texture);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
