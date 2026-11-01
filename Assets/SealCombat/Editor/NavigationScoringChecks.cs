#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NarutoVR.SealCombat.Rpg;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Deterministic score and actual controller/Physics checks; synthetic samples are not hardware accuracy measurements.</summary>
    public static class NavigationScoringChecks
    {
        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("导航评分检查须在编辑模式运行");
            int count = 0;
            var settings = new NavigationScoringSettings();
            Vector3 head = Vector3.up * 1.7f, hand = new Vector3(-.3f, 1.2f, .4f);
            Vector3 near = Vector3.forward * 1.5f, far = Vector3.forward * 3;
            NavigationScore Score(Vector3 target, Vector3 desired, bool ray, Vector3 rayDirection, Vector3? heading = null) =>
                NavigationTargetScoring.Evaluate(settings, Vector3.zero, target, desired, head, heading ?? Vector3.forward,
                    ray, hand, rayDirection);
            var n = Score(near, Vector3.forward, false, Vector3.zero);
            var f = Score(far, Vector3.forward, false, Vector3.zero);
            Require(n.total > f.total && !n.handRayUsed, "无手射线仍能选点，同行优先近点"); count++;
            n = Score(near, Vector3.forward, true, far - hand);
            f = Score(far, Vector3.forward, true, far - hand);
            Require(f.total - n.total > settings.switchAdvantage && f.handRay > .999f,
                "准确指向远点应能超过近距离偏好与换点门槛"); count++;
            var back = Score(-near, Vector3.back, true, -near - hand);
            Require(!back.headIntentUsed && back.total > .8f, "面向前方时后退不被头向扣分"); count++;
            var bad = Score(near, Vector3.forward, true, new Vector3(float.NaN, 0, 0));
            Require(float.IsFinite(bad.total) && !bad.handRayUsed && Mathf.Abs(bad.total - Score(near, Vector3.forward, false, Vector3.zero).total) < .0001f,
                "无效射线必须移除权重并归一，不能制造 NaN"); count++;
            var reverse = Score(near, Vector3.forward, true, hand - near);
            Require(reverse.handRay == 0 && reverse.total > 0, "指向背面的射线只失去加分，不能硬拒绝合法方向"); count++;
            var shift = new Vector3(15, 3, -22);
            var translated = NavigationTargetScoring.Evaluate(settings, shift, far + shift, Vector3.forward,
                head + shift, Vector3.forward, true, hand + shift, far - hand);
            Require(Mathf.Abs(translated.total - f.total) < .0001f, "评分必须对世界平移不变"); count++;
            Quaternion rotation = Quaternion.Euler(0, 90, 0);
            var rotated = NavigationTargetScoring.Evaluate(settings, Vector3.zero, rotation * far, rotation * Vector3.forward,
                rotation * head, rotation * Vector3.forward, true, rotation * hand, rotation * (far - hand));
            Require(Mathf.Abs(rotated.total - f.total) < .0001f, "评分必须随玩家朝向旋转，不能绑定世界正前方"); count++;
            Require(!new NavigationScoringSettings { directionWeight = float.NaN }.Valid &&
                !new NavigationScoringSettings { directionWeight = 0, distanceWeight = 0 }.Valid &&
                !new NavigationScoringSettings { directionWeight = float.MaxValue, distanceWeight = float.MaxValue }.Valid,
                "配置必须保留无射线/后退时的有效有限权重"); count++;
            count += CheckController();
            return count;
        }

        static int CheckController()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            var world = new GameObject("导航评分回归_临时场景") { hideFlags = HideFlags.HideAndDontSave };
            var registered = new List<NavigationNode>();
            const int layer = 30;
            Vector3 origin = new Vector3(7000, 0, 7000);
            GameObject Box(string name, Vector3 position, Vector3 size)
            {
                var go = new GameObject(name) { layer = layer, hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(world.transform, false); go.transform.position = position;
                go.AddComponent<BoxCollider>().size = size; return go;
            }
            NavigationNode Node(string id, Vector3 position)
            {
                var go = new GameObject(id) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(world.transform, false); go.transform.position = position;
                var node = go.AddComponent<NavigationNode>(); node.nodeId = id;
                NavigationNodeFixture.SetRegistered(node,true); registered.Add(node); return node;
            }
            try
            {
                Box("地面", origin + Vector3.down * .1f, new Vector3(10, .2f, 10));
                var rig = new GameObject("玩家原点"); rig.transform.SetParent(world.transform, false); rig.transform.position = origin;
                var head = new GameObject("头部"); head.transform.SetParent(rig.transform, false); head.transform.localPosition = Vector3.up * 1.7f;
                var controller = world.AddComponent<RpgLocomotionController>(); controller.rigRoot = rig.transform; controller.head = head.transform;
                controller.floorLayers = controller.obstacleLayers = 1 << layer;
                var start = Node("check.start", origin);
                var near = Node("check.near", origin + Vector3.forward * 1.5f);
                var far = Node("check.far", origin + Vector3.forward * 3);
                var rear = Node("check.rear", origin + Vector3.back * 1.5f);
                start.neighbors.AddRange(new[] { near, far, rear });
                Physics.SyncTransforms();
                NavigationHandInput Frame(double t, Vector3 displacement, NavigationNode aim)
                {
                    Vector3 index = origin + new Vector3(-.3f, 1.2f, .4f) + displacement;
                    return new NavigationHandInput
                    {
                        leftObserved = t, rightObserved = t, leftValid = true, rightValid = true,
                        leftIndex = index, leftWrist = index - Vector3.forward * .1f,
                        rightWrist = origin + new Vector3(.3f, 1.2f, .4f), leftPointing = true,
                        leftRayValid = true, leftRayOrigin = index, leftRayDirection = aim.floorPosition - index
                    };
                }
                void Feed(double t, Vector3 displacement, NavigationNode aim) => controller.ProcessInput(Frame(t, displacement, aim), false, false, t);
                void Prepare(double offset = 0)
                {
                    controller.ResetInput(); rig.transform.position = origin; Physics.SyncTransforms();
                    for (int i = 0; i <= 5; i++) Feed(offset + i * .05, Vector3.zero, near);
                    Require(controller.NavigationActive, "准备失败");
                }
                Prepare();
                Feed(.30, Vector3.forward * .08f, near);
                Require(controller.CandidateNode == near, "初次前选应锁定手指所指近点");
                int count = 1;
                Feed(.35, Vector3.forward * .08f, far);
                Feed(.40, Vector3.forward * .08f, far);
                Require(controller.CandidateNode == near && controller.DwellProgress == 0,
                    "换点优势未持续足够时不能跳点或完成旧目标驻留"); count++;
                // Repeated observation has no extra evidence despite advancing render time.
                controller.ProcessInput(Frame(.4, Vector3.forward * .08f, far), false, false, .46);
                Require(controller.CandidateNode == near, "重复手样本不能累计换点保持时间"); count++;
                Feed(.50, Vector3.forward * .08f, far);
                Require(controller.CandidateNode == far && controller.DwellProgress == 0, "稳定换点必须重新开始驻留"); count++;
                var blocker = Box("动态占位阻挡", far.floorPosition + Vector3.up * .8f, new Vector3(.6f, 1.6f, .6f));
                Physics.SyncTransforms();
                Feed(.55, Vector3.forward * .08f, far);
                Require(controller.CandidateNode == near, "当前点硬条件失效必须立即撤销，不能被防抖保留"); count++;
                Object.DestroyImmediate(blocker);
                Prepare(2);
                Feed(2.30, Vector3.back * .08f, rear);
                Require(controller.CandidateNode == rear, "实际控制器面向前方仍能后退选点"); count++;
                Prepare(3);
                far.transform.position += Vector3.up;
                Physics.SyncTransforms(); Feed(3.30, Vector3.forward * .08f, far);
                Require(controller.CandidateNode == near, "射线分再高也不能选择错误楼层"); count++;
                far.transform.position -= Vector3.up;
                start.neighbors.Remove(far); Physics.SyncTransforms();
                Prepare(4); Feed(4.30, Vector3.forward * .08f, far);
                Require(controller.CandidateNode == near, "射线不能越过显式邻接限制"); count++;
                start.neighbors.Add(far);
                blocker = Box("视线薄墙", origin + new Vector3(0, .9f, .75f), new Vector3(.8f, 1.8f, .04f));
                Physics.SyncTransforms(); Prepare(5); Feed(5.30, Vector3.forward * .08f, far);
                Require(!controller.CandidateNode, "高分不能穿过阻挡视线的墙选择落点"); count++;
                Object.DestroyImmediate(blocker);
                Prepare(6); Feed(6.30, Vector3.forward * .08f, near);
                for (int i = 7; i <= 21; i++) Feed(6 + i * .05, Vector3.forward * .08f, near);
                Require(controller.CurrentNodeId == near.nodeId && Vector3.Distance(rig.transform.position, near.floorPosition) < .01f,
                    "完整驻留应只移动到最终合法点，保持手势不得连续跳点"); count++;
                Prepare(8); Feed(8.30, Vector3.forward * .08f, near);
                for (int i = 7; i <= 11; i++)
                {
                    double time = 8 + i * .05;
                    var ambiguous = Frame(time, Vector3.forward * .08f, far);
                    ambiguous.leftRayDirection = origin + Vector3.forward * 2.4f - ambiguous.leftRayOrigin;
                    controller.ProcessInput(ambiguous, false, false, time);
                }
                Require(controller.CandidateNode == near, "两点接近同分时保持原点，不能持续跳选"); count++;
                NavigationNodeFixture.SetRegistered(far,false);
                Prepare(9); Feed(9.30, Vector3.forward * .08f, far);
                Require(controller.CandidateNode == near, "有邻接引用但未注册的节点不能成为候选"); count++;
                NavigationNodeFixture.SetRegistered(far,true);
                return count;
            }
            finally
            {
                foreach (var node in registered) if (node) NavigationNodeFixture.SetRegistered(node,false);
                Object.DestroyImmediate(world);
                if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                Physics.SyncTransforms();
            }
        }

        static void Require(bool value, string reason) { if (!value) throw new Exception("导航评分回归：" + reason); }
    }
}
#endif
