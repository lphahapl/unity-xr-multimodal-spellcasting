using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Events;
using UnityEngine.Events;
using NarutoVR.SealCombat.Validation;
using NarutoVR.SealCombat.Items;
using System.Reflection;
using NeuGameMRTools.Runtime.Components;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>独立空场景内核对曲线、序列、物理恢复、回调重入和实际触发/Meta 选择；不保存用户场景。</summary>
    [InitializeOnLoad]
    public static class SnapZoneChecks
    {
        const string Key = "NarutoVR.SnapCheck.Running", Previous = "NarutoVR.SnapCheck.PreviousScene";
        const string ScenePath = "Assets/Scene/SnapToolTest.unity";
        const string ReportPath = "Logs/snap-tool-adaptation-20261104/check.txt";
        static readonly List<string> passed = new();
        static IEnumerator running;
        static GameObject fixture;
        static double deadline;
        static int lastFrame;
        static bool background, restartXr;
        static XRManagerSettings xr;

        static SnapZoneChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    background = Application.runInBackground; Application.runInBackground = true;
                    xr = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
                    var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
                    restartXr = xr && xr.isInitializationComplete && displays.Exists(x => x.running);
                    if (restartXr) xr.StopSubsystems();
                    passed.Clear(); running = Run(); lastFrame = -1; deadline = EditorApplication.timeSinceStartup + 45;
                }
                if (state == PlayModeStateChange.ExitingPlayMode && running != null) Finish(new Exception("检查被提前停止"));
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Previous, ""));
                    EditorApplication.isPaused = SessionState.GetBool(Key + ".Paused", false);
                    if (SessionState.GetBool(Key + ".Batch", false))
                    {
                        SessionState.EraseBool(Key + ".Batch");
                        int code = SessionState.GetInt(Key + ".Exit", 1);
                        SessionState.EraseInt(Key + ".Exit");
                        EditorApplication.delayCall += () => EditorApplication.Exit(code);
                    }
                    SessionState.EraseBool(Key); SessionState.EraseBool(Key + ".Paused"); SessionState.EraseString(Previous);
                }
            };
            EditorApplication.update += Poll;
        }

        /// <summary>批处理运行同一套 Play 检查，失败带非零进程退出码。</summary>
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("请使用独立批处理工程");
            SessionState.SetBool(Key + ".Batch", true);
            StartChecks();
        }

        static void Poll()
        {
            const string request = "Logs/snap-tool.request";
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(request))
            {
                string command = File.ReadAllText(request).Trim(); File.Delete(request);
                try
                {
                    if (command == "check") StartChecks();
                    else if (command == "demo") CreateDemo();
                    else throw new ArgumentException("未知吸附工具请求：" + command);
                }
                catch (Exception e) { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, "FAILED\n" + e); Debug.LogException(e); }
            }
            if (running == null || !Application.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("吸附真实帧检查超时");
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                if (!running.MoveNext()) Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }

        [MenuItem("Tools/NarutoVR/交互/验证吸附与序列")]
        public static void StartChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("检查需要从编辑模式启动");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            bool batch = SessionState.GetBool(Key + ".Batch", false);
            if (batch) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else if (!File.Exists(ScenePath))
            {
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try { EditorSceneManager.SaveScene(scene, ScenePath); }
                finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
            }
            SessionState.SetString(Previous, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + ".Paused", EditorApplication.isPaused);
            SessionState.SetBool(Key, true); EditorApplication.isPaused = false;
            EditorSceneManager.playModeStartScene = batch ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            File.WriteAllText(ReportPath, "RUNNING\n" + DateTimeOffset.Now.ToString("O"));
            EditorApplication.isPlaying = true;
        }

        static void Finish(Exception error)
        {
            running = null;
            if (fixture) Object.DestroyImmediate(fixture);
            Time.timeScale = 1;
            Application.runInBackground = background;
            if (restartXr && xr) xr.StartSubsystems();
            restartXr = false;
            var text = DateTimeOffset.Now.ToString("O") + "\n" + (error == null ? "PASS " + passed.Count : "FAILED " + error) + "\n" + string.Join("\n", passed);
            File.WriteAllText(ReportPath, text);
            if (error == null) Debug.Log(text); else Debug.LogException(error);
            SessionState.SetInt(Key + ".Exit", error == null ? 0 : 1);
            EditorApplication.isPlaying = false;
        }


        static T OldEvent<T>(SnapZone zone, string field) where T : class =>
            (T)typeof(SnapZone).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(zone);

        static void InstallOldListeners(SnapZoneContractProbe probe)
        {
            UnityEventTools.AddPersistentListener(OldEvent<UnityEvent<GameObject>>(probe, "_onEnterEvent"), probe.LegacyStart);
            UnityEventTools.AddPersistentListener(OldEvent<SnapPointEvent>(probe, "_onPointReachedEvent"), probe.LegacyPoint);
            UnityEventTools.AddPersistentListener(OldEvent<UnityEvent<GameObject>>(probe, "_onSnapCompletedEvent"), probe.LegacyComplete);
            UnityEventTools.AddPersistentListener(OldEvent<UnityEvent<GameObject>>(probe, "_onExitEvent"), probe.LegacyRelease);
        }

        static IEnumerator CheckTemplate()
        {
            var original = New("旧序列化事件").AddComponent<SnapZoneContractProbe>();
            InstallOldListeners(original);
            // Instantiate 会经 Unity 序列化克隆并重新映射同物体上的持久监听目标。
            var probe = Object.Instantiate(original, fixture.transform);
            Object.DestroyImmediate(original.gameObject);
            SnapZone zone = probe;
            var oldEnter = OldEvent<UnityEvent<GameObject>>(zone, "_onEnterEvent");
            Check(oldEnter.GetPersistentEventCount() == 1 && oldEnter.GetPersistentTarget(0) == probe &&
                oldEnter.GetPersistentMethodName(0) == nameof(probe.LegacyStart), "旧 UnityEvent 持久调用克隆后保留目标与方法");
            foreach (var entry in new[]{"TrySnapObject", "SnapObject", "ReleaseObject", "TrySnapObjectAtRotation"})
                Check(!typeof(SnapZone).GetMethod(entry).IsVirtual, "基类固定入口 " + entry);
            var editor = UnityEditor.Editor.CreateEditor(probe);
            try { Check(editor is NeuGameMRTools.Editor.Editors.SnapZoneEditor, "派生吸附区复用基础 Inspector"); }
            finally { Object.DestroyImmediate(editor); }
            var serialized = new SerializedObject(probe);
            Check(new[]{"_snapTarget","_snapRotationTarget","_snapCurve","_snapPoints","_snapDuration","_snapMode",
                "_followZoneDuringSnap","_requireNameMatch","_targetObjectName","_disableCollidersOnSnap","_waitForRelease",
                "_requireSnapItem","_onEnterEvent","_onPointReachedEvent","_onSnapCompletedEvent","_onExitEvent"}
                .All(name=>serialized.FindProperty(name)!=null), "旧字段仍可序列化并从派生 Inspector 查找");
            var item = Item("带混合初始物理标志的道具");
            var child = New("道具子节点"); child.transform.SetParent(item.transform,false);
            var body = item.GetComponent<Rigidbody>(); body.isKinematic=false; body.useGravity=true;
            var off = child.AddComponent<BoxCollider>(); off.enabled=false;
            var drive = item.AddComponent<Light>(); drive.enabled=true;
            Set(item.GetComponent<SnapItem>(), "_additionalDrivers", new Behaviour[]{drive});
            var peer = New("竞争插槽").AddComponent<SnapZone>();
            Set(peer, "_snapDuration", 2f);
            var trace = new List<string>();
            probe.Legacy = trace.Add;
            zone.OnSnapStarted += _ => trace.Add("C.start");
            zone.OnPointReached += (_,i) => trace.Add("C.point"+i);
            zone.OnSnapCompleted += _ => trace.Add("C.done");
            zone.OnSnapReleased += (_,done) => trace.Add(done ? "C.exit.done" : "C.exit.cancel");
            bool Restored() => !zone.SnappedObject && !zone.IsSnapping && body.useGravity && !body.isKinematic &&
                item.GetComponent<Collider>().enabled && !off.enabled && drive.enabled;
            void CanClaimAgain(string label)
            {
                Check(Restored() && peer.TrySnapObject(item), label);
                peer.ReleaseObject();
            }
            Set(zone, "_targetObjectName", "明确不匹配的自动过滤条件");
            Set(zone, "_snapMode", SnapMode.Sequence);
            var mid=Point("中途", new Vector3(20,0,0));
            var end=Point("资产中的终点", new Vector3(21,0,0));
            Set(zone, "_snapPoints", new List<SnapWaypoint>{
                new SnapWaypoint{target=mid,overrideDuration=true,duration=0},
                new SnapWaypoint{target=end,overrideDuration=true,duration=0}
            });
            var pose = new Pose(new Vector3(23,1,0),Quaternion.Euler(16,73,5));
            probe.FinalPose=pose;
            probe.Accept=false;
            var before = item.transform.position;
            var req=Quaternion.Euler(0,53,0);
            Check(!zone.TrySnapObjectAtRotation(child,req) && zone.LastFailure.Contains("拒收") &&
                probe.Candidate==item && probe.Requested.HasValue && Quaternion.Angle(probe.Requested.Value,req)<.01f &&
                Near(item.transform.position,before) && trace.Count==0, "基类定向调用经过派生准入，拒绝无位移、无反馈");
            CanClaimAgain("拒绝后碰撞、驱动、重力和所有权完整");
            probe.Accept=true;
            var midRead=Vector3.zero;
            zone.OnPointReached += (obj,i)=>{ if(i==0) midRead=obj.transform.position; };
            Check(zone.TrySnapObjectAtRotation(child,req) && Near(midRead,mid.position) &&
                Near(item.transform.position,pose.position) && Quaternion.Angle(item.transform.rotation,pose.rotation)<.02f,
                "派生指定世界终姿，只覆盖最后一段，显式调用兼容旧名称过滤语义");
            Check(!drive.enabled && !body.useGravity && body.isKinematic && !item.GetComponent<Collider>().enabled,
                "接管期间基类独占物理与抓取驱动");
            zone.ReleaseObject(); zone.ReleaseObject();
            var legacy = trace.Where(v=>v.StartsWith("U.") || v.StartsWith("C.point") || v=="C.done");
            Check(string.Join(",",legacy)=="U.start,U.point0,C.point0,U.point1,C.point1,U.done,C.done,U.exit" &&
                trace.Count(v=>v=="C.start")==1 && trace.Count(v=>v=="C.exit.done")==1 &&
                trace.IndexOf("C.start")<trace.IndexOf("U.point0") && trace.Count==10,
                "点和完成保持 UnityEvent 在 C# 前，整轮接管与完成后释放各反馈一次");
            CanClaimAgain("完成后重复释放保持幂等，旧状态已还原");

            Set(zone,"_snapMode",SnapMode.SinglePoint); Set(zone,"_snapDuration",0f);
            trace.Clear();
            Action<GameObject,int> abort = (_,__)=>zone.ReleaseObject();
            zone.OnPointReached += abort;
            zone.SnapObject(child);
            zone.OnPointReached -= abort;
            Check(trace.Contains("C.start") && trace.Contains("C.point0") && !trace.Contains("U.done") &&
                !trace.Contains("C.done") && trace.Count(v=>v=="C.exit.cancel")==1, "点回调释放只发中断，旧路径不发完成");
            CanClaimAgain("点回调中断后另一区域能接管");

            trace.Clear(); Set(zone,"_snapDuration",2f);
            Action<GameObject> disable = _=>zone.enabled=false;
            zone.OnSnapStarted += disable;
            zone.TrySnapObject(item);
            zone.OnSnapStarted -= disable; zone.enabled=true;
            Check(trace.Count(v=>v=="C.exit.cancel")==1 && !trace.Contains("C.point0"),"开始反馈中禁用触发唯一中断");
            CanClaimAgain("禁用中的释放恢复原物理与驱动");
            trace.Clear();
            probe.DuringPrepare=()=>throw new InvalidOperationException("SNAP_EXPECTED_PREPARE");
            try { zone.TrySnapObject(item); } catch (InvalidOperationException e) when(e.Message=="SNAP_EXPECTED_PREPARE") { }
            probe.DuringPrepare=null;
            Check(trace.Count==0 && Restored(), "准入异常既不占用也不发已接管反馈");
            CanClaimAgain("准入异常后对象仍可被接管");

            trace.Clear();
            bool peerWon=false;
            probe.DuringPrepare=()=>{ peerWon=peer.TrySnapObject(item); };
            bool primaryWon=zone.TrySnapObject(item);
            probe.DuringPrepare=null;
            Check(peerWon!=primaryWon && (zone.SnappedObject==item)==primaryWon && (peer.SnappedObject==item)==peerWon,
                "准入钩子重入其他插槽时同一道具只有一个赢家");
            peer.ReleaseObject(); zone.ReleaseObject();
            CanClaimAgain("准入竞争后双边释放不会泄漏驱动与物理");
            trace.Clear(); probe.DuringPrepare=()=>zone.enabled=false;
            bool deactivated=zone.TrySnapObject(item);
            probe.DuringPrepare=null;
            Check(!deactivated && !zone.SnappedObject && trace.Count==0 && Restored(),
                "准入回调禁用本区后不提交所有权");
            zone.enabled=true;
            CanClaimAgain("准入中禁用不遗留静态占用");

            Set(zone,"_snapDuration",0f); trace.Clear();
            Action<GameObject> fault = _=>throw new InvalidOperationException("SNAP_EXPECTED_OBSERVER");
            int afterFault=0; Action<GameObject> witness=_=>afterFault++;
            zone.OnSnapCompleted+=fault; zone.OnSnapCompleted+=witness;
            Check(zone.TrySnapObject(item) && afterFault==1 && zone.SnappedObject==item, "完成监听抛错后其余订阅仍能观察已提交结果");
            zone.OnSnapCompleted-=fault; zone.OnSnapCompleted-=witness;
            var successor=Item("退出回调接管的新道具");
            bool nested=false;
            Action<GameObject,bool> replace=(obj,done)=>{if(obj==item && done) nested=zone.TrySnapObject(successor);};
            zone.OnSnapReleased+=replace; zone.ReleaseObject(); zone.OnSnapReleased-=replace;
            Check(nested && zone.SnappedObject==successor && !zone.IsSnapping && !body.isKinematic && drive.enabled,
                "释放反馈中开启新轮，旧轮不清掉新物体");
            zone.ReleaseObject();
            CanClaimAgain("异常与回调重入后原道具可重新归属");

            var reentrantDriver=item.AddComponent<SnapDriverCallbackProbe>();
            Set(item.GetComponent<SnapItem>(), "_additionalDrivers",new Behaviour[]{drive,reentrantDriver});
            trace.Clear(); Set(zone,"_snapDuration",2f);
            reentrantDriver.DuringDisable=()=>{if(zone.SnappedObject==item)zone.ReleaseObject();};
            bool accepted=zone.TrySnapObject(item);
            reentrantDriver.DuringDisable=null;
            bool started=trace.Contains("C.start"), released=trace.Contains("C.exit.cancel");
            CanClaimAgain("驱动 OnDisable 中断接管仍可恢复物理归属");
            var abortedTrace = string.Join(",",trace);
            bool paired = (!accepted && trace.Count==0) || (accepted && started && released &&
                trace.IndexOf("C.start") < trace.IndexOf("C.exit.cancel"));
            trace.Clear();
            Check(zone.TrySnapObject(item),"再次启动驱动保护用例");
            bool peerDuringRestore=false;
            reentrantDriver.DuringEnable=()=>peerDuringRestore=peer.TrySnapObject(item);
            zone.ReleaseObject(); reentrantDriver.DuringEnable=null;
            Check(!peerDuringRestore && Restored() && reentrantDriver.enabled,"驱动 OnEnable 恢复完成前不允许另一插槽抢占");

            var opening=Point("工具带开口",new Vector3(32,0,3));
            var pocketRoot=New("正式袋口"); pocketRoot.transform.SetParent(opening,false);
            var pocket=pocketRoot.AddComponent<ToolBeltSlot>(); pocket.opening=opening; pocket.penHolster=true;
            var generic=Item("通用测试道具"); var gameItem=generic.AddComponent<GameItemBase>();
            gameItem.stowEnvelope=generic.GetComponent<BoxCollider>();
            SnapZone production=pocket; Set(production,"_snapDuration",0f);
            Check(!production.TrySnapObject(generic) && !gameItem.Dock, "通过基类直接放置时生产笔套也拒绝普通物件");
            pocket.penHolster=false; gameItem.stowEnvelope.size=Vector3.one;
            Check(!production.TrySnapObjectAtRotation(generic,Quaternion.identity) && !gameItem.Dock, "生产袋口尺寸限制无法经基类定向入口绕过");
            gameItem.stowEnvelope.size=Vector3.one*.03f;
            Check(production.TrySnapObjectAtRotation(generic,req) && gameItem.Dock==production && gameItem.LastDock==production &&
                Quaternion.Angle(generic.transform.rotation,req)<.02f && generic.transform.position.x>31.9f,
                "生产派生袋口通过基类落座并记录来源");
            production.ReleaseObject();
            Check(!gameItem.Dock && gameItem.LastDock==production,"由基类释放生产袋口后来源记录保留");

            var otherOpening=Point("备用袋口",opening.position+Vector3.right);
            var otherRoot=New("另一正式袋口"); otherRoot.transform.SetParent(otherOpening,false);
            var otherSlot=otherRoot.AddComponent<ToolBeltSlot>(); otherSlot.opening=otherOpening;
            Set(otherSlot,"_snapDuration",0f);
            foreach(SnapZone next in new SnapZone[]{otherSlot, production})
            {
                Check(production.TrySnapObject(generic),"取物重入前处于已完成的原袋口");
                bool reseated=false;
                Action<GameObject,bool> reseat=(obj,done)=>{if(done && obj==generic) reseated=next.TrySnapObject(obj);};
                production.OnSnapReleased+=reseat;
                bool held=gameItem.Take();
                production.OnSnapReleased-=reseat;
                bool consistent = reseated ? !held && !gameItem.IsHeld && gameItem.Dock==next && next.SnappedObject==generic :
                    held && gameItem.IsHeld && !gameItem.Dock && !production.SnappedObject && !otherSlot.SnappedObject;
                Check(consistent,"取物触发释放时回调重新入"+(next==production ? "原口":"另一袋口")+"，手与插槽只保留一个所有者");
                if(reseated) next.ReleaseObject(); else gameItem.Drop();
            }

            // 真正的 FixedUpdate 触发。显式入口的名称例外不可泄漏到自动入口。
            var triggerObj=New("派生真实触发区"); triggerObj.transform.position=Vector3.right*80;
            var triggerProbe=triggerObj.AddComponent<SnapZoneContractProbe>(); SnapZone automatic=triggerProbe;
            var trigger=triggerObj.AddComponent<BoxCollider>(); trigger.isTrigger=true;
            var entering=Item("符合的自动物件");
            entering.transform.position=triggerObj.transform.position+Vector3.left*2;
            var triggerChild=New("触发子碰撞"); triggerChild.transform.SetParent(entering.transform,false);
            triggerChild.AddComponent<SphereCollider>().radius=.04f; entering.GetComponent<Collider>().enabled=false;
            triggerProbe.Accept=false;
            int starts=0,ends=0; automatic.OnSnapStarted+=_=>starts++; automatic.OnSnapCompleted+=_=>ends++;
            Set(automatic,"_snapDuration",0f); Set(automatic,"_targetObjectName","另一种名称");
            entering.transform.position=triggerObj.transform.position; Physics.SyncTransforms();
            double until=Time.realtimeSinceStartupAsDouble+.16;
            while(Time.realtimeSinceStartupAsDouble<until) yield return null;
            Check(triggerProbe.Calls==0 && !automatic.SnappedObject,"自动入口先执行旧名称过滤");
            Set(automatic,"_targetObjectName","自动");
            until=Time.realtimeSinceStartupAsDouble+3;
            while(triggerProbe.Calls==0 && Time.realtimeSinceStartupAsDouble<until) yield return null;
            Check(triggerProbe.Calls>0 && triggerProbe.Candidate==entering && starts==0 && ends==0 && !automatic.SnappedObject,
                "真实子碰撞触发经过相同派生准入，拒绝不误发");
            triggerProbe.Accept=true;
            until=Time.realtimeSinceStartupAsDouble+3;
            while(!automatic.SnappedObject && Time.realtimeSinceStartupAsDouble<until) yield return null;
            Check(automatic.SnappedObject==entering && starts==1 && ends==1 && !triggerProbe.Requested.HasValue,
                "原地满足条件后触发接管并完成一次");
            automatic.ReleaseObject();
            Check(paired, $"驱动 OnDisable 内中断接管的反馈须配对，accepted={accepted}，trace={abortedTrace}");
        }

        static void Set(object obj, string name, object value) => (obj is SnapZone ? typeof(SnapZone) : obj.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        static void Call(SnapZone zone, string method, params object[] args) => typeof(SnapZone).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(zone, args);
        static void Check(bool value, string name) { if (!value) throw new Exception(name); passed.Add(name); }
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
        static GameObject New(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(fixture.transform, false); return go;
        }
        static Transform Point(string name, Vector3 p) { var go = New(name); go.transform.position = p; return go.transform; }
        static GameObject Item(string name)
        {
            var go = New(name); go.AddComponent<SnapItem>();
            var rb = go.AddComponent<Rigidbody>(); rb.useGravity = false; rb.isKinematic = true;
            go.AddComponent<BoxCollider>().size = Vector3.one * .05f;
            return go;
        }

        static IEnumerator Run()
        {
            fixture = new GameObject("SnapChecks_Isolated");
            var contracts = CheckTemplate();
            while (contracts.MoveNext()) yield return contracts.Current;
            var zone = New("Zone").AddComponent<SnapZone>();
            var a = Item("A"); var b = Item("B");
            var target = Point("Target", Vector3.right * 2); target.rotation = Quaternion.Euler(0, 90, 0);
            Set(zone, "_snapTarget", target); Set(zone, "_snapRotationTarget", target); Set(zone, "_snapDuration", 2f);
            var quadratic = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2));
            Set(zone, "_snapCurve", quadratic);
            int completed = 0; zone.OnSnapCompleted += _ => completed++;
            Check(zone.TrySnapObject(a), "单点接受"); Call(zone, "Advance", .5f);
            Check(Near(a.transform.position, Vector3.right * .125f), "非线性原曲线在1/4时间给出1/16位移");
            Check(Quaternion.Angle(a.transform.rotation, Quaternion.Euler(0, 5.625f, 0)) < .02f, "旋转沿同一曲线Slerp");
            Check(!zone.TrySnapObject(b) && zone.SnappedObject == a, "忙碌区域不能被第二物体替换");
            var zone2 = New("Zone2").AddComponent<SnapZone>();
            Check(!zone2.TrySnapObject(a), "跨区域互斥所有权");
            Call(zone, "Advance", 3f);
            Check(!zone.IsSnapping && completed == 1 && Near(a.transform.position, target.position), "单点精确落位且完成一次");
            Check(!a.GetComponent<Collider>().enabled, "完成后按配置禁用碰撞体");
            zone.ReleaseObject(); Check(a.GetComponent<Collider>().enabled, "释放恢复原碰撞体状态");

            var p1 = Point("P1", Vector3.right); var p2 = Point("P2", Vector3.right * 3); var p3 = Point("P3", Vector3.right * 4);
            var points = new List<SnapWaypoint> { new() { target = p1 }, new() { target = p2, overrideDuration = true, duration = .4f }, new() { target = p3, overrideDuration = true, duration = 0 } };
            Set(zone, "_snapMode", SnapMode.Sequence); Set(zone, "_snapPoints", points); Set(zone, "_snapDuration", .2f);
            Set(zone, "_snapCurve", AnimationCurve.Linear(0, 0, 1, 1));
            a.transform.position = Vector3.zero;
            var reached = new List<int>(); Action<GameObject, int> record = (_, i) => reached.Add(i); zone.OnPointReached += record;
            Check(zone.TrySnapObject(a), "序列接受"); Call(zone, "Advance", .3f);
            Check(zone.CurrentPointIndex == 1 && Near(a.transform.position, Vector3.right * 1.5f), "低帧率跨段保留余时与单段覆盖时长");
            Check(completed == 1 && reached.Count == 1, "中间点不发整条完成事件");
            p2.position = Vector3.up * 99; points.Clear(); Set(zone, "_snapDuration", 100f);
            Call(zone, "Advance", .31f);
            Check(Near(a.transform.position, p3.position) && string.Join(",", reached) == "0,1,2" && completed == 2, "冻结路径配置、零秒末段、依序回调");
            zone.ReleaseObject(); zone.OnPointReached -= record;
            Check(!zone.TrySnapObject(a) && !zone.IsSnapping && a.GetComponent<Collider>().enabled, "空列表原子拒绝");
            points.Add(new SnapWaypoint()); Check(!zone.TrySnapObject(a), "空节点不跳过且拒绝");
            points[0].target = p1; points[0].overrideDuration = true; points[0].duration = float.NaN;
            Check(!zone.TrySnapObject(a), "非法时长拒绝"); points[0].duration = 1;
            var disabled = a.AddComponent<SphereCollider>(); disabled.enabled = false;
            var rb = a.GetComponent<Rigidbody>(); rb.isKinematic = false; rb.useGravity = true;
            Check(zone.TrySnapObject(a) && rb.isKinematic && !rb.useGravity, "吸附接管物理");
            Call(zone, "Advance", .25f); var stop = a.transform.position; zone.ReleaseObject(); Call(zone, "Advance", 1f);
            Check(Near(a.transform.position, stop) && !rb.isKinematic && rb.useGravity && !disabled.enabled, "中途释放停在当前位置并恢复原物理及禁用状态");
            rb.isKinematic = true; rb.useGravity = false;
            Check(zone.TrySnapObject(a), "取消后允许显式重试"); zone.enabled = false;
            Check(!zone.IsSnapping && !zone.SnappedObject && a.GetComponent<Collider>().enabled, "组件停用清理所有权"); zone.enabled = true;
            Check(zone2.TrySnapObject(a), "停用后另一区域可接管"); zone2.ReleaseObject();

            points[0].duration = 0;
            Action<GameObject, int> releaseAtPoint = (_, _) => zone.ReleaseObject(); zone.OnPointReached += releaseAtPoint;
            int beforeComplete = completed;
            Check(zone.TrySnapObject(a) && !zone.SnappedObject && completed == beforeComplete, "到点回调取消阻止旧完成事件");
            zone.OnPointReached -= releaseAtPoint;
            Action<GameObject> restart = obj => { if (obj == a) { zone.ReleaseObject(); zone.TrySnapObject(b); } };
            zone.OnSnapCompleted += restart;
            Check(zone.TrySnapObject(a) && zone.SnappedObject == b && !zone.IsSnapping, "完成回调可释放并启动下一道具");
            zone.OnSnapCompleted -= restart; zone.ReleaseObject();
            points[0].duration = 1;
            var doomed = Item("Doomed"); Check(zone.TrySnapObject(doomed), "可销毁对象启动"); Object.DestroyImmediate(doomed); Call(zone, "LateUpdate");
            Check(!zone.IsSnapping && !zone.SnappedObject, "对象销毁后退出吸附状态");

            // 实际 SDK 选择/释放事件，验证等待抓取与恢复组件。不是头显手部输入验收。
            var grab = a.AddComponent<Grabbable>(); grab.InjectOptionalRigidbody(rb); grab.InjectOptionalThrowWhenUnselected(false);
            yield return null;
            grab.ProcessPointerEvent(new PointerEvent(700, PointerEventType.Select, new Pose(a.transform.position, a.transform.rotation)));
            Check(a.GetComponent<SnapItem>().IsHeld && !zone.TrySnapObject(a), "Meta选择中等待松手");
            grab.ProcessPointerEvent(new PointerEvent(700, PointerEventType.Unselect, new Pose(a.transform.position, a.transform.rotation)));
            Check(zone.TrySnapObject(a) && !grab.enabled, "Meta松手后暂停抓取驱动");
            zone.ReleaseObject(); Check(grab.enabled, "释放恢复Meta抓取驱动");
            Object.DestroyImmediate(grab);

            // 真实 Unity 物理触发：两个子碰撞体合并到同一个 SnapItem，离开区域仍完成路径。
            zone.transform.position = Vector3.zero;
            var trigger = zone.gameObject.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one;
            Set(zone, "_snapMode", SnapMode.SinglePoint); Set(zone, "_snapTarget", p3); Set(zone, "_snapDuration", .15f);
            a.transform.position = Vector3.left * 3; b.transform.position = Vector3.left * 4;
            a.GetComponent<Collider>().enabled = false;
            var child = New("ChildCollider"); child.transform.SetParent(a.transform, false); child.AddComponent<BoxCollider>().size = Vector3.one * .05f;
            var child2 = New("ChildCollider2"); child2.transform.SetParent(a.transform, false); child2.AddComponent<BoxCollider>().size = Vector3.one * .04f;
            double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < .08) yield return null;
            a.transform.position = Vector3.zero; Physics.SyncTransforms();
            start = Time.realtimeSinceStartupAsDouble; beforeComplete = completed;
            while (completed == beforeComplete && Time.realtimeSinceStartupAsDouble - start < 3) yield return null;
            Check(zone.SnappedObject == a && !zone.IsSnapping && completed == beforeComplete + 1 && Near(a.transform.position, p3.position), "真实复合碰撞体吸附根物体且离区不取消");
            Check(child.transform.localPosition == Vector3.zero, "子节点局部位置保持");
            zone.ReleaseObject();
            Set(zone, "_snapTarget", zone.transform); Set(zone, "_snapRotationTarget", zone.transform); Set(zone, "_snapDuration", 0f);
            a.transform.position = Vector3.zero; Physics.SyncTransforms();
            start = Time.realtimeSinceStartupAsDouble;
            while (!zone.SnappedObject && Time.realtimeSinceStartupAsDouble - start < 3) yield return null;
            Check(zone.SnappedObject == a, "可重新进入区域吸附");
            zone.ReleaseObject(); beforeComplete = completed;
            start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < .15) yield return null;
            Check(!zone.SnappedObject && completed == beforeComplete, "区域内释放后不自动无限重吸附");
        }

        [MenuItem("Tools/NarutoVR/交互/生成吸附序列示例预制体")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式生成示例");
            const string path = "Assets/Prefabs/Interaction/SnapSequenceDemo.prefab";
            if (File.Exists(path)) { Debug.Log("示例已存在，保留用户修改：" + path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("SnapSequenceDemo"); SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var zoneObject = new GameObject("吸附区域"); zoneObject.transform.SetParent(root.transform, false);
                var zone = zoneObject.AddComponent<SnapZone>(); var trigger = zoneObject.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one * .3f;
                var list = new List<SnapWaypoint>();
                for (int i = 0; i < 3; i++)
                {
                    var point = new GameObject("路径点_" + (i + 1)); point.transform.SetParent(root.transform, false);
                    point.transform.localPosition = new Vector3(i * .35f, i == 1 ? .2f : 0, 0);
                    point.transform.localRotation = Quaternion.Euler(0, i * 45, 0);
                    list.Add(new SnapWaypoint { target = point.transform, overrideDuration = i == 1, duration = 1 });
                }
                Set(zone, "_snapMode", SnapMode.Sequence); Set(zone, "_snapPoints", list);
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = "示例道具_挂抓取组件或在Inspector播放";
                item.transform.SetParent(root.transform, false); item.transform.localPosition = Vector3.left * .5f; item.transform.localScale = Vector3.one * .08f;
                item.AddComponent<SnapItem>(); var rb = item.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("已创建吸附示例：" + path);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
