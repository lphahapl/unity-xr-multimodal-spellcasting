#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Events;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>保留用户动画剪辑，修正状态连线；生成独立敌人预制体，不保存当前场景。</summary>
    [InitializeOnLoad]
    public static class EnemySetupAndChecks
    {
        public const string ControllerPath = "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller";
        public const string PrefabPath = "Assets/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab";
        const string ArtPrefab = "Assets/Art/Enemies/CanJiOu/CanJiOu_AutoLOD.prefab";
        const string TestScene = "Assets/Scene/EnemyNavigationTest.unity";
        const string TestKey = "NarutoVR.Enemy.CheckRunning", PreviousStart = "NarutoVR.Enemy.PreviousStartScene", PreviousPause = "NarutoVR.Enemy.PreviousPause";
        static double checkAt;
        static EnemySetupAndChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(TestKey, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) checkAt = EditorApplication.timeSinceStartup + 1;
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousStart, ""));
                    EditorApplication.isPaused = SessionState.GetBool(PreviousPause, false);
                    SessionState.EraseBool(PreviousPause);
                    SessionState.EraseBool(TestKey); SessionState.EraseString(PreviousStart);
                }
            };
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(TestKey, false) || !Application.isPlaying || checkAt <= 0 || EditorApplication.timeSinceStartup < checkAt) return;
                checkAt = 0;
                try
                {
                    string report = Run();
                    File.WriteAllText("Logs/enemy-fsm-20261104/check.txt", DateTimeOffset.Now.ToString("O") + "\nRUNNING 逻辑检查完成，等待真实帧导航移动\n");
                    BeginMovementProbe(report);
                }
                catch (Exception e)
                {
                    Directory.CreateDirectory("Logs/enemy-fsm-20261104");
                    File.WriteAllText("Logs/enemy-fsm-20261104/check.txt", DateTimeOffset.Now.ToString("O") + "\nFAILED\n" + e);
                    Debug.LogException(e);
                    EditorApplication.isPlaying = false;
                }
            };
        }

        [MenuItem("Tools/NarutoVR/敌人/修复动画并生成敌人预制体")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play 再配置敌人资产。");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller) throw new InvalidOperationException("找不到残机偶 Animator Controller");
            Directory.CreateDirectory("Logs/enemy-fsm-20261104/before");
            string backup = "Logs/enemy-fsm-20261104/before/CanJiOu.controller";
            if (!File.Exists(backup)) File.Copy(ControllerPath, backup);
            Undo.RegisterCompleteObjectUndo(controller, "修复敌人 Animator");
            var root = controller.layers[0].stateMachine;
            if (root.states.Any(s => s.state.name != "CanJiOu_Idle" && s.state.name != "Run"))
                throw new InvalidOperationException("检测到扩展状态，禁止旧两招生成器重建。请使用增量适配新反应状态或仅迁移 EnemyObj。");
            var patrol = root.stateMachines.Single(s => s.stateMachine.name == "patrol").stateMachine;
            var turn = root.stateMachines.Single(s => s.stateMachine.name == "Turn").stateMachine;
            var attacks = root.stateMachines.Single(s => s.stateMachine.name == "Attack").stateMachine;
            var idle = root.states.Single(s => s.state.name == "CanJiOu_Idle").state;
            var run = root.states.Single(s => s.state.name == "Run").state;
            var walk = patrol.states.Single(s => s.state.name == "walk").state;
            var patrolIdle = patrol.states.Single(s => s.state.name == "CanJiOu_Idle").state;
            var left = turn.states.Single(s => s.state.name.Contains("Left")).state;
            var right = turn.states.Single(s => s.state.name.Contains("Right")).state;
            var basic = attacks.states.Single(s => s.state.name == "挥砍").state;
            var spin = attacks.states.Single(s => s.state.name == "转圈").state;
            foreach (var machine in new[] { root, patrol, turn, attacks })
            {
                foreach (var state in machine.states)
                    foreach (var transition in state.state.transitions) state.state.RemoveTransition(transition);
                foreach (var transition in machine.entryTransitions) machine.RemoveEntryTransition(transition);
                foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
                foreach (var child in machine.stateMachines)
                    foreach (var transition in machine.GetStateMachineTransitions(child.stateMachine)) machine.RemoveStateMachineTransition(child.stateMachine, transition);
                EditorUtility.SetDirty(machine);
            }
            EnsureParameter(controller, "isFindPlayer", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "angle", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "isAttacking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "attack1", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "motionState", AnimatorControllerParameterType.Int);
            root.defaultState = idle; patrol.defaultState = walk; turn.defaultState = right; attacks.defaultState = basic;
            var entry = attacks.AddEntryTransition(spin);
            entry.AddCondition(AnimatorConditionMode.If, 0, "attack1");
            var leftEntry = turn.AddEntryTransition(left);
            leftEntry.AddCondition(AnimatorConditionMode.Less, -3, "angle");
            var moving = new[] { idle, patrolIdle, walk, run, left, right };
            foreach (var state in moving)
            {
                state.writeDefaultValues = false;
                var attack = state.AddTransition(attacks);
                ConfigureTransition(attack);
                attack.AddCondition(AnimatorConditionMode.If, 0, "isAttacking");
                AddMotionTransition(state, idle, 0);
                AddMotionTransition(state, walk, 1);
                AddMotionTransition(state, run, 2);
                AddMotionTransition(state, left, 3, -1);
                AddMotionTransition(state, right, 3, 1);
                EditorUtility.SetDirty(state);
            }
            foreach (var state in new[] { basic, spin })
            {
                state.tag = "EnemyAttack"; state.writeDefaultValues = false;
                var exit = state.AddTransition(idle); ConfigureTransition(exit);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0, "isAttacking");
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            // 原美术 prefab 保留。新 prefab 增加导航和 AI，不触碰正在编辑的场景实例。
            var go = PrefabUtility.LoadPrefabContents(File.Exists(PrefabPath) ? PrefabPath : ArtPrefab);
            try
            {
                go.name = "残机偶_导航敌人";
                var anim = go.GetComponent<Animator>();
                anim.runtimeAnimatorController = controller; anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var agent = go.GetComponent<NavMeshAgent>();
                if (!agent) agent = go.AddComponent<NavMeshAgent>();
                agent.radius = .3f; agent.height = 1.8f; agent.speed = 2; agent.angularSpeed = 120;
                agent.acceleration = 8; agent.stoppingDistance = 1.36f; agent.updateRotation = false;
                var enemy = go.GetComponent<EnemyController>();
                if (!enemy) enemy = go.AddComponent<EnemyController>();
                enemy.animator = anim;
                enemy.BindEntityComponents();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            Debug.Log("敌人 Animator 已修复；独立预制体已生成，当前场景未保存或替换。");
        }

        [MenuItem("Tools/NarutoVR/敌人/仅迁移 EnemyObj 组件")]
        public static void MigrateEntityPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后迁移。");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var controller = root.GetComponent<EnemyController>();
                if (!controller) throw new InvalidOperationException("敌人 prefab 缺少 Controller。");
                var oldAnimator = controller.animator;
                string before = EditorJsonUtility.ToJson(controller);
                controller.BindEntityComponents();
                string after = EditorJsonUtility.ToJson(controller);
                string ConfigOnly(string json) => System.Text.RegularExpressions.Regex.Replace(json,
                    "\"(enemyObj|legacyAnimator)\":\\{[^}]*\\}", "\"$1\":null");
                if (ConfigOnly(before) != ConfigOnly(after) || (oldAnimator && controller.Entity.Animator != oldAnimator))
                    throw new InvalidOperationException("迁移改变了原配置/事件或动画引用，拒绝保存。");
                ConfigureCombatPrefab(root, controller.Entity);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Directory.CreateDirectory("Logs/enemy-obj-refactor-20261104");
                File.WriteAllText("Logs/enemy-obj-refactor-20261104/migration.txt", DateTimeOffset.Now.ToString("O") +
                    "\nPASSED 原 Controller 配置/事件序列化一致；动画引用保留；EnemyObj 已绑定。");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("EnemyObj 接线迁移完成，未重建 Animator 或保存当前场景。");
        }

        static void ConfigureCombatPrefab(GameObject root, EnemyObj entity)
        {
            const string folder = "Assets/ScriptableObjects/敌人战斗配置";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/ScriptableObjects", "敌人战斗配置");
            const string path = folder + "/CanJiOu_Combat.asset";
            var definition = AssetDatabase.LoadAssetAtPath<EnemyCombatDefinition>(path);
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<EnemyCombatDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            if (!entity.CombatDefinition) entity.CombatDefinition = definition;
            if (!entity.DetectionSensor)
            {
                var sensorObject = root.transform.Find("近距探测范围");
                if (!sensorObject) { sensorObject = new GameObject("近距探测范围").transform; sensorObject.SetParent(root.transform, false); }
                var sphere = sensorObject.GetComponent<SphereCollider>();
                if (!sphere) { sphere = sensorObject.gameObject.AddComponent<SphereCollider>(); sphere.radius = 2.5f; sphere.center = Vector3.up; }
                sphere.isTrigger = true;
                var body = sensorObject.GetComponent<Rigidbody>();
                if (!body) body = sensorObject.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true; body.useGravity = false;
                var sensor = sensorObject.GetComponent<EnemyDetectionSensor>();
                if (!sensor) sensor = sensorObject.gameObject.AddComponent<EnemyDetectionSensor>();
                sensor.owner = entity; entity.DetectionSensor = sensor;
            }
            var head = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mixamorig:Head");
            if (!head) throw new InvalidOperationException("未找到头骨，拒绝把后脑弱点挂到错误位置。");
            if (!root.transform.Find("身体受击区"))
            {
                var body = new GameObject("身体受击区"); body.transform.SetParent(root.transform, false);
                var collider = body.AddComponent<CapsuleCollider>(); collider.height = 1.5f; collider.radius = .25f; collider.center = Vector3.up * .75f;
                body.AddComponent<EnemyHitZone>().owner = entity;
            }
            void HeadZone(string name, bool weak, Vector3 worldOffset, float radius)
            {
                if (head.Find(name)) return; // 重跑不覆盖用户调好的区域和倍率。
                var zoneObject = new GameObject(name); zoneObject.transform.SetParent(head, false);
                zoneObject.transform.position = head.position + worldOffset;
                var sphere = zoneObject.AddComponent<SphereCollider>(); sphere.isTrigger = weak;
                Vector3 scale = zoneObject.transform.lossyScale;
                sphere.radius = radius / Mathf.Max(.0001f, Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                var zone = zoneObject.AddComponent<EnemyHitZone>(); zone.owner = entity; zone.isWeakPoint = weak;
            }
            HeadZone("头部受击区", false, root.transform.up * .04f, .16f);
            HeadZone("后脑弱点", true, root.transform.up * .04f - root.transform.forward * .16f, .11f);
            if (!root.TryGetComponent<EnemyPoiseBar>(out var bar)) bar = root.AddComponent<EnemyPoiseBar>();
            bar.enemy = entity;
            EditorUtility.SetDirty(entity); EditorUtility.SetDirty(bar);
        }

        [MenuItem("Tools/NarutoVR/敌人/增量适配 Hit Seated Dead")]
        public static void AdaptReactionStates()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后适配。");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var root = controller.layers[0].stateMachine;
            var idle = root.states.Single(s => s.state.name == "CanJiOu_Idle").state;
            var hit = root.states.Single(s => s.state.name == "Hit").state;
            var seated = root.states.Single(s => s.state.name == "Seated").state;
            var dead = root.states.Single(s => s.state.name == "Dead").state;
            EnsureParameter(controller, "dead", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "isSeated", AnimatorControllerParameterType.Trigger);
            foreach (var state in new[] { dead, seated, hit })
            {
                // 只调整已命名的反应入口，保留其它 AnyState/普通移动/攻击连线与全部剪辑引用。
                foreach (var old in root.anyStateTransitions.Where(t => t.destinationState == state).ToArray()) root.RemoveAnyStateTransition(old);
                var transition = root.AddAnyStateTransition(state); ConfigureTransition(transition);
                if (state == dead) transition.AddCondition(AnimatorConditionMode.If, 0, "dead");
                else
                {
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "dead");
                    transition.AddCondition(AnimatorConditionMode.If, 0, state == hit ? "Hit" : "isSeated");
                    foreach (var old in state.transitions.Where(t => t.destinationState == idle).ToArray()) state.RemoveTransition(old);
                    var exit = state.AddTransition(idle); ConfigureTransition(exit);
                    exit.hasExitTime = true; exit.exitTime = .98f;
                    exit.AddCondition(AnimatorConditionMode.IfNot, 0, "dead");
                }
                state.tag = state == dead ? "EnemyDead" : "EnemyReaction";
                EditorUtility.SetDirty(state);
            }
            root.anyStateTransitions = root.anyStateTransitions.OrderBy(t => t.destinationState == dead ? 0 : t.destinationState == seated ? 1 : t.destinationState == hit ? 2 : 3).ToArray();
            EditorUtility.SetDirty(root); EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            MigrateEntityPrefab();
            Debug.Log("新反应状态已增量适配，原移动与攻击连线保持。");
        }

        static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == name);
            if (existing != null && existing.type != type) throw new InvalidOperationException("Animator 参数类型错误：" + name);
            if (existing == null) controller.AddParameter(name, type);
        }

        static void ConfigureTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false; transition.duration = .08f;
            transition.hasFixedDuration = true; transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.None;
        }

        static void AddMotionTransition(AnimatorState from, AnimatorState to, int motion, int side = 0)
        {
            if (from == to) return;
            var transition = from.AddTransition(to); ConfigureTransition(transition);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "isAttacking");
            transition.AddCondition(AnimatorConditionMode.Equals, motion, "motionState");
            if (side < 0) transition.AddCondition(AnimatorConditionMode.Less, -3, "angle");
            if (side > 0) transition.AddCondition(AnimatorConditionMode.Greater, 3, "angle");
        }

        [MenuItem("Tools/NarutoVR/敌人/验证导航与攻击状态")]
        public static void RunMenu() => StartChecks();

        public static void StartChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            if (!File.Exists(TestScene))
            {
                var original = SceneManager.GetActiveScene();
                var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(test, TestScene); EditorSceneManager.CloseScene(test, true);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
            SessionState.SetString(PreviousStart, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(TestKey, true);
            SessionState.SetBool(PreviousPause, EditorApplication.isPaused);
            EditorApplication.isPaused = false;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene);
            EditorApplication.isPlaying = true;
        }

        static void BeginMovementProbe(string logicReport)
        {
            Vector3 origin = new(5000, 0, 5000);
            var sources = new List<NavMeshBuildSource> { new() { shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(30, .2f, 30), transform = Matrix4x4.TRS(origin + Vector3.down * .1f, Quaternion.identity, Vector3.one), area = 0 } };
            NavMeshData nav = null; NavMeshDataInstance registration = default;
            GameObject go = null, point = null;
            bool previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            var xrManager = XRGeneralSettings.Instance ? XRGeneralSettings.Instance.Manager : null;
            var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
            bool restoreXr = xrManager && xrManager.isInitializationComplete && displays.Any(d => d.running);
            if (restoreXr) xrManager.StopSubsystems();
            EditorApplication.CallbackFunction update = null;
            void Finish(Exception error)
            {
                EditorApplication.update -= update;
                if (go) Object.DestroyImmediate(go);
                if (point) Object.DestroyImmediate(point);
                if (registration.valid) registration.Remove();
                if (nav) Object.DestroyImmediate(nav);
                Application.runInBackground = previousBackground;
                if (restoreXr && xrManager) xrManager.StartSubsystems();
                string result = error == null ? logicReport + "\n真实帧导航 3 项通过（巡逻位移及转向、召唤追击位移、目标丢失停稳）。" : "FAILED\n" + error;
                File.WriteAllText("Logs/enemy-fsm-20261104/check.txt", DateTimeOffset.Now.ToString("O") + "\n" + result);
                if (error == null) Debug.Log(result); else Debug.LogException(error);
                EditorApplication.isPlaying = false;
            }
            try
            {
                nav = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                    new Bounds(origin, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
                registration = NavMesh.AddNavMeshData(nav);
                go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), origin, Quaternion.identity);
                point = new GameObject("运动测试目标"); point.transform.position = origin + Vector3.right * 4;
                var enemy = go.GetComponent<EnemyController>();
                enemy.InitializeSpawn(null, false); enemy.patrolPoints = new[] { point.transform };
                double deadline = EditorApplication.timeSinceStartup + 20;
                float stageAt = Time.time; int stage = 0; Vector3 before = go.transform.position;
                update = () =>
                {
                    try
                    {
                        if (!Application.isPlaying || !go || EditorApplication.timeSinceStartup > deadline)
                            throw new Exception($"真实帧导航被中断或超时 stage={stage}, gameTime={Time.time}, stageAt={stageAt}, paused={EditorApplication.isPaused}, timeScale={Time.timeScale}");
                        if (Time.time - stageAt < (stage == 2 ? .5f : 1.5f)) return;
                        if (stage == 0)
                        {
                            if (go.transform.position.x - before.x < .3f || Vector3.Dot(go.transform.forward, Vector3.right) < .7f)
                                throw new Exception("真实帧巡逻没有移动或转向");
                            point.transform.position = origin + Vector3.forward * 8;
                            enemy.InitializeSpawn(point.transform, true);
                        }
                        else if (stage == 1)
                        {
                            if (go.transform.position.z - before.z < .3f) throw new Exception("真实帧召唤追击没有移动");
                            enemy.SetTarget(null);
                        }
                        else
                        {
                            if (Vector3.Distance(go.transform.position, before) > .02f || enemy.CurrentBehaviour != EnemyController.Behaviour.Idle)
                                throw new Exception($"失去目标后未停稳待机 distance={Vector3.Distance(go.transform.position, before):F4}, state={enemy.CurrentBehaviour}, velocity={enemy.Agent.velocity}, reason={enemy.Status}");
                            EditorApplication.update -= update;
                            SkillEffectChecks.Begin((passed, message) =>
                            {
                                logicReport += "\n" + message;
                                Finish(passed ? null : new Exception(message));
                            });
                            return;
                        }
                        ++stage; stageAt = Time.time; before = go.transform.position;
                    }
                    catch (Exception e) { Finish(e); }
                };
                EditorApplication.update += update;
            }
            catch (Exception e) { Finish(e); }
        }

        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("导航检查需要通过专用空场景进入 Play。");
            int count = 0;
            void Check(bool ok, string reason) { if (!ok) throw new Exception("敌人检查失败：" + reason); ++count; }
            var originalScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("敌人隔离验证");
            var data = default(NavMeshDataInstance);
            NavMeshData navData = null;
            GameObject go = null;
            var randomState = UnityEngine.Random.state;
            try
            {
                Vector3 origin = new(4000, 0, 4000);
                var sources = new List<NavMeshBuildSource> { new() { shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(30, .2f, 30), transform = Matrix4x4.TRS(origin + Vector3.down * .1f, Quaternion.identity, Vector3.one), area = 0 } };
                var settings = NavMesh.GetSettingsByIndex(0);
                navData = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
                Check(navData, "构建隔离导航网格"); data = NavMesh.AddNavMeshData(navData);
                go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), origin, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(go, scene);
                var enemy = go.GetComponent<EnemyController>();
                var agent = go.GetComponent<NavMeshAgent>();
                enemy.tickAutomatically = false; enemy.checkLineOfSight = false;
                go.transform.position = origin;
                Check(NavMesh.SamplePosition(origin, out var sample, 2, NavMesh.AllAreas), "隔离导航面可采样");
                Check(agent.Warp(sample.position), "敌人可放置在导航面");
                enemy.ResetForSpawn(); enemy.animator.Rebind(); enemy.animator.Update(0);
                Check(enemy.ValidateAnimator(out _), "Animator 参数与默认攻击状态");
                Check(enemy.Entity && enemy.Entity == go.GetComponent<EnemyObj>() &&
                    enemy.Entity.Animator == enemy.animator && enemy.Entity.Agent == agent, "实体组件与兼容入口使用同一引用");
                enemy.BindEntityComponents(); enemy.BindEntityComponents();
                Check(go.GetComponents<EnemyObj>().Length == 1, "重复接线不增加组件或重置业务事件");
                Check(!enemy.animator.applyRootMotion && !agent.updateRotation, "单一位移转向所有者");
                Check(EnemyController.HorizontalAngle(Vector3.forward, Vector3.left) < -89, "左转有符号角");
                Check(EnemyController.HorizontalAngle(Vector3.forward, Vector3.right + Vector3.up * 10) > 89, "右转角忽略高度");
                int first = 0, began = 0, ended = 0, contacts = 0;
                int busFirst = 0, busBegan = 0, busEnded = 0, busContacts = 0;
                bool allAttackIdsPresent = true;
                using var firstSubscription = EventCenter.Subscribe(EnemyEvents.FirstPlayerDetected, e => { if (e.Enemy == enemy) ++busFirst; });
                using var beginSubscription = EventCenter.Subscribe(EnemyEvents.AttackStarted, e => { if (e.Enemy == enemy) { ++busBegan; allAttackIdsPresent &= !string.IsNullOrEmpty(e.AttackId); } });
                using var endSubscription = EventCenter.Subscribe(EnemyEvents.AttackFinished, e => { if (e.Enemy == enemy) ++busEnded; });
                using var contactSubscription = EventCenter.Subscribe(EnemyEvents.AttackWindowOpened, e => { if (e.Enemy == enemy) ++busContacts; });
                enemy.onFirstPlayerDetected.AddListener(() => ++first);
                enemy.onAttackStarted.AddListener(_ => ++began);
                enemy.onAttackFinished.AddListener(_ => ++ended);
                enemy.onAttackContact.AddListener(_ => ++contacts);
                var target = new GameObject("检查目标").transform; SceneManager.MoveGameObjectToScene(target.gameObject, scene);
                target.position = origin + Vector3.forward * 5;
                var patrol = new GameObject("检查巡逻点").transform; SceneManager.MoveGameObjectToScene(patrol.gameObject, scene);
                patrol.position = origin + Vector3.right * 4; enemy.patrolPoints = new[] { patrol };
                var actualAttacks = new HashSet<int>();
                void Step(int frames = 1)
                {
                    for (int i = 0; i < frames; i++)
                    {
                        enemy.Tick(.05f); enemy.animator.Update(.05f);
                        var state = enemy.animator.GetCurrentAnimatorStateInfo(0);
                        if (enemy.CurrentAttack >= 0 && state.IsTag("EnemyAttack") && !enemy.animator.IsInTransition(0))
                        {
                            int expected = Animator.StringToHash(enemy.CurrentAttack == 0 ? "挥砍" : "转圈");
                            if (state.shortNameHash != expected) throw new Exception("随机攻击编号与实际动画不符");
                            actualAttacks.Add(enemy.CurrentAttack);
                        }
                    }
                }
                enemy.InitializeSpawn(null, true); Step();
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Idle && !agent.hasPath, "召唤无目标不使用已配巡逻点");
                enemy.SetTarget(target); Step(4);
                Check(enemy.HasTarget && first == 1 && enemy.CurrentBehaviour == EnemyController.Behaviour.Chase, "召唤直接追击并触发一次首次事件");
                Check(agent.pathStatus == NavMeshPathStatus.PathComplete && agent.hasPath, "追击路径完整");
                enemy.SetTarget(null); Step(); enemy.SetTarget(target); Step();
                Check(first == 1, "换目标与重新发现不重复首次事件");
                enemy.SetSuspended(true); Step(); Check(!agent.hasPath && enemy.CurrentBehaviour == EnemyController.Behaviour.Suspended, "外部暂停停导航");
                enemy.SetSuspended(false); enemy.InitializeSpawn(null, false); Step(4);
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Patrol && agent.hasPath, "普通敌人未发现时巡逻");
                target.position = origin - Vector3.forward * 5; go.transform.rotation = Quaternion.identity;
                enemy.SetTarget(target); Step(); Check(!enemy.HasTarget, "背后目标不通过首次视野");
                target.position = origin + Vector3.forward * 5; Step(); Check(enemy.HasTarget && first == 2, "正前方发现事件新轮可重触发");
                enemy.loseSightSeconds = .1f; target.position = origin + Vector3.forward * 100; Step(4);
                Check(!enemy.HasTarget && enemy.CurrentBehaviour == EnemyController.Behaviour.Patrol, "普通敌人丢失后恢复巡逻");
                enemy.InitializeSpawn(target, true); target.position = origin + Vector3.forward * 1.2f + Vector3.up * 1.6f;
                go.transform.rotation = Quaternion.identity;
                // 固定种子重跑，覆盖两个实际动画入口及每轮冷却。
                UnityEngine.Random.InitState(31); var seen = new HashSet<int>();
                for (int attack = 0; attack < 12; attack++)
                {
                    int prior = began; Step(100);
                    if (enemy.CurrentAttack >= 0) seen.Add(enemy.CurrentAttack);
                    Check(began > prior, "攻击周期可重复 " + attack);
                }
                Check(ended > 0 && contacts > 0, "实际动画完成、接触与冷却后重攻击");
                Check(actualAttacks.Count == 2, "0默认挥砍与1Trigger转圈均实际播放");
                enemy.SetTarget(null); Step(); Check(enemy.CurrentAttack == -1 && !enemy.animator.GetBool("isAttacking"), "丢目标取消攻击并清标志");
                enemy.InitializeSpawn(target, true);
                UnityEngine.Events.UnityAction pause = () => enemy.SetSuspended(true);
                enemy.onFirstPlayerDetected.AddListener(pause); int before = began; Step(10);
                Check(began == before && enemy.CurrentBehaviour == EnemyController.Behaviour.Suspended, "首次事件暂停不漏出攻击");
                enemy.onFirstPlayerDetected.RemoveListener(pause);
                enemy.InitializeSpawn(target, true);
                UnityEngine.Events.UnityAction hideTarget = () => target.gameObject.SetActive(false);
                enemy.onFirstPlayerDetected.AddListener(hideTarget); before = began; Step(4);
                Check(began == before && !enemy.HasTarget, "首次回调禁用目标不继续攻击");
                enemy.onFirstPlayerDetected.RemoveListener(hideTarget); target.gameObject.SetActive(true);
                enemy.InitializeSpawn(target, true); enemy.attackCooldown = 0;
                UnityEngine.Events.UnityAction<int> reenter = _ => enemy.Tick(.05f);
                enemy.onAttackStarted.AddListener(reenter); before = began; int contactBefore = contacts;
                Step(250);
                Check(began > before + 1 && contacts - contactBefore <= began - before, "零冷却和回调重入不重复接触或卡住");
                enemy.onAttackStarted.RemoveListener(reenter);
                enemy.InitializeSpawn(target, true); Step(5);
                Check(enemy.CurrentAttack >= 0, "重生测试前进入攻击");
                UnityEngine.Events.UnityAction<int> respawn = _ => enemy.InitializeSpawn(null, true);
                enemy.onAttackFinished.AddListener(respawn); enemy.SetTarget(null); Step();
                Check(!enemy.HasTarget && !enemy.HasEverDetectedPlayer && enemy.CurrentBehaviour == EnemyController.Behaviour.Idle,
                    "攻击结束回调重生后旧生命周期不覆盖新状态");
                enemy.onAttackFinished.RemoveListener(respawn);
                enemy.InitializeSpawn(null, false); patrol.position = go.transform.position;
                enemy.patrolPoints = new[] { patrol }; Step();
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Idle && enemy.Status.Contains("停留"), "巡逻点在脚下也进入停留");
                enemy.InitializeSpawn(target, true); target.position = origin + Vector3.right * 100; Step();
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Idle && !agent.hasPath && enemy.Status.Contains("路径"), "不可达目标停止并报告");
                enemy.InitializeSpawn(null, false); enemy.patrolPoints = new Transform[] { null }; Step();
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Idle, "全空巡逻点安全待机");
                agent.enabled = false; target.position = origin + Vector3.forward * 5;
                enemy.InitializeSpawn(target, true); Step();
                Check(!agent.enabled && enemy.Status.Contains("NavMesh"), "禁用导航组件不抛异常并说明原因");
                agent.enabled = true; agent.Warp(origin); go.transform.rotation = Quaternion.identity;
                enemy.checkLineOfSight = true; enemy.InitializeSpawn(target, false);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(wall, scene);
                wall.transform.position = origin + new Vector3(0, 1, 2.5f); wall.transform.localScale = new Vector3(3, 3, .5f);
                Physics.SyncTransforms(); before = first; Step();
                Check(!enemy.HasTarget && first == before, "实体遮挡阻止首次发现");
                wall.SetActive(false); Physics.SyncTransforms(); Step();
                Check(enemy.HasTarget && first == before + 1, "解除遮挡后首次发现");
                wall.SetActive(true); Physics.SyncTransforms(); Step(4);
                Check(!enemy.HasTarget, "已发现目标被遮挡超过宽限后丢失");
                wall.SetActive(false); Physics.SyncTransforms(); enemy.checkLineOfSight = false;
                enemy.reactionTimeout = 100; // 证明由动画退出恢复，而非较短超时掩盖连线错误。
                enemy.InitializeSpawn(target, true); Step(4);
                Check(enemy.Entity.SupportsReactions && enemy.TryNotifyHit(), "新 Hit 状态接入并暂停导航");
                Check(!enemy.TryNotifyHit() && !agent.hasPath, "同级受击不重复入队且导航已停");
                Step(4);
                Check(enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Hit"), "实际播放 Hit");
                Check(enemy.TryEnterSeated() && !enemy.TryNotifyHit(), "Seated 覆盖 Hit，Hit 不覆盖 Seated");
                Step(4); Check(enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Seated"), "实际播放 Seated");
                Step(200); Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Chase, "坐地动画播完恢复追击");
                Check(enemy.TryNotifyHit(), "恢复后可再次受击"); Step(200);
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Chase, "受击动画播完恢复追击");
                target.position = origin + Vector3.forward; go.transform.rotation = Quaternion.identity;
                enemy.InitializeSpawn(target, true); Step(6);
                UnityEngine.Events.UnityAction<int> clearTargetOnEnd = _ => enemy.SetTarget(null);
                enemy.onAttackFinished.AddListener(clearTargetOnEnd);
                Check(enemy.CurrentAttack >= 0 && enemy.TryDie(), "死亡打断正在执行的攻击");
                enemy.onAttackFinished.RemoveListener(clearTargetOnEnd);
                Check(enemy.CurrentAttack == -1 && enemy.IsDead && !agent.hasPath, "死亡停止攻击和导航");
                Check(enemy.animator.GetBool("dead"), "攻击结束回调换目标不破坏死亡动画与行动锁一致性");
                Check(!enemy.TryNotifyHit() && !enemy.TryEnterSeated() && !enemy.TryDie(), "死亡拒绝受击、坐地及重复死亡");
                Step(200);
                Check(enemy.CurrentBehaviour == EnemyController.Behaviour.Dead && enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Dead"), "死亡动画持续停留");
                enemy.InitializeSpawn(null, true); Step(4);
                Check(!enemy.IsDead && !enemy.animator.GetBool("dead") && enemy.CurrentBehaviour == EnemyController.Behaviour.Idle, "重新生成清除死亡锁与参数");
                Check(busFirst == first && busBegan == began && busEnded == ended && busContacts == contacts, "Inspector 事件与事件中心逐次一致");
                Check(allAttackIdsPresent && busBegan > 0, "开始通知携带攻击唯一ID");
                string report = $"PASSED 敌人状态/动画/导航 {count} 项通过；事件中心 {EventCenterChecks.Run()} 项通过；" + EnemyAttackChecks.Run() + "；" + EnemyCombatChecks.Run() + "；" + PlayerObjChecks.Run() + "；" + EnemyDetectionChecks.Run() + $"；既有技能效果 {SkillEffectChecks.Run()} 项通过；隔离 Play 合成输入测试，非头显验收。";
                Directory.CreateDirectory("Logs/enemy-fsm-20261104");
                File.WriteAllText("Logs/enemy-fsm-20261104/check.txt", DateTimeOffset.Now.ToString("O") + "\n" + report);
                return report;
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (go) Object.DestroyImmediate(go);
                if (data.valid) data.Remove();
                if (navData) Object.DestroyImmediate(navData);
                SceneManager.UnloadSceneAsync(scene);
                if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
            }
        }
    }
}
#endif
