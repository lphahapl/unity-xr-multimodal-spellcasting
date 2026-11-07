#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;
using Lab = NarutoVR.SealLab.SealLab;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>在隔离对象上执行播放模式效果检查，覆盖生成、发射、碰撞与清理。</summary>
    public static class SkillEffectChecks
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static AsyncSweep activeSweep;

        public static int Run()
        {
            RequirePlaying();
            int passed = 0;
            using (var f = new Fixture())
            {
                f.RefreshHands();
                True(f.Effects.TryActivate(f.Fire), "当前有效右手应能生成火球");
                var ball = f.Held();
                Vector3 expected = f.Origin.TransformPoint(f.RightPosition) + f.Head.rotation * f.Effects.heldOffset;
                Near(expected, ball.transform.position, "右手火球必须使用录制原点转换后的当前腕部位置");
                True(ball.GetComponent<SkillProjectile>() == null, "生成当帧不能进入飞行");
                var collider = ball.GetComponent<Collider>();
                True(!collider || !collider.enabled, "持握火球不能有伤害碰撞体");
                passed++;

                int activations = f.Effects.ActivationCount;
                True(!f.Effects.TryActivate(f.Fire), "槽位占用时必须拒绝第二个火球");
                Equal(activations, f.Effects.ActivationCount, "拒绝生成不得计入成功激活");
                True(f.Effects.CancelLatest() && !f.Effects.HasHeldObject, "取消应收起持握火球");
                True(!f.Effects.CancelLatest(), "无效果时取消应返回false");
                passed++;

                f.Fire.leftHand = true;
                f.RefreshHands();
                True(f.Effects.TryActivate(f.Fire), "配置左手时应使用有效左手");
                ball = f.Held();
                Vector3 leftOffset = f.Effects.heldOffset; leftOffset.x = -leftOffset.x;
                Near(f.Origin.TransformPoint(f.LeftPosition) + f.Head.rotation * leftOffset,
                    ball.transform.position, "左手火球位置不正确");
                f.Effects.CancelLatest();
                f.RefreshHands(leftTracked: false);
                True(!f.Effects.TryActivate(f.Fire), "左手失效时不能自动换用右手");
                passed++;

                f.Fire.leftHand = false;
                f.RefreshHands(age: .5);
                True(!f.Effects.TryActivate(f.Fire), "即使fresh标记未更新，也不能使用过期腕部位置");
                f.RefreshHands();
                True(f.Effects.TryActivate(f.Fire), "恢复当前追踪后应能生成");
                ball = f.Held();
                f.RefreshHands(rightTracked: false);
                Invoke(f.Effects, "LateUpdate");
                True(!ball.activeSelf && f.Effects.HasHeldObject, "追踪丢失应立即隐藏，宽限期间仍占用槽位");
                Set(f.Effects, "lostAt", Time.realtimeSinceStartupAsDouble - .6);
                Invoke(f.Effects, "LateUpdate");
                True(!f.Effects.HasHeldObject, "追踪丢失超时必须消散");
                passed++;

                True(f.Effects.TryActivate(f.Buff), "护盾激活失败"); f.TrackBuff();
                Near(100, f.Effects.ShieldPoints, "新护盾数值");
                Near(0, f.Effects.AbsorbDamage(30), "护盾应完全吸收30点伤害");
                Near(70, f.Effects.ShieldPoints, "受击后的护盾数值");
                True(f.Effects.TryActivate(f.Buff), "护盾刷新失败");
                Near(100, f.Effects.ShieldPoints, "刷新应恢复到100而不是叠加");
                Near(30, f.Effects.AbsorbDamage(130), "超过护盾的伤害必须返回余量");
                Near(0, f.Effects.ShieldPoints, "护盾耗尽后不应保留数值");
                Near(0, f.Effects.BuffRemaining, "护盾耗尽后应结束效果");
                passed++;

                f.RefreshHands();
                True(f.Effects.TryActivate(f.Buff), "取消优先级测试护盾激活失败"); f.TrackBuff();
                True(f.Effects.TryActivate(f.Fire), "取消优先级测试火球激活失败"); f.Held();
                True(f.Effects.CancelLatest() && !f.Effects.HasHeldObject, "取消应优先处理持握火球");
                Near(100, f.Effects.ShieldPoints, "收起火球不得同时取消护盾");
                True(f.Effects.CancelLatest(), "第二次取消应结束护盾");
                Near(0, f.Effects.ShieldPoints, "护盾取消后应清零");
                passed++;

                Vector3 collisionPoint = Fixture.Base + Vector3.right * 10;
                var target = f.Target(collisionPoint, Vector3.one * .4f);
                target.gameObject.AddComponent<SphereCollider>().radius = .2f;
                Physics.SyncTransforms();
                int targetCallbacks = 0;
                var p = f.Projectile(collisionPoint);
                p.Initialize(Vector3.forward, f.Root.transform, ~0, 0, _ => targetCallbacks++, null);
                True(p.IsFinished, "发射点已重叠靶子时必须立即结束");
                Near(75, target.CurrentHealth, "原点重叠应命中一次");
                Equal(1, target.HitCount, "同一靶子的多个碰撞体不得重复伤害");
                Invoke(p, "Update"); p.Finish();
                Equal(1, targetCallbacks, "结束后重复更新不得重复回调");
                Near(75, target.CurrentHealth, "结束后不得持续伤害");
                passed++;

                Vector3 wallPoint = Fixture.Base + Vector3.right * 15;
                var blockedTarget = f.Target(wallPoint, Vector3.one * .4f);
                f.Cube("检查_重叠墙", wallPoint, Vector3.one * .2f);
                Physics.SyncTransforms();
                p = f.Projectile(wallPoint);
                p.Initialize(Vector3.forward, f.Root.transform, ~0, 0,
                    _ => throw new Exception("墙重叠时不应回调目标命中"), null);
                True(p.IsFinished, "墙体应阻挡火球");
                Near(100, blockedTarget.CurrentHealth, "墙与靶同时包含发射点时不得穿墙伤害");
                passed++;

                Vector3 aim = f.Head.position + f.Head.forward * 3;
                var aimTarget = f.Target(aim, Vector3.one * .4f);
                Physics.SyncTransforms();
                Near(f.Head.position + f.Head.forward * 2.8f, f.Effects.GetAimPoint(),
                    "准星应命中头向射线上最近靶子表面");
                passed++;
            }
            return passed;
        }

        /// <summary>Verifies real Update movement against thin targets and a nearer wall across frames.</summary>
        public static void Begin(Action<bool, string> completed)
        {
            RequirePlaying();
            if (activeSweep != null) throw new InvalidOperationException("火球跨帧检查已在执行。");
            activeSweep = new AsyncSweep(completed);
            try { activeSweep.Start(); }
            catch { activeSweep.Dispose(); activeSweep = null; throw; }
        }

        private static void RequirePlaying()
        {
            if (!Application.isPlaying || Time.timeScale <= 0)
                throw new InvalidOperationException("技能效果检查必须在未暂停的播放模式中执行。");
        }
        private static void True(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Equal(int expected, int actual, string message) => True(expected == actual, $"{message}：预期{expected}，实际{actual}");
        private static void Near(float expected, float actual, string message) => True(Mathf.Abs(expected - actual) < .001f, $"{message}：预期{expected}，实际{actual}");
        private static void Near(Vector3 expected, Vector3 actual, string message) => True(Vector3.Distance(expected, actual) < .002f, $"{message}：预期{expected}，实际{actual}");
        private static object Get(object value, string field) => value.GetType().GetField(field, PrivateInstance).GetValue(value);
        private static void Set(object value, string field, object data) => value.GetType().GetField(field, PrivateInstance).SetValue(value, data);
        private static void Invoke(object value, string method) => value.GetType().GetMethod(method, PrivateInstance).Invoke(value, null);

        private sealed class Fixture : IDisposable
        {
            public static readonly Vector3 Base = new(100, 100, 100);
            public readonly GameObject Root;
            public readonly Transform Origin, Head;
            public readonly Lab Lab;
            public readonly SkillEffects Effects;
            public readonly SkillDefinition Fire, Buff;
            public readonly Vector3 RightPosition = new(.25f, 1.2f, .45f), LeftPosition = new(-.25f, 1.2f, .45f);
            private readonly List<Object> objects = new();
            private int version;

            public Fixture()
            {
                Root = Own(new GameObject("检查_独立玩家")); Root.transform.position = Base;
                Origin = new GameObject("检查_录制原点").transform;
                Origin.SetParent(Root.transform, false); Origin.localRotation = Quaternion.Euler(0, 30, 0);
                Head = new GameObject("检查_头显").transform;
                Head.SetParent(Root.transform, false); Head.localPosition = Vector3.up * 1.6f;
                Lab = Root.AddComponent<Lab>(); Lab.recordingOrigin = Origin;
                Effects = Root.AddComponent<SkillEffects>(); Effects.lab = Lab; Effects.head = Head;
                Effects.playerRoot = Root.transform;
                // Give the reflection-created lab no frame loop: this fixture is synchronous and
                // destroys it before Unity can call its hardware-dependent Start method.
                Fire = Own(ScriptableObject.CreateInstance<SkillDefinition>());
                Fire.skillId = "check.fire"; Fire.displayName = "检查火球";
                Fire.effectKind = SkillEffectKind.HeldFireball; Fire.durationSeconds = 10;
                Buff = Own(ScriptableObject.CreateInstance<SkillDefinition>());
                Buff.skillId = "check.buff"; Buff.displayName = "检查护盾";
                Buff.effectKind = SkillEffectKind.SelfBuff; Buff.durationSeconds = 10;
            }

            private T Own<T>(T item) where T : Object { objects.Add(item); return item; }
            public void RefreshHands(bool leftTracked = true, bool rightTracked = true, double age = 0)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                var frame = new SealFrame { time = now };
                frame.left = Hand(LeftPosition, leftTracked, now - age);
                frame.right = Hand(RightPosition, rightTracked, now - age);
                typeof(Lab).GetProperty("Frame").SetValue(Lab, frame);
            }
            private HandSnapshot Hand(Vector3 position, bool tracked, double observed)
            {
                var hand = new HandSnapshot { connected = true, tracked = tracked, fresh = true,
                    highConfidence = true, observedAt = observed, version = ++version };
                int index = (int)HandJointId.HandWristRoot;
                hand.joints[index] = new Pose(position, Quaternion.identity); hand.jointValid[index] = tracked;
                return hand;
            }
            public GameObject Held()
            {
                var ball = (GameObject)Get(Effects, "heldObject");
                if (ball && !objects.Contains(ball)) objects.Add(ball);
                return ball;
            }
            public void TrackBuff()
            {
                var ring = (GameObject)Get(Effects, "buffObject");
                if (ring && !objects.Contains(ring)) objects.Add(ring);
            }
            public GameObject Cube(string name, Vector3 position, Vector3 scale)
            {
                var go = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); go.name = name;
                go.transform.SetPositionAndRotation(position, Quaternion.identity); go.transform.localScale = scale;
                return go;
            }
            public SkillTrainingTarget Target(Vector3 position, Vector3 scale) => Cube("检查_训练靶", position, scale).AddComponent<SkillTrainingTarget>();
            public SkillProjectile Projectile(Vector3 position)
            {
                var go = Own(new GameObject("检查_投射物")); go.transform.position = position;
                return go.AddComponent<SkillProjectile>();
            }
            public void Dispose()
            {
                if (Effects) Effects.ClearAll();
                for (int i = objects.Count - 1; i >= 0; --i) if (objects[i]) Object.DestroyImmediate(objects[i]);
                objects.Clear(); Physics.SyncTransforms();
            }
        }

        private sealed class AsyncSweep : IDisposable
        {
            private readonly Action<bool, string> completed;
            private readonly List<GameObject> objects = new();
            private SkillProjectile direct, blocked;
            private SkillTrainingTarget directTarget, blockedTarget;
            private int directHits, blockedHits, startFrame;
            private double deadline;
            private bool ended;
            private bool previousRunInBackground;
            public AsyncSweep(Action<bool, string> completed) { this.completed = completed; }
            public void Start()
            {
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                Vector3 start = Fixture.Base + Vector3.right * 25;
                var player = Make("检查_玩家碰撞忽略", start + Vector3.forward * .35f, new Vector3(.5f, .5f, .02f));
                directTarget = Make("检查_薄靶", start + Vector3.forward, new Vector3(.5f, .5f, .015f)).AddComponent<SkillTrainingTarget>();
                Vector3 second = start + Vector3.right * 3;
                Make("检查_薄墙", second + Vector3.forward * .5f, new Vector3(.5f, .5f, .015f));
                blockedTarget = Make("检查_墙后靶", second + Vector3.forward, new Vector3(.5f, .5f, .015f)).AddComponent<SkillTrainingTarget>();
                Physics.SyncTransforms();
                direct = Projectile(start, player.transform, _ => directHits++);
                blocked = Projectile(second, player.transform, _ => blockedHits++);
                deadline = EditorApplication.timeSinceStartup + 5;
                startFrame = Time.frameCount;
                EditorApplication.update += Poll;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
            }
            private GameObject Make(string name, Vector3 position, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(go); go.name = name;
                go.transform.position = position; go.transform.localScale = scale; return go;
            }
            private SkillProjectile Projectile(Vector3 position, Transform ignored, Action<SkillTrainingTarget> hit)
            {
                var go = new GameObject("检查_高速球"); objects.Add(go); go.transform.position = position;
                var p = go.AddComponent<SkillProjectile>(); p.speed = 1000; p.lifetime = 3;
                p.Initialize(Vector3.forward, ignored, ~0, 0, hit, null); return p;
            }
            private void Poll()
            {
                try
                {
                    if (!Application.isPlaying) { Finish(false, "退出播放模式，跨帧检查已清理"); return; }
                    if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "跨帧投射物检查超时"); return; }
                    if (Time.frameCount <= startFrame) return;
                    if ((!direct || direct.IsFinished) && (!blocked || blocked.IsFinished))
                    {
                        Equal(1, directHits, "高速球必须跨过被忽略的玩家碰撞体并命中薄靶一次");
                        Equal(0, blockedHits, "近处薄墙必须阻止命中远处靶子");
                        Near(75, directTarget.CurrentHealth, "高速球伤害值");
                        Near(100, blockedTarget.CurrentHealth, "薄墙后的靶子生命值");
                        Finish(true, "跨帧连续检测通过：高速薄靶命中一次、忽略玩家、薄墙阻挡");
                    }
                }
                catch (Exception error) { Finish(false, error.ToString()); }
            }
            private void Abort() => Finish(false, "程序集重新加载，跨帧检查已清理");
            private void Finish(bool passed, string message)
            {
                if (ended) return;
                ended = true;
                Dispose(); activeSweep = null; completed?.Invoke(passed, message);
            }
            public void Dispose()
            {
                EditorApplication.update -= Poll;
                AssemblyReloadEvents.beforeAssemblyReload -= Abort;
                Application.runInBackground = previousRunInBackground;
                foreach (var go in objects) if (go) Object.DestroyImmediate(go);
                objects.Clear(); Physics.SyncTransforms();
            }
        }
    }
}
#endif
