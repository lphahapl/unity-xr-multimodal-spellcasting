using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using NarutoVR.SealCombat.Events;

namespace NarutoVR.SealCombat
{
    public enum NarrationAdvanceMode
    {
        [InspectorName("自动继续（音频播完）")] AudioFinished = 0,
        [InspectorName("等待操作（可提前结束本句音频）")] Operation = 1,
        [InspectorName("音频和操作都完成")] AudioAndOperation = 2
    }

    [Serializable]
    public sealed class NarrationLine
    {
        [Header("一、本句内容")]
        [InspectorName("音频文件名"), Tooltip("Resources/Narration 下的音频名或相对路径，可带音频扩展名。仅等待操作模式可以留空。")]
        public string audioFileName;
        [InspectorName("说话者")]
        public string speaker;
        [Header("二、推进条件")]
        [InspectorName("推进方式")] public NarrationAdvanceMode advanceMode;
        [InspectorName("到下一句的间隔（秒）"), Min(0), Tooltip("本句条件满足后再等这些秒数。0 表示立即继续；使用游戏时间，最后一句忽略此项。")]
        public float delayAfter;
        [Header("三、本句事件")]
        [InspectorName("本句开始")] public UnityEvent onStarted = new UnityEvent();
        [InspectorName("本句音频自然播完"), Tooltip("保留原事件语义；被操作提前打断、停止或重启不会触发。")]
        public UnityEvent onCompleted = new UnityEvent();
        [InspectorName("本句条件通过（开始句间等待）")] public UnityEvent onAdvanced = new UnityEvent();

        internal string ResourcePath
        {
            get
            {
                string name = (audioFileName ?? "").Trim().Replace('\\', '/');
                switch (System.IO.Path.GetExtension(name).ToLowerInvariant())
                {
                    case ".wav": case ".mp3": case ".ogg": case ".aif": case ".aiff":
                    case ".flac": case ".xm": case ".mod": case ".it": case ".s3m":
                        name = name.Substring(0, name.LastIndexOf('.'));
                        break;
                }
                return "Narration/" + name;
            }
        }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class NarrationController : MonoBehaviour
    {
        [Header("一、对话内容")]
        [InspectorName("逐句配置")] public List<NarrationLine> lines = new List<NarrationLine>();
        [Header("二、整段对话事件")]
        [InspectorName("对话开始（每轮一次）")] public UnityEvent onDialogueStarted = new UnityEvent();
        [InspectorName("对话全部完成"), Tooltip("最后一句条件通过后触发；主动停止不触发。")]
        public UnityEvent onDialogueCompleted = new UnityEvent();
        /// <summary>当前句的运行状态。Id 就是列表下标，从 0 开始；配置里不存运行进度。</summary>
        public sealed class Step
        {
            public int Id { get; }
            public NarrationLine Line { get; }
            public bool AudioDone { get; internal set; }
            public bool OperationDone { get; internal set; }
            internal bool HasAudio, Loading, Advanced;
            internal double DelayUntil;
            internal AudioClip Clip;
            internal Step(int id, NarrationLine line) { Id = id; Line = line; }
            internal bool Ready => Line.advanceMode == NarrationAdvanceMode.AudioFinished ? AudioDone :
                Line.advanceMode == NarrationAdvanceMode.Operation ? OperationDone : AudioDone && OperationDone;
        }
        /// <summary>未开始或已结束时为 null。外部操作读取 Id 后即可发布本步骤完成事件。</summary>
        public Step CurrentStep => step;
        public NarrationLine CurrentLine => step?.Line;
        public int CurrentLineIndex => step?.Id ?? -1;
        public bool AudioCompleted => step != null && step.AudioDone;
        public bool OperationCompleted => step != null && step.OperationDone;
        public bool IsWaitingForNextLine => step != null && step.Advanced && index + 1 < sequence.Length;
        public float RemainingDelay => IsWaitingForNextLine ? (float)Math.Max(0, step.DelayUntil - Time.timeAsDouble) : 0;
        public string Status => step == null ? status :
            IsWaitingForNextLine ? $"第 {index + 1} 句已通过；到下一句还剩 {RemainingDelay:F1} 秒" :
            $"第 {index + 1} 句（步骤 ID：{step.Id}）；音频：{(step.AudioDone ? "完成" : "播放/加载中")}；操作：{(step.OperationDone ? "完成" : step.Line.advanceMode == NarrationAdvanceMode.AudioFinished ? "无需" : "等待操作")}";
        Step step;
        NarrationLine[] sequence;
        IDisposable operationSubscription;
        AudioSource voice;
        int index;
        int callbackDepth;
        bool startPending;
        string status = "未开始";

        void Reset() => GetComponent<AudioSource>().playOnAwake = false;
        void OnEnable()
        {
            operationSubscription?.Dispose();
            operationSubscription = EventCenter.Subscribe(NarrationEvents.OperationCompleted, OnOperation, this);
        }
        void OnDisable()
        {
            StopDialogue(); operationSubscription?.Dispose(); operationSubscription = null;
        }

        /// <summary>从第一句开始。已经播放时重复调用不重置；重新播放使用 RestartDialogue。</summary>
        public void StartDialogue()
        {
            if (step != null || startPending || !isActiveAndEnabled || lines == null || lines.Count == 0) return;
            // 开始事件里要求重启时留到下一帧执行，避免开始事件递归调用自身。
            if (callbackDepth > 0) { startPending = true; return; }
            sequence = lines.ToArray();
            voice = GetComponent<AudioSource>();
            voice.loop = false;
            index = 0;
            StartLine(true);
        }

        public void RestartDialogue()
        {
            StopDialogue();
            StartDialogue();
        }

        public void StopDialogue()
        {
            // 句间等待属于这一轮对话；停止、重启或禁用时一并取消。
            StopAllCoroutines();
            if (voice && step != null)
            {
                voice.Stop();
                voice.clip = null;
            }
            step = null; sequence = null; startPending = false; status = "对话已停止";
        }

        void StartLine(bool dialogueStart = false)
        {
            var line = sequence[index];
            if (line == null || !Enum.IsDefined(typeof(NarrationAdvanceMode), line.advanceMode))
            { Fail("本句配置为空或推进方式无效"); return; }
            var current = new Step(index, line);
            step = current;
            if (string.IsNullOrWhiteSpace(line.audioFileName) && line.advanceMode == NarrationAdvanceMode.Operation)
                current.AudioDone = true; // 纯操作句没有音频，也不发出“音频自然播完”事件。
            else if (!PrepareAudio(current)) return;
            if (dialogueStart && !Invoke(onDialogueStarted, current)) return;
            Invoke(line.onStarted, current);
        }

        bool PrepareAudio(Step current)
        {
            AudioClip clip = string.IsNullOrWhiteSpace(current.Line.audioFileName) ? null : Resources.Load<AudioClip>(current.Line.ResourcePath);
            if (!clip || !voice.enabled || clip.loadState == AudioDataLoadState.Failed ||
                (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData()))
            {
                Fail("无法播放音频：" + current.Line.ResourcePath); return false;
            }
            voice.clip = current.Clip = clip; current.HasAudio = true;
            current.Loading = clip.loadState == AudioDataLoadState.Loading;
            if (!current.Loading) voice.Play();
            return true;
        }

        void Update()
        {
            if (startPending)
            {
                startPending = false; StartDialogue(); return;
            }
            var current = step;
            if (current == null || current.Advanced) return;
            if (AudioListener.pause && voice && !voice.ignoreListenerPause) return;
            if (current.HasAudio && !current.AudioDone)
            {
                if (!voice || !voice.enabled || voice.clip != current.Clip || current.Clip.loadState == AudioDataLoadState.Failed)
                { Fail("音频来源被禁用、更换或加载失败"); return; }
                if (current.Loading)
                {
                    if (current.Clip.loadState != AudioDataLoadState.Loading)
                    { current.Loading = false; voice.Play(); }
                }
                else if (!voice.isPlaying)
                {
                    // 先记完成，再通知外部；下一帧不会重复发出音频完成事件。
                    current.AudioDone = true;
                    if (!Invoke(current.Line.onCompleted, current)) return;
                }
            }
            // 事件只记录操作结果；统一在这里推进一次，防止重复通知跳过多句。
            if (!IsCurrent(current) || !current.Ready) return;
            current.Advanced = true;
            if (voice) { voice.Stop(); voice.clip = null; }
            if (!Invoke(current.Line.onAdvanced, current)) return;
            if (index + 1 < sequence.Length) StartCoroutine(ContinueAfterDelay(current));
            else
            {
                StopDialogue(); status = "对话全部完成";
                Invoke(onDialogueCompleted, null);
            }
        }

        /// <summary>本句条件通过后只开一次协程。等完间隔再开始下一句，不需要单独计时组件。</summary>
        IEnumerator ContinueAfterDelay(Step current)
        {
            // Inspector 限制非负；脚本传入非法数值时按 0 处理，避免永远等不到下一句。
            float delay = float.IsFinite(current.Line.delayAfter) ? Mathf.Max(0, current.Line.delayAfter) : 0;
            current.DelayUntil = Time.timeAsDouble + delay;
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (!IsCurrent(current)) yield break;
            ++index;
            StartLine();
        }

        /// <summary>可直接绑定 UnityEvent：把当前步骤 ID 发到事件中心，无需额外 Publisher 组件。</summary>
        public void PublishCurrentOperation()
        {
            if (step != null && isActiveAndEnabled)
                NarrationEvents.PublishOperation(this, step.Id);
        }

        /// <summary>直接完成当前句操作，保留给 Inspector 中的即时回调绑定。</summary>
        public void CompleteCurrentOperation() => CompleteOperation(CurrentLineIndex);

        /// <summary>只接受当前步骤 ID；提前完成会保留到音频结束，重复完成不会重复推进。</summary>
        public bool CompleteOperation(int stepId)
        {
            var current = step;
            if (!isActiveAndEnabled || current == null || current.Advanced || current.Id != stepId ||
                current.Line.advanceMode == NarrationAdvanceMode.AudioFinished || current.OperationDone) return false;
            current.OperationDone = true;
            return true;
        }
        void OnOperation(NarrationOperationNotice notice)
        {
            // 步骤 ID 只在各自的对话内有效，因此先核对目标组件，避免两段对话串台。
            if (notice.Target == this) CompleteOperation(notice.StepId);
        }
        // 外部事件可能停止或重启对话；回到本方法后不得继续推进原来的句子。
        bool IsCurrent(Step current) => this && isActiveAndEnabled && current != null && ReferenceEquals(step, current);
        bool Invoke(UnityEvent callback, Step current)
        {
            ++callbackDepth;
            try { callback?.Invoke(); }
            catch (Exception error)
            {
                if (current != null && IsCurrent(current)) StopDialogue();
                Debug.LogException(error, this); return false;
            }
            finally { --callbackDepth; }
            return IsCurrent(current);
        }
        void Fail(string reason)
        {
            string message = $"旁白第 {index + 1} 句：{reason}。";
            StopDialogue(); status = message; Debug.LogError(message, this);
        }
    }
}
