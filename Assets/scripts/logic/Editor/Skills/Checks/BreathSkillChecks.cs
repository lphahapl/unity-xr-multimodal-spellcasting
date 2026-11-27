using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
using NarutoVR.SealLab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using NarutoVR.SealCombat.Enemies;
using Lab = NarutoVR.SealLab.SealLab;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>用正式技能资产和带时间戳的手部输入，验证口令、施法、耗用和清理的连接。</summary>
    public static class BreathSkillChecks
    {
        const string CatalogPath = SkillConfigurationSetup.CatalogPath;
        static void Require(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException("吐火回归：" + reason);
        }

        static void CheckCooldown(float actual, float expected, string reason)
        {
            Require(Math.Abs(actual - expected) < .001f, $"{reason}，期望 {expected:0.###} 秒，实际 {actual:0.###} 秒");
        }

        [MenuItem("NarutoVR/技能训练场/验证两种口部施法（Play）")]
        public static void Run()
        {
            Require(Application.isPlaying, "需要在 Play 模式运行");
            VerifySealProjectile();
            VerifyRecognizedStream();
            VerifyStream();
            VerifyStreamJointSwitches();
            VerifyStreamAimLoss();
            VerifyBlockedBurstAim(false);
            VerifyBlockedBurstAim(true);
            VerifyRelease(false);
            VerifyRelease(true);
            VerifyExhaustion();
            VerifySustainedCooldown();
            VerifyCooldownLimits();
            VerifyOptionalDistance();
            VerifyBurst();
            VerifyBurstImpact();
            VerifyBurstJointSwitches();
            VerifyBurstAfterInterruptedSeal();
            Debug.Log("BREATH_PLAY_PASS 认印后变形、迟到口令、分手作废、SO 起手和停喷边界、口令等手、曲指关节与掌心印顶、单手换点滞回与重选、近等高印顶混合、指尖遮挡、仅腕暂停与恢复、嘴后拒发、移动与转头、阻挡、松印、距离、耗尽、持续冷却、检查点、独立飞行");
        }

        public static void VerifySealProjectile()
        {
            using (var f = new Fixture("breath.burst"))
            {
                f.HandOffset = Vector3.right * .25f;
                f.Sample(3, seal:false); f.Speak(); f.Sample(10, near:false);
                var ember = f.Head.Find("飞炎掌心火种(Clone)");
                Require(ember && ember.gameObject.activeInHierarchy && ember.GetComponentsInChildren<Renderer>().Length > 0 &&
                    f.Effects.LaunchCount == 0 && f.Vitals.Mana == 100, "飞炎律印只蓄掌上火苗，无投射物或耗用");
                f.Head.rotation = Quaternion.Euler(0, 24, 0);
                f.Sample(8, near:false, seal:false);
                var start = ember.position;
                f.ReleaseSeal = true;
                f.Sample(16, near:false, seal:false);
                Require(f.Effects.ActivationCount == 1 && f.Effects.LaunchCount == 1 && f.Vitals.Mana == 88 &&
                    !ember.gameObject.activeSelf, "规印一次发球，只收费一次并收掉火苗");
                var dir = f.Head.position + f.Head.forward * 30f - start;
                Require(Vector3.Distance(start, f.Effects.LastLaunchPosition) < .002f &&
                    Vector3.Angle(dir, f.Effects.LastLaunchDirection) < .2f, "转头后从火苗位置射向最新头向落点");
            }
            using (var f = new Fixture("breath.burst"))
            {
                f.Sample(6); f.Speak(); f.Sample(4);
                f.Cancel();
                f.Sample(8, seal:false); f.ReleaseSeal = true; f.Sample(16, seal:false);
                Require(f.Effects.LaunchCount == 0 && f.LastActivated == "buff.zi" &&
                    !f.Head.Find("飞炎掌心火种(Clone)").gameObject.activeSelf, "取消后单结规印恢复加持");
            }
            using (var f = new Fixture("breath.burst"))
            {
                f.Sample(4, seal:false); f.Speak(); f.Sample(10);
                f.Sample(180, seal:false);
                f.ReleaseSeal = true; f.Sample(10, seal:false);
                Require(f.Effects.LaunchCount == 0 && f.LastActivated == "buff.zi",
                    "超过组合窗口后规印恢复独立加持");
            }
            using (var f = new Fixture("breath.burst"))
            {
                f.Sample(4, seal:false); f.Speak(); f.ReleaseSeal = true; f.Sample(12, seal:false);
                f.ReleaseSeal = false; f.Sample(8, seal:false); f.Sample(12);
                Require(f.Effects.LaunchCount == 0 && f.LastActivated == "buff.zi",
                    "规印在律印前不发射");
            }
            using (var f = new Fixture("breath.burst"))
            {
                f.Sample(6); f.Speak(); f.Sample(6);
                var ember = f.Head.Find("飞炎掌心火种(Clone)");
                Require(ember && ember.gameObject.activeSelf, "新输入时代前已蓄火");
                f.ResetInput(); f.Sample(8, seal:false);
                f.ReleaseSeal = true; f.Sample(12, seal:false);
                Require(f.Effects.LaunchCount == 0 && !ember.gameObject.activeSelf,
                    "恢复检查点后的新输入时代不会从旧火种发射");
            }
        }

        static void VerifyRecognizedStream()
        {
            using (var f = new Fixture("breath.stream"))
            {
                f.Sample(8, near:false);
                f.Sample(8, near:false, seal:false);
                f.Speak(.22);
                f.Sample(4, near:false, seal:false);
                Require(f.Effects.ActivationCount == 0 && f.Vitals.Mana == 100, "等待近嘴时不扣费");
                var pilot = f.Head.Find("龙火待吹火苗(Clone)");
                Require(pilot && pilot.gameObject.activeSelf && Vector3.Distance(pilot.position, f.Mouth) > .15f,
                    "认印后在胸前手印处看见火苗");
                // The simulated hand centre is initially 6 cm forward and 4.5 cm below the mouth.
                f.SetMouthDistance(f.StartDistance + .01f);
                f.Sample(1, seal:false);
                Require(f.Effects.ActivationCount == 0, "超过 SO 起手距离时继续等候");
                f.SetMouthDistance(f.StartDistance - .01f);
                f.Sample(1, seal:false);
                Require(f.Effects.ActivationCount == 1 && f.Effects.HasOccupiedHand,
                    "先正确结印，口令送达前手型改变，进入 SO 起手距离后应从原口令起手");
                Require(pilot.gameObject.activeSelf, "开始吹气时保留印上原火苗");
                var target = f.Target(f.Mouth + f.Head.forward);
                float mana = f.Vitals.Mana;
                f.SetMouthDistance(f.StopDistance - .01f);
                f.Sample(24, seal:false);
                Require(f.Effects.ActivationCount == 1 && f.Effects.HasOccupiedHand && target.HitCount > 0 && f.Vitals.Mana < mana,
                    "接近 SO 维持距离、印形持续不匹配时，原火流继续命中并扣持续费用");
                f.SetMouthDistance(f.StopDistance + .01f);
                mana = f.Vitals.Mana;
                f.Sample(1, seal:false);
                Require(f.Effects.HasOccupiedHand && f.Vitals.Mana == mana && pilot.gameObject.activeSelf,
                    "保印移出维持距离应立即停喷，原火苗复现并保留本次术");
            }
            using (var f = new Fixture("breath.stream"))
            {
                f.Sample(8, seal:false);
                f.Speak(); f.Sample(10, seal:false);
                Require(f.Effects.ActivationCount == 0 && f.Vitals.Mana == 100, "仅双腕合拢且念咒不能代替首次正确结印");
            }
            using (var f = new Fixture("breath.stream"))
            {
                f.Sample(8, near:false);
                f.HandsApart = true; f.Sample(2, near:false, seal:false);
                f.HandsApart = false; f.Sample(2, seal:false);
                f.Speak(.12); f.Sample(8, seal:false);
                Require(f.Effects.ActivationCount == 0 && f.Vitals.Mana == 100, "迟到口令不能复用分手前的律印");
            }
            using (var f = new Fixture("breath.stream"))
            {
                f.Sample(8, near:false); f.Speak(); f.Sample(1, near:false);
                f.HandsApart = true; f.Sample(2, near:false, seal:false);
                f.HandsApart = false; f.Sample(8);
                Require(f.Effects.ActivationCount == 0 && f.Vitals.Mana == 100, "等待举印时明确分手应使原口令失效");
            }
        }

        static void VerifyStream()
        {
            using var f = new Fixture("breath.stream");
            f.StartFromChest();
            var stream = f.Head.Find("龙火之术");
            Require(stream, "持续喷口应挂在头显下");
            int id = stream.GetInstanceID();
            float initial = f.Vitals.Mana;
            var forwardTarget = f.Target(f.Mouth + f.Head.forward * 1.3f);
            f.Sample(24);
            Require(forwardTarget.HitCount > 0 && f.Vitals.Mana < initial, "正前方持续伤害与度力扣除");
            int hits = forwardTarget.HitCount;
            // 两手腕、指尖和头部都静止，右手弯曲的中指关节升为最高点。
            f.RightKnuckle = new Vector3(.10f, .045f, .12f);
            Vector3 direction = f.Head.TransformDirection(f.RightKnuckle.normalized);
            var sideTarget = f.Target(f.Mouth + direction * 1.3f);
            f.Sample(24);
            Require(sideTarget.HitCount > 0 && forwardTarget.HitCount == hits &&
                Vector3.Angle(stream.forward, direction) < 1f, "曲指关节升过两手指尖时喷口和实际命中都应转向侧靶");
            f.TipsTracked = false;
            int sideHits = sideTarget.HitCount;
            f.Sample(20);
            Require(sideTarget.HitCount > sideHits && stream.gameObject.activeSelf &&
                Vector3.Angle(stream.forward, direction) < 1f, "指尖全部遮挡，仍按可见的曲指关节喷火命中");
            sideTarget.gameObject.SetActive(false);
            f.LeftKnuckle = new Vector3(-.10f, .049f, .12f);
            f.Sample(1);
            Vector3 blended = .75f * f.LeftKnuckle + .25f * f.RightKnuckle;
            Require(Vector3.Angle(stream.forward, f.Head.TransformDirection(blended.normalized)) < 1f,
                "两手关节只差 4 毫米时应按 8 毫米过渡带偏向较高的一手");
            f.RightPalm = new Vector3(-.07f, .08f, .08f);
            direction = f.Head.TransformDirection(f.RightPalm.normalized);
            var palmTarget = f.Target(f.Mouth + direction * 1.3f);
            f.Sample(20);
            Require(palmTarget.HitCount > 0 && Vector3.Angle(stream.forward, direction) < 1f,
                "掌心高于指节时应按掌心方向命中");
            palmTarget.gameObject.SetActive(false);
            f.LeftKnuckle = new Vector3(-.02f, -.02f, .10f);
            f.RightKnuckle = new Vector3(.02f, -.02f, .10f);
            f.RightPalm = new Vector3(.02f, -.035f, .075f);
            f.TipsTracked = true;
            f.Root.transform.position += Vector3.right;
            f.Head.rotation = Quaternion.Euler(0, 90, 0);
            var second = f.Target(f.Mouth + f.Head.forward * 1.3f);
            var wall = f.Cube(f.Mouth + f.Head.forward * .65f, Vector3.one * .55f);
            f.Sample(24);
            Require(stream && id == stream.GetInstanceID() &&
                Vector3.Distance(stream.position, f.Mouth) < .001f,
                "移动、转头期间应是原来的喷口，保持头部局部偏移");
            Require(forwardTarget.HitCount == hits && second.HitCount == 0, "转头后离开旧靶，墙后靶免伤");
            wall.SetActive(false);
            Physics.SyncTransforms();
            f.Sample(24);
            Require(second.HitCount > 0 && f.Effects.ActivationCount == 1, "移开墙后新方向命中，整段只施法一次");
        }

        static void VerifyStreamJointSwitches()
        {
            using var f = new Fixture("breath.stream");
            f.RightKnuckle = new Vector3(.07f, .040f, .12f);
            f.RightPalm = new Vector3(-.07f, .020f, .12f);
            f.StartFromChest();
            var stream = f.Head.Find("龙火之术");
            Require(stream, "应已生成持续喷口");

            void CheckNext(Vector3 expected, string reason)
            {
                f.Sample(1);
                Require(stream.gameObject.activeSelf &&
                    Vector3.Angle(stream.forward, f.Head.TransformDirection(expected)) < 1f, reason);
            }

            f.RightKnuckle.x = .10f;
            f.RightPalm.y = .043f;
            CheckNext(f.RightKnuckle, "新掌心高 3 毫米应保留旧指节，并跟到旧指节的最新位置");
            f.RightPalm.y = .045f;
            CheckNext(f.RightPalm, "新掌心高 5 毫米应立即改用掌心");
            f.RightKnuckle.y = .048f;
            CheckNext(f.RightPalm, "反向高 3 毫米须继续使用掌心");
            f.RightKnuckle.y = .050f;
            CheckNext(f.RightKnuckle, "反向高 5 毫米应切回指节");

            f.RightPalm.y = .049f;
            f.RightKnuckleTracked = false;
            CheckNext(f.RightPalm, "旧指节失效时该帧改用略低但有效的掌心");
            f.RightKnuckleTracked = true;
            CheckNext(f.RightPalm, "重新可见的指节仅高 1 毫米，不能立即抢回印顶");
            f.RightWristOnly = true;
            f.Sample(1);
            Require(!stream.gameObject.activeSelf && f.Effects.HasOccupiedHand, "短暂失追应暂停原喷口");
            f.RightWristOnly = false;
            CheckNext(f.RightKnuckle, "重新追踪后按此刻最高点重选，不能沿用失追前的掌心");
            Require(f.Effects.ActivationCount == 1, "换点与短暂失追恢复不能反复起手");
        }

        static void VerifyStreamAimLoss()
        {
            using var f = new Fixture("breath.stream");
            f.StartFromChest();
            var stream = f.Head.Find("龙火之术");
            var target = f.Target(f.Mouth + f.Head.forward);
            f.Sample(16);
            float mana = f.Vitals.Mana;
            int hits = target.HitCount;
            Require(hits > 0 && stream && stream.gameObject.activeSelf, "丢失印顶之前须实际喷火命中");
            f.LeftWristOnly = f.RightWristOnly = true;
            f.Sample(2);
            Require(!stream.gameObject.activeSelf && f.Effects.HasOccupiedHand &&
                f.Vitals.Mana == mana && target.HitCount == hits, "双手短暂只剩腕部时原施法暂停发射和扣费");
            f.LeftWristOnly = f.RightWristOnly = false;
            f.Sample(20);
            Require(stream.gameObject.activeSelf && f.Effects.ActivationCount == 1 &&
                f.Vitals.Mana < mana && target.HitCount > hits, "腕以外的手骨重新可见时原施法恢复喷射并命中");
            f.HandOffset = Vector3.back * .18f;
            mana = f.Vitals.Mana;
            hits = target.HitCount;
            f.Sample(20);
            Require(!f.Effects.HasOccupiedHand && f.Vitals.Mana == mana && target.HitCount == hits,
                "印顶移到嘴后应停喷，从失效首帧起无扣费或命中");
        }

        static void VerifyBlockedBurstAim(bool behindMouth)
        {
            using var f = new Fixture("breath.burst", legacyMouthBurst:true);
            if (behindMouth) f.HandOffset = Vector3.back * .18f;
            else f.LeftWristOnly = true;
            float initial = f.Vitals.Mana;
            f.Sample(6);
            f.Speak();
            f.Sample(12);
            Require(f.Effects.ActivationCount == 0 && f.Effects.LaunchCount == 0 &&
                f.Vitals.Mana == initial && f.Cooldown == 0,
                behindMouth ? "印在嘴边但印顶在嘴后时不能发弹或收费" : "一手只有腕部、另一手完整时不能发弹或收费");
        }

        static void VerifyRelease(bool leaveMouth)
        {
            using var f = new Fixture("breath.stream");
            f.StartFromChest();
            var pilot = f.Head.Find("龙火待吹火苗(Clone)");
            var stream = f.Head.Find("龙火之术");
            Require(pilot && stream && pilot.gameObject.activeSelf, "已激活的火流保留印上火苗");
            f.HandsApart = !leaveMouth;
            f.Sample(12, near:!leaveMouth, seal:leaveMouth);
            f.HandsApart = false;
            float remaining = f.Vitals.Mana;
            var target = f.Target(f.Mouth + f.Head.forward);
            if (leaveMouth)
            {
                for (int i = 0; i < 2; i++)
                {
                    Require(!stream.gameObject.activeSelf && pilot.gameObject.activeSelf &&
                        Vector3.Distance(pilot.position, f.Mouth) > .15f, "停喷同一簇火苗随双手远离喷口");
                    f.SetMouthDistance((f.StartDistance + f.StopDistance) * .5f);
                    float pausedMana = f.Vitals.Mana;
                    int hits = target.HitCount;
                    f.Sample(24);
                    Require(pilot.gameObject.activeSelf && f.Vitals.Mana == pausedMana && target.HitCount == hits,
                        "暂停时停在起手和停喷阈值之间不续喷、不消耗、不命中");
                    f.HandOffset = Vector3.zero;
                    f.Sample(18);
                    Require(pilot.gameObject.activeSelf && stream.gameObject.activeSelf && f.Vitals.Mana < pausedMana &&
                        target.HitCount > hits && f.Effects.ActivationCount == 1, "同次结印重新近嘴续喷");
                    f.Sample(2, near:false);
                }
                f.HandsApart = true;
                f.Sample(2, near:false, seal:false);
                Require(!f.Effects.HasOccupiedHand && !pilot.gameObject.activeSelf && !stream.gameObject.activeSelf,
                    "待吹期间松印应同时清除火流与火苗");
                return;
            }
            f.Sample(20);
            Require(!pilot.gameObject.activeSelf && Math.Abs(f.Vitals.Mana - remaining) < .001f && target.HitCount == 0 &&
                f.Effects.ActivationCount == 1, "松印后火苗与火流结束，重结不会重放口令");
        }

        static void VerifyExhaustion()
        {
            using var f = new Fixture("breath.stream", 5);
            f.StartFromChest();
            f.Sample(1, step: .5);
            Require(f.Vitals.Mana < .001f && !f.Effects.HasOccupiedHand, "度力耗尽当帧应停火");
            CheckCooldown(f.Cooldown, 2.125f, "剩余 1 点度力只够喷 0.125 秒，长帧只计入付费部分");
            var target = f.Target(f.Mouth + f.Head.forward);
            f.Sample(20);
            Require(target.HitCount == 0 && f.Effects.ActivationCount == 1, "度力耗尽后没有伤害或补发");
        }

        static void VerifySustainedCooldown()
        {
            using var f = new Fixture("breath.stream");
            f.StartFromChest();
            f.Sample(60);
            Require(f.Effects.HasOccupiedHand, "喷射 1.5 秒后仍在持续施法");
            bool reserved = f.Vitals.TryReserveCast("try-other-breath", "other.breath", f.Group, 0, 1, f.Now,
                out var other, out var failure, out _);
            other?.Dispose();
            Require(!reserved && failure == CastReservationFailure.Cooldown, "喷射期间同冷却组的其他技能应被拦住");

            var snapshot = f.Vitals.CaptureVitals(f.Now);
            var restored = new RpgPlayerState(100, 100, 0);
            Require(restored.RestoreVitals(snapshot, 100, out var reason), "喷射中检查点应可恢复：" + reason);
            CheckCooldown(restored.GetCooldown(f.Group, 100), 3.5f, "读档时保留已喷 1.5 秒的完整冷却");
            CheckCooldown(restored.GetCooldown(f.Group, 101), 2.5f, "读档后应正常倒计时");

            Require(f.Effects.CancelLatest(), "主动停火应成功");
            CheckCooldown(f.Cooldown, 3.5f, "喷射 1.5 秒后从停火时开始冷却");
            f.Sample(40);
            CheckCooldown(f.Cooldown, 2.5f, "停火 1 秒后实际剩余冷却");
        }

        static void VerifyCooldownLimits()
        {
            using (var f = new Fixture("breath.stream"))
            {
                f.StartFromChest();
                Require(f.Effects.CancelUnreleased(), "起手后立即收印应成功");
                CheckCooldown(f.Cooldown, 2, "立即停火取最小冷却");
            }
            using (var f = new Fixture("breath.stream"))
            {
                f.StartFromChest();
                f.Sample(280);
                Require(f.Effects.HasOccupiedHand && f.Effects.CancelLatest(), "连续喷射 7 秒应能主动停火");
                CheckCooldown(f.Cooldown, 8, "长时间喷射达到冷却上限");
            }
            using (var f = new Fixture("breath.stream", 4))
            {
                f.StartFromChest(expectSuccess: false);
                CheckCooldown(f.Cooldown, 0, "度力仅够起手，施法失败不产生冷却");
            }
        }

        static void VerifyOptionalDistance()
        {
            using var f = new Fixture("breath.stream", ignoreDistance: true);
            f.Sample(6, near: false);
            f.Speak();
            f.Sample(8, near: false);
            Require(f.Effects.ActivationCount == 1 && f.Effects.HasOccupiedHand,
                "关闭嘴部条件后，在胸前保持手印也能持续施法");
            f.HandsApart = true;
            f.Sample(8, near: false, seal: false);
            Require(!f.Effects.HasOccupiedHand, "关闭距离条件仍应在松印时停止持续术");
        }

        static void VerifyBurst()
        {
            using var f = new Fixture("breath.burst", legacyMouthBurst:true);
            f.LeftKnuckle = new Vector3(-.09f, .04f, .12f);
            f.RightKnuckle = new Vector3(-.05f, .04f, .12f);
            f.TipsTracked = false;
            f.StartFromChest();
            CheckCooldown(f.Cooldown, 1.5f, "单次吐息在发射时进入固定冷却");
            var projectiles = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None);
            var bolt = projectiles.Single(p => Vector3.Distance(p.transform.position, f.Head.position) < .6f);
            Vector3 start = bolt.transform.position;
            Vector3 direction = f.Head.TransformDirection((f.LeftKnuckle + f.RightKnuckle).normalized);
            Require(Vector3.Distance(start, f.Mouth) < .001f && Vector3.Angle(bolt.transform.forward, direction) < 1f &&
                Vector3.Angle(f.Effects.LastLaunchDirection, direction) < 1f, "指尖遮挡时飞炎从嘴部沿齐平的两手曲指关节中点发出");
            var sideTarget = f.Target(start + direction * 1.2f);
            var forwardTarget = f.Target(start + f.Head.forward * 1.2f);
            float paid = f.Vitals.Mana;
            f.Root.transform.position += Vector3.right * 1.7f;
            f.Head.rotation = Quaternion.Euler(0, 85, 0);
            f.Sample(10, near:false, seal:false);
            CheckCooldown(f.Cooldown, 1.25f, "单次吐息的固定冷却从发射时倒计时");
            Require(bolt && !bolt.transform.IsChildOf(f.Head) && !bolt.IsFinished &&
                Vector3.Distance(start, bolt.transform.position) < .01f, "单次飞弹应脱离玩家，松手后仍存在");
            var move = BindUpdate(bolt, "Update");
            move();
            Require(Vector3.Dot(bolt.transform.position - start, direction) > 0 &&
                Vector3.Angle(bolt.transform.position - start, direction) < 1f,
                "飞弹应沿出手时的方向前进");
            int stepsToTarget = Mathf.CeilToInt(1.3f / (bolt.speed * Time.deltaTime));
            for (int i = 0; i < stepsToTarget && !bolt.IsFinished; i++) move();
            Require(sideTarget.HitCount == 1 && forwardTarget.HitCount == 0, "离手后实际命中曲指关节瞄准的侧靶");
            Require(Math.Abs(f.Vitals.Mana - paid) < .001f, "单次施法只扣起手度力");
        }

        static void VerifyBurstImpact()
        {
            SkillProjectile Launch(Fixture f)
            {
                f.StartFromChest();
                var bolt = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None)
                    .Single(p => Vector3.Distance(p.transform.position, f.Mouth) < .01f);
                Require(bolt.impactPrefab, "玩家飞炎 SO 应将爆散预制体交给弹体");
                return bolt;
            }

            int MarksAt(Vector3 point) => Object.FindObjectsByType<DecalProjector>(FindObjectsSortMode.None)
                .Count(p => p.gameObject.name == "场景技能痕迹" && Vector3.Distance(p.transform.position, point) < .5f);

            using (var f = new Fixture("breath.burst", legacyMouthBurst:true))
            {
                var bolt = Launch(f);
                Vector3 direction = bolt.transform.forward;
                var wall = f.Cube(f.Mouth + direction * 1.2f, new Vector3(2, 2, .25f));
                wall.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 35, 0);
                Physics.SyncTransforms();
                var normal = -wall.transform.forward;
                var face = wall.transform.position + normal * .125f;
                int impacts = 0;
                GameObject visual = null;
                bolt.ImpactCreated += instance => { ++impacts; visual = instance; };
                var move = BindUpdate(bolt, "Update");
                int steps = Mathf.CeilToInt(1.8f / (bolt.speed * Time.deltaTime));
                for (int i = 0; i < steps && !bolt.IsFinished; i++) move();
                move();
                Require(bolt.IsFinished && impacts == 1 && visual && visual.activeInHierarchy &&
                    !visual.transform.IsChildOf(bolt.transform), "飞炎碰墙应脱离弹体只爆散一次");
                var impact = visual.transform;
                Require(Mathf.Abs(Vector3.Dot(impact.position - face, normal)) < .002f &&
                    Vector3.Dot(impact.forward, normal) > .999f && MarksAt(impact.position) == 1,
                    "斜墙爆散贴合实际接触平面、沿碰撞法线，并与一枚焦痕共存");
                f.Effects.ClearAll();
                Require(!visual.activeSelf && MarksAt(impact.position) == 0, "清理施法者应同时回收爆散和焦痕");
            }

            using (var f = new Fixture("breath.burst", legacyMouthBurst:true))
            {
                var pool = f.Root.AddComponent<VfxPoolManager>();
                pool.impactPrefab = f.Settings.impactPrefab;
                pool.capacity = 1;
                pool.Prewarm();
                f.Effects.vfxPool = pool;
                var bolt = Launch(f);
                var target = f.Target(f.Mouth + bolt.transform.forward * .7f);
                float before = target.CurrentHealth;
                int impacts = 0;
                bolt.ImpactCreated += _ => ++impacts;
                var move = BindUpdate(bolt, "Update");
                int steps = Mathf.CeilToInt(1f / (bolt.speed * Time.deltaTime));
                for (int i = 0; i < steps && !bolt.IsFinished; i++) move();
                move();
                Require(target.HitCount == 1 && Mathf.Approximately(before - target.CurrentHealth, bolt.damage) &&
                    impacts == 1 && pool.ActiveCount == 1 && MarksAt(target.transform.position) == 0,
                    "击中训练靶应正常扣血，仅租用一次池化爆散");
                f.Effects.ClearAll();
                Require(pool.AvailableCount == 1 && pool.ActiveCount == 0, "清理施法者应归还命中爆散槽位");
            }

            using (var f = new Fixture("breath.burst", legacyMouthBurst:true))
            {
                var bolt = Launch(f);
                var enemy = f.Cube(bolt.transform.position, Vector3.one * .8f);
                enemy.SetActive(false);
                var entity = enemy.AddComponent<EnemyObj>();
                enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                var ai = enemy.AddComponent<EnemyController>();
                enemy.GetComponent<BoxCollider>().isTrigger = true;
                enemy.AddComponent<EnemyHitZone>().owner = entity;
                enemy.SetActive(true);
                ai.ResetForSpawn();
                Physics.SyncTransforms();
                var direction = bolt.transform.forward;
                Require(enemy.GetComponent<Collider>().Raycast(new Ray(f.Mouth - direction, direction),
                    out var entry, 2), "回归敌人触发区应包住当前弹心");
                float health = entity.Combat.Health;
                int impacts = 0;
                GameObject visual = null;
                bolt.ImpactCreated += instance => { ++impacts; visual = instance; };
                bolt.CheckLaunchOverlap();
                bolt.CheckLaunchOverlap();
                Require(impacts == 1 && visual && entity.Combat.Health < health &&
                    Vector3.Distance(visual.transform.position, entry.point) < .002f &&
                    Vector3.Dot(visual.transform.forward, entry.normal) > .999f && MarksAt(entry.point) == 0,
                    "深度重叠敌人触发区：只结算一次，爆散贴在入射侧表面");
            }

            foreach (bool timeout in new[] { false, true })
            {
                using var f = new Fixture("breath.burst", legacyMouthBurst:true);
                var bolt = Launch(f);
                int impacts = 0;
                bolt.ImpactCreated += _ => ++impacts;
                if (timeout)
                {
                    bolt.GameClock = () => f.Now + bolt.lifetime + 1;
                    BindUpdate(bolt, "Update")();
                }
                else f.Effects.ClearAll();
                Require(bolt.IsFinished && impacts == 0, "飞炎未击中时超时或中断均应安静结束");
            }
            Debug.Log("FEIYAN_IMPACT_PASS angled-wall+decal, pooled training damage x1 + lease returned, enemy trigger overlap x1, timeout/cancel x0");
        }

        static void VerifyBurstJointSwitches()
        {
            using var f = new Fixture("breath.burst", legacyMouthBurst:true);
            f.RightKnuckle = new Vector3(.07f, .040f, .12f);
            f.RightPalm = new Vector3(-.07f, .020f, .12f);
            f.Sample(6, near:false);
            f.Speak();
            f.Sample(4, near:false);
            Require(f.Effects.LaunchCount == 0, "胸前结印应等待举到嘴前");

            f.RightPalm.y = .043f;
            f.Sample(1);
            Require(f.Effects.LaunchCount == 1 &&
                Vector3.Angle(f.Effects.LastLaunchDirection, f.Head.TransformDirection(f.RightKnuckle)) < 1f,
                "举印出手时新关节高 3 毫米，应沿准备期间选中的指节发出飞炎");

            f.Sample(70, seal:false);
            f.Sample(8);
            CheckCooldown(f.Cooldown, 0, "第二次施法前冷却已结束");
            // 同步采样只推进输入时钟。第二次直接提交已核准的施法，保留匹配器的同帧防重。
            var second = f.ActivateCurrentSkill();
            Require(second.Confirmed && f.Effects.LaunchCount == 2 &&
                Vector3.Angle(f.Effects.LastLaunchDirection, f.Head.TransformDirection(f.RightPalm)) < 1f,
                "松印并重新施放飞炎时，应从新一轮的最高点开始");
        }

        static void VerifyBurstAfterInterruptedSeal()
        {
            using var f = new Fixture("breath.burst", legacyMouthBurst:true);
            f.RightKnuckle = new Vector3(.07f, .040f, .12f);
            f.RightPalm = new Vector3(-.07f, .020f, .12f);
            f.Sample(6, near:false);
            f.Speak();
            f.Sample(4, near:false);
            f.Sample(10, near:false, seal:false); // 连续松印 0.25 秒，覆盖正式配置的 0.15 秒退印时间。
            f.RightPalm.y = .043f;
            f.Sample(8);
            Require(f.Effects.LaunchCount == 0 && f.Vitals.Mana == 100 && f.Cooldown == 0,
                $"胸前松印后再举印到嘴边，原口令不能发弹或收费。匹配状态为 {f.MatchStatus}");
            f.Speak();
            f.Sample(4);
            Require(f.Effects.ActivationCount == 1 && f.Effects.LaunchCount == 1 &&
                Vector3.Angle(f.Effects.LastLaunchDirection, f.Head.TransformDirection(f.RightPalm)) < 1f,
                $"重新结印并念口令，应只沿新一轮略高的掌心发射一次。匹配状态为 {f.MatchStatus}");
        }

        static Action BindUpdate(MonoBehaviour component, string method) =>
            (Action)component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action), component);

        internal sealed class Fixture : ISkillActivationPolicy, IDisposable
        {
            public readonly GameObject Root;
            public readonly Transform Head;
            public readonly SkillController Effects;
            public readonly RpgPlayerState Vitals;
            readonly SkillInputController input;
            readonly Lab lab;
            readonly SkillDefinition skill;
            readonly SkillCatalog customCatalog;
            readonly SealProfile profile, releaseProfile;
            readonly BreathEffectDefinition legacyEffect;
            public bool ReleaseSeal;
            public void ResetInput() => input.BeginFreshPlayerInput(false);
            public void Cancel() => input.Cancel();
            public string LastActivated;
            readonly List<GameObject> objects = new();
            readonly Action updateInput, updateEffects;
            int version, casts;
            double now = 10;
            public Vector3 LeftKnuckle = new(-.02f, -.02f, .10f);
            public Vector3 RightKnuckle = new(.02f, -.02f, .10f);
            public Vector3 RightPalm = new(.02f, -.035f, .075f);
            public Vector3 HandOffset;
            public bool HandsApart;
            public float StartDistance => skill.mouthStartDistance;
            public float StopDistance => skill.mouthStopDistance;
            public void SetMouthDistance(float metres) => HandOffset = new Vector3(0, .045f, metres - .06f);
            public bool LeftWristOnly, RightWristOnly;
            public bool TipsTracked = true;
            public bool RightKnuckleTracked = true;
            public BreathEffectDefinition Settings => (BreathEffectDefinition)skill.effectDefinition;
            public Vector3 Mouth => Head.TransformPoint(Settings.mouthOffset);
            public double Now => now;
            public string Group => string.IsNullOrEmpty(skill.cooldownGroup) ? skill.skillId : skill.cooldownGroup;
            public float Cooldown => Vitals.GetCooldown(Group, now);
            public string ActivationStatus => Effects.Status;
            public string MatchStatus => input.Engine.Status;

            public Fixture(string id, float initialPower = 100, bool ignoreDistance = false, bool legacyMouthBurst = false)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath);
                skill = catalog.skills.Single(s => s && s.skillId == id);
                Require(skill.effectDefinition is BreathEffectDefinition && skill.ValidateDefinition() == null, "正式技能配置 " + id);
                if (ignoreDistance || legacyMouthBurst)
                {
                    skill = Object.Instantiate(skill);
                    skill.castCondition = ignoreDistance ? SkillCastCondition.None : SkillCastCondition.SealNearMouth;
                    if (legacyMouthBurst)
                    {
                        skill.followingSeals = Array.Empty<string>();
                        legacyEffect = Object.Instantiate((BreathEffectDefinition)skill.effectDefinition);
                        legacyEffect.mode = BreathCastMode.Burst;
                        skill.effectDefinition = legacyEffect;
                    }
                    customCatalog = ScriptableObject.CreateInstance<SkillCatalog>();
                    customCatalog.skills.Add(skill);
                    catalog = customCatalog;
                }
                profile = AssetDatabase.LoadAssetAtPath<SealProfile>("Assets/ScriptableObjects/手印识别配置/" + skill.sealName + ".asset");
                if (skill.followingSeals?.Length > 0)
                    releaseProfile = AssetDatabase.LoadAssetAtPath<SealProfile>("Assets/ScriptableObjects/手印识别配置/" + skill.followingSeals[0] + ".asset");
                Root = new GameObject("吐火回归玩家");
                Root.transform.position = new Vector3(243, 210, -237);
                Head = new GameObject("模拟头显").transform;
                Head.SetParent(Root.transform, false);
                Head.localPosition = Vector3.up * 1.6f;
                lab = Root.AddComponent<Lab>();
                lab.recordingOrigin = Root.transform;
                Effects = Root.AddComponent<SkillController>();
                Effects.lab = lab;
                Effects.head = Head;
                Effects.playerRoot = Root.transform;
                Effects.GameClock = Effects.HandInputClock = () => now;
                Vitals = new RpgPlayerState(100, 100, 0);
                Vitals.Respawn(100, initialPower, now, true);
                Effects.BindVitals(Vitals);
                input = Root.AddComponent<SkillInputController>();
                input.lab = lab;
                input.catalog = catalog;
                input.effects = Effects;
                input.enableCastTrace = input.logFeatureFailures = input.enableOpenHandCancel = false;
                input.InputClock = () => now;
                input.ActivationPolicy = this;
                input.InitializeMatcher();
                updateInput = BindUpdate(input, "LateUpdate");
                updateEffects = BindUpdate(Effects, "LateUpdate");
            }

            public SkillCastResult TryActivate(SkillDefinition def)
            {
                LastActivated = def.skillId;
                return Effects.CommitCast(def, Vitals, "breath-check-" + ++casts, def.manaCost, now, () => true);
            }

            public SkillCastResult ActivateCurrentSkill() => TryActivate(skill);

            public void StartFromChest(bool expectSuccess = true)
            {
                float before = Vitals.Mana;
                Sample(6, near:false);
                Speak();
                Sample(4, near:false);
                Require(Effects.ActivationCount == 0 && Vitals.Mana == before, "嘴部条件未满足时应保留口令且不付费");
                CheckCooldown(Cooldown, 0, "等待举印到嘴边期间没有冷却");
                Sample(1);
                if (expectSuccess)
                    Require(Effects.ActivationCount == 1 && Vitals.Mana < before, "举印到嘴边后从匹配入口完成施法：" + input.LastMessage + " / " + Effects.Status);
                else
                    Require(casts > 0 && Effects.ActivationCount == 0 && !Effects.HasOccupiedHand && Vitals.Mana == before,
                        "已匹配的持续施法在度力不足时应失败并退还起手消耗");
            }

            public void Speak(double latency = 0) => input.ReceiveVoice(skill.phrase, now - latency);

            public void Sample(int count, bool near = true, bool seal = true, double step = .025)
            {
                for (int i = 0; i < count; i++)
                {
                    now += step;
                    ++version;
                    Vector3 signOrigin = Mouth + Head.rotation * (HandOffset + (near ? Vector3.zero : new Vector3(0, -.39f, .21f)));
                    var frame = new SealFrame {time = now, left = Hand(signOrigin, true), right = Hand(signOrigin, false)};
                    typeof(Lab).GetProperty(nameof(Lab.Frame)).SetValue(lab, frame);
                    lab.Scores.Clear();
                    lab.Scores.Add(new SealScore {profile = profile, score = seal ? 1 : 0, coverage = 1, passes = seal});
                    if (releaseProfile)
                        lab.Scores.Add(new SealScore {profile = releaseProfile, score = ReleaseSeal ? 1 : 0, coverage = 1, passes = ReleaseSeal});
                    updateEffects();
                    updateInput();
                }
            }

            static readonly HandJointId[] FingerTips = { HandJointId.HandThumbTip, HandJointId.HandIndexTip,
                HandJointId.HandMiddleTip, HandJointId.HandRingTip, HandJointId.HandPinkyTip };

            HandSnapshot Hand(Vector3 signOrigin, bool left)
            {
                var hand = new HandSnapshot { connected = true, tracked = true, highConfidence = true, fresh = true, observedAt = now, version = version };
                if (HandsApart)
                    signOrigin += Head.right * ((skill.sealReleaseWristDistance + .02f) * .5f * (left ? -1 : 1));
                var wrist = signOrigin + Head.rotation * new Vector3(left ? -.02f : .02f, -.045f, .06f);
                var point = Root.transform.InverseTransformPoint(wrist);
                bool wristOnly = left ? LeftWristOnly : RightWristOnly;
                for (int i = 0; i < hand.joints.Length; i++)
                {
                    hand.joints[i] = new Pose(point, Quaternion.identity);
                    hand.jointValid[i] = !wristOnly || i == (int)HandJointId.HandWristRoot;
                }
                var knuckle = left ? HandJointId.HandIndex2 : HandJointId.HandMiddle2;
                var knuckleWorld = signOrigin + Head.rotation * (left ? LeftKnuckle : RightKnuckle);
                hand.joints[(int)knuckle].position = Root.transform.InverseTransformPoint(knuckleWorld);
                if (!left) hand.jointValid[(int)knuckle] = !wristOnly && RightKnuckleTracked;
                var palm = left ? new Vector3(-.02f, -.035f, .075f) : RightPalm;
                hand.joints[(int)HandJointId.HandPalm].position = Root.transform.InverseTransformPoint(signOrigin + Head.rotation * palm);
                var highFinger = left ? HandJointId.HandIndexTip : HandJointId.HandMiddleTip;
                var tip = new Vector3(left ? -.02f : .02f, 0, .12f);
                foreach (var joint in FingerTips)
                {
                    var offset = joint == highFinger ? tip : tip + Vector3.down * .05f;
                    var jointWorld = signOrigin + Head.rotation * offset;
                    hand.joints[(int)joint].position = Root.transform.InverseTransformPoint(jointWorld);
                    hand.jointValid[(int)joint] = TipsTracked && !wristOnly;
                }
                Array.Fill(hand.fingerConfidence, !wristOnly);
                return hand;
            }

            public GameObject Cube(Vector3 position, Vector3 size)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                objects.Add(cube);
                cube.transform.position = position;
                cube.transform.localScale = size;
                Physics.SyncTransforms();
                return cube;
            }

            public SkillTrainingTarget Target(Vector3 position) =>
                Cube(position, Vector3.one * .32f).AddComponent<SkillTrainingTarget>();

            public void Dispose()
            {
                Effects.ClearAll();
                foreach (var go in objects) if (go) Object.DestroyImmediate(go);
                Object.DestroyImmediate(Root);
                if (customCatalog)
                {
                    Object.DestroyImmediate(customCatalog);
                    Object.DestroyImmediate(skill);
                    if (legacyEffect) Object.DestroyImmediate(legacyEffect);
                }
            }
        }
    }
}
