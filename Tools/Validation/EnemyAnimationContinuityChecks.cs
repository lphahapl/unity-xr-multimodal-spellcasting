using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnemyAnimationContinuityChecks
{
    const string Key = "NarutoVR.EnemyAnimationContinuity";
    const float Dt = .025f;
    static readonly List<string> failures = new();
    static readonly string[] scenes = {"Chapter01_Awakening", "KaogongP1Trial"};
    static readonly string folder = "Logs/enemy-animation";
    static int checks;
    static double deadline;
    static string Copy(string name) => "Assets/Scene/QA_EnemyAnimation_" + name + ".unity";
    static void Log(string text) => File.AppendAllText(Path.Combine(folder,"result.txt"),text+"\n");
    static void Check(bool ok, string detail) { checks++; Log((ok?"PASS ":"FAIL ")+detail); if(!ok) failures.Add(detail); }

    static EnemyAnimationContinuityChecks() { EditorApplication.playModeStateChanged += OnMode; }
    public static void Run()
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !Path.GetFullPath(Application.dataPath).StartsWith(temp,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请在临时项目批处理运行。");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"result.txt"),DateTimeOffset.Now.ToString("o")+"\n");
        Log("Play data builder: "+UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilder.Name);
        foreach (string name in scenes)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scene/"+name+".unity");
            var rpg = UnityEngine.Object.FindFirstObjectByType<LinearRpgController>();
            rpg.autoLoad=false; rpg.saveFileName="qa-enemy-animation-"+Guid.NewGuid().ToString("N")+".json";
            rpg.skills.voice.autoStartInEditor=false;
            if (!EditorSceneManager.SaveScene(scene,Copy(name))) throw new IOException(Copy(name));
        }
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Copy(scenes[0]));
        SessionState.SetBool(Key,true);SessionState.SetInt(Key+".code",1);
        EditorApplication.EnterPlaymode();
    }
    static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key,false)) return;
        if (state==PlayModeStateChange.EnteredPlayMode)
        {
            deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=Watchdog;
            Verify();
        }
        if (state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);
            EditorSceneManager.playModeStartScene=null;
            EditorApplication.Exit(SessionState.GetInt(Key+".code",1));
        }
    }
    static void Watchdog()
    {
        if(EditorApplication.timeSinceStartup<deadline) return;
        Log("FAIL watchdog");EditorApplication.update-=Watchdog;EditorApplication.ExitPlaymode();
    }
    static async void Verify()
    {
        try
        {
            failures.Clear(); checks=0;
            Time.captureDeltaTime=Dt;
            for(int i=0;i<scenes.Length;i++)
            {
                if(i>0)
                {
                    var change=EditorSceneManager.LoadSceneAsyncInPlayMode(Copy(scenes[i]),new LoadSceneParameters(LoadSceneMode.Single));
                    while(!change.isDone) await Task.Yield();
                }
                var room=UnityEngine.Object.FindObjectsByType<CombatRoomEncounter>(FindObjectsSortMode.None)
                    .First(x=>x.spawnPoints.Any(s=>s!=null&&s.point));
                var manager=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
                var spawn=room.spawnPoints.First(s=>s!=null&&s.point);
                var target=new GameObject("EnemyAnimationTarget").transform;
                var lease=await manager.SpawnEnemyAsync("canjiou",spawn.point.position,spawn.point.rotation,target,true);
                try
                {
                    var e=lease.Controller;e.tickAutomatically=false;
                    var bones=e.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s=>s.bones).Where(b=>b&&b!=e.transform).Distinct().ToArray();
                    Check(bones.Length>2,scenes[i]+" real skinned bones="+bones.Length);
                    target.position=e.transform.position+e.transform.forward*.65f;
                    e.checkLineOfSight=false;
                    Reactions(e,bones,scenes[i],EnemyReaction.Hit,3.2f);
                    Reactions(e,bones,scenes[i],EnemyReaction.Seated,3.2f);
                    Reactions(e,bones,scenes[i],EnemyReaction.Hit,e.reactionTimeout+2);
                    Turning(e,bones,target,scenes[i],1);
                    Turning(e,bones,target,scenes[i],-1);
                    Attack(e,bones,target,scenes[i]);
                    await PausedClock(e,bones,scenes[i]);
                    e.animator.speed=.8f;
                    e.TryNotifyHit();e.SetSuspended(true);
                    var old=e;
                    Check(manager.Recycle(lease),scenes[i]+" original lease recycled");
                    lease=await manager.SpawnEnemyAsync("canjiou",spawn.point.position,spawn.point.rotation,target,true);
                    e=lease.Controller;e.tickAutomatically=false;
                    e.animator.Update(0);
                    Check(e==old && e.CurrentAttack<0 && Mathf.Approximately(e.animator.speed,.8f) && !e.IsDead && !e.Entity.Combat.Broken,scenes[i]+" reuse during paused reaction preserves configured speed");
                    await Navigation(e,target,scenes[i]);
                }
                finally { manager.Recycle(lease); UnityEngine.Object.Destroy(target.gameObject); }
            }
            Log(checks+" checks / "+failures.Count+" failures");
            Log(failures.Count==0?"PASS END":"FAIL END");
            SessionState.SetInt(Key+".code",failures.Count==0?0:1);
        }
        catch(Exception error) {Log("FAIL "+error);Debug.LogException(error);}
        finally {Time.captureDeltaTime=0;EditorApplication.update-=Watchdog;EditorApplication.ExitPlaymode();}
    }
    static void Step(EnemyController e){e.Tick(Dt);e.animator.Update(Dt);}
    static Quaternion[] Pose(Transform[] bones)=>bones.Select(b=>b.localRotation).ToArray();
    static float Displacement(Transform[] bones,Quaternion[] pose)
    {
        float max=0;
        for(int i=0;i<bones.Length;i++)max=Mathf.Max(max,Quaternion.Angle(bones[i].localRotation,pose[i]));
        return max;
    }
    static void Reactions(EnemyController e,Transform[] bones,string scene,EnemyReaction reaction,float pauseSeconds)
    {
        e.ResetForSpawn();e.animator.Update(0);
        bool hit=reaction==EnemyReaction.Hit ? e.TryNotifyHit() :
            e.Entity.ReceiveHit("animation-poise-"+Guid.NewGuid().ToString("N"), 0, e.Entity.Combat.MaxPoise);
        Check(hit,scene+" entered "+reaction);
        e.SetSuspended(true);e.SetSuspended(true);
        var start=Pose(bones);float pausedMotion=0;
        for(int i=0;i<Mathf.RoundToInt(pauseSeconds/Dt);i++){Step(e);pausedMotion=Mathf.Max(pausedMotion,Displacement(bones,start));}
        e.SetSuspended(false);
        int orphan=0,longest=0;
        bool played=false,released=false;
        float postMotion=0;
        var before=Pose(bones);
        for(int i=0;i<360;i++)
        {
            Step(e);
            var state=e.animator.GetCurrentAnimatorStateInfo(0);
            bool acting=e.CurrentBehaviour.ToString()==reaction.ToString();
            bool visible=state.IsName("Base Layer."+reaction) || e.animator.IsInTransition(0);
            if(acting&&visible) {played=true;postMotion=Mathf.Max(postMotion,Displacement(bones,before));}
            orphan=acting&&!visible?orphan+1:0;longest=Math.Max(longest,orphan);
            if(!acting){released=true;break;}
        }
        Log(FormattableString.Invariant($"TRACE {scene} {reaction} pause={pauseSeconds:F2}s boneMax={pausedMotion:F2} deg postMotion={postMotion:F2} orphanAI={longest*Dt:F3}s played={played} released={released} state={e.animator.GetCurrentAnimatorStateInfo(0).fullPathHash}"));
        Check(pausedMotion<.12f,scene+" "+reaction+" retains pose while explicitly suspended");
        Check(played&&released&&longest*Dt<.20f&&postMotion>.25f,scene+" "+reaction+" visibly resumes and unlocks without watchdog");
    }
    static void Turning(EnemyController e,Transform[] bones,Transform target,string scene,int sign)
    {
        e.InitializeSpawn(target,true);e.checkLineOfSight=false;e.animator.Update(0);
        float motion=0, normalized=0;
        for(int i=0;i<162;i++)
        {
            target.position=e.transform.position+Quaternion.AngleAxis(sign*100,Vector3.up)*e.transform.forward*.65f;
            var before=Pose(bones);Step(e);
            if(i>=124){motion+=Displacement(bones,before);normalized=e.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;}
        }
        var clips=e.animator.GetCurrentAnimatorClipInfo(0);
        string sources=string.Join(",",clips.Select(info=>info.clip.name+" loop="+info.clip.isLooping+" asset="+AssetDatabase.GetAssetPath(info.clip)));
        Log(FormattableString.Invariant($"TRACE {scene} sustained-turn side={sign} behaviour={e.CurrentBehaviour} normalized={normalized:F2} boneMovement={motion:F2} deg /0.95s actual={sources}"));
        Check(e.CurrentBehaviour==EnemyController.Behaviour.Turn && motion>1,scene+" sustained rotation continues to animate limbs");
    }
    static async Task PausedClock(EnemyController e,Transform[] bones,string scene)
    {
        e.ResetForSpawn();
        e.TryNotifyHit();
        for(int i=0;i<7;i++)Step(e);
        string name=scene+" global timeScale=0 retains reaction pose then resumes";
        float scale=Time.timeScale, capture=Time.captureDeltaTime;
        try
        {
            // Compare two completed Unity frames after pause takes effect. Manual Animator.Update
            // and timeScale changes in the initiating frame do not represent a paused player loop.
            Time.captureDeltaTime=0;
            Time.timeScale=0;
            await Task.Yield();await Task.Yield();
            var before=Pose(bones);float position=e.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            for(int i=0;i<6;i++){await Task.Yield();e.Tick(Time.deltaTime);}
            float pausedBone=Displacement(bones,before),pausedClock=Mathf.Abs(e.animator.GetCurrentAnimatorStateInfo(0).normalizedTime-position);
            Time.timeScale=scale;
            for(int i=0;i<5;i++)Step(e);
            float resumedBone=Displacement(bones,before);
            Log(FormattableString.Invariant($"TRACE {scene} globalPause poseDelta={pausedBone:F2} normalizedDelta={pausedClock:F4} resumePose={resumedBone:F2} mode={e.animator.updateMode}"));
            Check(pausedBone<.12f && pausedClock<.001f && resumedBone>.2f,name);
        }
        finally{Time.timeScale=scale;Time.captureDeltaTime=capture;}
    }
    static void Attack(EnemyController e,Transform[] bones,Transform target,string scene)
    {
        e.InitializeSpawn(target,true); e.checkLineOfSight=false;
        target.position=e.transform.position+e.transform.forward*.65f;
        int started=0,ended=0;
        UnityEngine.Events.UnityAction<int> begin=_=>started++;UnityEngine.Events.UnityAction<int> finish=_=>ended++;
        e.onAttackStarted.AddListener(begin);e.onAttackFinished.AddListener(finish);
        float movement=0;bool interrupted=false;
        try{
            for(int i=0;i<340;i++){
                var before=Pose(bones);Step(e);
                if(e.animator.GetCurrentAnimatorStateInfo(0).IsTag("EnemyAttack")) movement+=Displacement(bones,before);
                if(!interrupted&&started>0){e.SetSuspended(true);for(int j=0;j<12;j++)Step(e);e.SetSuspended(false);interrupted=true;}
            }
            e.CancelAttack();
            Check(interrupted&&started>=2&&ended==started&&movement>2,scene+" attack cancel/re-enter balanced starts="+started+" finishes="+ended+" bone="+movement.ToString("F1",CultureInfo.InvariantCulture));
        }finally{e.onAttackStarted.RemoveListener(begin);e.onAttackFinished.RemoveListener(finish);}
    }
    static async Task Navigation(EnemyController e,Transform target,string scene)
    {
        var agent=e.Agent;var origin=e.transform.position;var path=new NavMeshPath();
        bool found=false;
        for(int i=0;i<12&&!found;i++){
            var heading=Quaternion.AngleAxis(30*i,Vector3.up)*Vector3.forward;
            if(!NavMesh.SamplePosition(origin+heading*4,out var point,1.5f,agent.areaMask) ||
                Vector3.Distance(origin,point.position)<2.5f || !agent.CalculatePath(point.position,path) || path.status!=NavMeshPathStatus.PathComplete)continue;
            target.position=point.position;found=true;
        }
        Check(found,scene+" sampled reachable NavMesh destination");
        if(!found)return;
        e.checkLineOfSight=false; bool chase=false;
        for(int i=0;i<48;i++){e.Tick(Dt);chase|=e.CurrentBehaviour==EnemyController.Behaviour.Chase; await Task.Yield();}
        Check(chase && Vector3.Distance(origin,e.transform.position)>.15f,scene+" real NavMesh root moved after reuse, metres="+Vector3.Distance(origin,e.transform.position).ToString("F2",CultureInfo.InvariantCulture));
    }
}
