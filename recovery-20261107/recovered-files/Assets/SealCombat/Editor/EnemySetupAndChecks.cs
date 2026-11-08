#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>保留用户动画剪辑，修正状态连线；生成独立敌人预制体，不保存当前场景。</summary>
    public static class EnemySetupAndChecks
    {
        public const string ControllerPath = "Assets/SealCombat/Art/Enemies/CanJiOu/Animations/CanJiOu.controller";
        public const string PrefabPath = "Assets/SealCombat/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab";
        const string ArtPrefab = "Assets/SealCombat/Art/Enemies/CanJiOu/CanJiOu_AutoLOD.prefab";

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
                controller.BindEntityComponents();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("EnemyObj 接线迁移完成，未重建 Animator 或保存当前场景。");
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
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("此检查在编辑模式运行。");
            int count = 0;
            void Check(bool ok, string reason) { if (!ok) throw new Exception("敌人检查失败：" + reason); ++count; }
            var originalScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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
                go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                var enemy = go.GetComponent<EnemyController>();
                var agent = go.GetComponent<NavMeshAgent>();
                enemy.tickAutomatically = false; enemy.checkLineOfSight = false;
                go.transform.position = origin;
                Check(agent.Warp(origin), "敌人可放置在导航面");
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
                agent.enabled = false; enemy.SetTarget(target); Step();
                Check(!agent.enabled, "禁用导航组件不抛异常");
                string report = $"敌人状态/动画/导航 {count} 项通过；隔离编辑器测试，非头显验收。";
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
                EditorSceneManager.CloseScene(scene, true);
                if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
            }
        }
    }
}
#endif
