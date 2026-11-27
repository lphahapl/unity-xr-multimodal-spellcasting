using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static partial class ChapterSupplyChecks
    {
        const string Key = "NarutoVR.Ch02Supplies1126";
        const string Report = "Logs/ChapterSupplyChecks.txt";
        const string CaseFile = "Logs/ChapterSupplyPrivateSave.txt";
        static readonly Stack<IEnumerator> queue = new();
        static int frame;
        static double deadline;
        static partial void ResolveLab(string name, ref IEnumerator test);
        static ChapterSupplyChecks() => EditorApplication.playModeStateChanged += state => {
            if (string.IsNullOrEmpty(SessionState.GetString(Key,""))) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var mode=SessionState.GetString(Key,"");
                IEnumerator test = mode == "fresh" || mode == "cold" ? Ration(mode=="cold") : null;
                ResolveLab(mode, ref test);
                queue.Clear(); queue.Push(test ?? throw new InvalidOperationException("Unknown test "+mode));
                deadline=EditorApplication.timeSinceStartup+155;
                EditorApplication.update+=Tick;
            }
            if (state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(Key);
                EditorApplication.Exit(SessionState.GetInt(Key+"Code",1));
            }
        };

        public static void Fresh() => Start("fresh");
        public static void Cold() => Start("cold");
        
        static void Start(string mode, string isolatedLab = null)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            Directory.CreateDirectory("Logs");
            if (isolatedLab == null) ChapterRationInstaller.Install();
            var scene=EditorSceneManager.OpenScene(isolatedLab ?? "Assets/Scene/Chapter02_SuspendedPath.unity");
            var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            if (mode=="fresh") File.WriteAllText(CaseFile,"ration-qa-"+Guid.NewGuid().ToString("N")+".json");
            player.saveFileName=mode=="ink" ? "ink-qa-"+Guid.NewGuid().ToString("N")+".json" : File.ReadAllText(CaseFile);
            player.autoLoad=mode=="cold";
            File.AppendAllText(Report,"\n"+DateTimeOffset.Now.ToString("o")+" "+mode+" "+Application.unityVersion+"\n");
            SessionState.SetString(Key,mode);SessionState.SetInt(Key+"Code",1);
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || frame==Time.frameCount) return;
            frame=Time.frameCount;
            try
            {
                if (EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Timed out");
                while(queue.Count>0)
                {
                    if (!queue.Peek().MoveNext()) { queue.Pop();continue; }
                    if (queue.Peek().Current is IEnumerator child) {queue.Push(child);continue;}
                    return;
                }
                File.AppendAllText(Report,"PASS END\n");SessionState.SetInt(Key+"Code",0);
            }
            catch(Exception e) { File.AppendAllText(Report,"FAIL "+e+"\n"); }
            EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();
        }
        static IEnumerator Wait(Func<bool> ready,string label)
        {
            double until=EditorApplication.timeSinceStartup+25;
            while(!ready())
            {
                if(EditorApplication.timeSinceStartup>until) throw new TimeoutException(label);
                yield return null;
            }
        }
        static void Check(bool condition,string label)
        {
            if(!condition) throw new InvalidOperationException(label);
            File.AppendAllText(Report,"PASS "+label+"\n");
        }
        static IEnumerator Ration(bool cold)
        {
            var supply=Object.FindFirstObjectByType<ChapterRationSupply>();var grab=supply.interaction;
            var player=grab.player;var belt=grab.sharedBelt;var home=supply.centralPocket;var lesson=player.GetComponent<Chapter02BridgeEquipment>();
            yield return Wait(()=>player.CanAct && belt.Initialised,"real player and belt: "+player.Status);
            Check(Path.GetFileName(player.SavePath)==File.ReadAllText(CaseFile),"uses only private QA save");
            var hands=Object.FindObjectsByType<Oculus.Interaction.HandVisual>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Check(new[]{"UnityXRHandVisualLeft","UnityXRHandVisualRight"}.All(n=>hands.Any(h=>h.name.Contains(n) ||
                (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(h.gameObject) ?? "").Contains(n))),"chapter retains both SDK hand visuals");
            Check(belt.slots.Length==5 && home.opening.name=="SoftPouch" && grab.mouth.transform.parent==player.locomotion.head,"one authored five-pocket belt, physical central pouch and head mouth");
            if(cold)
            {
                yield return Wait(()=>supply.HasPouch && grab.pouch.Dock==home && !home.IsSnapping,"fresh process restored saved pocket");
                Check(grab.pouch.InPouch==2 && grab.pouch.ConsumedMask!=0 && Mathf.Approximately(player.Player.Health,50),"cold disk read restores two portions, health, central dock");
                yield break;
            }
            Check(!supply.HasPouch && !home.SnappedObject,"new game does not award a pouch");
            var initial=new RpgCheckpointData();supply.CaptureEquipment(initial);
            var legacy=JsonUtility.FromJson<RpgCheckpointData>("{\"health\":52,\"mana\":11,\"cooldowns\":[]}");
            supply.RestoreEquipment(legacy);
            Check(initial.ration==null && !supply.HasPouch && (legacy.ration==null || !legacy.ration.owned),"legacy JSON with no ration field and absent record grant nothing");
            Check(supply.TryGrant() && !supply.TryGrant(),"configured award once only");
            yield return Wait(()=>!home.IsSnapping,"central dock");
            var bag=grab.pouch;
            Check(bag.InPouch==4 && bag.Dock==home,"approved art folded and seated with four pieces");
            grab.enabled=false; // The following samples replace only live hand observations in this replay.
            player.Player.Respawn(20f,10f,player.GameTime);
            var mouth=grab.mouth.transform;
            var rot=mouth.rotation;
            var far=mouth.position+mouth.right*.30f;
            var holdPos=mouth.position-mouth.right*.18f-mouth.up*.15f;
            TravelRationInteraction.PinchSample S(Vector3 pos,bool down)=>new(pos,rot,down ? .012f : .065f);
            void Step(Vector3 left,bool l,Vector3 right,bool rr,float dt=.02f)=>grab.Step(S(left,l),S(right,rr),dt);
            void TakeBag() {Step(bag.GripPosition,false,far,false);Step(bag.GripPosition,true,far,false);Step(holdPos,true,far,false);bag.AdvanceOpening(1f);}
            void PickPiece(){Step(holdPos,true,far,false);Step(holdPos,true,bag.NextBiscuitPosition,true);}
            IEnumerator EatOne(int expected)
            {
                PickPiece();Check(grab.HasServing,"physical biscuit picked; count before eat "+expected);
                var bite=mouth.position-mouth.forward*.022f;
                for(int i=0;i<6;i++) Step(holdPos,true,bite,true,.08f);
                yield return Wait(()=>!grab.mouth.GetComponent<NeuGameMRTools.Runtime.Components.SnapZone>().IsSnapping,"finish mouth travel");
                Step(holdPos,true,bite,true);Step(holdPos,true,bite,true);
                Check(!grab.HasServing && bag.InPouch==expected && Mathf.Approximately(player.Player.Health,20+(4-expected)*15),"exactly one 15 HP effect for piece; remaining "+expected);
                for(int i=0;i<8;i++) Step(holdPos,true,bite,true,.1f);
                Check(bag.InPouch==expected && Mathf.Approximately(player.Player.Health,20+(4-expected)*15),"lingering pinch cannot repeat piece "+expected);
            }
            Step(mouth.position,false,mouth.position,false);Step(mouth.position,true,mouth.position,true);
            Check(grab.BagHand<0 && bag.InPouch==4 && player.Player.Health==20,"empty hands near mouth cannot eat");
            TakeBag();
            Check(grab.BagHand==0 && belt.OwnsHeldItem(bag) && player.Input.InteractionOwnsHand,"left-held bag uses chapter belt's exclusive claim");
            PickPiece();
            Step(holdPos,true,mouth.position-mouth.forward*.022f,true,.08f);
            grab.Step(S(holdPos,true),default,.45f);
            Step(holdPos,true,far,false);
            Check(grab.HasServing && player.Player.Health==20,"tracking loss and an open reacquired hand do not consume or simulate a drop");
            Step(holdPos,true,far,true);Step(holdPos,true,far,false);
            Check(!grab.HasServing && bag.InPouch==4 && player.Player.Health==20,"deliberate release after reacquiring pinch returns the unconsumed piece");
            PickPiece();
            var bitePoint=mouth.position-mouth.forward*.022f;
            for(int i=0;i<6;i++) Step(holdPos,true,bitePoint,true,.08f);
            Check(grab.mouth.GetComponent<NeuGameMRTools.Runtime.Components.SnapZone>().SnappedObject,"last bite has entered mouth snap");
            grab.Step(S(holdPos,true),default,.1f);
            Check(!grab.HasServing && bag.InPouch==4 && player.Player.Health==20,"loss during final attraction cancels without awarding or losing food");
            yield return EatOne(3);
            yield return EatOne(2);
            int savedMask=bag.ConsumedMask;
            Step(home.opening.position,false,far,false);
            yield return Wait(()=>bag.Dock==home && !home.IsSnapping,"bag returned");
            Check(!belt.Held && !player.Input.InteractionOwnsHand && bag.InPouch==2,"normal release docks bag and releases input");
            Check(player.SaveCheckpoint() && File.Exists(player.SavePath),"official save writes private file");
            var disk=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(player.SavePath));
            Check(disk.progress.checkpoint.ration.owned && disk.progress.checkpoint.ration.spentMask==savedMask && disk.progress.checkpoint.ration.pocket==Array.IndexOf(belt.slots,home)+1,"disk stores exact piece mask and actual dock");
            TakeBag();yield return EatOne(1);yield return EatOne(0);
            PickPiece();Check(!grab.HasServing && bag.InPouch==0,"empty pouch cannot dispense a fifth piece");
            bool restored=player.RestoreCheckpoint();
            Check(restored,"official restore accepted while holding empty bag: "+player.Status+" / "+player.locomotion.Status);
            yield return Wait(()=>supply.HasPouch && grab.pouch.Dock==home && !home.IsSnapping,"restore recreates one folded pouch");
            bag=grab.pouch;
            Check(bag.ConsumedMask==savedMask && bag.InPouch==2 && player.Player.Health==50 && !belt.Held,"restore atomically rolls inventory and vitals back");
            // Reverse hands on the same physical pouch and return the loose biscuit without eating.
            Step(far,false,bag.GripPosition,false);Step(far,false,bag.GripPosition,true);
            Step(far,false,holdPos,true);bag.AdvanceOpening(1);
            Step(bag.NextBiscuitPosition,true,holdPos,true);
            Check(grab.BagHand==1 && grab.HasServing && belt.OwnsHeldItem(bag),"right hand holds pouch, left hand can select a piece");
            Step(far,false,holdPos,true);Step(far,false,home.opening.position,false);
            yield return Wait(()=>bag.Dock==home && !home.IsSnapping,"right hand pouch returns to real central dock");
            Check(bag.ConsumedMask==savedMask && !belt.Held,"unused piece and both input claims restored");
            // Use the existing pen/holster and its belt acquisition contract without advancing the chapter or overwriting the QA save.
            lesson.equipment.SetWritingAvailable(true);
            var pen=lesson.equipment.pen;
            yield return Wait(()=>pen.Dock && !pen.Dock.IsSnapping,"pen in dedicated holster");
            Check(belt.TryTake(pen,true),"existing pen acquires belt");TakeBag();
            Check(!bag.IsHeld && belt.OwnsHeldItem(pen),"bag cannot steal pen claim");
            belt.ReleaseAuxiliary(pen,true);yield return Wait(()=>!pen.home.IsSnapping,"release real pen");
            TakeBag();Check(!belt.TryTake(pen,true) && belt.OwnsHeldItem(bag),"pen cannot steal occupied hands");
            Step(home.opening.position,false,far,false);
        }

    }
}
