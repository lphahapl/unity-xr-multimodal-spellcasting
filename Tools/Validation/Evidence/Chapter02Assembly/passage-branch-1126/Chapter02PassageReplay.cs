using System;
using System.Collections;
using System.Collections.Generic;
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

// Desktop input fixture. Only the source of SealFrame is replaced. The player's LateUpdate,
// input ownership, locomotion, stage triggers, physical tool belt, drawing, ray and fist run normally.
public static class Chapter02PassageReplay
{
    const string Key="CH02.Passage.capture";
    static IEnumerator running;
    static int previous = -1;
    static double deadline;
    static string Folder => SessionState.GetString(Key,"");
    [InitializeOnLoadMethod] static void Bind() => EditorApplication.update += Tick;
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Chapter02_SuspendedPath.unity");
        var p=Object.FindFirstObjectByType<LinearRpgController>();
        string id=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        p.autoLoad=false; p.saveFileName="qa-passage-"+id+".json";
        string dir=Path.GetFullPath("Logs/ChapterPassage-"+id);
        Directory.CreateDirectory(dir);
        SessionState.SetString(Key,dir);
        Debug.Log("CH02_PASSAGE_FILM "+dir);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if(Folder.Length==0 || !Application.isPlaying || previous==Time.frameCount)return;
        previous=Time.frameCount;
        try {
            if(running==null){ deadline=EditorApplication.timeSinceStartup+540;running=Play(); }
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("chapter replay");
            if(running.MoveNext())return;
            Log("PASS END"); SessionState.EraseString(Key); EditorApplication.Exit(0);
        }catch(Exception ex){Log("FAIL "+ex);Debug.LogException(ex);(running as IDisposable)?.Dispose();SessionState.EraseString(Key);EditorApplication.Exit(1);}
    }
    static void Log(string m){File.AppendAllText(Path.Combine(Folder,"checks.txt"),m+"\n");Debug.Log("PASSAGE_ROUTE "+m);}
    static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException(msg);Log("PASS "+msg);}
    static IEnumerator Play()
    {
        var l=Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
        while(!l.player.CanAct)yield return null;
        Time.captureFramerate=30;
        using var f=new Film(l);
        foreach(var x in f.Route())yield return x;
    }

    enum PoseMode { Off, Rest, Grip, Point, Fist }
    sealed class Film:IDisposable
    {
        readonly Chapter02BridgeEquipment l;
        readonly LinearRpgController p;
        readonly GlyphPenController pen;
        readonly NarutoVR.SealLab.SealLab lab;
        readonly SealFeatureExtractor extractor=new();
        readonly PlayerLoopSystem old;
        readonly Rig left,right;
        readonly PassageGlyphAnchor a,b,c;
        readonly Transform head;
        readonly Camera cam;
        readonly RenderTexture surface;
        readonly Texture2D pix;
        readonly StreamWriter csv;
        readonly Vector3 nibFromGrip;
        readonly Dictionary<GlyphTemplate,int> matches=new();
        readonly GlyphTemplate passage,weight;
        PoseMode mode; bool pinch,walking,neutralLeft;
        Vector3 grip,aim,leftDir;
        Quaternion rot;
        int frame; double lastInput;
        string shot;
        PassageGlyphRoad road;
        Vector3 Floor => p.locomotion.PlayerFloorPosition;
        internal Film(Chapter02BridgeEquipment lesson)
        {
            l=lesson;p=l.player;pen=l.glyph;head=p.locomotion.head;
            head.position=Floor+Vector3.up*1.62f;
            lab=p.handTracking?p.handTracking:p.skills.lab;
            lab.enabled=false;
            right=new Rig("Right");left=new Rig("Left");
            var marks=l.repairedDeck.GetComponentsInChildren<PassageGlyphAnchor>(true);
            a=marks.Single(x=>x.name.StartsWith("甲"));b=marks.Single(x=>x.name.StartsWith("乙"));c=marks.Single(x=>x.name.StartsWith("丙"));
            passage=pen.activeTemplates.Single(x=>x.effect is PassageGlyphEffect);
            weight=pen.activeTemplates.Single(x=>x.effect is QianjunEffectDefinition);
            foreach(var template in new[]{passage,weight}) {var t=template;matches[t]=0;t.onCompleted.AddListener(_=>matches[t]++);}
            nibFromGrip=Quaternion.Inverse(l.equipment.pen.transform.rotation)*(pen.nib.position-l.equipment.pen.GripPosition);
            cam=new GameObject("实录观察镜头").AddComponent<Camera>();cam.CopyFrom(Camera.main);
            cam.enabled=false;cam.stereoTargetEye=StereoTargetEyeMask.None;cam.aspect=16f/9;cam.nearClipPlane=.025f;
            surface=new RenderTexture(960,540,24);surface.Create();cam.targetTexture=surface;
            pix=new Texture2D(960,540,TextureFormat.RGB24,false);
            csv=new StreamWriter(Path.Combine(Folder,"frames.csv")){AutoFlush=true};
            csv.WriteLine("frame,unity,shot,stage,x,y,z,point,fist,penHeld,ink,preview,progress,collider,frontX,frontY,frontZ");
            old=PlayerLoop.GetCurrentPlayerLoop();var replay=old;
            replay.subSystemList=(PlayerLoopSystem[])old.subSystemList.Clone();
            for(int i=0;i<replay.subSystemList.Length;i++) if(replay.subSystemList[i].type==typeof(UnityEngine.PlayerLoop.PreLateUpdate))
            {
                ref var phase=ref replay.subSystemList[i];var sys=phase.subSystemList.ToList();
                int at=sys.FindIndex(s=>s.type==typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate));
                sys.Insert(at,new PlayerLoopSystem{type=typeof(Film),updateDelegate=Supply});phase.subSystemList=sys.ToArray();
            }
            PlayerLoop.SetPlayerLoop(replay);
            p.locomotion.SetExplorationMode(ExplorationMovement.PointWalk);
        }
        void Supply()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            int wait=(int)((1d/30-(now-lastInput))*1000); if(wait>0)System.Threading.Thread.Sleep(wait);
            lastInput=Time.realtimeSinceStartupAsDouble; var f=RpgHandPoseChecks.Skeleton(lastInput);
            f.source="Chapter02 desktop joints";var origin=lab.recordingOrigin;
            left.Set(walking?PoseMode.Point:neutralLeft?PoseMode.Rest:PoseMode.Off,head.position-head.right*.23f-Vector3.up*.22f+head.forward*.2f,leftDir,Quaternion.identity,false,false,f.left,origin);
            right.Set(mode,grip,aim,rot,pinch,true,f.right,origin);
            lab.GetType().GetProperty("Frame").SetValue(lab,f);
            extractor.Extract(f,lab.Features);
        }
        internal IEnumerable Route()
        {
            Check(p.StageIndex==0&&!l.repairedDeck.activeInHierarchy&&!pen.enabled&&!a.Available,"新局从原入口开始，符岸与笔未开放");
            foreach(var x in Hold("entrance",8))yield return x;
            foreach(var x in Walk(p.CheckpointAt(1).floorPosition,"approach"))yield return x;
            for(int i=0;i<120&&(!pen.isActiveAndEnabled || !l.button.gameObject.activeInHierarchy);i++)yield return null;
            Check(p.StageIndex==1 && l.button.gameObject.activeInHierarchy,"步行到石案触发原教学");
            // No hand pose, or pinch away from the belt, opens no free board.
            Grip(head.position+head.forward*.50f,head.rotation,false);yield return null;
            Grip(grip,rot,true);yield return null;
            Check(!pen.IsHeld&&(!pen.activeBoard||!pen.activeBoard.IsOpen),"空挥和空捏未展开主动板");
            var axis=l.button.cap.forward;var cap=l.button.RestSurface;
            var toStation=Vector3.ProjectOnPlane(cap-Floor,Vector3.up).normalized;
            foreach(var x in Walk(new Vector3(cap.x,Floor.y,cap.z)-toStation*.47f,"approach"))yield return x;
            head.rotation=Quaternion.LookRotation(toStation);
            foreach(float d in new[]{-.024f,.008f,-.024f}){var t=cap+axis*d;FingerAt(t);yield return null;yield return null;}
            Check(l.panel.IsWaiting,"指尖按铜扣，剧情板打开");
            foreach(var x in Take("square"))yield return x;
            Check(!pen.activeBoard||!pen.activeBoard.IsOpen,"石案方直之矩优先于两种主动符");
            foreach(var x in Ink(l.panel.board,l.panel.template,"square"))yield return x;
            for(int i=0;i<240 && p.StageIndex==1;i++){yield return null;if(i%4==0)Snap("square");}
            Check(p.Model.Chapter.IsComplete(l.drawingStage.stageId)&&l.repairedDeck.activeInHierarchy&&p.StageIndex==2,"方直之矩实体桥修复后支路开放");
            foreach(var x in Stow())yield return x;
            var lane=new Vector3(p.CheckpointAt(1).floorPosition.x,a.landing.position.y,a.landing.position.z-2.85f);
            foreach(var x in Walk(lane,"mainBridge"))yield return x;
            var mouth=a.landing.position-Vector3.right*1.41f-Vector3.forward*2.85f;
            foreach(var x in Walk(mouth,"branch",.035f))yield return x;
            foreach(var x in Walk(mouth+Vector3.forward*1.54f,"branch",.045f))yield return x;
            foreach(var x in Walk(a.landing.position-Vector3.forward*1.08f,"branch"))yield return x;
            head.rotation=Quaternion.LookRotation(Vector3.forward);mode=PoseMode.Off;
            var mid=(a.landing.position+b.landing.position)*.5f;
            Check(!Physics.Raycast(mid+Vector3.up*.5f,Vector3.down,1f,p.locomotion.floorLayers,QueryTriggerInteraction.Ignore),"尚未搭路时两岸中点无可走地面");
            for(int i=0;i<22;i++){yield return null;Snap("bank");}
            foreach(var x in Take("weight"))yield return x;
            foreach(var x in Ink(pen.activeBoard,weight,"weight"))yield return x;
            yield return null;
            Check(matches[weight]==1&&pen.Status=="笔尖指向可推拉的重物"&&pen.IsHeld,"真实笔尖三笔识别千钧，启动生产牵引效果");
            foreach(var x in Stow())yield return x;
            foreach(var x in Take("ink"))yield return x;
            foreach(var x in Ink(pen.activeBoard,passage,"ink"))yield return x;
            Check(matches[passage]==1&&GameObject.Find("通路符 · 右手金光"),"真实笔尖两笔识别通路符");
            foreach(var x in Stow(true))yield return x;
            Check(!l.equipment.interaction.Held&&p.Input.InteractionOwnsHand&&GameObject.Find("通路符 · 右手金光"),"实体笔正常归槽，空手选岸保留输入权");
            Point(a);foreach(var x in Hold("first",22))yield return x;
            var locked=GameObject.Find("已选岸标");
            var hand=p.Input.LatestHand; var hits=Physics.RaycastAll(hand.rightTip,hand.rightIndexDirection,12,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance);
            Check(locked&&locked.GetComponent<LineRenderer>().enabled&&hand.rightPointing,"食指锁定甲岸 / "+pen.Status+" / "+string.Join(", ",hits.Take(5).Select(h=>h.collider.name+" "+h.distance.ToString("F2"))));
            Point(c);foreach(var x in Hold("alternate",16))yield return x;
            Check(GameObject.Find("通路符搭路预览"),"第二岸可自由先选丙");
            Grip(head.position-Vector3.up*.72f+head.forward*.20f,head.rotation,false);
            foreach(var x in Hold("cancel",12))yield return x;
            Check(!p.Input.InteractionOwnsHand&&!GameObject.Find("通路符搭路预览")&&!Object.FindFirstObjectByType<PassageGlyphRoad>(),"空手放低取消预览，未生成可走面");
            foreach(var x in Take("ink"))yield return x;
            foreach(var x in Ink(pen.activeBoard,passage,"ink"))yield return x;
            Check(matches[passage]==2,"取消后同一支笔可再画通路符");
            foreach(var x in Stow(true))yield return x;
            Point(a);foreach(var x in Hold("first",16))yield return x;
            Point(c);foreach(var x in Hold("alternate",12))yield return x;
            Point(b);foreach(var x in Hold("preview",22))yield return x;
            var preview=GameObject.Find("通路符搭路预览");
            Check(preview&&Vector3.Distance(preview.transform.position,(a.landing.position+b.landing.position)*.5f)<.10f,"换选乙岸后预览跟随实际落点");
            mode=PoseMode.Fist;
            for(int i=0;i<18&&!road;i++){yield return null;road=Object.FindFirstObjectByType<PassageGlyphRoad>();Snap("fist");}
            Check(road&&p.Input.LatestHand.rightClosed,"握拳确认成路");
            mode=PoseMode.Off;
            int growing=0;
            while(road.Progress<1){CheckCollision();Snap("grow");growing++;yield return null;}
            Check(growing>25&&road.GetComponent<Collider>().enabled&&Vector3.Distance(road.Front,b.landing.position)<.005f,"中途逐帧无碰撞，前沿到乙岸才可通行，增长采样="+growing);
            foreach(var x in Hold("joined",15))yield return x;
            foreach(var x in Walk(b.bank.position,"cross"))yield return x;
            Check(Physics.Raycast(Floor+Vector3.up,Vector3.down,out var hit,2,~0,QueryTriggerInteraction.Ignore)&&hit.collider.transform.IsChildOf(b.bank),"原探索移动跨桥站上乙岸石台");
            Check(p.StageIndex==2&&!p.Model.Chapter.IsComplete(p.StageAt(2).stageId),"支路未推进或跳过祭坛主线");
            foreach(var x in Hold("arrived",18))yield return x;
        }
        void CheckCollision(){if(road.GetComponent<Collider>().enabled)throw new Exception("未成形已开放碰撞");}
        void Grip(Vector3 g,Quaternion r,bool held){mode=PoseMode.Grip;grip=g;rot=r;pinch=held;}
        void FingerAt(Vector3 t){Grip(t,head.rotation,false);}
        void Point(PassageGlyphAnchor target){mode=PoseMode.Point;grip=head.position+new Vector3(.18f,-.25f,.18f);aim=target.landing.position+Vector3.up*.008f;}
        IEnumerable Hold(string label,int n){for(int i=0;i<n;i++){yield return null;Snap(label);}}
        IEnumerable Take(string label)
        {
            var item=l.equipment.pen;
            for(int i=0;i<120&&item.home.IsSnapping;i++)yield return null;
            var at=item.GripPosition;
            Grip(at,item.transform.rotation,false);yield return null;yield return null;
            Grip(at,item.transform.rotation,true);yield return null;yield return null;
            Check(l.equipment.interaction.Held==item && pen.IsHeld,label+" 从原五槽取出实体墨规笔");
            Snap(label);
        }
        IEnumerable Ink(GlyphBoard board,GlyphTemplate t,string label)
        {
            Check(board&&board.IsOpen,label+" 画板可书写");
            void Sample(Vector3 local){var orientation=board.transform.rotation;Grip(board.transform.TransformPoint(local)-orientation*nibFromGrip,orientation,true);}
            foreach(var line in t.Preview())
            {
                Sample((Vector3)(line[0]*.115f)+Vector3.back*.061f);yield return null;
                Sample((Vector3)(line[0]*.115f)+Vector3.forward*.001f);yield return null;Snap(label);
                for(int i=1;i<line.Length;i++)for(int j=1;j<=7;j++){Sample((Vector3)(Vector2.Lerp(line[i-1],line[i],j/7f)*.115f)+Vector3.forward*.001f);yield return null;Snap(label);}
                Sample(Vector3.back*.061f);yield return null;Snap(label);
            }
            double start=Time.realtimeSinceStartupAsDouble;
            while(board.IsOpen&&!board.IsShowingSuccess&&Time.realtimeSinceStartupAsDouble-start<3){yield return null;if(Time.frameCount%3==0)Snap(label);}
            Check(!board.IsOpen||board.IsShowingSuccess,"停笔辨符 "+t.Title+" / "+board.Status);
        }
        IEnumerable Stow(bool casting=false)
        {
            var item=l.equipment.pen;var slot=(NarutoVR.SealCombat.Items.ToolBeltSlot)item.home;
            var rot=slot.opening.rotation*Quaternion.Euler(item.stowEuler);
            var at=slot.opening.position-rot*nibFromGrip+Vector3.up*.03f;
            Grip(at,rot,true);yield return null;
            Grip(at,rot,false);yield return null;yield return null;
            Check(item.Dock==item.home&&!l.equipment.interaction.Held,"笔归原槽");
            mode=PoseMode.Off;
            if(casting){Point(a);mode=PoseMode.Grip;pinch=false;rot=Quaternion.identity;yield return null;}
        }
        IEnumerable Walk(Vector3 target,string label,float tolerance=.10f)
        {
            mode=PoseMode.Off;walking=false;neutralLeft=true;leftDir=head.forward;for(int k=0;k<14;k++)yield return null;neutralLeft=false;
            float gap=0;int stage=p.StageIndex;
            for(int i=0;i<850;i++)
            {
                if(stage!=p.StageIndex){stage=p.StageIndex;walking=false;neutralLeft=true;for(int k=0;k<14;k++)yield return null;neutralLeft=false;}
                Vector3 delta=Vector3.ProjectOnPlane(target-Floor,Vector3.up);gap=delta.magnitude;
                if(gap<tolerance)break;
                head.rotation=Quaternion.LookRotation(delta.normalized);leftDir=delta.normalized;walking=true;
                yield return null;
                if(i%3==0)Snap(label);
                if(i%160==159 && p.locomotion.Status.Contains("障碍")){var contact=(Collider)typeof(RpgLocomotionController).GetField("walkContact",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p.locomotion); if(contact)Log("阻挡 "+contact.name+" / "+contact.transform.parent?.name+" / "+contact.bounds); }
                if(i%160==159)Log(label+" floor="+Floor+" target="+target+" gap="+gap+" status="+p.locomotion.Status+" pose="+p.Input.HandPoseStatus);
            }
            walking=false;yield return null;yield return null;
            Check(gap<tolerance,label+" 原左食指步行到 "+Floor.ToString("F3"));
        }

        void Snap(string label)
        {
            if(shot!=label){shot=label;Log("SHOT "+shot+" "+Floor);}
            var eye=head.position;
            Vector3 pos=eye,look=eye+head.forward;float fov=67;
            if(label=="square"||label=="ink"||label=="weight"){
                var board=label=="square"?l.panel.board:pen.activeBoard;
                if(board){look=board.transform.position;pos=look-board.transform.forward*.58f+board.transform.up*.035f+board.transform.right*.055f;fov=58;}
            }else if(label=="first"||label=="alternate"||label=="preview"||label=="fist"||label=="grow"||label=="joined"){
                pos=a.landing.position+new Vector3(3.05f,3.3f,-1.85f);look=Vector3.Lerp(a.landing.position,b.landing.position,.59f)+Vector3.right*.65f;fov=60;
            }else if(label=="bank"){look=a.landing.position+Vector3.up*.30f;pos=eye;}
            else if(label=="cross"||label=="arrived"){look=eye+new Vector3(0,-1.1f,2.0f);}
            else{look=eye+head.forward*3f-Vector3.up*.68f;}
            cam.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(look-pos));cam.fieldOfView=fov;
            cam.Render();var backup=RenderTexture.active;RenderTexture.active=surface;pix.ReadPixels(new Rect(0,0,960,540),0,0,false);pix.Apply();RenderTexture.active=backup;
            File.WriteAllBytes(Path.Combine(Folder,frame.ToString("D5")+".jpg"),pix.EncodeToJPG(87));
            var v=Floor;var front=road?road.Front:a.landing.position;var hand=p.Input.LatestHand;
            csv.WriteLine(FormattableString.Invariant($"{frame++},{Time.frameCount},{label},{p.StageIndex},{v.x:F4},{v.y:F4},{v.z:F4},{hand.rightPointing},{hand.rightClosed},{(bool)l.equipment.interaction.Held},{pen.activeBoard?.PointCount??0},{(bool)GameObject.Find("通路符搭路预览")},{(road?road.Progress:0):F6},{(road&&road.GetComponent<Collider>().enabled)},{front.x:F4},{front.y:F4},{front.z:F4}"));
        }
        public void Dispose(){PlayerLoop.SetPlayerLoop(old);lab.enabled=true;csv.Dispose();surface.Release();Object.Destroy(cam.gameObject);Object.Destroy(surface);Object.Destroy(pix);left.Dispose();right.Dispose();}
    }

    sealed class Rig:IDisposable
    {
        const string SDK="Packages/com.meta.xr.sdk.interaction/Runtime/";
        readonly Transform root,wrist,tip,joint,index,middle,pinky;
        readonly Transform[][] fingers;readonly AnimationClip point,fist,open;
        readonly Material mat;
        internal Rig(string side)
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SDK+"Meshes/OpenXR"+side+"Hand.fbx")).transform;
            foreach(var a in root.GetComponentsInChildren<Animator>())a.enabled=false;
            var bones=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("XRHand_")).ToDictionary(t=>t.name.Substring(7));
            wrist=bones["Wrist"];tip=bones["IndexTip"];joint=bones["IndexIntermediate"];index=bones["IndexProximal"];middle=bones["MiddleProximal"];pinky=bones["LittleProximal"];
            fingers=new[]{"Thumb","Index","Middle","Ring","Little"}.Select(n=>n=="Thumb"?new[]{wrist,bones[n+"Metacarpal"],bones[n+"Proximal"],bones[n+"Distal"],bones[n+"Tip"]}:
                new[]{bones[n+"Metacarpal"],bones[n+"Proximal"],bones[n+"Intermediate"],bones[n+"Distal"],bones[n+"Tip"]}).ToArray();
            root.localScale*=.18f/Vector3.Distance(wrist.position,tip.position);
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.73f,.57f,.44f));
            var smr=root.GetComponentInChildren<SkinnedMeshRenderer>(true);smr.sharedMaterial=mat;smr.updateWhenOffscreen=true;
            AnimationClip Clip(string n)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(SDK+"Animations/Hands/OpenXR_"+n+"_"+side+".anim");
            point=Clip("IndexPoint");fist=Clip("HandFist");open=Clip("HandSlide");
            if(!point||!fist||!open)throw new Exception("SDK clips "+side);
        }
        internal void Set(PoseMode mode,Vector3 pos,Vector3 target,Quaternion rotation,bool pinch,bool right,HandSnapshot h,Transform origin)
        {
            root.gameObject.SetActive(mode!=PoseMode.Off);
            if(mode==PoseMode.Off){h.connected=h.tracked=h.fresh=false;return;}
            (mode==PoseMode.Fist?fist:mode==PoseMode.Point?point:open).SampleAnimation(wrist.parent.gameObject,0);
            if(mode==PoseMode.Grip){
                Vector3 center=(index.position+middle.position)*.5f;
                var actual=Quaternion.LookRotation(center-wrist.position,Vector3.Cross(index.position-wrist.position,pinky.position-wrist.position));
                root.rotation=rotation*Quaternion.Inverse(actual)*root.rotation;
                root.position+=pos-(index.position+middle.position)*.5f;
            }else{
                Vector3 axis=right?tip.position-joint.position:tip.position-index.position;
                var toward=right?(target-pos).normalized:target;
                if(mode!=PoseMode.Fist&&toward.sqrMagnitude>.1f){
                    root.rotation=Quaternion.LookRotation(toward,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(axis,Vector3.Cross(axis,index.position-pinky.position)))*root.rotation;
                }
                root.position+=pos-wrist.position;
                if(right&&mode==PoseMode.Point){root.rotation=Quaternion.FromToRotation(tip.position-joint.position,target-tip.position)*root.rotation;root.position+=pos-wrist.position;}
            }
            void Joint(HandJointId id,Transform t,Vector3 at){
                h.joints[(int)id]=new Pose(origin?origin.InverseTransformPoint(at):at,origin?Quaternion.Inverse(origin.rotation)*t.rotation:t.rotation);
                h.wristPoses[(int)id]=new Pose(Quaternion.Inverse(wrist.rotation)*(at-wrist.position),Quaternion.Inverse(wrist.rotation)*t.rotation);
            }
            Joint(HandJointId.HandWristRoot,wrist,wrist.position);
            for(int f=0;f<5;f++){
                var ids=HandJointUtils.FingerToJointList[f];
                for(int j=0;j<ids.Length;j++){
                    var t=fingers[f][5-ids.Length+j];Vector3 at=t.position;
                    if(mode==PoseMode.Grip&&j==ids.Length-1){if(f==1)at=pos;else if(f==0)at=pos+rotation*Vector3.right*(pinch?.012f:.065f);}
                    Joint(ids[j],t,at);
                }
            }
        }
        public void Dispose(){Object.Destroy(root.gameObject);Object.Destroy(mat);}
    }
}
