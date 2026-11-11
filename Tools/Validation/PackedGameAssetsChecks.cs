// Copy to Assets/scripts/logic/Editor of a temporary project.
// Unity -batchmode -nographics -buildTarget StandaloneWindows64 -projectPath <copy> -executeMethod PackedGameAssetsChecks.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

[InitializeOnLoad]
public static class PackedGameAssetsChecks
{
    const string Key = "NarutoVR.PackedGameAssets";
    const string Report = "Logs/packed-game-assets.txt";
    static readonly string[] Expected = {"buff.zi", "buff.voice", "fireball", "buff.hidden", "thunder.sword"};

    static PackedGameAssetsChecks() => EditorApplication.playModeStateChanged += ModeChanged;
    static void Log(string value) => File.AppendAllText(Report, value + Environment.NewLine);
    static void Require(bool condition, string error) { if (!condition) throw new InvalidDataException(error); }

    public static void Run()
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && Path.GetFullPath(Application.dataPath).StartsWith(temp, StringComparison.OrdinalIgnoreCase), "仅限临时项目批处理");
        Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64, "请使用 StandaloneWindows64");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, DateTimeOffset.Now.ToString("o") + Environment.NewLine);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        int packed = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
        Require(packed >= 0, "未找到 Use Existing Build");
        SessionState.SetInt(Key + ".builder", settings.ActivePlayModeDataBuilderIndex);
        foreach (string side in new[]{"Left", "Right"})
        {
            string path = "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_Turn"+side+"90.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(clip => clip.name == "CanJiOu_Turn"+side+"90");
            Log($"SOURCE {path} importer.loopTime={importer.clipAnimations[0].loopTime} clip.isLooping={imported.isLooping}");
        }
        AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        Require(result != null && string.IsNullOrEmpty(result.Error), "Addressables 构建失败 " + result?.Error);
        Log($"Unity {Application.unityVersion} / {EditorUserBuildSettings.activeBuildTarget} / version {settings.PlayerBuildVersion}");
        string catalog = Path.Combine(Path.GetDirectoryName(result.OutputPath), "catalog.bin");
        HashFile(result.OutputPath);
        HashFile(catalog);
        foreach (var bundle in result.AssetBundleBuildResults.OrderBy(entry => entry.FilePath, StringComparer.Ordinal))
            HashFile(bundle.FilePath);
        settings.ActivePlayModeDataBuilderIndex = packed;
        AssetDatabase.SaveAssets();
        var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        const string scene = "Assets/Scene/QA_PackedGameAssets.unity";
        Require(EditorSceneManager.SaveScene(empty, scene), "无法保存隔离的 Play 场景");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + ".code", 1);
        EditorApplication.EnterPlaymode();
    }

    static void HashFile(string path)
    {
        using var data = File.OpenRead(path);
        Require(data.Length > 0, "空内容 " + path);
        using var sha = SHA256.Create();
        string hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        Log($"SHA256 {hash}  bytes={data.Length}  {Path.GetFileName(path)}");
    }

    static double deadline;
    static void ModeChanged(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Timeout;
            VerifyPackedContent();
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".builder", 0);
            AssetDatabase.SaveAssets();
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.Exit(SessionState.GetInt(Key + ".code", 1));
        }
    }
    static void Timeout()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Timeout;
        Log("FAIL timeout while reading packed content");
        EditorApplication.ExitPlaymode();
    }
    static async void VerifyPackedContent()
    {
        var leases = new List<IDisposable>();
        try
        {
            // Only the two address maps come from the editor. The six prototypes go through
            // the shared runtime loader after checking their providers and bundle dependencies.
            var skills = AssetDatabase.LoadAssetAtPath<SkillResourceManifest>("Assets/ScriptableObjects/技能目录配置/首章技能资源清单.asset");
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset");
            Require(skills && enemies && skills.entries.Length == Expected.Length && skills.Validate() == null, "缺少正式目录");
            foreach (string id in Expected)
            {
                Require(skills.TryGet(id, out var entry), "技能 ID 不在正式目录 " + id);
                string provider = await PackedProvider<SkillDefinition>(entry.address);
                var lease = await skills.AcquireAsync(id);
                leases.Add(lease);
                var skill = lease.Asset;
                Require(entry.Matches(skill) && skill.effectDefinition, "技能原型或效果缺失 " + id);
                var effect = skill.effectDefinition;
                Require(skill.GetType().Assembly.GetName().Name == "NarutoVR.Game.Runtime" &&
                    effect.GetType().Assembly == skill.GetType().Assembly, "技能或效果的脚本程序集错误 " + id);
                Log($"LOAD {id}  {entry.address}  {provider}  {skill.name} => {effect.GetType().Name}/{effect.name}");
            }
            Require(enemies.TryResolve(typeof(GameObject), "canjiou", out string key), "敌人原型未注册");
            string enemyProvider = await PackedProvider<GameObject>(key);
            var enemy = await SharedAssetLoader.AcquireAsync<GameObject>(enemies, "canjiou");
            leases.Add(enemy);
            var obj = enemy.Asset;
            var controller = obj.GetComponentInChildren<EnemyController>(true);
            Require(controller && controller.GetType().Assembly.GetName().Name == "NarutoVR.Game.Runtime", "敌人原型未接正确脚本");
            var parts = obj.GetComponentsInChildren<Transform>(true);
            Require(parts.All(part => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(part.gameObject) == 0), "敌人预制体有丢失的脚本");
            Log($"LOAD canjiou  {key}  {enemyProvider}  {obj.name} => {controller.GetType().Name}  nodes={parts.Length}");
            var animator = obj.GetComponentInChildren<Animator>(true);
            Require(animator && animator.runtimeAnimatorController, "打包敌人缺少动画控制器");
            var rig = new GameObject("PackedCanJiOuPoseProbe");
            rig.SetActive(false);
            try
            {
                var clone = UnityEngine.Object.Instantiate(obj, rig.transform);
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var agent in clone.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = false;
                var playback = clone.GetComponentInChildren<Animator>(true);
                playback.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // The room prefab is authored inactive. Its normal spawn path activates it explicitly.
                clone.SetActive(true);
                rig.SetActive(true);
                Require(playback.isActiveAndEnabled, "打包骨骼采样器未激活");
                playback.Update(0);
                var bones = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(renderer => renderer.bones)
                    .Where(bone => bone).Distinct().ToArray();
                Require(bones.Length > 2, "打包敌人缺少有效骨骼");
                foreach (string side in new[]{"Left", "Right"})
                {
                    string clipName = "CanJiOu_Turn" + side + "90";
                    var clip = animator.runtimeAnimatorController.animationClips.Single(c => c && c.name == clipName);
                    Require(clip.isLooping && clip.length > 0, "打包转身动画无法持续循环 " + clipName);
                    string state = "Base Layer.Turn.Turn" + side;
                    playback.SetInteger("motionState", 3);
                    playback.SetFloat("angle", side == "Left" ? -90 : 90);
                    playback.Play(state, 0, 1.10f);
                    playback.Update(0);
                    var before = bones.Select(bone => bone.localRotation).ToArray();
                    playback.Update(clip.length * .25f);
                    float motion = bones.Select((bone, index) => Quaternion.Angle(before[index], bone.localRotation)).Max();
                    var active = playback.GetCurrentAnimatorClipInfo(0);
                    Log($"POSE {state} clip={string.Join(",",active.Select(info=>info.clip.name))} normalized={playback.GetCurrentAnimatorStateInfo(0).normalizedTime:F3} movement={motion:F2}deg");
                    Require(playback.GetCurrentAnimatorStateInfo(0).IsName(state) && motion > .5f, "第一个周期后打包骨骼不动 " + clipName);
                    Log($"CLIP {clipName} loop={clip.isLooping} duration={clip.length:F3}s bones={bones.Length} second-cycle delta={motion:F2}deg");
                }
            }
            finally { UnityEngine.Object.Destroy(rig); }
            Log("PASS END");
            SessionState.SetInt(Key + ".code", 0);
        }
        catch (Exception error) { Log("FAIL " + error); Debug.LogException(error); }
        finally
        {
            foreach (var lease in leases) lease.Dispose();
            EditorApplication.update -= Timeout;
            EditorApplication.ExitPlaymode();
        }
    }

    static async Task<string> PackedProvider<T>(string key)
    {
        var lookup = Addressables.LoadResourceLocationsAsync(key, typeof(T));
        try
        {
            var locations = await lookup.Task;
            var location = locations.Single();
            Require(location.ProviderId == typeof(BundledAssetProvider).FullName && location.HasDependencies &&
                location.Dependencies.Any(item => item.InternalId.IndexOf(".bundle", StringComparison.OrdinalIgnoreCase) >= 0),
                $"资源 {key} 来源错误 {location.ProviderId}");
            return location.ProviderId + " [" + string.Join(", ", location.Dependencies.Select(item => Path.GetFileName(item.InternalId))) + "]";
        }
        finally { Addressables.Release(lookup); }
    }
}
