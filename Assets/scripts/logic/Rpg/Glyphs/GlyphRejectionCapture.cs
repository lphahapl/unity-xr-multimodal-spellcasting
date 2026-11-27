#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    // 编辑器本地诊断。仅由停笔完成一次拒识的分支调用，玩家后续补笔不会改写已存样本。
    internal static class GlyphRejectionCapture
    {
        [Serializable] internal struct Point
        {
            public Vector3 boardPosition;
            public double time;
            public int strokeIndex;
        }

        [Serializable] internal struct Candidate
        {
            public string symbolId, name;
            public bool scored;
            public float score, threshold;
        }

        // Raw signed depth is measured before the pen visual snaps to the board (negative faces player).
        [Serializable] internal sealed class ContactLog
        {
            public int contacts, frontLifts, sideExits, trackingBreaks;
            public float latestRawZ;
            public double lastContactTime, lastReleaseTime;
            [NonSerialized] bool down;

            public void Observe(float z, bool inside, bool touching, double now)
            {
                latestRawZ = z;
                if (touching == down) return;
                down = touching;
                if (touching) { contacts++; lastContactTime = now; }
                else
                {
                    if (inside) frontLifts++; else sideExits++;
                    lastReleaseTime = now;
                }
            }

            public void LostTracking(double now)
            {
                trackingBreaks++;
                if (down) lastReleaseTime = now;
                down = false;
            }

            public void Clear()
            {
                contacts = frontLifts = sideExits = trackingBreaks = 0;
                latestRawZ = 0;
                lastContactTime = lastReleaseTime = 0;
                down = false;
            }
        }

        [Serializable] internal sealed class Snapshot
        {
            public string capturedUtc, scene, board, mode, reason;
            public double decisionTime;
            public float lastScore, minimumMargin, idleSeconds;
            public int strokeCount;
            public ContactLog contact;
            public Point[] points;
            public Candidate[] candidates;
        }

        // 自动化使用单独目录，以免合成输入覆盖实际玩家的最后一次拒识。
        internal static string OutputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/GlyphRecognition"));

        internal static void Write(GlyphBoard board, bool story, double now, GlyphTemplate[] templates, float[] scores)
        {
            try
            {
                var points = new Point[board.PointCount];
                for (int i = 0; i < points.Length; ++i)
                {
                    var sample = board.Samples[i];
                    points[i] = new Point { boardPosition = sample.boardPosition, time = sample.time, strokeIndex = sample.strokeIndex };
                }
                var candidates = new List<Candidate>(templates.Length);
                for (int i = 0; i < templates.Length; ++i)
                {
                    var candidate = templates[i];
                    if (candidate) candidates.Add(new Candidate {
                        symbolId = candidate.SymbolKey, name = candidate.Title, threshold = candidate.threshold,
                        scored = scores != null, score = scores == null ? 0 : scores[i]
                    });
                }
                string mode = story ? "story" : "active";
                var record = new Snapshot {
                    capturedUtc = DateTime.UtcNow.ToString("o"), scene = board.gameObject.scene.path, board = board.name,
                    mode = mode, reason = board.Status, decisionTime = now, lastScore = board.LastScore,
                    minimumMargin = board.minimumMargin, idleSeconds = board.idleSeconds, strokeCount = board.StrokeCount,
                    contact = board.contactLog, points = points, candidates = candidates.ToArray()
                };
                Directory.CreateDirectory(OutputDirectory);
                string path = Path.Combine(OutputDirectory, "last-" + mode + ".json");
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(record, true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("画符拒识记录写入失败：" + e.Message, board);
            }
        }
    }
}
#endif
