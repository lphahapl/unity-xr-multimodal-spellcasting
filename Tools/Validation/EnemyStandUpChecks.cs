using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyStandUpChecks
{
    const string Key = "NarutoVR.EnemyStandUpChecks";
    const string Folder = "Logs/enemy-standup";
    const string StandUpFbx = "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_StandUp.fbx";
    const string DazedFbx = "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_DazedSeated.fbx";
    const string CanJiOuController = "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller";
    const string ZhiHuoController = "Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Combat.controller";
    const string CanJiOuPrefab = "Assets/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab";
    const string ZhiHuoPrefab = "Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab";

    static readonly List<string> failures = new();
    static int checks;
    static double deadline;

    static void Log(string line) => File.AppendAllText(Path.Combine(Folder, "result.txt"), line + "\n");
    static void Check(bool ok, string msg)
    {
        checks++;
        Log((ok ? "PASS " : "FAIL ") + msg);
        if (!ok) failures.Add(msg);
    }

    static EnemyStandUpChecks() => EditorApplication.playModeStateChanged += OnPlayState;

    public static void Run()
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run EnemyStandUpChecks only inside a TEMP batchmode project.");

        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "result.txt"), DateTimeOffset.Now.ToString("o") + "\n");
        EditorSceneManager.OpenScene("Assets/Scene/EnemyNavigationTest.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + ".code", 1);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Watchdog;
            Verify();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetInt(Key + ".code", 1));
        }
    }

    static void Watchdog()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        Log("FAIL watchdog timeout");
        EditorApplication.update -= Watchdog;
        EditorApplication.ExitPlaymode();
    }

    static void Verify()
    {
        try
        {
            failures.Clear();
            checks = 0;

            VerifyAssetsAndControllers();
            VerifyEnemyLifecycle(CanJiOuPrefab, "CanJiOu");
            VerifyEnemyLifecycle(ZhiHuoPrefab, "ZhiHuoCanJi");

            string parryNote = "skipped";
            try
            {
                EnemyFireCoreParryChecks.Run();
                parryNote = "PASS";
            }
            catch (Exception e)
            {
                parryNote = "FAIL: " + e.Message;
                Check(false, "EnemyFireCoreParryChecks: " + e);
            }
            Log("EnemyFireCoreParryChecks: " + parryNote);

            string setupReport = EnemySetupAndChecks.Run();
            Check(setupReport.StartsWith("PASSED"), "EnemySetupAndChecks.Run -> " + setupReport);

            Log($"{checks} checks / {failures.Count} failures");
            Log(failures.Count == 0 ? "PASS END" : "FAIL END");
            SessionState.SetInt(Key + ".code", failures.Count == 0 ? 0 : 1);
        }
        catch (Exception ex)
        {
            Log("FAIL " + ex);
            Debug.LogException(ex);
        }
        finally
        {
            EditorApplication.update -= Watchdog;
            EditorApplication.ExitPlaymode();
        }
    }

    static void VerifyAssetsAndControllers()
    {
        var importer = AssetImporter.GetAtPath(StandUpFbx) as ModelImporter;
        Check(importer != null, "StandUp FBX ModelImporter exists");
        if (importer != null)
        {
            Check(importer.animationType == ModelImporterAnimationType.Human, "StandUp FBX imported as Humanoid");
            Check(importer.importAnimation, "StandUp FBX imports animation");
        }

        var standClip = AssetDatabase.LoadAllAssetsAtPath(StandUpFbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        var dazedClip = AssetDatabase.LoadAllAssetsAtPath(DazedFbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        Check(standClip != null, "StandUp AnimationClip loaded: " + (standClip ? standClip.name : "null"));
        Check(dazedClip != null, "DazedSeated AnimationClip loaded: " + (dazedClip ? dazedClip.name : "null"));
        if (standClip != null)
        {
            Check(!standClip.isLooping, "StandUp clip is single-shot (!isLooping)");
            Check(standClip.length >= 2.5f && standClip.length <= 2.9f, FormattableString.Invariant($"StandUp clip length ~2.7s (actual {standClip.length:F3}s @ {standClip.frameRate}fps)"));
        }

        var c1 = AssetDatabase.LoadAssetAtPath<AnimatorController>(CanJiOuController);
        var c2 = AssetDatabase.LoadAssetAtPath<AnimatorController>(ZhiHuoController);
        Check(c1 != null && c2 != null, "Both AnimatorControllers load cleanly");

        foreach (var (name, ctrl) in new[] { ("CanJiOu", c1), ("ZhiHuoCanJi", c2) })
        {
            var root = ctrl.layers[0].stateMachine;
            var seated = root.states.FirstOrDefault(s => s.state.name == "Seated").state;
            var standUp = root.states.FirstOrDefault(s => s.state.name == "StandUp").state;
            Check(seated != null && seated.motion == dazedClip, $"{name}: Seated shares CanJiOu_DazedSeated");
            Check(standUp != null && standUp.motion == standClip, $"{name}: StandUp shares CanJiOu_StandUp");
            Check(seated != null && seated.transitions.Any(t => t.destinationState == standUp), $"{name}: Seated -> StandUp transition wired");
            Check(standUp != null && standUp.transitions.Any(t => t.destinationState != null && t.destinationState.name.Contains("Idle")), $"{name}: StandUp -> Idle transition wired");
        }
    }

    static void VerifyEnemyLifecycle(string prefabPath, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Check(prefab != null, $"{label}: prefab loads ({prefabPath})");
        GameObject holder = null, go = null;
        try
        {
            holder = new GameObject($"StandUpTest_{label}");
            holder.SetActive(false);
            go = Object.Instantiate(prefab, new Vector3(100, 0, 100), Quaternion.identity, holder.transform);
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent) agent.enabled = false;
            holder.SetActive(true);

            var enemy = go.GetComponent<EnemyController>();
            enemy.tickAutomatically = false;
            enemy.checkLineOfSight = false;
            enemy.InitializeSpawn(null, true);
            enemy.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            enemy.animator.Rebind();
            enemy.animator.Update(0);

            Check(enemy.ValidateAnimator(out var reason), $"{label}: ValidateAnimator -> {reason}");
            Check(enemy.Entity.SupportsReactions, $"{label}: SupportsReactions == true");

            float standHeadY = enemy.animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            float standHipsY = enemy.animator.GetBoneTransform(HumanBodyBones.Hips).position.y;
            Log(FormattableString.Invariant($"POSE {label} Idle: HipsY={standHipsY:F3} HeadY={standHeadY:F3}"));

            // 1. Poise break -> Seated -> StandUp -> Idle (using prefab's real reactionTimeout = 8s!)
            bool broken = enemy.Entity.ReceiveHit("qa-poise-" + label, 0, enemy.Entity.Combat.MaxPoise);
            Check(broken && enemy.CurrentBehaviour == EnemyController.Behaviour.Seated && enemy.Entity.Combat.Broken, $"{label}: poise break enters Behaviour.Seated");

            const float dt = 0.05f;
            bool sawSeated = false, sawStandUp = false, lockedInStandUp = true, poiseZeroInStandUp = true, recovered = false;
            float seatedMinHeadY = float.MaxValue, seatedMinFootY = float.MaxValue;
            float standUpEndHeadY = 0f, standUpMinFootY = float.MaxValue, lowestAt = 0f;
            float elapsed = 0f;

            var hips = enemy.animator.GetBoneTransform(HumanBodyBones.Hips);
            var head = enemy.animator.GetBoneTransform(HumanBodyBones.Head);
            var lFoot = enemy.animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rFoot = enemy.animator.GetBoneTransform(HumanBodyBones.RightFoot);

            for (int i = 0; i < 180; i++)
            {
                elapsed += dt;
                enemy.Tick(dt);
                enemy.animator.Update(dt);
                var st = enemy.animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName("Base Layer.Seated"))
                {
                    if (!sawSeated) Log(FormattableString.Invariant($"STATE {label} entered Seated at t={elapsed:F2}s HipsY={hips.position.y:F3} HeadY={head.position.y:F3}"));
                    sawSeated = true;
                    seatedMinHeadY = Mathf.Min(seatedMinHeadY, head.position.y);
                    seatedMinFootY = Mathf.Min(seatedMinFootY, Mathf.Min(lFoot.position.y, rFoot.position.y));
                }
                if (st.IsName("Base Layer.StandUp"))
                {
                    if (!sawStandUp) Log(FormattableString.Invariant($"STATE {label} entered StandUp at t={elapsed:F2}s HipsY={hips.position.y:F3} HeadY={head.position.y:F3}"));
                    sawStandUp = true;
                    if (enemy.CurrentBehaviour != EnemyController.Behaviour.Seated) lockedInStandUp = false;
                    if (!enemy.Entity.Combat.Broken || enemy.Entity.Combat.Poise > 0f) poiseZeroInStandUp = false;
                    enemy.Entity.ReadReactionAnimation(EnemyReaction.Seated, out bool active, out _);
                    if (!active) lockedInStandUp = false;
                    standUpEndHeadY = head.position.y;
                    float footMin = Mathf.Min(lFoot.position.y, rFoot.position.y);
                    if (footMin < standUpMinFootY)
                    {
                        standUpMinFootY = footMin;
                        lowestAt = st.normalizedTime;
                    }
                }
                if (sawStandUp && enemy.CurrentBehaviour != EnemyController.Behaviour.Seated)
                {
                    recovered = true;
                    Log(FormattableString.Invariant($"STATE {label} recovered to {enemy.CurrentBehaviour} at t={elapsed:F2}s HipsY={hips.position.y:F3} HeadY={head.position.y:F3} FootY=({lFoot.position.y:F3},{rFoot.position.y:F3})"));
                    break;
                }
            }

            Check(sawSeated && sawStandUp, $"{label}: played both Seated and StandUp (seated={sawSeated}, standUp={sawStandUp})");
            Check(lockedInStandUp && poiseZeroInStandUp, $"{label}: remained locked and broken throughout StandUp (lock={lockedInStandUp}, zeroPoise={poiseZeroInStandUp})");
            Check(recovered && elapsed < enemy.reactionTimeout, FormattableString.Invariant($"{label}: completed StandUp before reactionTimeout={enemy.reactionTimeout}s (elapsed={elapsed:F2}s)"));
            Check(!enemy.Entity.Combat.Broken && Mathf.Approximately(enemy.Entity.Combat.Poise, enemy.Entity.Combat.MaxPoise), $"{label}: poise restored after StandUp completion");
            Check(seatedMinHeadY < standHeadY - 0.35f, FormattableString.Invariant($"{label}: visually lowered while seated (idleHead={standHeadY:F2}m -> seatedMin={seatedMinHeadY:F2}m)"));
            Check(standUpEndHeadY >= standHeadY * 0.80f && Mathf.Min(seatedMinFootY, standUpMinFootY) >= -0.04f,
                FormattableString.Invariant($"{label}: seated and standing foot heights (seated={seatedMinFootY:F3}m, standUp={standUpMinFootY:F3}m at {lowestAt:F3}, head={standUpEndHeadY:F2}m vs idle={standHeadY:F2}m)"));

            // Also check CanJiOu SkinnedMeshRenderer bounds vs StandUp
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in smrs)
                Log(FormattableString.Invariant($"BOUNDS {label} {s.name}: updateOffscreen={s.updateWhenOffscreen} center={s.localBounds.center} ext={s.localBounds.extents}"));

            // 2. Lethal hit mid-StandUp must interrupt into Dead immediately
            enemy.ResetForSpawn();
            enemy.animator.Rebind();
            enemy.animator.Update(0);
            enemy.Entity.ReceiveHit("qa-poise2-" + label, 0, enemy.Entity.Combat.MaxPoise);
            bool reachedStandUp = false;
            for (int i = 0; i < 160; i++)
            {
                enemy.Tick(dt);
                enemy.animator.Update(dt);
                if (enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.StandUp")) { reachedStandUp = true; break; }
            }
            Check(reachedStandUp, $"{label}: reached StandUp before lethal interrupt test");
            bool killed = enemy.Entity.ReceiveHit("qa-kill-" + label, 9999, 0);
            Check(killed && enemy.IsDead && enemy.CurrentBehaviour == EnemyController.Behaviour.Dead, $"{label}: lethal hit mid-StandUp triggers Dead");
            for (int i = 0; i < 25; i++) { enemy.Tick(dt); enemy.animator.Update(dt); }
            Check(enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Dead") && enemy.animator.GetBool("dead"), $"{label}: stays in Dead after interrupting StandUp");
        }
        finally
        {
            if (go) Object.DestroyImmediate(go);
            if (holder) Object.DestroyImmediate(holder);
        }
    }
}
