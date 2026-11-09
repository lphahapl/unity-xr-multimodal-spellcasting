// TEMPORARY INDEPENDENT-PACKAGE FIXTURE. Copy to isolated Assets only; never publish main Assets.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public sealed class QuestExteriorProbe : MonoBehaviour
{
    const string PackagePrefix="com.kaogonglvdu.chapter01.exteriorperf20261109";
    const string Controller="NarutoVR.SealCombat.Rpg.LinearRpgController";
    const string Movement="NarutoVR.SealCombat.Rpg.RpgLocomotionController";
    [Serializable] sealed class Config {public bool enabled=true;public int schemaVersion=1;public float lastPointSeconds=60;public bool levelViewForOffFace=false;public bool allowUntrackedGraphicsOnly=false;public float panSeconds=0;public float panDegrees=35;}
    [Serializable] sealed class Row
    {
        public string utc,kind,point,detail,package,quality,pipeline,headTrackingSource,sampleMode,phase;
        public int frameCount,qualityIndex,eyeWidth,eyeHeight,headTrackingBits;
        public bool legacyHeadDeviceValid,legacyTrackedFeatureAvailable,legacyTracked,inputHeadEnabled,inputHeadIsPressed;
        public bool graphicsOnly,graphicsFallbackUsed,headPoseAvailable,levelViewForOffFace,allowUntrackedGraphicsOnly;
        public float inputHeadTrackedValue,inputHeadPressPoint;
        public double inputHeadLastUpdate;
        public int inputHeadDeviceId;
        public bool actualXR,stereo,headTracked,focused,appGPUValid,refreshValid;
        public double realtime,windowSeconds,fpsObserved,meanFramePeriodMs,maxFramePeriodMs;
        public double phaseElapsed;
        public float currentYaw,panSeconds,panDegrees;
        public float appGPUms,refreshHz,eyeResolutionScale;
        public Vector3 headWorld,headLocal,originWorld,headWorldEuler,originWorldEuler;
    }
    sealed class Flag {public Behaviour component;public bool enabled;}
    struct View {public string name;public Vector3 eye,look;public View(string n,Vector3 p,Vector3 t){name=n;eye=p;look=t;}}
    static readonly View[] Views={
        new View("door",new Vector3(0,1.65f,36.1f),new Vector3(0,1.65f,45)),
        new View("portal",new Vector3(5.3f,1.65f,59.7f),new Vector3(10,1.65f,63)),
        new View("canal",new Vector3(3.3f,1.65f,53),new Vector3(0,1.65f,64)),
        new View("wake-window",new Vector3(-3.8f,1.65f,3),new Vector3(-22,1.65f,4)),
        new View("trial-window",new Vector3(4.8f,1.65f,25),new Vector3(24,1.65f,26)),
        new View("court-hot-hold",new Vector3(3.7f,1.65f,40.5f),new Vector3(-3,1.65f,48))};
    readonly List<Flag> frozen=new List<Flag>(),affected=new List<Flag>();
    readonly List<XRDisplaySubsystem> displays=new List<XRDisplaySubsystem>();
    Config config;string controlPath;StreamWriter log;Camera head;Transform origin;
    Vector3 originalPosition;Quaternion originalRotation;bool haveOrigin,restored;
    double nextControlRead;bool controlEnabled=true;
    string point="startup",phase="startup";
    Vector3 panBasePosition;Quaternion panBaseRotation;
    bool panPoseActive,panCompleted,applicationPaused,panSkipNextDelta,resetReadyStability;
    double phaseElapsed;float currentYaw,effectivePanSeconds,effectivePanDegrees;
#if ENABLE_INPUT_SYSTEM
    UnityEngine.InputSystem.XR.XRHMD inputHead;
    double lastInputStamp=-1,lastInputUpdateSeen=-1000;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // Guard before reading files or creating objects. Main package/editor are inert.
        if(Application.platform!=RuntimePlatform.Android||!Application.identifier.StartsWith(PackagePrefix,StringComparison.Ordinal))return;
        string path=Path.Combine(Application.persistentDataPath,"exterior-probe.json");
        if(!File.Exists(path))return;
        try
        {
            var cfg=JsonUtility.FromJson<Config>(File.ReadAllText(path));
            if(cfg==null||!cfg.enabled)return;
            if(cfg.schemaVersion!=1)throw new InvalidOperationException("Unsupported exterior probe schema");
            var go=new GameObject("INDEPENDENT EXTERIOR PERFORMANCE FIXTURE");DontDestroyOnLoad(go);
            var probe=go.AddComponent<QuestExteriorProbe>();probe.config=cfg;probe.controlPath=path;
            probe.log=new StreamWriter(Path.Combine(Application.persistentDataPath,"exterior-probe-metrics.jsonl"),true){AutoFlush=true};
            probe.StartCoroutine(probe.Run());
        }
        catch(Exception e){Debug.LogError("[EXTERIOR_FIXTURE] START_FAILURE "+e);}
    }
    IEnumerator Run()
    {
        try
        {
            Write("BEGIN","Automatic exterior rendering fixture, NOT chapter completion. No save/progression API called. Disables gameplay input/flow temporarily; not normal gameplay CPU profiling.");
            Freeze(); // Before normal Start/update can react to the upcoming rig relocation.
            double deadline=Time.realtimeSinceStartupAsDouble+90;
            double nextWaitReport=Time.realtimeSinceStartupAsDouble+10;
            double readySince=-1;
            phase="waiting-ready";
            while(true)
            {
                if(!ControlEnabled()){Write("CANCEL","Control file absent/disabled");yield break;}
                if(Time.realtimeSinceStartupAsDouble>deadline){Write("FAIL","Required XR display/camera/rig/pose state unavailable for90s; see sampleMode and tracking operands");yield break;}
                if(resetReadyStability){readySince=-1;resetReadyStability=false;}
                bool ready=BindRig()&&Ready()&&!applicationPaused;
                if(!ready)readySince=-1;
                else if(readySince<0)readySince=Time.realtimeSinceStartupAsDouble;
                else if(Time.realtimeSinceStartupAsDouble-readySince>=1)
                {
                    // Device state may become valid before TrackedPoseDriver writes the
                    // Camera transform. Wait through BeforeRender, then read the real pose.
                    yield return new WaitForEndOfFrame();
                    if(!ControlEnabled()){Write("CANCEL","Control disabled while waiting for first rendered camera pose");yield break;}
                    if(Time.realtimeSinceStartupAsDouble>deadline){Write("FAIL","90s startup deadline expired while waiting for camera pose");yield break;}
                    if(!resetReadyStability&&!applicationPaused&&Ready())break;
                    readySince=-1;resetReadyStability=false;
                }
                if(Time.realtimeSinceStartupAsDouble>=nextWaitReport){Write("WAIT_XR","Actual tracking evidence; no pose assumed");nextWaitReport+=10;}
                yield return null;
            }
            originalPosition=origin.position;originalRotation=origin.rotation;haveOrigin=true;
            Write("XR_READY","Continuous Ready>=1s plus EndOfFrame recheck; explicit RpgLocomotionController.rigRoot="+Hierarchy(origin)+"; camera="+Hierarchy(head.transform)+"; camera local pose will not be assigned; graphics-only mode does NOT certify tracking/interaction");
            for(int index=0;index<Views.Length;index++)
            {
                if(!ControlEnabled()){Write("CANCEL","Control disabled before point transition; not COMPLETE");yield break;}
                point=Views[index].name;
                phase="point-move";phaseElapsed=0;currentYaw=0;
                // The first point is already in the startup EndOfFrame continuation.
                // Every subsequent transition also samples the latest TPD camera pose.
                if(index>0)yield return new WaitForEndOfFrame();
                if(!ControlEnabled()){Write("CANCEL","Control disabled waiting for point camera pose; not COMPLETE");yield break;}
                if(!Ready()){Write("FAIL","Required XR/camera/pose state unavailable at point transition");yield break;}
                var view=Views[index];Vector3 localBefore=head.transform.localPosition;
                Vector3 current=head.transform.forward;current.y=0;Vector3 target=view.look-view.eye;target.y=0;
                if(current.sqrMagnitude<.001f){Write("FAIL","Head heading vertical at transition");yield break;}
                var alignment=config.levelViewForOffFace
                    ? Quaternion.LookRotation(view.look-view.eye,Vector3.up)*Quaternion.Inverse(head.transform.rotation)
                    : Quaternion.AngleAxis(Vector3.SignedAngle(current,target,Vector3.up),Vector3.up);
                // One origin operation per point. HMD local position/rotation remain tracked.
                Vector3 position=view.eye+alignment*(origin.position-head.transform.position);
                origin.SetPositionAndRotation(position,alignment*origin.rotation);
                Physics.SyncTransforms();
                Write("POINT_MOVE_ONCE","targetEye="+view.eye+"; alignment="+(config.levelViewForOffFace?"explicit off-face reference alignment; NOT locomotion/comfort testing":"yaw only")+"; headLocalBefore="+localBefore+"; headLocalAfter="+head.transform.localPosition);
                phase="settle";
                double settle=Time.realtimeSinceStartupAsDouble+5;
                while(Time.realtimeSinceStartupAsDouble<settle){if(!ControlEnabled()){Write("CANCEL","Control disabled during settling; not COMPLETE");yield break;}yield return null;}
                if(config.panSeconds>0)
                {
                    if(GraphicsOnly())
                    {
                        yield return PanAtPoint();
                        // A nested iterator can return early on cancellation/failure. Do not start
                        // steady dwell or eventually claim COMPLETE in that case.
                        if(!panCompleted)yield break;
                    }
                    else Write("PAN_SKIPPED","panSeconds>0 requires both explicit graphics-only flags; tracked/default behavior retained");
                }
                if(!ControlEnabled()){Write("CANCEL","Control disabled before steady dwell; not COMPLETE");yield break;}
                phase="steady";phaseElapsed=0;currentYaw=0;
                double duration=index==Views.Length-1?Mathf.Clamp(config.lastPointSeconds,60,3600):60;
                double validElapsed=0,window=0,max=0,last=Time.realtimeSinceStartupAsDouble;int frames=0;bool paused=false;
                while(validElapsed<duration)
                {
                    yield return null;double now=Time.realtimeSinceStartupAsDouble,dt=now-last;last=now;
                    if(!ControlEnabled()){Write("CANCEL","File absent/disabled; restoring origin/components");yield break;}
                    bool valid=Ready()&&Application.isFocused;
                    if(!valid){if(!paused)Write("PAUSED","Required XR/pose/focus unavailable; this time excluded");paused=true;continue;}
                    if(paused){Write("RESUMED","Tracking/focus returned; origin is NOT moved again");paused=false;continue;}
                    validElapsed+=dt;phaseElapsed=validElapsed;window+=dt;frames++;max=Math.Max(max,dt);
                    if(window>=1)
                    {
                        var row=MakeRow("SAMPLE","Frame period/FPS are observed loop cadence, NOT CPU time. GPU is plugin's last-frame sample, not this whole window average.");
                        row.frameCount=frames;row.windowSeconds=window;row.fpsObserved=frames/window;
                        row.meanFramePeriodMs=window/frames*1000;row.maxFramePeriodMs=max*1000;
                        Emit(row);window=0;max=0;frames=0;
                    }
                }
                Write("POINT_DONE","Required-state focused dwellSeconds="+validElapsed+"; mode="+Mode()+"; no chapter/trigger completion claimed");
            }
            if(!ControlEnabled()){Write("CANCEL","Control disabled at final completion check; not COMPLETE");yield break;}
            phase="complete";
            Write("COMPLETE","Six automatic viewpoints complete; restoring startup origin and component enabled flags");
        }
        finally{Restore();if(log!=null){log.Dispose();log=null;}Destroy(gameObject);}
    }
    IEnumerator PanAtPoint()
    {
        panCompleted=false;
        if(float.IsNaN(config.panSeconds)||float.IsInfinity(config.panSeconds)||float.IsNaN(config.panDegrees)||float.IsInfinity(config.panDegrees))
        {Write("FAIL","Non-finite pan configuration; not COMPLETE");yield break;}
        // Explicit test controls; clamped effective values are recorded, never implied defaults.
        effectivePanSeconds=Mathf.Clamp(config.panSeconds,1,120);
        effectivePanDegrees=Mathf.Clamp(Mathf.Abs(config.panDegrees),0,85);
        panBasePosition=origin.position;panBaseRotation=origin.rotation;panPoseActive=true;
        phase="pan";phaseElapsed=0;currentYaw=0;panSkipNextDelta=false;
        double elapsed=0,window=0,max=0,last=Time.realtimeSinceStartupAsDouble;int frames=0;bool paused=false;
        try
        {
            Write("PAN_BEGIN","Independent graphics-only phase; duration="+effectivePanSeconds+"s; yaw=0 -> -"+effectivePanDegrees+" -> +"+effectivePanDegrees+" -> 0; restores this point's entry origin pose, NOT startup pose");
            while(elapsed<effectivePanSeconds)
            {
                yield return null;
                double now=Time.realtimeSinceStartupAsDouble,dt=Math.Max(0,now-last);last=now;
                if(!ControlEnabled()){Write("CANCEL","Control disabled during PAN; restore point base before overall cleanup; not COMPLETE");yield break;}
                bool valid=GraphicsOnly()&&!applicationPaused&&Ready()&&PoseAvailable();
                if(!valid||panSkipNextDelta)
                {
                    if(!paused)Write("PAN_PAUSED","Required XR/pose/focus or application pause; excludes elapsed time and origin motion");
                    paused=true;panSkipNextDelta=false;continue;
                }
                if(paused){Write("PAN_RESUMED","Required state returned; skip resume delta, retain sweep progress and HMD local pose");paused=false;continue;}
                elapsed=Math.Min(effectivePanSeconds,elapsed+dt);phaseElapsed=elapsed;
                Quaternion sweep=PanRotation((float)(elapsed/effectivePanSeconds),effectivePanDegrees,out currentYaw);
                Quaternion desired=sweep*panBaseRotation;
                Quaternion delta=desired*Quaternion.Inverse(origin.rotation);
                Vector3 pivot=head.transform.position;
                // Rotate around the CURRENT world eye. This transform operation leaves that
                // eye position invariant and never assigns the tracked head's local pose.
                origin.SetPositionAndRotation(pivot+delta*(origin.position-pivot),desired);
                // No per-frame Physics.SyncTransforms: input/navigation are frozen and only
                // rendered orientation is sampled. Sync once when restoring the base pose.
                window+=dt;frames++;max=Math.Max(max,dt);
                if(window>=1||elapsed>=effectivePanSeconds)
                {
                    var row=MakeRow("PAN_SAMPLE","Smooth yaw graphics-only phase, separate from steady SAMPLE; loop cadence is NOT CPU time; AppGPU is plugin last-frame, not window mean");
                    row.frameCount=frames;row.windowSeconds=window;row.fpsObserved=frames/window;
                    row.meanFramePeriodMs=window/frames*1000;row.maxFramePeriodMs=max*1000;
                    Emit(row);window=0;max=0;frames=0;
                }
            }
        }
        finally{RestorePanBase();}
        Write("PAN_DONE","Completed independent smooth sweep; exact point-entry origin pose restored before steady dwell; mode="+Mode());
        panCompleted=true;
    }
    static Quaternion PanRotation(float progress,float degrees,out float yaw)
    {
        float from,to,t;
        progress=Mathf.Clamp01(progress);
        // 1/4, 1/2, 1/4 durations give equal angular-speed scale for the three legs.
        if(progress<.25f){from=0;to=-degrees;t=progress*4;}
        else if(progress<.75f){from=-degrees;to=degrees;t=(progress-.25f)*2;}
        else{from=degrees;to=0;t=(progress-.75f)*4;}
        float eased=Mathf.SmoothStep(0,1,t);
        yaw=Mathf.Lerp(from,to,eased);
        return Quaternion.Slerp(Quaternion.AngleAxis(from,Vector3.up),Quaternion.AngleAxis(to,Vector3.up),eased);
    }
    void RestorePanBase()
    {
        if(!panPoseActive)return;
        panPoseActive=false;currentYaw=0;
        if(origin){origin.SetPositionAndRotation(panBasePosition,panBaseRotation);Physics.SyncTransforms();}
    }
    bool BindRig()
    {
        if(origin&&head)return true;
        foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if(!behaviour||behaviour.GetType().FullName!=Movement)continue;
            var type=behaviour.GetType();var rig=type.GetField("rigRoot").GetValue(behaviour) as Transform;var trackedHead=type.GetField("head").GetValue(behaviour) as Transform;
            var camera=trackedHead?trackedHead.GetComponent<Camera>():null;
            if(rig&&camera&&camera.CompareTag("MainCamera")&&camera.transform.IsChildOf(rig)&&camera.transform!=rig){origin=rig;head=camera;return true;}
        }
        return false;
    }
    XRDisplaySubsystem Display()
    {displays.Clear();SubsystemManager.GetSubsystems(displays);foreach(var display in displays)if(display.running)return display;return null;}
    bool Tracked()
    {
#if ENABLE_INPUT_SYSTEM
        // This project selects the new Input System only. Legacy XR InputDevices
        // may have no HMD even while the new TrackedPoseDriver receives real poses.
        if(inputHead==null||!inputHead.added)inputHead=UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRHMD>();
        if(inputHead!=null)
        {
            if(inputHead.lastUpdateTime!=lastInputStamp){lastInputStamp=inputHead.lastUpdateTime;lastInputUpdateSeen=Time.realtimeSinceStartupAsDouble;}
            return inputHead.enabled&&inputHead.isTracked.isPressed&&(inputHead.trackingState.ReadValue()&3)==3;
        }
#endif
        var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        return device.isValid&&device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)&&tracked;
    }
    bool GraphicsOnly()=>config!=null&&config.levelViewForOffFace&&config.allowUntrackedGraphicsOnly;
    string Mode()=>GraphicsOnly()?(phase=="pan"?"off-face smooth-pan graphics only; NOT tracking/interaction/gameplay acceptance":"off-face static graphics only; tracking may be inferred; NOT interaction/gameplay acceptance"):"tracked focused viewpoints";
    bool PoseAvailable()
    {
        if(!head||!Finite(head.transform.position)||!Finite(head.transform.forward))return false;
#if ENABLE_INPUT_SYSTEM
        // This mode samples actual stereo rendering at fixed viewpoints. Valid
        // inferred pose data are allowed, but headTracked remains the raw result.
        if(inputHead!=null)return inputHead.added&&inputHead.enabled&&Time.realtimeSinceStartupAsDouble-lastInputUpdateSeen<=1&&
            (inputHead.trackingState.ReadValue()&3)==3;
#endif
        var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        return device.isValid&&device.TryGetFeatureValue(CommonUsages.trackingState,out InputTrackingState state)&&
            (state&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
    }
    static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
    bool Ready()
    {
        bool tracked=Tracked();
        return head&&head.isActiveAndEnabled&&head.stereoEnabled&&origin&&Display()!=null&&Application.isFocused&&
            (tracked||(GraphicsOnly()&&PoseAvailable()));
    }
    bool ControlEnabled()
    {
        if(restored||!isActiveAndEnabled)return false;
        // One control-file read per second, never per rendered frame during metrics.
        if(Time.realtimeSinceStartupAsDouble<nextControlRead)return controlEnabled;
        nextControlRead=Time.realtimeSinceStartupAsDouble+1;
        try{return controlEnabled=File.Exists(controlPath)&&JsonUtility.FromJson<Config>(File.ReadAllText(controlPath)).enabled;}
        catch(Exception e){Write("FAIL","Control read: "+e.Message);return false;}
    }
    static bool Flow(Type type)
    {for(var t=type;t!=null;t=t.BaseType)if(t.FullName=="NarutoVR.SealCombat.Rpg.RpgChapterFlow")return true;return false;}
    void Freeze()
    {
        var all=FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach(var b in all)
        {
            if(!b)continue;string type=b.GetType().FullName;
            if(type!=Controller&&type!=Movement&&type!="NarutoVR.SceneArt.SceneTravelGate"&&!Flow(b.GetType()))continue;
            frozen.Add(new Flag{component=b,enabled=b.enabled});
            if(type==Controller)
            {
                var skill=b.GetType().GetField("skills")?.GetValue(b) as Behaviour;
                if(skill){affected.Add(new Flag{component=skill,enabled=skill.enabled});var effects=skill.GetType().GetField("effects")?.GetValue(skill) as Behaviour;if(effects)affected.Add(new Flag{component=effects,enabled=effects.enabled});}
            }
        }
        foreach(var f in frozen){Write("FREEZE",f.component.GetType().FullName+" originallyEnabled="+f.enabled);f.component.enabled=false;}
    }
    void Restore()
    {
        if(restored)return;restored=true;
        if(panPoseActive){Write("CANCEL","Fixture disabled/cleaning up during PAN; restore point base before startup origin");RestorePanBase();}
        if(haveOrigin&&origin)origin.SetPositionAndRotation(originalPosition,originalRotation);
        foreach(var f in frozen)if(f.component)f.component.enabled=f.enabled;
        foreach(var f in affected)if(f.component)f.component.enabled=f.enabled;
        Write("RESTORED","Origin startup pose and recorded enabled flags; no save/progression writes. Transient input/effects may reset via existing OnDisable/OnEnable.");
    }
    Row MakeRow(string kind,string detail)
    {
        var display=Display();var row=new Row{utc=DateTimeOffset.UtcNow.ToString("O"),realtime=Time.realtimeSinceStartupAsDouble,kind=kind,point=point,detail=detail,package=Application.identifier,
            actualXR=display!=null,stereo=head&&head.stereoEnabled,headTracked=head&&Tracked(),focused=Application.isFocused,
            qualityIndex=QualitySettings.GetQualityLevel(),eyeWidth=XRSettings.eyeTextureWidth,eyeHeight=XRSettings.eyeTextureHeight,eyeResolutionScale=XRSettings.eyeTextureResolutionScale,sampleMode=Mode(),
            phase=phase,phaseElapsed=phaseElapsed,currentYaw=currentYaw,panSeconds=effectivePanSeconds,panDegrees=effectivePanDegrees};
        var names=QualitySettings.names;row.quality=row.qualityIndex<names.Length?names[row.qualityIndex]:"unknown";row.pipeline=GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"none";
        var legacy=InputDevices.GetDeviceAtXRNode(XRNode.Head);row.legacyHeadDeviceValid=legacy.isValid;
        row.legacyTrackedFeatureAvailable=legacy.TryGetFeatureValue(CommonUsages.isTracked,out bool legacyTracked);
        row.legacyTracked=legacyTracked;
        row.graphicsOnly=GraphicsOnly();row.headPoseAvailable=PoseAvailable();row.graphicsFallbackUsed=row.graphicsOnly&&!row.headTracked&&row.headPoseAvailable;
        row.levelViewForOffFace=config!=null&&config.levelViewForOffFace;row.allowUntrackedGraphicsOnly=config!=null&&config.allowUntrackedGraphicsOnly;
        row.headTrackingSource="Legacy XR InputDevices";
#if ENABLE_INPUT_SYSTEM
        if(inputHead!=null){row.headTrackingSource="Input System "+inputHead.layout;row.headTrackingBits=inputHead.trackingState.ReadValue();row.inputHeadEnabled=inputHead.enabled;row.inputHeadIsPressed=inputHead.isTracked.isPressed;row.inputHeadTrackedValue=inputHead.isTracked.ReadValue();row.inputHeadPressPoint=inputHead.isTracked.pressPointOrDefault;row.inputHeadDeviceId=inputHead.deviceId;row.inputHeadLastUpdate=inputHead.lastUpdateTime;}
#endif
        if(head){row.headWorld=head.transform.position;row.headLocal=head.transform.localPosition;row.headWorldEuler=head.transform.eulerAngles;}
        if(origin){row.originWorld=origin.position;row.originWorldEuler=origin.eulerAngles;}
        if(display!=null)
        {
            row.appGPUValid=display.TryGetAppGPUTimeLastFrame(out float seconds)&&seconds>=0&&!float.IsNaN(seconds)&&!float.IsInfinity(seconds);row.appGPUms=row.appGPUValid?seconds*1000:0;
            row.refreshValid=display.TryGetDisplayRefreshRate(out float hz);row.refreshHz=row.refreshValid?hz:0;
        }
        return row;
    }
    void Write(string kind,string detail)=>Emit(MakeRow(kind,detail));
    void Emit(Row row){try{log?.WriteLine(JsonUtility.ToJson(row));}catch(Exception e){Debug.LogError("[EXTERIOR_FIXTURE] LOG_FAILURE "+e.Message);}if(row.kind!="SAMPLE"&&row.kind!="PAN_SAMPLE")Debug.Log("[EXTERIOR_FIXTURE] "+row.kind+" "+row.point+" "+row.detail);}
    static string Hierarchy(Transform t)=>t.parent?Hierarchy(t.parent)+"/"+t.name:t.name;
    void OnApplicationPause(bool paused){applicationPaused=paused;resetReadyStability=true;if(panPoseActive)panSkipNextDelta=true;}
    void OnApplicationFocus(bool focused){if(!focused){resetReadyStability=true;if(panPoseActive)panSkipNextDelta=true;}}
    void OnDisable(){Restore();}
}
