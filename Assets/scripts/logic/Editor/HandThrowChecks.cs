#if UNITY_EDITOR
using System;
using System.Collections;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class HandThrowChecks
    {
        static ThrowGripSample Grip(double time, bool closed, bool pinch = false) => new ThrowGripSample
        {
            observed = time, fistValid = !pinch, fistClosed = !pinch && closed, fistOpen = !pinch && !closed,
            pinchValid = pinch, pinchClosed = pinch && closed, pinchOpen = pinch && !closed
        };
        static void Require(bool value, string text) { if (!value) throw new Exception("投掷回归：" + text); }
        public static int Run()
        {
            int count = 0;
            // The SDK can spend several frames in Neutral between Closed and Open.
            // The hand is already returning during those frames; this must not reverse a forward throw.
            var delayed = new HandThrowState(new HandThrowSettings());
            Vector3 delayedResult = Vector3.zero;
            for (int i = 0; i <= 23; i++)
            {
                double t = i * .01;
                bool closed = i < 14;
                var sample = Grip(t, closed); sample.fistOpen = i >= 20;
                float z = i <= 14 ? i * .025f : .35f - (i - 14) * .04f;
                var velocity = delayed.Process(Vector3.forward * z, Vector3.up * 1.7f, sample, t);
                if (velocity != Vector3.zero) delayedResult = velocity;
                if (i < 20) Require(velocity == Vector3.zero, "半张开的Neutral阶段不能直接发射");
            }
            Require(delayedResult != Vector3.zero && Vector3.Dot(delayedResult.normalized, Vector3.forward) > .98f,
                "Closed→Neutral先松手、80毫秒后才Open：收手不能反转发射方向"); count++;
            foreach (bool pinch in new[] { false, true }) foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.back })
            {
                var state = new HandThrowState(new HandThrowSettings()); Vector3 result = Vector3.zero;
                for (int i = 0; i <= 15; i++)
                {
                    double t = i * .02;
                    Vector3 hand = direction * Mathf.Max(0, (float)t - .12f) * 2;
                    result = state.Process(hand, Vector3.up * 1.7f, Grip(t, t < .24, pinch), t);
                    if (result != Vector3.zero) break;
                }
                Require(result != Vector3.zero && Vector3.Dot(result.normalized, direction) > .999f, "握持松手必须沿真实轨迹发射，含向上/向后，不能改成头向");
                Require(!state.Armed && state.Process(direction, Vector3.up, Grip(.32,false,pinch), .32) == Vector3.zero, "一次松手不能重复发射");
                count++;
            }
            var slow = new HandThrowState(new HandThrowSettings());
            for (int i = 0; i < 20; i++) Require(slow.Process(Vector3.zero, Vector3.up, Grip(i*.02,i<10),i*.02) == Vector3.zero, "静止松手不能发射"); count++;
            var ungripped = new HandThrowState(new HandThrowSettings());
            for (int i = 0; i < 20; i++) Require(ungripped.Process(Vector3.right*i*.04f,Vector3.up,Grip(i*.02,false),i*.02)==Vector3.zero,"未握过不能直接甩手发射"); count++;
            var lost = new HandThrowState(new HandThrowSettings());
            for (int i = 0; i < 8; i++) lost.Process(Vector3.zero,Vector3.up,Grip(i*.02,true),i*.02);
            Require(lost.Armed,"握持未确认");
            lost.Process(Vector3.right*.1f,Vector3.up,new ThrowGripSample{observed=.16},.16);
            for (int i = 9; i < 15; i++) Require(lost.Process(Vector3.right*i*.04f,Vector3.up,Grip(i*.02,false),i*.02)==Vector3.zero,"丢失特征后张掌不能视为释放"); count++;
            var drift = new HandThrowState(new HandThrowSettings());
            for (int i = 0; i < 20; i++) { var offset=Vector3.right*i*.04f; Require(drift.Process(offset, Vector3.up+offset,Grip(i*.02,i<13),i*.02)==Vector3.zero,"整体身体移动不能当投掷"); } count++;
            var stale = new HandThrowState(new HandThrowSettings());
            stale.Process(Vector3.zero,Vector3.up,Grip(1,true),1);
            for (int i=0;i<6;i++) stale.Process(Vector3.zero,Vector3.up,Grip(1,true),1+i*.02);
            Require(!stale.Armed,"重复观测不能累计握持确认");
            Require(stale.Process(Vector3.right*.2f,Vector3.up,Grip(1.02,false),1.4)==Vector3.zero,"过期样本不能发射"); count++;
            var jump = new HandThrowState(new HandThrowSettings());
            for(int i=0;i<8;i++) jump.Process(Vector3.zero,Vector3.up,Grip(i*.02,true),i*.02);
            Require(jump.Process(Vector3.right,Vector3.up,Grip(.16,false),.16)==Vector3.zero && !jump.Armed,"单帧跳变应撤销投掷"); count++;
            Require(!new HandThrowSettings { minimumSpeed=float.NaN }.Valid,"非有限投掷配置必须无效"); count++;
            foreach (int hz in new[] { 72, 90, 120 })
            {
                var sampled = new HandThrowState(new HandThrowSettings());
                Vector3 result = Vector3.zero;
                for (int i = 0; i < hz / 2; i++)
                {
                    double t = (double)i / hz;
                    var shape = Grip(t, t < .2); shape.fistOpen = t >= .26;
                    float z = t <= .2 ? (float)t * 2 : .4f - ((float)t - .2f) * 2;
                    Vector3 p = new Vector3(Mathf.Sin(i * 2.3f) * .004f, Mathf.Cos(i * 1.7f) * .004f, z);
                    Vector3 v = sampled.Process(p, Vector3.up * 1.7f, shape, t);
                    if (v != Vector3.zero) { result = v; break; }
                }
                Require(result != Vector3.zero && Vector3.Angle(result, Vector3.forward) < 5,
                    hz + "Hz带4毫米噪声和延迟张掌，方向误差应小于5度"); count++;
            }
            var neutral = new HandThrowState(new HandThrowSettings());
            for (int i=0;i<40;i++)
            {
                var shape = Grip(i*.01, i<14); shape.fistOpen=false;
                Require(neutral.Process(Vector3.forward*i*.02f,Vector3.up,shape,i*.01)==Vector3.zero,"Neutral不能发射");
            }
            Require(!neutral.Armed,"Neutral超时应要求重新握持"); count++;
            var regrip = new HandThrowState(new HandThrowSettings());
            for (int i=0;i<14;i++) regrip.Process(Vector3.forward*i*.02f,Vector3.up,Grip(i*.01,true),i*.01);
            var firstRelease=Grip(.14,false); firstRelease.fistOpen=false;
            regrip.Process(Vector3.forward*.28f,Vector3.up,firstRelease,.14);
            regrip.Process(Vector3.forward*.28f,Vector3.up,Grip(.15,true),.15);
            for (int i=16;i<23;i++) Require(regrip.Process(Vector3.forward*.28f,Vector3.up,Grip(i*.01,false),i*.01)==Vector3.zero,"收回松手后立即再张掌不得沿旧轨迹发射"); count++;
            var noisy = new HandThrowState(new HandThrowSettings()); bool noisyLaunched=false;
            for(int i=0;i<30;i++)
            {
                Vector3 p=Vector3.forward*(i*.02f)+Vector3.right*(i==13?.2f:0);
                if(noisy.Process(p,Vector3.up,Grip(i*.01,i<15),i*.01)!=Vector3.zero)noisyLaunched=true;
            }
            Require(!noisyLaunched,"小于单帧跳变阈值的离群点也应被拟合误差拒绝"); count++;
            var gap = new HandThrowState(new HandThrowSettings());
            for(int i=0;i<15;i++) gap.Process(Vector3.forward*i*.02f,Vector3.up,Grip(i*.01,true),i*.01);
            Require(gap.Process(Vector3.forward*.3f,Vector3.up,Grip(.4,false),.4)==Vector3.zero&&!gap.Armed,"长采样间隔不能跨段拟合"); count++;
            var undersampled = new HandThrowState(new HandThrowSettings());
            for(int i=0;i<12;i++) Require(undersampled.Process(Vector3.forward*i*.07f,Vector3.up,Grip(i*.04,i<6),i*.04)==Vector3.zero,
                "释放窗口不足4个真实样本时应拒绝，不能降低门槛或补造样本"); count++;
            return count;
        }

        /// <summary>Actual SkillEffects LateUpdate, grip callback, held bone placement and projectile launch across real frames.</summary>
        public static IEnumerator PlayProbe(Action<string> record)
        {
            var root = new GameObject("投掷验收_独立实体");
            var head = new GameObject("测试头部"); head.transform.SetParent(root.transform);
            head.transform.position = new Vector3(10000, 1.7f, 10000); head.transform.rotation = Quaternion.Euler(0,90,0);
            var effect = root.AddComponent<SkillEffects>(); effect.head = head.transform; effect.playerRoot = root.transform; effect.hitLayers = 0;
            var trace = new System.Collections.Generic.List<HandThrowObservation>();
            effect.ThrowObserved += observation => trace.Add(observation);
            var definition = ScriptableObject.CreateInstance<ProjectileEffectDefinition>(); definition.releaseMode = ProjectileReleaseMode.GripThrow;
            definition.throwLocalOffset = new Vector3(.03f,.04f,.06f);
            var skill = ScriptableObject.CreateInstance<SkillDefinition>(); skill.effectDefinition = definition; skill.durationSeconds = 10; skill.displayName="投掷测试";
            Vector3 origin=head.transform.position + Vector3.down*.3f, hand=origin;
            Quaternion rotation=Quaternion.Euler(40,15,70);
            bool closed=true;
            // Real LateUpdate and projectile entities; synthetic observation time stays 50Hz even
            // when an unfocused editor throttles render frames. Low-rate rejection is tested separately.
            double observedTime=100;
            effect.GameClock = () => observedTime;
            effect.HandInputClock = () => observedTime;
            effect.WristPoseOverride = _ => new Pose(hand,rotation);
            effect.ThrowGripProvider = (_,__) => Grip(observedTime,closed);
            try
            {
                Require(effect.TryActivate(skill),"实际效果执行器未生成投掷球");
                for(int i=0;i<24;i++) { observedTime+=.02; yield return null; }
                var held = root.scene.GetRootGameObjects();
                var field=typeof(SkillEffects).GetField("heldObject",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var ball=(GameObject)field.GetValue(effect);
                Require(ball && Vector3.Distance(ball.transform.position,hand+rotation*definition.throwLocalOffset)<.002f,"持球位置必须跟随腕骨局部偏移而非头朝向");
                record("实际持握球跟随腕骨局部坐标，头部转向不改变偏移基准");
                closed=false;
                for(int i=0;i<8;i++) { observedTime+=.02; yield return null; }
                Require(effect.HasHeldObject && effect.LaunchCount==0,"实际静止松手必须保留持球");
                closed=true;
                for(int i=0;i<8;i++) { observedTime+=.02; yield return null; }
                for(int i=1;i<=16 && effect.HasHeldObject;i++)
                {
                    float t=i*.02f; observedTime+=.02;
                    hand=origin + Vector3.up*(t*1.8f); closed=t<.13f;
                    yield return null;
                }
                Vector3 gaze=(effect.GetAimPoint()-effect.LastLaunchPosition).normalized;
                Require(effect.LaunchCount==1 && !effect.HasHeldObject && Vector3.Dot(effect.LastLaunchDirection,gaze)>.99999f &&
                    Vector3.Dot(effect.LastLaunchDirection,Vector3.right)>.98f,
                    "上甩松手只负责触发，火球应从手边飞向头显右方准星；" + effect.ThrowStatus);
                record("实际LateUpdate投掷：静止松手不发射，重新握持上甩松手只发一次，沿头显准星飞行");
                hand=origin; closed=true; bool open=false;
                int secondTraceStart=trace.Count;
                effect.ThrowGripProvider = (_,__) => { var sample=Grip(observedTime,closed); sample.fistOpen=open; return sample; };
                Require(effect.TryActivate(skill),"延迟张掌对照未生成第二颗火球");
                for(int i=0;i<24;i++) { observedTime+=.02; yield return null; }
                for(int i=1;i<=20 && effect.HasHeldObject;i++)
                {
                    float t=i*.02f; observedTime+=.02;
                    closed=t<.15f; open=t>=.23f;
                    hand=origin+Vector3.forward*(t<=.15f ? t*2 : .3f-(t-.15f)*2);
                    yield return null;
                }
                Require(effect.LaunchCount==2 && Vector3.Dot(effect.LastLaunchDirection,(effect.GetAimPoint()-effect.LastLaunchPosition).normalized)>.99999f,
                    "延迟Open确认时收手不能把弹道从准星改成回收方向");
                record("实际LateUpdate延迟张掌：先向前甩后收手，确认Open后仍沿头显准星发射");
                var secondRelease=trace.GetRange(secondTraceStart,trace.Count-secondTraceStart).Find(o=>o.pending&&o.estimate.accepted);
                Require(Vector3.Distance(effect.LastLaunchPosition,secondRelease.releaseOrigin)<.002f &&
                    Vector3.Distance(effect.LastLaunchPosition,hand+rotation*definition.throwLocalOffset)>.05f,
                    "确认前已经收手时必须从冻结释放点发射，不从回收后的手位跳出");
                record("投掷候选冻结出手点：收手不会拖走发射点");

                effect.ClearAll(); hand=origin;closed=true;open=false;effect.hitLayers=1<<30;
                GameObject obstacle=null;
                try
                {
                    Require(effect.TryActivate(skill),"动态障碍对照未生成火球");
                    for(int i=0;i<24;i++){observedTime+=.02;yield return null;}
                    for(int i=1;i<=20;i++)
                    {
                        float t=i*.02f;observedTime+=.02;closed=t<.15f;open=t>=.23f;
                        hand=origin+Vector3.forward*(t<=.15f?t*2:.3f-(t-.15f)*2);
                        yield return null;
                        if(effect.ReleasePending && effect.ThrowPreviewValid && !obstacle)
                        {
                            obstacle=new GameObject("投掷释放点_动态阻挡"){layer=30};
                            obstacle.transform.position=effect.ThrowPreviewOrigin;
                            obstacle.AddComponent<BoxCollider>().size=Vector3.one*.12f;
                            Physics.SyncTransforms();
                        }
                    }
                    Require(obstacle&&effect.LaunchCount==2&&effect.HasHeldObject,"确认前移入释放点的墙必须拒绝发射，不能改从当前手位穿墙");
                    record("释放确认前动态占位重新检查：阻挡不发射，原有飞行球数不增加");
                }
                finally { if(obstacle)Object.Destroy(obstacle); }
            }
            finally
            {
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllLines("Logs/throw-probe.jsonl", trace.ConvertAll(o => JsonUtility.ToJson(o)));
                effect.ClearAll(); Object.Destroy(root); Object.Destroy(definition); Object.Destroy(skill);
            }
        }
    }
}
#endif
