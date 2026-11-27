using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class ChapterWristGoldCapture
    {
        const string Key = "NarutoVR.QA.ChapterWristGold";
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string Report = "Logs/ChapterWristGoldCapture.txt";
        
        static double expires, time;
        static LinearRpgController p;
        static WristMenuController m;

        static PlayerHandObservation hand;
        static ChapterWristGoldCapture() => EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                expires = EditorApplication.timeSinceStartup + 120;

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
                if (EditorApplication.timeSinceStartup>expires) throw new TimeoutException();
                p=Object.FindFirstObjectByType<LinearRpgController>();
                if (!p || !p.CanAct) return;
                m=p.wristMenu;
                Inspect();
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
static readonly string[] Keys={"breath.burst","breath.stream","fireball","shield","thunder.sword"};
        static Camera cam;
        static Vector3 eye;
        static Quaternion gaze;
        static string Dir => Environment.GetEnvironmentVariable("CH02_WRIST_GOLD_OUTPUT");
        static void Note(string s)=>File.AppendAllText(Report,s+"\n");
        static Transform Panel(string name)=>m.view.OwnedRoot.transform.Find(name);
        static string[] IconRows(Transform pane)
        {
            var images=pane.GetComponentsInChildren<Image>();
            var icons=images.Where(i=>i.name=="图标" && i.transform.parent.name.StartsWith("术法 ")).ToArray();
            foreach(var i in icons)
            {
                var id=i.transform.parent.name.Substring(3);
                Need(i.isActiveAndEnabled && i.sprite && i.sprite==Resources.Load<Sprite>("SkillStatus/"+id) &&
                    i.sprite.name==id,"rendered native skill icon "+id+" / "+i.transform.parent.Find("术名").GetComponent<Text>().text);
            }
            return icons.Select(i=>i.sprite.name).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        }
        static void Capture(string file,Transform pane)
        {
            Canvas.ForceUpdateCanvases();
            var corners=new Vector3[4]; ((RectTransform)pane).GetWorldCorners(corners);
            var uv=corners.Select(pt=>cam.WorldToViewportPoint(pt)).ToArray();
            Note("FRAME "+file+" head "+cam.name+" position="+eye.ToString("F5")+" forward="+cam.transform.forward.ToString("F5")+
                " viewport="+string.Join(",",uv.Select(pt=>pt.ToString("F3"))));
            Need(cam==p.locomotion.head.GetComponent<Camera>() && cam.transform.position==eye && cam.transform.rotation==gaze &&
                  pane.gameObject.activeInHierarchy && uv.All(pt=>pt.z>0 && pt.x>0 && pt.x<1 && pt.y>0 && pt.y<1),"same player eye and entire active UI in frame "+file);
            int width=1920,height=Mathf.RoundToInt(width/cam.aspect);
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var old=RenderTexture.active;var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                if (!RenderPipeline.SupportsRenderRequest(cam,request)) throw new InvalidOperationException("Camera capture unsupported");
                RenderPipeline.SubmitRenderRequest(cam,request);RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(Dir,file+".png"),tex.EncodeToPNG());
            }
            finally { RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex); }
        }
        static void Inspect()
        {
            cam=p.locomotion.head.GetComponent<Camera>();eye=cam.transform.position;gaze=cam.transform.rotation;
            InitPose();
            hand.headForward=cam.transform.forward;
            hand.rightPalmNormal=-hand.headForward;
            hand.mountRotation=hand.leftRotation=gaze;
            hand.leftWrist=eye+gaze*new Vector3(-.27f,-.17f,.45f);
            hand.rightWrist=eye+gaze*new Vector3(.06f,-.10f,.38f);
            hand.rightTip=hand.rightWrist;
            for(int i=0;i<16;i++) Tick();
            Need(m.RightHandStatusVisible && !m.OwnsInput,"right palm opened live glance without taking command input");
            var glance=Panel("右手术法快览");
            var initial=IconRows(glance);
            Note("FRESH_KEYS "+string.Join(",",initial));
            Capture("01-glance-fresh",glance);
            var native=Resources.LoadAll<Sprite>(KaogongUISprites.AtlasResourcePath).OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            Need(native.Length==KaogongUISprites.Manifest.Length && native.All(s=>AssetDatabase.IsSubAsset(s) &&
                AssetDatabase.GetAssetPath(s).EndsWith("/KaogongUIAtlas.png") && KaogongUISprites.Get(s.name)==s),"native sprite subassets used by loader "+string.Join(",",native.Select(s=>s.name)));
            if (!initial.SequenceEqual(Keys))
            {
                var ids=p.skills.ConfiguredSkills.Where(s=>s && (Keys.Contains(s.skillId) || s.skillId=="buff.zi" && s.cooldownGroup=="shield")).Select(s=>s.skillId).ToArray();
                Note("TEMP_STAGE_GRANTS "+string.Join(",",ids));
                p.Model.SetStageGrants(ids);p.skills.RefreshAvailableSkills();
                for(int i=0;i<15;i++) Tick();
            }
            Need(IconRows(glance).SequenceEqual(Keys),"all five skill sprites on active glance");
            Capture("02-glance-five",glance);
            // Worn switch is driven by the real Zhunwang cap, not a call to ShowLauncher.
            hand.leftWrist=eye+gaze*new Vector3(-.065f,-.11f,.53f);Tick();Press();
            var launcher=Panel("术式启动器");
            Need(m.State.LauncherOpen && !m.RightHandStatusVisible,"pressing physical switch opens the real launcher");
            var title=launcher.Find("启动器标题").GetComponent<Text>();
            Need(m.view.useKaogongTheme && title.text=="准望匣" && title.color==KaogongTheme.JadeWarm,"theme switch is on and heading draws in warm jade "+ColorUtility.ToHtmlStringRGBA(title.color));
            var controls=launcher.GetComponentsInChildren<Image>().Where(i=>i.isActiveAndEnabled && i.sprite && native.Contains(i.sprite)).ToArray();
            Note("LAUNCHER_IMAGES "+string.Join(",",controls.Select(i=>i.sprite.name).Distinct().OrderBy(k=>k)));
            Need(controls.Any(i=>i.sprite.name=="Frame_Bronze_Main" && i.type==Image.Type.Sliced) &&
                controls.Any(i=>i.sprite.name=="Button_Normal" && i.type==Image.Type.Sliced) &&
                new[]{"Icon_Seals","Icon_GlyphBrush","Icon_VoiceBell","Icon_StatusWheel"}.All(k=>controls.Any(i=>i.sprite.name==k)),
                "bronze frame, sliced buttons and four launcher emblems reference imported sprites");
            Capture("03-case",launcher);
            RayClick("status");
            hand.leftWrist.y=eye.y-.77f;
            for(int i=0;i<14;i++)Tick();
            var panel=Panel("独立术式面板");
            Need(m.PanelOpen && !m.State.LauncherOpen && !m.RightHandStatusVisible && IconRows(panel).SequenceEqual(Keys),"manual status page renders five skill sprites independently of launcher");
            Capture("04-status-five",panel);
        }

    }
}
