using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Editor;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using Object = UnityEngine.Object;

// Install in the game's Editor assembly of an isolated project. Requires a graphics device.
public static class PassageGlyphPlayCapture
{
    const string Key = "NarutoVR.PassageMovie";
    static IEnumerator replay;
    static int last = -1;
    static double deadline;
    static string Folder => SessionState.GetString(Key, "");
    [InitializeOnLoadMethod]
    static void InstallTick() => EditorApplication.update += Tick;
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scene/PassageGlyphLab.unity");
        string dir = Path.GetFullPath("Logs/PassageFilm-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(dir);
        Debug.Log("PASSAGE_FILM_DIR " + dir);
        SessionState.SetString(Key, dir);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (Folder == "" || !Application.isPlaying || Time.frameCount == last) return;
        last = Time.frameCount;
        try
        {
            if (replay == null) { deadline=EditorApplication.timeSinceStartup+480; replay=Play(); }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("capture");
            if (replay.MoveNext()) return;
            Debug.Log("PASSAGE_FILM_PASS");
            SessionState.EraseString(Key); EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e); File.WriteAllText(Path.Combine(Folder,"failed.txt"),e.ToString());
            (replay as IDisposable)?.Dispose(); SessionState.EraseString(Key); EditorApplication.Exit(1);
        }
    }
    static IEnumerator Play()
    {
        var p=Object.FindFirstObjectByType<LinearRpgController>();
        while (!p || !p.CanAct) { yield return null; p=Object.FindFirstObjectByType<LinearRpgController>(); }
        Time.captureFramerate=30;
        using var film=new Film(p);
        foreach (var step in film.Approach()) yield return step;
        foreach (var step in film.DrawAndSelect()) yield return step;
        while(film.Road.Progress < 1) { yield return null; film.Growth(); }
        foreach (var step in film.Cross()) yield return step;
    }
    static void Need(bool test,string msg) { if(!test) throw new InvalidOperationException(msg); }

    sealed class Film : IDisposable
    {
        readonly LinearRpgController player;
        readonly GlyphPenController pen;
        readonly PassageGlyphAnchor a,b;
        readonly Vector3 eye,baseFloor,nibOffset;
        readonly GlyphTemplate template;
        readonly Camera cam;
        readonly RenderTexture rt, small;
        readonly Texture2D pix, thumb;
        readonly StreamWriter data;
        readonly Transform handRoot,wrist,tip,joint;
        readonly AnimationClip point,fist,relaxed;
        readonly Transform[][] fingers;
        readonly SealFeatureExtractor extractor=new();
        readonly NarutoVR.SealLab.SealLab lab;
        readonly Material handMaterial;
        readonly PlayerLoopSystem originalLoop;
        int frame, completions;
        bool detectedPoint, detectedFist;
        double clock=Time.realtimeSinceStartupAsDouble;
        public PassageGlyphRoad Road {get;private set;}
        Vector3 handPosition => eye+new Vector3(.19f,-.20f,.27f);
        const string SDK="Packages/com.meta.xr.sdk.interaction/Runtime/";
        internal Film(LinearRpgController p)
        {
            player=p;pen=p.GlyphPen;lab=p.handTracking?p.handTracking:p.skills.lab;
            baseFloor=p.locomotion.PlayerFloorPosition;eye=baseFloor+Vector3.up*1.62f;
            p.locomotion.head.position=eye;
            template=pen.activeTemplates.Single(t=>t.effect is PassageGlyphEffect);
            template.onCompleted.AddListener(Completed);
            var marks=Object.FindObjectsByType<PassageGlyphAnchor>(FindObjectsSortMode.None);
            a=marks.Single(x=>x.name=="A");b=marks.Single(x=>x.name=="B");
            nibOffset=pen.penRoot.InverseTransformPoint(pen.nib.position);
            cam=new GameObject("通路符摄像机").AddComponent<Camera>();
            cam.CopyFrom(Camera.main);cam.enabled=false;cam.stereoTargetEye=StereoTargetEyeMask.None;
            cam.aspect=16f/9f;cam.nearClipPlane=.009f;cam.farClipPlane=32;
            cam.clearFlags=RenderSettings.skybox?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;
            cam.backgroundColor=new Color(.17f,.23f,.25f);cam.rect=new Rect(0,0,1,1);cam.ResetProjectionMatrix();
            rt=new RenderTexture(1280,720,24);small=new RenderTexture(384,216,24);
            rt.Create();small.Create();pix=new Texture2D(1280,720,TextureFormat.RGB24,false);
            thumb=new Texture2D(384,216,TextureFormat.RGB24,false);
            handRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SDK+"Meshes/OpenXRRightHand.fbx")).transform;
            foreach(var animator in handRoot.GetComponentsInChildren<Animator>()) animator.enabled=false;
            var bones=handRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("XRHand_")).ToDictionary(t=>t.name.Substring(7));
            wrist=bones["Wrist"];tip=bones["IndexTip"];joint=bones["IndexIntermediate"];
            var names=new[]{"Thumb","Index","Middle","Ring","Little"};
            fingers=names.Select(n=>(n=="Thumb"? new[]{wrist,bones[n+"Metacarpal"],bones[n+"Proximal"],bones[n+"Distal"],bones[n+"Tip"]} :
                new[]{bones[n+"Metacarpal"],bones[n+"Proximal"],bones[n+"Intermediate"],bones[n+"Distal"],bones[n+"Tip"]})).ToArray();
            handRoot.localScale*=.18f/Vector3.Distance(wrist.position,tip.position);
            handMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            handMaterial.SetColor("_BaseColor",new Color(.81f,.65f,.50f));
            var mesh=handRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            mesh.sharedMaterial=handMaterial;mesh.updateWhenOffscreen=true;
            point=AssetDatabase.LoadAssetAtPath<AnimationClip>(SDK+"Animations/Hands/OpenXR_IndexPoint_Right.anim");
            fist=AssetDatabase.LoadAssetAtPath<AnimationClip>(SDK+"Animations/Hands/OpenXR_HandFist_Right.anim");
            relaxed=AssetDatabase.LoadAssetAtPath<AnimationClip>(SDK+"Animations/Hands/OpenXR_HandSlide_Right.anim");
            Need(point&&fist&&relaxed,"官方姿态资源缺失");handRoot.gameObject.SetActive(false);
            // This desktop replay supplies the same player input calls once per rendered frame.
            // Suppress the live LateUpdate poll so a missing headset cannot overwrite a replay sample.
            originalLoop = PlayerLoop.GetCurrentPlayerLoop();
            var replayLoop = originalLoop;
            replayLoop.subSystemList = (PlayerLoopSystem[])originalLoop.subSystemList.Clone();
            for (int i = 0; i < replayLoop.subSystemList.Length; i++)
            {
                ref var phase = ref replayLoop.subSystemList[i];
                if (phase.type == typeof(UnityEngine.PlayerLoop.PreLateUpdate))
                    phase.subSystemList = phase.subSystemList.Where(s => s.type != typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)).ToArray();
            }
            PlayerLoop.SetPlayerLoop(replayLoop);
            data=new StreamWriter(Path.Combine(Folder,"frames.csv"));
            data.WriteLine("frame,unityFrame,stage,completeCount,rightPointing,rightFist,preview,progress,frontX,frontY,frontZ,collider,playerX,playerY,playerZ,firstSelected,rayVisible,contactVisible,rayMetres,aScreenX,aScreenY,bScreenX,bScreenY,approachHint");
        }
        void Completed(GlyphBoard _) => completions++;
        void Grip(Vector3 g,Quaternion rot,bool pinch=false)
        {
            clock+=1d/30;
            pen.Observe(new PlayerHandObservation{headPosition=eye,headForward=Vector3.forward,rightValid=true,rightObserved=clock,
                gripPoseValid=true,gripPosition=g,gripRotation=rot,rightTip=g,rightWrist=g,
                pinchValid=true,systemPinch=true,systemPressed=pinch},clock);
            pen.Advance(clock);
        }
        void Ink(Vector3 p)
        {
            var board=pen.activeBoard.transform;
            var rot=board.rotation*Quaternion.Euler(13,28,0);
            Grip(board.TransformPoint(p)-rot*nibOffset,rot);
        }
        void JointInput(Vector3 aim,bool closed,bool resting=false)
        {
            clock+=1d/30;
            handRoot.gameObject.SetActive(true);
            (closed?fist:resting?relaxed:point).SampleAnimation(wrist.parent.gameObject,0);
            if(!closed)
            {
                var axis=(tip.position-joint.position).normalized;
                var side=fingers[1][1].position-fingers[4][1].position;
                var up=Vector3.Cross(axis,side).normalized;
                var target=(aim-handPosition).normalized;
                handRoot.rotation=Quaternion.LookRotation(target,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(axis,up))*handRoot.rotation;
            }
            handRoot.position+=handPosition-wrist.position;
            if(!closed)
            {
                var fix=Quaternion.FromToRotation(tip.position-joint.position,aim-tip.position);
                handRoot.rotation=fix*handRoot.rotation;handRoot.position+=handPosition-wrist.position;
            }
            var skeleton=RpgHandPoseChecks.Skeleton(clock);
            skeleton.source=lab.Frame?.source;
            skeleton.left.connected=skeleton.left.tracked=skeleton.left.fresh=false;
            var h=skeleton.right;
            void Joint(HandJointId id, Transform t)
            {
                var origin=lab.recordingOrigin;
                h.joints[(int)id]= new Pose(origin?origin.InverseTransformPoint(t.position):t.position,
                    origin?Quaternion.Inverse(origin.rotation)*t.rotation:t.rotation);
                h.wristPoses[(int)id]=new Pose(Quaternion.Inverse(wrist.rotation)*(t.position-wrist.position),Quaternion.Inverse(wrist.rotation)*t.rotation);
            }
            Joint(HandJointId.HandWristRoot,wrist);
            for(int finger=0;finger<5;finger++)
            {
                var ids=HandJointUtils.FingerToJointList[finger];
                for(int i=0;i<ids.Length;i++)Joint(ids[i],fingers[finger][5-ids.Length+i]);
            }
            lab.GetType().GetProperty("Frame").SetValue(lab,skeleton);
            extractor.Extract(skeleton,lab.Features);
            player.Input.SampleHands(clock);
            var hand=player.Input.LatestHand;
            detectedPoint=hand.rightPointing;detectedFist=hand.rightClosed;
            pen.Observe(hand,clock);pen.Advance(clock);
        }

        internal IEnumerable Approach()
        {
            var original=player.locomotion.head.rotation;
            var presenter=Object.FindFirstObjectByType<PassageApproachHint>();
            Need(presenter && !presenter.strokeReference.activeSelf,"取笔前试绘图谱应收起");
            player.locomotion.head.rotation=Quaternion.LookRotation(Vector3.back,Vector3.up);
            for(int i=0;i<16;i++) yield return null;
            Need(!GameObject.Find("近岸行路提示"),"背向渡口仍出现提示");
            player.locomotion.head.rotation=Quaternion.LookRotation(baseFloor+new Vector3(.05f,.2f,3.35f)-eye,Vector3.up);
            var effect=(PassageGlyphEffect)template.effect;
            float span=effect.maxSpan;
            try
            {
                effect.maxSpan=.1f;
                for(int i=0;i<16;i++) yield return null;
                Need(!GameObject.Find("近岸行路提示"),"没有合法对岸时仍提示搭路");
            }
            finally { effect.maxSpan=span; }
            for(int i=0;i<72;i++){yield return null;Snap("approach");}
            var hint=GameObject.Find("近岸行路提示");
            Need(hint && hint.GetComponent<CanvasGroup>().alpha>.9f && !pen.IsHeld,"玩家看向断口时缺少取笔前提示");
            for(int i=0;i<42;i++){yield return null;Snap("socket");}
            player.locomotion.head.rotation=original;
        }

        internal IEnumerable DrawAndSelect()
        {
            var g=pen.holster?pen.holster.position:eye+pen.beltOffset;
            Grip(g,Quaternion.identity);Grip(g,Quaternion.identity,true);
            Need(pen.IsHeld&&pen.activeBoard.IsOpen,"取笔未开板 "+pen.Status);
            yield return null;
            Need(GameObject.Find("通路符图谱") && !GameObject.Find("近岸行路提示"),"取笔后未从近岸提示切换到试绘图谱");
            var lift=Vector3.back*.055f;
            Ink(lift);
            foreach(var line in template.Preview())
            {
                Ink(line[0]*.115f);Snap("ink");yield return null;
                for(int j=1;j<line.Length;j++) for(int k=1;k<=13;k++)
                {Ink(Vector2.Lerp(line[j-1],line[j],k/13f)*.115f);Snap("ink");yield return null;}
                Ink(lift);Snap("ink");yield return null;
            }
            for(int i=0;i<35&&completions==0;i++){Ink(lift);Snap(completions>0?"recognized":"idle");yield return null;}
            Need(completions==1,"停笔未成符");
            for(int i=0;i<32;i++){JointInput(handPosition+new Vector3(.22f,.03f,.8f),false,true);Snap("recognized");yield return null;}
            for(int i=0;i<38;i++){JointInput(a.transform.position,false);Snap("first",true);yield return null;}
            Need(detectedPoint && GameObject.Find("已选岸标").GetComponent<LineRenderer>().enabled,
                "第一面未选；"+player.Input.HandPoseStatus+" "+pen.Status);
            for(int i=0;i<46;i++)
            {
                var target=Vector3.Lerp(a.transform.position,b.transform.position,Mathf.SmoothStep(0,1,i/12f));
                JointInput(target,false);
                Snap(GameObject.Find("通路符搭路预览")?"preview":"aim",true);yield return null;
            }
            Need(GameObject.Find("通路符搭路预览"),"第二面未预览 "+pen.Status);
            for(int i=0;i<9;i++)
            {
                JointInput(b.transform.position,true);
                Road=Object.FindFirstObjectByType<PassageGlyphRoad>();
                Snap("confirm",true);
                if(Road)break;
                yield return null;
            }
            Need(detectedFist && Road && Road.Progress==0 && !Road.GetComponent<Collider>().enabled,
                "握拳未确认通路 "+player.Input.HandPoseStatus+" "+pen.Status);
        }

        internal void Growth()
        {
            bool solid=Road.GetComponent<Collider>().enabled;
            Need(solid==(Road.Progress==1),"成路与碰撞不同步");
            Snap(solid?"joined":"forming",true);
        }

        internal IEnumerable Cross()
        {
            for(int i=0;i<22;i++){yield return null;Snap("joined");}
            handRoot.gameObject.SetActive(false);detectedFist=false;
            var movement=player.locomotion;movement.SetExplorationMode(ExplorationMovement.PointWalk);
            for(int i=0;i<330&&movement.PlayerFloorPosition.z-baseFloor.z<6.38f;i++)
            {
                yield return null;clock+=1d/30;
                var h=new NavigationHandInput{leftValid=true,leftObserved=clock,leftWrist=movement.head.position-Vector3.up*.12f,
                    leftIndex=movement.head.position+Vector3.forward*.20f,leftRayOrigin=movement.head.position,
                    leftRayDirection=Vector3.forward,leftRayValid=true,leftPointing=i>4,leftNavigationPointing=i>4};
                movement.ProcessInput(h,false,false,clock);Physics.SyncTransforms();
                Snap("walk");
            }
            var at=movement.PlayerFloorPosition;
            Need(Physics.Raycast(at+Vector3.up,Vector3.down,out var bank,2,~0,QueryTriggerInteraction.Ignore)&&
                 bank.collider.transform.IsChildOf(b.bank),"未走到乙岸 "+at+" "+movement.Status);
            for(int i=0;i<22;i++){yield return null;Snap("arrived");}
        }
        void CameraAt(Vector3 pos,Vector3 at,float fov)
        {
            cam.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(at-pos,Vector3.up));
            cam.fieldOfView=fov;
        }
        void RenderFrame(RenderTexture surface,Texture2D tex,string name)
        {
            cam.targetTexture=surface;cam.Render();
            var old=RenderTexture.active;
            try{
                RenderTexture.active=surface;tex.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0,false);tex.Apply(false,false);
                File.WriteAllBytes(Path.Combine(Folder,name),tex.EncodeToJPG(91));
            }finally{RenderTexture.active=old;}
        }
        void LookAtHand()
        {
            var hand=player.Input.LatestHand;
            var center=Vector3.Lerp(hand.rightWrist,hand.gripPosition,.66f);
            var rot=hand.gripRotation;
            CameraAt(center+rot*new Vector3(.055f,.18f,-.065f),center,48);
        }
        void Snap(string stage,bool inset=false)
        {
            Vector3 playerAt=player.locomotion.PlayerFloorPosition;
            if(stage=="approach")
                CameraAt(eye,eye+player.locomotion.head.forward,67);
            else if(stage=="socket")
                CameraAt(a.landing.position+new Vector3(-.87f,.57f,-1.02f),a.landing.position+new Vector3(-.1f,.22f,0),53);
            else if(stage=="ink"||stage=="idle")
            {
                var board=pen.activeBoard.transform;
                CameraAt(board.position-board.forward*.57f+board.right*.083f+board.up*.035f,board.position,51);
            }
            else if(stage=="walk"||stage=="arrived")
            {
                var pos=playerAt+new Vector3(0,1.60f,-.045f);
                CameraAt(pos,pos+new Vector3(0,-1.35f,2.02f),70);
            }
            else if(stage=="recognized" && handRoot.gameObject.activeSelf)
            {
                LookAtHand();
            }
            else if(stage=="first" || stage=="aim" || stage=="preview" || stage=="confirm")
            {
                CameraAt(baseFloor+new Vector3(2.10f,1.46f,-.20f),baseFloor+new Vector3(0,.76f,1.90f),58);
            }
            else
            {
                CameraAt(baseFloor+new Vector3(-2.35f,2.85f,.28f), baseFloor+new Vector3(.02f,.12f,3.54f),53);
            }
            Canvas.ForceUpdateCanvases();RenderFrame(rt,pix,frame.ToString("D5")+".jpg");
            var av=cam.WorldToViewportPoint(a.landing.position);
            var bv=cam.WorldToViewportPoint(b.landing.position);
            var beam=GameObject.Find("食指指向")?.GetComponent<LineRenderer>();
            var mark=GameObject.Find("射线命中符纹")?.GetComponent<MeshRenderer>();
            bool ray=beam && beam.enabled, contact=mark && mark.enabled;
            float distance=ray?Vector3.Distance(beam.GetPosition(0),beam.GetPosition(1)):0;
            if(ray) Need(Vector3.Distance(beam.GetPosition(0),player.Input.LatestHand.rightTip)<.001f,"射线离开指尖");
            if(inset && handRoot.gameObject.activeSelf && (stage!="forming"|| Road.Progress<.22f))
            {
                if(contact && stage=="preview")
                {
                    var at=mark.transform.position;
                    CameraAt(at+new Vector3(.14f,.40f,-.22f),at,45);
                    RenderFrame(small,thumb,"target-"+frame.ToString("D5")+".jpg");
                }
                else
                {
                    LookAtHand();
                    RenderFrame(small,thumb,"hand-"+frame.ToString("D5")+".jpg");
                }
            }
            var front=Road?Road.Front:a.landing.position;
            var marker=GameObject.Find("已选岸标");
            bool locked=marker && marker.GetComponent<LineRenderer>().enabled;
            var hint=GameObject.Find("近岸行路提示");
            bool readable=hint && hint.GetComponent<CanvasGroup>().alpha>.8f;
            data.WriteLine(FormattableString.Invariant($"{frame},{Time.frameCount},{stage},{completions},{detectedPoint},{detectedFist},{(bool)GameObject.Find("通路符搭路预览")},{(Road?Road.Progress:0):F6},{front.x:F5},{front.y:F5},{front.z:F5},{(Road&&Road.GetComponent<Collider>().enabled)},{playerAt.x:F5},{playerAt.y:F5},{playerAt.z:F5},{locked},{ray},{contact},{distance:F4},{av.x:F6},{av.y:F6},{bv.x:F6},{bv.y:F6},{readable}"));
            frame++;
        }
        public void Dispose()
        {
            PlayerLoop.SetPlayerLoop(originalLoop);data.Dispose();template.onCompleted.RemoveListener(Completed);
            cam.targetTexture=null;rt.Release();small.Release();Object.Destroy(pix);Object.Destroy(thumb);
            Object.Destroy(rt);Object.Destroy(small);Object.Destroy(cam.gameObject);Object.Destroy(handRoot.gameObject);Object.Destroy(handMaterial);
        }
    }
}
