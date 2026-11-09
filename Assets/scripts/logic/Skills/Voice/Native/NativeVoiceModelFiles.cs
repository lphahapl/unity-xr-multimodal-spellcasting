using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace NarutoVR.SealCombat
{
    /// <summary>将APK内只读模型校验并复制到应用私有缓存；首次启动不访问网络。</summary>
    public static class NativeVoiceModelFiles
    {
        [Serializable] public sealed class Entry { public string name, sha256; public long bytes; }
        [Serializable] public sealed class Manifest
        {
            public int schema;
            public string version, encoder, decoder, joiner, tokens;
            public Entry[] files;
        }
        public static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
        public static void ValidateManifest(Manifest manifest)
        {
            if (manifest == null || manifest.schema != 1 || !Regex.IsMatch(manifest.version ?? "", @"\A[a-zA-Z0-9_.-]{1,100}\z") ||
                manifest.version == "." || manifest.version.Contains("..") ||
                manifest.files == null || manifest.files.Length != 4) throw new InvalidDataException("原生语音模型清单无效");
            string[] required = { manifest.encoder, manifest.decoder, manifest.joiner, manifest.tokens };
            if (required.Any(string.IsNullOrEmpty) || required.Distinct().Count() != 4) throw new InvalidDataException("模型文件名重复或缺失");
            foreach (var entry in manifest.files)
                if (entry == null || !Regex.IsMatch(entry.name ?? "", @"\A[a-zA-Z0-9_.-]+\z") || entry.name.Contains("..") ||
                    !Regex.IsMatch(entry.sha256 ?? "", @"\A[0-9a-f]{64}\z") || entry.bytes <= 0 || entry.bytes > 32 * 1024 * 1024 ||
                    !required.Contains(entry.name)) throw new InvalidDataException("模型条目无效");
            if (manifest.files.Select(f => f.name).Distinct().Count() != 4) throw new InvalidDataException("模型条目重复");
        }
        static IEnumerator Read(string name, Action<byte[]> done, Action<string> failed)
        {
            string path = Application.streamingAssetsPath.TrimEnd('/') + "/SkillVoice/" + name;
            string url = path.Contains("://") || path.StartsWith("jar:", StringComparison.Ordinal) ? path : new Uri(path).AbsoluteUri;
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 60;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) failed("读取随包语音文件失败：" + name + "；" + request.error);
                else done(request.downloadHandler.data);
            }
        }
        public static IEnumerator Prepare(Action<string> success, Action<string> failure)
        {
            byte[] data = null;
            string error = null;
            yield return Read("model-manifest.json", value => data = value, value => error = value);
            if (error != null) { failure(error); yield break; }
            Manifest manifest = null;
            string directory = null;
            try
            {
                manifest = JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(data));
                ValidateManifest(manifest);
                directory = Path.Combine(Application.persistentDataPath, "SkillVoice", manifest.version);
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex) { error = ex.Message; }
            if (error != null) { failure(error); yield break; }
            foreach (var entry in manifest.files)
            {
                string target = Path.Combine(directory, entry.name);
                bool valid = false;
                try { valid = File.Exists(target) && new FileInfo(target).Length == entry.bytes && Hash(File.ReadAllBytes(target)) == entry.sha256; }
                catch (IOException) { }
                if (!valid)
                {
                    data = null;
                    yield return Read(entry.name, value => data = value, value => error = value);
                    if (error != null) { failure(error); yield break; }
                    try
                    {
                        if (data.LongLength != entry.bytes || Hash(data) != entry.sha256) throw new InvalidDataException("语音模型校验失败：" + entry.name);
                        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            File.WriteAllBytes(temporary, data);
                            if (File.Exists(target)) File.Delete(target);
                            File.Move(temporary, target);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                    catch (Exception ex) { error = ex.Message; }
                    if (error != null) { failure(error); yield break; }
                }
                yield return null;
            }
            success(directory);
        }
    }
}
