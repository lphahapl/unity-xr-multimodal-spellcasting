using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Obj = UnityEngine.Object;

[InitializeOnLoad]
public static class HearthNarrationQA
{
    const string Key = "HearthNarration1127.qa";
    const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly List<XRDisplaySubsystem> displays = new();
    static string Output => Environment.GetEnvironmentVariable("CH02_HEARTH_QA");
    static T Find<T>() where T : Component => Obj.FindFirstObjectByType<T>();
    static HearthNarrationQA() => EditorApplication.update += Tick;

    static void NoXR(XRGeneralSettings settings)
    {
        displays.Clear();
        SubsystemManager.GetSubsystems(displays);
        if (!settings || settings.InitManagerOnStart || !settings.Manager || settings.Manager.activeLoader ||
            settings.Manager.activeLoaders.Count != 0 || displays.Any(x => x.running))
            throw new InvalidOperationException("XR isolation failed.");
    }

    public static void Run()
    {
        if (!Application.isBatchMode || Application.companyName != "OfflineChapter02VoiceQA" ||
            !Application.dataPath.Replace('\\','/').Equals("D:/Temp/Chapter02Voice1127/Project/Assets",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run only in the narration QA copy.");
        NoXR(XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone));
        NarutoVR.SealCombat.Editor.NarrationTableChecks.Run();
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var route = Find<Chapter02GoldTrail>();
        var supply = Find<ChapterRationSupply>();
        route.player.autoLoad = false;
        route.player.saveFileName = "hearth-narration-"+Guid.NewGuid().ToString("N")+".json";
        foreach (var receiver in Obj.FindObjectsByType<SkillVoiceReceiver>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            receiver.autoStartInEditor = false;
            receiver.enabled = false;
        }
        var layout = Environment.GetEnvironmentVariable("HEARTH_LOCATION");
        if (!string.IsNullOrEmpty(layout)) HearthFoodInstaller.Configure(supply, route, layout);
        else if (!supply.bagSurface || !supply.biscuitSurface)
        {
            // Private lower-storey test anchors. The scene integrator owns the actual furnace placement.
            supply.hearthEncounter = route.altar;
            HearthFoodInstaller.ConnectSharedMouth(supply);
            var fixture = new GameObject("Workshop-height narration fixture");
            fixture.transform.position = new Vector3(35.6f,.83f,26f);
            supply.bagSurface = Anchor("Bag",-.14f);
            supply.biscuitSurface = Anchor("Biscuit",.14f);
            Transform Anchor(string name,float x)
            {
                var anchor = new GameObject(name).transform;
                anchor.SetParent(fixture.transform,false);
                anchor.SetLocalPositionAndRotation(new Vector3(x,0,0),Quaternion.Euler(-90,0,0));
                return anchor;
            }
        }
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }

    static async void Tick()
    {
        if (!Application.isPlaying || !SessionState.GetBool(Key,false) || Time.frameCount<15) return;
        SessionState.EraseBool(Key);
        try { await Exercise(); Check(true,"END"); EditorApplication.Exit(0); }
        catch (Exception error) { File.AppendAllText(Path.Combine(Output,"checks.txt"),error+"\n"); EditorApplication.Exit(2); }
    }

    static void Check(bool condition,string label)
    {
        File.AppendAllText(Path.Combine(Output,"checks.txt"),(condition ? "PASS " : "FAIL ")+label+"\n");
        if (!condition) throw new InvalidOperationException(label);
    }

    static async Task Until(Func<bool> test,string label,Func<string> describe = null)
    {
        double deadline = EditorApplication.timeSinceStartup + 31d;
        while (!test() && EditorApplication.timeSinceStartup < deadline) await Awaitable.NextFrameAsync();
        Check(test(),label+(describe == null ? "" : " / "+describe()));
    }

    static async Task Next(int frames = 2)
    {
        for (int i=0;i<frames;i++) await Awaitable.NextFrameAsync();
    }

    static async Task Exercise()
    {
        var story=Find<Chapter02Narration>();
        var route=Find<Chapter02GoldTrail>();
        var supply=Find<ChapterRationSupply>();
        var player=route.player;
        var loc=player.locomotion;
        var n=story.GetComponentInChildren<NarrationController>();
        var audio=n.GetComponent<AudioSource>();
        n.StepStarted += step => File.AppendAllText(Path.Combine(Output,"steps.txt"),
            $"{Time.time:F2} {step.LineId} stat={player.Status} near={supply.IsNearHearth} picked={supply.HearthBagPickedUp} stowed={supply.HearthBagStowed}\n");
        var body=typeof(Chapter02Narration);
        var lines=(NarrationLine[])body.GetField("script",PrivateInstance).GetValue(story);
        object Call(string name,params object[] args)=> body.GetMethod(name,PrivateInstance).Invoke(story,args);
        bool CanStart(string id)=>(bool)Call("MayStart",id);
        bool Done(string id)=>(bool)Call("OperationReached",id);
        string Hint()=>(string)Call("CurrentHint");
        NarrationLine Line(string id)=>lines.Single(x=>x.lineId==id);
        void Move(Vector3 point)
        {
            loc.rigRoot.position+=point-loc.PlayerFloorPosition;
            Physics.SyncTransforms();
        }

        NoXR(XRGeneralSettings.Instance);
        Check(new SerializedObject(story).FindProperty("hearthProgress").objectReferenceValue==supply,"serialized narration shares real supply");
        Check(lines.Length==32 && lines.Select(x=>x.lineId).Distinct().Count()==32,"all 32 line IDs retained");
        await Until(()=>player.IsReady && n.CurrentStep?.LineId=="EN01" && audio.isPlaying,"actual first entry voice plays by itself",()=>player.Status);
        var captions=loc.head.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t=>t.name=="申屠字幕");
        await Until(()=>captions.text.StartsWith("申屠："),"wired voice uses the head-mounted subtitles");
        story.enabled=false;
        Move(player.CheckpointAt(1).floorPosition);
        await Until(()=>route.bridge.drawingObjective.IsRunning,"actual desk phase");
        route.bridge.drawingObjective.Complete();
        await Until(()=>player.StageAt(player.StageIndex)==route.bridge.originalFirstStage,"actual altar phase");
        var post=Find<StoneCarrierPost>();
        Move(post.transform.position-Vector3.ProjectOnPlane(post.patrolPoints[0].position-post.transform.position,Vector3.up).normalized*3);
        await Until(()=>route.altar.HasStarted && route.altar.enemies.Length==3,"actual three altar enemies load");
        var enemies=route.altar.enemies.ToArray();
        for (int i=0;i<enemies.Length;i++)
        {
            var entity=enemies[i].Entity;
            Check(entity.ReceiveHit("hearth-narration-qa-"+i,entity.Combat.Health,0,attacker:loc.head),"actual altar kill "+(i+1));
            await Next(3);
            if(i<2) Check(!supply.HearthBagOffered && !CanStart("RT01"),"partial clear never enables food cue");
        }
        await Until(()=>route.altar.IsComplete && supply.HearthBagOffered,"all three deaths actually offer the bag");
        Check(CanStart("AB01") && !CanStart("RT01") && Hint()=="HT03","upper departure and reminder independent of untouched lower bag");
        var shelf=supply.bagSurface.position;
        Move(new Vector3(shelf.x,80,shelf.z));
        Check(!supply.IsNearHearth && !CanStart("RT01"),"same XZ on upper level does not pass the lower furnace range");

        var snapshot=new RpgCheckpointData();
        supply.CaptureEquipment(snapshot);
        Check(snapshot.hearthFood.offered && !snapshot.hearthFood.bagPickedUp,"save captures untouched shelf offer");
        Check(player.TryActivateStageCheckpoint(route.baskets.middleCheckpoint) && player.RestoreCheckpoint(),"use actual private middle checkpoint");
        await Until(()=>route.baskets.AtMiddle,"restored A basket on actual middle station");
        ReachAreaChapterObjective RunningArea() =>
            Obj.FindObjectsByType<ReachAreaChapterObjective>(FindObjectsSortMode.None).SingleOrDefault(o=>o.IsRunning);
        await Until(()=>RunningArea(),"middle-stage area is rebound after loading");
        var reach=RunningArea();
        var area=reach.area;
        Move(area.transform.TransformPoint(area.center));
        await Until(()=>player.StageAt(player.StageIndex)==route.workshop.stage,"actual middle-floor area advances workshop stage");
        Move(new Vector3(shelf.x+4,0,shelf.z));
        Check(!supply.IsNearHearth && !CanStart("RT01"),"away from the furnace no invitation starts");

        var belt=supply.interaction.sharedBelt;
        var biscuit=belt.items.OfType<ConsumableItem>().Single(i=>i&&i.config==supply.pouchPrefab.servingPrefab.config);
        player.Player.Respawn(player.Player.MaxHealth,20,player.GameTime);
        Check(biscuit.Take() && supply.HearthBiscuitPickedUp && !supply.HearthBagPickedUp,"actual separate biscuit pickup does not complete pack pickup");
        Check(!biscuit.Eat(player.Player) && !Done("RT01") && !CanStart("RT02"),"full health neither consumes biscuit nor changes the bag lesson");
        biscuit.DropInWorld();

        var bag=supply.interaction.pouch;
        Check(!supply.IsNearHearth && bag.Take() && supply.HearthBagPickedUp,"real early pack receipt arrives before proximity");
        Check(!CanStart("RT01") && CanStart("RT02") && Done("RT01"),"early pack take skips redundant invitation, enables follow-up");
        supply.RestoreEquipment(snapshot);
        await Until(()=>supply.HasPouch && !supply.HearthBagPickedUp,"read untouched shelf before proximity case");
        Move(new Vector3(shelf.x+2,0,shelf.z));
        Check(supply.IsNearHearth && CanStart("RT01") && !Done("RT01") && !CanStart("RT02"),"same-floor furnace range enables new invitation and waits for actual bag");
        audio.pitch=3;
        player.Player.Respawn(player.Player.MaxHealth,20,player.GameTime);
        story.enabled=true;
        await Until(()=>n.CurrentStep?.LineId=="RT01" && audio.isPlaying,"live scheduler plays the new workshop invitation",()=>n.Status);
        Check(audio.clip.name=="RT01_WorkshopHearth","new RT01 WAV and matching script are bound");
        bag=supply.interaction.pouch;
        Check(bag.Take(),"real pack pickup after live invitation");
        Check(Done("RT01") && CanStart("RT02") && !supply.HearthBagStowed,"new live receipt immediately enables RT02");
        audio.pitch=3;
        await Until(()=>n.CurrentStep?.LineId=="RT02" && audio.isPlaying,"actual pickup unlocks RT02 without eating",()=>$"{n.Status}; t={Time.time}; scale={Time.timeScale}; act={player.CanAct}; player={player.Status}; phase={player.StageIndex}; picked={supply.HearthBagPickedUp} stowed={supply.HearthBagStowed} canRT02={CanStart("RT02")}");
        await Until(()=>n.CurrentStep?.LineId=="RT03" && audio.isPlaying,"RT03 plays the stow explanation",()=>n.Status);
        Check(audio.clip.name=="RT03_WorkshopHearth" && !Done("RT03"),"before docking the stow step remains incomplete");
        var home=supply.centralPocket;
        var rotation=Quaternion.Euler(bag.stowEuler);
        bag.MoveHeld(home.opening.position,home.opening.rotation*rotation);
        bag.Drop();
        Physics.SyncTransforms();
        Check(bag.TryDock(home,rotation),"actual release followed by docking into central pocket");
        await Until(()=>supply.HearthBagStowed && !home.IsSnapping,"central pocket snap emits the real stow receipt");
        Check(Done("RT03") && Hint()!="HT02" && bag.InPouch==4,"stow completes lesson with all four portions uneaten");

        supply.RestoreEquipment(snapshot);
        await Until(()=>supply.HasPouch && !supply.HearthBagPickedUp,"restore to test optional audio priority again");
        var workshop = route.workshop;
        var workshopArea=workshop.roof.area;
        Move(workshopArea.transform.TransformPoint(workshopArea.center));
        await Until(()=>workshop.roof.HasStarted && workshop.courtyard.HasStarted &&
            workshop.roof.enemies.Length>0 && workshop.courtyard.enemies.Length>0,"real workshop encounters enter combat");
        n.StopDialogue();
        Call("Play",Line("RT01"));
        await Until(()=>audio.isPlaying && n.CurrentStep?.LineId=="RT01","optional ration line active before workshop clears");
        int kill=0;
        foreach(var enemy in workshop.roof.enemies.Concat(workshop.courtyard.enemies).ToArray())
        {
            var entity=enemy.Entity;
            Check(entity.ReceiveHit("hearth-workshop-qa-"+kill++,entity.Combat.Health,0,attacker:loc.head),"real test kill for workshop clear");
        }
        await Until(()=>workshop.IsUnlocked,"actual workshop clearing unlocks the master tenon");
        await Until(()=>n.CurrentStep?.LineId=="WS03" && audio.isPlaying,"workshop tenon instruction preempts optional ration",()=>n.Status);
        Check(!supply.HearthBagPickedUp && !supply.HearthBagStowed,"main instruction did not require ration completion");
        Check(Obj.FindObjectsByType<NarrationController>(FindObjectsSortMode.None).Length==1,"one shared voice source");

        story.enabled=false;
        foreach(var line in lines)
        {
            var path="Narration/"+Path.ChangeExtension(line.audioFileName,null).Replace('\\','/');
            var clip=Resources.Load<AudioClip>(path);
            Check(clip && clip.channels==1 && clip.samples>24000,"resource "+line.lineId+" "+line.audioFileName);
            Call("Play",line);
            await Until(()=>audio.isPlaying && audio.clip==clip && n.CurrentStep?.LineId==line.lineId,"single AudioSource actually plays "+line.lineId);
            n.StopDialogue();
            await Next(1);
        }
        NoXR(XRGeneralSettings.Instance);
    }
}
