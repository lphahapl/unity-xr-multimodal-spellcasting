#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>仅移动配置文件，保持 GUID 和文件内容；不进行数值或旧格式转换。</summary>
    public static class ConfigurationAssetOrganizer
    {
        const string Root = LinearRpgSceneSetup.ConfigurationRoot;
        const string Log = "Logs/configuration-by-class-20261104";
        [Serializable] public sealed class Entry { public string from, to, type, guid, assetHash, metaHash; }
        [Serializable] public sealed class Report { public string time; public bool passed; public List<Entry> files = new(); }
        static string Hash(string path)
        {
            using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }
        [MenuItem("Tools/NarutoVR/配置整理/按实际类整理 LinearRpg 配置")]
        public static void Menu() => Debug.Log(Run());
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。");
            Directory.CreateDirectory(Log);
            var report = new Report { time = DateTimeOffset.Now.ToString("O") };
            foreach (var path in Directory.GetFiles(Root).Where(p => p.EndsWith(".asset") || p.EndsWith(".mat")).Select(p => p.Replace('\\', '/')))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset) throw new Exception("无法识别资产：" + path);
                string type = asset.GetType().Name, folder = Root + "/" + type, destination = folder + "/" + Path.GetFileName(path);
                if (File.Exists(destination)) throw new Exception("目标已存在，未覆盖：" + destination);
                report.files.Add(new Entry { from = path, to = destination, type = type,
                    guid = AssetDatabase.AssetPathToGUID(path), assetHash = Hash(path), metaHash = Hash(path + ".meta") });
            }
            if (report.files.Count == 0) return Validate();
            // Prepare the complete mapping and copies before the first mutation.
            foreach (var item in report.files)
            {
                File.Copy(item.from, Log + "/" + Path.GetFileName(item.from), false);
                File.Copy(item.from + ".meta", Log + "/" + Path.GetFileName(item.from) + ".meta", false);
            }
            File.WriteAllText(Log + "/mapping.json", JsonUtility.ToJson(report, true));
            var moved = new List<Entry>();
            try
            {
                foreach (var item in report.files)
                {
                    string folder = Root + "/" + item.type;
                    if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, item.type);
                    string error = AssetDatabase.MoveAsset(item.from, item.to);
                    if (!string.IsNullOrEmpty(error)) throw new Exception(error);
                    moved.Add(item);
                    if (AssetDatabase.AssetPathToGUID(item.to) != item.guid || Hash(item.to) != item.assetHash || Hash(item.to + ".meta") != item.metaHash)
                        throw new Exception("移动后内容或GUID发生变化：" + item.to);
                }
                string detail = Validate(); report.passed = true;
                File.WriteAllText(Log + "/mapping.json", JsonUtility.ToJson(report, true));
                return detail;
            }
            catch
            {
                foreach (var item in moved.AsEnumerable().Reverse())
                {
                    string error = AssetDatabase.MoveAsset(item.to, item.from);
                    if (!string.IsNullOrEmpty(error)) Debug.LogError("恢复移动失败：" + error);
                }
                throw;
            }
        }
        public static string Validate()
        {
            int count = 0;
            foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(p => p.EndsWith(".asset") || p.EndsWith(".mat")))
            {
                string path = file.Replace('\\', '/'); var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset || Path.GetFileName(Path.GetDirectoryName(path)) != asset.GetType().Name) throw new Exception("类型目录不匹配：" + path);
                count++;
            }
            if (count == 0 || !File.Exists(LinearRpgSceneSetup.VoiceConfigurationPath)) throw new Exception("配置或语音导出缺失");
            var fire = AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>(Root + "/ProjectileEffectDefinition/效果_fireball.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(Root + "/SkillCatalog/线性RPG技能目录.asset");
            if (!fire || !catalog || catalog.Validate().Count != 0 || !AssetDatabase.LoadAssetAtPath<BasicHandPoseSettings>(OfficialHandPoseSetup.ProfilePath)) throw new Exception("技能或基础手形接线失败");
            string detail = $"{count} 个配置按实际类分类；GUID及文件内容不变；技能目录和效果接线有效；语音JSON路径保留。";
            File.WriteAllText(Log + "/check.txt", DateTimeOffset.Now.ToString("O") + "\n" + detail); return detail;
        }
    }
}
#endif
