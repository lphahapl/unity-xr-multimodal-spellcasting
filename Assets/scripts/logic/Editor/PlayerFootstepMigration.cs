using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NarutoVR.Audio;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>在旧场景首次重存前，把原来由音乐组件保存的脚步设置迁给玩家。</summary>
    public static class PlayerFootstepMigration
    {
        [MenuItem("CONTEXT/LinearRpgController/迁移旧场景玩家脚步")]
        static void MigrateSelection(MenuCommand command)
        {
            var player = (LinearRpgController)command.context;
            if (Migrate(player)) Debug.Log("已迁移玩家脚步，保存当前场景后生效。", player);
        }

        public static bool Migrate(LinearRpgController player)
        {
            if (!player.chapter || !player.chapter.UsesExplorationFootsteps || !player.locomotion) return false;
            var actor = player.locomotion.gameObject;
            var steps = actor.GetComponent<FootstepPlayer>();
            if (steps && steps.HasClips) return false;
            var mixer = player.GetComponentInChildren<P1AudioManager>(true);
            string path = player.gameObject.scene.path;
            if (!mixer || !File.Exists(path)) return false;

            string yaml = File.ReadAllText(path).Replace("\r\n", "\n");
            var localId = GlobalObjectId.GetGlobalObjectIdSlow(mixer).targetObjectId;
            var block = Regex.Match(yaml, @"(?ms)^--- !u!114 &" + localId + @"\n.*?(?=^--- !u!|\z)").Value;
            var list = Regex.Match(block, @"(?m)^  footstepClips: *(?:\[\])?$\n((?:  - [^\n]*\n)*)");
            if (!list.Success) return false;
            var clips = Regex.Matches(list.Groups[1].Value, @"fileID: (-?\d+), guid: ([a-z0-9]+)")
                .Cast<Match>().Select(match =>
                {
                    long id = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    string clipPath = AssetDatabase.GUIDToAssetPath(match.Groups[2].Value);
                    return AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AudioClip>().FirstOrDefault(clip =>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out _, out long local) && local == id);
                }).ToArray();
            if (clips.Any(clip => !clip)) throw new InvalidDataException("旧脚步引用缺失，请先恢复原音频。");
            if (clips.Length == 0)
                clips = AssetDatabase.FindAssets("t:AudioClip", new[]{"Assets/Resources/Audio/Footsteps"})
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal)
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
            if (clips.Length == 0) throw new InvalidDataException("缺少原默认脚步资源。");
            float ReadNumber(string key, float fallback)
            {
                var value = Regex.Match(block, @"(?m)^  " + key + @": (.+)$");
                return value.Success ? float.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture) : fallback;
            }
            float volume = ReadNumber("footstepVolume", .22f), spacing = ReadNumber("footstepSpacing", .55f);
            if (!steps) steps = Undo.AddComponent<FootstepPlayer>(actor);
            Undo.RecordObject(steps, "迁移玩家脚步");
            steps.clip = clips[0];
            steps.variations = clips.Skip(1).ToArray();
            steps.volume = volume;
            steps.stepDistance = spacing;
            EditorUtility.SetDirty(steps);
            EditorSceneManager.MarkSceneDirty(actor.scene);
            return true;
        }
    }
}
