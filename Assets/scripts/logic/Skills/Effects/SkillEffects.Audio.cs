using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    public sealed partial class SkillEffects
    {
        SkillAudioPlayer skillAudio;
        Transform activationAudioAnchor;
        public bool LastActivationAudioHandled { get; private set; }
        public bool LastLaunchAudioHandled { get; private set; }
        public bool LastImpactAudioHandled { get; private set; }
        public SkillAudioPlayer AudioPlayer => skillAudio ? skillAudio : GetComponent<SkillAudioPlayer>();

        internal bool PlayAudio(SkillAudioProfile profile, SkillAudioCue cue, Vector3 position)
        {
            if (!profile) return false;
            try
            {
                if (!skillAudio) skillAudio = GetComponent<SkillAudioPlayer>();
                if (!skillAudio && Application.isPlaying) skillAudio = gameObject.AddComponent<SkillAudioPlayer>();
                return skillAudio && skillAudio.TryPlay(profile, cue, position);
            }
            catch (Exception error) { Debug.LogWarning("技能音频入口失败：" + error.Message, this); return false; }
        }
        internal void SetActivationAudioAnchor(Transform anchor) => activationAudioAnchor = anchor;
        void CompleteActivationAudio(SkillDefinition skill)
        {
            try
            {
                var profile = skill && skill.effectDefinition ? skill.effectDefinition.audioProfile : null;
                Vector3 point = activationAudioAnchor ? activationAudioAnchor.position : head ? head.position : transform.position;
                LastActivationAudioHandled = PlayAudio(profile, SkillAudioCue.Activated, point);
                if (profile && activationAudioAnchor)
                {
                    // A loop-only profile still needs a player, even when its activation slot is empty.
                    if (!skillAudio) skillAudio = GetComponent<SkillAudioPlayer>();
                    if (!skillAudio && profile.heldLoop && Application.isPlaying) skillAudio = gameObject.AddComponent<SkillAudioPlayer>();
                    if (skillAudio) skillAudio.StartHeldLoop(profile, activationAudioAnchor);
                }
            }
            catch (Exception error) { Debug.LogWarning("技能音频初始化失败：" + error.Message, this); }
        }
        internal void StopHeldAudio(Transform anchor)
        {
            var player = AudioPlayer;
            if (player) player.StopHeldLoop(anchor);
        }
        void StopAllAudio()
        {
            var player = AudioPlayer;
            if (player) player.StopAll();
        }
    }
}
