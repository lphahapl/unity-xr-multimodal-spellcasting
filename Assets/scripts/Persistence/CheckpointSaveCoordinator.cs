using System;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>持有一个玩家的最近检查点和待写快照，读盘失败的临时角色可单独禁止写盘。</summary>
    public sealed class CheckpointSaveCoordinator
    {
        const double RetryInterval = 2;
        readonly RpgSaveStore store;
        readonly IRpgSaveJson json;
        RpgProgressData pending, last;
        double nextRetry;
        bool writing;
        public bool Pending => pending != null;
        public bool WritesBlocked { get; private set; }
        public string CheckpointId => last?.checkpointId;

        public CheckpointSaveCoordinator(RpgSaveStore store, IRpgSaveJson json)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public (RpgProgressData progress, string diagnostic) ReadStartup(bool autoLoad)
        {
            if (!autoLoad || !store.HasSaveFiles) return default;
            store.TryLoad(out var progress, out var diagnostic);
            return (progress, diagnostic);
        }

        public bool Validate(RpgProgressData snapshot, out string reason) => store.Validate(snapshot, out reason);
        public void BlockWrites() => WritesBlocked = true;
        public void SetRestored(RpgProgressData restored) => last = Copy(restored);
        public RpgProgressData CaptureLatest() => Copy(pending ?? last);

        public void Queue(RpgProgressData snapshot, double now)
        {
            pending = Copy(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
            nextRetry = now + RetryInterval;
        }

        public bool RetryDue(double now) => Pending && !WritesBlocked && now >= nextRetry;
        public bool TryWrite(double now, out string reason)
        {
            if (WritesBlocked) { reason = "启动时存档验证失败，本次临时会话禁止覆盖原文件"; return false; }
            if (writing) { reason = "正在写入检查点"; return false; }
            if (!Pending) { reason = null; return true; }
            nextRetry = now + RetryInterval;
            var snapshot = pending;
            writing = true;
            try
            {
                if (!store.TrySave(snapshot, out reason)) return false;
                last = snapshot;
                if (ReferenceEquals(pending, snapshot)) pending = null;
                return true;
            }
            finally { writing = false; }
        }

        public bool TryReadForRestore(out RpgProgressData snapshot, out string reason)
        {
            if (!WritesBlocked && !Pending && store.HasSaveFiles) return store.TryLoad(out snapshot, out reason);
            snapshot = CaptureLatest();
            reason = snapshot == null ? "没有可恢复的检查点" : null;
            return snapshot != null;
        }

        RpgProgressData Copy(RpgProgressData value) => value == null ? null : json.Deserialize<RpgProgressData>(json.Serialize(value));
    }
}
