using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealLab;
using UnityEngine;
using UnityEngine.UI;
using Lab = NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat
{
    /// <summary>Owns input lifecycle and connects matching to effects. Presentation lives in SkillTestView.</summary>
    [DefaultExecutionOrder(400)]
    public sealed class SkillTestController : MonoBehaviour
    {
        [Header("一、共享配置与场景接线")]
        [InspectorName("技能目录")] public SkillCatalog catalog;
        [InspectorName("骨骼实验台")] public Lab lab;
        [InspectorName("效果执行器")] public SkillEffects effects;
        [InspectorName("中文语音接收器")] public SkillVoiceReceiver voice;
        [InspectorName("中文界面")] public SkillTestView view;
        // Serialized compatibility for existing scenes and editor tools; catalog/view now own these settings.
        [HideInInspector] public List<SkillDefinition> skills = new();
        [HideInInspector] public Text statusText;
        [HideInInspector] public Font chineseFont;
        [HideInInspector] public bool showDesktopControls = true;
        [Header("二、免手操作与取消")]
        [InspectorName("模拟口令倒计时（秒）"), Range(1,15)] public float simulatedDelay = 5;
        [InspectorName("启用张开双手取消")] public bool enableOpenHandCancel = true;
        [InspectorName("取消保持时间（秒）"), Range(.2f,2)] public float cancelHold = .5f;
        [InspectorName("取消双腕最小距离（米）"), Range(.2f,.8f)] public float cancelDistance = .35f;
        [Header("三、等待中的失败诊断")]
        [InspectorName("打印缺少印的特征原因")] public bool logFeatureFailures = true;
        [InspectorName("诊断间隔（秒）"), Min(.5f)] public float diagnosticInterval = 2;

        [Header("四、施法链条日志")]
        [InspectorName("自动记录施法链条")] public bool enableCastTrace = true;
        [InspectorName("状态采样间隔（秒）"), Min(.2f)] public float castTraceInterval = .5f;
        SkillCastTrace castTrace;
        double nextCastSnapshot, nextVoiceSnapshot, nextThrowSnapshot;
        long castAttempt;
        string activatingSkill;
        public SkillMatchEngine Engine { get; private set; }
        public string CastTracePath => castTrace?.Path;
        public void TraceCast(string stage, string detail, string skillId = "", double inputTime = 0, string payload = "")
        {
            if(!enableCastTrace || !Application.isPlaying) return;
            try {
                if(castTrace==null) {
                    string directory=Application.isEditor ? System.IO.Path.Combine(Application.dataPath,"../Logs/skill-casts") : System.IO.Path.Combine(Application.persistentDataPath,"Diagnostics/skill-casts");
                    castTrace=new SkillCastTrace(directory);
                    castTrace.Write(new SkillCastTrace.Entry { stage="session_start", time=Time.realtimeSinceStartupAsDouble,
                        frame=Time.frameCount, detail="场景="+gameObject.scene.path+"；追踪="+(lab?lab.TrackingSource:"无") });
                }
                castTrace.Write(new SkillCastTrace.Entry { stage=stage, detail=detail, skillId=skillId,
                    time=Time.realtimeSinceStartupAsDouble, inputTime=inputTime, frame=Time.frameCount,
                    epoch=Engine?.Epoch??0, attempt=string.IsNullOrEmpty(activatingSkill)?0:castAttempt,
                    gates=Engine?.ObservationDiagnostics, payload=payload });
            } catch(Exception) { /* Diagnostics must never interrupt input or effects. */ }
        }
        bool ActivateTraced(SkillDefinition skill)
        {
            ++castAttempt; activatingSkill=skill.skillId;
            TraceCast("activation_request",skill.displayName,skill.skillId);
            try {
                bool accepted=ActivationOverride!=null?ActivationOverride(skill):ActivationPolicy!=null?ActivationPolicy.TryActivate(skill):effects.TryActivate(skill);
                TraceCast(accepted?"activation_accepted":"activation_rejected", ActivationDetail(accepted),skill.skillId);
                return accepted;
            } catch(Exception e) { TraceCast("activation_exception",e.ToString(),skill.skillId); throw; }
            finally { activatingSkill=null; }
        }
        string ActivationDetail(bool accepted)
        {
            try { return ActivationOverride!=null?"旧式激活回调返回："+accepted:ActivationPolicy!=null?ActivationPolicy.ActivationStatus:effects.Status; }
            catch(Exception e) { return "激活结果="+accepted+"；诊断状态读取失败："+e.Message; }
        }
        void EffectReport(string text) { TraceCast("effect_message",text,activatingSkill??""); Report(text); }
        void TraceVoiceTransport(string reason,string phrase) { TraceCast("voice_transport",reason+"；"+phrase); }
        void TraceThrow(HandThrowObservation observation)
        {
            if(observation.eventType=="spawn" && effects)
                TraceCast("throw_configuration","shotId="+observation.shotId+"；生成时冻结的三路投掷配置",activatingSkill??"",payload:JsonUtility.ToJson(effects.ActiveThrowSettings));
            double now=Time.realtimeSinceStartupAsDouble;
            bool frequent=observation.eventType=="frame"||observation.eventType=="warming_up"||observation.eventType=="suppressed"||observation.eventType=="tracking_lost"||observation.eventType=="sample_skew";
            if(frequent && now<nextThrowSnapshot) return;
            nextThrowSnapshot=now+.25;
            TraceCast("throw_"+observation.eventType,observation.status,activatingSkill??"",observation.observed,JsonUtility.ToJson(observation));
        }
        public IReadOnlyList<SkillDefinition> ConfiguredSkills => catalog ? catalog.skills : skills;
        public IReadOnlyList<SkillRecipeInfo> Recipes => Engine?.ActiveRecipes ?? Array.Empty<SkillRecipeInfo>();
        public string LastMessage { get; private set; } = "等待初始化";
        public IEnumerable<string> RecentMessages => messages;
        public int VoiceEventCount { get; private set; }
        // Optional session policy: installed before Start by the RPG adapter. Old scenes need no policy.
        public Func<SkillDefinition, bool> ActivationOverride { get; set; }
        /// <summary>Preferred extension seam; legacy ActivationOverride retains priority when assigned.</summary>
        public ISkillActivationPolicy ActivationPolicy { get; set; }
        public Func<bool> GestureInputBlocked { get; set; }
        public string LastVoiceBlock { get; private set; } = "无";
        public Func<string> VoiceOwnershipDetail { get; set; }
        public Func<bool> VoiceInputBlocked { get; set; }
        // An exclusive local UI session consumes even cancel phrases. Separate from ordinary navigation gates.
        public Func<bool> ExclusiveInputBlocked { get; set; }
        double voiceNotBefore = double.NegativeInfinity;
        public void InvalidateEarlierVoice() { voiceNotBefore = InputNow; RejectDeferredVoice("腕部界面切换使已排队口令失效"); }
        public Action CancelOverride { get; set; }
        public bool DeferVoiceUntilLateUpdate { get; set; }
        /// <summary>Monotonic realtime input clock; tests may supply a deterministic clock. Never use paused game time.</summary>
        public Func<double> InputClock { get; set; }
        private double InputNow => InputClock?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
        private readonly Queue<(string phrase, double endedAt)> deferredVoice = new();
        public string ScheduledPhrase => scheduledPhrase;
        public double ScheduledAt => scheduledAt;
        public string CancelPhrase => Engine?.CancelPhrase ?? SkillPhrase.Normalize(catalog ? catalog.cancelPhrase : SkillPhrase.DefaultCancel);
        private SealFrame previous;
        private readonly Queue<string> messages = new();
        private string scheduledPhrase;
        private string[] voicePhrases=Array.Empty<string>();
        private double scheduledAt, nextDiagnostic, openSince=-1, lastCancelFrame=-1;
        private bool cancelLatched, applicationPaused, inputSuspended;

        public List<string> ConfigurationErrors()
        {
            var issues=catalog?catalog.Validate():new List<string>();
            if(!lab||!effects)issues.Add("请配置骨骼实验台和效果执行器");
            if(ConfiguredSkills==null||ConfiguredSkills.Count==0)issues.Add("技能列表为空");
            if(view)issues.AddRange(view.ConfigurationErrors(ConfiguredSkills));
            if(catalog&&catalog.matching!=null&&lab && catalog.matching.capacity<Math.Ceiling(catalog.matching.historySeconds*lab.maximumSampleRate)+2)
                issues.Add("缓存容量不足以覆盖当前采样频率和历史时长，请增加目录中的容量");
            return issues;
        }
        private void Start()
        {
            TraceCast("controller_start","初始化技能目录与接线");
            var errors=ConfigurationErrors();
            if(errors.Count>0){Report("配置未通过："+string.Join("；",errors));enabled=false;return;}
            if(!chineseFont)chineseFont=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            lab.recordOnStart=false;lab.SetTestMode(true);
            Engine=new SkillMatchEngine(ConfiguredSkills,catalog?catalog.matching:null,CancelPhrase);
            Engine.Log=Report;Engine.TryActivate=ActivateTraced;
            Engine.ExternalCooldownAuthority=ActivationOverride!=null||(ActivationPolicy?.OwnsCooldowns??false);Engine.Tick(InputNow);
            voicePhrases=Recipes.Where(r=>r.Mode!=SkillTriggerMode.GestureOnly).Select(r=>r.Phrase).Append(Engine.CancelPhrase).Distinct().ToArray();
            if(!view)view=GetComponent<SkillTestView>();
            if(!view)
            {
                view=gameObject.AddComponent<SkillTestView>();view.statusText=statusText;view.chineseFont=chineseFont;
                view.showDesktopControls=showDesktopControls;view.AssignDefaultShortcuts(ConfiguredSkills);
            }
            view.controller=this;Subscribe();
            Report($"技能训练已启动：{Recipes.Count} 个有效配方。取消口令：{Engine.CancelPhrase}。");
        }
        private void LateUpdate()
        {
            if(Engine==null)return;
            if(applicationPaused||Time.timeScale<=0)
            {
                if(!inputSuspended)ResetInputLifecycle();inputSuspended=true;return;
            }
            if(inputSuspended){ResetInputLifecycle();inputSuspended=false;}
            while(deferredVoice.Count>0)
            {
                var input=deferredVoice.Dequeue(); ProcessVoice(input.phrase,input.endedAt);
            }
            // Voice processing and cancellation callbacks can advance the input clock.
            // Sample Tick time afterwards; an earlier cached value looks like a real rollback.
            double now=InputNow;
            Engine.Tick(now);
            if(lab.Frame!=previous&&lab.Frame!=null)
            {
                previous=lab.Frame;
                if(lastCancelFrame<0||now-lastCancelFrame>lab.staleAfterSeconds)openSince=-1;
                lastCancelFrame=now;
                bool open=enableOpenHandCancel&&!(ExclusiveInputBlocked?.Invoke()??false)&&OpenCancelPose();
                if(open)
                {
                    if(openSince<0)openSince=now;
                    if(!cancelLatched&&now-openSince>=cancelHold){Cancel();cancelLatched=true;}
                }
                else {openSince=-1;cancelLatched=false;}
                Engine.Observe(lab.Frame,lab.Scores,cancelLatched||(GestureInputBlocked?.Invoke()??false), effects && effects.HasHeldObject);
            }
            if(now>=nextCastSnapshot) {
                nextCastSnapshot=now+Math.Max(.2,castTraceInterval);
                TraceCast("match_snapshot",$"状态={Engine.Status}；截止={Engine.PendingDeadline:F3}；帧间隔={Time.unscaledDeltaTime:F4}；追踪={(lab?lab.TrackingSource:"无")}",Engine.PendingSkillId,lab.Frame?.time??0);
            }
            if(voice && now>=nextVoiceSnapshot) { nextVoiceSnapshot=now+2; TraceCast("voice_health",voice.BuildDebugDetails()); }
            if(scheduledPhrase!=null&&now>=scheduledAt)
            {
                string phrase=scheduledPhrase;scheduledPhrase=null;
                Report("【模拟口令】"+phrase);ReceiveVoice(phrase,now);
            }
            if(logFeatureFailures&&now>=nextDiagnostic&&!string.IsNullOrEmpty(Engine.PendingSkillId))
            {
                nextDiagnostic=now+Math.Max(.5,diagnosticInterval);
                var pending=Recipes.First(r=>r.Id==Engine.PendingSkillId);
                var score=lab.Scores.FirstOrDefault(s=>s.profile&&s.profile.sealName==pending.Seal);
                if(score!=null)Debug.Log($"[技能等待] 本次有效阈值 {Math.Max(pending.Floor,score.profile.threshold-pending.Discount)*100:F1} 分；实际触发门槛：\n"+Engine.ObservationDiagnostics+"\n以下为基础模板明细：\n"+SealTestDiagnostics.Explain(score),this);
                else Report($"等待 {pending.Seal} 印，但骨骼实验台中未找到该模板的评分，请检查印模板接线。");
            }
        }
        private bool OpenCancelPose()
        {
            var f=lab.Frame;
            if(!f.left.connected||!f.right.connected||!f.left.tracked||!f.left.fresh||!f.right.tracked||!f.right.fresh||
                Math.Abs(f.left.observedAt-f.right.observedAt)>SealFeatureExtractor.MaximumHandSkew)return false;
            bool Feature(string id,Func<float,bool> predicate)=>lab.Features.Any(x=>x.id==id&&x.valid&&x.highConfidence&&predicate(x.value));
            if(!Feature("Both.WristDistance",v=>v>=cancelDistance))return false;
            foreach(var side in new[]{"L.","R."})foreach(var finger in new[]{"Index","Middle","Ring"})
                if(!Feature(side+finger+".Curl",v=>v<25))return false;
            return true;
        }
        void RejectVoice(string phrase, double endedAt, string reason)
        {
            LastVoiceBlock = $"{phrase}：{reason}";
            TraceCast("voice_rejected",LastVoiceBlock,inputTime:endedAt);
        }
        void RejectDeferredVoice(string reason)
        {
            while (deferredVoice.Count>0) { var v=deferredVoice.Dequeue(); RejectVoice(v.phrase,v.endedAt,reason); }
        }
        bool VoiceGate(string phrase,double endedAt,string stage)
        {
            string reason=Engine==null?"技能系统未初始化":!isActiveAndEnabled?"技能组件未启用":
                applicationPaused?"应用已暂停":inputSuspended?"输入暂停中":Time.timeScale<=0?"游戏时间暂停":
                (ExclusiveInputBlocked?.Invoke()??false)?(VoiceOwnershipDetail?.Invoke()??"腕部菜单独占输入"):
                endedAt<=voiceNotBefore?$"口令早于输入切换屏障（词尾 {endedAt:F3}，屏障 {voiceNotBefore:F3}）":null;
            if(reason==null)return true;
            RejectVoice(phrase,endedAt,stage+"："+reason);return false;
        }
        public void ReceiveVoice(string phrase,double endedAt)
        {
            TraceCast("voice_received",phrase,inputTime:endedAt);
            if(!VoiceGate(phrase,endedAt,"接收"))return;
            if(DeferVoiceUntilLateUpdate)
            {
                if(deferredVoice.Count<32) { deferredVoice.Enqueue((phrase,endedAt)); TraceCast("voice_queued",phrase,inputTime:endedAt); }
                else RejectVoice(phrase,endedAt,"口令队列已满");
                return;
            }
            ProcessVoice(phrase,endedAt);
        }
        private void ProcessVoice(string phrase,double endedAt)
        {
            if(!VoiceGate(phrase,endedAt,"处理"))return;
            TraceCast("voice_processing",phrase,inputTime:endedAt);
            VoiceEventCount++;
            if(SkillPhrase.Normalize(phrase)==Engine.CancelPhrase)
            {
                bool pending=!string.IsNullOrEmpty(Engine.PendingSkillId);long epoch=Engine.Epoch;
                Engine.Voice(phrase,endedAt,InputNow);
                if(Engine.LastVoiceRejection!=null) RejectVoice(phrase,endedAt,Engine.LastVoiceRejection);
                if(Engine.Epoch!=epoch){if(CancelOverride!=null)CancelOverride();else FinishCancel(pending);}
                return;
            }
            if(VoiceInputBlocked?.Invoke()??false) { RejectVoice(phrase,endedAt,VoiceOwnershipDetail?.Invoke()??"动作仲裁占用"); return; }
            Engine.Voice(phrase,endedAt,InputNow);
            if(Engine.LastVoiceRejection!=null) RejectVoice(phrase,endedAt,Engine.LastVoiceRejection);
        }
        public void ScheduleVoice(string phrase)
        {
            if(Engine==null||!isActiveAndEnabled||applicationPaused||inputSuspended||Time.timeScale<=0)return;
            string normalized=SkillPhrase.Normalize(phrase);
            if(!voicePhrases.Contains(normalized)){Report("模拟口令不在当前技能目录中");return;}
            scheduledPhrase=normalized;scheduledAt=InputNow+Math.Max(1,simulatedDelay);
            Report($"模拟口令将在 {Math.Max(1,simulatedDelay):F0} 秒后输入：{normalized}。可以先摆印。");
        }
        public void Cancel()
        {
            if(CancelOverride!=null){CancelOverride();return;}
            scheduledPhrase=null;bool pending=Engine!=null&&!string.IsNullOrEmpty(Engine.PendingSkillId);
            Engine?.Cancel(InputNow);FinishCancel(pending);
        }
        /// <summary>Invalidate pending evidence without cancelling already committed effects.</summary>
        public void CancelInputOnly()
        {
            scheduledPhrase=null;RejectDeferredVoice("取消施法清除了队列");Engine?.Cancel(InputNow);ResetCancelGesture();
        }
        private void FinishCancel(bool wasPending)
        {
            scheduledPhrase=null;
            if(!wasPending&&effects)effects.CancelLatest();
            Report(wasPending?"已取消待匹配技能；旧输入作废，请重新结印或念咒。":"已取消输入，并收起当前可取消效果。");
        }
        private void Report(string text)
        {
            TraceCast("status",text,Engine?.PendingSkillId??"");
            LastMessage=text;messages.Enqueue(text);while(messages.Count>4)messages.Dequeue();
            Debug.Log("[技能测试] "+text,this);
        }
        private void ResetCancelGesture(){openSince=lastCancelFrame=-1;cancelLatched=false;previous=null;}
        private void Subscribe()
        {
            if(effects){effects.Message-=EffectReport;effects.Message+=EffectReport;effects.ThrowObserved-=TraceThrow;effects.ThrowObserved+=TraceThrow;}
            if(voice){voice.Diagnostic-=TraceVoiceTransport;voice.Diagnostic+=TraceVoiceTransport;voice.PhraseReceived-=ReceiveVoice;voice.PhraseReceived+=ReceiveVoice;voice.ConfigurePhrases(voicePhrases);}
        }
        private void OnEnable(){if(Engine!=null){ResetInputLifecycle();Subscribe();}}
        private void ResetInputLifecycle()
        {
            TraceCast("input_reset","暂停、恢复或重新启用：清除旧输入与临时效果");
            scheduledPhrase=null;RejectDeferredVoice("输入生命周期重置");ResetCancelGesture();Engine?.Reset(InputNow);effects?.ClearAll();
        }
        private void OnApplicationPause(bool paused){applicationPaused=inputSuspended=paused;ResetInputLifecycle();}
        private void OnDisable()
        {
            scheduledPhrase=null;deferredVoice.Clear();ResetCancelGesture();
            if(voice){voice.Diagnostic-=TraceVoiceTransport;voice.PhraseReceived-=ReceiveVoice;voice.ConfigurePhrases(null);}
            if(effects){effects.ClearAll();effects.Message-=EffectReport;effects.ThrowObserved-=TraceThrow;}
            Engine?.Reset(InputNow);
            TraceCast("session_end","组件停用或退出Play");castTrace?.Dispose();castTrace=null;
        }
    }
}

