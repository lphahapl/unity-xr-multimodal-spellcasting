// Copy only to the isolated project's Assets/Editor. Real Play Mode and real async scene load.
// -executeMethod RuntimeAcceptance.Run (no -quit); optional EXTERIOR_RUNTIME_OUT directory.
// No runtime test API or fake scene loader is introduced. Never run in the main project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NarutoVR.SceneArt;
using NarutoVR.SealCombat.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.XR;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RuntimeAcceptance
{
    const string SessionKey = "NarutoVR.ExteriorRuntimeAcceptance";
    const string MainProject = "D:/UnityProjects/NarutoVR";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] public class Entry { public string name, evidence; public bool passed; }
    [Serializable] public class BuildScene { public string path; public bool enabled; }
    [Serializable] public class RunState
    {
        public string phase, projectRoot, output, originalScene, originalBuildSettingsSHA256, backup;
        public string fixtureScene, destinationScene, error;
        public double deadline;
        public bool testsPassed, playModeObserved, realSceneLoadObserved, buildSettingsRestored;
        public int destinationLoadCount;
        public BuildScene[] originalScenes;
        public List<Entry> checks = new List<Entry>();
    }
    [Serializable] public class Result
    {
        public bool passed, playModeObserved, realSceneLoadObserved, buildSettingsRestored;
        public bool fakeLoaderUsed;
        public int destinationLoadCount;
        public string unityVersion, timestamp, error, originalBuildSettingsSHA256, restoredBuildSettingsSHA256;
        public string fixtureScene, destinationScene;
        public Entry[] checks;
        public string scope = "Synthetic Transform/head/hand/enemy fixtures in actual Unity Play Mode; real SceneManager.LoadSceneAsync into an isolated temporary scene. Does not validate final art reference assignment, full gameplay transfer, headset traversal or a user-selected destination.";
    }
    static RunState state;
    static GameObject root, hand, enemy;
    static Transform intact, broken, head, replacementHead;
    static ArmillaryRingMotion motion;
    static SceneTravelGate gate;
    static Quaternion initialRing, pausedRing, initialBroken, initialRoot;
    static Vector3 ringPosition, ringScale, brokenPosition, brokenScale, rootPosition, rootScale;
    static int step;
    static double next;
    static bool pauseSnapshotReady;
    const float TravelDelaySeconds = 1.5f;
    static int travelStarts;
    static double gateEnteredAt, destinationActivatedAt;
    static float requestedSeconds;
    static bool startedInFixture, busyDuringPresentation;
    static StoryCameraManager manager;
    static StoryCameraSequence sequence;
    static IDisposable storySubscription, entrySubscription;
    static int storyRequests;
    static bool requestedDirectorPlaying;
    static SceneLoadingOverlay overlayPrefab;
    static double overlayShownAt;
    static float lastFill;
    static bool sawRamp, sawFull, sawDestinationCamera, sawBusyInDestination, sawStoryBeyondDelay;

    static RuntimeAcceptance()
    {
        string saved = SessionState.GetString(SessionKey, "");
        if (saved.Length == 0) return;
        state = JsonUtility.FromJson<RunState>(saved);
        Subscribe();
    }

    public static void Run()
    {
        if (!Application.isBatchMode) { Debug.LogError("Runtime acceptance requires an isolated batch process."); return; }
        string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
        if (string.Equals(project.TrimEnd('/', '\\'), Path.GetFullPath(MainProject).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing runtime fixture/build-settings writes in the main project.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || !string.IsNullOrEmpty(SessionState.GetString(SessionKey,"")))
            throw new InvalidOperationException("Another Play Mode run/acceptance is already active.");
        Directory.SetCurrentDirectory(project);
        string output = Environment.GetEnvironmentVariable("EXTERIOR_RUNTIME_OUT") ?? Path.Combine(project,"Logs/exterior-runtime-acceptance");
        Directory.CreateDirectory(output);
        string token = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        state = new RunState
        {
            phase="preparing", projectRoot=project, output=Path.GetFullPath(output),
            originalScene=SceneManager.GetActiveScene().path,
            fixtureScene="Assets/Scene/QA_ExteriorRuntimeFixture_"+token+".unity",
            destinationScene="Assets/Scene/QA_ExteriorRuntimeDestination_"+token+".unity",
            originalScenes=EditorBuildSettings.scenes.Select(s=>new BuildScene{path=s.path,enabled=s.enabled}).ToArray(),
            backup=Path.Combine(Path.GetFullPath(output),"build-settings-before-"+token+".asset"),
            deadline=EditorApplication.timeSinceStartup+300
        };
        try
        {
            string settings=BuildSettingsPath();
            byte[] original=File.ReadAllBytes(settings);
            state.originalBuildSettingsSHA256=Sha(original);File.WriteAllBytes(state.backup,original);
            Require(!File.Exists(state.fixtureScene) && !File.Exists(state.destinationScene),"Unique temporary scene paths");
            var target=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("RuntimeAcceptanceDestinationMarker");
            new GameObject("Destination camera", typeof(Camera)).tag="MainCamera";
            Require(EditorSceneManager.SaveScene(target,state.destinationScene),"Saved isolated destination fixture");
            var fixture=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Require(EditorSceneManager.SaveScene(fixture,state.fixtureScene),"Saved empty Play Mode fixture");
            EditorBuildSettings.scenes=state.originalScenes.Select(s=>new EditorBuildSettingsScene(s.path,s.enabled))
                .Concat(new[]{new EditorBuildSettingsScene(state.destinationScene,true),new EditorBuildSettingsScene(state.fixtureScene,true)}).ToArray();
            state.phase="waiting-play";Save();Subscribe();EditorApplication.isPlaying=true;
        }
        catch(Exception e) { Fail(e); }
    }

    static void Subscribe()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(state==null)return;
        try
        {
            if(EditorApplication.timeSinceStartup>state.deadline)throw new TimeoutException("Play Mode acceptance deadline exceeded");
            if(state.phase=="waiting-edit")
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode)RestoreAndFinish();
                return;
            }
            if(!EditorApplication.isPlaying)return;
            EditorApplication.QueuePlayerLoopUpdate();
            if(state.phase=="waiting-play")
            {
                state.playModeObserved=true;Require(EditorApplication.isPlaying,"Actual Editor Play Mode entered");
                Setup();state.phase="running";Save();step=0;Wait(.4);return;
            }
            if(state.phase!="running")return;
            if(!root)throw new InvalidOperationException("Runtime fixture lost during unexpected domain/scene reload");
            if(EditorApplication.timeSinceStartup<next)return;
            Advance();
        }
        catch(Exception e) { Fail(e); }
    }

    static void Setup()
    {
        root=new GameObject("ExteriorRuntimeFixtureRoot");root.SetActive(false);
        Object.DontDestroyOnLoad(root);
        root.transform.SetPositionAndRotation(new Vector3(3,0,4),Quaternion.Euler(0,17,0));
        initialRoot=root.transform.rotation;rootPosition=root.transform.position;rootScale=root.transform.localScale;
        var ring=new GameObject("IntactMeasuringRing");ring.transform.SetParent(root.transform,false);
        intact=ring.transform;intact.localPosition=new Vector3(0,2,0);intact.localScale=new Vector3(1,1.2f,1);
        intact.localRotation=Quaternion.Euler(12,19,31);initialRing=intact.localRotation;
        ringPosition=intact.localPosition;ringScale=intact.localScale;
        var fixedFrame=new GameObject("BrokenFrameMustStayFixed");fixedFrame.transform.SetParent(root.transform,false);
        broken=fixedFrame.transform;broken.localPosition=new Vector3(0,1,0);broken.localRotation=Quaternion.Euler(1,2,3);
        initialBroken=broken.localRotation;brokenPosition=broken.localPosition;brokenScale=broken.localScale;
        motion=root.AddComponent<ArmillaryRingMotion>();Set(motion,"intactRing",intact);

        // Gate and head use world positions unrelated to the armillary transform.
        var gateObject=new GameObject("TravelGateFixture");gateObject.transform.SetParent(root.transform,false);
        gateObject.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        var box=gateObject.AddComponent<BoxCollider>();box.size=new Vector3(2,3,1);box.isTrigger=true;
        gate=gateObject.AddComponent<SceneTravelGate>();
        manager=StoryCameraManager.Instance;
        sequence=NewSequence();
        Set(gate,"presentation",sequence);
        overlayPrefab=AssetDatabase.LoadAssetAtPath<SceneLoadingOverlay>("Assets/Prefabs/SceneLoadingOverlay.prefab");
        var bar=overlayPrefab.GetComponentInChildren<SceneLoadingProgressBar>();
        Require(bar && Get(bar,"progressFill") is Image image && image.sprite && image.type==Image.Type.Filled &&
            Get(bar,"percentText") is Text,"The replaceable progress bar binds its own filled sprite and percentage");
        var backdrop=overlayPrefab.transform.GetChild(0);
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(backdrop.gameObject)=="Assets/Prefabs/SceneLoadingBackdrop.prefab" &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bar.gameObject)=="Assets/Prefabs/SceneLoadingProgressBar.prefab" &&
            bar.transform.parent==overlayPrefab.transform && bar.transform.GetSiblingIndex()>backdrop.GetSiblingIndex(),
            "The overlay draws its independent background prefab behind its progress bar prefab");
        var anchor=((RectTransform)bar.transform).anchorMin;
        Require(Mathf.Approximately(anchor.x,.5f) && anchor.y>0f && anchor.y<.25f,
            "The progress bar is anchored in the lower quarter of the viewport");
        Set(gate,"loadingOverlayPrefab",overlayPrefab);
        entrySubscription=EventCenter.Subscribe(TravelEvents.GateEntered, notice =>
        {
            if (notice.Gate!=gate) return;
            gateEnteredAt=Time.realtimeSinceStartupAsDouble;
            requestedSeconds=notice.PresentationSeconds;
        });
        storySubscription=EventCenter.Subscribe(TravelEvents.StoryPlayRequested, notice =>
        {
            storyRequests++;
            requestedDirectorPlaying=manager.ActiveDirector==notice.Director && notice.Director.state==PlayState.Playing;
        });
        Require((float)Get(gate,"minimumDelaySeconds")==0f,"Existing gates default to immediate activation");
        Set(gate,"minimumDelaySeconds",TravelDelaySeconds);
        ((UnityEvent)Get(gate,"onTravelStarted")).AddListener(StartPresentation);
        Require(!gate.LoadEnabled && string.IsNullOrEmpty(gate.DestinationScene),"Gate defaults disabled with empty destination");
        head=CameraObject("PrimaryTrackedHead");head.position=Outside;
        hand=new GameObject("HandColliderMustNotActivateGate");hand.transform.SetParent(root.transform,false);
        hand.transform.position=Inside;hand.AddComponent<BoxCollider>();
        var handBody=hand.AddComponent<Rigidbody>();handBody.isKinematic=true;
        enemy=new GameObject("EnemyColliderMustNotActivateGate");enemy.transform.SetParent(root.transform,false);
        enemy.transform.position=Inside;enemy.AddComponent<BoxCollider>();
        var enemyBody=enemy.AddComponent<Rigidbody>();enemyBody.isKinematic=true;
        SceneManager.sceneLoaded+=SceneLoaded;
        root.SetActive(true);Time.timeScale=1f;
    }
    static readonly Vector3 Inside=new Vector3(0,1.6f,0),Outside=new Vector3(0,1.6f,-3);
    static StoryCameraSequence NewSequence()
    {
        var director=new GameObject("Gate story").AddComponent<PlayableDirector>();
        director.playOnAwake=false;
        director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;
        var timeline=ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.durationMode=TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration=2;
        director.playableAsset=timeline;
        var sequence=director.gameObject.AddComponent<StoryCameraSequence>();
        Set(sequence,"shotPose",director.transform);
        return sequence;
    }
    static void StartPresentation()
    {
        travelStarts++;
        startedInFixture=SceneManager.GetActiveScene().path==state.fixtureScene;
        busyDuringPresentation=AsyncSceneLoader.TryLoad(state.destinationScene)==AsyncSceneLoader.Result.Busy;
        Time.timeScale=0f;
        gate.gameObject.SetActive(false);
    }
    static Transform CameraObject(string name)
    {
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.tag="MainCamera";
        go.AddComponent<Camera>();return go.transform;
    }
    static void Advance()
    {
        switch(step)
        {
            case 0:
                Require(Quaternion.Angle(intact.localRotation,initialRing)>.05f,"Intact ring rotates during actual Update frames");
                CheckFixedTransforms();Time.timeScale=0;pauseSnapshotReady=false;Wait(.1);break;
            case 1:
                // Let the frame in which the editor changed timeScale finish before measuring freeze.
                if(!pauseSnapshotReady){pausedRing=intact.localRotation;pauseSnapshotReady=true;Wait(.25);return;}
                Require(intact.localRotation.Equals(pausedRing),"Time.timeScale=0 freezes ring motion");
                CheckFixedTransforms();Time.timeScale=1;head.position=Inside;Wait(.15);break;
            case 2:
                NoLoad("Disabled/unconfigured gate does not load with head inside");
                Set(gate,"loadEnabled",true);Wait(.15);break;
            case 3:
                NoLoad("First enabled sample already inside does not travel");head.position=Outside;Wait(.15);break;
            case 4:head.position=Inside;Wait(.15);break;
            case 5:
                NoLoad("Empty destination on a genuine outside-to-inside entry does not load");
                Set(gate,"destinationScene",state.destinationScene);head.position=Outside;Wait(.2);break;
            case 6:
                Require(Application.CanStreamedLevelBeLoaded(state.destinationScene),"Configured temporary target is truly loadable from Build Settings");
                NoLoad("Hands and enemy colliders inside do not activate a valid enabled gate");
                gate.enabled=false;head.position=Inside;Wait(.15);break;
            case 7:
                NoLoad("Disabled component does not load");gate.enabled=true;Wait(.15);break;
            case 8:
                NoLoad("Re-enabled component starts inside without travelling");head.position=Outside;Wait(.15);break;
            case 9:
                Set(gate,"loadEnabled",false);head.position=Inside;Wait(.15);break;
            case 10:
                NoLoad("Load-enabled switch off prevents travel");Set(gate,"loadEnabled",true);Wait(.15);break;
            case 11:
                NoLoad("Load-enabled switch reactivation inside resets the previous outside sample");head.position=Outside;Wait(.15);break;
            case 12:
                head.GetComponent<Camera>().enabled=false;
                replacementHead=CameraObject("ReplacementActiveMainCamera");replacementHead.position=Outside;Wait(.2);break;
            case 13:
                Require((Transform)Get(gate,"playerHead")==replacementHead,"Disabled cached camera replaced by active MainCamera");
                head.position=Inside;Wait(.2);break;
            case 14:
                NoLoad("Disabled old camera entering does not activate gate");
                Require(travelStarts==0 && storyRequests==0,"Rejected gate entries never request a story");
                replacementHead.gameObject.AddComponent<TrackedPoseDriver>();
                replacementHead.position=Inside;next=EditorApplication.timeSinceStartup;step++;Save();return;
            case 15:
                var overlay=Object.FindAnyObjectByType<SceneLoadingOverlay>();
                if(manager.ActiveDirector)
                {
                    if(overlay)throw new InvalidOperationException("Mask obscured an active cutscene");
                    sawStoryBeyondDelay |= Time.realtimeSinceStartupAsDouble-gateEnteredAt>TravelDelaySeconds;
                }
                if(overlay) ObserveOverlay(overlay);
                if(state.destinationLoadCount==0 || overlay)
                {
                    if(EditorApplication.timeSinceStartup-next>15)throw new TimeoutException("Real LoadSceneAsync did not complete");
                    return;
                }
                Require(state.destinationLoadCount==1 && SceneManager.GetActiveScene().path==state.destinationScene,
                    "Active tracked head entry completed one real Single LoadSceneAsync");
                Require(sawStoryBeyondDelay,"A longer Timeline remains visible beyond the configured minimum delay");
                Require(sawRamp && sawFull && overlayShownAt>0 && Time.realtimeSinceStartupAsDouble-overlayShownAt>=0.6,
                    "The progress bar interpolates from zero and reaches 100% after at least 0.6 unscaled seconds");
                Require(sawDestinationCamera && sawBusyInDestination,
                    "Mask binds the destination camera and blocks a second transition during its visible tail");
                Require(travelStarts==1 && startedInFixture && busyDuringPresentation,
                    "Camera event fires once in the source scene with a load already in flight");
                Require(storyRequests==1 && requestedDirectorPlaying && !sequence &&
                    StoryCameraManager.Instance==manager && !manager.ActiveDirector && replacementHead.GetComponent<TrackedPoseDriver>().enabled,
                    "Real gate requests the bound director once; singleton survives source unload and restores tracking");
                double elapsed=destinationActivatedAt-gateEnteredAt;
                Require(requestedSeconds==TravelDelaySeconds && elapsed>=TravelDelaySeconds && Time.timeScale==0f && !gate.gameObject.activeSelf,
                    $"Destination activated after {elapsed:F3}s (minimum {TravelDelaySeconds}s) while paused and gate inactive");
                Time.timeScale=1f;
                Camera.main.enabled=false;
                replacementHead.GetComponent<Camera>().enabled=true;
                gate.gameObject.SetActive(true);
                Require((bool)Get(gate,"loading"),"Successful load keeps the one-shot submission guard");
                replacementHead.position=Outside;Wait(.15);break;
            case 16:replacementHead.position=Inside;Wait(.15);break;
            case 17:replacementHead.position=Outside;Wait(.15);break;
            case 18:replacementHead.position=Inside;Wait(.2);break;
            case 19:
                Require(state.destinationLoadCount==1,"Repeated post-load exits/entries submit no additional scene load");
                Require(travelStarts==1,"Re-entering the used gate does not replay the camera event");
                Require(AsyncSceneLoader.TryLoad(" ") == AsyncSceneLoader.Result.InvalidDestination &&
                    AsyncSceneLoader.TryLoad("Missing_" + Path.GetFileNameWithoutExtension(state.fixtureScene)) == AsyncSceneLoader.Result.InvalidDestination,
                    "Shared loader rejects empty and unknown destinations");
                Require(AsyncSceneLoader.TryLoad(state.destinationScene) == AsyncSceneLoader.Result.AlreadyInScene &&
                    AsyncSceneLoader.TryLoad(SceneManager.GetActiveScene().name) == AsyncSceneLoader.Result.AlreadyInScene,
                    "Shared loader ignores the active scene by path and name");
                var returnGate=new GameObject("Return gate").AddComponent<SceneTravelGate>();
                Set(returnGate,"destinationScene"," "+Path.GetFileNameWithoutExtension(state.fixtureScene)+" ");
                Set(returnGate,"loadEnabled",true);
                Set(returnGate,"presentation",NewSequence());
                Set(returnGate,"loadingOverlayPrefab",overlayPrefab);
                Set(returnGate,"minimumDelaySeconds",120f);
                EventCenter.Publish(TravelEvents.GateEntered,new GateEnteredNotice(returnGate));
                var immediateMask=Object.FindAnyObjectByType<SceneLoadingOverlay>();
                Require(immediateMask && ((Image)Get(immediateMask,"progressFill")).fillAmount==0f &&
                    ((Text)Get(immediateMask,"percentText")).text=="0%","No-story entry immediately displays exactly zero");
                overlayShownAt=(double)Get(immediateMask,"shownAt");
                Require((bool)Get(returnGate,"loading") && storyRequests==1 && !manager.ActiveDirector,
                    "Omitted event duration starts the return trip without a story even with a configured sequence");
                Require(AsyncSceneLoader.TryLoad(state.fixtureScene) == AsyncSceneLoader.Result.Busy,
                    "A second caller in the same frame cannot queue another transition");
                next=EditorApplication.timeSinceStartup;step++;Save();return;
            case 20:
                if(SceneManager.GetActiveScene().path!=state.fixtureScene || Object.FindAnyObjectByType<SceneLoadingOverlay>())
                {
                    if(EditorApplication.timeSinceStartup-next>15)throw new TimeoutException("Shared loader return trip did not complete");
                    return;
                }
                Require(state.destinationLoadCount==1,"Return trip reached the fixture without reloading the gate destination");
                Require(Time.realtimeSinceStartupAsDouble-overlayShownAt>=0.6,"Fast return trip keeps the mask for at least 0.6 seconds");
                CheckFixedTransforms();
                var heldGate=new GameObject("Held gate").AddComponent<SceneTravelGate>();
                Set(heldGate,"destinationScene",state.destinationScene);
                Set(heldGate,"loadEnabled",true);
                sequence=NewSequence();
                Set(heldGate,"presentation",sequence);
                Set(heldGate,"loadingOverlayPrefab",overlayPrefab);
                EventCenter.Publish(TravelEvents.GateEntered,new GateEnteredNotice(heldGate,120f));
                Require(manager.ActiveDirector && !Object.FindAnyObjectByType<SceneLoadingOverlay>(),
                    "The held transition starts its story before showing the mask");
                sequence.Stop();
                Wait(.9);break;
            case 21:
                var heldMask=Object.FindAnyObjectByType<SceneLoadingOverlay>();
                Require(heldMask && !manager.ActiveDirector,"Skipping the story opens the pending load mask");
                var heldOperation=(AsyncOperation)Get(heldMask,"operation");
                Require(!heldOperation.isDone && Mathf.Approximately(heldOperation.progress,0.9f) &&
                    Mathf.Approximately(((Image)Get(heldMask,"progressFill")).fillAmount,heldOperation.progress),
                    "After the initial ramp a pending native operation holds the mask at its real progress");
                Require(SceneManager.GetActiveScene().path==state.fixtureScene && state.destinationLoadCount==1,
                    "Source scene is still active when leaving Play mode mid-presentation");
                state.testsPassed=true;LeavePlay();return;
        }
        step++;Save();
    }
    static void ObserveOverlay(SceneLoadingOverlay overlay)
    {
        overlayShownAt=(double)Get(overlay,"shownAt");
        var operation=(AsyncOperation)Get(overlay,"operation");
        float fill=((Image)Get(overlay,"progressFill")).fillAmount;
        if(fill<lastFill || ((Text)Get(overlay,"percentText")).text!=$"{Mathf.FloorToInt(fill*100f)}%")
            throw new InvalidOperationException("Progress bar regressed or disagreed with its percentage");
        lastFill=fill;
        double elapsed=(double)Get(overlay,"lastSample")-overlayShownAt;
        if(elapsed>0.1 && elapsed<0.55 && operation.isDone && Mathf.Abs(fill-(float)elapsed/0.6f)<.01f) sawRamp=true;
        sawFull |= fill==1f && operation.isDone;
        if(state.destinationLoadCount>0 && operation.isDone)
        {
            sawDestinationCamera |= overlay.GetComponent<Canvas>().worldCamera==Camera.main && Camera.main &&
                Camera.main.gameObject.scene.path==state.destinationScene;
            sawBusyInDestination |= AsyncSceneLoader.TryLoad(state.destinationScene)==AsyncSceneLoader.Result.Busy;
        }
    }
    static void CheckFixedTransforms()
    {
        Require(broken.localRotation.Equals(initialBroken) && broken.localPosition==brokenPosition && broken.localScale==brokenScale,
            "Broken frame remains fixed");
        Require(root.transform.position==rootPosition && root.transform.rotation.Equals(initialRoot) && root.transform.localScale==rootScale &&
            intact.localPosition==ringPosition && intact.localScale==ringScale,
            "Only intact ring rotation changes; root, ring position and scale stay fixed");
    }
    static void NoLoad(string name)
    {
        Require(state.destinationLoadCount==0 && !(bool)Get(gate,"loading") && SceneManager.GetActiveScene().path==state.fixtureScene,name);
    }
    static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(state==null || scene.path!=state.destinationScene)return;
        destinationActivatedAt=Time.realtimeSinceStartupAsDouble;
        replacementHead.GetComponent<Camera>().enabled=false;
        state.destinationLoadCount++;state.realSceneLoadObserved=true;Save();
    }
    static void LeavePlay()
    {
        storySubscription?.Dispose();
        entrySubscription?.Dispose();
        Time.timeScale=1;SceneManager.sceneLoaded-=SceneLoaded;
        state.phase="waiting-edit";Save();EditorApplication.isPlaying=false;
    }
    static void Fail(Exception error)
    {
        if(state==null)throw error;
        state.error=(state.error??"")+error+"\n";state.testsPassed=false;
        if(EditorApplication.isPlayingOrWillChangePlaymode)LeavePlay();else RestoreAndFinish();
    }
    static void RestoreAndFinish()
    {
        EditorApplication.update-=Tick;
        string restored="";
        try
        {
            if(state.originalScenes!=null && File.Exists(state.backup))
            {
                EditorBuildSettings.scenes=state.originalScenes.Select(s=>new EditorBuildSettingsScene(s.path,s.enabled)).ToArray();
                byte[] original=File.ReadAllBytes(state.backup);
                File.WriteAllBytes(BuildSettingsPath(),original);
                restored=Sha(File.ReadAllBytes(BuildSettingsPath()));
                state.buildSettingsRestored=restored==state.originalBuildSettingsSHA256;
                Require(state.buildSettingsRestored,"Isolated EditorBuildSettings restored byte-for-byte");
            }
            if(!string.IsNullOrEmpty(state.originalScene) && File.Exists(Path.Combine(state.projectRoot,state.originalScene)))
                EditorSceneManager.OpenScene(state.originalScene,OpenSceneMode.Single);
        }
        catch(Exception e) { state.error=(state.error??"")+e;state.testsPassed=false; }
        var result=new Result
        {
            passed=state.testsPassed && state.buildSettingsRestored,playModeObserved=state.playModeObserved,
            realSceneLoadObserved=state.realSceneLoadObserved,destinationLoadCount=state.destinationLoadCount,
            buildSettingsRestored=state.buildSettingsRestored,originalBuildSettingsSHA256=state.originalBuildSettingsSHA256,
            restoredBuildSettingsSHA256=restored,unityVersion=Application.unityVersion,timestamp=DateTimeOffset.Now.ToString("O"),
            fixtureScene=state.fixtureScene,destinationScene=state.destinationScene,error=state.error,checks=state.checks.ToArray()
        };
        File.WriteAllText(Path.Combine(state.output,"runtime-acceptance.json"),JsonUtility.ToJson(result,true));
        SessionState.EraseString(SessionKey);state=null;EditorApplication.Exit(result.passed?0:1);
    }
    static string BuildSettingsPath()=>Path.Combine(state.projectRoot,"ProjectSettings/EditorBuildSettings.asset");
    static void Wait(double seconds) { next=EditorApplication.timeSinceStartup+seconds; }
    static void Save() { SessionState.SetString(SessionKey,JsonUtility.ToJson(state)); }
    static void Require(bool result,string name)
    {
        state.checks.Add(new Entry{name=name,passed=result,evidence=EditorApplication.isPlaying?"actual Play Mode":"isolated Editor preparation/restoration"});
        if(!result)throw new InvalidOperationException(name);
    }
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static object Get(object target,string name)=>target.GetType().GetField(name,Private).GetValue(target);
    static string Sha(byte[] data)
    {
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(data)).Replace("-","").ToLowerInvariant();
    }
}
