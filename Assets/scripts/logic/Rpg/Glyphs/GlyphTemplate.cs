using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    [CreateAssetMenu(menuName = "考工律度/画符/符形模板")]
    public sealed class GlyphTemplate : ScriptableObject
    {
        [Serializable] public sealed class Stroke { public Vector2[] points = Array.Empty<Vector2>(); }
        [Serializable] public sealed class Example { public Stroke[] strokes = Array.Empty<Stroke>(); }

        [InspectorName("稳定符号 ID")] public string symbolId;
        [InspectorName("符名")] public string displayName;
        [InspectorName("匹配后效果")] public GlyphEffectDefinition effect;
        [Range(.01f, 1f), InspectorName("最低相似度")] public float threshold = .83f;
        [Tooltip("每个样本是一幅完整图案，第一份有效样本同时用于教学图样。")]
        public Example[] examples = Array.Empty<Example>();

        [Header("画符完成反馈")]
        [InspectorName("完成特效"), Tooltip("在画板中心播放的一次性粒子预制体。")] public ParticleSystem completionVfx;
        [InspectorName("完成音效")] public AudioClip completionSfx;
        [InspectorName("完成音量"), Range(0, 1)] public float completionVolume = .6f;
        [InspectorName("画符完成（本次画板）")] public UnityEvent<GlyphBoard> onCompleted = new();

        public string Title => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string SymbolKey => string.IsNullOrWhiteSpace(symbolId) ? name : symbolId;

        internal void RaiseCompleted(GlyphBoard board)
        {
            var at = board.transform;
            var scene = board.gameObject.scene;
            if (completionVfx)
            {
                var particles = Instantiate(completionVfx, at.position, at.rotation, new InstantiateParameters { scene = scene });
                var main = particles.main;
                main.loop = false;
                main.stopAction = ParticleSystemStopAction.Destroy;
                particles.Play(true);
            }
            if (completionSfx && completionVolume > 0f)
            {
                var voice = new GameObject(Title + "·成符音");
                SceneManager.MoveGameObjectToScene(voice, scene);
                voice.transform.position = at.position;
                var audio = voice.AddComponent<AudioSource>();
                audio.spatialBlend = 1f;
                audio.dopplerLevel = 0f;
                audio.clip = completionSfx;
                audio.volume = completionVolume;
                audio.Play();
                Destroy(voice, completionSfx.length * Mathf.Max(Time.timeScale, .01f) + .1f);
            }
            onCompleted?.Invoke(board);
        }

        public void SetShape(params Vector2[][] strokes)
        {
            if (strokes == null) { examples = Array.Empty<Example>(); return; }
            var shape = new Example { strokes = new Stroke[strokes.Length] };
            for (int i = 0; i < strokes.Length; i++)
                shape.strokes[i] = new Stroke { points = strokes[i] == null ? Array.Empty<Vector2>() : (Vector2[])strokes[i].Clone() };
            examples = new[] { shape };
        }

        public Vector2[][] Preview()
        {
            if (examples != null)
                foreach (var example in examples)
                {
                    var lines = Points(example);
                    if (lines != null) return lines;
                }
            return Array.Empty<Vector2[]>();
        }

        public float Score(IReadOnlyList<Vector2[]> ink)
        {
            float best = 0;
            if (examples != null)
                foreach (var example in examples)
                {
                    var lines = Points(example);
                    if (lines != null) best = Mathf.Max(best, GlyphRecognizer.Similarity(ink, lines));
                }
            return best;
        }

        static Vector2[][] Points(Example sample)
        {
            if (sample?.strokes == null || sample.strokes.Length == 0) return null;
            var lines = new Vector2[sample.strokes.Length][];
            for (int i = 0; i < lines.Length; i++)
            {
                var points = sample.strokes[i]?.points;
                if (points == null || points.Length == 0) return null;
                foreach (var point in points)
                    if (!float.IsFinite(point.x) || !float.IsFinite(point.y)) return null;
                lines[i] = points;
            }
            return lines;
        }
    }
}
