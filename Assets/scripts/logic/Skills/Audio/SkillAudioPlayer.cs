using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat
{
    /// <summary>施法者拥有的空间音频：有限声道、不连续重复变奏、跟随持物的循环和统一清理。</summary>
    [DisallowMultipleComponent]
    public sealed class SkillAudioPlayer : MonoBehaviour
    {
        [InspectorName("技能音效音量"), Range(0, 1)] public float masterVolume = 1;
        readonly AudioSource[] voices = new AudioSource[8];
        readonly Dictionary<(int, SkillAudioCue), AudioClip> previous = new();
        readonly List<LoopVoice> loops = new();
        readonly System.Random random = new();
        GameObject root;
        int nextVoice;
        bool applicationPaused, gamePaused;
        sealed class LoopVoice
        {
            public Transform Follow;
            public AudioSource Source;
            public float Volume;
        }
        public int PlayCount { get; private set; }
        public int ActiveLoopCount => loops.Count;
        public SkillAudioCue LastCue { get; private set; }
        public AudioClip LastClip { get; private set; }
        public Vector3 LastPosition { get; private set; }

        /// <summary>有配置但主动静音也视为已处理；无配置返回 false，允许调用方使用通用提示。</summary>
        public bool TryPlay(SkillAudioProfile profile, SkillAudioCue cue, Vector3 position)
        {
            var choice = profile ? profile.Get(cue) : null;
            if (choice == null || !choice.HasClip || !Finite(position)) return false;
            if (!isActiveAndEnabled || !Application.isPlaying || applicationPaused || Time.timeScale <= 0) return false;
            if (!float.IsFinite(masterVolume) || masterVolume <= 0 || !float.IsFinite(choice.volume) || choice.volume <= 0) return true;
            try
            {
                var key = (profile.GetInstanceID(), cue);
                previous.TryGetValue(key, out var last);
                AudioClip pick = null;
                int candidates = 0;
                foreach (var clip in choice.clips)
                    if (clip && clip != last && random.Next(++candidates) == 0) pick = clip;
                if (!pick) pick = last;
                if (!pick) return false;
                EnsureVoices();
                int selected = nextVoice;
                for (int i = 0; i < voices.Length; i++)
                {
                    int index = (nextVoice + i) % voices.Length;
                    if (!voices[index].isPlaying) { selected = index; break; }
                }
                nextVoice = (selected + 1) % voices.Length;
                var source = voices[selected];
                source.Stop();
                Configure(source, profile);
                source.transform.position = position;
                source.clip = pick;
                source.volume = Mathf.Clamp01(choice.volume) * Mathf.Clamp01(masterVolume);
                float variation = float.IsFinite(choice.pitchVariation) ? Mathf.Clamp(choice.pitchVariation, 0, .15f) : 0;
                source.pitch = 1 + ((float)random.NextDouble() * 2 - 1) * variation;
                source.Play();
                previous[key] = pick;
                LastClip = pick; LastCue = cue; LastPosition = position; PlayCount++;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("技能音效播放失败：" + error.Message, this);
                return false;
            }
        }

        public void StartHeldLoop(SkillAudioProfile profile, Transform follow)
        {
            if (!profile || !profile.heldLoop || !follow || !isActiveAndEnabled || !Application.isPlaying || applicationPaused || Time.timeScale <= 0) return;
            StopHeldLoop(follow);
            if (!float.IsFinite(profile.heldLoopVolume) || profile.heldLoopVolume <= 0) return;
            AudioSource source = null;
            try
            {
                EnsureVoices();
                source = NewSource("持握循环");
                Configure(source, profile);
                source.loop = true; source.clip = profile.heldLoop;
                source.transform.position = follow.position;
                float volume = Mathf.Clamp01(profile.heldLoopVolume);
                source.volume = volume * MasterGain;
                source.Play();
                loops.Add(new LoopVoice { Follow = follow, Source = source, Volume = volume });
            }
            catch (Exception error)
            {
                if (source) { source.Stop(); Destroy(source.gameObject); }
                Debug.LogWarning("持握音效启动失败：" + error.Message, this);
            }
        }

        public void StopHeldLoop(Transform follow)
        {
            for (int i = loops.Count - 1; i >= 0; i--)
                if (!loops[i].Follow || loops[i].Follow == follow) RemoveLoop(i);
        }

        void RemoveLoop(int index)
        {
            var source = loops[index].Source;
            loops.RemoveAt(index);
            if (source) { source.Stop(); Destroy(source.gameObject); }
        }

        public void StopAll()
        {
            foreach (var source in voices) if (source) source.Stop();
            for (int i = loops.Count - 1; i >= 0; i--) RemoveLoop(i);
        }

        float MasterGain => float.IsFinite(masterVolume) ? Mathf.Clamp01(masterVolume) : 0;
        void LateUpdate()
        {
            bool paused = Time.timeScale <= 0;
            if (paused != gamePaused)
            {
                foreach (var source in voices) if (source) { if (paused) source.Pause(); else source.UnPause(); }
                foreach (var loop in loops) if (loop.Source) { if (paused) loop.Source.Pause(); else loop.Source.UnPause(); }
                gamePaused = paused;
            }
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                var loop = loops[i];
                if (!loop.Follow || !loop.Source) { RemoveLoop(i); continue; }
                if (Finite(loop.Follow.position)) loop.Source.transform.position = loop.Follow.position;
                loop.Source.volume = loop.Follow.gameObject.activeInHierarchy ? loop.Volume * MasterGain : 0;
            }
        }

        void EnsureVoices()
        {
            if (root) return;
            root = new GameObject("技能空间声源");
            // One-shot impacts stay at the contact point when the caster moves.
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            for (int i = 0; i < voices.Length; i++) voices[i] = NewSource("技能声道 " + (i + 1));
        }
        AudioSource NewSource(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(root.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1; source.dopplerLevel = 0;
            return source;
        }
        static void Configure(AudioSource source, SkillAudioProfile profile)
        {
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = float.IsFinite(profile.minDistance) ? Mathf.Max(.01f, profile.minDistance) : .7f;
            source.maxDistance = float.IsFinite(profile.maxDistance) ? Mathf.Max(source.minDistance, profile.maxDistance) : 18;
        }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) StopAll(); }
        void OnDisable()
        {
            StopAll();
            if (root) { root.SetActive(false); Destroy(root); }
            root = null; Array.Clear(voices, 0, voices.Length); previous.Clear();
            applicationPaused = gamePaused = false;
        }
    }
}
