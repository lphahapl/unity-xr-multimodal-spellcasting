using System;
using NarutoVR.SealCombat.Rpg;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class GlyphPhysicsChecks
    {
        public static void Run()
        {
            bool stepDynamics = Application.isPlaying;
            var scene = stepDynamics
                ? SceneManager.CreateScene("千钧符独立物理回归_" + Guid.NewGuid().ToString("N"),
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D))
                : EditorSceneManager.NewPreviewScene();
            var settings = ScriptableObject.CreateInstance<QianjunEffectDefinition>();
            GlyphEffectInstance effect = null;
            int checks = 0;
            const float dt = .02f;
            void Check(bool value, string label)
            {
                if (!value) throw new InvalidOperationException("千钧符物理：" + label);
                ++checks;
            }
            GameObject Item(string name, Vector3 position)
            {
                var item = new GameObject(name);
                SceneManager.MoveGameObjectToScene(item, scene);
                item.transform.position = position;
                return item;
            }
            try
            {
                CheckWalkingTether(scene, settings, Check, stepDynamics);
                Vector3 origin = new Vector3(23, 7, 18);
                var player = Item("试验持笔人", origin);
                var context = new GlyphEffectContext {
                    owner = player.transform, hand = origin, tip = origin, body = origin,
                    forward = Vector3.forward, tracked = true, observed = 3
                };
                var rock = Item("试验石块", origin + Vector3.forward * 2);
                rock.AddComponent<BoxCollider>();
                var heavy = rock.AddComponent<GlyphHeavyBody>();
                var body = heavy.Body;
                body.mass = 4;
                var wall = Item("不受符力的遮挡", origin + Vector3.forward);
                wall.AddComponent<BoxCollider>().size = new Vector3(1, 1, .1f);
                Physics.SyncTransforms();
                effect = settings.Begin(context);
                effect.Tick(context, dt);
                Check(!player.GetComponentInChildren<LineRenderer>(), "实墙阻挡锁定");

                wall.transform.SetParent(player.transform, true);
                Physics.SyncTransforms();
                context.observed += dt;
                effect.Tick(context, dt);
                var line = player.GetComponentInChildren<LineRenderer>();
                Check(line && line.enabled, "忽略玩家自身碰撞体并创建一条线");
                Vector3 localHit = rock.transform.InverseTransformPoint(line.GetPosition(1));
                Check((localHit - new Vector3(0, 0, -.5f)).magnitude < .002f, "线端在笔尖命中的表面");

                Vector3 Step(Vector3 handTravel, Vector3 bodyTravel)
                {
                    Vector3 before = body.GetAccumulatedForce(dt);
                    context.hand += handTravel;
                    context.body += bodyTravel;
                    context.observed += dt;
                    effect.Tick(context, dt);
                    effect.FixedTick(dt);
                    return body.GetAccumulatedForce(dt) - before;
                }
                Check(Step(Vector3.forward * .015f, Vector3.zero).z > 0, "手前推向自由刚体施加正向力");
                Check(Step(Vector3.back * .015f, Vector3.zero).z < 0, "手回拉向自由刚体施加反向力");
                Check(Step(Vector3.zero, Vector3.zero).sqrMagnitude < .0001f, "持笔手静止无持续施力");
                Check(Step(Vector3.forward * .012f, Vector3.forward * .012f).sqrMagnitude < .0001f, "身体和手一起靠近时绳松、不推石块");
                Check(Step(Vector3.right * .008f, Vector3.zero).sqrMagnitude < .0001f, "手沿切向轻晃不推石块");
                Step(Vector3.left * .008f, Vector3.zero);
                heavy.localAxis = Vector3.right;
                Check(Step(Vector3.forward * .015f, Vector3.zero).sqrMagnitude < .0001f, "垂直于导轨的牵引被投影掉");
                heavy.localAxis = new Vector3(1, 0, 1);
                Vector3 diagonal = Step(Vector3.forward * .015f, Vector3.zero);
                Check(diagonal.z > 0 && Mathf.Abs(diagonal.x - diagonal.z) < .001f, "沿指定的局部斜轴施力");

                context.forward = Vector3.left;
                rock.transform.SetPositionAndRotation(rock.transform.position + Vector3.right, Quaternion.Euler(0, 37, 0));
                Step(Vector3.zero, Vector3.zero);
                Check((line.GetPosition(1) - rock.transform.TransformPoint(localHit)).sqrMagnitude < .000001f, "移开笔尖后线端跟随表面旋转和平移");
                Check(line.GetPosition(0) == context.hand, "线起点是持笔手");
                var beforeLoss = body.GetAccumulatedForce(dt);
                context.hand += Vector3.forward * .02f;
                context.observed += dt;
                effect.Tick(context, dt);
                context.tracked = false;
                effect.Tick(context, dt);
                effect.FixedTick(dt);
                Check(line.enabled && body.GetAccumulatedForce(dt) == beforeLoss, "整帧失追时保留锁点与连线、立即丢弃未执行的力");
                context.tracked = true;
                Check(Step(Vector3.back * .09f, Vector3.zero).sqrMagnitude < .0001f, "重新追踪以新基线恢复");

                var velocity = body.linearVelocity;
                effect.End();
                effect.End();
                Check(effect.IsComplete && (!line || !line.enabled), "可重复断线并移除连线");
                Check(!body.isKinematic && body.useGravity && body.linearVelocity == velocity, "断线保留重力、刚体速度");
                if (stepDynamics)
                {
                    CheckSimulatedMotion(scene, settings, Check);
                    CheckLongHaul(scene, settings, Check);
                }
                Debug.Log($"GlyphPhysicsChecks PASS {checks}");
            }
            finally
            {
                effect?.End();
                Object.DestroyImmediate(settings);
                if (stepDynamics) SceneManager.UnloadSceneAsync(scene);
                else EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void CheckWalkingTether(Scene scene, QianjunEffectDefinition settings, Action<bool, string> check, bool simulate)
        {
            const float dt = .02f;
            var owner = new GameObject("走动牵绳试验_玩家");
            var stone = new GameObject("走动牵绳试验_重物");
            SceneManager.MoveGameObjectToScene(owner, scene);
            SceneManager.MoveGameObjectToScene(stone, scene);
            Vector3 start = new Vector3(2, 4, -12);
            owner.transform.position = start;
            stone.transform.position = start + Vector3.forward * 2;
            stone.AddComponent<BoxCollider>();
            var heavy = stone.AddComponent<GlyphHeavyBody>();
            var body = heavy.Body;
            body.mass = 4;
            body.useGravity = false;
            Physics.SyncTransforms();
            var context = new GlyphEffectContext {
                owner = owner.transform, body = start, hand = start, tip = start, observed = 1,
                forward = Vector3.forward, tracked = true, bodyTracked = true
            };
            var effect = settings.Begin(context);
            Vector3 Advance(float bodyZ, float handZ)
            {
                context.body += Vector3.forward * bodyZ;
                context.hand += Vector3.forward * handZ;
                context.observed += dt;
                effect.Tick(context, dt);
                var before = body.GetAccumulatedForce(dt);
                effect.FixedTick(dt);
                return body.GetAccumulatedForce(dt) - before;
            }
            try
            {
                effect.Tick(context, dt);
                var line = owner.GetComponentInChildren<LineRenderer>();
                check(line && line.enabled, "走动试验锁住初始表面锚点");
                context.forward = Vector3.back;
                stone.transform.position += Vector3.up * .45f;
                context.hand.y += .04f;
                context.body.y += .04f;
                check(Advance(0,0).sqrMagnitude < .0001f, "上下台阶时头手一起升降，不沿倾斜绳路产生额外手势力");
                context.body.y += .02f;
                check(Advance(0,0).sqrMagnitude < .0001f, "持笔手未动时单独抬头不产生手势力");
                stone.transform.position -= Vector3.up * .45f;
                context.hand = context.body = start;
                context.rebaseHand = true;
                Advance(0,0);
                context.rebaseHand = false;
                var pull = Vector3.zero;
                for (int i = 0; i < 10; ++i) pull = Advance(-.02f, -.02f);
                check(pull.z < -1f && pull.magnitude <= settings.maxForce + .01f, "背离重物行走拉紧绳索并施加有限张力");
                check(effect.IsTethered && !effect.IsComplete, "转身后仍是同一段锁绳");
                check(Advance(0, 0).z < -1f, "停下时仍绷紧的绳继续拉向玩家");
                heavy.localAxis = Vector3.right;
                check(Advance(0, 0).sqrMagnitude < .0001f, "走动的绳力也服从石块指定的导轨");
                heavy.localAxis = Vector3.zero;
                Vector3 combined = Advance(-.02f, -.08f);
                check(combined.z < pull.z && combined.magnitude <= settings.maxForce + .01f,
                    "手再回拉会增加牵引，两者合力仍受同一个最大力约束");
                context.tracked = false;
                var lastHand = line.GetPosition(0);
                var before = Advance(-.04f, 0);
                check(before.z < -1 && Vector3.Distance(line.GetPosition(0), lastHand + Vector3.back * .04f) < .0001f,
                    "手失追后仅通过可靠的身体位移继续牵引，线起点随身体移动");
                float estimatedHandY = line.GetPosition(0).y;
                context.body.y += .06f;
                Advance(0, 0);
                check(Mathf.Abs(line.GetPosition(0).y - estimatedHandY) < .0001f,
                    "遮手时上下动头不会改变估算握点高度");
                context.bodyTracked = false;
                check(Advance(-.03f, 0).sqrMagnitude < .0001f && effect.IsTethered, "双重失追停止力并保留原锁点");
                context.tracked = context.bodyTracked = true;
                check(Advance(0, -.18f).sqrMagnitude < .0001f && effect.IsTethered, "全失追后首帧重建绳长且不补力");
                check(Advance(0, 0).sqrMagnitude < .0001f, "恢复后静止没有旧拉力");
                check(Advance(.1f,.1f).sqrMagnitude < .0001f, "人和手一起走近产生松绳，不会把石块顶走");
                context.hand += Vector3.back * .015f;
                context.observed += dt;
                effect.Tick(context, dt);
                var beforeSwap = body.GetAccumulatedForce(dt);
                context.rebaseHand = true;
                context.hand += Vector3.forward * .09f;
                effect.Tick(context, dt);
                effect.FixedTick(dt);
                check(body.GetAccumulatedForce(dt) == beforeSwap, "同一观测切换握点会丢弃旧冲量，不把位置差算成手势力");
                context.rebaseHand = false;
                check(Advance(0, -.01f).z < 0, "切换握点后新的真实手势仍然拉动重物");
                check(Advance(-2.5f,-2.5f).sqrMagnitude < .0001f && Advance(0,0).sqrMagnitude < .0001f,
                    "玩家传送后绳长重设，随后无迟发冲量");
                if (simulate)
                {
                    // 上述检查读取物理引擎内的待加力。清空后单独验证走路和手遮挡的真实位移。
                    var world = scene.GetPhysicsScene();
                    world.Simulate(dt);
                    body.linearVelocity = Vector3.zero;
                    float initialZ = body.position.z;
                    context.tracked = false;
                    for (int i = 0; i < 28; ++i) { Advance(-.018f, 0); world.Simulate(dt); }
                    check(body.position.z < initialZ - .06f && body.linearVelocity.z < -.1f,
                        "持笔手暂时不可见时向后走，物理步进实际拖动重物");
                    context.tracked = true;
                    context.rebaseHand = true;
                    float speedBefore = body.linearVelocity.magnitude;
                    Advance(0, -.49f);
                    world.Simulate(dt);
                    check(body.linearVelocity.magnitude <= speedBefore + settings.maxForce * dt / body.mass + .001f,
                        "重新看见手时不会把遮挡期间的位移作为额外冲量");
                    context.rebaseHand = false;
                    var obstacle = new GameObject("牵引碰撞试验墙");
                    SceneManager.MoveGameObjectToScene(obstacle, scene);
                    try
                    {
                        obstacle.transform.position = body.position + Vector3.back * .78f;
                        obstacle.AddComponent<BoxCollider>().size = new Vector3(3, 3, .1f);
                        Physics.SyncTransforms();
                        float contactZ = obstacle.transform.position.z + .05f + .5f;
                        for (int i = 0; i < 70; ++i) { Advance(-.02f, -.02f); world.Simulate(dt); }
                        check(body.position.z >= contactZ - .025f && body.position.z < contactZ + .05f,
                            "持续后退时石块被实体墙挡住");
                    }
                    finally { Object.DestroyImmediate(obstacle); }
                }
                for (int i = 0; i < 12; ++i) effect.FixedTick(dt);
                var stale = body.GetAccumulatedForce(dt);
                effect.Tick(context, dt);
                effect.FixedTick(dt);
                check(body.GetAccumulatedForce(dt) == stale, "重复过期的旧观测不会继续施力");
                effect.End();
                check(!effect.IsTethered, "主动释放后清除锁绳状态");
            }
            finally { effect.End(); Object.DestroyImmediate(owner); Object.DestroyImmediate(stone); }
        }

        static void CheckLongHaul(Scene scene, QianjunEffectDefinition settings, Action<bool, string> check)
        {
            const float dt = .02f;
            var physics = scene.GetPhysicsScene();
            var floor = new GameObject("长距离牵引试验地面");
            var owner = new GameObject("长距离牵引试验持笔人");
            var stone = new GameObject("长距离牵引试验石块");
            foreach (var obj in new[] { floor, owner, stone }) SceneManager.MoveGameObjectToScene(obj, scene);
            Vector3 origin = new Vector3(40, .50f, 0);
            floor.transform.position = origin + Vector3.down * .60f;
            var floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(5, .2f, 32);
            owner.transform.position = origin;
            stone.transform.position = origin + Vector3.forward * 2f;
            var stoneCollider = stone.AddComponent<BoxCollider>();
            var body = stone.AddComponent<GlyphHeavyBody>().Body;
            body.mass = 4.5f;
            var context = new GlyphEffectContext {
                owner = owner.transform, body = origin, hand = origin, tip = origin,
                tracked = true, bodyTracked = true, observed = 1, forward = Vector3.forward
            };
            var effect = settings.Begin(context);
            try
            {
                Physics.SyncTransforms();
                effect.Tick(context, dt);
                check(effect.IsTethered, "地面重物长距离试验取得锁点");
                var line = owner.GetComponentInChildren<LineRenderer>();
                float initialDistance = Vector3.Distance(line.GetPosition(0), line.GetPosition(1));
                context.forward = Vector3.left;
                void WalkTo(float destination)
                {
                    while (Mathf.Abs(context.body.z - destination) > .0001f)
                    {
                        float nextZ = Mathf.MoveTowards(context.body.z, destination, dt);
                        context.body.z = context.hand.z = nextZ;
                        context.observed += dt;
                        effect.Tick(context, dt);
                        effect.FixedTick(dt);
                        physics.Simulate(dt);
                    }
                }
                float CurrentDistance() => Vector3.Distance(line.GetPosition(0), line.GetPosition(1));
                WalkTo(-8f);
                float outward = CurrentDistance();
                check(body.position.z < -5f && outward < initialDistance + .55f,
                    $"拖过初始工作区仍保持绳长，手Z={context.hand.z:F2}，石块Z={body.position.z:F2}，线长={outward:F2}");
                WalkTo(7f);
                float returning = CurrentDistance();
                check(body.position.z > 4f && returning < initialDistance + .55f,
                    "玩家反向越过最初锁点后，原石块仍会被拖至另一侧");
                WalkTo(-4f);
                float groundGap = stoneCollider.bounds.min.y - floorCollider.bounds.max.y;
                check(effect.IsTethered && body.position.z < -1f && CurrentDistance() < initialDistance + .55f &&
                    Mathf.Abs(groundGap) < .04f,
                    $"再次转向后跨回起点，锁定={effect.IsTethered}，石块Z={body.position.z:F3}，离地={groundGap:F3}，线长={CurrentDistance():F3}");
                Debug.Log($"GlyphPhysicsLongHaul rest={initialDistance:F3}m, -8m={outward:F3}m, +7m={returning:F3}m, -4m={CurrentDistance():F3}m, groundGap={groundGap:F4}m");
            }
            finally
            {
                effect.End();
                Object.DestroyImmediate(owner); Object.DestroyImmediate(stone); Object.DestroyImmediate(floor);
            }
        }

        static void CheckSimulatedMotion(Scene scene, QianjunEffectDefinition settings, Action<bool, string> check)
        {
            const float dt = .02f;
            var physics = scene.GetPhysicsScene();
            check(physics.IsValid() && physics != Physics.defaultPhysicsScene, "物理步进只运行在隔离场景");

            for (int fixture = 0; fixture < 2; ++fixture)
            {
                bool constrained = fixture == 1;
                Vector3 origin = new Vector3(-12 + 5 * fixture, 5, 6);
                var owner = new GameObject("推拉运动试验_手");
                var stone = new GameObject(constrained ? "斜向导轨运动试验" : "自由推拉运动试验");
                SceneManager.MoveGameObjectToScene(owner, scene);
                SceneManager.MoveGameObjectToScene(stone, scene);
                owner.transform.position = origin;
                stone.transform.position = origin + Vector3.forward * 2;
                stone.AddComponent<BoxCollider>();
                var heavy = stone.AddComponent<GlyphHeavyBody>();
                if (constrained)
                {
                    heavy.localAxis = Vector3.forward;
                    stone.transform.rotation = Quaternion.Euler(0, 45, 0);
                }
                var body = heavy.Body;
                body.mass = 4;
                body.linearDamping = 0;
                Physics.SyncTransforms();
                var context = new GlyphEffectContext {
                    owner = owner.transform, tracked = true, hand = origin, tip = origin,
                    body = origin, forward = Vector3.forward, observed = 5
                };
                var effect = settings.Begin(context);
                try
                {
                    effect.Tick(context, dt);
                    check(owner.GetComponentInChildren<LineRenderer>(), "步进前锁定重物表面");
                    void Advance(float travel)
                    {
                        context.hand += Vector3.forward * travel;
                        context.observed += dt;
                        effect.Tick(context, dt);
                        effect.FixedTick(dt);
                        physics.Simulate(dt);
                    }
                    var start = body.position;
                    for (int i = 0; i < 4; i++) Advance(.01f);
                    var displacement = body.position - start;
                    check(displacement.z > .01f && body.linearVelocity.z > .1f, "真实物理步进产生前向位移与速度");
                    check(displacement.y < -.002f && body.linearVelocity.y < 0, "牵引时仍受重力下落");
                    if (constrained)
                        check(displacement.x > .01f && Mathf.Abs(displacement.x - displacement.z) < .001f,
                            "刚体沿旋转后的局部斜轴移动");
                    else
                        check(Mathf.Abs(displacement.x) < .001f, "自由石块沿手至命中点的方向移动");

                    for (int i = 0; i < 7; i++) Advance(-.014f);
                    float beforeFinalPull = body.position.z;
                    Advance(-.014f);
                    check(body.linearVelocity.z < -.1f && body.position.z < beforeFinalPull,
                        "真实回拉使刚体反向并向玩家靠近");
                    effect.End();
                    var released = body.linearVelocity;
                    float releaseHeight = body.position.y;
                    physics.Simulate(dt);
                    check(body.position.y < releaseHeight && body.linearVelocity.y < released.y
                        && Mathf.Abs(body.linearVelocity.z - released.z) < .001f, "断开后重力继续加速并保留水平惯性");
                    Debug.Log($"GlyphPhysicsMotion {(constrained ? "斜轨" : "自由")} push={displacement.ToString("F4")} pullVz={released.z:F4} releaseVy={released.y:F4}->{body.linearVelocity.y:F4}");
                }
                finally { effect.End(); }
            }
        }
    }
}
