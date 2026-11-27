using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Management;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class FormalWorkshopReload
    {
        const string Key="Formal.Tenon.ColdCheck";
        static int frames;
        static double begin;
        static FormalWorkshopReload() {EditorApplication.update+=Poll;}
        public static void Run()
        {
            string which=Environment.GetEnvironmentVariable("CH02_COLD_STATE");
            if(which!="middle" && which!="seated")throw new ArgumentException("Specify middle or seated");
            EditorSceneManager.OpenScene(WorkshopTenonAssembly.SourceScene);
            var p=UnityEngine.Object.FindFirstObjectByType<LinearRpgController>();
            if(p.saveFileName!=FormalWorkshopAssembly.ChapterFile || !p.autoLoad)
                throw new Exception("The production player was not configured for its own save");
            var tmp="tenon-cold-qa-"+Guid.NewGuid().ToString("N")+".json";
            p.saveFileName=tmp;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.Copy(Path.GetFullPath("../"+which+"-checkpoint.json"),Path.Combine(Application.persistentDataPath,tmp));
            p.skills.voice.autoStartInEditor=false;
            if(XRGeneralSettings.Instance) XRGeneralSettings.Instance.InitManagerOnStart=false;
            SessionState.SetString(Key,which);
            EditorApplication.EnterPlaymode();
        }
        static void Poll()
        {
            string which=SessionState.GetString(Key,"");
            if(string.IsNullOrEmpty(which) || !Application.isPlaying || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
            if(begin==0)begin=EditorApplication.timeSinceStartup;
            try
            {
                if(EditorApplication.timeSinceStartup-begin>120)throw new TimeoutException("cold load");
                var o=UnityEngine.Object.FindFirstObjectByType<WorkshopTenonObjective>();
                if(!o || !o.player || !o.player.CanAct || ++frames<8)return;
                var p=o.player; bool done=which=="seated";
                var cp=p.CheckpointAt(p.StageIndex);
                if(cp.nodeId!="CH02_HUB" || p.StageIndex!=(done?2:1) || o.IsSeated!=done ||
                    o.IsRunning==done || o.consoleReady.activeSelf!=done || o.consoleFault.activeSelf==done ||
                    o.courtyard.HasStarted || o.roof.HasStarted || o.tenon.IsHeld ||
                    o.courtyard.enabled==done || o.roof.enabled==done ||
                    Vector3.Distance(p.locomotion.PlayerFloorPosition,cp.floorPosition)>.10f ||
                    Vector3.Distance(o.tenon.transform.position,(done?o.socket.bottom:o.holder).position)>.01f)
                    throw new Exception($"Invalid restore stage={p.StageIndex}, seated={o.IsSeated}, rooms {o.roof.enabled}/{o.courtyard.enabled}, floor={p.locomotion.PlayerFloorPosition}, checkpoint={cp.floorPosition}");
                if(o.GetComponentsInChildren<NarutoVR.SealCombat.Items.GameItemBase>(true).Length!=1)throw new Exception("Duplicate master tenon");
                File.WriteAllText(Path.GetFullPath("../cold-"+which+".txt"),$"PASS separate Unity process opened {p.gameObject.scene.path}; load stage {p.StageIndex}, source checkpoint {cp.nodeId} at {cp.floorPosition:F3}, actual feet {p.locomotion.PlayerFloorPosition:F3}; seated={o.IsSeated}; roof={o.roof.enabled}, yard={o.courtyard.enabled}; item {o.tenon.transform.position:F3}.");
                SessionState.EraseString(Key);EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText(Path.GetFullPath("../cold-"+which+".txt"),"FAIL "+e);SessionState.EraseString(Key);EditorApplication.Exit(1);}
        }
    }
}
