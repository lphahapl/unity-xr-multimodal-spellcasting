using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using NeuGameMRTools.Runtime.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class SettledPickupRunner
    {
        const string Key = "NarutoVR.SettledGeometryProbe";
        static readonly Stack<IEnumerator> jobs = new();
        static string Folder => Environment.GetEnvironmentVariable("PICKUP_QA");
        static string[] Cases => Environment.GetEnvironmentVariable("PICKUP_CASE").Split('|');
        static string Mode => Cases[SessionState.GetInt(Key+"CaseIndex",0)];
        static string Report => Path.Combine(Folder, Mode + ".txt");
        static double end;
        static readonly List<UnityEngine.XR.XRDisplaySubsystem> displays = new();
        internal static void RequireDesktopIsolation(bool playing)
        {
            var expected = Environment.GetEnvironmentVariable("PICKUP_ISOLATED_PROJECT");
            var runtime = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
            if (string.IsNullOrEmpty(expected) ||
                !string.Equals(Path.GetFullPath(expected), Directory.GetParent(Application.dataPath).FullName,
                               StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(runtime) || !Path.IsPathRooted(runtime) || File.Exists(runtime))
                throw new InvalidOperationException("Use the guarded pickup runner on its isolated project");
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (!xr || xr.InitManagerOnStart || !xr.Manager || xr.Manager.activeLoaders.Count != 0 || xr.Manager.activeLoader)
                throw new InvalidOperationException("Standalone XR configuration permits hardware");
            var inputs = new List<UnityEngine.XR.XRInputSubsystem>(); SubsystemManager.GetSubsystems(inputs); if (inputs.Any(i=>i.running)) throw new InvalidOperationException("Active XR input in geometry check");
            SubsystemManager.GetSubsystems(displays);
            if (displays.Any(display => display.running) || (playing && UnityEngine.XR.Management.XRGeneralSettings.Instance &&
                UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader))
                throw new InvalidOperationException("Active hardware XR in pickup tests");
        }
        static SettledPickupRunner() => EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false))
                return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                jobs.Clear();
                jobs.Push(RunCase());
                end = EditorApplication.timeSinceStartup + 100;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(Key);
                int code=SessionState.GetInt(Key+"Exit",1);
                int next=SessionState.GetInt(Key+"CaseIndex",0)+1;
                // After Play is fully torn down each case opens a fresh scene and uses a private save.
                if(code!=0 || next>=Cases.Length) { EditorApplication.Exit(code); return; }
                SessionState.SetInt(Key+"CaseIndex",next);
                EditorApplication.delayCall+=Run;
            }
        };
        public static void Run()
        {
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(Folder))
                throw new InvalidOperationException("Use isolated batch QA");
            RequireDesktopIsolation(false);
            var path = Mode switch { "floor-ink" => "Assets/Samples/ToolBeltItems/InkRubbingLab.unity", "floor-stake" => "Assets/Samples/ToolBeltItems/CooldownStakeLab.unity", "ink" => "Assets/Samples/ToolBeltItems/InkRubbingLab.unity",
                                     "stake" => "Assets/Samples/ToolBeltItems/CooldownStakeLab.unity",
                                     "generic" => "Assets/Samples/ToolBeltItems/ItemInteractionLab.unity",
                                     _ => "Assets/Scene/Chapter02_SuspendedPath.unity" };
            var scene = EditorSceneManager.OpenScene(path);
            var p = scene.GetRootGameObjects()
                        .SelectMany(g => g.GetComponentsInChildren<LinearRpgController>(true))
                        .Single();
            p.autoLoad = false;
            p.saveFileName = "pickup-qa-" + Guid.NewGuid().ToString("N") + ".json";
            File.WriteAllText(Report,
                              DateTimeOffset.Now.ToString("o") + " " + path + " " + Application.unityVersion + "\n");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + "Exit", 1);
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            try
            {
                RequireDesktopIsolation(true);
                if (EditorApplication.timeSinceStartup > end)
                    throw new TimeoutException(Mode);
                while (jobs.Count > 0)
                {
                    var top = jobs.Peek();
                    if (!top.MoveNext())
                    {
                        jobs.Pop();
                        continue;
                    }
                    if (top.Current is IEnumerator nested)
                    {
                        jobs.Push(nested);
                        continue;
                    }
                    return;
                }
                SessionState.SetInt(Key + "Exit", 0);
                Pass("Standalone init=false, configured loaders=0, active loader=null, running XR displays=0 in Play");
                File.AppendAllText(Report, "PASS END\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(Report, "FAIL " + ex + "\n");
            }
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
        static void Pass(string label) => File.AppendAllText(Report, "PASS " + label + "\n");
        static void Check(bool result, string label)
        {
            if (!result)
                throw new InvalidOperationException(label);
            Pass(label);
        }
        static IEnumerator RunCase()
        {
            var player = Object.FindFirstObjectByType<LinearRpgController>();
            while (!player.CanAct)
                yield return null;
            if (Mode == "floor-cake") { yield return SettledBiscuitProbe.Run(player, Pass); yield break; }
            if (Mode.StartsWith("floor-")) { yield return SettledPickupProbe.Run(player, Mode, Pass); yield break; }
            if (Mode == "ration")
            {
                yield return RationPickupChecks.Run(player, Pass);
                yield break;
            }
            var belt =
                Object.FindObjectsByType<ToolBeltInteraction>(FindObjectsSortMode.None).Single(x => x.player == player);
            while (!belt.Initialised)
                yield return null;
            if (Mode == "ink")
            {
                yield return TestItem(belt, belt.items.Single(i => i && i.Kind == GameItemKind.PineInk));
                yield return TestItem(belt, belt.items.Single(i => i && i.Kind == GameItemKind.BronzeRubbing));
            }
            else if (Mode == "chapterpen")
            {
                var lesson = Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
                Check(!lesson.equipment.pen.isActiveAndEnabled, "fresh chapter pen remains story locked");
                lesson.equipment.SetWritingAvailable(true);
                yield return TestItem(belt, lesson.equipment.pen);
            }
            else if (Mode == "generic")
            {
                var items = belt.items.Where(it => it && it.home && it.isActiveAndEnabled).ToArray();
                Check(items.Length > 0, "authored sample items initialized");
                foreach (var item in items)
                    yield return TestItem(belt, item);
            }
            else
                yield return TestItem(belt, belt.items.OfType<CooldownStake>().Single());
        }
        static IEnumerator TestItem(ToolBeltInteraction belt, GameItemBase item)
        {
            var p = belt.player;
            var picks = p.Input.Pickups;
            var head = p.locomotion.head;
            while (!item.Dock || item.Dock.IsSnapping)
                yield return null;
            var source = item.Dock;
            var dockRot = item.transform.rotation;
            var start = item.GripPosition;
            var from = start + head.forward * .28f + head.up * .15f;
            double t = Time.realtimeSinceStartupAsDouble;
            PlayerHandObservation Pose(Vector3 position, Vector3 target, bool pressed)
            {
                var dir = (target - position).normalized;
                if (dir.sqrMagnitude < .5f)
                    dir = head.forward;
                return new PlayerHandObservation { headPosition = head.position,
                                                   headForward = head.forward,
                                                   rightObserved = t,
                                                   rightValid = true,
                                                   rightIndexRayValid = true,
                                                   rightIndexDirection = dir,
                                                   rightWrist = position,
                                                   rightTip = position,
                                                   rayOrigin = position,
                                                   rayValid = true,
                                                   rayDirection = dir,
                                                   gripPosition = position,
                                                   gripPoseValid = true,
                                                   gripRotation = dockRot,
                                                   pinchValid = true,
                                                   pinchDistance = pressed ? .012f : .07f,
                                                   systemPinch = true,
                                                   systemPressed = pressed,
                                                   source = "QA" };
            }
            void Sample(bool left, Vector3 pos, Vector3 target, bool down)
            {
                t = Math.Max(t + .018, Time.realtimeSinceStartupAsDouble);
                var f = Pose(pos, target, down);
                var right = left ? default : f;
                picks.Evaluate(left ? f : default, right, t);
                if (Mode == "chapterpen")
                    p.GlyphPen.Observe(right, t);
                else
                    belt.Step(right, t, .018f);
            }
            string label = item.Kind.ToString();
            Sample(true, from, start, false);
            Check(picks.Highlighted(true) == item.transform, label + " left ray marks actual pocket item");
            var marker = p.GetComponentsInChildren<LineRenderer>().FirstOrDefault(line =>
                line.enabled && line.GetComponent<Oculus.Interaction.ConicalFrustum>());
            Check(marker && marker.positionCount > 2 && Vector3.Distance(marker.GetPosition(1), start) < .24f,
                  label + " visible world-space ray and endpoint on target");
            Vector3 beyond = from + head.forward * (p.pickupRayReach + 1f);
            Sample(true, beyond, start, false);
            Check(picks.Highlighted(true) != item.transform, label + " outside configured reach is unmarked");
            Sample(true, from, start, false);
            var wrapper = item.GetComponent<DistanceGrabInteractionController>();
            Check(wrapper && wrapper.DistanceGrab && wrapper.DistanceGrab.Rigidbody == item.GetComponent<Rigidbody>(),
                  label + " original NeuGame wrapper binds actual Meta rigidbody");
            wrapper.DisableGrab();
            Sample(true, from, start, false);
            Check(picks.Highlighted(true) != item.transform, label + " original DisableGrab removes candidate");
            wrapper.EnableGrab();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (from + start) * .5f;
            wall.transform.localScale = Vector3.one * .075f;
            Physics.SyncTransforms();
            Sample(true, from, start, false);
            Sample(true, from, start, true);
            Check(!item.IsHeld && picks.Highlighted(true) != item.transform,
                  label + " wall rejects highlight and pickup");
            wall.SetActive(false);
            Object.Destroy(wall);
            Physics.SyncTransforms();
            Sample(true, from, start, false);
            Sample(true, from, start, true);
            Check(belt.Held == item && belt.HeldInLeftHand && Vector3.Distance(item.GripPosition, from) < .005f,
                  label + " left distant pinch owns same item");
            t += .02;
            var other = Pose(item.GripPosition, item.GripPosition, true);
            picks.Evaluate(default, other, t);
            belt.Step(other, t, .02f);
            Check(belt.Held == item && belt.HeldInLeftHand, label + " other hand and brief loss cannot steal");
            Sample(true, from, start, true);
            var drop = head.TransformPoint(new Vector3(.33f, -.30f, .65f));
            Sample(true, drop, start, true);
            Sample(true, drop, start, false);
            Check(!belt.Held && !item.Dock && !item.IsHeld, label + " deliberate release leaves world drop");
            // Use the post-release pose, including the real stake's floor-placement handling.
            var real = item.GripPosition;
            var rightFrom = real - head.forward * .28f;
            Sample(false, rightFrom, real, false);
            Sample(false, rightFrom, real, true);
            Check(belt.Held == item && !belt.HeldInLeftHand && Vector3.Distance(item.GripPosition, rightFrom) < .005f,
                  label + " right ray recovers actual dropped item");
            var opening = ((ToolBeltSlot)source).opening.position;
            Sample(false, opening, opening, true);
            Sample(false, opening, opening, false);
            Check(item.Dock == source, label + " returns to original legal pocket");
            while (item.Dock.IsSnapping)
                yield return null;
            var returnedGrip = item.GripPosition;
            Sample(true, returnedGrip + head.forward * .30f, returnedGrip, false);
            Check(picks.Highlighted(true) == item.transform, label + " re-docked pocket can be marked from afar");
            Sample(true, item.GripPosition, item.GripPosition, false);
            var nearTarget = picks.Highlighted(true);
            var nearPosition = item.GripPosition;
            Sample(true, item.GripPosition, item.GripPosition, true);
            if (belt.Held != item)
            {
                p.Input.CanInteract(out var reason);
                var ray = nearPosition - head.position;
                var obstruction = Physics.RaycastAll(head.position, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore);
                File.AppendAllText(Report, $"NEAR head={head.position:F3} grip={nearPosition:F3} marker={nearTarget} claimed={p.Input.InteractionOwnsHand} held={belt.Held} reason={reason} " +
                    $"rayHits={string.Join(",", obstruction.Select(h => h.collider.name))}\n");
            }
            Check(belt.Held == item && belt.HeldInLeftHand, label + " left near pickup");
            Sample(true, start, start, false);
            while (item.Dock && item.Dock.IsSnapping)
                yield return null;
        }
    }
}
