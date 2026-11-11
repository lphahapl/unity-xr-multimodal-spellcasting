using System;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // 为未指定音乐的探索与战斗提供循环片段。
    static class P1FallbackAudio
    {
        const int Rate = 16000;
        static readonly float[] Explore = { 293.66f, 0, 440, 329.63f, 0, 392, 329.63f, 0 };
        static readonly float[] Combat = { 146.83f,220,146.83f,261.63f,196,220,146.83f,196 };

        public static AudioClip Music(bool combat)
        {
            var notes = combat ? Combat : Explore;
            int beat = combat ? Rate / 2 : Rate;
            var samples = new float[beat * notes.Length];
            for (int i=0; i<samples.Length; ++i)
            {
                float frequency = notes[i / beat];
                if (frequency <= 0) continue;
                float t = (i % beat) / (float)Rate;
                float envelope = Mathf.Min(1, t/.014f) * Mathf.Exp(-(combat ? 7 : 4)*t)
                               * Mathf.Clamp01((beat/(float)Rate-t)/.04f);
                double phase = Math.PI * 2 * frequency * t;
                samples[i] = (float)((Math.Sin(phase) + .32*Math.Sin(phase*2.41) + .12*Math.Sin(phase*3.98))*envelope*.36);
            }
            return Create(combat ? "法阵 · 战斗钟列" : "山门 · 探索编磬", samples);
        }
        static AudioClip Create(string name,float[] samples)
        {
            var clip = AudioClip.Create(name,samples.Length,1,Rate,false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples,0);
            return clip;
        }
    }
}
