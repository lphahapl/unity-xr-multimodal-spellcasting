using System;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Editor unit/physics checks. No hand-tracking hardware is simulated as real tracking.</summary>
    public static class RpgLocomotionChecks
    {
        static readonly Vector3 Left = new Vector3(-0.3f, 1.2f, 0.4f);
        static readonly Vector3 Right = new Vector3(0.3f, 1.2f, 0.4f);

        [MenuItem("Tools/NarutoVR/线性RPG/运行导航闪避检查")]
        public static void Menu() => Debug.Log("[线性RPG] 导航闪避检查通过 " + Run() + " 项；不代表真机验证。");

        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请在编辑模式运行导航闪避检查。");
            int passed = 0;
            CheckLowHands(); passed++;
            CheckCancelOnlyNewPairs(); passed++;
            CheckForwardOwnership(); passed++;
            CheckSideAndBackWithHeldSpell(); passed++;
            CheckDodgeNavigationGate(); passed++;
            CheckDwellAndNeutral(); passed++;
            CheckHeadingLock(); passed++;
            CheckBadSamples(); passed++;
            CheckTrackingLoss(); passed++;
            CheckSwipeBounds(); passed++;
            CheckTranslatedAnchors(); passed++;
            passed += CheckPhysics();
            return passed;
        }

        static NavigationHandInput Frame(double stamp, Vector3? left = null, Vector3? right = null)
        {
            return new NavigationHandInput
            {
                leftObserved = stamp, rightObserved = stamp,
                leftValid = true, rightValid = true,
                leftIndex = left ?? Left, leftWrist = (left ?? Left) - Vector3.forward * 0.1f,
                rightWrist = right ?? Right, leftPointing = true, rightOpen = true
            };
        }

        static void CheckLowHands()
        {
            var state = new RpgGestureState();
            int cancellations = 0;
            for (int i = 0; i < 15; i++)
            {
                double t = i * 0.05;
                var frame = Frame(t); frame.bothHandsLow = true; frame.rightValid = false;
                if (state.Process(frame, Vector3.forward, false, false, t).cancel) cancellations++;
            }
            Require(cancellations == 0, "缺失手部数据不能视为双手低位。");
            for (int i = 15; i < 40; i++)
            {
                double t = i * 0.05;
                var frame = Frame(t); frame.bothHandsLow = true;
                if (state.Process(frame, Vector3.forward, false, false, t).cancel) cancellations++;
            }
            Require(cancellations == 1, "可靠低位保持只能发出一次取消。");
            var invalid = Frame(2); invalid.leftValid = false; invalid.bothHandsLow = true;
            state.Process(invalid, Vector3.forward, false, false, 2);
            for (int i = 41; i < 56; i++)
            {
                double t = i * 0.05; var frame = Frame(t); frame.bothHandsLow = true;
                if (state.Process(frame, Vector3.forward, false, false, t).cancel) cancellations++;
            }
            Require(cancellations == 1, "失踪后低位重获不能在未抬手时重复取消。");
            state.Process(Frame(2.8), Vector3.forward, false, false, 2.8);
            for (int i = 57; i < 72; i++)
            {
                double t = i * 0.05; var frame = Frame(t); frame.bothHandsLow = true;
                if (state.Process(frame, Vector3.forward, false, false, t).cancel) cancellations++;
            }
            Require(cancellations == 2, "抬手后再次可靠低位应能重新取消。");
        }

        static void CheckCancelOnlyNewPairs()
        {
            var state = new RpgGestureState();
            var frame = Frame(1); frame.bothHandsLow = true;
            state.Process(frame, Vector3.forward, false, false, 1);
            for (int i = 1; i < 20; i++)
            {
                Require(!state.Process(frame, Vector3.forward, false, false, 1 + i * 0.05).cancel,
                    "重复或陈旧样本不能累计低位时间。");
            }
            for (int i = 0; i < 5; i++)
            {
                double t = 2 + i * 0.05; frame = Frame(t); frame.bothHandsLow = true;
                Require(!state.Process(frame, Vector3.forward, false, false, t).cancel,
                    "失踪间隔不能计入低位保持。");
            }
        }

        static void CheckForwardOwnership()
        {
            var state = RightOnlyState();
            state.Process(Frame(0), Vector3.forward, true, false, 0);
            state.Process(Frame(0.03, right: Right + Vector3.forward * 0.04f), Vector3.forward, true, false, 0.03);
            for (int i = 2; i < 12; i++)
            {
                double t = i * 0.03;
                var intent = state.Process(Frame(t, right: Right + Vector3.forward * 0.15f), Vector3.forward,
                    false, false, t);
                Require(intent.dodgeDirection == Vector3.zero, "持球前推在发射后不能退化为前闪。");
            }
            state.Process(Frame(0.4), Vector3.forward, false, false, 0.4);
            state.Process(Frame(0.45, right: Right + Vector3.right * 0.04f), Vector3.forward, false, false, 0.45);
            var afterNeutral = state.Process(Frame(0.53, right: Right + Vector3.right * 0.12f),
                Vector3.forward, false, false, 0.53);
            Require(afterNeutral.dodgeDirection == Vector3.right, "回中立后应允许新的侧闪。");

            state = RightOnlyState();
            state.Process(Frame(1), Vector3.forward, true, false, 1);
            state.Process(Frame(1.05, right: Right + Vector3.forward * 0.025f), Vector3.forward, false, false, 1.05);
            Require(state.Process(Frame(1.13, right: Right + Vector3.forward * 0.13f), Vector3.forward,
                false, false, 1.13).dodgeDirection == Vector3.zero, "动作开始时的持球所有权必须锁存。");

            state = RightOnlyState();
            state.Process(Frame(2), Vector3.forward, false, false, 2);
            state.Process(Frame(2), Vector3.forward, true, false, 2.01);
            var closed = Frame(2.05, right: Right + Vector3.forward * 0.02f); closed.rightOpen = false;
            state.Process(closed, Vector3.forward, false, false, 2.05);
            Require(state.Process(Frame(2.13, right: Right + Vector3.forward * 0.13f), Vector3.forward,
                false, false, 2.13).dodgeDirection == Vector3.zero,
                "手样本之间的持球与发射姿势变化也不能丢失前推所有权。");
        }

        static void CheckSideAndBackWithHeldSpell()
        {
            foreach (Vector3 direction in new[] { Vector3.left, Vector3.right, Vector3.back })
            {
                var state = RightOnlyState();
                state.Process(Frame(0), Vector3.forward, true, false, 0);
                state.Process(Frame(0.05, right: Right + direction * 0.04f), Vector3.forward, true, false, 0.05);
                var intent = state.Process(Frame(0.13, right: Right + direction * 0.13f), Vector3.forward, true, false, 0.13);
                Require(intent.dodgeDirection == direction, "持球应保留左、右、后短划闪避。");
            }
        }

        // These fixtures isolate right-hand trajectory rules from the separately checked default navigation gate.
        static RpgGestureState RightOnlyState() => new RpgGestureState(new RpgGestureSettings { requireNavigationForDodge = false });

        static void CheckDodgeNavigationGate()
        {
            var state = new RpgGestureState(new RpgGestureSettings { requireNavigationForDodge = true });
            var frame = Frame(0); frame.leftPointing = false;
            state.Process(frame, Vector3.forward, false, false, 0);
            frame = Frame(0.05, right: Right + Vector3.right * 0.04f); frame.leftPointing = false;
            state.Process(frame, Vector3.forward, false, false, 0.05);
            frame = Frame(0.13, right: Right + Vector3.right * 0.13f); frame.leftPointing = false;
            Require(state.Process(frame, Vector3.forward, false, false, 0.13).dodgeDirection == Vector3.zero,
                "显式启用旧导航门控时，未准备不能闪避。");

            state = PreparedState();
            frame = Frame(0.4, right: Right + Vector3.right * 0.04f); frame.rightOpen = false;
            state.Process(frame, Vector3.forward, true, false, 0.4);
            frame = Frame(0.48, right: Right + Vector3.right * 0.13f); frame.rightOpen = false;
            Require(state.Process(frame, Vector3.forward, true, false, 0.48).dodgeDirection == Vector3.zero,
                "即使持球且已导航准备，未张掌也不能侧闪。");
            state.Process(Frame(0.53), Vector3.forward, true, false, 0.53);
            state.Process(Frame(0.58, right: Right + Vector3.right * 0.04f), Vector3.forward, true, false, 0.58);
            Require(state.Process(Frame(0.66, right: Right + Vector3.right * 0.13f), Vector3.forward, true, false, 0.66)
                .dodgeDirection == Vector3.right, "导航准备后，张开的右掌新短划应允许持球侧闪。");
        }

        static RpgGestureState PreparedState()
        {
            var state = new RpgGestureState(new RpgGestureSettings { dwellSeconds = .35f });
            for (int i = 0; i <= 7; i++)
            {
                double t = i * 0.05;
                state.Process(Frame(t), Vector3.forward, false, false, t);
            }
            Require(state.NavigationActive, "稳定指向后应进入导航。");
            return state;
        }

        static void CheckDwellAndNeutral()
        {
            var state = PreparedState(); int steps = 0;
            for (int i = 8; i <= 25; i++)
            {
                double t = i * 0.05;
                if (state.Process(Frame(t, Left + Vector3.forward * 0.08f), Vector3.forward, false, false, t)
                    .navigationStep != Vector2.zero) steps++;
            }
            Require(steps == 1, "一次驻留只能触发一次，保持指向不能连发。");
            state.Process(Frame(1.3), Vector3.forward, false, false, 1.3);
            for (int i = 27; i <= 38; i++)
            {
                double t = i * 0.05;
                if (state.Process(Frame(t, Left + Vector3.forward * 0.08f), Vector3.forward, false, false, t)
                    .navigationStep != Vector2.zero) steps++;
            }
            Require(steps == 2, "回中立后才能再次触发驻留。");
            var down = Frame(2); down.leftPointing = false;
            state.Process(down, Vector3.forward, false, false, 2);
            Require(!state.NavigationActive, "放下手应退出导航。");
        }

        static void CheckHeadingLock()
        {
            var state = PreparedState(); Vector2 step = Vector2.zero;
            for (int i = 8; i <= 20; i++)
            {
                double t = i * 0.05;
                var intent = state.Process(Frame(t, Left + Vector3.forward * 0.08f), Vector3.right, false, false, t);
                if (intent.navigationStep != Vector2.zero) step = intent.navigationStep;
            }
            Require(step == Vector2.up, "导航开始后转头不能改变已锁定的方向基准。");
            Require(RpgGestureState.HorizontalForward(Vector3.up, Vector3.left) == Vector3.left,
                "抬头导致水平朝向退化时必须使用稳定后备朝向。");
            state.InterruptDwell();
            Require(state.Selection == Vector2.zero && state.DwellProgress == 0, "闪避必须清空未完成驻留。");
        }

        static void CheckBadSamples()
        {
            var state = PreparedState(); var frame = Frame(0.4);
            frame.leftIndex = new Vector3(float.NaN, 0, 0);
            state.Process(frame, Vector3.forward, false, false, 0.4);
            Require(!state.NavigationActive, "非有限手部数据必须退出导航。");
            state = PreparedState();
            state.Process(Frame(0.35), Vector3.forward, false, false, 0.6);
            Require(!state.NavigationActive, "陈旧追踪数据必须退出导航。");
            state = PreparedState();
            frame = Frame(1); frame.bothHandsLow = true;
            Require(!state.Process(frame, Vector3.forward, false, false, 0.4).cancel, "未来时间戳必须拒绝。");
            Require(!state.NavigationActive, "未来样本不能保持导航。");
        }

        static void CheckTrackingLoss()
        {
            var state = RightOnlyState();
            state.Process(Frame(0), Vector3.forward, false, false, 0);
            state.Process(Frame(0.05, right: Right + Vector3.right * 0.04f), Vector3.forward, false, false, 0.05);
            var missing = Frame(0.1); missing.rightValid = false;
            state.Process(missing, Vector3.forward, false, false, 0.1);
            Require(state.Process(Frame(0.15, right: Right + Vector3.right * 0.2f), Vector3.forward,
                false, false, 0.15).dodgeDirection == Vector3.zero, "追踪重获不能把缺失期间位移当作短划。");
        }

        static void CheckSwipeBounds()
        {
            var state = RightOnlyState();
            state.Process(Frame(0), Vector3.forward, false, false, 0);
            Require(state.Process(Frame(0.1, right: Right + Vector3.right * 0.4f), Vector3.forward,
                false, false, 0.1).dodgeDirection == Vector3.zero, "超限追踪跳变不能触发闪避。");
            state = RightOnlyState();
            for (int i = 0; i <= 30; i++)
            {
                double t = i * 0.05;
                Require(state.Process(Frame(t, right: Right + Vector3.right * (i * 0.004f)), Vector3.forward,
                    false, false, t).dodgeDirection == Vector3.zero, "缓慢漂移不能累计为短划。");
            }
        }

        static void CheckTranslatedAnchors()
        {
            var state = PreparedState(); Vector3 delta = new Vector3(2, 0, 1);
            state.TranslateAnchors(delta);
            var intent = state.Process(Frame(0.4, Left + delta, Right + delta), Vector3.forward, false, false, 0.4);
            Require(intent.navigationStep == Vector2.zero && intent.dodgeDirection == Vector3.zero &&
                state.Selection == Vector2.zero, "Rig位移后的追踪原点补偿不能造成新的手势。");
        }

        static int CheckPhysics()
        {
            // Temporary colliders are placed far from authored content on a dedicated query mask.
            Scene previousScene = SceneManager.GetActiveScene();
            Scene temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporaryScene);
            var world = new GameObject("RpgLocomotionChecks_Temporary") { hideFlags = HideFlags.HideAndDontSave };
            const int layer = 30;
            Vector3 origin = new Vector3(10000, 0, 10000);
            var registeredNodes = new System.Collections.Generic.List<NavigationNode>();
            RpgLocomotionController diagnosticController = null;
            void Require(bool condition, string message)
            {
                string status = diagnosticController ? diagnosticController.Status : "尚未创建";
                string node = diagnosticController ? diagnosticController.CurrentNodeId : "无";
                RpgLocomotionChecks.Require(condition, message + " [Status=" + status + "; CurrentNode=" + node +
                    "; 注册节点=" + NavigationNode.ActiveNodes.Count + "]");
            }
            bool Registered(NavigationNode node)
            {
                foreach (NavigationNode active in NavigationNode.ActiveNodes) if (active == node) return true;
                return false;
            }
            NavigationNode Register(GameObject item, string id)
            {
                var node = item.AddComponent<NavigationNode>(); node.nodeId = id;
                // EditMode fixtures must explicitly run lifecycle hooks of ordinary runtime MonoBehaviours.
                NavigationNodeFixture.SetRegistered(node,true);
                registeredNodes.Add(node);
                return node;
            }
            try
            {
                GameObject Box(string name, Vector3 position, Vector3 scale)
                {
                    var value = new GameObject(name) { layer = layer, hideFlags = HideFlags.HideAndDontSave };
                    value.transform.SetParent(world.transform, false);
                    value.transform.position = position; value.transform.localScale = scale;
                    value.AddComponent<BoxCollider>(); return value;
                }
                GameObject floorObject = Box("Floor", origin + Vector3.down * 0.1f, new Vector3(8, 0.2f, 8));
                var rig = new GameObject("Rig") { hideFlags = HideFlags.HideAndDontSave };
                rig.transform.SetParent(world.transform, false); rig.transform.position = origin;
                var camera = new GameObject("Head") { hideFlags = HideFlags.HideAndDontSave };
                camera.transform.SetParent(rig.transform, false);
                camera.transform.localPosition = new Vector3(0.2f, 1.6f, 0.15f);
                camera.transform.localRotation = Quaternion.Euler(5, 10, 3);
                var controller = rig.AddComponent<RpgLocomotionController>();
                diagnosticController = controller;
                controller.rigRoot = rig.transform; controller.head = camera.transform;
                controller.floorLayers = 1 << layer; controller.obstacleLayers = 1 << layer;
                Vector3 local = camera.transform.localPosition;
                Quaternion rotation = camera.transform.localRotation;
                Vector3 floorUnderHead = new Vector3(camera.transform.position.x, 0, camera.transform.position.z);
                var blocker = Box("EndBlocked", floorUnderHead + new Vector3(0, 0.75f, 0.8f), new Vector3(0.5f, 1.5f, 0.4f));
                Require(!controller.TryDebugDodge(Vector2.up, 10) && controller.DodgeRemaining == 0,
                    "终点无效不应移动或花费冷却。");
                Require((rig.transform.position - origin).sqrMagnitude < 0.0001f, "失败动作不能偷偷反向移动。");
                Object.DestroyImmediate(blocker);
                blocker = Box("ThinPathBlocker", floorUnderHead + new Vector3(0, 0.75f, 0.4f), new Vector3(0.5f, 1.5f, 0.03f));
                Require(!controller.TryDebugDodge(Vector2.up, 10) && controller.DodgeRemaining == 0,
                    "沿途薄墙必须阻止闪避，失败不花冷却。");
                Object.DestroyImmediate(blocker);
                blocker = Box("StartOverlapped", floorUnderHead + new Vector3(0, 0.75f, 0), new Vector3(0.1f, 1.5f, 0.1f));
                Require(!controller.TryDebugDodge(Vector2.up, 10), "起点重叠必须单独拒绝。");
                Object.DestroyImmediate(blocker);
                blocker = Box("TriggerIsIgnored", floorUnderHead + new Vector3(0, 0.75f, 0.8f), new Vector3(0.5f, 1.5f, 0.4f));
                blocker.GetComponent<BoxCollider>().isTrigger = true;
                var self = new GameObject("SelfCollider") { layer = layer, hideFlags = HideFlags.HideAndDontSave };
                self.transform.SetParent(rig.transform, false);
                self.transform.localPosition = new Vector3(0.2f, 0.75f, 0.15f);
                self.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1, 0.2f);
                Require(controller.TryDebugDodge(Vector2.up, 10), "平地合法闪避应过滤自身和Trigger碰撞体。");
                Require(controller.DodgeRemaining > 1 && !controller.TryDebugDodge(Vector2.up, 10.1), "成功闪避应进入冷却。");
                Require(camera.transform.localPosition == local && camera.transform.localRotation == rotation,
                    "瞬移不能改写头部本地位置或旋转。");
                int passed = 6;

                var aObject = new GameObject("NodeA") { hideFlags = HideFlags.HideAndDontSave };
                aObject.transform.SetParent(world.transform, false); aObject.transform.position = floorUnderHead;
                NavigationNode a = Register(aObject, "checks.a");
                var bObject = new GameObject("NodeB") { hideFlags = HideFlags.HideAndDontSave };
                bObject.transform.SetParent(world.transform, false); bObject.transform.position = floorUnderHead + Vector3.forward * 2;
                NavigationNode b = Register(bObject, "checks.b");
                Require(Registered(a) && Registered(b), "编辑模式创建的活动节点必须显式进入运行时注册表。");
                a.neighbors.Add(b); b.neighbors.Add(a);
                rig.transform.position = origin;
                Physics.SyncTransforms();
                controller.ResetInput();
                Require(controller.TryDebugStep(Vector2.up, 12), "相邻合法预设点应允许前移。");
                Require(controller.OnNode && controller.CurrentNodeId == "checks.b", "实际头部投影应落在目标节点。");
                Require(Vector3.Distance(new Vector3(camera.transform.position.x, 0, camera.transform.position.z), b.floorPosition) < 0.01f,
                    "存在Room-scale偏移时仍应使用头部地面投影对齐节点。");
                Require(!controller.TryDebugStep(Vector2.up, 13), "无前方邻居时不能反向吸回旧点。");
                Require(controller.TryDebugDodge(Vector2.right, 14) && !controller.OnNode, "闪避允许落在预设点外。");
                Require(!controller.TryDebugStep(Vector2.up, 15), "离点后前方无目标时不能吸回旧点。");
                Require(camera.transform.localPosition == local && camera.transform.localRotation == rotation,
                    "节点位移后应继续保留头部偏移与旋转。");
                passed += 4;

                // Checkpoint 1: a missing/death floor must not prevent a safe registered respawn.
                Require(controller.TryDebugDodge(Vector2.down, 20), "检查点测试需要先建立未结束的闪避冷却。");
                for (int i = 0; i < 8; i++)
                {
                    double t = 20.05 + i * 0.05;
                    var frame = Frame(t, camera.transform.position + new Vector3(-0.3f, -0.4f, 0.35f),
                        camera.transform.position + new Vector3(0.3f, -0.4f, 0.35f));
                    frame.rightOpen = false;
                    controller.ProcessInput(frame, false, false, t);
                }
                Require(controller.NavigationActive && controller.DodgeRemaining > 0, "恢复前应确实存在导航状态及未结束的冷却。");
                float checkpointCooldown = controller.DodgeRemaining;
                int restoreMoves = 0;
                controller.Moved += _ => restoreMoves++;
                rig.transform.position = origin + new Vector3(12, 3, 0);
                Physics.SyncTransforms();
                Require(!floorObject.GetComponent<Collider>().Raycast(new Ray(camera.transform.position, Vector3.down), out _, 10),
                    "死亡位置测试必须确实没有脚下地面。");
                Require(controller.TryRestoreCheckpoint(a), "无脚下地面也应能恢复至安全的同场景已注册节点。");
                Require(controller.OnNode && controller.CurrentNodeId == a.nodeId && !controller.NavigationActive &&
                    controller.CandidateNode == null && restoreMoves == 1, "恢复应重置手势、刷新实际节点并发出一次Moved。");
                Require(camera.transform.localPosition == local && camera.transform.localRotation == rotation &&
                    Mathf.Abs(controller.DodgeRemaining - checkpointCooldown) < 0.001f,
                    "检查点恢复必须保留头部本地姿态，并且不消费或清除闪避冷却。");
                passed++;

                // Checkpoint 2: dynamic occupancy is rechecked at the destination.
                Vector3 beforeRejectedRestore = rig.transform.position;
                blocker = Box("CheckpointOccupied", b.floorPosition + Vector3.up * 0.75f, new Vector3(0.5f, 1.5f, 0.5f));
                Require(!controller.TryRestoreCheckpoint(b) && rig.transform.position == beforeRejectedRestore &&
                    Mathf.Abs(controller.DodgeRemaining - checkpointCooldown) < 0.001f,
                    "被动态碰撞体占据的检查点必须拒绝，位置和冷却均保持原值。");
                Object.DestroyImmediate(blocker);
                passed++;

                // Checkpoint 3: a globally registered node in another loaded scene is still forbidden.
                Scene foreignScene = EditorSceneManager.NewPreviewScene();
                NavigationNode foreign = null;
                try
                {
                    var foreignObject = new GameObject("ForeignCheckpoint") { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(foreignObject, foreignScene);
                    foreignObject.transform.position = a.floorPosition;
                    foreign = Register(foreignObject, "checks.foreign");
                    Require(foreign.isActiveAndEnabled && Registered(foreign) && !controller.TryRestoreCheckpoint(foreign) &&
                        rig.transform.position == beforeRejectedRestore, "即便其他场景节点已注册且地面安全，也必须拒绝跨场景恢复。");
                }
                finally
                {
                    if (foreign) NavigationNodeFixture.SetRegistered(foreign,false);
                    SceneManager.SetActiveScene(temporaryScene);
                    EditorSceneManager.ClosePreviewScene(foreignScene);
                }
                passed++;

                // Checkpoint 4: restoring is a safe spawn, so walls and adjacency cannot trap a dead player.
                a.neighbors.Clear(); b.neighbors.Clear();
                blocker = Box("WallBetweenCheckpoints", floorUnderHead + new Vector3(0, 1, 1), new Vector3(4, 2, 0.03f));
                Require(controller.TryRestoreCheckpoint(b) && controller.CurrentNodeId == b.nodeId &&
                    camera.transform.localPosition == local && camera.transform.localRotation == rotation &&
                    Mathf.Abs(controller.DodgeRemaining - checkpointCooldown) < 0.001f,
                    "安全检查点恢复应绕过邻接与沿途墙体限制，且继续保留头部姿态和冷却。");
                Object.DestroyImmediate(blocker);
                passed++;

                // Checkpoint 5: disabling a node unregisters it and makes it unavailable for restore.
                b.enabled = false;
                NavigationNodeFixture.SetRegistered(b,false);
                beforeRejectedRestore = rig.transform.position;
                Require(!Registered(b) && !controller.TryRestoreCheckpoint(b) && rig.transform.position == beforeRejectedRestore,
                    "已禁用并取消注册的节点不能用于检查点恢复。");
                b.enabled = true;
                NavigationNodeFixture.SetRegistered(b,true);
                passed++;

                // Clock 1: keep input timestamps in realtime while a separate game clock is paused.
                var clockRig = new GameObject("PausedClockRig") { hideFlags = HideFlags.HideAndDontSave };
                clockRig.transform.SetParent(world.transform, false); clockRig.transform.position = origin + Vector3.left * 2;
                var clockHead = new GameObject("PausedClockHead") { hideFlags = HideFlags.HideAndDontSave };
                clockHead.transform.SetParent(clockRig.transform, false); clockHead.transform.localPosition = local;
                var clockController = clockRig.AddComponent<RpgLocomotionController>();
                diagnosticController = clockController;
                clockController.rigRoot = clockRig.transform; clockController.head = clockHead.transform;
                clockController.floorLayers = 1 << layer; clockController.obstacleLayers = 1 << layer;
                double gameNow = 100;
                clockController.GameClock = () => gameNow;
                Require(clockController.TryDebugDodge(Vector2.up, 1000) &&
                    Mathf.Abs(clockController.DodgeRemaining - clockController.dodgeCooldown) < 0.001f,
                    "闪避截止时间必须基于GameClock，不能写成realtime now加冷却。");
                void ClockInput(double realtime)
                {
                    var frame = Frame(realtime, clockHead.transform.position + new Vector3(-0.3f, -0.3f, 0.3f),
                        clockHead.transform.position + new Vector3(0.3f, -0.3f, 0.3f));
                    frame.leftPointing = false; frame.rightOpen = false;
                    clockController.ProcessInput(frame, false, true, realtime);
                }
                ClockInput(1001); ClockInput(1050); ClockInput(1100);
                Require(Mathf.Abs(clockController.DodgeRemaining - clockController.dodgeCooldown) < 0.001f &&
                    !clockController.TryDebugDodge(Vector2.up, 2000),
                    "GameClock暂停时即使追踪realtime持续前进，闪避冷却也不得缩短或结束。");
                passed++;

                // Clock 2: resuming advances only game seconds and permits the next dodge at that deadline.
                gameNow = 100.6; ClockInput(2000.1);
                Require(Mathf.Abs(clockController.DodgeRemaining - 0.6f) < 0.002f &&
                    !clockController.TryDebugDodge(Vector2.up, 2000.1), "恢复游戏时钟后只应扣除经过的游戏秒数。");
                gameNow = 101.21; ClockInput(2000.2);
                Require(clockController.DodgeRemaining == 0 && clockController.TryDebugDodge(Vector2.up, 2000.2) &&
                    Mathf.Abs(clockController.DodgeRemaining - clockController.dodgeCooldown) < 0.001f,
                    "仅GameClock到达截止后才能再次闪避，并从同一游戏时钟开启新冷却。");
                passed++;
                return passed;
            }
            finally
            {
                foreach (NavigationNode node in registeredNodes)
                    if (node) NavigationNodeFixture.SetRegistered(node,false);
                Object.DestroyImmediate(world);
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(temporaryScene, true);
                Physics.SyncTransforms();
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[导航闪避检查] " + message);
        }
    }
}
