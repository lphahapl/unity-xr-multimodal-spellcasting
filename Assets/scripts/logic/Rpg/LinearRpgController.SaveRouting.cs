using System;
using System.IO;
using System.Linq;
using NarutoVR.SceneArt;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Rpg
{
    public enum CampaignSaveSource
    {
        [InspectorName("当前场景的存档")] CurrentScene,
        [InspectorName("最近的正式章节存档")] LatestCampaign,
        [InspectorName("第一章存档")] ChapterOne,
        [InspectorName("第二章存档")] ChapterTwo
    }

    public sealed partial class LinearRpgController
    {
        const string AwakeningScene = "Assets/Scene/Chapter01_Awakening.unity";
        const string CourtyardScene = "Assets/Scene/Chapter01_Courtyard.unity";
        const string SuspendedScene = "Assets/Scene/Chapter02_SuspendedPath.unity";
        const string FirstSlot = "kaogong-chapter01-v2.json";
        const string SecondSlot = "chapter02-suspended-path.json";

        sealed class SaveLocation
        {
            public RpgProgressData snapshot;
            public string scene, slot, diagnostic;
            public long time;
        }

        static SaveLocation inTransit;
        static int originHandle;
        SaveLocation preparedSave;
        string startupSaveError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSaveTravel()
        {
            inTransit = null;
            SceneManager.sceneLoaded -= ClearUnclaimedSave;
            SceneManager.sceneLoaded += ClearUnclaimedSave;
        }

        static void ClearUnclaimedSave(Scene scene, LoadSceneMode mode)
        {
            if (inTransit == null || mode != LoadSceneMode.Single || scene.handle == originHandle) return;
            Debug.LogError("场景已加载，但没有匹配的玩家接收存档：" + inTransit.scene);
            inTransit = null;
        }

        static string SlotForScene(string path) => path switch
        {
            AwakeningScene or CourtyardScene => FirstSlot,
            SuspendedScene => SecondSlot,
            _ => null
        };

        bool OwnsCampaignSlot => SlotForScene(gameObject.scene.path) == saveFileName && !string.IsNullOrEmpty(saveFileName);
        bool AcceptsLocalSaveLocation(RpgProgressData progress) => !OwnsCampaignSlot || AcceptsSlot(progress, saveFileName);
        bool AcceptsSlot(RpgProgressData progress, string slot) => OwnerOf(progress, slot) != null &&
            progress.savedUtcTicks >= 0 && progress.savedUtcTicks <= DateTime.MaxValue.Ticks;

        void StampSaveScene(RpgProgressData snapshot)
        {
            if (!OwnsCampaignSlot) return;
            snapshot.scenePath = gameObject.scene.path;
            snapshot.savedUtcTicks = DateTime.UtcNow.Ticks;
        }

        // Both first-chapter scenes used the same v2 file, stages and checkpoint IDs. An old
        // first-chapter save can be restored in place, or in Awakening when arriving from outside.
        string OwnerOf(RpgProgressData snapshot, string slot)
        {
            if (!string.IsNullOrEmpty(snapshot.scenePath))
                return SlotForScene(snapshot.scenePath) == slot ? snapshot.scenePath : null;
            if (slot == saveFileName && OwnsCampaignSlot)
                return FindCheckpoint(snapshot.checkpointId) ? gameObject.scene.path : null;
            if (slot == SecondSlot)
                return snapshot.checkpointId is "CH02_ENTRANCE" or "CH02_BRIDGE_DRAW" or "CH02_A_MIDDLE"
                    or "CH02_HUB" or "CH02_B_VALLEY" ? SuspendedScene : null;
            if (snapshot.checkpointId is not ("courtyard.0.0" or "approach.0.12" or "room.2.5" or
                "opening.outside" or "opening.court" or "opening.pass" or "opening.end")) return null;
            return SlotForScene(gameObject.scene.path) == FirstSlot ? gameObject.scene.path : AwakeningScene;
        }

        RpgSaveStore LocalSlotReader(string path)
        {
            var ids = skillResources ? skillResources.entries.Select(e => e.id) : skills.catalog.skills.Select(s => s.skillId);
            var groups = skillResources ? skillResources.entries.Select(e => e.cooldownGroup) : skills.catalog.skills.Select(CooldownGroup);
            return new RpgSaveStore(UnityRpgSaveJson.Instance, path, "linear-rpg-player", ids,
                playerDefinition ? playerDefinition.maxHealth : 100, playerDefinition ? playerDefinition.maxMana : 100,
                groups, AcceptsLocalSaveLocation);
        }

        SaveLocation ReadSlot(string slot, out string error)
        {
            error = null;
            var path = Path.Combine(Application.persistentDataPath, slot);
            try
            {
                if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
                var reader = OwnsCampaignSlot && slot == saveFileName ? LocalSlotReader(path) :
                    RpgSaveStore.InspectLocation(UnityRpgSaveJson.Instance, path, "linear-rpg-player", p => AcceptsSlot(p, slot));
                if (!reader.TryLoad(out var snapshot, out var diagnostic, out var copy))
                { error = diagnostic; return null; }
                return new SaveLocation
                {
                    scene = OwnerOf(snapshot, slot), slot = slot, snapshot = snapshot, diagnostic = diagnostic,
                    time = snapshot.savedUtcTicks > 0 ? snapshot.savedUtcTicks : File.GetLastWriteTimeUtc(copy).Ticks
                };
            }
            catch (Exception ex) { error = "检查存档失败：" + ex.Message; return null; }
        }

        SaveLocation SelectSave(CampaignSaveSource source, out string error)
        {
            if (source == CampaignSaveSource.CurrentScene) return ReadSlot(saveFileName, out error);
            if (source == CampaignSaveSource.ChapterOne) return ReadSlot(FirstSlot, out error);
            if (source == CampaignSaveSource.ChapterTwo) return ReadSlot(SecondSlot, out error);
            var first = ReadSlot(FirstSlot, out var firstError);
            var second = ReadSlot(SecondSlot, out var secondError);
            error = firstError ?? secondError;
            if (first == null) return second;
            if (second == null) return first;
            return second.time > first.time || (second.time == first.time && saveFileName == SecondSlot) ? second : first;
        }

        bool TravelToSave(SaveLocation target)
        {
            if (inTransit != null) return Reject("正在切换至存档所属场景");
            originHandle = gameObject.scene.handle;
            inTransit = target;
            var result = AsyncSceneLoader.TryLoad(target.scene);
            if (result != AsyncSceneLoader.Result.Started)
            {
                inTransit = null;
                return Reject("未能进入存档所属场景：" + target.scene + "（" + result + "）");
            }
            Status = "正在返回存档所属场景：" + target.scene;
            enabled = false;
            return true;
        }

        /// <summary>直接进场时选择存档。从其他场景发起的读档优先消费那份快照，且只消费一次。</summary>
        bool PrepareSaveEntry(CampaignSaveSource source, bool requestLoad, out bool readLocal)
        {
            readLocal = requestLoad;
            if (!OwnsCampaignSlot) return true;
            if (inTransit != null && inTransit.scene == gameObject.scene.path)
            {
                var incoming = inTransit;
                inTransit = null;
                readLocal = true;
                var local = LocalSlotReader(Path.Combine(Application.persistentDataPath, saveFileName));
                if (local.ValidateLoadedProgress(incoming.snapshot, out var reason))
                { preparedSave = incoming; return true; }

                // The preliminary header check could not use the destination catalogue. Give its
                // normal backup policy a chance, including a backup recorded in the other subscene.
                preparedSave = ReadSlot(saveFileName, out startupSaveError);
                if (preparedSave == null) startupSaveError ??= "目标章节不接受所选存档：" + reason;
            }
            else
            {
                if (!requestLoad) return true;
                preparedSave = SelectSave(source, out startupSaveError);
                if (preparedSave != null) startupSaveError = null;
                // Missing an explicitly selected slot starts this scene with its authored defaults.
                readLocal = preparedSave != null || startupSaveError != null;
            }
            if (preparedSave == null || preparedSave.scene == gameObject.scene.path) return true;
            if (TravelToSave(preparedSave)) return false;
            startupSaveError = Status;
            preparedSave = null;
            return true;
        }

        (RpgProgressData progress, string diagnostic) ReadPreparedSave(bool readLocal)
        {
            if (startupSaveError != null) return (null, startupSaveError);
            // Async skill assembly may cancel after reading and retry. Keep this instance's selection
            // stable across retries instead of reopening a possibly changed shared first-chapter file.
            return preparedSave != null ? (preparedSave.snapshot, preparedSave.diagnostic) : checkpoints.ReadStartup(readLocal);
        }

        void ReleasePreparedSave() => preparedSave = null;

        bool TryRouteForeignCheckpoint(RpgProgressData saved, out bool scheduled)
        {
            scheduled = false;
            if (!OwnsCampaignSlot || saved == null || string.IsNullOrEmpty(saved.scenePath) ||
                saved.scenePath == gameObject.scene.path) return false;
            if (OwnerOf(saved, saveFileName) == null) { Reject("存档的所属场景与当前章节文件不符"); return true; }
            scheduled = TravelToSave(new SaveLocation { snapshot = saved, scene = saved.scenePath, slot = saveFileName });
            return true;
        }

        /// <summary>按 Inspector 选定的来源读取磁盘；跨场景时目标消费同一份已选快照。</summary>
        public bool LoadSelectedSave(CampaignSaveSource source)
        {
            if (source == CampaignSaveSource.CurrentScene && !OwnsCampaignSlot) return RestoreCheckpoint();
            if (PublishingCast) { confirmedCastCommands.Enqueue(() => LoadSelectedSave(source)); return false; }
            return RunCheckpointCommand(() =>
            {
                if (paused || Time.timeScale <= 0) return Reject("暂停时不能读档");
                if (CheckpointSavePending && !PersistCheckpoint(true)) return Reject("当前检查点尚未写入，暂无法切换存档");
                var selected = SelectSave(source, out var error);
                if (selected == null) return Reject(error ?? "所选章节还没有存档");
                if (selected.scene != gameObject.scene.path) return TravelToSave(selected);
                if (saveFileName != selected.slot) return Reject("本场景的存档文件已被重定向，无法安全恢复正式存档");
                checkpointMigration?.MigrateCheckpoint(selected.snapshot);
                return ApplyCheckpoint(selected.snapshot, false);
            });
        }
    }
}
