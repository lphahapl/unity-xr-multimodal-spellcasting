#if UNITY_EDITOR
using System;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Pure gesture FSM regression checks; synthetic samples do not verify headset tracking.</summary>
    public static class RpgDirectionalInteractionChecks
    {
        const double Interval = .04;
        static readonly Vector3 LeftWrist = new Vector3(-.45f, 1.2f, .4f);
        static readonly Vector3 LeftIndex = LeftWrist + Vector3.forward * .1f;
        static readonly Vector3 RightWrist = new Vector3(.45f, 1.2f, .4f);
        static readonly Vector3 PairCenter = new Vector3(0, 1.2f, .4f);
        static readonly Vector3[] Directions = { Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("四向交互检查需要编辑模式。");
            int passed = 0;
            void Check(string name, Action test)
            {
                try { test(); passed++; }
                catch (Exception error) { throw new InvalidOperationException("[四向交互检查] " + name + "：" + error.Message, error); }
            }
            Check("默认右掌四向独立于左指向和左追踪", CheckIndependentRight);
            Check("兼容导航门槛仍然可选", CheckLegacyGate);
            Check("张掌要求、一次触发和回中重备", CheckSwipeRearm);
            Check("斜向动作按主轴选择四向", CheckDiagonalCardinals);
            Check("左指独立四向、默认驻留和回中重备", CheckNavigationDirections);
            Check("左指重复和陈旧观测不完成驻留", CheckNavigationFreshness);
            Check("近腕保护同时清除导航并禁止闪避", CheckProtectionEntry);
            Check("保护距离迟滞和新鲜稳定分离", CheckProtectionRelease);
            Check("持续分手动作不能变成闪避", CheckContinuousSeparation);
            Check("重复和陈旧双手观测不能解除保护", CheckRepeatedAndStalePairs);
            Check("不同步双手观测不能解除保护", CheckAsynchronousPairs);
            Check("保护中左手丢失不能暴露右手闪避", CheckLeftLossWhileProtected);
            Check("持球前推占用跨越结印保护", CheckForwardOwnershipAcrossProtection);
            Check("握持投掷四向占用和出手后跟随动作", CheckAllDirectionThrowOwnership);
            Check("追踪原点平移不改变选点闪避和保护", CheckTranslationInvariance);
            return passed;
        }

        static void CheckIndependentRight()
        {
            foreach (bool leftValid in new[] { true, false })
                foreach (Vector3 direction in Directions)
                {
                    var f = new Fixture();
                    int dodges = Swipe(f, RightWrist, direction * .12f, direction, leftValid: leftValid);
                    Require(dodges == 1, "默认配置应允许一次" + direction + "闪避；leftValid=" + leftValid);
                    Require(!f.State.NavigationActive && !f.State.SealProtectionActive,
                        "未指向且从未靠近的样本不应需要导航或结印状态。");
                }
        }

        static void CheckLegacyGate()
        {
            foreach (Vector3 direction in Directions)
            {
                var cold = new Fixture(new RpgGestureSettings { requireNavigationForDodge = true });
                Require(Swipe(cold, RightWrist, direction * .12f, direction) == 0,
                    "显式兼容开关必须阻止未准备导航的闪避。");
                var ready = new Fixture(new RpgGestureSettings { requireNavigationForDodge = true });
                Ready(ready);
                Require(Swipe(ready, RightWrist, direction * .12f, direction, pointing: true) == 1,
                    "兼容开关下，已准备导航的右掌仍应四向闪避。");
            }
        }

        static void CheckSwipeRearm()
        {
            var closed = new Fixture();
            Require(Swipe(closed, RightWrist, Vector3.right * .12f, Vector3.right, open: false) == 0,
                "闭掌不能作为右掌短划。");
            var f = new Fixture();
            Require(Swipe(f, RightWrist, Vector3.right * .12f, Vector3.right) == 1, "首次短划必须成功。");
            for (int i = 0; i < 12; i++)
                NoActions(f.Tick(right: RightWrist + Vector3.right * .12f), "维持终点不能重复闪避。");
            NoActions(f.Tick(), "回中帧不能直接闪避。");
            Require(Swipe(f, RightWrist, Vector3.left * .12f, Vector3.left) == 1,
                "回中后的新短划应重新触发。");
        }

        static void CheckDiagonalCardinals()
        {
            var motions = new[]
            {
                new Vector3(.12f, 0, .06f), new Vector3(-.12f, 0, .06f),
                new Vector3(.06f, 0, .12f), new Vector3(.06f, 0, -.12f),
                new Vector3(.12f, 0, -.06f), new Vector3(-.12f, 0, -.06f),
                new Vector3(-.06f, 0, .12f), new Vector3(-.06f, 0, -.12f)
            };
            var expected = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
                Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            for (int i = 0; i < motions.Length; i++)
            {
                Require(Swipe(new Fixture(), RightWrist, motions[i], expected[i]) == 1,
                    "斜向短划必须仅输出主轴方向：" + motions[i]);
                var left = new Fixture(); Ready(left, rightValid: false);
                Vector2 selected = expected[i] == Vector3.right ? Vector2.right :
                    expected[i] == Vector3.left ? Vector2.left : expected[i] == Vector3.forward ? Vector2.up : Vector2.down;
                Require(HoldSelection(left, motions[i], selected) == 1, "左指斜向驻留主轴错误。");
            }
        }

        static void CheckNavigationDirections()
        {
            var expected = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
            for (int d = 0; d < Directions.Length; d++)
            {
                var f = new Fixture(); Ready(f, rightValid: false);
                Require(HoldSelection(f, Directions[d] * .08f, expected[d], enforceDefaultDwell: true) == 1,
                    "右手缺失时左指仍须独立选点一次。");
                for (int i = 0; i < 10; i++)
                    NoActions(f.Tick(leftIndex: LeftIndex - Directions[d] * .08f, pointing: true, rightValid: false),
                        "未回中时换向不能绕过一次触发限制。");
                NoActions(f.Tick(pointing: true, rightValid: false), "回中不得立刻产生选点。");
                Require(HoldSelection(f, Directions[d] * .08f, expected[d]) == 1,
                    "回中后同一方向应能再次驻留选点。");
            }
        }

        static void CheckNavigationFreshness()
        {
            var f = new Fixture(); Ready(f, rightValid: false);
            f.Tick(leftIndex: LeftIndex + Vector3.right * .08f, pointing: true, rightValid: false);
            double repeated = f.Now;
            for (int i = 0; i < 20; i++)
                NoActions(f.Tick(leftIndex: LeftIndex + Vector3.right * .08f, pointing: true,
                    rightValid: false, leftObserved: repeated), "重复后变陈旧的左指观测不得累计驻留。");
            Require(!f.State.NavigationActive, "过期左手必须撤销导航。");
            Ready(f, rightValid: false);
            Require(HoldSelection(f, Vector3.right * .08f, Vector2.right, enforceDefaultDwell: true) == 1,
                "恢复新鲜样本后必须重新完成完整驻留。");
        }

        static void CheckProtectionEntry()
        {
            var f = new Fixture(); Ready(f);
            for (int i = 0; i < 6; i++)
                NoActions(f.Tick(leftIndex: LeftIndex + Vector3.right * .08f, pointing: true), "驻留尚未达到默认时间。");
            Require(f.State.Selection == Vector2.right, "进入保护前必须已有真实待完成选点。");
            NoActions(f.Pair(.26f, pointing: true), ".26米近腕必须先保护而非执行动作。");
            Require(f.State.SealProtectionActive && !f.State.NavigationActive && f.State.Selection == Vector2.zero &&
                f.State.DwellProgress == 0, "保护必须清除导航准备、方向和驻留进度。");
            for (int i = 0; i < 24; i++)
            {
                Vector3 shift = Vector3.forward * (i % 4) * .04f;
                NoActions(f.Pair(.2f, pointing: true, shift: shift), "近腕期间指向和右掌轨迹都不能产生动作。");
                Require(f.State.SealProtectionActive && !f.State.NavigationActive, "新鲜近腕观测必须保持保护。");
            }
        }

        static void CheckProtectionRelease()
        {
            var f = new Fixture(); f.Pair(.2f);
            for (int i = 0; i < 10; i++)
            {
                NoActions(f.Pair(.3f), "迟滞带内不能执行动作。");
                Require(f.State.SealProtectionActive, ".26到.34米之间即使保持足够久也不能解除保护。");
            }
            SettlePair(f, .34f);
            Require(!f.State.SealProtectionActive, ".34米新鲜稳定双手保持超过.18秒应解除保护。");
            Vector3 separatedRight = PairCenter + Vector3.right * .17f;
            Require(Swipe(f, separatedRight, Vector3.forward * .12f, Vector3.forward,
                leftWrist: PairCenter - Vector3.right * .17f) == 1, "稳定解除后新短划应正常工作。");
        }

        static void CheckContinuousSeparation()
        {
            var f = new Fixture(); f.Pair(.2f);
            // Both wrists keep moving .04m per fresh sample; crossing .34m is not a new swipe.
            for (int i = 0; i < 12; i++)
            {
                NoActions(f.Pair(.36f + i * .08f), "持续分手不能在保护计时后半段变成闪避。");
                Require(f.State.SealProtectionActive, "仍在连续移动的分手动作不得提前解除保护。");
            }
            SettlePair(f, 1.24f);
            Vector3 origin = PairCenter + Vector3.right * .62f;
            Require(Swipe(f, origin, Vector3.back * .12f, Vector3.back,
                leftWrist: PairCenter - Vector3.right * .62f) == 1, "分手稳定后必须允许独立的新动作。");
        }

        static void CheckRepeatedAndStalePairs()
        {
            var f = new Fixture(); f.Pair(.2f); f.Pair(.4f);
            double repeated = f.Now;
            for (int i = 0; i < 12; i++)
            {
                NoActions(f.Pair(.4f, leftObserved: repeated, rightObserved: repeated), "重复及陈旧双手观测不能产生动作。");
                Require(f.State.SealProtectionActive, "渲染轮询的时间不能解除保护。");
            }
            SettlePair(f, .4f);
            Require(!f.State.SealProtectionActive, "重复/过期中断后，应由新鲜稳定双手重新解除保护。");
        }

        static void CheckAsynchronousPairs()
        {
            var f = new Fixture(); f.Pair(.2f);
            double start = f.Now;
            for (int i = 1; i <= 4; i++)
            {
                // Ramp skew with monotonic .04s left and .02s right observations.
                NoActions(f.Pair(.4f, rightObserved: start + i * .02), "建立时间差时也不能执行动作。");
                Require(f.State.SealProtectionActive, "尚未满足解除时间。");
            }
            for (int i = 0; i < 12; i++)
            {
                // Each hand is younger than .15s, but their .10s skew exceeds the .08s pair limit.
                double next = f.Now + Interval;
                NoActions(f.Pair(.4f, leftObserved: next, rightObserved: next - .1), "不同步双手不能产生解除动作。");
                Require(f.State.SealProtectionActive, "单手新鲜不等于可用于解除保护的新鲜配对。");
            }
            SettlePair(f, .4f);
            Require(!f.State.SealProtectionActive, "恢复同步后必须重新满足稳定分离时间。");
        }

        static void CheckLeftLossWhileProtected()
        {
            var f = new Fixture(); f.Pair(.2f);
            for (int i = 0; i < 20; i++)
            {
                NoActions(f.Tick(right: PairCenter + Vector3.right * (.1f + (i % 4) * .04f), leftValid: false),
                    "结印中丢失左手不能把可见右掌暴露成闪避。");
                Require(f.State.SealProtectionActive, "保护必须跨越左手丢追踪。");
            }
            SettlePair(f, .4f);
            Require(!f.State.SealProtectionActive, "左手恢复后仍需可靠分离方可解除保护。");
        }

        static void CheckForwardOwnershipAcrossProtection()
        {
            var f = new Fixture();
            f.Tick(heldForward: true);
            f.Pair(.2f, heldForward: true);
            for (int i = 0; i < 4; i++) NoActions(f.Pair(.2f), "发射后保护仍应保留前推所有权。");
            SettlePair(f, .4f);
            Vector3 origin = PairCenter + Vector3.right * .2f;
            Require(Swipe(f, origin, Vector3.forward * .12f, Vector3.forward,
                leftWrist: PairCenter - Vector3.right * .2f) == 0, "持球前推所有权不得在保护或发射时丢失。");
            for (int i = 0; i < 6; i++)
                NoActions(f.Tick(right: origin + Vector3.forward * .12f,
                    leftWrist: PairCenter - Vector3.right * .2f), "同次前推后续不能追加前闪。");
            NoActions(f.Tick(right: origin, leftWrist: PairCenter - Vector3.right * .2f), "回中只应重备。");
            Require(Swipe(f, origin, Vector3.forward * .12f, Vector3.forward,
                leftWrist: PairCenter - Vector3.right * .2f) == 1, "回中后独立的空手前闪应恢复。");
        }

        static void CheckAllDirectionThrowOwnership()
        {
            foreach (Vector3 direction in Directions)
            {
                var f = new Fixture();
                Require(Swipe(f, RightWrist, direction * .12f, direction, allOwned: true) == 0,
                    "握持投掷必须占用所有方向的右手动作。");
                // Ownership ends at release, but the physical follow-through is still the same throw.
                for (int i = 1; i <= 6; i++)
                    NoActions(f.Tick(right: RightWrist + direction * (.12f + i * .04f)),
                        "出手后同次投掷跟随动作不能闪避：" + direction);
                Vector3 rest = RightWrist + direction * .36f;
                for (int i = 0; i < 7; i++) NoActions(f.Tick(right: rest), "投掷稳定重备期间不能闪避。");
                Require(Swipe(f, rest, direction * .12f, direction) == 1,
                    "出手并稳定超过.18秒后，应允许新的同向短划。");
            }
        }

        static void CheckTranslationInvariance()
        {
            var baseline = new Fixture(); var translated = new Fixture();
            Vector3 offset = Vector3.zero; int steps = 0, dodges = 0; bool sawProtection = false;
            for (int i = 0; i < 62; i++)
            {
                if (i == 5)
                {
                    offset = new Vector3(2, .4f, -3);
                    translated.State.TranslateAnchors(offset);
                }
                if (i == 50)
                {
                    Vector3 shift = new Vector3(-1, .2f, .7f);
                    offset += shift;
                    translated.State.TranslateAnchors(shift);
                }
                Vector3 leftIndex = LeftIndex + (i >= 14 ? Vector3.forward * .08f : Vector3.zero);
                Vector3 left = LeftWrist;
                Vector3 right = RightWrist + Vector3.right * Mathf.Clamp((i - 8) * .04f, 0, .12f);
                if (i >= 43)
                {
                    float separation = i < 48 ? .2f : .4f;
                    left = PairCenter - Vector3.right * (separation * .5f);
                    right = PairCenter + Vector3.right * (separation * .5f);
                }
                var a = baseline.Tick(right: right, leftWrist: left, leftIndex: leftIndex, pointing: true);
                var b = translated.Tick(right: right + offset, leftWrist: left + offset,
                    leftIndex: leftIndex + offset, pointing: true);
                Require(a.navigationStep == b.navigationStep && a.dodgeDirection == b.dodgeDirection &&
                    baseline.State.SealProtectionActive == translated.State.SealProtectionActive &&
                    baseline.State.NavigationActive == translated.State.NavigationActive,
                    "相同物理动作在追踪原点平移后应具有相同结果，sample=" + i);
                if (a.navigationStep != Vector2.zero) { Require(a.navigationStep == Vector2.up, "预期前方选点。"); steps++; }
                if (a.dodgeDirection != Vector3.zero) { Require(a.dodgeDirection == Vector3.right, "预期向右闪避。"); dodges++; }
                sawProtection |= baseline.State.SealProtectionActive;
            }
            Require(steps == 1 && dodges == 1 && sawProtection && !baseline.State.SealProtectionActive,
                "平移场景必须真实完成一次选点、一次闪避以及保护进入和退出。");

            baseline = new Fixture(); translated = new Fixture();
            baseline.Tick(allOwned: true); translated.Tick(allOwned: true);
            offset = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                if (i == 2)
                {
                    offset = new Vector3(1, 0, 2);
                    translated.State.TranslateAnchors(offset);
                }
                NoActions(baseline.Tick(), "投掷释放后的静止样本不能执行动作。");
                NoActions(translated.Tick(right: RightWrist + offset, leftWrist: LeftWrist + offset,
                    leftIndex: LeftIndex + offset), "平移不能执行动作或改变投掷稳定重备。");
            }
            Require(Swipe(baseline, RightWrist, Vector3.right * .12f, Vector3.right) == 1,
                "基准投掷应已稳定重备。");
            Require(Swipe(translated, RightWrist + offset, Vector3.right * .12f, Vector3.right,
                leftWrist: LeftWrist + offset) == 1, "原点平移不得延后投掷稳定重备时间。");
        }

        static void Ready(Fixture f, bool rightValid = true)
        {
            for (int i = 0; i < 8; i++) NoActions(f.Tick(pointing: true, rightValid: rightValid), "准备阶段不能产生动作。");
            Require(f.State.NavigationActive, "连续.28秒稳定左指应准备导航。");
        }

        static int HoldSelection(Fixture f, Vector3 displacement, Vector2 expected, bool enforceDefaultDwell = false)
        {
            int steps = 0;
            for (int i = 0; i < 21; i++)
            {
                var intent = f.Tick(leftIndex: LeftIndex + displacement, pointing: true, rightValid: false);
                Require(intent.dodgeDirection == Vector3.zero, "左指测试中不应产生右手闪避。");
                if (enforceDefaultDwell && i <= 16)
                    Require(intent.navigationStep == Vector2.zero, "默认驻留.65秒之前不得选点（当前最多.64秒）。");
                if (intent.navigationStep == Vector2.zero) continue;
                Require(intent.navigationStep == expected, "左指驻留方向错误：" + intent.navigationStep);
                steps++;
            }
            return steps;
        }

        static int Swipe(Fixture f, Vector3 origin, Vector3 displacement, Vector3 expected,
            bool pointing = false, bool leftValid = true, bool open = true, bool allOwned = false,
            Vector3? leftWrist = null)
        {
            int dodges = 0;
            for (int i = 0; i < 4; i++)
            {
                var intent = f.Tick(right: origin + displacement * (i / 3f), pointing: pointing,
                    leftValid: leftValid, open: open, allOwned: allOwned, leftWrist: leftWrist);
                Require(intent.navigationStep == Vector2.zero, "短划夹具不应夹带左手选点。");
                if (intent.dodgeDirection == Vector3.zero) continue;
                Require((intent.dodgeDirection - expected).sqrMagnitude < .000001f,
                    "右掌短划方向错误：预期" + expected + "，实际" + intent.dodgeDirection);
                dodges++;
            }
            return dodges;
        }

        static void SettlePair(Fixture f, float separation)
        {
            // Seven samples allow .20s even if reacquisition consumes the first sample after a gap.
            for (int i = 0; i < 7; i++)
            {
                NoActions(f.Pair(separation), "分离稳定本身不能产生选点或闪避。");
                if (i < 4) Require(f.State.SealProtectionActive, "新鲜分离不足.18秒不得解除保护。");
            }
            Require(!f.State.SealProtectionActive, "新鲜稳定分离.24秒后必须解除保护。");
        }

        sealed class Fixture
        {
            public readonly RpgGestureState State;
            public double Now { get; private set; } = 10;
            public Fixture(RpgGestureSettings settings = null) { State = new RpgGestureState(settings); }
            public RpgGestureIntent Tick(Vector3? right = null, Vector3? leftWrist = null, Vector3? leftIndex = null,
                bool pointing = false, bool leftValid = true, bool rightValid = true, bool open = true,
                bool heldForward = false, bool allOwned = false, double? leftObserved = null, double? rightObserved = null)
            {
                Now += Interval;
                var frame = new NavigationHandInput
                {
                    leftObserved = leftObserved ?? Now, rightObserved = rightObserved ?? Now,
                    leftValid = leftValid, rightValid = rightValid, leftWrist = leftWrist ?? LeftWrist,
                    leftIndex = leftIndex ?? LeftIndex, rightWrist = right ?? RightWrist,
                    leftPointing = pointing, rightOpen = open, rightMotionOwnedBySpell = allOwned
                };
                return State.Process(frame, Vector3.forward, heldForward, false, Now);
            }
            public RpgGestureIntent Pair(float separation, bool pointing = false, bool heldForward = false,
                Vector3? shift = null, double? leftObserved = null, double? rightObserved = null)
            {
                Vector3 center = PairCenter + (shift ?? Vector3.zero);
                return Tick(right: center + Vector3.right * (separation * .5f),
                    leftWrist: center - Vector3.right * (separation * .5f), pointing: pointing,
                    heldForward: heldForward, leftObserved: leftObserved, rightObserved: rightObserved);
            }
        }

        static void NoActions(RpgGestureIntent intent, string message) =>
            Require(intent.navigationStep == Vector2.zero && intent.dodgeDirection == Vector3.zero, message);
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
