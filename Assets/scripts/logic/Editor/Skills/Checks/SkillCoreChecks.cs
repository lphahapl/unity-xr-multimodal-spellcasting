#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NarutoVR.SealLab;
using NarutoVR.SealCombat.Events;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>使用真实配置资产进行确定性匹配检查，覆盖保持、窗口、消费、冷却与取消。</summary>
    public static class SkillCoreChecks
    {
        [MenuItem("NarutoVR/技能训练场/运行核心回归检查")]
        public static void RunMenu() => Debug.Log("技能核心回归通过：" + Run());

        public static int Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出播放模式后运行确定性核心检查。");
            int passed = 0;
            Check("先印后咒", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0); f.Voice(5, 5.1); Equal(1, f.Activations); }); passed++;
            Check("先咒后印", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Voice(0, .05); f.Hold(5); Equal(1, f.Activations); }); passed++;
            Check("同时输入", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Voice(0, 0); f.Hold(0); Equal(1, f.Activations); }); passed++;
            Check("8秒边界包含保持起点", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Emit(0); f.Emit(.07); f.Voice(8, 9.99); Equal(1, f.Activations); }); passed++;
            Check("10秒缓存完整覆盖8加2秒预算", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Emit(0); f.Emit(.07); f.Voice(8, 10); Equal(1, f.Activations); }); passed++;
            Check("窗口外不能激活", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Emit(0); f.Emit(.07); f.Voice(8.001, 8.1); Equal(0, f.Activations); }); passed++;
            Check("先语音窗口结束", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Voice(0, 0); f.Hold(9); Equal(0, f.Activations); True(string.IsNullOrEmpty(f.Engine.PendingSkillName), "超时必须清待选"); }); passed++;
            Check("Tick晚于合法边界样本仍接收", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Voice(0, 0); f.Emit(7.92); f.Emit(7.96); f.Engine.Tick(8.01);
                True(string.IsNullOrEmpty(f.Engine.PendingSkillName), "超时HUD应立即清待选");
                f.Engine.Observe(f.Frame(8), f.Scores(.9f)); Equal(1, f.Activations);
            }); passed++;
            Check("处理宽限不扩大8秒输入窗口", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Voice(0, 0); f.Emit(7.921); f.Emit(7.961); f.Engine.Tick(8.01);
                f.Engine.Observe(f.Frame(8.001), f.Scores(.9f)); Equal(0, f.Activations);
            }); passed++;
            Check("超过120毫秒不接受边界迟处理", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Voice(0, 0); f.Emit(7.92); f.Emit(7.96); f.Engine.Tick(8.121);
                f.Engine.Observe(f.Frame(8), f.Scores(.9f)); Equal(0, f.Activations);
            }); passed++;
            Check("取消清除迟处理候选及代次", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Voice(0, 0); f.Emit(7.92); f.Emit(7.96); f.Engine.Tick(8.01);
                long epoch = f.Engine.Epoch; f.Engine.Cancel(8.01); True(f.Engine.Epoch > epoch, "必须更新取消代次");
                f.Engine.Observe(f.Frame(8), f.Scores(.9f)); Equal(0, f.Activations);
            }); passed++;
            Check("语音延迟2秒边界", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0); f.Voice(5, 7); Equal(1, f.Activations); }); passed++;
            Check("语音超延迟拒绝", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0); f.Voice(5, 7.001); Equal(0, f.Activations); True(f.Engine.Status.Contains("延迟"), "应报告语音延迟"); }); passed++;
            Check("完整口令精确匹配", SkillTriggerMode.VoiceOnly, f =>
            { f.Engine.Voice("我想使用火球术", 0, 0); Equal(0, f.Activations); f.Voice(.1, .1); Equal(1, f.Activations); }); passed++;
            Check("纯语音无需手部追踪", SkillTriggerMode.VoiceOnly, f =>
            { f.Emit(0, .9f, false); f.Voice(.1, .1); Equal(1, f.Activations); }); passed++;
            Check("重复语音回调", SkillTriggerMode.VoiceOnly, f =>
            { f.Voice(.1, .1); f.Voice(.1, .2); Equal(1, f.Activations); }); passed++;
            Check("历史减免保留保持时间", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0, .72f); f.Voice(1, 1); Equal(1, f.Activations); }); passed++;
            Check("瞬时过线不能回判", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Emit(0, .72f); f.Emit(.02, .1f); f.Voice(.1, .1); Equal(0, f.Activations); }); passed++;
            Check("纯印没有语音减免", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0, .72f); Equal(0, f.Activations); }); passed++;
            Check("阈值下限不可越过", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0, .64f); f.Voice(.2, .2); Equal(0, f.Activations); }, s => s.voiceDiscount = .5f); passed++;
            Check("覆盖率不可减免", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0, .95f, .7f); f.Voice(.2, .2); Equal(0, f.Activations); }); passed++;
            Check("必需特征缺失不可减免", SkillTriggerMode.VoiceAndGesture, f =>
            { f.EmitMissing(0); f.EmitMissing(.08); f.Voice(.2, .2); Equal(0, f.Activations); }); passed++;
            Check("比较未装备竞争印原始分", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0, .72f, 1, .70f); f.Voice(.2, .2); Equal(0, f.Activations); }); passed++;
            Check("竞争印5分边界", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0, .80f, 1, .75f); f.Voice(.2, .2); Equal(1, f.Activations); }); passed++;
            Check("双手不同步拒绝", SkillTriggerMode.GestureOnly, f =>
            { f.Emit(0, .9f, true, 1, .1f, .061); f.Emit(.08, .9f, true, 1, .1f, .061); Equal(0, f.Activations); }); passed++;
            Check("低置信沿用模板政策", SkillTriggerMode.GestureOnly, f =>
            {
                f.Emit(0, .9f, true, 1, .1f, 0, false); f.Emit(.08, .9f, true, 1, .1f, 0, false); Equal(0, f.Activations);
                f.Target.allowLowConfidence = true;
                f.Emit(.1, .9f, true, 1, .1f, 0, false); f.Emit(.18, .9f, true, 1, .1f, 0, false); Equal(1, f.Activations);
            }); passed++;
            Check("断帧不可拼接保持", SkillTriggerMode.GestureOnly, f =>
            { f.Emit(0); f.Emit(.5); Equal(0, f.Activations); f.Emit(.58); Equal(1, f.Activations); }); passed++;
            Check("同版本样本不推进保持", SkillTriggerMode.GestureOnly, f =>
            { f.Emit(0); f.Version--; f.Emit(.08); Equal(0, f.Activations); }); passed++;
            Check("长保持只激活一次", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); for (int i = 6; i <= 220; i++) f.Emit(i * .02); Equal(1, f.Activations); }); passed++;
            Check("长时间持续保持只响应不同的新口令", SkillTriggerMode.VoiceAndGesture, f =>
            {
                for (int i = 0; i <= 650; i++) f.Emit(i * .02);
                f.Voice(13, 13.01); Equal(1, f.Activations);
                f.Voice(13, 13.02); Equal(1, f.Activations);
                for (int i = 651; i <= 1300; i++) f.Emit(i * .02);
                Equal(1, f.Activations); f.Voice(26, 26.01); Equal(2, f.Activations);
            }, s => s.cooldownSeconds = 0); passed++;
            Check("组合新口令先到也须等待连续一秒的同印", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Hold(0); f.Voice(.1,.1);
                for(int i=6;i<=30;i++)f.Emit(i*.02);
                f.Voice(.6,.6);Equal(1,f.Activations);
                True(f.Engine.ObservationDiagnostics.Contains("重备剩余 0.50"),"应显示当前重备倒计时");
                for(int i=31;i<55;i++)f.Emit(i*.02);
                Equal(1,f.Activations);f.Emit(1.1);Equal(2,f.Activations);
                f.Voice(.6,1.11);for(int i=56;i<=115;i++)f.Emit(i*.02);
                Equal(2,f.Activations);
            },s=>s.cooldownSeconds=0);passed++;
            Check("组合重备被遮挡、歧义和断帧打断", SkillTriggerMode.VoiceAndGesture,f=>
            {
                f.Hold(0);f.Voice(.1,.1);f.Voice(.12,.12);
                for(int i=6;i<=40;i++)f.Emit(i*.02);
                f.Emit(.82,.9f,false);
                for(int i=42;i<=76;i++)f.Emit(i*.02);
                f.Emit(1.54,.9f,true,1,.89f);
                for(int i=78;i<=112;i++)f.Emit(i*.02);
                for(int i=125;i<175;i++)f.Emit(i*.02); // gap at 2.50; no time during the gap counts.
                Equal(1,f.Activations);f.Emit(3.5);Equal(2,f.Activations);
            },s=>s.cooldownSeconds=0);passed++;
            Check("取消清旧词而持续有效的新样本可重新就绪",SkillTriggerMode.VoiceAndGesture,f=>
            {
                f.Hold(0);f.Voice(.1,.1);
                for(int i=6;i<=45;i++)f.Emit(i*.02);
                f.Engine.Cancel(.91);f.Voice(.90,.92);
                for(int i=47;i<=97;i++)f.Emit(i*.02);
                Equal(1,f.Activations);f.Voice(.90,1.95);Equal(1,f.Activations);
                f.Voice(1.96,1.96);Equal(2,f.Activations);
            },s=>s.cooldownSeconds=0);passed++;
            Check("持有物暂停并重置同印重备，松手后按配置计时",SkillTriggerMode.VoiceAndGesture,f=>
            {
                f.Hold(0);f.Voice(.1,.1);
                for(int i=6;i<=15;i++)f.Emit(i*.02);
                for(int i=16;i<=70;i++)f.Emit(i*.02,occupied:true);
                True(f.Engine.ObservationDiagnostics.Contains("重备暂停"),"持有期间应说明暂停原因");
                f.Voice(1.4,1.4);
                for(int i=71;i<96;i++)f.Emit(i*.02);
                Equal(1,f.Activations);f.Emit(1.92);Equal(2,f.Activations);
            },s=>s.cooldownSeconds=0,new SkillMatchSettings{comboHeldRearmSeconds=.5f});passed++;
            Check("长保持恢复需重新完成保持才能滚动", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Hold(0); f.Emit(.2, .9f, false); f.Engine.Tick(11);
                f.Emit(11.01); f.Emit(11.03); f.Voice(11.03, 11.04); Equal(0, f.Activations);
                f.Emit(11.05); Equal(0, f.Activations); f.Emit(11.09); Equal(1, f.Activations);
            }); passed++;
            Check("未消费长保持滚动不逐帧分配", SkillTriggerMode.VoiceAndGesture, f =>
            {
                var frame = f.Frame(0); var scores = f.Scores(.9f);
                for (int i = 0; i < 650; i++)
                {
                    frame.time = i * .02; frame.left.observedAt = frame.right.observedAt = frame.time;
                    frame.left.version = frame.right.version = i; f.Engine.Tick(frame.time); f.Engine.Observe(frame, scores);
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 650; i < 750; i++)
                {
                    frame.time = i * .02; frame.left.observedAt = frame.right.observedAt = frame.time;
                    frame.left.version = frame.right.version = i; f.Engine.Tick(frame.time); f.Engine.Observe(frame, scores);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                True(allocated == 0, "滚动保持分配了 " + allocated + " 字节");
                f.Voice(15, 15); Equal(1, f.Activations);
            }); passed++;
            Check("追踪恢复不能再激活", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); f.Emit(.2, .9f, false); f.Hold(.3); Equal(1, f.Activations); }); passed++;
            Check("有效退出后可重新输入", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); f.Exit(.2); f.Hold(.5); Equal(2, f.Activations); }, s => s.cooldownSeconds = 0); passed++;
            Check("取消清缓冲与代次并锁住原姿态", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Hold(0); long epoch = f.Engine.Epoch; f.Engine.Cancel(.2);
                Equal(0, f.Engine.BufferCount); True(f.Engine.Epoch > epoch, "取消必须提升输入代次");
                f.Voice(.1, .21); f.Hold(.3); f.Voice(.5, .5); Equal(0, f.Activations);
                f.Exit(.6); f.Hold(.9); Equal(1, f.Activations);
            }); passed++;
            Check("取消口令走同一取消路径", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Voice(0, 0); f.Engine.Tick(.1); f.Engine.Voice("取消施法", .1, .1); True(string.IsNullOrEmpty(f.Engine.PendingSkillName), "取消需清待选"); }); passed++;
            Check("取消锁定时仍观察退出并重新施法", SkillTriggerMode.GestureOnly, f =>
            {
                f.Hold(0); f.Engine.Cancel(.2);
                for (int i = 0; i <= 9; i++) f.Emit(.3 + i * .02, .1f, true, 1, .1f, 0, true, true);
                Equal(1, f.Activations); f.Hold(.6); Equal(2, f.Activations);
            }, s => s.cooldownSeconds = 0); passed++;
            Check("抑制期间不能累计新保持", SkillTriggerMode.GestureOnly, f =>
            {
                for (int i = 0; i <= 9; i++) f.Emit(i * .02, .9f, true, 1, .1f, 0, true, true);
                Equal(0, f.Activations); f.Emit(.2); Equal(0, f.Activations); f.Emit(.28); Equal(1, f.Activations);
            }); passed++;
            Check("暂停重置后退出原印可重新施法", SkillTriggerMode.GestureOnly, f =>
            {
                f.Hold(0); f.Engine.Reset(.2); f.Hold(.3); Equal(1, f.Activations);
                f.Exit(.5); f.Hold(.8); Equal(2, f.Activations);
            }, s => s.cooldownSeconds = 0); passed++;
            Check("冷却输入作废且结束不补发", SkillTriggerMode.GestureOnly, f =>
            {
                f.Hold(0); f.Exit(.2); f.Hold(.5); Equal(2, f.Attempts); Equal(1, f.Activations);
                f.Hold(4); Equal(1, f.Activations); f.Exit(4.2); f.Hold(4.5); Equal(2, f.Activations);
            }); passed++;
            Check("激活失败消费输入", SkillTriggerMode.GestureOnly, f =>
            {
                f.Succeed = false; f.Hold(0); Equal(1, f.Attempts); f.Succeed = true;
                f.Hold(1); Equal(1, f.Attempts); f.Exit(1.2); f.Hold(1.5); Equal(1, f.Activations);
            }); passed++;
            Check("骨骼深拷贝与历史评分不可变", SkillTriggerMode.VoiceAndGesture, f =>
            {
                var frame = f.Emit(0, .72f); frame.left.joints[0] = new Pose(new Vector3(999, 0, 0), Quaternion.identity);
                var field = typeof(SkillMatchEngine).GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic);
                var slots = (Array)field.GetValue(f.Engine); var first = slots.GetValue(0);
                var saved = (SealFrame)first.GetType().GetField("Frame").GetValue(first);
                True(saved.left.joints[0].position.x != 999, "缓冲不能引用调用方骨骼数组");
                f.Emit(.08, .72f); f.Target.threshold = .99f; f.Target.minimumCoverage = .99f;
                f.LastScores[0].score = 0; f.LastScores[0].coverage = 0; f.LastScores[0].details.Clear();
                f.Voice(.2, .2); Equal(1, f.Activations);
            }); passed++;
            Check("环形缓存容量与时间清理", SkillTriggerMode.VoiceAndGesture, f =>
            {
                for (int i = 0; i < 700; i++) f.Emit(i * .01, .1f);
                Equal(640, f.Engine.BufferCount); True(f.Engine.BufferSeconds <= 10, "缓存覆盖上限10秒");
                f.Engine.Tick(20); Equal(0, f.Engine.BufferCount);
            }); passed++;
            Check("无效模板规则不能形成证据", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Target.rules.Add(new FeatureRule { featureId = "律印", min = 1, max = 1, tolerance = 1, weight = 1 });
                f.Hold(0); f.Voice(.2, .2); Equal(0, f.Activations);
            }); passed++;
            Check("暖机后常规观察不分配托管堆", SkillTriggerMode.VoiceAndGesture, f =>
            {
                var frame = f.Frame(0); var scores = f.Scores(.1f);
                for (int i = 0; i < 650; i++)
                {
                    frame.time = i * .01; frame.left.observedAt = frame.right.observedAt = frame.time;
                    frame.left.version = frame.right.version = i; f.Engine.Tick(frame.time); f.Engine.Observe(frame, scores);
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 650; i < 750; i++)
                {
                    frame.time = i * .01; frame.left.observedAt = frame.right.observedAt = frame.time;
                    frame.left.version = frame.right.version = i; f.Engine.Tick(frame.time); f.Engine.Observe(frame, scores);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                True(allocated == 0, "常规Observe/Tick分配了 " + allocated + " 字节");
            }); passed++;
            passed += CheckMouthCondition();
            passed += CheckSequences();
            passed += CheckLeftHandSeals();
            CheckConfigurationAndReplacement(); passed++;
            CheckOneAttemptPerFrame(); passed++;
            return passed;
        }

        private static int CheckLeftHandSeals()
        {
            using var f = new Fixture(SkillTriggerMode.GestureOnly);
            f.Target.handMode = SealHandMode.LeftOnly;
            f.Target.rules[0].featureId = "L.Index.Curl";
            int casts = 0, notices = 0;
            SkillMatchEngine Matcher(params SkillDefinition[] recipes)
            {
                casts = notices = 0;
                var matcher = new SkillMatchEngine(recipes);
                matcher.TryActivate = _ => { casts++; return true; };
                matcher.SealConfirmed += (seal, _) => { if (seal == f.Target.sealName) notices++; };
                return matcher;
            }
            var engine = Matcher(f.Skill);
            int index = 0;
            double boundary = double.NegativeInfinity;
            void Sample(float score = .95f, Action<SealFrame> change = null, bool held = false, bool global = false,
                bool navigation = false, float other = .1f)
            {
                var frame = f.Frame(++index * .02);
                change?.Invoke(frame);
                engine.Tick(frame.time);
                engine.Observe(frame, f.Scores(score, competitor: other), global, held, boundary, navigation);
            }
            void NoRight(SealFrame frame) => frame.right = null;
            void StaleRight(SealFrame frame) => frame.right.observedAt = 0;
            void Disconnected(SealFrame frame) => frame.right.connected = false;
            void LostRight(SealFrame frame) => frame.right.tracked = false;
            foreach (Action<SealFrame> unavailable in new Action<SealFrame>[] { NoRight, StaleRight, Disconnected, LostRight })
            {
                engine = Matcher(f.Skill);
                for (int i = 0; i < 18; i++) Sample(change: unavailable, held:true);
                True(casts == 1 && notices == 1, "右手缺失、陈旧、断开或失追，持物期间连续左印应仅激活和反馈一次");
            }
            for (int i = 0; i < 10; i++) Sample(.1f, NoRight, held:true);
            for (int i = 0; i < 6; i++) Sample(change: NoRight, held:true);
            True(casts == 2 && notices == 2, "有效松印并重新保持后，右手持物期间左印应再次可用");

            engine = Matcher(f.Skill);
            var frozen = f.Frame((index + 1) * .02).left;
            for (int i = 0; i < 16; i++) Sample(change: frame => frame.left = frozen);
            True(casts == 0 && notices == 0, "右手刷新不能为冻结的左手累积保持或反馈");
            for (int i = 0; i < 3; i++) Sample(change: StaleRight);
            True(casts == 0 && notices == 0, "左手恢复后要重新取得完整保持证据");
            for (int i = 0; i < 3; i++) Sample(change: StaleRight);
            True(casts == 1 && notices == 1, "左手独立恢复后应正常成印");

            engine = Matcher(f.Skill);
            for (int i = 0; i < 12; i++) Sample(change: NoRight, global:true);
            for (int i = 0; i < 3; i++) Sample(change: NoRight);
            for (int i = 0; i < 12; i++) Sample(change: NoRight, navigation:true);
            for (int i = 0; i < 3; i++) Sample(change: NoRight);
            True(casts == 0 && notices == 0, "全局占用和左手导航预指向均须清除部分保持，解除后不能立刻开盾");
            for (int i = 0; i < 3; i++) Sample(change: NoRight);
            True(casts == 1 && notices == 1, "解除导航后重新保持可以开盾");

            engine = Matcher(f.Skill);
            boundary = (index + 3) * .02;
            for (int i = 0; i < 6; i++) Sample(change: NoRight);
            True(casts == 0 && notices == 0, "所有权切换前的左手观测不能累计到新输入");
            for (int i = 0; i < 3; i++) Sample(change: NoRight);
            True(casts == 1 && notices == 1, "越过所有权边界的新左手观测不必等待右手");

            var dual = f.MakeSkill("dual", "双手", SkillTriggerMode.GestureOnly, "", f.Competitor.sealName);
            engine = Matcher(dual);
            for (int i = 0; i < 8; i++) Sample(.1f, StaleRight, other:.95f);
            Equal(0, casts);
            for (int i = 0; i < 8; i++) Sample(.1f, navigation:true, other:.95f);
            True(casts == 1, "旧双手印仍需双手，左手专属的导航门控不会拦截其正常输入");

            f.Skill.followingSeals = new[] { f.Competitor.sealName };
            engine = Matcher(f.Skill);
            for (int i = 0; i < 8; i++) Sample(change: NoRight);
            for (int i = 0; i < 8; i++) Sample(.1f, other:.95f);
            True(casts == 0, "缺右手时的左印不能作为混合序列前缀转交给原双手纯印树");
            for (int i = 0; i < 10; i++) Sample();
            for (int i = 0; i < 10; i++) Sample(.1f, other:.95f);
            Equal(1, casts);

            f.Skill.followingSeals = Array.Empty<string>();
            engine = Matcher(f.Skill);
            void LowConfidence(SealFrame frame) { frame.left.highConfidence = false; frame.right = null; }
            var current = f.Frame(index * .02);
            LowConfidence(current);
            var highScores = f.Scores(.95f);
            True(highScores[0].score >= f.Target.threshold, "置信度用例需要有效的高分手指特征");
            True(!engine.IsCurrentSealHeld(f.Skill, current, highScores, current.time), "默认模板应拒绝整手低置信的当前左印");
            for (int i = 0; i < 8; i++) Sample(change: LowConfidence);
            True(casts == 0 && notices == 0, "手指特征达标且整手低置信时，默认不能开盾或反馈");
            f.Target.allowLowConfidence = true;
            True(engine.IsCurrentSealHeld(f.Skill, current, highScores, current.time), "显式允许低置信的模板应能读取当前左印");
            for (int i = 0; i < 8; i++) Sample(change: LowConfidence);
            True(casts == 1 && notices == 1, "显式允许低置信后，整手低置信也可以独立开盾一次");

            f.Target.allowLowConfidence = false;
            engine = Matcher(f.Skill);
            SealFrame repeated = null;
            for (int i = 0; i < 3; i++) Sample(change: frame => repeated = frame);
            Sample(change: frame =>
            {
                frame.left = repeated.left; frame.right = repeated.right;
                frame.left.highConfidence = false;
            });
            for (int i = 0; i < 3; i++) Sample();
            True(casts == 0 && notices == 0, "版本和时间戳不变的置信度下降仍须打断此前的左印保持");
            for (int i = 0; i < 3; i++) Sample();
            True(casts == 1 && notices == 1, "置信度恢复后重新累计完整的左手保持才能开盾");

            engine = Matcher(f.Skill);
            f.Target.calibrated = false;
            for (int i = 0; i < 8; i++) Sample(change: NoRight);
            True(casts == 0 && notices == 0, "未标定左手模板不能提供反馈或激活");
            return 9;
        }

        private static int CheckMouthCondition()
        {
            void Configure(SkillDefinition skill) => skill.castCondition = SkillCastCondition.SealNearMouth;
            Check("口令与印配上后可以保持并靠近嘴部", SkillTriggerMode.VoiceAndGesture, f =>
            {
                bool near = false;
                f.Engine.IsCastConditionMet = _ => near;
                f.Hold(0); f.Voice(.10, .10); f.Emit(.12); Equal(0, f.Attempts);
                near = true; f.Emit(.14); f.Hold(.16); Equal(1, f.Activations); Equal(1, f.Attempts);
            }, Configure);
            Check("迟到口令和旧印不能点燃已松开的手", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Engine.IsCastConditionMet = _ => true;
                f.Hold(0); f.Emit(.12, .1f); f.Voice(.10, .14);
                f.Hold(.16); Equal(0, f.Attempts);
            }, Configure);
            Check("语音先于本帧松手样本到达", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Engine.IsCastConditionMet = _ => true;
                f.Hold(0); f.Voice(.10, .12); Equal(0, f.Attempts);
                f.Emit(.12, .1f); f.Hold(.14); Equal(0, f.Attempts);
            }, Configure);
            void Interrupted(string name, Action<Fixture> interrupt)
            {
                Check(name, SkillTriggerMode.VoiceAndGesture, f =>
                {
                    f.Engine.IsCastConditionMet = _ => false;
                    f.Voice(0, 0); f.Hold(.02);
                    True(f.Engine.PendingSkillId == f.Skill.skillId, "保持等待时应显示当前技能");
                    interrupt(f);
                    True(string.IsNullOrEmpty(f.Engine.PendingSkillId), "等待须作废");
                    f.Engine.IsCastConditionMet = _ => true;
                    f.Hold(.4); Equal(0, f.Attempts);
                }, s => { Configure(s); s.matchWindow = .36f; });
            }
            Interrupted("抬手等待被输入占用打断", f => f.Emit(.14, suppress: true));
            Interrupted("取消清除嘴部等待", f => f.Engine.Cancel(.14));
            Interrupted("追踪断流结束嘴部等待", f => f.Engine.Tick(.28));
            Interrupted("持续举印也不能延长嘴部窗口", f => { f.Hold(.14); f.Hold(.26); f.Emit(.38); });
            Check("纯印术也能配置一次嘴部起手条件", SkillTriggerMode.GestureOnly, f =>
            {
                bool near = false; f.Engine.IsCastConditionMet = _ => near;
                f.Hold(0); near = true; f.Hold(.12); Equal(1, f.Activations);
            }, Configure);
            Check("实时检查复用组合减免并拒绝过期和歧义", SkillTriggerMode.VoiceAndGesture, f =>
            {
                var frame = f.Frame(.1); var scores = f.Scores(.72f);
                True(f.Engine.IsCurrentSealHeld(f.Skill, frame, scores, .11), "组合阈值");
                True(!f.Engine.IsCurrentSealHeld(f.Skill, frame, scores, 1), "骨骼过期");
                True(!f.Engine.IsCurrentSealHeld(f.Skill, frame, f.Scores(.72f, competitor: .71f), .11), "印歧义");
            }, Configure);
            return 9;
        }

        private static int CheckSequences()
        {
            void Pair(SkillDefinition skill) => skill.followingSeals = new[] { "规印" };
            void Second(Fixture f, double t) => f.Hold(t, .1f, 1, .95f);
            Check("连续三印与快速切回第一印", SkillTriggerMode.GestureOnly, f =>
            {
                f.Hold(0); Equal(0, f.Activations); Second(f, .12); Equal(0, f.Activations);
                f.Hold(.24); Equal(1, f.Activations); f.Hold(.36); Equal(1, f.Activations);
            }, s => s.followingSeals = new[] { "规印", "律印" });
            Check("同印须主动退出", SkillTriggerMode.GestureOnly, f =>
            {
                f.Hold(0); f.Hold(.12); f.Hold(.24); Equal(0, f.Activations);
                f.Exit(.36); f.Hold(.56); Equal(1, f.Activations);
            }, s => s.followingSeals = new[] { s.sealName });
            Check("先咒后序列", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Voice(0, 0); f.Hold(.02); Equal(0, f.Activations); Second(f, .14); Equal(1, f.Activations); }, Pair);
            Check("迟到口令匹配且消费整串", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Hold(0); Second(f, .12); f.Voice(.18, .9); Equal(1, f.Attempts);
                f.Voice(.91, 1); Equal(1, f.Attempts);
            }, Pair);
            Check("全序列共享窗口", SkillTriggerMode.VoiceAndGesture, f =>
            { f.Hold(0); Second(f, .12); f.Voice(8.01, 8.1); Equal(0, f.Attempts); }, Pair);
            Check("顺序错误与已确认的无关印", SkillTriggerMode.GestureOnly, f =>
            {
                Second(f, 0); f.Hold(.12); Equal(0, f.Attempts);
                for (int i = 0; i <= 5; i++)
                {
                    double t = .24 + .02 * i;
                    var scores = f.Scores(.1f);
                    scores[2].score = .96f;
                    f.Engine.Tick(t); f.Engine.Observe(f.Frame(t), scores);
                }
                Second(f, .36); Equal(0, f.Attempts);
                f.Hold(.48); Second(f, .60); Equal(1, f.Attempts);
            }, Pair);
            Check("断帧使部分序列失效", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); Second(f, 1); Equal(0, f.Attempts); }, Pair);
            Check("占用打断序列", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); f.Emit(.11, suppress: true); Second(f, .12); Equal(0, f.Attempts); }, Pair);
            Check("取消打断序列", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); f.Engine.Cancel(.11); Second(f, .12); Equal(0, f.Attempts); }, Pair);
            Check("短暂过渡允许换印", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); f.Emit(.12, .1f); Second(f, .14); Equal(1, f.Attempts); }, Pair);
            Check("无模板占位无法成印", SkillTriggerMode.GestureOnly, f =>
            { f.Hold(0); f.Competitor.calibrated = false; Second(f, .12); Equal(0, f.Attempts); }, Pair);
            Check("占用同一帧清除已完成序列", SkillTriggerMode.VoiceAndGesture, f =>
            {
                f.Hold(0);
                Second(f, .12);
                var frame = f.Emit(.24, .1f, competitor: .95f);
                f.Engine.Observe(frame, f.LastScores, suppressActivation: true);
                f.Voice(.24, .26); Equal(0, f.Attempts);
            }, Pair);
            Check("成印事件按动作计数且覆盖配方外的印", SkillTriggerMode.GestureOnly, f =>
            {
                var confirmed = new List<string>();
                f.Engine.SealConfirmed += (seal,time) => confirmed.Add(seal);
                f.Hold(0); f.Hold(.12);
                Second(f,.24); Second(f,.36);
                f.Exit(.48); Second(f,.70);
                f.Hold(.84);
                True(string.Join(",", confirmed) == "律印,规印,规印,律印", "每次成印应恰好发一个事件");
            });
            Check("组合放宽阈值仍只报一次印", SkillTriggerMode.VoiceAndGesture, f =>
            {
                int count = 0;
                f.Engine.SealConfirmed += (seal,time) => count++;
                f.Hold(0, .70f); f.Hold(.12); f.Voice(.24,.25); f.Hold(.26);
                Equal(1,count); Equal(1,f.Attempts);
            }, s => { s.voiceDiscount = .10f; s.thresholdFloor = .6f; });
            CheckSharedSequenceInput();
            CheckSharedPrefix();
            CheckPurePrefix();
            CheckSealEventCenter();
            return 18;
        }

        private static void CheckSealEventCenter()
        {
            using var f = new Fixture(SkillTriggerMode.VoiceAndGesture,
                skill => skill.followingSeals = new[] { "规印" });
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>();
            var root = new GameObject("成印事件回归") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var input = root.AddComponent<SkillInputController>();
                input.lab = root.AddComponent<NarutoVR.SealLab.SealLab>();
                input.effects = root.AddComponent<SkillController>();
                catalog.skills.Add(f.Skill);
                input.catalog = catalog;
                double now = 0;
                input.InputClock = () => now;
                input.InitializeMatcher();
                var notices = new List<SealConfirmedNotice>();
                using var subscription = EventCenter.Subscribe(PlayerEvents.SealConfirmed, notices.Add);
                int legacy = 0, inspector = 0;
                input.SealConfirmed += (seal, at) => legacy++;
                input.onSealConfirmed.AddListener(seal => inspector++);
                for (int i = 0; i < 24; i++)
                {
                    double t = .02 * i;
                    input.Engine.Tick(t);
                    input.Engine.Observe(f.Frame(t), i < 12 ? f.Scores(.95f) : f.Scores(.1f, competitor: .95f));
                }
                True(notices.Count == 2 && legacy == 2 && inspector == 2 &&
                    notices[0].Source == input && notices[1].Source == input &&
                    notices[0].SealName == "律印" && notices[1].SealName == "规印" &&
                    notices[0].CapturedAt < notices[1].CapturedAt,
                    "真实输入组件应在无口令时为每个稳定印各发布一次中心、实例和 Inspector 通知");
                bool granted = false;
                input.HasSkillPermission = _ => granted;
                now = .5;
                input.RefreshAvailableSkills();
                Equal(0, input.AvailableSkills.Count);
                input.Engine.Voice(f.Skill.phrase, .51, .51);
                True(input.Engine.LastVoiceRejection != null, "收回试用后旧技能口令不能进入待选");
                for (int i = 0; i < 6; i++)
                {
                    double t = .52 + i * .02;
                    input.Engine.Tick(t);
                    input.Engine.Observe(f.Frame(t), f.Scores(.95f));
                }
                Equal(3, notices.Count);
                now = .64;
                granted = true;
                input.RefreshAvailableSkills();
                int casts = 0;
                input.Engine.TryActivate = _ => { casts++; return true; };
                input.Engine.Voice(f.Skill.phrase, .63, .65);
                True(input.Engine.LastVoiceRejection != null, "补授技能不能接受授权前的口令");
                input.Engine.Voice(f.Skill.phrase, .66, .66);
                for (int i = 0; i < 6; i++)
                {
                    double t = .68 + i * .02;
                    input.Engine.Tick(t);
                    input.Engine.Observe(f.Frame(t), f.Scores(.1f, competitor: .95f));
                }
                Equal(0, casts);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(catalog); }
        }

        private static void CheckSharedSequenceInput()
        {
            using var f = new Fixture(SkillTriggerMode.VoiceAndGesture);
            var pair = f.MakeSkill("pair", "双印", SkillTriggerMode.VoiceAndGesture, "双印术", "规印");
            pair.followingSeals = new[] { "律印" };
            var engine = new SkillMatchEngine(new[] { f.Skill, pair });
            int attempts = 0;
            engine.TryActivate = _ => { attempts++; return true; };
            engine.Tick(0);
            for (int i = 0; i <= 30; i++)
            {
                double t = i * .02;
                engine.Tick(t);
                engine.Observe(f.Frame(t), i < 6 ? f.Scores(.1f, 1, .9f) : f.Scores(.9f));
            }
            // The single-seal rolling interval now starts AFTER the pair finished.
            // Both still describe the same held final pose and must be consumed together.
            engine.Voice(f.Skill.phrase, .60, .60);
            engine.Voice(pair.phrase, .62, .62);
            Equal(1, attempts);

            var pure = f.MakeSkill("auto", "单印", SkillTriggerMode.GestureOnly, "", "规印");
            var conflict = new SkillMatchEngine(new[] { pure, pair });
            Equal(0, conflict.ActiveRecipes.Count);
            True(conflict.Status.Contains("冲突"), "纯印与相同起手的长配方会冲突");
        }

        private static void CheckSharedPrefix()
        {
            using var f = new Fixture(SkillTriggerMode.VoiceAndGesture,
                skill => skill.followingSeals = new[] { "规印" });
            var longer = f.MakeSkill("long", "三印", SkillTriggerMode.VoiceAndGesture, "三印术", "律印");
            longer.followingSeals = new[] { "规印", "律印" };
            foreach (var chosen in new[] { f.Skill, longer })
            {
                var engine = new SkillMatchEngine(new[] { longer, f.Skill });
                var casts = new List<SkillDefinition>();
                engine.TryActivate = skill => { casts.Add(skill); return true; };
                for (int i = 0; i < 18; i++)
                {
                    double t = i * .02;
                    engine.Tick(t);
                    engine.Observe(f.Frame(t), i < 6 || i >= 12 ? f.Scores(.95f) : f.Scores(.1f, competitor: .95f));
                    if (i == 11)
                    {
                        True(casts.Count == 0 && engine.NextSeal(longer.skillId) == "律印", "短术印齐时长术仍可续印");
                        if (chosen == f.Skill) engine.Voice(chosen.phrase, t, t);
                    }
                }
                engine.Voice(longer.phrase, .36, .36);
                True(casts.Count == 1 && casts[0] == chosen, "共前缀的两条印序只能按完整口令释放一次");
            }
        }

        private static void CheckPurePrefix()
        {
            using var f = new Fixture(SkillTriggerMode.GestureOnly, s => s.followingSeals = new[] { "规印" });
            var longer = f.MakeSkill("long", "长术", SkillTriggerMode.GestureOnly, "", "律印");
            longer.followingSeals = new[] { "规印", "律印" };
            var settings = new SkillMatchSettings { shortSealHoldSeconds = .4f };
            var hints = new List<SkillSealHint>();
            var casts = new List<SkillDefinition>();
            SkillMatchEngine engine = null;
            void Start(params SkillDefinition[] skills)
            {
                casts.Clear();
                engine = new SkillMatchEngine(skills, settings);
                engine.TryActivate = skill => { casts.Add(skill); return true; };
                engine.Tick(0);
            }
            void Pose(int first, int end, bool second, bool tracked = true, bool occupied = false)
            {
                for (int i=first; i<=end; i++)
                {
                    double t=i*.02;
                    engine.Tick(t);
                    engine.Observe(f.Frame(t, tracked), second ? f.Scores(.1f, competitor:.95f) : f.Scores(.95f),
                        suppressHeldRearm:occupied);
                }
            }
            void ReachBranch()
            {
                Start(longer, f.Skill);
                Pose(0, 5, false); Pose(6, 10, true);
                engine.GetSealHints(hints);
                True(casts.Count == 0 && hints.Count == 2 && hints[0].Skill == f.Skill && hints[0].Holding &&
                    hints[1].Skill == longer && hints[1].Matched == 2 && hints[1].NextSeal == "律印",
                    "短术待确认，长术同步显示下一印");
            }
            ReachBranch(); Pose(11,29,true); Equal(0, casts.Count);
            Pose(30,55,true); True(casts.Count==1 && casts[0]==f.Skill, "稳定额外保持到边界只释放一次短术");
            ReachBranch(); Pose(11,29,true); Pose(30,40,false);
            True(casts.Count==1 && casts[0]==longer,"截止帧换印只释放长术");
            ReachBranch(); engine.Tick(1.1); Pose(56,80,true); Equal(0,casts.Count);
            float Progress()
            {
                engine.GetSealHints(hints);
                True(hints.Count == 2 && hints[0].Holding && hints[1].Matched == 2, "宽限期间保留短术进度和长术前缀");
                return hints[0].HoldProgress;
            }
            ReachBranch(); Pose(11,28,true);
            float frozen = Progress();
            Pose(29,33,true,false);
            True(Progress() == frozen && casts.Count == 0, "丢追踪不走进度，也不在遮挡中释放");
            Pose(34,34,true); True(Progress() == frozen && casts.Count == 0, "恢复首帧不补算遮挡");
            Pose(35,35,true); Equal(0,casts.Count);
            Pose(36,48,true); True(casts.Count == 1 && casts[0] == f.Skill, "重新稳住只补足剩余时长");

            ReachBranch(); Pose(11,28,true); Pose(29,32,true,false); Pose(33,40,false);
            True(casts.Count == 1 && casts[0] == longer, "遮挡后直接结出下一印只发长术");
            ReachBranch(); Pose(11,28,true);
            frozen = Progress();
            engine.Tick(.710); True(Progress() == frozen, "整段无回调 150 毫秒仍冻结两个候选");
            engine.Observe(f.Frame(.710), f.Scores(.1f, competitor:.95f));
            True(Progress() == frozen && casts.Count == 0, "150 毫秒边界的同印恢复不补时");
            ReachBranch(); Pose(11,28,true); engine.Tick(.711);
            engine.GetSealHints(hints); Equal(0,hints.Count);
            Pose(36,48,false); Equal(0,casts.Count);
            ReachBranch(); Pose(11,21,true,false); Pose(22,36,false); Equal(0,casts.Count);
            ReachBranch(); Pose(11,28,true); frozen = Progress();
            for (int i = 29; i <= 33; i++)
            {
                double t = i * .02;
                var oneHand = f.Frame(t);
                oneHand.left.observedAt = .56;
                engine.Tick(t); engine.Observe(oneHand, f.Scores(.1f, competitor:.95f));
            }
            True(Progress() == frozen && casts.Count == 0, "只有一只手更新时，重复的旧手样本无法蓄势");
            Pose(34,34,true); True(Progress() == frozen, "双手重新同步后从冻结值继续");

            ReachBranch(); Pose(11,24,true);
            frozen = Progress();
            for (int i = 25; i <= 28; i++)
            {
                engine.Tick(i*.02); engine.Observe(f.Frame(i*.02), f.Scores(.87f, competitor:.88f));
            }
            True(Progress() == frozen, "两个印近分时只冻结");
            Pose(29,29,true); True(Progress() == frozen, "分数恢复首帧不补发");
            Pose(30,40,true); True(casts.Count == 1 && casts[0] == f.Skill, "短时分数抖动后继续出短术");
            ReachBranch(); Pose(11,24,true);
            for (int i = 25; i <= 28; i++)
            {
                engine.Tick(i*.02); engine.Observe(f.Frame(i*.02), f.Scores(.1f));
            }
            engine.GetSealHints(hints);
            True(hints.Count == 1 && hints[0].Skill == longer && hints[0].Matched == 2, "明确离开旧印后只保留可续的长术");
            Pose(29,33,false,false); Pose(34,42,false);
            True(casts.Count == 1 && casts[0] == longer, "换印过渡中短暂遮挡也能接上长术");
            var voicePair = f.MakeSkill("separate.voice", "旁路语音术", SkillTriggerMode.VoiceAndGesture, "旁路术", "规印");
            voicePair.followingSeals = new[] { "矩印" };
            Start(f.Skill, longer, voicePair);
            Pose(0,5,false); Pose(6,10,true); Pose(11,14,true,false);
            for (int i = 15; i <= 22; i++)
            {
                var third = f.Scores(.1f);
                third[2].score = .98f;
                engine.Tick(i*.02); engine.Observe(f.Frame(i*.02), third);
            }
            engine.Voice(voicePair.phrase,.44,.44);
            True(casts.Count == 0 && engine.PendingSkillId == voicePair.skillId, "纯术宽限不保留被遮挡打断的语音配方");
            ReachBranch(); Pose(11,12,true,occupied:true); Pose(13,40,true); Equal(0,casts.Count);
            ReachBranch(); engine.Cancel(.21); Pose(11,40,true); Equal(0,casts.Count);
            ReachBranch();
            var previous = engine;
            var newSingle = f.MakeSkill("grant", "新解锁", SkillTriggerMode.GestureOnly, "", "规印");
            Start(newSingle);
            engine.BeginAfterPermissionChange(previous, .205);
            Pose(11,40,true); Equal(0,casts.Count);
            Pose(41,52,false); Pose(53,62,true);
            True(casts.Count == 1 && casts[0] == newSingle, "授权变更时保持的末印须松开重结");
            ReachBranch();
            for (int i=11;i<=40;i++)
            {
                var score=f.Scores(.1f);
                score[2].score=.96f;
                engine.Tick(i*.02); engine.Observe(f.Frame(i*.02),score);
            }
            engine.GetSealHints(hints); Equal(0,hints.Count); Equal(0,casts.Count);
            Start(f.Skill); Pose(0,5,false); Pose(6,10,true);
            True(casts.Count==1 && casts[0]==f.Skill,"没有可用长术时保持原有瞬发");

            f.Skill.followingSeals = Array.Empty<string>();
            longer.followingSeals = new[] { "律印", "规印" };
            // Re-enter the same first seal after leaving it without confirming another seal.
            Start(f.Skill,longer); Pose(0,5,false);
            for (int i=6;i<=15;i++)
            {
                engine.Tick(i*.02); engine.Observe(f.Frame(i*.02),f.Scores(.1f));
            }
            Pose(16,42,false); Equal(0,casts.Count);
            Pose(43,49,true); True(casts.Count==1 && casts[0]==longer,"重复起手不会在长术中间重开短术");
            Start(f.Skill,longer); Pose(0,30,false);
            True(casts.Count==1 && casts[0]==f.Skill,"单印短术支持保持确认");

            var suffix=f.MakeSkill("suffix","尾印",SkillTriggerMode.GestureOnly,"","规印");
            Start(suffix,longer); Equal(0,engine.ActiveRecipes.Count);
            Start(f.Skill,f.MakeSkill("same","同串",SkillTriggerMode.GestureOnly,"","律印"));
            Equal(0,engine.ActiveRecipes.Count);

            var alternatives = new SkillDefinition[4];
            for (int i = 0; i < alternatives.Length; i++)
            {
                alternatives[i] = f.MakeSkill("choice" + i, "候选" + i, SkillTriggerMode.VoiceAndGesture, "咒" + i, "律印");
                alternatives[i].followingSeals = new[] { "规印" };
            }
            Start(alternatives); Pose(0,5,false);
            engine.GetSealHints(hints);
            True(hints.Count == 3 && hints[0].Skill == alternatives[0] && hints[1].Skill == alternatives[1] &&
                hints[2].Skill == alternatives[2], "同进度候选依目录顺序只显示前三项");
        }

        private static void CheckConfigurationAndReplacement()
        {
            using var f = new Fixture(SkillTriggerMode.VoiceAndGesture);
            var pure = f.MakeSkill("pure", "规印加持", SkillTriggerMode.GestureOnly, "", "律印");
            var conflict = new SkillMatchEngine(new[] { f.Skill, pure });
            conflict.Tick(0); int count = 0; conflict.TryActivate = _ => { count++; return true; };
            conflict.Voice("火球术", 0, 0); True(conflict.Status.Contains("冲突"), "同印纯手配方必须显式报错");
            conflict.Observe(f.Frame(0), f.Scores(.9f)); conflict.Observe(f.Frame(.08), f.Scores(.9f)); Equal(0, count);
            var extra = f.MakeSkill("extra", "护盾", SkillTriggerMode.VoiceAndGesture, "护盾术", "律印");
            var replacement = new SkillMatchEngine(new[] { f.Skill, extra }); replacement.Tick(0); replacement.TryActivate = _ => { count++; return true; };
            replacement.Voice("火球术", 0, 0); replacement.Tick(.1); replacement.Observe(f.Frame(.1), f.Scores(.9f));
            replacement.Voice("护盾术", .11, .11); Equal(0, replacement.BufferCount);
            replacement.Tick(.2); replacement.Observe(f.Frame(.2), f.Scores(.9f));
            replacement.Tick(.3); replacement.Observe(f.Frame(.3), f.Scores(.9f)); Equal(0, count);
            True(replacement.PendingSkillName == "护盾", "新完整口令应替换旧候选");
            var containing = f.MakeSkill("containing", "大火球", SkillTriggerMode.VoiceOnly, "大火球术", "");
            var phrases = new SkillMatchEngine(new[] { f.Skill, containing });
            True(phrases.Status.Contains("包含冲突"), "包含口令必须禁用双方");
        }

        private static void CheckOneAttemptPerFrame()
        {
            using var f = new Fixture(SkillTriggerMode.VoiceOnly);
            var second = f.MakeSkill("second", "护盾", SkillTriggerMode.VoiceOnly, "护盾术", "");
            var engine = new SkillMatchEngine(new[] { f.Skill, second }); int attempts = 0;
            engine.TryActivate = _ => { attempts++; return false; }; engine.Tick(1);
            engine.Voice("火球术", .8, 1); engine.Voice("护盾术", .9, 1); Equal(1, attempts);
            engine.Tick(2); engine.Voice("护盾术", .9, 2); Equal(1, attempts);
            engine.Tick(3); engine.Voice("护盾术", 3, 3); Equal(2, attempts);
        }

        private static void Check(string name, SkillTriggerMode mode, Action<Fixture> action, Action<SkillDefinition> configure = null, SkillMatchSettings settings = null)
        {
            using var fixture = new Fixture(mode, configure, settings);
            try { action(fixture); }
            catch (Exception ex) { throw new InvalidOperationException("核心回归失败：" + name + "。" + ex.Message, ex); }
        }
        private static void Equal(int expected, int actual) { if (expected != actual) throw new InvalidOperationException($"预期 {expected}，实际 {actual}"); }
        private static void True(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> objects = new();
            public readonly SkillDefinition Skill;
            public readonly SealProfile Target, Competitor, Other;
            public readonly SkillMatchEngine Engine;
            public int Version, Activations, Attempts;
            public bool Succeed = true;
            readonly Rpg.RpgPlayerState player = new();
            double inputNow;
            public List<SealScore> LastScores;
            public Fixture(SkillTriggerMode mode, Action<SkillDefinition> configure = null, SkillMatchSettings settings = null)
            {
                Target = Profile("律印"); Competitor = Profile("规印"); Other = Profile("矩印");
                Skill = MakeSkill("fire", "火球", mode, "火球术", "律印"); configure?.Invoke(Skill);
                Engine = new SkillMatchEngine(new[] { Skill }, settings);
                Engine.TryActivate = skill =>
                {
                    Attempts++;
                    if (!Succeed || !player.TryReserveCast("check:"+Attempts,skill.skillId,skill.cooldownGroup,0,
                        skill.cooldownSeconds,inputNow,out var reservation,out _)) return false;
                    using(reservation) reservation.Commit();
                    Activations++;
                    return true;
                };
                Engine.Tick(0);
            }
            public SkillDefinition MakeSkill(string id, string display, SkillTriggerMode mode, string phrase, string seal)
            {
                var item = ScriptableObject.CreateInstance<SkillDefinition>(); objects.Add(item);
                item.effectDefinition = ScriptableObject.CreateInstance<ShieldEffectDefinition>(); objects.Add(item.effectDefinition);
                item.skillId = id; item.displayName = display; item.triggerMode = mode; item.phrase = phrase; item.sealName = seal; return item;
            }
            private SealProfile Profile(string name)
            {
                var item = ScriptableObject.CreateInstance<SealProfile>(); objects.Add(item);
                item.sealName = name; item.calibrated = true; item.threshold = .75f; item.minimumCoverage = .8f; item.holdSeconds = .07f;
                item.rules.Add(new FeatureRule { featureId = name, min = 1, max = 1, tolerance = 1, weight = 1, required = true }); return item;
            }
            public List<SealScore> Scores(float score, float coverage = 1, float competitor = .1f, bool confidence = true)
            {
                var result = new List<SealScore>
                {
                    SealScorer.Evaluate(Target, new[] { new SealFeature(Target.rules[0].featureId, "", score, true, confidence) }),
                    SealScorer.Evaluate(Competitor, new[] { new SealFeature(Competitor.sealName, "", competitor, true, true) }),
                    SealScorer.Evaluate(Other, new[] { new SealFeature(Other.sealName, "", .1f, true, true) })
                };
                result[0].coverage = Math.Min(result[0].coverage, coverage); return result;
            }
            public SealFrame Frame(double time, bool tracked = true, double skew = 0)
            {
                int version = ++Version;
                return new SealFrame
                {
                    time = time,
                    left = new HandSnapshot { version = version, connected = tracked, tracked = tracked, fresh = tracked, observedAt = time, highConfidence = true },
                    right = new HandSnapshot { version = version, connected = tracked, tracked = tracked, fresh = tracked, observedAt = time - skew, highConfidence = true }
                };
            }
            public SealFrame Emit(double time, float score = .9f, bool tracked = true, float coverage = 1, float competitor = .1f, double skew = 0, bool confidence = true, bool suppress = false, bool occupied = false)
            {
                inputNow=time; Engine.Tick(time); var frame = Frame(time, tracked, skew); LastScores = Scores(score, coverage, competitor, confidence);
                Engine.Observe(frame, LastScores, suppress, occupied); return frame;
            }
            public void EmitMissing(double time)
            {
                Engine.Tick(time); var scores = Scores(.9f);
                scores[0] = SealScorer.Evaluate(Target, Array.Empty<SealFeature>()); scores[0].score = .9f; scores[0].coverage = 1;
                Engine.Observe(Frame(time), scores);
            }
            public void Hold(double start, float score = .9f, float coverage = 1, float competitor = .1f)
            { for (int i = 0; i <= 5; i++) Emit(start + i * .02, score, true, coverage, competitor); }
            public void Exit(double start) { for (int i = 0; i <= 9; i++) Emit(start + i * .02, .1f); }
            public void Voice(double endedAt, double arrivedAt) { inputNow=arrivedAt; Engine.Tick(arrivedAt); Engine.Voice(Skill.phrase, endedAt, arrivedAt); }
            public void Dispose() { foreach (var item in objects) if (item) UnityEngine.Object.DestroyImmediate(item); }
        }
    }
}
#endif
