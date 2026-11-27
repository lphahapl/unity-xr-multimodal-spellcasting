using System;
using System.IO;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class GlyphHandPickupChecks
    {
        const string Session = "NarutoVR.GlyphHandPickupChecks";
        static bool Chapter => SessionState.GetBool(Session + ".chapter", false);
        static string Log => Chapter ? "Logs/ChapterBeltPenHandChecks.txt" : "Logs/GlyphHandPickupChecks.txt";
        static double deadline;
        static bool atStation;

        static GlyphHandPickupChecks() => EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Session, false)) return;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += RunWhenReady;
        };

        public static void Run() => Start(false);
        public static void RunChapter() => Start(true);

        static void Start(bool chapter)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            HandPickupPlayChecks.RequireDesktopIsolation(false);
            SessionState.SetBool(Session + ".chapter", chapter);
            EditorSceneManager.OpenScene(chapter ? "Assets/Scene/Chapter02_SuspendedPath.unity" : GlyphLabSetup.ScenePath);
            var player = Object.FindFirstObjectByType<LinearRpgController>();
            player.autoLoad = false;
            player.saveFileName = "pen-hand-qa-" + Guid.NewGuid().ToString("N") + ".json";
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Log, DateTimeOffset.Now.ToString("o") + " Unity " + Application.unityVersion + "\n");
            SessionState.SetBool(Session, true);
            EditorApplication.EnterPlaymode();
        }

        static void RunWhenReady()
        {
            int exit;
            try
            {
                HandPickupPlayChecks.RequireDesktopIsolation(true);
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Waiting for GlyphLab player");
                var player = Object.FindFirstObjectByType<LinearRpgController>();
                if (player && player.Status.StartsWith("线性RPG初始化失败")) throw new InvalidOperationException(player.Status);
                if (!player || !player.CanAct) return;
                var lesson = Chapter ? player.GetComponent<Chapter02BridgeEquipment>() : null;
                if (Chapter && !ReadyAtStation(player, lesson)) return;
                CheckDrawing(player, player.GetComponent<GlyphPenController>(), lesson);
                File.AppendAllText(Log, "PASS END\n");
                exit = 0;
            }
            catch (Exception ex) { File.AppendAllText(Log, "FAIL " + ex + "\n"); exit = 1; }
            EditorApplication.update -= RunWhenReady;
            SessionState.EraseBool(Session);
            EditorApplication.Exit(exit);
        }

        static bool ReadyAtStation(LinearRpgController player, Chapter02BridgeEquipment lesson)
        {
            var belt = lesson.equipment;
            if (!belt.interaction.Initialised) return false;
            if (!atStation)
            {
                Require(!belt.pen.isActiveAndEnabled && !lesson.glyph.enabled, "fresh chapter keeps the pen locked");
                var reach = (ReachAreaChapterObjective)((ObjectiveChapterFlow)player.chapter).stages[0].objectives[0];
                Require(player.locomotion.TryRestoreCheckpoint(reach.GetComponent<NavigationNode>()), "entered the real stone-table objective");
                atStation = true;
            }
            if (!lesson.drawingObjective.IsRunning || !belt.pen.Dock || belt.pen.Dock.IsSnapping) return false;
            lesson.OpenPanel();
            Require(lesson.panel.IsWaiting && belt.pen.Dock == belt.pen.home, "stone lesson reveals docked pen and original story board");
            return true;
        }

        static void Require(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException(label);
            File.AppendAllText(Log, "PASS " + label + "\n");
        }

        static void CheckDrawing(LinearRpgController player, GlyphPenController pen, Chapter02BridgeEquipment lesson)
        {
            var floor = player.locomotion.PlayerFloorPosition;
            var head = lesson ? player.locomotion.head.position : floor + Vector3.up * 1.62f;
            var heading = Vector3.ProjectOnPlane(player.locomotion.head.forward, Vector3.up).normalized;
            var yaw = Quaternion.LookRotation(heading);
            var carried = lesson ? lesson.equipment.pen : null;
            var rest = carried ? carried.GripPosition : head + yaw * pen.beltOffset;
            var seated = carried ? carried.transform.rotation : yaw;
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (bool left in (lesson ? new[] { true } : new[] {true, false}))
            {
                var start = head + yaw * new Vector3(left ? -.27f : .27f, -.12f, .56f);
                var other = head - Vector3.up * .85f;
                void Step(Vector3 grip, Vector3 aim, bool down, Quaternion rotation)
                {
                    now += .016;
                    var working = new PlayerHandObservation {
                        headPosition = head, headForward = heading, rightObserved = now, rightValid = true,
                        rightTip = grip, rightWrist = grip, rightOpen = !down, rightPointing = !down,
                        gripPoseValid = true, gripPosition = grip, gripRotation = rotation,
                        pinchValid = true, systemPinch = true, systemPressed = down,
                        rightIndexRayValid = true, rightIndexDirection = aim, rayValid = true,
                        rayOrigin = grip, rayDirection = aim, rayAnchorValid = true
                    };
                    var free = working;
                    free.rightTip = free.rightWrist = free.gripPosition = free.rayOrigin = other;
                    free.rightIndexDirection = free.rayDirection = Vector3.down;
                    free.systemPressed = false;
                    var right = left ? free : working;
                    player.Input.Pickups.Evaluate(left ? working : free, right, now);
                    pen.Observe(right, now);
                    pen.Advance(now);
                }
                var direction = (rest - start).normalized;
                for (int sample = 0; sample < 12; sample++) Step(start, direction, false, yaw);
                Require(player.Input.Pickups.Highlighted(left) == pen.penRoot, (left ? "left" : "right") + " ray identified the actual holstered pen");
                Require(Vector3.Distance(start, rest) > pen.pickupRadius * 2 && !pen.IsHeld, "pointing alone leaves the pen in its holster");
                Step(start, direction, true, yaw);
                var side = left ? "left" : "right";
                Require(pen.IsHeld && player.Input.InteractionOwnsHand &&
                    Vector3.Distance(start, carried ? carried.GripPosition : pen.penRoot.position) < .005f, side + " ray took the actual pen");
                if (lesson) Require(lesson.equipment.interaction.OwnsHeldItem(carried) && lesson.equipment.interaction.HeldInLeftHand, "one belt claim drives the chapter pen in the left hand");
                var board = lesson ? lesson.panel.board : pen.activeBoard;
                Require(board && board.IsOpen && !player.Input.CanCast(out _),
                    side + " pickup opens the board and retains the exclusive cast gate");

                var rot = board.transform.rotation;
                var actualGrip = carried ? carried.GripPosition : pen.penRoot.position;
                var tipOffset = rot * (Quaternion.Inverse(pen.penRoot.rotation) * (pen.nib.position - actualGrip));
                void Draw(float x, float z) => Step(board.transform.TransformPoint(new Vector3(x,0,z)) - tipOffset, heading, true, rot);
                Draw(-.025f,-.065f); Draw(-.025f,.002f); Draw(.0f,.002f); Draw(.025f,.002f);
                Require(board.PointCount >= 2 && pen.IsHeld, side + " hand moves the real nib and leaves an ink stroke while opposite hand is low");
                Step(carried ? rest : other, heading, false, seated);
                Require(!pen.IsHeld && !player.Input.InteractionOwnsHand && (carried ? carried.Dock == carried.home :
                    Vector3.Distance(pen.penRoot.position,rest) < .01f), side + " deliberate release returns same pen to holster");
            }
        }
    }
}
