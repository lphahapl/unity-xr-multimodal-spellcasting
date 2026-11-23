#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NarutoVR.SealLab;
using NarutoVR.SealCombat.Rpg;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEditor;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Synthetic joint geometry through the real SDK extractor and RPG input adapter.</summary>
    public static class RpgHandPoseChecks
    {
        public static int Run()
        {
            if (Application.isPlaying) throw new Exception("基础手形检查需要编辑模式");
            var checks = new List<string>();
            void Check(string name, Action<Fixture> test)
            {
                using var fixture = new Fixture();
                try { test(fixture); checks.Add(name); }
                catch (Exception e) { throw new Exception("基础手形回归失败：" + name + "；" + fixture.Controller.HandPoseStatus + "；SDK特征：" + string.Join(" / ", fixture.Lab.Features.Where(v => v.id.EndsWith(".Curl")).Select(v => $"{v.id}={v.value:0.0},有效{v.valid},高质量{v.highConfidence}")), e); }
            }
            Check("安装版官方阈值与场景资产", f =>
            {
                Require(f.Controller.handPoseSettings.Validate() == null, "官方配置无效");
                Require(AssetDatabase.GetAssetPath(f.Controller.handPoseSettings.index).StartsWith("Packages/com.meta.xr.sdk.interaction/"), "应引用官方阈值");
                Require(typeof(LinearRpgController).GetField("straightCurl") == null && typeof(LinearRpgController).GetField("foldedCurl") == null, "旧阈值路径残留");
            });
            Check("原错误回归：SDK真实骨骼提取到右手张掌", f =>
            {
                var input = f.Warm();
                Require(input.rightOpen, "原始Curl约180度的伸直骨骼必须能通过张掌");
                Require(Mathf.Abs(f.Lab.Features.Single(v => v.id == "R.Index.Curl").value - 180) < .01f, "直指Curl单位未保留");
            });
            Check("冷启动与最短状态确认", f =>
            {
                Require(!f.Tick(10).rightOpen, "首帧不能立即通过");
                Require(!f.Tick(10.01).rightOpen, "官方最短时间之前不能通过");
                Require(f.Tick(10.03).rightOpen, "新观测满足官方时间应通过");
            });
            Check("官方握拳和实际投掷输入适配", f =>
            {
                f.Warm(features: values => { for(int i=1;i<=4;i++) Replace(values, $"R.{(HandFinger)i}.Curl",275); });

                var grip=f.Controller.Input.ReadThrowGrip(false,new HandThrowSettings(),10.18);
                Require(grip.fistValid && grip.fistClosed && !grip.fistOpen,"官方四指Closed应映射成有效握拳");
                f.Tick(10.2);f.Tick(10.24);f.Tick(10.28);f.Tick(10.32);
                grip=f.Controller.Input.ReadThrowGrip(false,new HandThrowSettings(),10.32);
                Require(grip.fistValid && grip.fistOpen && !grip.fistClosed,"官方Open应映射成松拳");
                grip=f.Controller.Input.ReadThrowGrip(false,new HandThrowSettings(),10.6);
                Require(!grip.fistValid && !grip.pinchValid,"过期腕骨不能产生松手证据");
            });
            Check("投掷三指松拳与闪避张掌分离", f =>
            {
                f.Warm(features: values => Replace(values,"R.Ring.Curl",210));

                ThrowGripSample Read(bool relaxed) => f.Controller.Input.ReadThrowGrip(false,new HandThrowSettings{allowPartialFistRelease=relaxed},10.18);
                Require(Read(true).fistOpen && !Read(false).fistOpen,"三个Open一个Neutral，仅宽松投掷应能松手");
                Require(!f.Read(10.18).rightOpen,"不能降低闪避张掌要求");
            });
            Check("左指向与右张掌组合", f =>
            {
                var input = f.Warm(); Require(input.leftPointing && input.rightOpen, "指向/张掌应同时通过");
                Require(f.Lab.Features.Single(v => v.id == "L.Middle.Curl").value > 245, "收指骨链必须产生正确SDK弯曲值");
            });
            Check("左右不同朝向的伸直骨骼", f =>
            {
                var input = f.Warm(false); Require(!input.leftPointing && input.rightOpen, "左张掌不能当作指向");
                Require(Mathf.Abs(f.Lab.Features.Single(v => v.id == "L.Index.Curl").value - f.Lab.Features.Single(v => v.id == "R.Index.Curl").value) < .01f, "左右骨骼朝向不应改变Curl单位");
            });
            Check("缺少拇指不影响基础手形", f =>
            {
                var input = f.Warm(mutate: frame => { frame.left.fingerConfidence[0] = frame.right.fingerConfidence[0] = false; });
                Require(input.leftPointing && input.rightOpen, "不应增加拇指约束");
            });
            Check("半弯不等同于收拢", f =>
            {
                var input = f.Warm(features: values => Replace(values, "L.Middle.Curl", 210));
                Require(!input.leftPointing && input.rightOpen, "Neutral不能满足Closed");
            });
            Check("自然指向步行、握拳快跑转向及追踪失效", f =>
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    var rig = f.Controller.transform;
                    rig.position = new Vector3(60, 0, 60);
                    f.Lab.recordingOrigin = rig;
                    ground.transform.position = rig.position + Vector3.down * .1f;
                    ground.transform.localScale = new Vector3(4, .2f, 4);
                    Physics.SyncTransforms();
                    f.Movement.rigRoot = rig;
                    f.Movement.encounterModesEnabled = true;
                    f.Movement.SetEncounterCombat(false);
                    Vector3 start = rig.position;
                    NavigationHandInput Feed(int index, bool pointing, bool tracked = true, bool curlIndex = false,
                        bool fist = false, float yaw = 0f, bool steeringJoints = true, bool verticalFist = false)
                    {
                        double time = 10 + index * .02;
                        var input = f.Tick(time, pointing, mutate: frame =>
                        {
                            var hand = frame.left;
                            hand.tracked = tracked;
                            var wrist = hand.joints[(int)HandJointId.HandWristRoot].position;
                            var forward = Quaternion.Euler(0f, yaw, 0f) * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                            for (int joint = 0; joint < hand.joints.Length; joint++)
                                hand.joints[joint].position = wrist + forward * (hand.joints[joint].position - wrist);
                            if (fist)
                            {
                                var knuckles = wrist + (verticalFist ? Vector3.up : forward * Vector3.up) * .05f;
                                var across = forward * (Vector3.right * .0125f);
                                hand.joints[(int)HandJointId.HandIndex1].position = knuckles - across;
                                hand.joints[(int)HandJointId.HandMiddle1].position = knuckles + across;
                            }
                            if (!steeringJoints) hand.jointValid[(int)HandJointId.HandMiddle1] = false;
                        }, features: values =>
                        {
                            if (!pointing) return;
                            Replace(values, "L.Middle.Curl", 210);
                            Replace(values, "L.Ring.Curl", 210);
                            if (curlIndex) Replace(values, "L.Index.Curl", 260);
                            if (fist)
                                foreach (var finger in new[] { HandFinger.Index, HandFinger.Middle, HandFinger.Ring, (HandFinger)4 })
                                    Replace(values, $"L.{finger}.Curl", 260);
                        });
                        Require(!input.leftPointing, "自然弯指或张掌应保留腕部操作的严格手型");
                        f.Movement.ProcessInput(input, false, false, time);
                        return input;
                    }
                    for (int i = 0; i < 9; i++) Feed(i, true);
                    var aiming = Feed(9, true);
                    Require(aiming.leftNavigationPointing && aiming.leftRayValid && rig.position.z > start.z + .08f,
                        "自然弯指应能起步，同时为战斗选点提供真实食指射线");
                    for (int i = 10; i < 16; i++) Feed(i, false);
                    Vector3 stopped = rig.position;
                    for (int i = 16; i < 24; i++) Feed(i, false);
                    Require(!f.Movement.NavigationActive && rig.position == stopped, "张掌后应停止步行");
                    for (int i = 24; i < 32; i++) Feed(i, true);
                    Require(f.Movement.NavigationActive, "恢复自然指向后应重新起步");
                    stopped = rig.position;
                    Feed(32, true, tracked: false);
                    Require(!f.Movement.NavigationActive && rig.position == stopped, "丢追踪当帧停止");
                    for (int i = 33; i < 42; i++) Feed(i, true);
                    Require(f.Movement.NavigationActive, "追踪重新确认后能起步");
                    for (int i = 42; i < 51; i++) Feed(i, true, curlIndex: true);
                    Require(!f.Movement.NavigationActive, "其余手指仍弯曲时单独收回食指也应停止");
                    for (int i = 51; i < 62; i++) Feed(i, true);
                    for (int i = 62; i < 71; i++) Feed(i, true, fist: true);
                    Vector3 beforeSprint = rig.position;
                    var grip = Feed(71, true, fist: true);
                    float minimumSprintStep = f.Movement.exploreSpeed * .027f;
                    Require(grip.leftClosed && !grip.leftRayValid && rig.position.z - beforeSprint.z > minimumSprintStep,
                        "确认左拳后沿拳头方向快跑，食指选点射线未开启");
                    Vector3 beforeTurn = rig.position;
                    Feed(72, true, fist: true, yaw: 90f);
                    Vector3 turned = rig.position - beforeTurn;
                    Require(turned.x > minimumSprintStep && Mathf.Abs(turned.z) < .001f,
                        $"保持握拳并向右转拳时应向右快跑，实际位移 {turned:F5}");
                    beforeTurn = rig.position;
                    grip = Feed(73, true, fist: true, yaw: -90f, steeringJoints: false);
                    Require(grip.leftClosed && rig.position.x - beforeTurn.x > minimumSprintStep &&
                        Mathf.Abs(rig.position.z - beforeTurn.z) < .001f, "指根不可靠时维持最近有效的快跑方向");
                    beforeTurn = rig.position;
                    Feed(74, true, fist: true, yaw: -90f);
                    Require(beforeTurn.x - rig.position.x > minimumSprintStep, "拳头换向后跟随新的朝向");
                    beforeTurn = rig.position;
                    Feed(75, true, fist: true, verticalFist: true);
                    Require(beforeTurn.x - rig.position.x > minimumSprintStep && Mathf.Abs(rig.position.z - beforeTurn.z) < .001f,
                        "拳头近垂直时保持上次朝向");
                    stopped = rig.position;
                    Feed(76, true, tracked: false, fist: true);
                    for (int i = 77; i < 88; i++) Feed(i, true, fist: true, yaw: 90f);
                    Require(!f.Movement.NavigationActive && rig.position == stopped, "追踪恢复后原地握拳不会重新起跑");
                }
                finally { UnityEngine.Object.DestroyImmediate(ground); }
            });
            Check("重复快照不能推进确认时间", f =>
            {
                for (int i = 0; i < 8; i++) Require(!f.Tick(10, now: 10 + i * .01).rightOpen, "渲染轮询不能充当新骨骼");
                Require(f.Tick(10.03).rightOpen, "真正新观测可继续确认");
            });
            Check("Open迟滞区保持与越界确认", f =>
            {
                f.Warm();
                for (int i = 0; i < 6; i++) Require(f.Tick(10.2 + i * .02, features: v => Replace(v, "R.Index.Curl", i % 2 == 0 ? 188 : 194)).rightOpen, "迟滞区不应闪断");
                Require(f.Tick(10.34, features: v => Replace(v, "R.Index.Curl", 200)).rightOpen, "单帧越界不能立即切换");
                Require(!f.Tick(10.38, features: v => Replace(v, "R.Index.Curl", 200)).rightOpen, "持续越界应进入Neutral");
                Require(!f.Tick(10.4, features: v => Replace(v, "R.Index.Curl", 183)).rightOpen, "回到Open仍需确认");
                Require(f.Tick(10.44, features: v => Replace(v, "R.Index.Curl", 183)).rightOpen, "低于退出阈值后应恢复Open");
            });
            Check("快照过期立即撤销并重新确认", f =>
            {
                f.Warm(); Require(!f.Tick(10.18, now: 10.34).rightOpen, "过期不能保留true");
                Require(!f.Tick(10.36).rightOpen && f.Tick(10.40).rightOpen, "恢复需重新确认");
            });
            Check("样本年龄边界", f =>
            {
                f.Movement.gestures.sampleMaxAge = .125f; f.Tick(10); Require(f.Tick(10.0625).rightOpen, "预热失败");
                Require(f.Tick(10.0625, now: 10.1875).rightOpen, "年龄等于上限应有效");
                Require(!f.Tick(10.0625, now: 10.188).rightOpen, "刚超过上限应失效");
            });
            Check("长间隔不能补足确认", f =>
            { f.Warm(); Require(!f.Tick(11).rightOpen && f.Tick(11.04).rightOpen, "间隔后必须重新确认"); });
            Check("时间倒退与未来时间", f =>
            {
                f.Warm(); Require(!f.Tick(10.1).rightOpen, "倒退应失效");
                Require(!f.Tick(10.2, now: 10.19).rightOpen, "未来快照应失效");
                Require(!f.Tick(10.22).rightOpen && f.Tick(10.26).rightOpen, "时钟恢复应重新确认");
            });
            Check("非有限时间与空帧", f =>
            {
                f.Warm(); Require(!f.Tick(10.2, now: double.NaN).rightOpen, "NaN时钟应失效");
                typeof(Lab).GetProperty("Frame").SetValue(f.Lab, null); Require(!f.Read(10.22).rightOpen, "空帧应失效");
            });
            foreach (string fault in new[] { "断连", "丢追踪", "低手质量", "低指质量", "腕相对无效", "缺关节", "非有限特征" })
            {
                Check(fault + "只清空故障手且恢复重新确认", f =>
                {
                    f.Warm(); var input = f.Tick(10.2, mutate: frame =>
                    {
                        var h = frame.right;
                        if (fault == "断连") h.connected = false;
                        if (fault == "丢追踪") h.tracked = false;
                        if (fault == "低手质量") h.highConfidence = false;
                        if (fault == "低指质量") h.fingerConfidence[2] = false;
                        if (fault == "腕相对无效") h.wristPosesValid = false;
                        if (fault == "缺关节") h.jointValid[(int)HandJointId.HandMiddleTip] = false;
                    }, features: v => { if (fault == "非有限特征") Replace(v, "R.Index.Curl", float.NaN); });
                    Require(!input.rightOpen && input.leftPointing, "故障不可沿用旧状态或污染另一手");
                    Require(!f.Tick(10.22).rightOpen && f.Tick(10.26).rightOpen, "恢复不能继承旧计时");
                });
            }
            Check("左右观测时间独立", f =>
            {
                f.Tick(10);
                var input = f.Tick(10.04, mutate: frame => frame.left.observedAt = 10);
                Require(input.rightOpen && !input.leftPointing, "右手更新不得推进左手");
                Require(f.Tick(10.08).leftPointing, "左手新观测可以完成确认");
            });
            Check("来源切换不能继承状态", f =>
            {
                f.Warm(); Require(!f.Tick(10.2, mutate: frame => frame.source = "new-runtime").rightOpen, "换来源需重置");
                Require(f.Tick(10.24, mutate: frame => frame.source = "new-runtime").rightOpen, "新来源重新确认");
            });
            Check("缺配置拒绝而非回退旧角度", f =>
            {
                bool rejected = false;
                try { _ = new OfficialHandPoseDetector(null); } catch (ArgumentNullException) { rejected = true; }
                Require(rejected, "缺配置应报错");
            });
            Check("退出时技能组件先销毁仍可安全停用主控", f =>
            {
                var skills = f.Controller.skills;
                skills.effects = f.Controller.gameObject.AddComponent<SkillController>();
                typeof(LinearRpgController).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f.Controller, true);
                UnityEngine.Object.DestroyImmediate(skills);
                typeof(LinearRpgController).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(f.Controller, null);
            });
            Check("退出时效果与导航组件先销毁仍可安全停用主控", f =>
            {
                var effects = f.Controller.gameObject.AddComponent<SkillController>(); f.Controller.skills.effects = effects;
                typeof(LinearRpgController).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f.Controller, true);
                UnityEngine.Object.DestroyImmediate(effects); UnityEngine.Object.DestroyImmediate(f.Movement);
                typeof(LinearRpgController).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(f.Controller, null);
            });
            Check("实际骨骼到选点FSM并且只触发一次", f =>
            {
                var state = new RpgGestureState(); int steps = 0;
                for (int i = 0; i < 70; i++)
                {
                    double t = 10 + i * .02;
                    var input = f.Tick(t, mutate: frame => { if (i >= 25) Translate(frame.left, new Vector3(.08f, 0, 0)); });
                    var intent = state.Process(input, Vector3.forward, false, false, t);
                    if (intent.navigationStep != Vector2.zero) { Require(intent.navigationStep == Vector2.right, "方向错误"); steps++; }
                }
                Require(steps == 1, "驻留选点应触发一次，未回中不得重复");
            });
            Check("实际骨骼到双手低位取消", f =>
            {
                var state = new RpgGestureState(); int cancellations = 0;
                for (int i = 0; i < 40; i++)
                {
                    double t = 10 + i * .02;
                    var input = f.Tick(t, mutate: frame => { Translate(frame.left, Vector3.down * .4f); Translate(frame.right, Vector3.down * .4f); });
                    if (state.Process(input, Vector3.forward, false, false, t).cancel) cancellations++;
                }
                Require(cancellations == 1, "可靠双手低位应触发一次取消");
            });
            Check("实际骨骼到闪避及持球前推优先级", f =>
            {
                foreach (bool held in new[] { false, true })
                {
                    using var sample = new Fixture(); var state = new RpgGestureState(); int dodges = 0;
                    for (int i = 0; i < 40; i++)
                    {
                        double t = 10 + i * .02;
                        var input = sample.Tick(t, mutate: frame => { if (i >= 25) Translate(frame.right, Vector3.forward * Mathf.Min(.14f, (i - 25) * .02f)); });
                        if (state.Process(input, Vector3.forward, held, false, t).dodgeDirection != Vector3.zero) dodges++;
                    }
                    Require(dodges == (held ? 0 : 1), "持球前推必须禁止前闪；无持球正常前闪");
                }
            });
            Check("探索指示线只需食指几何，完整手型仍控制驻留", f =>
            {
                f.Movement.encounterModesEnabled = true;
                f.Movement.SetExplorationMode(ExplorationMovement.Teleport);
                var input = f.Tick(10, features: values => values.RemoveAll(feature => feature.id == "L.Middle.Curl"));
                Require(input.leftIndexAim && input.leftRayValid && input.leftPoseUnconfirmed && !input.leftNavigationPointing,
                    "其余手指特征暂缺时仍可显示指示线");
                input = f.Tick(10.02, mutate: frame => frame.left.jointValid[(int)HandJointId.HandIndexTip] = false);
                Require(!input.leftIndexAim && !input.leftRayValid, "失去食指尖不得保留指示线");
            });
            Directory.CreateDirectory("Logs/official-hand-poses");
            File.WriteAllText("Logs/official-hand-poses/pose-checks.json", JsonUtility.ToJson(new Report { passed = true, utc = DateTime.UtcNow.ToString("O"), checks = checks.ToArray() }, true));
            return checks.Count;
        }

        [Serializable] sealed class Report { public bool passed, hardwareTested; public string utc; public string[] checks; }
        sealed class Fixture : IDisposable
        {
            readonly GameObject root;
            readonly SealFeatureExtractor extractor = new();
            public readonly Lab Lab;
            public readonly RpgLocomotionController Movement;
            public readonly LinearRpgController Controller;
            public Fixture()
            {
                root = new GameObject("手形回归_合成骨骼"); root.SetActive(false);
                var head = new GameObject("合成头部"); head.transform.SetParent(root.transform); head.transform.position = Vector3.up * 1.7f;
                Lab = root.AddComponent<Lab>(); var skills = root.AddComponent<SkillInputController>(); skills.lab = Lab;
                Movement = root.AddComponent<RpgLocomotionController>(); Movement.head = head.transform;
                Controller = root.AddComponent<LinearRpgController>();
                Controller.chapter = root.AddComponent<TrainingChapterFlow>();
                ((TrainingChapterFlow)Controller.chapter).rpg = Controller;
                Controller.playerDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<RpgPlayerDefinition>(LinearRpgSceneSetup.TrainingPlayerPath); Controller.skills = skills; Controller.locomotion = Movement;
                Controller.handPoseSettings = OfficialHandPoseSetup.EnsureProfile();
            }
            public NavigationHandInput Warm(bool pointing = true, Action<SealFrame> mutate = null, Action<List<SealFeature>> features = null)
            {
                NavigationHandInput input = default;
                for (int i = 0; i < 10; i++) input = Tick(10 + i * .02, pointing, mutate: mutate, features: features);
                return input;
            }
            public NavigationHandInput Tick(double stamp, bool pointing = true, double? now = null, Action<SealFrame> mutate = null, Action<List<SealFeature>> features = null)
            {
                var frame = Skeleton(stamp, pointing); mutate?.Invoke(frame);
                typeof(Lab).GetProperty("Frame").SetValue(Lab, frame); extractor.Extract(frame, Lab.Features); features?.Invoke(Lab.Features);
                return Read(now ?? stamp);
            }
            public NavigationHandInput Read(double now) => Controller.Input.ReadNavigationFrame(now);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
        static void Replace(List<SealFeature> values, string id, float value)
        { int index = values.FindIndex(f => f.id == id); values[index] = new SealFeature(id, "deg", value, true, true); }
        static void Translate(HandSnapshot hand, Vector3 delta)
        { for (int i = 0; i < hand.joints.Length; i++) hand.joints[i].position += delta; }

        internal static SealFrame Skeleton(double time, bool pointing = true)
        {
            var frame = new SealFrame { time = time, source = "synthetic-regression-only" };
            for (int side = 0; side < 2; side++)
            {
                var hand = side == 0 ? frame.left : frame.right;
                var basis = side == 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                var wrist = new Vector3(side == 0 ? -.2f : .2f, 1.2f, .3f);
                hand.connected = hand.tracked = hand.fresh = hand.highConfidence = hand.wristPosesValid = true;
                hand.observedAt = time; hand.version = (int)(time * 1000);
                for (int j = 0; j < hand.joints.Length; j++)
                { hand.jointValid[j] = true; hand.joints[j] = new Pose(wrist, basis); hand.wristPoses[j] = Pose.identity; }
                for (int f = 0; f < 5; f++)
                {
                    hand.fingerConfidence[f] = true;
                    var chain = HandJointUtils.FingerToJointList[f];
                    Vector3 position = new Vector3((f - 2) * .025f, .04f, 0), direction = Vector3.up;
                    float bend = side == 0 && pointing && f >= 2 ? 80 : 0;
                    for (int j = 0; j < chain.Length; j++)
                    {
                        hand.wristPoses[(int)chain[j]] = new Pose(position, Quaternion.identity);
                        hand.joints[(int)chain[j]] = new Pose(wrist + basis * position, basis);
                        position += direction * .025f;
                        direction = Quaternion.AngleAxis(bend, Constants.LeftThumbSide) * direction;
                    }
                }
            }
            return frame;
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
#endif
