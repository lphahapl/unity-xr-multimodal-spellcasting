using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class ChapterWristAcceptance
    {
        const string Key = "NarutoVR.QA.ChapterWrist";
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string Report = "Logs/ChapterWristAcceptance.txt";
        static int phase;
        static double expires, time;
        static LinearRpgController p;
        static WristMenuController m;
        static Chapter02BridgeEquipment lesson;
        static PlayerHandObservation hand;
        static ChapterWristAcceptance() => EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                expires = EditorApplication.timeSinceStartup + 120;
                phase = 0;
                EditorApplication.update += Step;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(Key);
                EditorApplication.Exit(SessionState.GetInt(Key+"Exit", 1));
            }
        };

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            p = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            p.autoLoad = false;
            p.saveFileName = "wrist-qa-"+Guid.NewGuid().ToString("N")+".json";
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key+"Exit",1);
            File.WriteAllText(Report, DateTimeOffset.Now.ToString("o")+" Unity "+Application.unityVersion+"\n");
            EditorApplication.EnterPlaymode();
        }

        static void Need(bool ok,string label)
        {
            if (!ok) throw new InvalidOperationException(label+" | "+(m ? m.DebugDetails : "no wrist menu"));
            File.AppendAllText(Report,"PASS "+label+"\n");
        }

        static void Step()
        {
            try
            {
                if (EditorApplication.timeSinceStartup>expires) throw new TimeoutException("phase "+phase);
                p=Object.FindFirstObjectByType<LinearRpgController>();
                if (!p || !p.CanAct) return;
                lesson=Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
                m=p.wristMenu;
                if (phase == 0)
                {
                    Need(m && m.view && m.teachingCatalog && m.wornDevice && m.wornDevice.handInput==m &&
                        m.wornDevice!=lesson.button && lesson.button.playerHandInput==p,"first-chapter menu and distinct stone-table button are correctly wired");
                    var hud=p.locomotion.head.GetComponentsInChildren<Canvas>(true).Single(c=>c.name=="技能状态中文面板");
                    Need(!hud.gameObject.activeInHierarchy && !m.RightHandStatusVisible && !m.PanelOpen && !m.State.LauncherOpen,"fresh spawn: old eye HUD and all three wrist panels hidden");
                    Need(m.RightHandStatusEnabled,"default glance enabled; test uses current preference without writing it");
                    RunMenu();
                    lesson.equipment.SetWritingAvailable(true);
                    phase=1;
                    return;
                }
                var pen=lesson.equipment.pen;
                if (!pen.Dock || pen.Dock.IsSnapping) return;
                InitPose();
                for(int n=0;n<12;n++) Tick();
                // Real pickup using the actual pen, belt and the runtime drawing session.
                hand.gripPosition=hand.rightTip=hand.rightWrist=pen.GripPosition;
                hand.gripPoseValid=true;
                hand.gripRotation=Quaternion.identity;
                hand.pinchDistance=.07f;
                ObservePen();
                hand.pinchDistance=.012f;
                ObservePen();
                Need(lesson.glyph.IsHeld && lesson.glyph.BlocksCombat && p.Input.InteractionOwnsHand,"physical belt pen owns the chapter drawing input");
                Press();
                Need(!m.State.LauncherOpen && !m.OwnsInput && !m.RightHandStatusVisible && lesson.glyph.IsHeld,"wrist press and glance cannot steal the drawing hand");
                Need(!p.Input.CanCast(out _) && p.skills.GestureInputBlocked(),"drawing hand blocks combat gesture matching");
                File.AppendAllText(Report,"PASS END\n");
                SessionState.SetInt(Key+"Exit",0);
            }
            catch(Exception ex) { File.AppendAllText(Report,"FAIL "+ex+"\n"); }
            EditorApplication.update-=Step;
            EditorApplication.ExitPlaymode();
        }

        static void InitPose()
        {
            time=Time.realtimeSinceStartupAsDouble;
            var head=p.locomotion.head.position;
            hand=new PlayerHandObservation {headPosition=head, headForward=Vector3.forward, leftValid=true,
                rightValid=true, rightPalmValid=true,rightPalmNormal=Vector3.back, leftRotation=Quaternion.identity,
                mountRotation=Quaternion.identity,mountValid=true, rightOpen=true, pinchValid=true,pinchDistance=.07f,
                leftWrist=head+new Vector3(-.23f,-.18f,.33f),rightWrist=head+new Vector3(.12f,-.09f,.39f),
                rightTip=head+new Vector3(.13f,-.06f,.40f),source="QA",raySource="QA",pinchSource="QA"};
        }
        static void Stamp(){time+=.026; hand.rightObserved=hand.leftObserved=time;}
        static void ObservePen(){Stamp();lesson.glyph.Observe(hand,time);}
        static void Tick()
        {
            Stamp();
            var nav=new NavigationHandInput{leftValid=hand.leftValid,rightValid=hand.rightValid,leftWrist=hand.leftWrist,rightWrist=hand.rightWrist};
            p.UpdateWristUi(nav,hand,p.Input.InteractionOwnsHand || p.skills.effects.HasOccupiedHand,time);
        }
        static void Press()
        {
            hand.rayValid=false;
            var btn=m.wornDevice;
            var axis=btn.cap.forward;
            hand.rightTip=btn.RestSurface-axis*.054f;Tick();
            hand.rightTip=btn.RestSurface-axis*.012f;Tick();
            hand.rightTip=btn.RestSurface+axis*btn.travelMetres;Tick();
        }
        static RectTransform Target(string id)=>m.view.Targets.Single(t=>t.id==id && t.rect.gameObject.activeInHierarchy).rect;
        static void RayClick(string id)
        {
            var rect=Target(id);
            int clicks=m.ClickCount;
            hand.rightPointing=true; hand.rayValid=hand.rayAnchorValid=true;
            hand.rightTip=hand.headPosition+Vector3.right*.45f;
            hand.rayDirection=rect.forward;
            hand.rayOrigin=rect.TransformPoint(rect.rect.center)-rect.forward*.11f;
            hand.pinchDistance=.07f; for(int i=0;i<5;i++) Tick();
            hand.pinchDistance=.012f; Tick();
            Need(m.ClickCount==clicks+1,"ray and pinch activated "+id+" once");
            hand.pinchDistance=.07f; hand.rayValid=false; Tick();
        }
        static void RunMenu()
        {
            InitPose();
            for(int i=0;i<12;i++)Tick();
            Need(m.RightHandStatusVisible && !m.OwnsInput && p.Input.CanCast(out _),"raising right palm shows status without consuming combat input");
            var wrist=hand.rightWrist;
            hand.rightWrist=hand.headPosition+Vector3.down*.65f;Tick();
            Need(!m.RightHandStatusVisible,"lowering right wrist hides readout");
            hand.rightWrist=wrist;
            hand.leftWrist=hand.rightWrist+Vector3.right*.025f;
            for(int i=0;i<12;i++) Tick();
            Need(!m.RightHandStatusVisible && !m.OwnsInput,"two-hand seal pose does not open the glance or claim UI");
            hand.leftWrist=hand.headPosition+new Vector3(-.23f,-.18f,.33f);Tick();
            Press();
            Need(m.State.LauncherOpen && p.UiInputOwned && !p.Input.CanInteract(out _) && p.skills.GestureInputBlocked(),"physical wrist button opens launcher and suspends combat and pickups");
            Press();
            Need(!m.State.LauncherOpen,"withdraw and press again closes launcher");
            for(int i=0;i<9;i++) Tick();
            Need(!p.UiInputOwned && p.Input.CanCast(out _),"closing releases combat ownership");
            Press();
            RayClick("status");
            Need(m.PanelOpen && m.PanelKind==WristPanelKind.SkillStatus,"manual status panel opened");
            hand.leftWrist.y=hand.headPosition.y-.78f;
            for(int i=0;i<12;i++) Tick();
            Need(m.PanelOpen && !m.State.LauncherOpen && !p.UiInputOwned,"manual status stays open with wrist lowered; idle card releases input");
            RayClick("follow");
            var rig=p.locomotion.rigRoot;
            var rect=Target("close");
            var before=rect.position;
            var delta=Vector3.right*.08f;
            rig.position+=delta;hand.headPosition+=delta;Tick();
            Need(m.FollowPlayer && Vector3.Distance(before+delta,rect.position)<.004f,"follow moves visible panel with rig");
            RayClick("follow");
            before=rect.position;rig.position-=delta;hand.headPosition-=delta;Tick();
            Need(!m.FollowPlayer && Vector3.Distance(before,rect.position)<.004f,"fixed panel stays in world when rig moves");
            RayClick("close");
            Need(!m.PanelOpen,"explicit close hides status card");
        }
    }
}
