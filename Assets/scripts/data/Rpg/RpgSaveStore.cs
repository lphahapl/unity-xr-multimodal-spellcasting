using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    /// <summary>Explicit-path, versioned JSON store. Call on one thread; multiple writers to the same path are not supported.</summary>
    public sealed class RpgSaveStore
    {
        public const int CurrentVersion = 1;
        const int MaximumFileBytes = 1024 * 1024;
        readonly string path, profileId;
        readonly HashSet<string> knownSkills, knownCooldownGroups;
        readonly float maxHealth, maxMana;
        [Serializable] sealed class VersionHeader { public int version = 0; }

        public RpgSaveStore(string path, string profileId, IEnumerable<string> knownSkills,
            float maxHealth = 100, float maxMana = 100, IEnumerable<string> knownCooldownGroups = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("必须明确指定存档文件路径。", nameof(path));
            if (!RpgDataRules.ValidId(profileId)) throw new ArgumentException("档案ID无效。", nameof(profileId));
            if (knownSkills == null) throw new ArgumentNullException(nameof(knownSkills));
            if (!RpgDataRules.InRange(maxHealth, 1, RpgDataRules.MaximumStat) || !RpgDataRules.InRange(maxMana, 1, RpgDataRules.MaximumStat))
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            this.path = Path.GetFullPath(path); this.profileId = profileId;
            this.knownSkills = new HashSet<string>(knownSkills, StringComparer.Ordinal);
            this.knownCooldownGroups = knownCooldownGroups == null
                ? new HashSet<string>(this.knownSkills, StringComparer.Ordinal)
                : new HashSet<string>(knownCooldownGroups, StringComparer.Ordinal);
            foreach (string id in this.knownSkills) if (!RpgDataRules.ValidId(id)) throw new ArgumentException("技能白名单包含无效ID。");
            foreach (string id in this.knownCooldownGroups) if (!RpgDataRules.ValidId(id)) throw new ArgumentException("冷却组白名单包含无效ID。");
            this.maxHealth = maxHealth; this.maxMana = maxMana;
        }

        public bool TryLoad(out RpgProgressData progress, out string reason)
        {
            progress = null;
            if (Read(path, out var primary, out bool future, out string primaryReason))
            { progress = primary.progress; reason = null; return true; }
            if (future) { reason = primaryReason; return false; }
            if (Read(path + ".bak", out var backup, out bool backupFuture, out string backupReason))
            { progress = backup.progress; reason = "主存档不可用，已从备份恢复：" + primaryReason; return true; }
            reason = backupFuture ? backupReason : "存档与备份均不可用。主文件：" + primaryReason + "；备份：" + backupReason;
            return false;
        }

        public bool TrySave(RpgProgressData progress, out string reason)
        {
            string temporary = path + ".tmp";
            try
            {
                // Refuse all writes while either persisted copy belongs to a newer format.
                bool primaryValid = Read(path, out var existing, out bool future, out reason);
                if (future || DifferentProfile(existing)) return false;
                Read(path + ".bak", out var existingBackup, out future, out string backupReason);
                if (future || DifferentProfile(existingBackup)) { reason = backupReason; return false; }
                if (!Validate(progress, out reason)) return false;
                var envelope = new SaveEnvelope { profileId = profileId, progress = progress };
                string json = JsonUtility.ToJson(envelope, true);
                if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes) { reason = "存档超过大小上限"; return false; }
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (!Read(temporary, out _, out _, out reason) || File.ReadAllText(temporary, Encoding.UTF8) != json)
                { reason = "临时存档复读校验失败：" + reason; return false; }
                if (File.Exists(path))
                {
                    // File.Replace is atomic on the target filesystem. If unsupported, fail without deleting the old file.
                    // A corrupt primary must never overwrite the last known-good .bak.
                    File.Replace(temporary, path, primaryValid ? path + ".bak" : null);
                }
                else File.Move(temporary, path);
                reason = null;
                return true;
            }
            catch (Exception ex)
            { reason = "保存失败，保留原存档与备份：" + ex.GetType().Name + "：" + ex.Message; return false; }
        }

        public bool Validate(RpgProgressData progress, out string reason)
        {
            reason = "进度字段缺失或超出合法范围";
            if (progress == null || progress.currency < 0 || progress.currency > RpgDataRules.MaximumCurrency ||
                !ValidIds(progress.unlockedSkillIds, knownSkills) || !ValidIds(progress.completedStageIds, null) ||
                !ValidIds(progress.claimedRewardIds, null) ||
                (!string.IsNullOrEmpty(progress.checkpointId) && !RpgDataRules.ValidId(progress.checkpointId)) ||
                progress.skillLevels == null || progress.skillLevels.Count > RpgDataRules.MaximumEntries) return false;
            var levels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in progress.skillLevels)
                if (item == null || !RpgDataRules.ValidId(item.skillId) || !knownSkills.Contains(item.skillId) ||
                    !progress.unlockedSkillIds.Contains(item.skillId) || !levels.Add(item.skillId) ||
                    item.level < 0 || item.level > RpgDataRules.MaximumSkillLevel) return false;
            if (!RpgDataRules.ValidateCheckpoint(progress.checkpoint, maxHealth, maxMana, out reason)) return false;
            foreach (var cd in progress.checkpoint.cooldowns)
                if (!knownCooldownGroups.Contains(cd.group)) { reason = "存档包含未知冷却组：" + cd.group; return false; }
            reason = null; return true;
        }

        bool ValidIds(List<string> ids, HashSet<string> whitelist)
        {
            if (ids == null || ids.Count > RpgDataRules.MaximumEntries) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (!RpgDataRules.ValidId(id) || !unique.Add(id) || (whitelist != null && !whitelist.Contains(id))) return false;
            return true;
        }

        bool DifferentProfile(SaveEnvelope envelope) => envelope != null &&
            RpgDataRules.ValidId(envelope.profileId) && envelope.profileId != profileId;

        bool Read(string file, out SaveEnvelope envelope, out bool future, out string reason)
        {
            envelope = null; future = false;
            try
            {
                if (!File.Exists(file)) { reason = "文件不存在"; return false; }
                if (new FileInfo(file).Length > MaximumFileBytes) { reason = "文件超过大小上限"; return false; }
                string json = File.ReadAllText(file, Encoding.UTF8);
                // A separate header with no initializer ensures a missing version cannot inherit the DTO's v1 default.
                var header = JsonUtility.FromJson<VersionHeader>(json);
                if (header == null) { reason = "缺少存档版本"; return false; }
                if (header.version > CurrentVersion)
                { future = true; reason = "存档版本较新（" + header.version + "），请升级游戏；禁止覆盖"; return false; }
                if (header.version != CurrentVersion) { reason = "不支持的存档版本：" + header.version; return false; }
                envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope == null) { reason = "JSON内容为空或无效"; return false; }
                if (envelope.profileId != profileId) { reason = "存档档案ID不匹配"; return false; }
                return Validate(envelope.progress, out reason);
            }
            catch (Exception ex)
            { reason = "读取或校验失败：" + ex.GetType().Name; return false; }
        }
    }
}
