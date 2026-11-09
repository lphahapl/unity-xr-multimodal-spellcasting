// Isolated-project Editor tool. Never saves a scene, rebuilds art, publishes, or cleans warnings.
// EXTERIOR_MERGE_REPORT=<absolute merge-check.json>; -executeMethod ExteriorMergedAcceptance.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NarutoVR.EnvironmentAudio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ExteriorMergedAcceptance
{
    const string Source = "Assets/Scene/Chapter01_Awakening.unity";
    const string Candidate = "Assets/Scene/QA_ExteriorPolishMerged.unity";
    [Serializable] public class Merge
    {
        public string baseSha256, candidateSha256, reportDir, newPrefabGUID, scene;
        public string newPrefabSHA256;
        public bool allOriginalMonoBehavioursByteIdentical, allUnlistedBlocksByteIdentical;
        public bool allUnlistedFieldsInChangedBlocksByteIdentical;
        public int originalMonoBehaviourCount, candidateSerializedMonoBehaviourCount;
    }
    [Serializable] public class Change
    {
        public string objectType, propertyPath, before, after, prefabSourceGUID;
        public long fileID, prefabInstanceFileID, prefabSourceFileID;
        public bool prefabInstance;
    }
    [Serializable] public class Author { public Change[] changes; public string newPrefabPath; }
    [Serializable] public class Check { public string name, detail; public bool passed; }
    [Serializable] public class Review
    {
        public bool passed, automaticChecksOnly = true, requiresParentVisualReview = true;
        public bool visualAccepted;
        public string baseSha256, candidateSha256, unityVersion, time, mergeReport, exception;
        public int sourceMissingScripts, candidateMissingScripts, sourceColliderCount, candidateColliderCount;
        public string[] sourceMissingScriptPaths, candidateMissingScriptPaths;
        public string[] sourceWarnings, candidateWarnings, sourceErrors, candidateErrors;
        public string[] orphanMonoBehaviourIdsPreserved;
        public Check[] checks;
        public string limitations = "Automatic import/render/serialization checks only. Parent must inspect photos and visual design separately. No audible listening, gameplay traversal, Quest timing or APK validation. Existing orphan MonoBehaviour warnings are compared with the source and never cleaned.";
    }
    sealed class State
    {
        public Dictionary<string, string> colliders = new Dictionary<string, string>();
        public Dictionary<string, string> changedValues = new Dictionary<string, string>();
        public List<string> missing = new List<string>(), warnings = new List<string>(), errors = new List<string>();
    }
    static readonly List<Check> Checks = new List<Check>();
    static State capture;
    static string output;

    public static void Run()
    {
        if (!Application.isBatchMode) { Debug.LogError("Merged acceptance is restricted to an isolated Unity batch process."); return; }
        Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.dataPath));
        var review = new Review { time = DateTimeOffset.Now.ToString("O"), unityVersion = Application.unityVersion };
        string reportPath = Environment.GetEnvironmentVariable("EXTERIOR_MERGE_REPORT");
        output = !string.IsNullOrEmpty(reportPath) ? Path.GetDirectoryName(Path.GetFullPath(reportPath))
            : Path.GetFullPath("Logs/exterior-merged-acceptance");
        Directory.CreateDirectory(output);
        Checks.Clear();
        Application.logMessageReceived += Log;
        try
        {
            Require(!string.IsNullOrEmpty(reportPath) && File.Exists(reportPath), "Explicit EXTERIOR_MERGE_REPORT exists");
            review.mergeReport = Path.GetFullPath(reportPath);
            var merge = JsonUtility.FromJson<Merge>(File.ReadAllText(reportPath));
            Require(!string.IsNullOrEmpty(merge.scene) && !string.Equals(Path.GetFullPath(Source),Path.GetFullPath(merge.scene),StringComparison.OrdinalIgnoreCase),
                "Acceptance runs in isolated project, not the main scene path");
            review.baseSha256 = merge.baseSha256; review.candidateSha256 = merge.candidateSha256;
            Require(Sha(Source) == merge.baseSha256, "Isolated source matches merge base hash", "Sync latest main source into clone before running acceptance if this fails.");
            Require(Sha(Candidate) == merge.candidateSha256, "Candidate matches merged candidate hash");
            Require(Sha(Path.Combine(output, "publication-base.unity")) == merge.baseSha256, "Merge publication-base snapshot matches base hash");
            Require(merge.allOriginalMonoBehavioursByteIdentical && merge.allUnlistedBlocksByteIdentical &&
                merge.allUnlistedFieldsInChangedBlocksByteIdentical, "Merger proved unlisted serialized bytes unchanged");
            var author = JsonUtility.FromJson<Author>(File.ReadAllText(Path.Combine(merge.reportDir, "scene-field-changes.json")));
            var sourceScripts = ScriptBlocks(File.ReadAllText(Source));
            var candidateScripts = ScriptBlocks(File.ReadAllText(Candidate));
            Require(sourceScripts.Count == merge.originalMonoBehaviourCount && sourceScripts.Count == candidateScripts.Count,
                "Serialized MonoBehaviour counts agree with merge report", sourceScripts.Count.ToString());
            Require(sourceScripts.All(x => candidateScripts.ContainsKey(x.Key) && candidateScripts[x.Key] == x.Value),
                "Every original MonoBehaviour block byte-identical, including orphan blocks");
            review.orphanMonoBehaviourIdsPreserved = OrphanScripts(File.ReadAllText(Source));
            WaterPolishAcceptance.Validate();

            capture = new State();
            var source = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            Require(source.IsValid() && source.isLoaded, "Source baseline scene opened");
            Physics.SyncTransforms();
            CaptureScene(source, author, capture);
            var sourceState = capture;
            Photo(source, "accept-source-canal.png");
            var sourceMaterials = Materials(source);
            foreach (var material in sourceMaterials) CheckShader(material.shader, "Source shader");

            capture = new State();
            var candidate = EditorSceneManager.OpenScene(Candidate, OpenSceneMode.Single);
            Require(candidate.IsValid() && candidate.isLoaded, "Merged candidate scene opened");
            Physics.SyncTransforms();
            CaptureScene(candidate, author, capture);
            var candidateState = capture;
            Require(candidateState.missing.Count<=sourceState.missing.Count && !candidateState.missing.Except(sourceState.missing).Any(), "No new missing script component paths or count increase");
            Require(sourceState.colliders.All(x => candidateState.colliders.ContainsKey(x.Key) && candidateState.colliders[x.Key] == x.Value),
                "All original collider type, enabled state, transform, bounds and shape retained", sourceState.colliders.Count.ToString());
            foreach (var change in author.changes.Where(c => c.objectType != "TransientPipelinePreviewSetting"))
            {
                string key = ChangeKey(change);
                Require(sourceState.changedValues.ContainsKey(key) && ValueEqual(sourceState.changedValues[key], change.before),
                    "Unity source before: " + key, sourceState.changedValues.ContainsKey(key) ? sourceState.changedValues[key] : "not found");
                Require(candidateState.changedValues.ContainsKey(key) && ValueEqual(candidateState.changedValues[key], change.after),
                    "Unity merged after: " + key, candidateState.changedValues.ContainsKey(key) ? candidateState.changedValues[key] : "not found");
            }
            Require(author.changes.Any(c => c.objectType == "MeshRenderer" && c.propertyPath == "m_Enabled" && c.after == "0"),
                "Actual old-renderer disable targets were checked against source");
            string prefabPath = AssetDatabase.GUIDToAssetPath(merge.newPrefabGUID);
            Require(prefabPath == author.newPrefabPath && Sha(prefabPath) == merge.newPrefabSHA256,
                "EnvironmentPolish prefab GUID/path/hash match merge");
            var roots = candidate.GetRootGameObjects().Where(go =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == prefabPath).ToArray();
            Require(roots.Length == 1, "Exactly one EnvironmentPolish scene root");
            var environment = roots[0];
            Require(environment.GetComponentsInChildren<Transform>(true).All(t =>
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "New prefab contains no missing scripts");
            ValidateAssets(environment, prefabPath);
            ValidateWater(environment);
            ValidatePortal(environment);
            Photo(candidate, "accept-merged-canal.png");
            foreach (var material in Materials(candidate)) CheckShader(material.shader, "Rendered candidate shader");
            review.sourceWarnings = sourceState.warnings.ToArray(); review.candidateWarnings = candidateState.warnings.ToArray();
            review.sourceErrors = sourceState.errors.ToArray(); review.candidateErrors = candidateState.errors.ToArray();
            review.sourceColliderCount = sourceState.colliders.Count; review.candidateColliderCount = candidateState.colliders.Count;
            // Unity embeds the opened scene's path in its orphan-component preamble.
            // Compare both message content and count after normalizing only those two paths.
            string NormalizeScenePath(string message) => message.Replace(Source,"<scene>").Replace(Candidate,"<scene>");
            var sourceErrorCounts = sourceState.errors.GroupBy(NormalizeScenePath).ToDictionary(g=>g.Key,g=>g.Count());
            Require(candidateState.errors.GroupBy(NormalizeScenePath).All(g=>sourceErrorCounts.TryGetValue(g.Key,out int count)&&g.Count()<=count),
                "No new Unity error content or count compared with source");
            var newOrphanWarnings = candidateState.warnings.Where(x => x.Contains("does not reference component MonoBehaviour") && !sourceState.warnings.Contains(x));
            Require(!newOrphanWarnings.Any(), "No additional orphan MonoBehaviour warning compared with source");
            Require(Sha(Source) == merge.baseSha256 && Sha(Candidate) == merge.candidateSha256,
                "Source and candidate files unchanged after scene import and rendering");
            review.sourceMissingScripts = sourceState.missing.Count;
            review.candidateMissingScripts = candidateState.missing.Count;
            review.sourceMissingScriptPaths = sourceState.missing.ToArray();
            review.candidateMissingScriptPaths = candidateState.missing.ToArray();
            review.sourceWarnings = sourceState.warnings.ToArray(); review.candidateWarnings = candidateState.warnings.ToArray();
            review.sourceErrors = sourceState.errors.ToArray(); review.candidateErrors = candidateState.errors.ToArray();
            review.sourceColliderCount = sourceState.colliders.Count; review.candidateColliderCount = candidateState.colliders.Count;
            review.passed = true;
        }
        catch (Exception e) { review.passed = false; review.exception = e.ToString(); Debug.LogException(e); }
        finally
        {
            capture = null;
            Application.logMessageReceived -= Log;
            review.checks = Checks.ToArray();
            File.WriteAllText(Path.Combine(output, "parent-review.json"), JsonUtility.ToJson(review, true));
        }
        EditorApplication.Exit(review.passed ? 0 : 1);
    }

    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static string Key(Object obj)
    {
        var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        return unchecked((long)id.targetObjectId) + ":" + unchecked((long)id.targetPrefabId);
    }
    static string ChangeKey(Change c) => c.fileID + ":" + c.prefabInstanceFileID + ":" + c.propertyPath;
    static string F(float f) => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    static string Vector(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
    static string AssetKey(Object obj)
    {
        if (!obj) return "null";
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long id);
        return guid + ":" + id;
    }
    static void CaptureScene(Scene scene, Author author, State state)
    {
        foreach (var t in All<Transform>(scene))
        {
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            for (int i = 0; i < missing; i++) state.missing.Add(PathOf(t) + "|missing#" + i);
        }
        foreach (var c in All<Collider>(scene))
        {
            string shape = c is BoxCollider box ? Vector(box.center) + "|" + Vector(box.size) :
                c is SphereCollider sphere ? Vector(sphere.center) + "|" + F(sphere.radius) :
                c is CapsuleCollider capsule ? Vector(capsule.center) + "|" + F(capsule.radius) + "|" + F(capsule.height) + "|" + capsule.direction :
                c is MeshCollider mesh ? AssetKey(mesh.sharedMesh) + "|" + mesh.convex : "other";
            var tr = c.transform;
            state.colliders.Add(Key(c), c.GetType().Name + "|" + c.enabled + "|" + c.isTrigger + "|" +
                Vector(tr.position) + "|" + tr.rotation + "|" + Vector(tr.lossyScale) + "|" +
                Vector(c.bounds.center) + "|" + Vector(c.bounds.size) + "|" + shape + "|" + AssetKey(c.sharedMaterial));
        }
        var components = All<Component>(scene).Where(c => c).ToArray();
        foreach (var change in author.changes.Where(c => c.objectType != "TransientPipelinePreviewSetting"))
        {
            if (change.objectType == "RenderSettings")
            {
                state.changedValues[ChangeKey(change)] = RenderValue(change.propertyPath); continue;
            }
            Component target = components.FirstOrDefault(component =>
            {
                var id = GlobalObjectId.GetGlobalObjectIdSlow(component);
                if (!change.prefabInstance) return unchecked((long)id.targetObjectId) == change.fileID && id.targetPrefabId == 0;
                if (unchecked((long)id.targetPrefabId) != change.prefabInstanceFileID) return false;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(component);
                if (!src) return false;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(src, out string guid, out long fid);
                return guid == change.prefabSourceGUID && fid == change.prefabSourceFileID;
            });
            Require(target, "Resolved author target " + ChangeKey(change));
            var property = new SerializedObject(target).FindProperty(change.propertyPath);
            Require(property != null, "Serialized property exists " + change.propertyPath);
            state.changedValues[ChangeKey(change)] = PropertyValue(property);
        }
    }
    static string PropertyValue(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: return p.boolValue ? "1" : "0";
            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: return p.intValue.ToString();
            case SerializedPropertyType.Float: return F(p.floatValue);
            case SerializedPropertyType.Color: return JsonUtility.ToJson(p.colorValue);
            case SerializedPropertyType.ObjectReference: return "{fileID: " + (p.objectReferenceValue ? unchecked((long)GlobalObjectId.GetGlobalObjectIdSlow(p.objectReferenceValue).targetObjectId) : 0) + "}";
            default: throw new InvalidOperationException("Unsupported exact property " + p.propertyType);
        }
    }
    static string RenderValue(string path)
    {
        switch (path)
        {
            case "m_AmbientMode": return ((int)RenderSettings.ambientMode).ToString();
            case "m_AmbientSkyColor": return JsonUtility.ToJson(RenderSettings.ambientSkyColor);
            case "m_AmbientEquatorColor": return JsonUtility.ToJson(RenderSettings.ambientEquatorColor);
            case "m_AmbientGroundColor": return JsonUtility.ToJson(RenderSettings.ambientGroundColor);
            case "m_AmbientIntensity": return F(RenderSettings.ambientIntensity);
            case "m_Sun": return "{fileID: " + (RenderSettings.sun ? unchecked((long)GlobalObjectId.GetGlobalObjectIdSlow(RenderSettings.sun).targetObjectId) : 0) + "}";
            case "m_Fog": return RenderSettings.fog ? "1" : "0";
            case "m_FogMode": return ((int)RenderSettings.fogMode).ToString();
            case "m_FogDensity": return F(RenderSettings.fogDensity);
            case "m_FogColor": return JsonUtility.ToJson(RenderSettings.fogColor);
            case "m_LinearFogStart": return F(RenderSettings.fogStartDistance);
            case "m_LinearFogEnd": return F(RenderSettings.fogEndDistance);
            default: throw new InvalidOperationException("Unsupported RenderSettings field " + path);
        }
    }
    static bool ValueEqual(string a, string b)
    {
        if (a.StartsWith("{fileID:") || b.StartsWith("{fileID:")) return a == b;
        if (a.StartsWith("{"))
        {
            Color x = JsonUtility.FromJson<Color>(a), y = JsonUtility.FromJson<Color>(b);
            return Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b)+Mathf.Abs(x.a-y.a) < .00005f;
        }
        return double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double av) &&
            double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double bv) && Math.Abs(av-bv)<.00005;
    }
    static Material[] Materials(Scene scene) => All<Renderer>(scene).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToArray();
    static void ValidateAssets(GameObject root, string prefabPath)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) Require(filter.sharedMesh, "New mesh loaded " + PathOf(filter.transform));
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SkinnedMeshRenderer skin) Require(skin.sharedMesh, "New skinned mesh loaded");
            Require(renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m && m.shader), "New renderer material and shader loaded " + renderer.name);
        }
        foreach (string path in AssetDatabase.GetDependencies(prefabPath, true))
        {
            if (path == "Resources/unity_builtin_extra" || path == "Library/unity default resources") continue;
            Require(AssetDatabase.LoadMainAssetAtPath(path), "Dependency imported " + path);
            if (Path.GetExtension(path) != ".mat" && Path.GetExtension(path) != ".prefab" && Path.GetExtension(path) != ".asset") continue;
            string text = File.ReadAllText(path);
            foreach (Match match in Regex.Matches(text, @"guid: ([a-fA-F0-9]{32})"))
            {
                string guid = match.Groups[1].Value;
                if (guid.StartsWith("0000000000000000")) continue;
                Require(!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)), "Serialized dependency GUID resolved " + guid);
            }
        }
        foreach (var material in root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
        {
            var serialized = new SerializedObject(material);
            var textureEnvs = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
            if (textureEnvs != null) for (int i = 0; i < textureEnvs.arraySize; i++)
            {
                var texture = textureEnvs.GetArrayElementAtIndex(i).FindPropertyRelative("second.m_Texture");
                Require(texture == null || texture.objectReferenceValue || texture.objectReferenceInstanceIDValue == 0,
                    "No missing serialized texture " + material.name + " #" + i);
            }
        }
    }
    static void ValidateWater(GameObject root)
    {
        var water = root.GetComponentsInChildren<WaterAudio>(true);
        Require(water.Length == 1, "Exactly one integrated WaterAudio");
        var sources = water[0].GetComponentsInChildren<AudioSource>(true);
        Require(sources.Length == 1, "WaterAudio owns exactly one AudioSource");
        var audio = sources[0];
        Require(audio.clip && audio.clip.channels == 1 && audio.loop && audio.spatialBlend == 1f && audio.dopplerLevel == 0f,
            "Integrated spatial looping mono water clip configured");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Chapter01/CanalFlow.mat");
        Require(material && material.shader && material.passCount == 1 && material.GetTexture("_FlowMap"), "Integrated canal remains one pass with RGBA flow map");
        CheckShader(material.shader, "CanalFlow shader");
    }
    static void ValidatePortal(GameObject root)
    {
        var gates = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "量水道尽头 · 残规门庭").ToArray();
        Require(gates.Length == 1, "Exactly one portal frame");
        var gate = gates[0]; var colliders = gate.GetComponentsInChildren<Collider>(true);
        Require(colliders.Length == 3 && colliders.All(c => c is BoxCollider && c.enabled && !c.isTrigger), "Portal has exactly three enabled solid box colliders");
        foreach (float x in new[] {-.55f,0f,.55f}) foreach (float y in new[] {.4f,1.3f,2.4f})
        {
            Vector3 point = gate.TransformPoint(new Vector3(x,y,0));
            Require(colliders.All(c => Vector3.Distance(c.ClosestPoint(point),point) > .2f), "Portal aperture sphere clearance " + x + "," + y);
        }
        var landings=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="侧置门庭 · 石台").ToArray();
        Require(landings.Length==1 && landings[0].GetComponents<Collider>().Length==1 &&
            landings[0].GetComponent<Collider>().enabled && !landings[0].GetComponent<Collider>().isTrigger,
            "Portal landing retains exactly one enabled solid collider");
    }
    static void Photo(Scene scene, string filename)
    {
        var go = new GameObject("MergedAcceptancePhoto"); SceneManager.MoveGameObjectToScene(go,scene);
        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
        camera.transform.position = new Vector3(3.3f,1.65f,53); camera.transform.LookAt(new Vector3(0,1.8f,64));
        camera.fieldOfView=72; camera.nearClipPlane=.03f; camera.farClipPlane=320; camera.allowHDR=false; camera.allowMSAA=false;
        camera.clearFlags=CameraClearFlags.Skybox;
        var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=true;
        data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
        var old=RenderTexture.active; var rt=new RenderTexture(960,600,24,RenderTextureFormat.ARGB32);
        var image=new Texture2D(960,600,TextureFormat.RGB24,false);
        try
        {
            rt.Create();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,filename),image.EncodeToPNG());
            Require(image.GetPixels32().Count(c => c.r>4 || c.g>4 || c.b>4)>960*600*.1f, "Rendered photograph is not black " + filename);
        }
        finally { RenderTexture.active=old;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go); }
    }
    static void CheckShader(Shader shader,string label)
    {
        Require(shader && shader.name != "Hidden/InternalErrorShader", label + " exists");
        foreach (var message in ShaderUtil.GetShaderMessages(shader)) Require(message.severity.ToString() != "Error", label + ": " + message.message);
    }
    static Dictionary<string,string> ScriptBlocks(string text)
    {
        var result=new Dictionary<string,string>();
        foreach(Match match in Regex.Matches(text,@"^--- !u!114 &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)",RegexOptions.Multiline|RegexOptions.Singleline))
            result.Add(match.Groups[1].Value,match.Value);
        return result;
    }
    static string[] OrphanScripts(string text)
    {
        var referenced=new HashSet<string>(Regex.Matches(text,@"component: \{fileID: (-?\d+)\}").Cast<Match>().Select(m=>m.Groups[1].Value));
        return ScriptBlocks(text).Keys.Where(id=>!referenced.Contains(id)).ToArray();
    }
    static void Log(string message,string stack,LogType type)
    {
        if(capture==null)return;
        if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)capture.errors.Add(message);
        else if(type==LogType.Warning)capture.warnings.Add(message);
    }
    static void Require(bool passed,string name,string detail="")
    {
        Checks.Add(new Check{name=name,passed=passed,detail=detail});
        if(!passed)throw new InvalidOperationException(name+" "+detail);
    }
    static string Sha(string path)
    {
        using(var algorithm=SHA256.Create())return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
    }
}
