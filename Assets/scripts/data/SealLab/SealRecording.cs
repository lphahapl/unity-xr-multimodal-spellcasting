using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealLab
{
    // Streaming JSONL keeps a long recording from accumulating in RAM.
    public sealed class SealRecording : IDisposable
    {
        private StreamWriter writer;
        private int pending;
        public string Path { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsOpen => writer != null;
        public void Start(string directory)
        {
            Dispose(); Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, $"hands-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl");
            writer = new StreamWriter(new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            FrameCount = pending = 0;
        }
        public void Write(SealFrame frame)
        {
            if (writer == null) return;
            writer.WriteLine(JsonUtility.ToJson(frame)); ++FrameCount;
            if (++pending >= 30) { writer.Flush(); pending = 0; }
        }
        public void Dispose() { writer?.Dispose(); writer = null; }

        public static List<SealFrame> Read(string path, int maximumFrames = 20000)
        {
            var frames = new List<SealFrame>(); double previous = double.NegativeInfinity;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (frames.Count >= maximumFrames) throw new InvalidDataException("录制超过回放帧数上限，请分段录制。");
                SealFrame frame;
                try { frame = JsonUtility.FromJson<SealFrame>(line); }
                catch (Exception ex) { throw new InvalidDataException($"第 {frames.Count} 帧的数据格式无效。", ex); }
                if (frame == null || frame.schema != 1 || !double.IsFinite(frame.time) || frame.time < previous ||
                    !ValidHand(frame.left) || !ValidHand(frame.right)) throw new InvalidDataException($"第 {frames.Count} 帧的骨骼快照无效或格式不兼容。");
                previous = frame.time; frames.Add(frame);
            }
            if (frames.Count == 0) throw new InvalidDataException("录制文件为空。");
            return frames;
        }
        private static bool ValidHand(HandSnapshot hand)
        {
            int count = Oculus.Interaction.Input.Constants.NUM_HAND_JOINTS;
            if (hand == null || !double.IsFinite(hand.observedAt) || !float.IsFinite(hand.scale) || hand.scale <= 0 ||
                hand.joints?.Length != count || hand.jointValid?.Length != count ||
                hand.wristPoses?.Length != count || hand.fingerConfidence?.Length != 5) return false;
            for (int i = 0; i < count; ++i)
                if ((hand.jointValid[i] && !SealLab.FinitePose(hand.joints[i])) ||
                    (hand.wristPosesValid && !SealLab.FinitePose(hand.wristPoses[i]))) return false;
            return true;
        }
    }
}
