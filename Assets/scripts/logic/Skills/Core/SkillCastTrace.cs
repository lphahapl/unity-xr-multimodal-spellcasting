using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>将施法事件写入有容量上限的 JSONL 日志；记录失败时继续执行玩法。</summary>
    public sealed class SkillCastTrace : IDisposable
    {
        [Serializable] public sealed class Entry
        {
            public int schema = 1, frame;
            public long sequence, epoch, attempt;
            public double time, inputTime;
            public string utc, session, stage, skillId, detail, gates, payload;
        }
        readonly string session = Guid.NewGuid().ToString("N");
        StreamWriter writer;
        long sequence, bytes;
        double nextFlush;
        bool failed;
        public string Path { get; private set; }
        public SkillCastTrace(string directory)
        {
            try {
                Directory.CreateDirectory(directory);
                Path = System.IO.Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + session + ".jsonl");
                writer = new StreamWriter(new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false), 16384);
            } catch(Exception e) { Fail(e); }
        }
        public void Write(Entry entry)
        {
            if(failed || writer == null) return;
            try {
                entry.session=session; entry.sequence=++sequence; entry.utc=DateTime.UtcNow.ToString("O");
                string line=JsonUtility.ToJson(entry);
                bytes+=Encoding.UTF8.GetByteCount(line)+1;
                if(bytes>32*1024*1024) { Fail(new IOException("本次施法日志达到32MB上限")); return; }
                writer.WriteLine(line);
                if(entry.time>=nextFlush) { writer.Flush(); nextFlush=entry.time+1; }
            } catch(Exception e) { Fail(e); }
        }
        void Fail(Exception e)
        {
            failed=true;
            try { writer?.Dispose(); } catch { }
            writer=null;
            Debug.LogWarning("[施法追踪] 日志已停止，不影响技能："+e.Message);
        }
        public void Dispose() { try { writer?.Dispose(); } catch(Exception e) { Fail(e); } finally { writer=null; } }
    }
}
