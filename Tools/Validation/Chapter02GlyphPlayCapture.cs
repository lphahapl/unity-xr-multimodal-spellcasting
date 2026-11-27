using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    [InitializeOnLoad]
    public static class Chapter02GlyphPlayCapture
    {
        const string Session = "NarutoVR.Chapter02GlyphPlayCapture";
        const string ScenePath = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const int Fps = 30, Width = 1280, Height = 720;
        static IEnumerator run;
        static double deadline;
        static int lastFrame = -1;

        static Chapter02GlyphPlayCapture() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use the isolated project with -batchmode -force-d3d11, without -quit or -nographics.");

            string raw = Environment.GetEnvironmentVariable("CH02_GLYPH_CAPTURE_DIR");
            string token = Environment.GetEnvironmentVariable("CH02_GLYPH_CAPTURE_TOKEN");
            if (string.IsNullOrWhiteSpace(raw) || !Path.IsPathRooted(raw) ||
                string.IsNullOrEmpty(token) || token.Any(c => !("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".Contains(c))))
                throw new InvalidOperationException("Set an absolute CH02_GLYPH_CAPTURE_DIR and a fresh alphanumeric CH02_GLYPH_CAPTURE_TOKEN.");
            string folder = Path.GetFullPath(raw);
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                throw new IOException("Use an empty capture directory: " + folder);
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath);
            var lesson = FindLesson();
            lesson.player.autoLoad = false;
            lesson.player.saveFileName = "qa-ch02-glyph-capture-" + token + ".json";
            if (File.Exists(Path.Combine(Application.persistentDataPath, lesson.player.saveFileName)))
                throw new IOException("Use a new capture token.");
            SessionState.SetString(Session, folder);
            SessionState.SetInt(Session + ".exit", 1);
            EditorApplication.EnterPlaymode();
        }

        static Chapter02BridgeEquipment FindLesson()
        {
            var result = Object.FindFirstObjectByType<Chapter02BridgeEquipment>();
            if (!result) throw new InvalidOperationException("Install the real Chapter02 bridge lesson in the isolated scene first.");
            return result;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            string folder = SessionState.GetString(Session, "");
            if (string.IsNullOrEmpty(folder)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                run = Play(folder);
                deadline = EditorApplication.timeSinceStartup + 210;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseString(Session);
                EditorApplication.Exit(SessionState.GetInt(Session + ".exit", 1));
            }
        }

        static void Tick()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Chapter02 glyph capture timed out.");
                if (run.MoveNext()) return;
                SessionState.SetInt(Session + ".exit", 0);
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(SessionState.GetString(Session, ""), "failure.txt"), e.ToString());
                Debug.LogException(e);
            }
            finally
            {
                if (SessionState.GetInt(Session + ".exit", 1) == 0)
                    Debug.Log("CH02_GLYPH_CAPTURE_PASS");
            }
            (run as IDisposable)?.Dispose();
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }

        static IEnumerator Play(string folder)
        {
            var lesson = FindLesson();
            var player = lesson.player;
            var pen = lesson.equipment.pen;
            var home = (ToolBeltSlot)pen.home;
            while (!player.IsReady || !player.CanAct)
            {
                if (!player.enabled || player.CanRetryInitialCheckpoint)
                    throw new InvalidOperationException(player.Status);
                yield return null;
            }
            Need(player.StageAt(player.StageIndex) == lesson.approachStage, "Fresh Play must begin at the formal entrance.");
            Need(!pen.isActiveAndEnabled && !HasDeck(lesson), "Pen and bridge must start unavailable.");
            Need(player.locomotion.TryRestoreCheckpoint(player.CheckpointAt(1)), player.locomotion.Status);
            while (player.StageAt(player.StageIndex) != lesson.drawingStage || !lesson.button.gameObject.activeInHierarchy ||
                   pen.Dock != home || home.IsSnapping)
                yield return null;

            using var film = new Film(lesson, folder);
            film.Record();
            while (player.StageAt(player.StageIndex) == lesson.drawingStage) yield return null;
            Need(player.StageAt(player.StageIndex) == lesson.originalFirstStage, "The repair must preserve the original hub objective.");
            film.Reveal();
            film.Save();
        }

        static bool HasDeck(Chapter02BridgeEquipment lesson)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(-9.121364f, 80.6f, 55f), Vector3.down, out var hit,
                1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.transform.IsChildOf(lesson.repairedDeck.transform);
        }

        static void Need(bool result, string reason)
        {
            if (!result) throw new InvalidOperationException(reason);
        }

        [Serializable]
        sealed class Evidence
        {
            public string unityVersion, sourceScene, penPrefab, penMeshAsset, template, input, finalStage;
            public int width, height, fps, frames, pressFrame, pickupFrame, contactFrame, dragFrame,
                stoppedFrame, liftFrame, recognitionFrame, repairFrame, strokes, sampledInk, completionCount;
            public float matchScore, dwellRequiredSeconds, peakBrushDeflectionMm, stoppedBrushDeflectionMm, liftedBrushDeflectionMm,
                largestRecordedNibPlaneErrorMm;
            public bool brushBInRuntime, fixedNibMarker, bridgeWasOpen, bridgeColliderEnabled, originalBarrierRemoved;
        }

        sealed class Film : IDisposable
        {
            readonly Chapter02BridgeEquipment lesson;
            readonly GlyphPenController glyph;
            readonly GlyphStoryPanel panel;
            readonly GameItemBase item;
            readonly Transform head, board;
            readonly Camera camera;
            readonly RenderTexture surface;
            readonly Texture2D pixels;
            readonly string folder;
            readonly Evidence evidence;
            readonly List<string> cues = new();
            readonly GlyphNibDragVisual brush;
            readonly FieldInfo bendField = typeof(GlyphNibDragVisual).GetField("appliedBend", BindingFlags.Instance | BindingFlags.NonPublic);
            readonly Vector3 nibFromGrip, nibInPen;
            readonly Quaternion writingRotation;
            readonly Vector3 bridgeLook = new(-9.121364f, 80.15f, 55.35f);
            readonly Vector3 bridgeEye = new(-11.35f, 82.35f, 59.20f);
            double clock;
            int frame;
            Vector3 virtualGrip;
            bool pinched, readInk;
            Quaternion gripRotation;

            public Film(Chapter02BridgeEquipment lesson, string folder)
            {
                this.lesson = lesson;
                this.folder = folder;
                glyph = lesson.glyph;
                panel = lesson.panel;
                item = lesson.equipment.pen;
                head = lesson.player.locomotion.head;
                board = panel.board.transform;
                var src = Camera.main ? Camera.main : head.GetComponentInChildren<Camera>(true);
                Need(src, "The real XR camera is missing.");
                camera = new GameObject("Chapter02 glyph recording camera").AddComponent<Camera>();
                camera.CopyFrom(src);
                camera.enabled = false;
                camera.orthographic = false;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                // Use the actual authored gorge sky for these exterior demonstration angles.
                if (RenderSettings.skybox) camera.clearFlags = CameraClearFlags.Skybox;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.nearClipPlane = .009f;
                camera.aspect = (float)Width / Height;
                camera.ResetProjectionMatrix();
                surface = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                surface.Create();
                camera.targetTexture = surface;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var mesh = item.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m =>
                    AssetDatabase.GetAssetPath(m.sharedMesh).StartsWith("Assets/Art/Props/MoGuiPen/", StringComparison.Ordinal));
                Need(mesh && item.insertionTip == glyph.nib && item.insertionTip.name == "Nib" &&
                    glyph.penRoot == item.transform && panel.template.symbolId == "glyph.square-rule",
                    "The installed MoGuiPen model and original SquareRule must drive the recording.");
                nibFromGrip = item.transform.InverseTransformPoint(item.insertionTip.position) -
                    item.transform.InverseTransformPoint(item.GripPosition);
                nibInPen = item.transform.InverseTransformPoint(item.insertionTip.position);
                writingRotation = board.rotation * Quaternion.Euler(14f, 34f, 0f);
                brush = item.insertionTip.GetComponentInParent<GlyphNibDragVisual>();
                Need(brush && brush.transform.IsChildOf(item.transform) && bendField != null,
                    "The approved B brush must be the visible prefab above the real Nib marker.");
                evidence = new Evidence {
                    unityVersion = Application.unityVersion, sourceScene = ScenePath,
                    penPrefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(brush.gameObject),
                    penMeshAsset = AssetDatabase.GetAssetPath(mesh.sharedMesh),
                    template = panel.template.symbolId, input = "desktop synthetic hand observation",
                    width = Width, height = Height, fps = Fps, brushBInRuntime = brush,
                    dwellRequiredSeconds = panel.board.idleSeconds, fixedNibMarker = true
                };
                panel.onCompleted.AddListener(Completed);
                clock = Time.realtimeSinceStartupAsDouble;
            }

            void Completed() => evidence.completionCount++;

            public void Record()
            {
                var table = board.parent.parent.GetComponent<BoxCollider>();
                Need(table && !lesson.repairedDeck.activeInHierarchy && lesson.brokenBarrier.activeInHierarchy,
                    "Missing real stone desk or broken bridge state.");
                evidence.bridgeWasOpen = HasDeck(lesson);
                Aim(bridgeEye, bridgeLook, 57);
                Cue("断桥石案 · 修复前");
                Hold(20, false);

                Vector3 button = lesson.button.RestSurface;
                Vector3 axis = lesson.button.cap.forward;
                Aim(board.position - board.forward * .91f + board.right * .26f + Vector3.up * .17f,
                    Vector3.Lerp(table.bounds.center, board.position, .74f), 53);
                Cue("按下石案旁的铜扣，展开范本");
                lesson.button.Sample(button - axis * .012f);
                Hold(7, false);
                lesson.button.Sample(button + axis * .003f);
                evidence.pressFrame = frame;
                Need(lesson.button.IsPressed && panel.IsWaiting && panel.board.IsOpen, "Physical button sample did not open the board.");
                Hold(8, false);
                lesson.button.Sample(button - axis * .012f);
                Hold(10, false);

                var grip = item.GripPosition;
                var boardFacing = Vector3.ProjectOnPlane(-board.forward, Vector3.up).normalized;
                Aim(grip + boardFacing * .34f + board.right * .17f + Vector3.up * .075f,
                    grip - Vector3.up * .025f, 50);
                Cue("轻捏右侧笔鞘里的实体墨规笔");
                virtualGrip = grip;
                gripRotation = item.transform.rotation;
                Hold(10);
                pinched = true;
                evidence.pickupFrame = frame;
                Hold(6);
                Need(glyph.IsHeld && lesson.equipment.interaction.Held == item && lesson.player.Input.InteractionOwnsHand,
                    "The ordinary pinch did not pick up the real pen.");

                var pattern = panel.template.Preview();
                Need(pattern.Length == 1 && pattern[0].Length >= 3, "Unexpected SquareRule sample.");
                var points = pattern[0].Select(v => (Vector3)v * .1375f).ToArray();
                var first = points[0] + Vector3.back * .060f;
                MoveGrip(board.TransformPoint(first) - writingRotation * nibFromGrip, writingRotation, 12);
                BoardShot();
                Cue("笔尖从板面正面靠近，接触后落墨");
                MoveTip(points[0] + Vector3.forward * .001f, 10);
                Need(panel.board.PointCount > 0, "Approach did not produce contact ink.");
                evidence.contactFrame = frame - 1;
                readInk = true;
                MoveTip(points[1] + Vector3.forward * .001f, 30);
                Cue("沿板面拖写 · 笔锋随切向运动略微反弯");
                MoveTip(points[2] + Vector3.forward * .001f, 29, true);
                evidence.dragFrame = frame - 1;
                evidence.strokes = panel.board.StrokeCount;
                evidence.sampledInk = panel.board.PointCount;
                Need(evidence.strokes == 1 && evidence.sampledInk >= 15 && evidence.largestRecordedNibPlaneErrorMm < .5f,
                    "The real ink path did not stay on the board.");
                Cue("贴板停笔，笔锋回直；随后抬离板面");
                Hold(10, true, true);
                evidence.stoppedFrame = frame - 1;
                evidence.stoppedBrushDeflectionMm = Bend();
                readInk = false;
                MoveTip(points[2] + Vector3.back * .056f, 8, true);
                evidence.liftFrame = frame - 1;
                evidence.liftedBrushDeflectionMm = Bend();

                if (brush)
                    Need(evidence.peakBrushDeflectionMm > 1f && evidence.peakBrushDeflectionMm <= 4.51f &&
                         evidence.stoppedBrushDeflectionMm < evidence.peakBrushDeflectionMm * .25f &&
                         evidence.fixedNibMarker,
                         "Runtime B brush did not deflect and recover while retaining the true Nib marker.");
                BoardShot();
                Cue("停笔自动辨符 · 等待识别反馈");
                for (int i = 0; i < 60 && !panel.IsResolving; i++) Snapshot();
                Need(panel.IsResolving && !HasDeck(lesson) && lesson.brokenBarrier.activeInHierarchy, "Expected the live match and pre-repair gold display.");
                evidence.recognitionFrame = frame - 1;
                evidence.matchScore = panel.board.LastScore;
                Cue("方直之矩匹配成功 · 墨迹吸附并亮金");
                int safety = 0;
                while (panel.IsResolving && safety++ < 50) Snapshot();
                Need(evidence.completionCount == 1 && !panel.IsWaiting && HasDeck(lesson) &&
                    !lesson.brokenBarrier.activeInHierarchy, "The actual board completion did not repair the bridge exactly once.");
                evidence.repairFrame = frame - 1;
                Hold(5, false);
            }

            public void Reveal()
            {
                Aim(bridgeEye, bridgeLook, 57);
                Cue("原断桥缺口闭合 · 已启用真实桥面碰撞");
                Hold(28, false);
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.SmoothStep(0, 1, (i + 1) / 16f);
                    Aim(Vector3.Lerp(bridgeEye, new Vector3(-10.9f, 82.0f, 57.9f), a), bridgeLook, 57);
                    Snapshot(false);
                }
                evidence.bridgeColliderEnabled = HasDeck(lesson);
                evidence.originalBarrierRemoved = !lesson.brokenBarrier.activeInHierarchy;
                evidence.finalStage = lesson.player.StageAt(lesson.player.StageIndex).stageId;
            }

            public void Save()
            {
                evidence.frames = frame;
                File.WriteAllLines(Path.Combine(folder, "cues.tsv"), cues);
                File.WriteAllText(Path.Combine(folder, "capture.json"), JsonUtility.ToJson(evidence, true));
            }

            void Cue(string value)
            {
                cues.Add(frame + "\t" + value);
                Debug.Log("CH02_GLYPH_FRAME " + frame + " " + value);
            }

            void BoardShot() => Aim(board.position - board.forward * .64f - board.right * .17f +
                Vector3.up * .036f, board.position - Vector3.up * .015f, 49);

            void BrushShot()
            {
                var tip = item.insertionTip.position;
                Aim(tip - board.forward * .215f + board.right * .095f + Vector3.up * .045f,
                    tip - board.forward * .014f + board.right * .029f, 40);
            }

            void Aim(Vector3 eye, Vector3 focus, float fov)
            {
                camera.fieldOfView = fov;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye, Vector3.up));
            }

            void MoveTip(Vector3 local, int count, bool close = false) =>
                MoveGrip(board.TransformPoint(local) - writingRotation * nibFromGrip, writingRotation, count, close);

            void MoveGrip(Vector3 to, Quaternion rotation, int count, bool close = false)
            {
                var from = virtualGrip;
                var initial = gripRotation;
                for (int i = 1; i <= count; i++)
                {
                    float a = Mathf.SmoothStep(0, 1, i / (float)count);
                    virtualGrip = Vector3.Lerp(from, to, a);
                    gripRotation = Quaternion.Slerp(initial, rotation, a);
                    Snapshot(true, close);
                }
            }

            void Hold(int count, bool hand = true, bool close = false)
            {
                for (int i = 0; i < count; i++) Snapshot(hand, close);
            }

            float Bend() => brush && bendField != null && bendField.GetValue(brush) is Vector3 bend
                ? bend.magnitude * 1000f : 0f;

            void Snapshot(bool hand = true, bool close = false)
            {
                if (hand)
                {
                    clock += 1d / Fps;
                    glyph.Observe(new PlayerHandObservation
                    {
                        rightValid = true, rightObserved = clock, gripPoseValid = true, pinchValid = true,
                        systemPinch = true, systemPressed = pinched, headPosition = head.position, headForward = head.forward,
                        rightTip = virtualGrip, rightWrist = virtualGrip, gripPosition = virtualGrip, gripRotation = gripRotation
                    }, clock);
                    glyph.Advance(clock);
                    evidence.peakBrushDeflectionMm = Mathf.Max(evidence.peakBrushDeflectionMm, Bend());
                    evidence.fixedNibMarker &= (item.transform.InverseTransformPoint(item.insertionTip.position) - nibInPen).sqrMagnitude < 1e-9f;
                    if (readInk)
                    {
                        float error = Mathf.Abs(board.InverseTransformPoint(item.insertionTip.position).z) * 1000f;
                        evidence.largestRecordedNibPlaneErrorMm = Mathf.Max(evidence.largestRecordedNibPlaneErrorMm, error);
                    }
                }
                if (close) BrushShot();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var prior = RenderTexture.active;
                try
                {
                    RenderTexture.active = surface;
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    pixels.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(folder, frame.ToString("D5") + ".jpg"), pixels.EncodeToJPG(93));
                    frame++;
                }
                finally { RenderTexture.active = prior; }
            }

            public void Dispose()
            {
                panel.onCompleted.RemoveListener(Completed);
                camera.targetTexture = null;
                surface.Release();
                Object.Destroy(pixels);
                Object.Destroy(surface);
                Object.Destroy(camera.gameObject);
            }
        }
    }
}
