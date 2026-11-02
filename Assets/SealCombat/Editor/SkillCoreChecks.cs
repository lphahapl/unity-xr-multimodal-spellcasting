#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Deterministic checks with real ScriptableObject profiles. No microphone, scene, headset or test package needed.</summary>
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
                f.Hold(0); f.Exit(.2); f.Hold(.5); Equal(1, f.Attempts);
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
                f.Target.rules.Add(new FeatureRule { featureId = "寅", min = 1, max = 1, tolerance = 1, weight = 1 });
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
            CheckConfigurationAndReplacement(); passed++;
            CheckOneAttemptPerFrame(); passed++;
            return passed;
        }

        private static void CheckConfigurationAndReplacement()
        {
            using var f = new Fixture(SkillTriggerMode.VoiceAndGesture);
            var pure = f.MakeSkill("pure", "子印加持", SkillTriggerMode.GestureOnly, "", "寅");
            var conflict = new SkillMatchEngine(new[] { f.Skill, pure });
            conflict.Tick(0); int count = 0; conflict.TryActivate = _ => { count++; return true; };
            conflict.Voice("火球术", 0, 0); True(conflict.Status.Contains("冲突"), "同印纯手配方必须显式报错");
            conflict.Observe(f.Frame(0), f.Scores(.9f)); conflict.Observe(f.Frame(.08), f.Scores(.9f)); Equal(0, count);
            var extra = f.MakeSkill("extra", "护盾", SkillTriggerMode.VoiceAndGesture, "护盾术", "寅");
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
            public List<SealScore> LastScores;
            public Fixture(SkillTriggerMode mode, Action<SkillDefinition> configure = null, SkillMatchSettings settings = null)
            {
                Target = Profile("寅"); Competitor = Profile("子"); Other = Profile("丑");
                Skill = MakeSkill("fire", "火球", mode, "火球术", "寅"); configure?.Invoke(Skill);
                Engine = new SkillMatchEngine(new[] { Skill }, settings);
                Engine.TryActivate = _ => { Attempts++; if (Succeed) Activations++; return Succeed; }; Engine.Tick(0);
            }
            public SkillDefinition MakeSkill(string id, string display, SkillTriggerMode mode, string phrase, string seal)
            {
                var item = ScriptableObject.CreateInstance<SkillDefinition>(); objects.Add(item);
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
                    SealScorer.Evaluate(Target, new[] { new SealFeature(Target.sealName, "", score, true, confidence) }),
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
                Engine.Tick(time); var frame = Frame(time, tracked, skew); LastScores = Scores(score, coverage, competitor, confidence);
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
            public void Voice(double endedAt, double arrivedAt) { Engine.Tick(arrivedAt); Engine.Voice(Skill.phrase, endedAt, arrivedAt); }
            public void Dispose() { foreach (var item in objects) if (item) UnityEngine.Object.DestroyImmediate(item); }
        }
    }
}
#endif
