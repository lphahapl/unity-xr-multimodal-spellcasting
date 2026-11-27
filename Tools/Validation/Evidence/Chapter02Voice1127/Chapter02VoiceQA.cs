using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using Obj=UnityEngine.Object;

[InitializeOnLoad] public static class Chapter02VoiceQA
{
    const string Key="Chapter02VoiceQA.armed";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static string Out=>Environment.GetEnvironmentVariable("CH02_VOICE_QA");
    static int state,frames,slot,count;
    static double until;
    static readonly List<string> report=new();
    static readonly List<XRDisplaySubsystem> xr=new();
    static LinearRpgController player;
    static Chapter02Narration story;
    static Chapter02GoldTrail trail;
    static NarrationLine[] script;
    static NarrationController voice;
    static AudioSource source;
    static string Mode=>Environment.GetEnvironmentVariable("CH02_VOICE_ALL");
    static Chapter02VoiceQA(){EditorApplication.update+=Tick;}
    static void Guard(XRGeneralSettings s)
    {
        xr.Clear();SubsystemManager.GetSubsystems(xr);
        if(!s || s.InitManagerOnStart || !s.Manager || s.Manager.activeLoader ||
            s.Manager.activeLoaders.Count!=0 || xr.Exists(x=>x.running)) throw new Exception("XR isolation failed");
    }
    public static void Run()
    {
        if(!Application.isBatchMode || Application.companyName!="OfflineChapter02VoiceQA")throw new Exception("wrong project");
        Guard(XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone));
        NarutoVR.SealCombat.Editor.NarrationTableChecks.Run();
        EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var player=Obj.FindFirstObjectByType<LinearRpgController>();player.autoLoad=false;player.saveFileName="chapter02-voice-"+Guid.NewGuid().ToString("N")+".json";
        if (Mode=="cold")
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.Copy(Path.Combine(Out,"seated.json"),Path.Combine(Application.persistentDataPath,player.saveFileName));
            player.autoLoad=true;
        }
        foreach(var v in Obj.FindObjectsByType<SkillVoiceReceiver>(FindObjectsInactive.Include,FindObjectsSortMode.None)){v.enabled=false;v.autoStartInEditor=false;}
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static object Call(string method,params object[] a)=>typeof(Chapter02Narration).GetMethod(method,Flags).Invoke(story,a);
    static T Field<T>(string field)=>(T)typeof(Chapter02Narration).GetField(field,Flags).GetValue(story);
    static void Field(string field,object value)=>typeof(Chapter02Narration).GetField(field,Flags).SetValue(story,value);
    static void Check(bool b,string msg){if(!b)throw new Exception(msg);report.Add("PASS "+msg);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying || !SessionState.GetBool(Key,false))return;
        try
        {
            Guard(XRGeneralSettings.Instance);
            if(until==0)until=EditorApplication.timeSinceStartup+330;
            if(EditorApplication.timeSinceStartup>until)throw new Exception("timeout stage="+state+" line "+voice?.CurrentStep?.LineId+" "+voice?.Status);
            if(state==0)
            {
                player=Obj.FindFirstObjectByType<LinearRpgController>();
                if(!player || !player.IsReady)return;
                story=Obj.FindFirstObjectByType<Chapter02Narration>();trail=Obj.FindFirstObjectByType<Chapter02GoldTrail>();
                Check(story&&story.isActiveAndEnabled&&trail.player==player,"Scene contains the configured real chapter voice and player");
                script=Field<NarrationLine[]>("script");
                Check(script.Length==32&&script.Select(x=>x.lineId).Distinct().Count()==32,"32 authored rows");
                voice=story.GetComponentInChildren<NarrationController>(); source=voice.GetComponent<AudioSource>();
                Check(Obj.FindObjectsByType<NarrationController>(FindObjectsSortMode.None).Length==1&&source.spatialBlend==0,"One non-spatial voice");
                Check(Field<GlyphTemplate>("branchTemplate") && Field<GlyphTemplate>("branchTemplate").effect is PassageGlyphEffect,
                    "North branch subscribed to actual Passage template");
                if (Mode=="cold")
                {
                    Check(player.StageAt(player.StageIndex).stageId==Chapter02BasketBControl.StageId,"Cold fixture restores real B stage");
                    Check(!(bool)Call("MayStart","EN01") && !(bool)Call("MayStart","AD01") && !(bool)Call("MayStart","AD02") &&
                        !(bool)Call("MayStart","BDN05"),"Cold B entry does not replay entry, A travel, or valley arrival");
                    Check((bool)Call("MayStart","BDN01"),"Cold B entry resumes matching console line");
                    state=40;
                    return;
                }
                Check((bool)Call("MayStart","EN01")&&!(bool)Call("MayStart","RT01")&&!(bool)Call("MayStart","AD01")&&!(bool)Call("MayStart","BDN05"),"Fresh entry only enables matching phase");
                state=1;
            }
            if(state==40)
            {
                if(voice.CurrentStep?.LineId!="BDN01" || !source.isPlaying)return;
                Check(source.clip.name=="BDN01","B stage recording actually resumed after cold load");
                Finish(0);
                return;
            }
            if(state==1)
            {
                if(voice.CurrentStep?.LineId!="EN01" || !source.isPlaying)return;
                Check(source.clip.name=="EN01","Fresh scene started real EN01 by itself");
                Check(source.clip.channels==1&&source.clip.samples>0,"Narration clip sampled as mono");
                state=2; frames=0;
            }
            if(state==2)
            {
                if(++frames<5)return;
                var label=player.locomotion.head.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(x=>x.name=="申屠字幕");
                Check(label.text.StartsWith("申屠：") && label.font,"Head-mounted captions are rendering the real line");
                var region=Obj.FindObjectsByType<ReachAreaChapterObjective>(FindObjectsSortMode.None).Single(o=>o.IsRunning);
                var loc=player.locomotion;
                loc.rigRoot.position+=region.area.transform.TransformPoint(region.area.center)-loc.PlayerFloorPosition;
                Physics.SyncTransforms();
                state=3;
                return;
            }
            if(state==3)
            {
                if(player.StageIndex!=1)return;
                Check(voice.CurrentStep?.LineId=="EN01"&&source.clip.name=="EN01","Stone desk real progress does not overlap the entry sentence");
                Check((bool)Call("MayStart","BR01")&&!(bool)Call("MayStart","BR02"),"Real drawing stage enables BR01, waits for glyph success before BR02");
                Check(Obj.FindObjectsByType<NarrationController>(FindObjectsSortMode.None).Length==1,"No temporary bridge voice coexists");
                if(Mode=="no"){Finish(0);return;}
                // Let the real transition finish the entry clip and its gap before BR01.
                source.pitch=3;
                state=4;
            }
            if(state==4)
            {
                if(voice.CurrentStep?.LineId!="BR01" || !source.isPlaying)return;
                Check(source.clip.name=="BR01","BR01 started after current entry sentence ended");
                trail.bridge.panel.onCompleted.Invoke();
                Check((bool)Call("OperationReached","BR01") && (bool)Call("MayStart","BR02"),"Actual bridge recognized callback advances to BR02");
                state=5;
            }
            if(state==5)
            {
                if(voice.CurrentStep?.LineId!="BR02" || !source.isPlaying)return;
                Check(source.clip.name=="BR02","BR02 actually plays on the same source");
                if(Mode=="chapter"){Finish(0);return;} voice.StopDialogue();story.enabled=false;
                // Use the production line start/controller/audio path for each serialized line.
                state=6;slot=0;
                frames=0;
                return;
            }
            if(state==6)
            {
                if(slot==script.Length){Finish(0);return;}
                var line=script[slot];var clip=Resources.Load<AudioClip>("Narration/"+Path.GetFileNameWithoutExtension(line.audioFileName).Insert(0,"Chapter02/"));
                if(frames++==0)
                {
                    Check(clip&&clip.name==line.lineId&&clip.samples>24000&&clip.channels==1,"Resources load "+line.lineId);
                    Call("Play",line);
                }
                if(!source.isPlaying)return;
                Check(voice.CurrentStep?.LineId==line.lineId&&source.clip==clip,"AudioSource playing "+line.lineId);
                voice.StopDialogue();slot++;frames=0;count++;
            }
        }
        catch(Exception e){report.Add(e.ToString());Finish(2);}
    }
    static void Finish(int code)
    {
        report.Add("isolatedXR=0 playedEach="+count+" exit="+code);
        File.WriteAllLines(Path.Combine(Out,"checks.txt"),report);
        SessionState.EraseBool(Key);EditorApplication.Exit(code);
    }
}
