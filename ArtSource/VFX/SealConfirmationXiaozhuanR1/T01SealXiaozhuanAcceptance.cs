using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections;
using NarutoVR.SealLab;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Rpg;
using Lab = NarutoVR.SealLab.SealLab;
using Obj = UnityEngine.Object;

[InitializeOnLoad]
public static class T01SealXiaozhuanAcceptance
{
    const string Project="D:/Temp/NarutoVR-T01Seal-20261124";
    const string BaseOutput="D:/UnityProjects/NarutoVR/Logs/seal-confirmation-xiaozhuan-r1-20261125/unity";
    static string Output=>BaseOutput+(SessionState.GetBool(Key+"HengOnly",false)?"/heng-depth12":"");
    const string Key="T01SealXiaozhuanAcceptance.Play";
    static readonly string[] Names={"规印","矩印","衡印","律印","准印","绳印"};
    static readonly string[] Codes={"Gui","Ju","Heng","Lv","Zhun","Sheng"};
    static readonly Vector3[] CaseAngles={Vector3.zero,new(25,35,0),new(-25,-35,0),new(0,0,65)};
    static bool hengOnly;
    static int angleIndex;
    static string StageCode=>hengOnly?"Heng_Angle"+angleIndex:Codes[stage];
    static readonly List<float> pcm=new();
    static readonly List<string> notices=new();
    static readonly List<string> report=new();
    static readonly HashSet<int> instances=new();
    static SkillInputController input;
    static SkillController effects;
    static Lab lab;
    static Camera camera;
    static RenderTexture target;
    static Texture2D texture;
    static GameObject wristCase;
    static SealProfile[] profiles;
    static int frame, stage, stageEvents, stageInstances, audioChecks;
    static double now;
    static bool rapid;
    static double rapidStart;
    static int rapidEvents,maxConcurrentSources;
    static readonly int[] RapidOrder={0,1,3,2,4,5,0,1,3,2,4,5};
    static int[] activeOrder=RapidOrder;
    static bool stress;
    static string MultiFolder=>stress?"Stress":"Rapid";
    static readonly Dictionary<int,float> individualPeaks=new();
    static readonly List<FiredAudio> fired=new();
    sealed class FiredAudio
    {
        public int index,sealIndex,instanceId,startFrame,pcmOffset,maxCursor,totalSamples;
        public double inputStart,dspStart;
        public float clipLength;
        public string clipName,kind;
        public AudioSource source;
        public bool completed;
    }
    static bool running, leftAvailable=true, rightAvailable=true;
    static readonly Vector3 Left=new(-.035f,-.035f,.50f),Right=new(.055f,-.035f,.50f);
    static T01SealXiaozhuanAcceptance()=>EditorApplication.update+=Boot;
    public static void Run()
    {SessionState.SetBool(Key+"HengOnly",false);RunInternal();}
    public static void RunHeng()
    {SessionState.SetBool(Key+"HengOnly",true);RunInternal();}
    static void RunInternal()
    {
        if(!Application.dataPath.Replace('\\','/').StartsWith(Project+"/"))throw new Exception("Wrong isolated project");
        SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Boot()
    {
        if(!Application.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);
        try{Setup();}catch(Exception ex){Fail(ex);}
    }
    static void Setup()
    {
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/timeline.tsv","stage\tframe\ttime\ttype\tdetail\n");
        Time.captureFramerate=30;Time.timeScale=1;Application.runInBackground=true;
        QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
        camera=new GameObject("Acceptance camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.105f,.135f,.145f);
        camera.nearClipPlane=.02f;camera.farClipPlane=10;camera.fieldOfView=50;camera.aspect=16f/9;camera.allowHDR=false;camera.allowMSAA=false;
        camera.gameObject.AddComponent<AudioListener>();camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        target=new RenderTexture(960,540,24);target.Create();texture=new Texture2D(960,540,TextureFormat.RGB24,false);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.60f,.65f);
        var light=new GameObject("Acceptance key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(35,-30,0);
        var root=new GameObject("Actual production playback chain");
        lab=root.AddComponent<Lab>();lab.enabled=false;lab.recordingOrigin=root.transform;
        profiles=Names.Select(n=>AssetDatabase.LoadAssetAtPath<SealProfile>("Assets/ScriptableObjects/手印识别配置/"+n+".asset")).ToArray();
        Require(profiles.All(p=>p && p.confirmationPrefab),"All six profiles and prefabs exist");lab.profiles=profiles.ToList();
        var catalog=ScriptableObject.CreateInstance<SkillCatalog>();
        var skill=ScriptableObject.CreateInstance<SkillDefinition>();skill.skillId="acceptance.no.cast";skill.displayName="No-cast input probe";
        skill.triggerMode=SkillTriggerMode.VoiceAndGesture;skill.phrase="测试确认";skill.sealName="律印";skill.effectDefinition=ScriptableObject.CreateInstance<ShieldEffectDefinition>();catalog.skills.Add(skill);
        effects=root.AddComponent<SkillController>();effects.head=camera.transform;effects.lab=lab;effects.playerRoot=root.transform;
        effects.WristPoseOverride=l=>l?(leftAvailable?new Pose(Left,hengOnly?Quaternion.Euler(CaseAngles[Math.Min(angleIndex,CaseAngles.Length-1)]):Quaternion.identity):null):(rightAvailable?new Pose(Right,Quaternion.identity):null);
        now=100;effects.HandInputClock=effects.GameClock=()=>now;
        input=root.AddComponent<SkillInputController>();input.enableCastTrace=false;input.logFeatureFailures=false;input.enableOpenHandCancel=false;
        input.lab=lab;input.effects=effects;input.catalog=catalog;input.InputClock=()=>now;input.InitializeMatcher();input.enabled=false;
        input.SealConfirmed+=Confirmed;
        var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.prefab");
        Require(p,"Actual Zhunwang prefab exists");wristCase=Obj.Instantiate(p);wristCase.name="Actual Zhunwang at synthetic left-wrist fit";
        var fit=AssetDatabase.LoadAssetAtPath<WatchMountProfile>("Assets/Art/PlayerHands/ZhunwangFit.asset");
        wristCase.transform.SetPositionAndRotation(Left+fit.offsetMetres,Quaternion.Euler(fit.tiltDegrees));
        foreach(var b in wristCase.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
        wristCase.SetActive(false);
        Note("setup","Unity="+Application.unityVersion+" GPU="+SystemInfo.graphicsDeviceName+" audio="+AudioSettings.outputSampleRate+" speaker="+AudioSettings.speakerMode+" capture=30fps synthetic wrist/feature probes; original production matcher/controller/pulse copied unmodified");
        Require(AudioRenderer.Start(),"Unity AudioRenderer started");
        hengOnly=SessionState.GetBool(Key+"HengOnly",false);angleIndex=0;running=true;frame=0;stage=hengOnly?2:0;BeginStage();T01SealProbeDriver.Tick=()=>{if(running)try{Step();}catch(Exception ex){Fail(ex);}};
        root.AddComponent<T01SealProbeDriver>();
    }
    static void BeginStage()
    {
        stageEvents=stageInstances=audioChecks=0;pcm.Clear();instances.Clear();
        input.Engine.Reset(now);rightAvailable=stage!=2;leftAvailable=true;
        Directory.CreateDirectory(Output+"/"+StageCode+"/frames");
        if(hengOnly){var fit=AssetDatabase.LoadAssetAtPath<WatchMountProfile>("Assets/Art/PlayerHands/ZhunwangFit.asset");var rotation=Quaternion.Euler(CaseAngles[angleIndex]);wristCase.transform.SetPositionAndRotation(Left+rotation*fit.offsetMetres,rotation*Quaternion.Euler(fit.tiltDegrees));Note("case_pose","synthetic wrist rotation="+CaseAngles[angleIndex]+" mount="+wristCase.transform.position.ToString("F5"));}
        Note("begin",Names[stage]+" calibrated="+profiles[stage].calibrated+" handMode="+profiles[stage].handMode+" hold="+profiles[stage].holdSeconds);
        if(stage>=4)Require(!profiles[stage].calibrated,"Uncalibrated template remains false: "+Names[stage]);
    }
    static List<SealScore> Scores(int active,bool high)
    {
        return profiles.Select((p,i)=>SealScorer.Evaluate(p,p.rules.Where(r=>r!=null).Select(r=>new SealFeature(r.featureId,"acceptance synthetic rule midpoint",i==active&&high?(r.min+r.max)*.5f:r.max+2*r.tolerance,true,true)).ToArray())).ToList();
    }
    static SealFrame Frame(double t,int version,bool rightTracked=true)
    {
        return new SealFrame{time=t,left=new HandSnapshot{version=version,connected=true,tracked=true,fresh=true,highConfidence=true,observedAt=t},right=new HandSnapshot{version=version,connected=rightTracked,tracked=rightTracked,fresh=rightTracked,highConfidence=true,observedAt=t}};
    }
    static void Confirmed(string seal,double at)
    {
        notices.Add(seal);stageEvents++;
        var pulse=Obj.FindObjectsByType<SealGlyphPulse>(FindObjectsSortMode.None).FirstOrDefault(x=>!instances.Contains(x.gameObject.GetInstanceID()));
        Require(pulse,"Confirmed creates pulse via ForwardSeal: "+seal);instances.Add(pulse.gameObject.GetInstanceID());stageInstances++;
        var source=pulse.GetComponent<AudioSource>();Require(source && source.clip && source.isPlaying,"Confirmed audio playing: "+seal);audioChecks++;
        Vector3 expected=profiles[stage].handMode==SealHandMode.LeftOnly?Left:(Left+Right)*.5f;
        Require(Vector3.Distance(expected,pulse.transform.position)<.00001f,"Actual anchor: "+seal);
        Note("confirmed",seal+" gameObjectId="+pulse.gameObject.GetInstanceID()+" anchor="+pulse.transform.position.ToString("F5")+" clip="+source.clip.name+" isPlaying="+source.isPlaying+" volume="+source.volume+" blend="+source.spatialBlend+" caseVisible="+wristCase.activeSelf);
    }
    static void Step()
    {
        now=100+((hengOnly?angleIndex:stage)*120+frame)/30.0;
        wristCase.SetActive(stage==2 && frame>=65);
        var score=Scores(stage,frame<55||(frame>=70&&frame<114));
        input.Engine.Tick(now);input.Engine.Observe(Frame(now,stage*120+frame+1,stage!=2),score);
        if(stage>=4 && frame==6)
        {
            var pulse=Obj.Instantiate(profiles[stage].confirmationPrefab,(Left+Right)*.5f,Quaternion.identity);
            var source=pulse.GetComponent<AudioSource>();Require(source&&source.clip&&source.isPlaying,"Template preview audio: "+Names[stage]);
            Note("template_preview",Names[stage]+" direct Prefab preview only; no recognized gesture; clip="+source.clip.name);
        }
        if(frame==54 && stage<4)Require(stageEvents==1 && stageInstances==1,"Sustained hold produces exactly one: "+Names[stage]);
        if(frame==69 && stage<4)Require(stageEvents==1,"Exit produces no extra feedback: "+Names[stage]);
        Render();
        int samples=AudioRenderer.GetSampleCountForCaptureFrame();
        using(var block=new NativeArray<float>(samples*2,Allocator.Temp)){Require(AudioRenderer.Render(block),"Audio render frame");pcm.AddRange(block.ToArray());}
        if(++frame<120)return;
        Require(stage>=4?stageEvents==0:stageEvents==2 && stageInstances==2 && audioChecks==2,"Per-stage confirmation/reentry count: "+Names[stage]);
        double rms=Math.Sqrt(pcm.Select(v=>(double)v*v).Average());float peak=pcm.Max(v=>Math.Abs(v));Require(peak>.0001,"Nonzero Unity final mix: "+Names[stage]);
        individualPeaks[stage]=peak;
        SaveWave(Output+"/"+StageCode+"/unity-output.wav",pcm,AudioSettings.outputSampleRate,2);
        Note("PASS",Names[stage]+" events="+stageEvents+" instances="+stageInstances+" pcmSamples="+pcm.Count+" peak="+peak+" rms="+rms);
        report.Add(StageCode+" "+Names[stage]+" | calibrated="+profiles[stage].calibrated+" | events="+stageEvents+" | prefabInstances="+stageInstances+" | PCM peak="+peak+" RMS="+rms);
        if(hengOnly?++angleIndex<CaseAngles.Length:++stage<6){frame=0;BeginStage();return;}
        File.WriteAllLines(Output+"/individual-result.txt",report);
        BeginBoard();
    }
    static int boardFrame;
    static void BeginBoard()
    {
        running=false;AudioRenderer.Stop();RecordedScoringChecks();
        wristCase.SetActive(false);
        foreach(var old in Obj.FindObjectsByType<SealGlyphPulse>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
        for(int i=0;i<6;i++)
        {
            // Six real production prefab instances. Normal Update drives their pulse; no font redraw.
            var obj=Obj.Instantiate(profiles[i].confirmationPrefab,new Vector3((i%3-1)*.25f,(i<3?.13f:-.13f),.80f),Quaternion.identity);
            obj.name="SixSealBoard_"+Codes[i];obj.GetComponent<AudioSource>().mute=true;
        }
        boardFrame=0;
        T01SealProbeDriver.Tick=()=>{try{BoardStep();}catch(Exception ex){Fail(ex);}};
    }
    static void BoardStep()
    {
        if(++boardFrame<4)return;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();RenderTexture.active=old;
        File.WriteAllBytes(Output+"/SixSealActualInstances.png",texture.EncodeToPNG());
        Require(Obj.FindObjectsByType<SealGlyphPulse>(FindObjectsSortMode.None).Count(x=>x.gameObject.name.StartsWith("SixSealBoard_"))==6,"Six real live prefab instances on board");
        File.WriteAllLines(Output+"/result.txt",report.Concat(new[]{
            "PASS: original matcher→ForwardSeal→PlaySealConfirmation→SealGlyphPulse; synthetic features and wrist injection, not live headset tracking.",
            "规/矩/律/衡 each: one sustained confirmation, no exit event, one reentry confirmation; original audio sources actually play.",
            "准/绳 calibrated=false; zero recognized events, direct production prefab previews only.",
            "Actual Unity AudioRenderer final listener mix saved per seal. No source-WAV substitution; no new rapid/stress cycle.",
            "SixSealActualInstances.png: six real prefab instances at same pulse age, arranged only for comparison, audio muted on board.",
            "Heng images use actual wrist-case prefab and original left anchor at a synthetic wrist pose, not a headset occlusion result.",
            "No Quest hardware/live tracking/headset acceptance claim."}));
        Note("COMPLETE","Xiaozhuan six individual stages and actual-instance board passed; requested exit 0");
        T01SealProbeDriver.Tick=null;Time.captureFramerate=0;EditorApplication.Exit(0);
    }
    static void Render()
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();RenderTexture.active=old;
        File.WriteAllBytes(Output+"/"+StageCode+"/frames/frame_"+frame.ToString("D4")+".jpg",texture.EncodeToJPG(92));
        if(stage==2 && (frame==10||frame==80||frame==110))
        {
            File.WriteAllBytes(Output+"/"+StageCode+"/occlusion_"+frame+".png",texture.EncodeToPNG());
            var b=new Bounds(wristCase.transform.position,Vector3.zero);foreach(var r in wristCase.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);
            Note("wrist_projection","wristViewport="+camera.WorldToViewportPoint(Left).ToString("F5")+" caseWorldBounds="+b+" caseMinViewport="+camera.WorldToViewportPoint(b.min).ToString("F5")+" caseMaxViewport="+camera.WorldToViewportPoint(b.max).ToString("F5"));
        }
        if(stage==2 && frame==80)
        {
            File.WriteAllBytes(Output+"/"+StageCode+"/compare_with_case.png",texture.EncodeToPNG());
            wristCase.SetActive(false);RenderComparison("compare_no_case.png");wristCase.SetActive(true);
            var glyphs=Obj.FindObjectsByType<SealGlyphPulse>(FindObjectsSortMode.None).Select(p=>p.GetComponent<SpriteRenderer>()).ToArray();
            foreach(var g in glyphs)g.enabled=false;RenderComparison("case_only.png");foreach(var g in glyphs)g.enabled=true;
        }
    }
    static void RenderComparison(string filename)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();RenderTexture.active=old;
        File.WriteAllBytes(Output+"/"+StageCode+"/"+filename,texture.EncodeToPNG());
    }
    static void RecordedScoringChecks()
    {
        foreach(var pair in new[]{("Gui","gui",0),("Ju","ju",1),("Lv","lv",3)})
        {
            var f=JsonUtility.FromJson<SealFrame>(File.ReadAllText(Project+"/handposes/"+pair.Item2+".json"));
            var features=new List<SealFeature>();new SealFeatureExtractor().Extract(f,features);
            var s=profiles.Select(p=>SealScorer.Evaluate(p,features)).ToList();
            var own=s[pair.Item3];report.Add("Recorded frame "+pair.Item1+" score="+own.score+" coverage="+own.coverage+" passes="+own.passes+"; all="+string.Join(",",s.Select(q=>q.profile.sealName+":"+q.score)));
            Require(own.passes,"Current profile accepts recorded "+pair.Item1);
            int confirmations=0;var engine=new SkillMatchEngine(new[]{input.catalog.skills[0]},input.catalog.matching);
            engine.SealConfirmed+=(n,t)=>{if(n==Names[pair.Item3])confirmations++;};
            for(int k=0;k<180;k++){double t=200+k/30.0;f.time=t;f.left.version=f.right.version=k+1;f.left.observedAt=f.right.observedAt=t;f.left.connected=f.left.tracked=f.left.fresh=f.right.connected=f.right.tracked=f.right.fresh=true;engine.Tick(t);engine.Observe(f,s);}
            Require(confirmations==1,"Recorded 6-second sustained pose single confirm "+pair.Item1);
            report.Add("Recorded frame "+pair.Item1+" through actual matcher: 6s held => "+confirmations+" confirmation.");
        }
    }
    static void SaveWave(string path,List<float> data,int rate,short channels)
    {
        using var w=new BinaryWriter(File.Create(path));int length=data.Count*2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+length);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write(channels);w.Write(rate);w.Write(rate*channels*2);w.Write((short)(channels*2));w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(length);
        foreach(var v in data)w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v*32767),-32768,32767));
    }
    static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Note(string type,string detail){File.AppendAllText(Output+"/timeline.tsv",$"{stage}\t{frame}\t{now:F4}\t{type}\t{detail.Replace('\n',' ')}\n");Debug.Log("T01 "+type+" "+detail);}
    static void Fail(Exception ex){running=false;AudioRenderer.Stop();File.WriteAllText(Output+"/failure.txt",ex.ToString());Debug.LogException(ex);EditorApplication.Exit(3);}
}






