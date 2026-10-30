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

        public SkillMatchEngine Engine { get; private set; }
        public IReadOnlyList<SkillDefinition> ConfiguredSkills => catalog ? catalog.skills : skills;
        public IReadOnlyList<SkillRecipeInfo> Recipes => Engine?.ActiveRecipes ?? Array.Empty<SkillRecipeInfo>();
        public string LastMessage { get; private set; } = "等待初始化";
        public IEnumerable<string> RecentMessages => messages;
        public int VoiceEventCount { get; private set; }
        // Optional session policy: installed before Start by the RPG adapter. Old scenes need no policy.
        public Func<SkillDefinition, bool> ActivationOverride { get; set; }
        public Func<bool> GestureInputBlocked { get; set; }
        public Func<bool> VoiceInputBlocked { get; set; }
        public Action CancelOverride { get; set; }
        public bool DeferVoiceUntilLateUpdate { get; set; }
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
            var errors=ConfigurationErrors();
            if(errors.Count>0){Report("配置未通过："+string.Join("；",errors));enabled=false;return;}
            if(!chineseFont)chineseFont=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            lab.recordOnStart=false;lab.SetTestMode(true);
            Engine=new SkillMatchEngine(ConfiguredSkills,catalog?catalog.matching:null,CancelPhrase);
            Engine.Log=Report;Engine.TryActivate=skill=>ActivationOverride!=null?ActivationOverride(skill):effects.TryActivate(skill);
            Engine.ExternalCooldownAuthority=ActivationOverride!=null;Engine.Tick(Time.realtimeSinceStartupAsDouble);
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
            double now=Time.realtimeSinceStartupAsDouble;
            if(applicationPaused||Time.timeScale<=0)
            {
                if(!inputSuspended)ResetInputLifecycle();inputSuspended=true;return;
            }
            if(inputSuspended){ResetInputLifecycle();inputSuspended=false;}
            while(deferredVoice.Count>0)
            {
                var input=deferredVoice.Dequeue(); ProcessVoice(input.phrase,input.endedAt);
            }
            Engine.Tick(now);
            if(lab.Frame!=previous&&lab.Frame!=null)
            {
                previous=lab.Frame;
                if(lastCancelFrame<0||now-lastCancelFrame>lab.staleAfterSeconds)openSince=-1;
                lastCancelFrame=now;
                bool open=enableOpenHandCancel&&OpenCancelPose();
                if(open)
                {
                    if(openSince<0)openSince=now;
                    if(!cancelLatched&&now-openSince>=cancelHold){Cancel();cancelLatched=true;}
                }
                else {openSince=-1;cancelLatched=false;}
                Engine.Observe(lab.Frame,lab.Scores,cancelLatched||(GestureInputBlocked?.Invoke()??false));
            }
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
                if(score!=null)Debug.Log($"[技能等待] 本次有效阈值 {Math.Max(pending.Floor,score.profile.threshold-pending.Discount)*100:F1} 分；以下为基础模板明细：\n"+SealTestDiagnostics.Explain(score),this);
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
        public void ReceiveVoice(string phrase,double endedAt)
        {
            if(Engine==null||!isActiveAndEnabled||applicationPaused||inputSuspended||Time.timeScale<=0)return;
            if(DeferVoiceUntilLateUpdate)
            {
                if(deferredVoice.Count<32)deferredVoice.Enqueue((phrase,endedAt));
                return;
            }
            ProcessVoice(phrase,endedAt);
        }
        private void ProcessVoice(string phrase,double endedAt)
        {
            VoiceEventCount++;
            if(SkillPhrase.Normalize(phrase)==Engine.CancelPhrase)
            {
                bool pending=!string.IsNullOrEmpty(Engine.PendingSkillId);long epoch=Engine.Epoch;
                Engine.Voice(phrase,endedAt,Time.realtimeSinceStartupAsDouble);
                if(Engine.Epoch!=epoch){if(CancelOverride!=null)CancelOverride();else FinishCancel(pending);}
                return;
            }
            if(!(VoiceInputBlocked?.Invoke()??false))Engine.Voice(phrase,endedAt,Time.realtimeSinceStartupAsDouble);
        }
        public void ScheduleVoice(string phrase)
        {
            if(Engine==null||!isActiveAndEnabled||applicationPaused||inputSuspended||Time.timeScale<=0)return;
            string normalized=SkillPhrase.Normalize(phrase);
            if(!voicePhrases.Contains(normalized)){Report("模拟口令不在当前技能目录中");return;}
            scheduledPhrase=normalized;scheduledAt=Time.realtimeSinceStartupAsDouble+Math.Max(1,simulatedDelay);
            Report($"模拟口令将在 {Math.Max(1,simulatedDelay):F0} 秒后输入：{normalized}。可以先摆印。");
        }
        public void Cancel()
        {
            if(CancelOverride!=null){CancelOverride();return;}
            scheduledPhrase=null;bool pending=Engine!=null&&!string.IsNullOrEmpty(Engine.PendingSkillId);
            Engine?.Cancel(Time.realtimeSinceStartupAsDouble);FinishCancel(pending);
        }
        /// <summary>Invalidate pending evidence without cancelling already committed effects.</summary>
        public void CancelInputOnly()
        {
            scheduledPhrase=null;deferredVoice.Clear();Engine?.Cancel(Time.realtimeSinceStartupAsDouble);ResetCancelGesture();
        }
        private void FinishCancel(bool wasPending)
        {
            scheduledPhrase=null;
            if(!wasPending&&effects)effects.CancelLatest();
            Report(wasPending?"已取消待匹配技能；旧输入作废，请重新结印或念咒。":"已取消输入，并收起当前可取消效果。");
        }
        private void Report(string text)
        {
            LastMessage=text;messages.Enqueue(text);while(messages.Count>4)messages.Dequeue();
            Debug.Log("[技能测试] "+text,this);
        }
        private void ResetCancelGesture(){openSince=lastCancelFrame=-1;cancelLatched=false;previous=null;}
        private void Subscribe()
        {
            if(effects){effects.Message-=Report;effects.Message+=Report;}
            if(voice){voice.PhraseReceived-=ReceiveVoice;voice.PhraseReceived+=ReceiveVoice;voice.ConfigurePhrases(voicePhrases);}
        }
        private void OnEnable(){if(Engine!=null){ResetInputLifecycle();Subscribe();}}
        private void ResetInputLifecycle()
        {
            scheduledPhrase=null;deferredVoice.Clear();ResetCancelGesture();Engine?.Reset(Time.realtimeSinceStartupAsDouble);effects?.ClearAll();
        }
        private void OnApplicationPause(bool paused){applicationPaused=inputSuspended=paused;ResetInputLifecycle();}
        private void OnDisable()
        {
            scheduledPhrase=null;deferredVoice.Clear();ResetCancelGesture();
            if(voice){voice.PhraseReceived-=ReceiveVoice;voice.ConfigurePhrases(null);}
            if(effects){effects.Message-=Report;effects.ClearAll();}
            Engine?.Reset(Time.realtimeSinceStartupAsDouble);
        }
    }
}

