using System;
using NarutoVR.Audio;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>关卡音乐、环境声及语音避让协调。技能声音由效果SO和SkillAudioPlayer管理，腕部声音由视图管理。</summary>
    [DisallowMultipleComponent]
    public sealed class P1AudioManager : MonoBehaviour
    {
        [Header("音乐与环境（未配置音乐时使用程序生成的钟磬循环）")]
        [InspectorName("探索音乐")] public AudioClip explorationMusic;
        [InspectorName("探索音乐未指定时保持静音")] public bool keepExplorationSilent;
        [InspectorName("战斗音乐")] public AudioClip combatMusic;
        [InspectorName("环境底声（可选）")] public AudioClip ambience;
        [InspectorName("音乐音量"), Range(0,1)] public float musicVolume = .28f;
        [InspectorName("环境音量"), Range(0,1)] public float ambienceVolume = .2f;
        [InspectorName("探索与战斗交叉淡化秒数"), Min(.05f)] public float crossfadeSeconds = 1.2f;
        [Header("语音避让（音乐、环境声和玩家脚步）")]
        [InspectorName("衰减分贝"), Range(-24,0)] public float duckDecibels = -8f;
        [InspectorName("有声帧保持秒数"), Range(.1f,.6f)] public float voiceHoldSeconds = .24f;
        [InspectorName("压低时间（秒）"), Min(.01f)] public float duckAttack = .06f;
        [InspectorName("回升时间（秒）"), Min(.01f)] public float duckRelease = .4f;
        // Serialized compatibility for existing scenes. New scenes author the FootstepPlayer directly.
        [HideInInspector] public AudioClip[] footstepClips = Array.Empty<AudioClip>();
        [HideInInspector] public float footstepVolume = .22f;
        [HideInInspector] public float footstepSpacing = .55f;

        public float CombatMix { get; private set; }
        public float DuckGain { get; private set; } = 1;
        public bool VoiceDucking { get; private set; }
        public bool MusicReady => explorationSource && combatSource && (explorationSource.clip || keepExplorationSilent) && combatSource.clip;
        public int SpatialCueCount { get; private set; }
        public int FootstepCount { get; private set; }
        public Vector3 LastCuePosition { get; private set; }
        public string Status => $"音乐 {(MusicReady ? "已就绪" : "未启动")} · 战斗混合 {CombatMix:P0} · 麦克风避让 {(VoiceDucking ? "开" : "关")} · 背景增益 {DuckGain:0.00}";

        LinearRpgController controller;
        GameObject audioRoot;
        AudioSource explorationSource, combatSource, ambientSource;
        FootstepPlayer footsteps;
        RpgLocomotionController walking;
        readonly AudioSource[] effectsSources = new AudioSource[8];
        readonly bool[] duckable = new bool[8];
        readonly float[] voiceVolume = new float[8];
        readonly AudioClip[] ownedClips = new AudioClip[2];
        int nextChannel;
        bool appPaused;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            controller = GetComponent<LinearRpgController>();
            if (!controller) { Debug.LogWarning("P1 音频管理器需要挂在线性关卡控制器同一物体。", this); return; }
            audioRoot = new GameObject("P1 音轨与空间声源");
            audioRoot.transform.SetParent(transform, false);
            explorationSource = Source("探索", false, true);
            combatSource = Source("战斗", false, true);
            explorationSource.clip = explorationMusic ? explorationMusic : keepExplorationSilent ? null : (ownedClips[0] = P1FallbackAudio.Music(false));
            combatSource.clip = combatMusic ? combatMusic : (ownedClips[1] = P1FallbackAudio.Music(true));
            if (ambience) { ambientSource = Source("环境", false, true); ambientSource.clip = ambience; }
            for (int i = 0; i < effectsSources.Length; ++i) effectsSources[i] = Source("空间声道 " + (i+1), true, false);
            CombatMix = IsCombat ? 1 : 0;
            ApplyVolumes();
            double start = AudioSettings.dspTime + .05;
            if (explorationSource.clip) explorationSource.PlayScheduled(start); combatSource.PlayScheduled(start);
            if (ambientSource) ambientSource.PlayScheduled(start);
            if (controller.chapter && controller.chapter.UsesExplorationFootsteps && controller.locomotion)
            {
                walking = controller.locomotion;
                footsteps = walking.GetComponent<FootstepPlayer>();
                if (!footsteps) footsteps = walking.gameObject.AddComponent<FootstepPlayer>();
                if (!footsteps.HasClips)
                {
                    footsteps.variations = HasVariants(footstepClips) ? footstepClips : Resources.LoadAll<AudioClip>("Audio/Footsteps");
                    footsteps.stepDistance = footstepSpacing;
                    footsteps.volume = footstepVolume;
                }
                footsteps.UseReportedTravel(true);
                footsteps.SetGain(DuckGain);
                footsteps.Stepped += OnFootstep;
                walking.ExplorationTravelled += OnExplorationTravelled;
            }
        }
        bool IsCombat => controller && controller.locomotion && controller.locomotion.EncounterCombat;
        AudioSource Source(string label, bool spatial, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(audioRoot.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = spatial ? 1 : 0;
            source.dopplerLevel = 0; source.minDistance = .7f; source.maxDistance = 18;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
        void Update()
        {
            if (!MusicReady || !controller) return;
            var receiver = controller.skills ? controller.skills.voice : null;
            VoiceDucking = !appPaused && Time.timeScale > 0 &&
                ((receiver && receiver.HasRecentMicrophoneSpeech(voiceHoldSeconds)) ||
                 (controller && controller.chapter && controller.chapter.Speaking));
            CombatMix = MoveMix(CombatMix, IsCombat, Time.unscaledDeltaTime, crossfadeSeconds);
            DuckGain = MoveDuck(DuckGain, VoiceDucking, Time.unscaledDeltaTime, duckDecibels, duckAttack, duckRelease);
            ApplyVolumes();
        }
        public static float MoveMix(float value, bool combat, float delta, float duration) =>
            Mathf.MoveTowards(value, combat ? 1 : 0, Mathf.Max(0, delta) / Mathf.Max(.01f,duration));
        public static float MoveDuck(float value, bool active, float delta, float db, float attack, float release)
        {
            float low = Mathf.Pow(10, Mathf.Clamp(db,-24,0) / 20f);
            if (low >= 1) return 1;
            return Mathf.MoveTowards(value, active ? low : 1, (1-low)*Mathf.Max(0,delta)/Mathf.Max(.01f, active ? attack : release));
        }
        void ApplyVolumes()
        {
            explorationSource.volume = musicVolume * DuckGain * Mathf.Sqrt(1-CombatMix);
            combatSource.volume = musicVolume * DuckGain * Mathf.Sqrt(CombatMix);
            if (ambientSource) ambientSource.volume = ambienceVolume * DuckGain;
            if (footsteps) footsteps.SetGain(DuckGain);
            for (int i=0; i<effectsSources.Length; ++i)
                if (effectsSources[i] && duckable[i]) effectsSources[i].volume = voiceVolume[i] * DuckGain;
        }

        void OnExplorationTravelled(float distance)
        {
            float travel = appPaused || !controller.CanAct || controller.UiInputOwned ? 0 : distance;
            footsteps.ReportTravel(travel,walking.PlayerFloorPosition,walking.head ? walking.head.right : walking.transform.right);
        }

        void OnFootstep(Vector3 position)
        {
            LastCuePosition = position;
            ++SpatialCueCount;
            ++FootstepCount;
        }

        static bool HasVariants(AudioClip[] clips) => clips != null && Array.Exists(clips, clip => clip);

        /// <summary>For authored ambient emitters (e.g. distant mechanism/footstep). Critical cues use the unattenuated effect route.</summary>
        public bool PlayAmbienceAt(AudioClip clip, Vector3 position, float volume) => PlayAt(clip,position,volume,true);
        bool PlayAt(AudioClip clip, Vector3 position, float volume, bool environmental)
        {
            if (!isActiveAndEnabled || !MusicReady || !clip || appPaused) return false;
            int channel = nextChannel;
            for (int offset=0; offset<effectsSources.Length; ++offset)
            {
                int candidate = (nextChannel + offset) % effectsSources.Length;
                if (!effectsSources[candidate].isPlaying) { channel = candidate; break; }
            }
            nextChannel = (channel+1) % effectsSources.Length;
            var source = effectsSources[channel];
            source.Stop(); source.transform.position = position; source.clip = clip;
            duckable[channel] = environmental; voiceVolume[channel] = Mathf.Clamp01(volume);
            source.volume = voiceVolume[channel] * (environmental ? DuckGain : 1); source.Play();
            LastCuePosition = position; ++SpatialCueCount;
            return true;
        }
        void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (!MusicReady) return;
            if (paused)
            {
                if (footsteps) footsteps.ResetTracking();
                VoiceDucking = false; DuckGain = 1; ApplyVolumes();
                explorationSource.Pause(); combatSource.Pause(); if (ambientSource) ambientSource.Pause();
                foreach (var source in effectsSources) if (source) source.Stop();
            }
            else { explorationSource.UnPause(); combatSource.UnPause(); if (ambientSource) ambientSource.UnPause(); }
        }
        void OnDisable()
        {
            if (walking) walking.ExplorationTravelled -= OnExplorationTravelled;
            walking = null;
            if (footsteps)
            {
                footsteps.Stepped -= OnFootstep;
                footsteps.ResetTracking();
            }
            footsteps = null;
            VoiceDucking = appPaused = false; DuckGain = 1;
            if (audioRoot) { audioRoot.SetActive(false); Destroy(audioRoot); }
            audioRoot = null; explorationSource = combatSource = ambientSource = null;
            for (int i=0; i<ownedClips.Length; ++i) { if (ownedClips[i]) Destroy(ownedClips[i]); ownedClips[i] = null; }
            Array.Clear(effectsSources,0,effectsSources.Length); Array.Clear(duckable,0,duckable.Length);
        }
    }
}
