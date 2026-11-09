using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public enum SkillAudioCue { Activated, Released, Swing, Impact, Lightning, Ended }

    [Serializable]
    public sealed class SkillAudioVariation
    {
        [InspectorName("音频变奏")] public AudioClip[] clips = Array.Empty<AudioClip>();
        [InspectorName("音量"), Range(0, 1)] public float volume = .7f;
        [InspectorName("音高随机幅度"), Range(0, .15f)] public float pitchVariation = .025f;
        public bool HasClip => clips != null && Array.Exists(clips, clip => clip);
    }

    /// <summary>技能共享音频配置；上次变奏、声源和循环生命周期由 SkillAudioPlayer 持有。</summary>
    [CreateAssetMenu(menuName = "NarutoVR/技能/音频配置")]
    public sealed class SkillAudioProfile : ScriptableObject
    {
        [InspectorName("生效或成形")] public SkillAudioVariation activated = new();
        [InspectorName("脱手或出波")] public SkillAudioVariation released = new();
        [InspectorName("有效挥砍")] public SkillAudioVariation swing = new();
        [InspectorName("命中")] public SkillAudioVariation impact = new();
        [InspectorName("追加落雷")] public SkillAudioVariation lightning = new();
        [InspectorName("收起或消散")] public SkillAudioVariation ended = new();
        [Header("持握循环（可选）")]
        [InspectorName("循环音频")] public AudioClip heldLoop;
        [InspectorName("循环音量"), Range(0, 1)] public float heldLoopVolume = .12f;
        [Header("空间衰减")]
        [InspectorName("近距离（米）"), Min(.01f)] public float minDistance = .7f;
        [InspectorName("最远距离（米）"), Min(.1f)] public float maxDistance = 18f;

        public SkillAudioVariation Get(SkillAudioCue cue) => cue switch
        {
            SkillAudioCue.Activated => activated,
            SkillAudioCue.Released => released,
            SkillAudioCue.Swing => swing,
            SkillAudioCue.Impact => impact,
            SkillAudioCue.Lightning => lightning,
            SkillAudioCue.Ended => ended,
            _ => null
        };
    }
}
