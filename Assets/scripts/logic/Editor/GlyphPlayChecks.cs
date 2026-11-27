using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>从真实 GlyphLab 序列化副本进入 Play，给玩家注入合成观测，隔离存档，验证整条交互链。</summary>
    [InitializeOnLoad]
    public static class GlyphPlayChecks
    {
        const string Key = "NarutoVR.GlyphPlayChecks.";
        const string LogDir = "Logs/GlyphPlayChecks";
        static double deadline;
        static bool running;

        static GlyphPlayChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key + "active", false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    deadline = EditorApplication.timeSinceStartup + 100;
                    running = false;
                    EditorApplication.update += Tick;
                }
                if (state == PlayModeStateChange.EnteredEditMode) Cleanup();
            };
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Application.isBatchMode)
                throw new InvalidOperationException("使用 -batchmode -executeMethod NarutoVR.SealCombat.Editor.GlyphPlayChecks.Run 启动，不添加 -quit。");
            GlyphBoardChecks.Run();
            GlyphPhysicsChecks.Run();
            if (!File.Exists(GlyphLabSetup.ScenePath)) GlyphLabSetup.Build();
            GlyphTemplateChecks.Run();
            string id = Guid.NewGuid().ToString("N");
            string copy = "Assets/Scene/QA_GlyphPlay_" + id + ".unity";
            string save = "qa-glyph-play-" + id + ".json";
            string preference = RpgLocomotionController.ExplorationPreference;
            SessionState.SetBool(Key + "hadPreference", PlayerPrefs.HasKey(preference));
            SessionState.SetInt(Key + "oldPreference", PlayerPrefs.GetInt(preference));
            SessionState.SetString(Key + "copy", copy);
            SessionState.SetString(Key + "save", save);
            SessionState.SetInt(Key + "code", 1);
            SessionState.SetBool(Key + "active", true);
            try
            {
                if (!AssetDatabase.CopyAsset(GlyphLabSetup.ScenePath, copy)) throw new IOException("未能复制 GlyphLab 场景");
                var scene = EditorSceneManager.OpenScene(copy);
                var rpg = Object.FindFirstObjectByType<LinearRpgController>();
                Require(rpg && rpg.enabled, "GlyphLab 挂载并启用玩家组件");
                rpg.autoLoad = false;
                rpg.saveFileName = save;
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("未能保存隔离场景");
                Directory.CreateDirectory(LogDir);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Cleanup();
            }
        }

        static void Tick()
        {
            if (!Application.isPlaying || running) return;
            try
            {
                var rpg = Object.FindFirstObjectByType<LinearRpgController>();
                if (rpg && (!rpg.enabled || (rpg.Status != null && rpg.Status.StartsWith("线性RPG初始化失败", StringComparison.Ordinal))))
                    throw new InvalidOperationException("GlyphLab 的玩家初始化中止：" + rpg.Status);
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("等候 GlyphLab 就绪：" + (rpg ? rpg.Status : "未找到玩家"));
                if (!rpg || !rpg.IsReady || !rpg.CanAct) return;
                running = true;
                Require(Path.GetFileName(rpg.SavePath) == SessionState.GetString(Key + "save", ""), "实际使用独立测试存档");
                GlyphPhysicsChecks.Run();
                new Replay(rpg).Run();
                Debug.Log("GlyphPlayChecks PASS: 真实 GlyphLab 引用，右手取笔/多笔千钧/指物/断线/移动抢占/剧情方矩。");
                SessionState.SetInt(Key + "code", 0);
            }
            catch (Exception e) { running = true; Debug.LogException(e); }
            if (running)
            {
                EditorApplication.update -= Tick;
                EditorApplication.ExitPlaymode();
            }
        }

        static void Cleanup()
        {
            SessionState.SetBool(Key + "active", false);
            int code = SessionState.GetInt(Key + "code", 1);
            try
            {
                string preference = RpgLocomotionController.ExplorationPreference;
                bool hadPreference = SessionState.GetBool(Key + "hadPreference", false);
                int old = SessionState.GetInt(Key + "oldPreference", 0);
                if (hadPreference && (!PlayerPrefs.HasKey(preference) || PlayerPrefs.GetInt(preference) != old))
                {
                    PlayerPrefs.SetInt(preference, old);
                    PlayerPrefs.Save();
                }
                else if (!hadPreference && PlayerPrefs.HasKey(preference))
                {
                    PlayerPrefs.DeleteKey(preference);
                    PlayerPrefs.Save();
                }
                string copy = SessionState.GetString(Key + "copy", "");
                string save = SessionState.GetString(Key + "save", "");
                EditorSceneManager.OpenScene(GlyphLabSetup.ScenePath);
                if (copy.StartsWith("Assets/Scene/QA_GlyphPlay_", StringComparison.Ordinal) && File.Exists(copy))
                    Require(AssetDatabase.DeleteAsset(copy), "移除本次隔离场景");
                if (Path.GetFileName(save) == save && save.StartsWith("qa-glyph-play-", StringComparison.Ordinal))
                    foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    {
                        string path = Path.Combine(Application.persistentDataPath, save + suffix);
                        if (File.Exists(path)) File.Delete(path);
                    }
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static void Require(bool good, string message)
        {
            if (!good) throw new InvalidOperationException("GlyphPlayChecks FAIL: " + message);
        }

        sealed class Replay
        {
            readonly LinearRpgController player;
            readonly GlyphPenController pen;
            readonly GlyphHeavyBody stone;
            readonly GlyphStoryPanel story;
            readonly List<GlyphBoard> activeCompletions = new();
            readonly List<GlyphBoard> storyCompletions = new();
            readonly List<int> storyCountWhenGateOpened = new();
            CompletionMedia media;
            Vector3 head;
            Vector3 headForward = Vector3.forward;
            double clock;

            public Replay(LinearRpgController player)
            {
                this.player = player;
                pen = player.GetComponent<GlyphPenController>();
                var roots = player.gameObject.scene.GetRootGameObjects();
                stone = roots.SelectMany(root => root.GetComponentsInChildren<GlyphHeavyBody>())
                    .Single(body => body.localAxis == Vector3.zero);
                story = roots.SelectMany(root => root.GetComponentsInChildren<GlyphStoryPanel>()).Single();
                head = stone.transform.parent.position + Vector3.up * 1.62f;
                clock = Time.realtimeSinceStartupAsDouble;
            }

            public void Run()
            {
                Require(pen && player.GlyphPen == pen && pen.activeTemplates.Length > 0 && story, "真实玩家的笔、面板、模板装配");
                Require(!player.locomotion.EncounterCombat, "画符试验从非战斗开始");
                CheckSkeletonGrip();
                player.locomotion.SetExplorationMode(ExplorationMovement.PointWalk);
                var shape = pen.activeTemplates.Single(t => t.effect is QianjunEffectDefinition);
                var storyTemplate = story.template;
                Require(shape != storyTemplate && shape.onCompleted != null && storyTemplate && storyTemplate.onCompleted != null,
                    "主动与剧情使用不同的真实符例资产，各自有完成事件");
                CheckTetherRetention(shape);
                media = new CompletionMedia(shape, storyTemplate);
                var captureDirectory = GlyphRejectionCapture.OutputDirectory;
                try
                {
                    GlyphRejectionCapture.OutputDirectory = Path.Combine(LogDir, "Recognition");
                    shape.onCompleted.AddListener(activeCompletions.Add);
                    storyTemplate.onCompleted.AddListener(storyCompletions.Add);
                    story.onCompleted.AddListener(RecordGateOpening);
                    RunInteractions(shape);
                    CheckCombatDrawingDodge();
                }
                finally
                {
                    GlyphRejectionCapture.OutputDirectory = captureDirectory;
                    shape.onCompleted.RemoveListener(activeCompletions.Add);
                    storyTemplate.onCompleted.RemoveListener(storyCompletions.Add);
                    story.onCompleted.RemoveListener(RecordGateOpening);
                }
            }

            void RecordGateOpening() => storyCountWhenGateOpened.Add(storyCompletions.Count);

            void RequireEventCounts(int activeCount, int storyCount, int gateCount, string stage)
            {
                Require(activeCompletions.Count == activeCount && activeCompletions.All(board => board == pen.activeBoard) &&
                    storyCompletions.Count == storyCount && storyCompletions.All(board => board == story.board) &&
                    storyCountWhenGateOpened.Count == gateCount,
                    $"{stage}：主动 {activeCompletions.Count}/{activeCount}、剧情 {storyCompletions.Count}/{storyCount}、机关 {storyCountWhenGateOpened.Count}/{gateCount} 次，各事件携带对应画板");
                media.Check(activeCount, storyCount, pen.activeBoard, story.board, stage);
            }

            void RunInteractions(GlyphTemplate shape)
            {
                TakePen();
                Require(pen.activeBoard && pen.activeBoard.IsOpen, "取笔一次轻捏即打开主动板");
                CheckPenContact(pen.activeBoard);
                LiftAt(pen.activeBoard);
                for (int i = 0; i <= 12; ++i)
                    DrawAt(pen.activeBoard, new Vector3(-.09f + .015f * i, .06f, 0));
                LiftAt(pen.activeBoard);
                clock += pen.idleSeconds + .025;
                LiftAt(pen.activeBoard);
                Require(pen.activeBoard.IsOpen && pen.activeBoard.Status.Contains("未匹配") && !pen.CanYieldDrawingToDodge,
                    "主动板拒绝错误符形，已评判墨迹不属于进战快推的草稿");
                RequireEventCounts(0, 0, 0, "错误的主动符");
                string activeTrace = CheckRejectionCapture(pen.activeBoard, shape, "active");
                DrawAt(pen.activeBoard, new Vector3(0, -.179f, 0));
                LiftAt(pen.activeBoard);
                Paint(pen.activeBoard, shape);
                Require(pen.activeBoard.StrokeCount == shape.Preview().Length, "千钧符多笔被分别采样");
                CheckBoardMeshes(pen.activeBoard, false);
                Screenshot("01-ink", head, pen.activeBoard.transform.position);
                RequireEventCounts(0, 0, 0, "主动书写中");
                clock += pen.idleSeconds + .025;
                LiftAt(pen.activeBoard);
                Require(!pen.activeBoard.IsOpen && pen.Status.Contains(shape.Title) && !pen.CanYieldDrawingToDodge,
                    "主动停笔匹配实际千钧符资源，已提交的效果不属于进战快推的草稿");
                RequireEventCounts(1, 0, 0, "主动符匹配当帧");
                Require(ReadCapture("active") == activeTrace, "主动符成功后保留最近的拒识记录");

                Vector3 aimPoint = stone.GetComponent<Collider>().bounds.center;
                Vector3 hand = aimPoint + new Vector3(.05f, .95f, -.67f);
                var aim = Quaternion.LookRotation(aimPoint - hand);
                Physics.SyncTransforms();
                Step(hand, aim, false);
                var line = player.locomotion.rigRoot.GetComponentsInChildren<LineRenderer>(true)
                    .SingleOrDefault(l => l.enabled && l.name == "千钧符·磁墨牵引线");
                Require(line && Vector3.Distance(line.GetPosition(0), hand) < .01f, "笔尖指中后从持笔手连线");
                var collider = stone.GetComponent<Collider>();
                Vector3 hit = line.GetPosition(1);
                Require((collider.ClosestPoint(hit) - hit).sqrMagnitude < .0001f, "线端落在自由镇石表面");
                Step(hand + aim * new Vector3(.05f, 0, .03f), Quaternion.Euler(0, 120, 0), false);
                Require(line.enabled && Vector3.Distance(line.GetPosition(1), hit) < .001f, "移开笔尖仍锁定原命中点");
                Screenshot("02-tether", head, (hand + hit) * .5f);
                HoldLow(.55, aim);
                Require(!pen.IsHeld && !player.Input.InteractionOwnsHand && (!line || !line.enabled), "持笔手独立放低断线，归还 claim");
                Require(pen.BlocksCombat, "放笔当帧仍抑制旧结印");
                Debug.Log("GlyphPlayChecks: 右手取笔、技能互斥、主动千钧、实物锚定、低手退出通过");

                TakePen();
                Require(pen.activeBoard.IsOpen, "再次取笔也只需一次轻捏");
                LiftAt(pen.activeBoard);
                Paint(pen.activeBoard, shape);
                clock += pen.idleSeconds + .035;
                var board = pen.activeBoard;
                Vector3 lifted = board.transform.TransformPoint(new Vector3(0, 0, -.06f));
                ObserveNib(lifted, board.transform.rotation);
                StartPointWalk();
                Require(!pen.IsHeld && !player.Input.InteractionOwnsHand, "PointWalk 左指移动意图优先结束画符：" + player.locomotion.Status);
                pen.Advance(clock);
                Require(!board.IsOpen && board.LastScore == 0 && !pen.Status.Contains("已识别"), "同帧停笔到时无迟发效果");
                RequireEventCounts(1, 0, 0, "移动抢占主动匹配");
                Debug.Log("GlyphPlayChecks: PointWalk/停笔到时同帧先取消通过");
                StopPointWalk();

                var opener = Object.FindObjectsByType<ZhunwangPressButton>(FindObjectsSortMode.None)
                    .Single(b => Enumerable.Range(0, b.onPressed.GetPersistentEventCount()).Any(i =>
                        b.onPressed.GetPersistentTarget(i) == story && b.onPressed.GetPersistentMethodName(i) == "Show"));
                var gate = GameObject.Find("方矩联动铜栅");
                Require(gate && gate.activeSelf && !story.IsWaiting, "场景初始铜栅关闭");
                opener.onPressed.Invoke();
                Require(story.IsWaiting && story.board.IsOpen, "真实按钮绑定在固定地点展开剧情板");
                head = story.board.transform.position - Vector3.forward * .34f + Vector3.up * .16f;
                TakePen();
                Require(!pen.activeBoard.IsOpen && story.board.IsOpen, "取笔自动选用附近候笔的剧情板");
                CheckPenContact(story.board);
                for (int i = 0; i <= 12; ++i)
                    DrawAt(story.board, new Vector3(-.09f + .015f * i, .06f, 0));
                LiftAt(story.board);
                clock += story.board.idleSeconds + .02f;
                LiftAt(story.board);
                Require(story.IsWaiting && !story.IsResolving && gate.activeSelf,
                    "错误符形停笔后仍能重试且没有开栅");
                RequireEventCounts(1, 0, 0, "错误的剧情符");
                Require(story.board.GetComponentsInChildren<Text>().Any(t => t.text.Contains("符形不符") && t.text.Contains("重画")),
                    "剧情板在 Play 中显示失败提示：" + story.board.Status);
                string storyTrace = CheckRejectionCapture(story.board, story.template, "story");
                DrawAt(story.board, new Vector3(0, -.179f, 0));
                LiftAt(story.board);
                Paint(story.board, story.template);
                clock += story.board.idleSeconds + .02f;
                LiftAt(story.board);
                Require(story.IsResolving && story.board.IsShowingSuccess && gate.activeSelf && !pen.CanYieldDrawingToDodge,
                    "吸附反馈已经开始，机关仍关闭，已判对的剧情符不属于进战快推的草稿");
                Require(ReadCapture("story") == storyTrace && ReadCapture("active") == activeTrace, "剧情符成功后两块板各自保留拒识记录");
                clock += story.board.successDuration * .49;
                pen.Advance(clock);
                RequireEventCounts(1, 0, 0, "吸附尚未完成");
                pen.Finish();
                clock += story.board.successDuration + .05f;
                pen.Advance(clock);
                Require(!story.IsResolving && story.IsWaiting && gate.activeSelf,
                    "成功反馈中取消后，过期回调不能开栅");
                RequireEventCounts(1, 0, 0, "中点前取消后跨过结束时刻");

                TakePen();
                Paint(story.board, story.template);
                CheckBoardMeshes(story.board, true);
                Screenshot("03-story", head, story.board.transform.position);
                clock += story.board.idleSeconds + .02f;
                LiftAt(story.board);
                double started = clock;
                Require(story.IsResolving && gate.activeSelf, "重新画对后，自动进入成功反馈");
                RequireEventCounts(1, 0, 0, "重试识别成功当帧");
                clock = started + story.board.successDuration * .5 + .00001;
                pen.Advance(clock);
                RequireEventCounts(1, 1, 0, "吸附刚好结束");
                clock = started + story.board.successDuration * .76;
                pen.Advance(clock);
                Require(story.IsResolving && gate.activeSelf, "闪光期间铜栅仍然关闭");
                RequireEventCounts(1, 1, 0, "金闪期间");
                Screenshot("04-story-gold", head, story.board.transform.position);
                clock = started + story.board.successDuration * .99;
                pen.Advance(clock);
                Require(gate.activeSelf, "反馈结束前铜栅始终关闭");
                RequireEventCounts(1, 1, 0, "结束前最后一帧");
                pen.Finish();
                Require(story.IsWaiting && !story.IsResolving, "金闪后放笔可以重新画符");

                // 重画后让第一帧直接越过吸附和金闪，检查低帧率下的事件次序。
                TakePen();
                Paint(story.board, story.template);
                clock += story.board.idleSeconds + .02f;
                LiftAt(story.board);
                Require(story.IsResolving, "跨帧重试进入成功反馈");
                RequireEventCounts(1, 1, 0, "跨帧重试开始");
                clock += story.board.successDuration + .01;
                pen.Advance(clock);
                Require(!story.IsWaiting && !story.IsResolving && !gate.activeSelf && storyCountWhenGateOpened.SequenceEqual(new[] { 2 }),
                    "成功反馈末尾只执行一次真实铜栅回调");
                RequireEventCounts(1, 2, 1, "直接越过中点和末端时先完成符例、后开栅");
                pen.Advance(clock + 1);
                RequireEventCounts(1, 2, 1, "重复推进已完成的剧情板");
                pen.Finish();
                Debug.Log("GlyphPlayChecks: 剧情优先开板、错误符重画、吸附中取消、金闪结束后开栅一次通过");
            }

            void StartPointWalk()
            {
                var eye = player.locomotion.head.position;
                var nav = new NavigationHandInput {
                    leftValid = true, leftWrist = eye - Vector3.up * .13f, leftIndex = eye + Vector3.forward * .24f,
                    leftRayOrigin = eye, leftRayValid = true, leftRayDirection = Vector3.forward, leftObserved = clock - .02
                };
                player.locomotion.ProcessInput(nav, true, false, nav.leftObserved);
                nav.leftObserved = clock;
                nav.leftPointing = nav.leftNavigationPointing = true;
                player.locomotion.ProcessInput(nav, true, false, clock);
            }

            void StopPointWalk()
            {
                player.locomotion.ResetInput();
                player.locomotion.ProcessInput(new NavigationHandInput { leftValid = true, leftObserved = clock += .02 }, false, false, clock);
            }

            void CheckCombatDrawingDodge()
            {
                var move = player.locomotion;
                var receiver = player.GetComponent<RpgDamageReceiver>();
                Require(move.UsesEncounterModes && receiver, "GlyphLab 使用正式遭遇移动与受击入口");
                var originalFloor = move.floorLayers;
                var originalRig = move.rigRoot.position;
                player.Player.Respawn(100, 20, player.GameTime, true);
                bool Hit(string id) => receiver.TryReceiveDamage(new EnemyAttackHit(null, receiver, id, 0, 5, receiver.HitPosition));
                void Wrist(float forward, float side = .08f)
                {
                    head = move.head.position;
                    var yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(move.head.forward, Vector3.up));
                    var wrist = head + yaw * new Vector3(side, -move.chestBelowHead, forward);
                    Observe(wrist, yaw, false);
                    player.ProcessMovementFrame(new NavigationHandInput {
                        rightValid = true, rightObserved = clock, rightWrist = wrist
                    }, player.MovementBlockReason != null, clock);
                    pen.Advance(clock);
                }
                try
                {
                    foreach (bool noFloor in new[] { true, false })
                    {
                        // Both left-hand travel choices must allow a drawn stroke to yield to the first combat push.
                        move.SetCombatMode(noFloor ? CombatMovement.NodeTeleport : CombatMovement.PointWalk);
                        bool storyDraft = !noFloor;
                        if (storyDraft) story.Show();
                        head = storyDraft ? story.board.transform.position - Vector3.forward * .34f + Vector3.up * .16f : move.head.position;
                        TakePen();
                        var board = storyDraft ? story.board : pen.activeBoard;
                        LiftAt(board);
                        DrawAt(board, new Vector3(-.05f, .04f, 0));
                        DrawAt(board, new Vector3(.04f, .04f, 0));
                        Require(board.HasUnsubmittedInk && pen.CanYieldDrawingToDodge, "板面真实采样产生未提交墨迹");
                        if (!storyDraft)
                        {
                            Wrist(.16f);
                            Wrist(.38f);
                            Require(pen.IsHeld && !move.EncounterSlideActive, "探索中快推不抢笔");
                        }
                        Wrist(.16f);
                        int points = board.PointCount;
                        int completions = activeCompletions.Count + storyCompletions.Count;
                        player.SetEncounterCombat(true);
                        if (noFloor)
                        {
                            clock += move.gestures.sampleMaxGap + .03;
                            Wrist(.37f);
                            Require(pen.IsHeld && !move.EncounterSlideActive, "过期的进战前腕点不能启动滑退");
                            Wrist(.16f);
                            Wrist(.18f, .27f);
                            for (int i = 0; i < 90; i++) Wrist(.18f + i * .002f);
                            Require(pen.IsHeld && player.Input.InteractionOwnsHand && board.PointCount == points &&
                                board.HasUnsubmittedInk && !move.EncounterSlideActive, "进战、侧推、短推、慢推及停笔超时保留原稿");
                            player.SetEncounterCombat(false);
                            LiftAt(board);
                            Require(board.HasUnsubmittedInk && board.PointCount == points, "返回探索时不会补交超时草稿");
                            DrawAt(board, new Vector3(.07f, .06f, 0));
                            Require(board.PointCount == points + 1, "退战后可在原稿上续笔");
                            player.SetEncounterCombat(true);
                            move.floorLayers = 0;
                            Wrist(.16f);
                            Wrist(.16f);
                        }
                        Vector3 start = move.rigRoot.position;
                        // The story case starts at the chest one frame before combat and
                        // crosses the threshold on its first combat sample.
                        Wrist(.37f);
                        Require(move.EncounterSlideActive && move.rigRoot.position == start && !pen.IsHeld &&
                            !player.Input.InteractionOwnsHand && board.PointCount == 0, "首次有效右手快推同帧启动滑退、清稿并归还持笔权");
                        Require(board.IsOpen == storyDraft && (!storyDraft || story.IsWaiting) &&
                            activeCompletions.Count + storyCompletions.Count == completions, "主动板关闭、剧情板仍候笔，取消不发完成事件");
                        Require(Hit(noFloor ? "glyph-wall-intent" : "glyph-slide-intent") && player.Player.Mana == 20,
                            "快推已识别但没有实际位移时仍可受伤");
                        Wrist(.37f);
                        if (noFloor)
                            Require(!move.EncounterSlideActive && move.rigRoot.position == start && Hit("glyph-wall-step") &&
                                player.Player.Mana == 20, "首步缺地面时正常受伤、没有度力奖励");
                        else
                        {
                            Vector3 back = -Vector3.ProjectOnPlane(move.head.forward, Vector3.up).normalized;
                            Require(Vector3.Dot(move.rigRoot.position - start, back) > .001f, "不再快推，下一帧已经向后位移");
                            Require(!Hit("glyph-slide-first") && !Hit("glyph-slide-second") && player.Player.Mana == 45,
                                "首次有效位移后免伤，同一次滑退仅回复一次 25 度力");
                        }
                        player.SetEncounterCombat(false);
                        move.floorLayers = originalFloor;
                    }
                    Debug.Log("GlyphCombatDodge PASS: active/story, exploration, invalid/stale push, cross-transition push, same push release/slide, blocked floor, first-step protection, single mana award.");
                }
                finally
                {
                    move.floorLayers = originalFloor;
                    move.rigRoot.position = originalRig;
                    player.SetEncounterCombat(false);
                    pen.Finish();
                    story.Cancel();
                }
            }

            void HoldLow(double duration, Quaternion rotation)
            {
                Vector3 low = head - Vector3.up * (pen.tetherReleaseBelowHead + .06f);
                double until = clock + duration;
                while (clock < until) Step(low, rotation, false);
            }

            void LoseTracking(double duration)
            {
                double until = clock + duration;
                while (clock < until)
                {
                    clock = Math.Min(until, clock + .1);
                    pen.Observe(new PlayerHandObservation { headPosition = head, headForward = headForward }, clock);
                    pen.Advance(clock);
                }
            }

            void CheckTetherRetention(GlyphTemplate shape)
            {
                var failures = new List<string>();
                var wristOffset = new Vector3(.015f, .025f, .018f);
                var fixedTick = typeof(GlyphPenController).GetMethod("FixedUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                void ApplyForces() => fixedTick.Invoke(pen, null);
                void Case(string label, Action<LineRenderer, Vector3, Vector3> check)
                {
                    Vector3 initialHead = head;
                    try
                    {
                        TakePen();
                        LiftAt(pen.activeBoard);
                        Paint(pen.activeBoard, shape);
                        clock += pen.idleSeconds + .025;
                        LiftAt(pen.activeBoard);
                        Require(!pen.activeBoard.IsOpen && pen.Status.Contains(shape.Title), "先识别千钧符");
                        Physics.SyncTransforms();
                        Vector3 center = stone.GetComponent<Collider>().bounds.center;
                        Vector3 hand = center + new Vector3(.05f, .95f, -.67f);
                        Observe(hand, Quaternion.LookRotation(center - hand), false, hand - wristOffset);
                        pen.Advance(clock);
                        var line = player.locomotion.rigRoot.GetComponentsInChildren<LineRenderer>(true)
                            .Single(l => l.enabled && l.name == "千钧符·磁墨牵引线");
                        Vector3 hit = line.GetPosition(1);
                        Require(Vector3.Distance(stone.GetComponent<Collider>().ClosestPoint(hit), hit) < .001f, "实际投射命中自由石");
                        check(line, hand, hit);
                        Debug.Log("GlyphTetherPlay PASS " + label);
                    }
                    catch (Exception e) { failures.Add(label + "：" + e.Message); Debug.Log("GlyphTetherPlay FAIL " + failures.Last()); }
                    finally
                    {
                        if (!pen.enabled) pen.enabled = true;
                        pen.Finish();
                        head = initialHead;
                        headForward = Vector3.forward;
                        StopPointWalk();
                    }
                }
                void Locked(LineRenderer line, Vector3 hit, string message)
                {
                    Require(pen.IsHeld && player.Input.InteractionOwnsHand && line && line.enabled &&
                        Vector3.Distance(line.GetPosition(1), hit) < .001f, message);
                }
                void Released(LineRenderer line, string message)
                {
                    Require(!pen.IsHeld && !player.Input.InteractionOwnsHand && (!line || !line.enabled), message);
                }
                Case("PointWalk 和转身", (line, hand, hit) =>
                {
                    var origin = player.locomotion.rigRoot.position;
                    int starts = 0;
                    void CountMove() => starts++;
                    player.locomotion.MovementStarting += CountMove;
                    try { StartPointWalk(); }
                    finally { player.locomotion.MovementStarting -= CountMove; }
                    Require(starts > 0, "PointWalk 确实发出了移动开始事件：" + player.locomotion.Status);
                    var travel = player.locomotion.rigRoot.position - origin;
                    Debug.Log($"GlyphTetherPlay PointWalk 步长 {travel.magnitude:F4} 米，{player.locomotion.Status}");
                    var turn = Quaternion.Euler(0, 120, 0);
                    headForward = turn * Vector3.forward;
                    Vector3 turnedHand = head + travel + turn * (hand - head);
                    head += travel;
                    Step(turnedHand, turn, false);
                    Locked(line, hit, "左手起步、头部转向并移开笔尖后仍锁住原命中点");
                    double until = clock + .6;
                    while (clock < until) Step(head - Vector3.up * .59f, turn, false);
                    Locked(line, hit, "走路摆臂经过画符的退出高度仍保留牵引");
                });
                Case("短暂低手和有意收笔", (line, hand, hit) =>
                {
                    HoldLow(.15, Quaternion.identity);
                    Step(hand, Quaternion.Euler(0, 130, 0), false);
                    Locked(line, hit, "低手约 0.15 秒后抬起仍牵引原石");
                    HoldLow(.30, Quaternion.identity);
                    Require(pen.IsHeld, "短暂低手后抬起，中止上一轮收笔计时");
                    HoldLow(.25, Quaternion.identity);
                    Released(line, "连续放低超过约半秒应释放占用和牵引线");
                });
                Case("短暂失追恢复和续拉", (line, hand, hit) =>
                {
                    LoseTracking(2);
                    Require(pen.IsHeld && player.Input.InteractionOwnsHand, "失追 2 秒仍保留牵引");
                    var body = stone.Body;
                    Vector3 priorForce = body.GetAccumulatedForce();
                    Vector3 returnedHand = hand + new Vector3(.10f, .01f, .04f);
                    Step(returnedHand, Quaternion.Euler(0, 140, 0), false);
                    Locked(line, hit, "恢复时笔尖转离石块，仍连接原表面点");
                    ApplyForces();
                    Require(Vector3.Distance(priorForce, body.GetAccumulatedForce()) < .001f, "恢复首帧只建基线，刚体不受跳变冲量");
                    Vector3 pull = (returnedHand - hit).normalized;
                    Step(returnedHand + .04f * pull, Quaternion.Euler(0, 145, 0), false);
                    ApplyForces();
                    Require(Vector3.Dot(body.GetAccumulatedForce() - priorForce, pull) > .01f, "下一笔可信回拉实际影响原刚体");
                });
                Case("仅有新鲜腕位仍能拉石", (line, hand, hit) =>
                {
                    void WristOnly(Vector3 wrist)
                    {
                        clock += .035;
                        pen.Observe(new PlayerHandObservation {
                            headPosition = head, headForward = headForward, rightValid = true, rightObserved = clock,
                            rightWrist = wrist, tipUnavailable = true
                        }, clock);
                        pen.Advance(clock);
                    }
                    Vector3 wrist = hand - wristOffset;
                    double until = clock + 1.65;
                    while (clock < until) WristOnly(wrist);
                    Locked(line, hit, "仅余手腕时经过旧 1.2 秒时限仍保留同一命中点");
                    Vector3 pull = (hand - hit).normalized;
                    Vector3 delta = pull * .04f;
                    var before = stone.Body.GetAccumulatedForce();
                    WristOnly(wrist + delta);
                    ApplyForces();
                    Require(Vector3.Distance(line.GetPosition(0), hand + delta) < .004f &&
                        Vector3.Dot(stone.Body.GetAccumulatedForce() - before, pull) > .01f,
                        "握点沿用最后可信的腕部偏移，真实腕部回拉向刚体施力");
                });
                Case("右手完全不可见时身体拖绳", (line, hand, hit) =>
                {
                    Vector3 retreat = Vector3.ProjectOnPlane(hand - hit, Vector3.up).normalized;
                    var before = stone.Body.GetAccumulatedForce();
                    LoseTracking(.12);
                    headForward = Quaternion.Euler(0, 18, 0) * Vector3.forward;
                    for (int i = 0; i < 35; ++i)
                    {
                        head += retreat * .018f;
                        LoseTracking(.025);
                        ApplyForces();
                    }
                    Require(pen.IsHeld && player.Input.InteractionOwnsHand &&
                        Vector3.Dot(stone.Body.GetAccumulatedForce() - before, retreat) > .01f,
                        "右手不可见、头仍可信时，身体转向并退开能拉动石块");
                    var atTurn = stone.Body.GetAccumulatedForce();
                    for (int i = 0; i < 35; ++i)
                    {
                        head -= retreat * .018f;
                        LoseTracking(.025);
                        ApplyForces();
                    }
                    Require(Vector3.Dot(stone.Body.GetAccumulatedForce() - atTurn, retreat) > -.01f,
                        "身体往回走时绳力不会反向推石");
                });
                Case("持续失追释放", (line, hand, hit) =>
                {
                    LoseTracking(5.6);
                    Require(pen.IsHeld && player.Input.InteractionOwnsHand, "六秒失追期限前保留牵引");
                    LoseTracking(.7);
                    Released(line, "超过六秒的持续失追清除牵引");
                });
                Case("宿主禁用", (line, hand, hit) =>
                {
                    pen.enabled = false;
                    Released(line, "禁用宿主立即归还输入并清理线");
                });
                Require(failures.Count == 0, string.Join(" | ", failures));
            }

            static string ReadCapture(string mode) =>
                File.ReadAllText(Path.Combine(GlyphRejectionCapture.OutputDirectory, "last-" + mode + ".json"));

            string CheckRejectionCapture(GlyphBoard board, GlyphTemplate expected, string mode)
            {
                string rejection = ReadCapture(mode);
                var saved = JsonUtility.FromJson<GlyphRejectionCapture.Snapshot>(rejection);
                Require(saved.mode == mode && saved.reason == board.Status && saved.scene == board.gameObject.scene.path &&
                    saved.strokeCount == board.StrokeCount && saved.points.Length == board.PointCount && board.PointCount > 2 &&
                    Math.Abs(saved.decisionTime - clock) < .000000001 && Mathf.Approximately(saved.lastScore, board.LastScore),
                    "拒识文件关联本次画板、采样、时间与实际判定");
                var entry = saved.candidates.Single(c => c.symbolId == expected.SymbolKey);
                var lines = board.Samples.GroupBy(p => p.strokeIndex).Where(group => group.Count() > 1)
                    .Select(group => group.Select(p => (Vector2)p.boardPosition).ToArray()).ToList();
                Require(entry.scored && entry.name == expected.Title && Mathf.Approximately(entry.threshold, expected.threshold) &&
                    Mathf.Approximately(entry.score, expected.Score(lines)), "保存实际候选得分和配置阈值");
                for (int i = 0; i < saved.points.Length; ++i)
                {
                    var sample = board.Samples[i];
                    var copy = saved.points[i];
                    Require(copy.strokeIndex == sample.strokeIndex && Math.Abs(copy.time - sample.time) < .000000001 &&
                        Vector3.Distance(copy.boardPosition, sample.boardPosition) < .000001f, "原始板面坐标、笔序和时间戳");
                }
                DrawAt(board, new Vector3(.035f, -.035f, 0));
                LiftAt(board);
                Require(board.IsOpen && board.PointCount == saved.points.Length + 1 && ReadCapture(mode) == rejection,
                    "拒识后可立即续画，补笔不会覆盖磁盘样本");
                return rejection;
            }

            void CheckPenContact(GlyphBoard board)
            {
                var original = board.transform.rotation;
                board.transform.rotation = original * Quaternion.Euler(-10, 23, 12);
                var gripRotation = board.transform.rotation * Quaternion.Euler(8, -15, 4);
                var nibOffset = Quaternion.Inverse(pen.penRoot.rotation) * (pen.nib.position - pen.penRoot.position);
                void Pose(float x, float y, float z)
                {
                    Vector3 rawTip = board.transform.TransformPoint(new Vector3(x, y, z));
                    Step(rawTip - gripRotation * nibOffset, gripRotation, false);
                }
                void CheckAt(float x, float y, float z)
                {
                    Vector3 expected = board.transform.TransformPoint(new Vector3(x, y, z));
                    Require(Vector3.Distance(pen.nib.position, expected) < .0002f &&
                        Vector3.Distance(pen.penRoot.position, expected - gripRotation * nibOffset) < .0002f &&
                        Quaternion.Angle(pen.penRoot.rotation, gripRotation) < .04f,
                        "整笔平移并保留手追旋转；笔尖实际停在预期板面坐标");
                }
                try
                {
                    Pose(.42f, .02f, -.04f);
                    Pose(.07f, .02f, .18f);
                    CheckAt(.07f, .02f, .18f);
                    Require(board.PointCount == 0, "快速从侧边绕过，穿过所在平面的位置在板外不会落墨");
                    Pose(-.045f, .02f, -.075f);
                    Pose(-.045f, .02f, .14f);
                    CheckAt(-.045f, .02f, 0);
                    Pose(.06f, .03f, .17f);
                    CheckAt(.06f, .03f, 0);
                    Require(board.PointCount == 2 && board.StrokeCount == 1 &&
                        Vector3.Distance(board.transform.TransformPoint(board.Samples[1].boardPosition), pen.nib.position) < .0002f,
                        "快速穿越及深压滑动产生一笔，采样点等于受限的实体笔尖");
                    Pose(.26f, .02f, .12f);
                    CheckAt(.26f, .02f, .12f);
                    Pose(.26f, .02f, .003f);
                    Pose(.01f, .02f, .003f);
                    Pose(.03f, .02f, -.003f);
                    Pose(-.119f, -.179f, .002f);
                    Require(board.PointCount == 2, "侧缘绕到背面并从背面掠过容差不会落墨");
                    Pose(.03f, .02f, -.07f);
                    Pose(.03f, .02f, .12f);
                    Require(board.StrokeCount == 2, "抬到前面后能够重新落笔");
                    clock += .016;
                    pen.Observe(new PlayerHandObservation { headPosition = head, headForward = Vector3.forward }, clock);
                    pen.Advance(clock);
                    Pose(.07f, .02f, .006f);
                    CheckAt(.07f, .02f, .006f);
                    Require(board.StrokeCount == 2 && board.PointCount == 3, "丢追再从背面恢复不沿用贴面或补笔");
                    Pose(.02f, .04f, -.035f);
                    Pose(.02f, .04f, -board.contactDistance * .55f);
                    CheckAt(.02f, .04f, 0);
                    Require(board.StrokeCount == 3, "重获前侧后只需轻触，未穿过书写面也能重新起笔");
                    Pose(.02f, .04f, -board.releaseDistance * 1.3f);
                    CheckAt(.02f, .04f, -board.releaseDistance * 1.3f);
                    clock += .016;
                    pen.Observe(new PlayerHandObservation { headPosition = head }, clock);
                    pen.Advance(clock);
                    Pose(-.04f, .02f, -board.contactDistance * .75f);
                    Pose(-.04f, .02f, -board.contactDistance * .4f);
                    CheckAt(-.04f, .02f, 0);
                    Require(board.StrokeCount == 4, "第一帧就在正面触碰容差内，向前轻压即可落笔");
                    Pose(-.119f, -.179f, -.07f);
                    Pose(-.119f, -.179f, .10f);
                    CheckAt(-.119f, -.179f, 0);
                    Require(board.StrokeCount == 3, "旋转板的撤销键同样阻挡深压笔尖");
                    Pose(0, -.179f, -.07f);
                    Pose(0, -.179f, .1f);
                    CheckAt(0, -.179f, 0);
                    Require(board.PointCount == 0 && board.StrokeCount == 0, "旋转板的清空键仍可点");
                }
                finally { board.transform.rotation = original; }
                LiftAt(board);
                Debug.Log("GlyphPlayChecks: " + board.name + " 旋转板整笔限位、同点采样、正背面、丢追、撤销清空通过");
            }

            void CheckSkeletonGrip()
            {
                var lab = player.handTracking ? player.handTracking : player.skills.lab;
                var saved = lab.Frame;
                var frameProperty = typeof(NarutoVR.SealLab.SealLab).GetProperty("Frame");
                double stamp = Time.realtimeSinceStartupAsDouble;
                var frame = RpgHandPoseChecks.Skeleton(stamp);
                frame.source = saved?.source;
                frame.left.connected = frame.left.tracked = frame.left.fresh = false;
                int wrist = (int)Oculus.Interaction.Input.HandJointId.HandWristRoot;
                int index = (int)Oculus.Interaction.Input.HandJointId.HandIndex1;
                int middle = (int)Oculus.Interaction.Input.HandJointId.HandMiddle1;
                try
                {
                    frameProperty.SetValue(lab, frame);
                    player.Input.SampleHands(stamp);
                    var input = player.Input.LatestHand;
                    var anchor = (frame.right.joints[index].position + frame.right.joints[middle].position) * .5f;
                    if (lab.recordingOrigin) anchor = lab.recordingOrigin.TransformPoint(anchor);
                    Require(!input.leftValid && input.rightValid && input.gripPoseValid, "右手关节帧独立提供握笔姿态");
                    Require(Vector3.Distance(input.gripPosition, anchor) < .0001f &&
                        Vector3.Dot(input.gripRotation * Vector3.forward, (anchor - input.rightWrist).normalized) > .999f,
                        "实际输入适配器以指根为握点、腕至指根为笔尖前向");
                    frame.right.jointValid[wrist] = false;
                    frame.right.observedAt = frame.time = Time.realtimeSinceStartupAsDouble;
                    player.Input.SampleHands(frame.time);
                    Require(!player.Input.LatestHand.gripPoseValid, "握笔腕关节丢失使姿态失效");
                    Debug.Log("GlyphPlayChecks: 真实输入适配器的单右手取笔锚点、笔尖朝向和丢腕失效通过");
                }
                finally
                {
                    frameProperty.SetValue(lab, saved);
                    player.Input.SampleHands(Time.realtimeSinceStartupAsDouble);
                }
            }

            void TakePen()
            {
                Vector3 belt = pen.holster ? pen.holster.position : head + pen.beltOffset;
                Step(belt, Quaternion.identity, false);
                Step(belt, Quaternion.identity, true);
                Require(pen.IsHeld && player.Input.InteractionOwnsHand, "仅右手有效时轻捏上腹拿笔：" + pen.Status);
                Require(!player.Input.CanCast(out _) && player.skills.ExclusiveInputBlocked() &&
                    player.skills.GestureInputBlocked() && player.skills.VoiceInputBlocked(), "持笔时语音和结印入口均被占用");
            }

            void Paint(GlyphBoard board, GlyphTemplate template)
            {
                foreach (var stroke in template.Preview())
                {
                    DrawAt(board, stroke[0] * .115f);
                    for (int i = 1; i < stroke.Length; ++i)
                        for (int point = 1; point <= 12; ++point)
                            DrawAt(board, Vector2.Lerp(stroke[i - 1], stroke[i], point / 12f) * .115f);
                    LiftAt(board);
                }
            }

            void LiftAt(GlyphBoard board) => DrawAt(board, new Vector3(0, 0, -.055f));

            void DrawAt(GlyphBoard board, Vector3 local)
            {
                ObserveNib(board.transform.TransformPoint(local), board.transform.rotation);
                pen.Advance(clock);
            }

            void ObserveNib(Vector3 nib, Quaternion rotation)
            {
                Vector3 offset = pen.penRoot.InverseTransformPoint(pen.nib.position);
                Observe(nib - rotation * offset, rotation, false);
            }

            void Step(Vector3 hand, Quaternion rotation, bool pinched)
            {
                Observe(hand, rotation, pinched);
                pen.Advance(clock);
            }

            void Observe(Vector3 grip, Quaternion rotation, bool pinched, Vector3? trackedWrist = null)
            {
                clock += .016;
                var rightOnly = new PlayerHandObservation
                {
                    rightValid = true, rightObserved = clock, gripPoseValid = true,
                    headPosition = head, headForward = this.headForward, rightWrist = trackedWrist ?? grip, rightTip = grip,
                    gripPosition = grip, gripRotation = rotation, pinchValid = true,
                    systemPinch = true, systemPressed = pinched
                };
                pen.Observe(rightOnly, clock);
            }
        }

        // 在单帧回放的每个判定点比较实际出现的粒子实例和音源，也能捕获重复播放或失败时先行播放。
        sealed class CompletionMedia
        {
            readonly GlyphTemplate active, story;
            readonly HashSet<int> knownVfx = new();
            readonly HashSet<int> knownAudio = new();
            int priorActive, priorStory;

            public CompletionMedia(GlyphTemplate active, GlyphTemplate story)
            {
                this.active = active;
                this.story = story;
                foreach (var item in Particles()) knownVfx.Add(item.GetInstanceID());
                foreach (var item in Sources()) knownAudio.Add(item.GetInstanceID());
            }

            IEnumerable<ParticleSystem> Particles() => Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Where(p => (!p.transform.parent || !p.transform.parent.GetComponentInParent<ParticleSystem>()) &&
                    (Matches(p, active.completionVfx) || Matches(p, story.completionVfx)));

            IEnumerable<AudioSource> Sources() => Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Where(source => source.clip && (source.clip == active.completionSfx || source.clip == story.completionSfx));

            static bool Matches(ParticleSystem instance, ParticleSystem prefab)
            {
                var renderer = instance.GetComponent<ParticleSystemRenderer>();
                var original = prefab.GetComponent<ParticleSystemRenderer>();
                return renderer && original && renderer.sharedMaterial == original.sharedMaterial &&
                    instance.main.maxParticles == prefab.main.maxParticles &&
                    Mathf.Approximately(instance.main.duration, prefab.main.duration);
            }

            public void Check(int activeCount, int storyCount, GlyphBoard activeBoard, GlyphBoard storyBoard, string stage)
            {
                var newVfx = Particles().Where(p => knownVfx.Add(p.GetInstanceID())).ToArray();
                var newAudio = Sources().Where(a => knownAudio.Add(a.GetInstanceID())).ToArray();
                int expected = activeCount - priorActive + storyCount - priorStory;
                Require(newVfx.Length == expected && newAudio.Length == expected,
                    $"{stage}：新出现的模板粒子 {newVfx.Length}、音源 {newAudio.Length}，各应为 {expected}");
                if (expected == 1)
                {
                    bool activeFired = activeCount > priorActive;
                    var template = activeFired ? active : story;
                    var anchor = (activeFired ? activeBoard : storyBoard).transform;
                    var vfx = newVfx[0];
                    var audio = newAudio[0];
                    Require(Matches(vfx, template.completionVfx) && vfx.isPlaying &&
                        Vector3.Distance(vfx.transform.position, anchor.position) < .001f &&
                        Quaternion.Angle(vfx.transform.rotation, anchor.rotation) < .1f,
                        stage + "：正确模板的粒子在画板中心并沿板面朝向播放");
                    Require(audio.clip == template.completionSfx && Vector3.Distance(audio.transform.position, anchor.position) < .001f,
                        stage + "：正确模板的指定音效从画板中心发出");
                }
                priorActive = activeCount;
                priorStory = storyCount;
            }
        }

        static void CheckBoardMeshes(GlyphBoard board, bool story)
        {
            // 验证合成笔迹产生的实际 CanvasRenderer 网格。单次编辑器回放在一帧内发出整幅笔迹。
            var labels = board.GetComponentsInChildren<Text>();
            foreach (var label in labels)
                if (label.font.dynamic) label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            Canvas.ForceUpdateCanvases();
            foreach (var label in labels)
            {
                Mesh mesh = label.canvasRenderer.GetMesh();
                Require(!string.IsNullOrWhiteSpace(label.text) && mesh && mesh.vertexCount >= 4, "中文标签实际字形网格：" + label.name);
            }
            var strokes = board.GetComponentsInChildren<GlyphStrokeGraphic>();
            var guide = Array.FindAll(strokes, graphic => graphic.name == "范本");
            var ink = Array.FindAll(strokes, graphic => graphic.name == "朱砂墨迹");
            Require(guide.Length == 1 && ink.Length == 1, "板面必须各有一层范本与笔迹");
            foreach (var graphic in strokes)
                if (story || graphic.name == "朱砂墨迹")
                {
                    Mesh mesh = graphic.canvasRenderer.GetMesh();
                    Require(mesh && mesh.vertexCount >= 12, "实际笔迹网格：" + graphic.name);
                }
        }

        static void Screenshot(string name, Vector3 eye, Vector3 focus)
        {
            var gameplay = Camera.main;
            if (!gameplay || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            // XR 未连接时游戏相机可能在地面，回放中只移动这台复制渲染设置的相机。
            var go = new GameObject("墨规笔合成眼位验收相机");
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(gameplay);
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.orthographic = false;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.fieldOfView = 68f;
            camera.nearClipPlane = .02f;
            camera.aspect = 4f / 3f;
            camera.ResetProjectionMatrix();
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(focus - eye, Vector3.up));
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1200, 900, 24);
            Texture2D texture = null;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0,0,target.width,target.height), 0, 0);
                texture.Apply();
                string path = Path.Combine(LogDir, name + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("GlyphPlayChecks 截图 " + Path.GetFullPath(path));
            }
            catch (Exception e) { Debug.LogWarning("GlyphPlayChecks 截图跳过：" + e.Message); }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(go);
                if (texture) Object.Destroy(texture);
            }
        }
    }
}
