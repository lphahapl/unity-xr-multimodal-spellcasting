using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class Chapter02BasketBChecks
    {
        const string Key = "Ch02.BControl.QA";
        static string Out => Environment.GetEnvironmentVariable("CH02_BASKET_QA_OUT");
        static bool checking;
        static double deadline;
        static string mode;
        static readonly List<string> log = new();
        static readonly MethodInfo walk = typeof(RpgLocomotionController).GetMethod("WalkStep", BindingFlags.Instance | BindingFlags.NonPublic);
        static Chapter02BasketBChecks() => EditorApplication.update += Update;
        public static void Ride() => Begin("ride");
        public static void Interrupted() => Begin("interrupted");
        public static void Cold() => Begin("cold");
        public static void Legacy() => Begin("legacy");
        public static void Early() => Begin("early");

        static void Begin(string run)
        {
            if (!Application.isBatchMode || string.IsNullOrEmpty(Out)) throw new InvalidOperationException("CH02_BASKET_QA_OUT missing");
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene(Chapter02BasketBInstaller.ScenePath);
            var player = One<LinearRpgController>();
            player.autoLoad = true;
            player.saveFileName = "ch02-basket-b-test-" + Guid.NewGuid().ToString("N") + ".json";
            var destination = Path.Combine(Application.persistentDataPath, player.saveFileName);
            var file = run switch { "early" => "hub.json", "cold" => "b-valley.json", "legacy" => "b-legacy-complete.json", _ => "seated.json" };
            Directory.CreateDirectory(Application.persistentDataPath);
            File.Copy(Path.Combine(Out, file), destination);
            SessionState.SetString(Key + ".path", destination);
            SessionState.SetString(Key, run);
            EditorApplication.EnterPlaymode();
        }

        static async void Update()
        {
            if (checking && EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException("B Play watchdog")); return; }
            if (!Application.isPlaying || checking) return;
            mode = SessionState.GetString(Key, "");
            if (mode.Length == 0) return;
            SessionState.EraseString(Key);
            checking = true;
            deadline = EditorApplication.timeSinceStartup + 180;
            try { await Run(); Finish(null); }
            catch (Exception e) { Finish(e); }
        }

        static void Finish(Exception error)
        {
            if (!checking) return;
            checking = false;
            log.Add(error == null ? "PASS " + mode : "FAIL " + error);
            File.WriteAllLines(Path.Combine(Out, "b-" + mode + ".txt"), log);
            var file = SessionState.GetString(Key + ".path", "");
            if (Path.GetFileName(file).StartsWith("ch02-basket-b-test-") && File.Exists(file)) File.Delete(file);
            SessionState.EraseString(Key + ".path");
            EditorApplication.Exit(error == null ? 0 : 1);
        }

        static Transform[] Nodes() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        static T One<T>() where T : Component => Nodes().Select(t => t.GetComponent<T>()).Single(c => c);
        static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            log.Add(label);
            Debug.Log("CH02_B_QA " + label);
        }
        static async Task Until(Func<bool> predicate, string label)
        {
            var end = EditorApplication.timeSinceStartup + 30;
            while (!predicate() && EditorApplication.timeSinceStartup < end) await Awaitable.NextFrameAsync();
            Check(predicate(), label);
        }
        static async Task Walk(RpgLocomotionController loc, Vector3 target, string label, Func<bool> stop = null)
        {
            float y = loc.PlayerFloorPosition.y, meters = 0;
            int stalled = 0;
            for (int i = 0; i < 900; i++)
            {
                var feet = loc.PlayerFloorPosition;
                var delta = Vector3.ProjectOnPlane(target - feet, Vector3.up);
                if (stop?.Invoke() == true || delta.magnitude < .045f) break;
                if (!(bool)walk.Invoke(loc, new object[] { Vector3.ClampMagnitude(delta, .07f), "B 路线复验" }))
                {
                    var next = feet + Vector3.ClampMagnitude(delta, .07f);
                    var nearby = Physics.OverlapCapsule(next + Vector3.up * (loc.bodyRadius + .015f), next + Vector3.up * 1.55f, loc.bodyRadius + .04f, ~0, QueryTriggerInteraction.Ignore);
                    throw new InvalidOperationException(label + " 受阻于 " + feet.ToString("F3") + " -> " + target + ": " + loc.Status + " (实体=" + string.Join(",", nearby.Select(c=>c.name+"@"+c.ClosestPoint(next+Vector3.up*.9f).ToString("F2"))) + ")");
                }
                var step = loc.PlayerFloorPosition - feet;
                meters += step.magnitude;
                stalled = Vector3.Dot(step, delta.normalized) < .001f ? stalled + 1 : 0;
                if (stalled > 5 || Mathf.Abs(loc.PlayerFloorPosition.y - y) > .27f) throw new InvalidOperationException(label + " 走离路面或受阻 " + loc.PlayerFloorPosition.ToString("F3"));
                if (stop?.Invoke() == true) break;
                if (i % 12 == 11) await Awaitable.NextFrameAsync();
            }
            Check(stop?.Invoke() == true || Vector3.ProjectOnPlane(target - loc.PlayerFloorPosition, Vector3.up).magnitude < .06f,
                label + " " + meters.ToString("F2") + "m，脚底 " + loc.PlayerFloorPosition.ToString("F3"));
        }

        static void Hand(Chapter02BasketBControl b, bool closed, Vector3 world, bool valid = true)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var sample = new PlayerHandObservation { rightValid = valid, rightObserved = now, rightTip = world,
                pinchValid = valid, systemPinch = true, systemPressed = closed, pinchDistance = closed ? .015f : .06f };
            b.Step(sample, now);
        }

        static Vector3 Stroke(Chapter02BasketBControl b)
        {
            Vector3 pivot = new Vector3(-.11f,.52f,0), grip = b.leverGrip.localPosition;
            Vector3 end = pivot + Quaternion.AngleAxis(34f,Vector3.forward) * (grip - pivot);
            return b.leverMesh.transform.TransformVector(end - grip);
        }

        static void Pull(Chapter02BasketBControl b)
        {
            var grip = b.GripPosition;
            Hand(b, false, grip);
            Hand(b, true, grip);
            Hand(b, true, grip + Stroke(b) * .5f);
            Hand(b, true, grip + Stroke(b));
        }

        static int[] Equipment(LinearRpgController p)
        {
            var visuals = Nodes().Select(t => t.GetComponent<HandVisual>()).Where(v => v).ToArray();
            var source = p.handTracking.xrHandsSource;
            var ids = new List<int>();
            Check(source && source.IsConfigured && visuals.Length == 2, "原玩家 Meta 双手输入及两个可视手均在");
            foreach (bool left in new[] { true, false })
            {
                var visual = visuals.Single(v => ReferenceEquals(v.Hand, left ? source.leftHand : source.rightHand));
                var skin = (SkinnedMeshRenderer)new SerializedObject(visual).FindProperty("_openXRSkinnedMeshRenderer").objectReferenceValue;
                string side = left ? "Left" : "Right";
                Check(visual.isActiveAndEnabled && skin && skin.sharedMesh &&
                    AssetDatabase.GetAssetPath(skin.sharedMesh) == "Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXR" + side + "Hand.fbx", "官方 " + side + " 网格及其手部输入引用");
                ids.Add(visual.GetInstanceID());
            }
            var eq = One<Chapter02BridgeEquipment>();
            var ration = One<TravelRationInteraction>();
            var bag = One<ChapterRationSupply>();
            var belt = One<ToolBeltInteraction>();
            Check(eq.player == p && eq.equipment.pen.Dock == eq.equipment.pen.home && ration.player == p &&
                belt.Initialised && ration.sharedBelt == belt && ration.mouth && ration.mouth.transform.parent == p.locomotion.head && !bag.HasPouch,
                "原笔入套、原工具带和嘴区一致，未自动发放干粮");
            ids.AddRange(new[] { eq.equipment.pen.GetInstanceID(), belt.GetInstanceID(), ration.GetInstanceID() });
            return ids.ToArray();
        }

        static SaveEnvelope Disk(LinearRpgController p) => JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(p.SavePath));
        static string Node(SaveEnvelope save) => save.progress.checkpointId;
        static bool SameWorld(WorkshopTenonObjective t) =>
            t.IsSeated && !t.tenon.enabled && Vector3.Distance(t.socket.bottom.position,t.tenon.transform.position) < .003f &&
            t.consoleReady.activeInHierarchy && !t.consoleFault.activeSelf && t.poweredCircuits.All(o => o.activeInHierarchy);

        static async Task Run()
        {
            var b = One<Chapter02BasketBControl>();
            var p = b.player;
            var m = b.motion;
            var loc = p.locomotion;
            var valley = p.chapter.FinalCheckpoint;
            var lip = Nodes().Single(t => t.name == "B 中层篮口").position;
            await Until(() => p.CanAct && (p.CampaignComplete || mode == "early" || b.arrival.IsRunning), "真实章节会话就绪");
            await Until(() => One<ToolBeltInteraction>().Initialised, "工具带初始化完成");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            if (mode == "early")
            {
                Check(m.AtMiddle && !b.tenon.IsSeated, "中层抵达④、主榫尚未入座");
                var lipBefore = m.basketB.position;
                await Walk(loc, new Vector3(41.6f,0,12.5f), "缺榫时走至东转向位");
                await Walk(loc, new Vector3(49.6f,0,20.4f), "缺榫时步入东栈道");
                await Walk(loc, lipBefore + Vector3.left * 1.3f, "缺榫时步行到 B 外");
                await Walk(loc, b.GripPosition + Vector3.forward * .66f, "站在可触及的分控杆旁");
                Pull(b);
                Check(!b.DepartureArmed && !b.arrival.IsRunning, "缺榫时在原铜杆处完成捏拉，分控保持断开");
                await Walk(loc, new Vector3(b.leverGrip.position.x, lipBefore.y, lipBefore.z), "退到栈道中线");
                await Walk(loc, lipBefore + Vector3.left * .38f, "走到 B 门口");
                await Walk(loc, lipBefore + Vector3.right * .6f, "缺榫时登篮", () => m.PlayerInB);
                Check(m.PlayerInB && !m.TryBeginB() && !m.IsMoving && !b.arrival.IsRunning, "缺榫时已在中层篮内仍无法开程");
                return;
            }
            Check(SameWorld(b.tenon), "同一主榫归座，两路分控已亮");
            if (mode == "cold" || mode == "legacy")
            {
                Check(p.CampaignComplete && m.AtBottom && !m.IsMoving && !m.PlayerInB &&
                    Vector3.Distance(loc.PlayerFloorPosition, valley.floorPosition) < .09f &&
                    b.arrival.IsRunning == false && !b.DepartureArmed,
                    "冷读完章落在 B 谷底静态地面，吊篮停稳，主榫与完章状态保留");
                Check(p.SaveCheckpoint() && Node(Disk(p)) == Chapter02BasketBControl.ValleyId, "完章重存使用谷底 ID");
                Equipment(p);
                return;
            }
            Check(!p.CampaignComplete && b.arrival.IsRunning && m.AtMiddle && !m.PlayerInB &&
                p.StageAt(p.StageIndex).stageId == Chapter02BasketBControl.StageId && valley.nodeId == Chapter02BasketBControl.ValleyId,
                "插榫档回中控，B 中层待命，谷底检查点已配置");
            var equipment = Equipment(p);
            var stone = m.ballast.slot.GetComponentsInChildren<BallastStone>().Single();
            await Walk(loc, new Vector3(41.6f,0,12.5f), "④ 向东转向");
            await Walk(loc, new Vector3(49.6f,0,20.4f), "东接驳栈道西端");
            await Walk(loc, lip + Vector3.left * .55f, "循东栈道到 B 篮门外");
            await Walk(loc, lip + Vector3.right * .5f, "先登 B，未拉杆");
            await Awaitable.NextFrameAsync();
            Check(m.PlayerInB && m.AtMiddle && !m.IsMoving, "未拉杆独自上篮不启动");
            await Walk(loc, lip + Vector3.left * .55f, "退回 B 门外");
            Vector3 station = b.GripPosition + Vector3.forward * .66f;
            await Walk(loc, station, "可自然伸手的分控杆旁");
            Check(!m.PlayerInB && Vector3.ProjectOnPlane(loc.PlayerFloorPosition - b.GripPosition, Vector3.up).magnitude < .72f, "站在原铜杆外侧，篮内无人");
            var grip = b.GripPosition;
            var shoulder = loc.PlayerFloorPosition + Vector3.up * 1.35f;
            var line = grip - shoulder;
            var blocked = Physics.RaycastAll(shoulder, line.normalized, line.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !h.collider.transform.IsChildOf(loc.rigRoot) && !h.collider.transform.IsChildOf(b.transform));
            Check(!blocked, "从栈道一侧伸手到铜柄有实体净空");
            var verts = b.leverMesh.sharedMesh.vertices;
            int rodIndex = b.leverMesh.sharedMesh.GetIndices(3)[0];
            int baseIndex = b.leverMesh.sharedMesh.GetIndices(0)[0];
            Hand(b, true, grip);
            Hand(b, true, grip + Stroke(b));
            Check(!b.DepartureArmed, "不能持握姿势靠近而误拉");
            Check(p.Input.TryAcquireInteraction(m, out long held), "用现有物品占用接口占右手");
            Pull(b);
            Check(!b.DepartureArmed && p.Input.OwnsInteraction(m,held), "操作杆不会抢占持有物的右手");
            p.Input.ReleaseInteraction(m,held);
            Hand(b, false, grip);
            Hand(b, true, grip);
            Hand(b, true, grip + Stroke(b) * .3f);
            Hand(b, true, grip, false);
            Check(!b.DepartureArmed && !p.Input.InteractionOwnsHand &&
                Vector3.Distance(b.GripPosition,grip) < .003f, "追踪丢失松开输入且短行程回弹");
            Pull(b);
            var thrown = b.leverMesh.sharedMesh.vertices;
            Check(b.DepartureArmed && !p.Input.InteractionOwnsHand && Vector3.Distance(verts[rodIndex],thrown[rodIndex]) > .17f &&
                Vector3.Distance(verts[baseIndex],thrown[baseIndex]) < .0001f, "真实铜手柄带动上部两段网格，底座固定，释放右手");
            await Awaitable.NextFrameAsync();
            Check(m.AtMiddle && !m.IsMoving && !p.CampaignComplete, "空篮拉杆后在中层等待乘员");
            await Walk(loc, new Vector3(b.leverGrip.position.x, lip.y, lip.z), "退至东栈道中线");
            await Walk(loc, lip + Vector3.left * .38f, "回到 B 门口");
            await Walk(loc, lip + Vector3.right * .6f, "从原篮口实际登篮", () => m.PlayerInB);
            int arrivals = 0;
            m.BArrived += () => arrivals++;
            var rigBefore = loc.rigRoot.rotation;
            var headBefore = loc.head.rotation;
            var offset = loc.PlayerFloorPosition - m.basketB.position;
            var from = m.basketB.position;
            double startTime = Time.timeAsDouble;
            await Until(() => m.IsMoving, "乘员到位后唯一 B 开程");
            if (mode == "interrupted")
            {
                await Until(() => m.basketB.position.y < from.y - 6, "B 已离中层至少 6m");
                Check(p.SaveCheckpoint() && Node(Disk(p)) == "CH02_HUB" && !p.CampaignComplete, "行程中存档保留中控安全点");
                Check(p.RestoreCheckpoint(), "使用正式读档入口中断垂降");
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                Check(m.AtMiddle && !m.IsMoving && !m.PlayerInB && !b.DepartureArmed && !p.CampaignComplete &&
                    arrivals == 0 && !m.comfortCurtain.activeSelf && !p.Input.PassengerTransitActive &&
                    Vector3.Distance(loc.PlayerFloorPosition,p.CheckpointAt(p.StageIndex).floorPosition) < .1f &&
                    Vector3.Distance(m.basketB.position,from) < .02f && SameWorld(b.tenon),
                    "读档清除旧行程/定目帘和占用，玩家回中控，B 回中层，主榫仍归座");
                return;
            }
            bool opaque = false;
            float maxDrift = 0, maxTurn = 0;
            Vector3 worstOffset = Vector3.zero;
            var curtain = m.comfortCurtain.GetComponent<CanvasGroup>();
            while (!m.AtBottom)
            {
                if (p.CampaignComplete || arrivals != 0) throw new InvalidOperationException("停稳前完章");
                opaque |= curtain.gameObject.activeSelf && curtain.alpha > .95f;
                var relativeError = loc.PlayerFloorPosition - m.basketB.position - offset;
                if (relativeError.magnitude > maxDrift) { worstOffset = relativeError; maxDrift = relativeError.magnitude; }
                maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(rigBefore,loc.rigRoot.rotation), Quaternion.Angle(headBefore,loc.head.rotation));
                await Awaitable.NextFrameAsync();
            }
            double duration = Time.timeAsDouble - startTime;
            await Until(() => p.CampaignComplete, "B 停稳后才由原章节目标完成");
            Check(opaque && !curtain.gameObject.activeSelf && arrivals == 1 && m.PlayerInB && !p.Input.PassengerTransitActive &&
                maxTurn < .12f && maxDrift < .04f && Mathf.Abs(m.basketB.position.y - from.y + 80f) < .04f &&
                duration >= 5.9 && duration < 7.4, $"实篮垂降 80m，{duration:F2}s，定目帘与乘员锁恢复，漂移 {maxDrift:F4}m，强制转角 {maxTurn:F4}°，分轴误差 {worstOffset:F5}，乘员 {m.PlayerInB}，入站 {arrivals}，中段遮景 {opaque}");
            Check(stone && m.ballast.slot.GetComponentsInChildren<BallastStone>().Single() == stone &&
                equipment.SequenceEqual(Equipment(p)) && SameWorld(b.tenon), "镇石、主榫、笔、双手和干粮均保留原对象");
            await Walk(loc, valley.floorPosition, "停稳后步出 B 到谷底静态地板");
            Check(!m.PlayerInB && !m.TryBeginB() && arrivals == 1, "下篮可继续行走，反复操作不再开程");
            var sign = Nodes().Single(t => t.name == "谷底方向提示");
            var approach = sign.position + Vector3.forward * 1.7f;
            await Walk(loc, approach, "沿谷底地面走向首个方向标");
            Check(p.SaveCheckpoint(), "正式入口保存完章");
            var saved = Disk(p);
            Check(Node(saved) == Chapter02BasketBControl.ValleyId &&
                saved.progress.completedStageIds.Last() == Chapter02BasketBControl.StageId &&
                saved.progress.claimedRewardIds.Count(s => s == "reward." + Chapter02BasketBControl.StageId) == 1,
                "谷底静态检查点及 B 完成记录写盘一次");
            File.Copy(p.SavePath, Path.Combine(Out, "b-valley.json"), true);
            saved.progress.checkpointId = "CH02_HUB";
            File.WriteAllText(Path.Combine(Out, "b-legacy-complete.json"), JsonUtility.ToJson(saved, true));
        }
    }
}
