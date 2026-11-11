using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Obj=UnityEngine.Object;

// Batch-only fixture. Copy into the isolated project's Editor folder.
[InitializeOnLoad]
public static class SkillAssemblyPlayFixture
{
    const string Key = "NarutoVR.SkillAssemblyPlayFixture.";
    static string Root => Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
    static string SaveDirectory => Path.Combine(Root,"Temp","SkillAssemblyPlayerSave");
    static string SaveName => SessionState.GetString(Key+"save","");
    static string Mode => SessionState.GetString(Key + "mode", "");
    static string Output => SessionState.GetString(Key + "out", "");
    static readonly List<string> checks = new();
    static bool entered, earlyObserved, bootGuard, voiceQueued;
    static LinearRpgController departingOwner;
    static AsyncOperation sceneReload;
    static double deadline, preReadyVoiceEndedAt;
    static int frames;
    static string savePath, failure;
    static Task<int> leaseChecks;
    [Serializable] public class Report
    {
        public bool passed; public string phase, project, scene, engine, save, error, finished;
        public string[] assertions;
    }
    static SkillAssemblyPlayFixture()
    {
        SceneManager.sceneLoaded += (scene, _) => {
            if (!EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key,false) ||
                earlyObserved || !scene.name.StartsWith("QA_Resource_")) return;
            try
            {
                var rpg=Obj.FindFirstObjectByType<LinearRpgController>();
                earlyObserved = true;
                if (Mode=="candidate" || Mode=="destroy" || Mode=="retry" || Mode=="unauthorized")
                {
                    Ensure(rpg && !rpg.CanAct, "before Start: gameplay gated");
                    Ensure(!rpg.DebugCast("thunder.sword"), "before Ready: DebugCast refused");
                    preReadyVoiceEndedAt = Time.realtimeSinceStartupAsDouble;
                    rpg.skills.ReceiveVoice("雷光剑",preReadyVoiceEndedAt);
                    bootGuard = true; voiceQueued = true;
                    if (Mode=="destroy") {
                        departingOwner = rpg;
                        departingOwner.enabled = false;
                        sceneReload = EditorSceneManager.LoadSceneAsyncInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                    }
                }
            }
            catch(Exception e) { failure=e.ToString(); }
        };
        EditorApplication.playModeStateChanged += state => {
            if(!SessionState.GetBool(Key,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode) {
                entered=true; deadline=EditorApplication.timeSinceStartup+75;
                EditorApplication.update+=Tick;
            }
            else if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key+"done",false)) {
                SessionState.SetBool(Key,false); EditorApplication.Exit(SessionState.GetInt(Key+"exit",1));
            }
        };
    }
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();
        string Arg(string key) {
            int at=Array.IndexOf(args,key);
            return at>=0 && at+1<args.Length ? args[at+1] : throw new ArgumentException(key);
        }
        string temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!Application.isBatchMode || !Root.StartsWith(temporary,StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(Root,".skill-assembly-test-project"))) throw new Exception("An explicitly marked isolated copy in TEMP is required");
        var mode=Arg("-resourcePhase"); var output=Path.GetFullPath(Arg("-resourceResult"));
        if(!new[]{"baseline","candidate","legacy","destroy","retry","unauthorized"}.Contains(mode)) throw new Exception(mode);
        if(File.Exists(output)) throw new Exception("An existing evidence file would be overwritten");
        if(Directory.GetFiles("Assets/XR","*.asset",SearchOption.AllDirectories)
            .Any(p=>System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(p),@"m_InitManagerOnStart:\s*1\b")))
            throw new Exception("XR auto-init must be disabled in clone");
        if(!File.ReadAllText("Assets/scripts/logic/Rpg/LinearRpgController.cs").Contains("SkillAssemblyPlayerSave"))
            throw new Exception("Isolate the storage directory before running the fixture");
        SessionState.SetString(Key+"save","qa-skill-resource-"+Guid.NewGuid().ToString("N")+".json");
        savePath=Path.Combine(SaveDirectory,SaveName);
        Directory.CreateDirectory(SaveDirectory);
        if(mode=="baseline" || mode=="candidate") SessionState.SetInt(Key+"dataChecks",RpgDataChecks.Run());
        var scene=EditorSceneManager.OpenScene(mode=="legacy"?"Assets/Scene/LinearRpgTest.unity":"Assets/Scene/Chapter01_Awakening.unity");
        var owner=Obj.FindFirstObjectByType<LinearRpgController>();
        if(!owner) throw new Exception("No RPG in scene");
        // The candidate opts in to v2 loading in the actual chapter. Verify it before editing the copy.
        if(mode!="baseline" && mode!="legacy" && !owner.autoLoad)throw new Exception("Original Chapter01 autoLoad is disabled");
        owner.saveFileName=SaveName; owner.autoLoad=mode!="baseline" && mode!="legacy";
        if(mode=="baseline" && File.Exists(savePath)) throw new Exception("Baseline requires empty isolated save");
        if(owner.autoLoad) File.Copy(Path.Combine(Path.GetDirectoryName(output),"baseline-v2.json"),savePath,true);
        if(mode=="unauthorized") {
            var chapter=(OpeningTrialChapter)owner.chapter;
            foreach(var section in chapter.sections)
                section.trialSkills=section.trialSkills.Where(id=>id!="thunder.sword").ToArray();
            EditorUtility.SetDirty(chapter);
        }
        if (mode == "candidate")
        {
            var view = owner.skills.view;
            var serialized = new SerializedObject(view);
            var rows = serialized.FindProperty("shortcuts");
            rows.arraySize = 1;
            var row = rows.GetArrayElementAtIndex(0);
            row.FindPropertyRelative("skillId").stringValue = "thunder.sword";
            row.FindPropertyRelative("key").intValue = (int)UnityEngine.InputSystem.Key.F12;
            serialized.FindProperty("showDesktopControls").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        var voice = owner.skills.voice;
        if(voice) { voice.autoStartInEditor=false; }
        if(mode=="retry") {
            var assetField=owner.GetType().GetField("skillResources");
            var asset=assetField.GetValue(owner) as ScriptableObject;
            var faulty=Obj.Instantiate(asset); var so=new SerializedObject(faulty);
            so.FindProperty("entries").GetArrayElementAtIndex(0).FindPropertyRelative("address").stringValue="fixture/no-such-skill-20261110";
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(faulty,"Assets/QA_Resource_bad_"+Guid.NewGuid().ToString("N")+".asset");
            assetField.SetValue(owner,faulty);
        }
        EditorUtility.SetDirty(owner); if(voice)EditorUtility.SetDirty(voice);
        if(!EditorSceneManager.SaveScene(scene,"Assets/Scene/QA_Resource_"+mode+".unity")) throw new Exception("Save isolated scene copy");
        SessionState.SetString(Key+"mode",mode);SessionState.SetString(Key+"out",output);
        SessionState.SetBool(Key+"done",false); SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static RpgProgressData ReadProgress(LinearRpgController rpg)
    {
        var export=rpg.GetType().GetMethod("CaptureProgressSnapshot");
        return (RpgProgressData)(export!=null?export.Invoke(rpg,null):rpg.GetType().GetProperty("Progress").GetValue(rpg));
    }
    static void Ensure(bool ok,string label) { if(!ok)throw new Exception(label); checks.Add(label); }
    static void Tick()
    {
        if(!entered || !Application.isPlaying)return;
        try {
            if(failure!=null)throw new Exception(failure);
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Waiting for "+Mode);
            frames++;
            var rpg=Obj.FindFirstObjectByType<LinearRpgController>();
            savePath=Path.Combine(SaveDirectory,SaveName);
            if(Mode=="destroy") {
                if(sceneReload==null || !sceneReload.isDone || !rpg || !rpg.CanAct || rpg.skills.Engine==null)return;
                Ensure(earlyObserved && bootGuard && voiceQueued && !departingOwner,"leaving during assembly destroys the old owner");
                Ensure(ReadProgress(rpg).currency==17 && ReadProgress(rpg).checkpoint.mana==37,"replacement player restored the original v2 snapshot");
                Ensure(!rpg.skills.effects.HasOccupiedHand && rpg.skills.effects.ActivationCount==0 && string.IsNullOrEmpty(rpg.skills.Engine.PendingSkillId),"new scene never replays departing player's input");
                Ensure(SameBaseline(),"leaving the scene in flight preserved v2 file");
                Finish(null); return;
            }
            if(Mode=="retry" && !retried) {
                if(!rpg || rpg.CanAct || !File.Exists(savePath))throw new Exception("failure gate/save guard");
                if(!(bool)rpg.GetType().GetProperty("CanRetrySkillAssembly").GetValue(rpg))return;
                Ensure(SameBaseline(),"missing address preserved valid save");
                var f=rpg.GetType().GetField("skillResources");
                f.SetValue(rpg,AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath("701b33e06d6045b8881322435e85659c")));
                rpg.GetType().GetMethod("RetrySkillAssembly").Invoke(rpg,null); retried=true;
            }
            if(!rpg || !rpg.CanAct || rpg.skills.Engine==null || frames<12)return;
            if(!readyChecked) {
                readyChecked=true;
                var xr=new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(xr);
                Ensure(xr.All(d=>!d.running),"no active XR display");
                Ensure(Path.GetFullPath(rpg.SavePath)==Path.GetFullPath(savePath),"unique isolated save path");
                Ensure(ReferenceEquals(rpg.skills.ActivationPolicy,rpg),"RPG owns activation and cooldowns");
                if(Mode=="legacy"){
                    Ensure(rpg.skills.ConfiguredSkills.Count>0,"legacy synchronous catalog usable"); Finish(null); return;
                }
                Ensure(rpg.chapter is OpeningTrialChapter,"production chapter controller");
                Ensure(rpg.IsSkillDiscovered("thunder.sword")&&!rpg.IsSkillUnlocked("thunder.sword"),"sword is discovered but not permanently unlocked");
                if(Mode=="unauthorized") {
                    Ensure(!rpg.IsSkillAvailable("thunder.sword")&&!rpg.DebugCast("thunder.sword"),"loaded sword denied without chapter permission");
                } else Ensure(rpg.IsSkillAvailable("thunder.sword"),"sword granted by chapter trial");
                Ensure(rpg.skills.ConfiguredSkills.Count(s=>s.skillId=="thunder.sword")==1,"exactly one sword recipe bound");
                if(Mode=="baseline") {
                    var openingVitals = rpg.Player.CaptureCheckpoint(rpg.GameTime);
                    openingVitals.mana = 37;
                    Ensure(rpg.Player.RestoreCheckpoint(openingVitals, rpg.GameTime, out var reason),"seed player mana before baseline writer: " + reason);
                    var originalProgress = rpg.GetType().GetProperty("Progress");
                    if(originalProgress != null) ((RpgProgressData)originalProgress.GetValue(rpg)).currency = 17;
                    else {
                        var growth = rpg.GetType().GetProperty("Growth").GetValue(rpg);
                        growth.GetType().GetMethod("CreditCurrency").Invoke(growth,new object[]{17});
                    }
                    Ensure(rpg.SaveCheckpoint(),"baseline v2 was written");
                    var env=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(savePath));
                    Ensure(env.version==2 && env.profileId=="linear-rpg-player","baseline envelope identifies v2 player");
                    Ensure(env.progress.checkpoint.mana==37 && env.progress.currency==17,"baseline writer persisted both sentinel values");
                    File.Copy(savePath,Path.Combine(Path.GetDirectoryName(Output),"baseline-v2.json"));
                } else {
                    Ensure(earlyObserved&&bootGuard&&voiceQueued,"tested input during assembly");
                    Ensure(!rpg.skills.effects.HasOccupiedHand && rpg.skills.effects.ActivationCount==0 &&
                        string.IsNullOrEmpty(rpg.skills.Engine.PendingSkillId),"no buffered cast after Ready");
                    var attempt=rpg.skills.GetType().GetField("castAttempt",BindingFlags.Instance|BindingFlags.NonPublic);
                    Ensure(attempt!=null && (long)attempt.GetValue(rpg.skills)==0,"discarded voice caused zero activation attempts, including rejected attempts");
                    Ensure(ReadProgress(rpg).checkpoint.mana==37 && rpg.Player.Mana>=37 && ReadProgress(rpg).currency==17,"loaded sentinel values from baseline v2");
                    Ensure(SameBaseline(),"loading successful v2 does not rewrite save");
                    if(Mode=="candidate"){
                        var core = typeof(RpgPlayerState).Assembly;
                        var persistence = typeof(RpgSaveStore).Assembly;
                        Ensure(core.GetName().Name == "NarutoVR.Game.Core" &&
                            persistence.GetName().Name == "NarutoVR.Game.Persistence" &&
                            persistence.GetReferencedAssemblies().Any(assembly => assembly.Name == core.GetName().Name),
                            "Unity loaded separate player Core and Persistence assemblies");
                        Ensure(!core.GetReferencedAssemblies().Any(assembly => assembly.Name == persistence.GetName().Name) &&
                            !core.GetReferencedAssemblies().Concat(persistence.GetReferencedAssemblies())
                                .Any(assembly => assembly.Name.StartsWith("Unity", StringComparison.Ordinal)),
                            "Unity player model and save assemblies have no engine references or reverse dependency");
                        var original = ReadProgress(rpg);
                        var sameTimeVitals = new RpgPlayerState();
                        Ensure(sameTimeVitals.RestoreCheckpoint(original.checkpoint, 0, out _), "map v2 vitals into an independent model at a fixed time");
                        var mapper = persistence.GetType("NarutoVR.SealCombat.Rpg.RpgSaveMapper", true);
                        var exportModel = mapper.GetMethod("Export", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                        var restoredModel = rpg.GetType().GetProperty("Model").GetValue(rpg);
                        var mapped = (RpgProgressData)exportModel.Invoke(null, new object[] { restoredModel, sameTimeVitals.CaptureCheckpoint(0), original.checkpointId });
                        var roundtrip = new SaveEnvelope { profileId = "linear-rpg-player", progress = mapped };
                        var encoded = new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(roundtrip, true));
                        Ensure(encoded.SequenceEqual(File.ReadAllBytes(savePath)), "Unity v2 checkpoint round-trip matches original writer byte for byte");
                        rpg.skills.ReceiveVoice("雷光剑",preReadyVoiceEndedAt);
                        Ensure(rpg.skills.LastVoiceBlock.Contains("屏障"),"late delivery of a pre-Ready word is rejected by the input time barrier");
                        var effects=rpg.skills.effects;
                        var previousWrist=effects.WristPoseOverride;
                        try {
                            var head=effects.WeaponAttacker;
                            Ensure(head,"real chapter head anchor is bound");
                            effects.WristPoseOverride=left=>left?(Pose?)null:new Pose(head.TransformPoint(.25f,-.15f,.35f),head.rotation);
                            var sword=rpg.skills.ConfiguredSkills.Single(skill=>skill.skillId=="thunder.sword");
                            float mana=rpg.Player.Mana;
                            beforeConfirmedDamage = rpg.Player.Health;
                            bool observed = false;
                            void OnCastConfirmed(RpgActionCue cue)
                            {
                                if (cue != RpgActionCue.CastReady) return;
                                rpg.ReceiveDamage("confirmed-damage-check", 3);
                                observed = rpg.Player.Health == beforeConfirmedDamage;
                            }
                            rpg.Feedback += OnCastConfirmed;
                            try { Ensure(rpg.DebugCast(sword.skillId),"trial sword equips from synthetic wrist: "+rpg.Status); }
                            finally { rpg.Feedback -= OnCastConfirmed; }
                            Ensure(observed,"confirmed-cast listener cannot nest damage inside the cast");
                            awaitConfirmedDamage = true;
                            Ensure(effects.HasOccupiedHand && Math.Abs(rpg.Player.Mana-(mana-sword.manaCost))<.01f,"trial cast owns hand and charges configured MP once");
                            rpg.CancelCasting();
                            Ensure(!effects.HasOccupiedHand && !rpg.IsSkillUnlocked(sword.skillId),"cancelling releases trial sword without unlocking it");
                        } finally {effects.WristPoseOverride=previousWrist;}
                        leaseChecks=ExerciseSharedLease((ScriptableObject)rpg.GetType().GetField("skillResources").GetValue(rpg));
                    }
                }
                if(Mode=="baseline"||Mode=="candidate") {
                    Ensure(SkillActivationPolicyChecks.Run()>0,"existing activation policy checks");
                    if(Mode=="candidate") Ensure(!string.IsNullOrEmpty(PlayerObjChecks.Run()),"unique player state, shield and hit-ledger checks");
                    Ensure(SessionState.GetInt(Key+"dataChecks",0)>0,"existing edit-mode v2/data checks");
                    Ensure(SkillEffectConfigurationChecks.Run()>0,"existing configuration/lifecycle checks");
                }
            }
            if (Mode == "candidate" && !shortcutVerified && !CheckShortcut(rpg)) return;
            if (awaitConfirmedDamage)
            {
                if (rpg.Player.Health == beforeConfirmedDamage) return;
                Ensure(Math.Abs(rpg.Player.Health - (beforeConfirmedDamage - 3)) < .001f &&
                    !rpg.ReceiveDamage("confirmed-damage-check", 3), "next command phase settles confirmed listener damage exactly once");
                awaitConfirmedDamage = false;
            }
            if(leaseChecks!=null) {
                if(!leaseChecks.IsCompleted)return;
                Ensure(leaseChecks.GetAwaiter().GetResult()>0,"shared load, cancel waiter, lease release");
            }
            Finish(null);
        } catch(Exception e){Finish(e);}
    }
    static bool readyChecked,retried;
    static Keyboard shortcutKeyboard;
    static int shortcutFrame;
    static bool shortcutVerified;
    static string shortcutInputUpdate;
    static InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
    static void PressShortcutInPlayer()
    {
        var update = InputState.currentUpdateType;
        if (update != InputUpdateType.Dynamic && update != InputUpdateType.Fixed && update != InputUpdateType.Manual) return;
        InputSystem.onAfterUpdate -= PressShortcutInPlayer;
        var key = UnityEngine.InputSystem.Key.F12;
        InputState.Change(shortcutKeyboard, new KeyboardState(key));
        shortcutInputUpdate = $"{update}, pressed={shortcutKeyboard[key].isPressed}, edge={shortcutKeyboard[key].wasPressedThisFrame}";
    }
    static bool CheckShortcut(LinearRpgController rpg)
    {
        if (shortcutKeyboard == null)
        {
            previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            shortcutKeyboard = InputSystem.AddDevice<Keyboard>();
            shortcutKeyboard.MakeCurrent();
            // Headless EditMode also pumps its own keyboard buffers. Set the player device in the game update.
            InputSystem.onAfterUpdate += PressShortcutInPlayer;
            shortcutFrame = Time.frameCount;
            return false;
        }
        if (rpg.skills.ScheduledPhrase == null && Time.frameCount - shortcutFrame < 20) return false;
        var recipe = rpg.skills.Recipes.Single(r => r.Id == "thunder.sword");
        Ensure(rpg.skills.ScheduledPhrase == recipe.Phrase,
            $"virtual F12 scheduled the active player recipe by ID (game input={shortcutInputUpdate}, " +
            $"view active={rpg.skills.view.isActiveAndEnabled}, " +
            $"desktop controls={rpg.skills.view.showDesktopControls})");
        rpg.skills.CancelInputOnly();
        shortcutVerified = true;
        return true;
    }
    static bool awaitConfirmedDamage;
    static float beforeConfirmedDamage;
    static async Task<int> ExerciseSharedLease(ScriptableObject manifest)
    {
        // The same source is compiled against baseline and candidate. Only candidate has resource types.
        var acquire=manifest.GetType().GetMethod("AcquireAsync");
        async Task<IDisposable> Get(System.Threading.CancellationToken token) {
            var t=(Task)acquire.Invoke(manifest,new object[]{"fireball",token});
            await t;
            return (IDisposable)t.GetType().GetProperty("Result").GetValue(t);
        }
        Obj Asset(IDisposable lease)=>(Obj)lease.GetType().GetProperty("Asset").GetValue(lease);
        using var first=await Get(default);
        using var second=await Get(default);
        Ensure(ReferenceEquals(Asset(first),Asset(second)),"two users share fireball prototype");
        var expected=Asset(second);
        first.Dispose(); first.Dispose();
        Ensure(ReferenceEquals(expected,Asset(second)),"releasing first user twice leaves second usable");
        try {await Get(new System.Threading.CancellationToken(true));throw new Exception("pre-cancelled load accepted"); }
        catch(OperationCanceledException){checks.Add("pre-cancelled request rejected");}
        return 3;
    }
    static bool SameBaseline()=>File.ReadAllBytes(savePath).SequenceEqual(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Output),"baseline-v2.json")));
    static void Finish(Exception error)
    {
        if(!entered)return;entered=false;EditorApplication.update-=Tick;
        InputSystem.onAfterUpdate -= PressShortcutInPlayer;
        if (shortcutKeyboard != null)
        {
            InputSystem.RemoveDevice(shortcutKeyboard);
            shortcutKeyboard = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
        }
        var report=new Report{passed=error==null,phase=Mode,project=Root,scene=SceneManager.GetActiveScene().path,
            engine=Application.unityVersion,save=savePath,error=error?.ToString(),assertions=checks.ToArray(),
            finished=DateTimeOffset.Now.ToString("o")};
        File.WriteAllText(Output,JsonUtility.ToJson(report,true));
        SessionState.SetBool(Key+"done",true); SessionState.SetInt(Key+"exit",error==null?0:1);
        EditorApplication.ExitPlaymode();
    }
}
