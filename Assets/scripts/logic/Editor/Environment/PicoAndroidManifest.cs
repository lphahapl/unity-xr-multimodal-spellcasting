#if UNITY_ANDROID
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using Unity.XR.OpenXR.Features.PICOSupport;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine.XR.OpenXR;

namespace NarutoVR.Editor
{
    internal sealed class PicoAndroidManifest : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string HorizonNamespace = "http://schemas.horizonos/sdk";
        private const string ToolsNamespace = "http://schemas.android.com/tools";

        // Both vendor SDKs modify the generated project. This runs after their callbacks.
        public int callbackOrder => int.MaxValue;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings?.GetFeature<PICOFeature>()?.enabled != true)
                return;

            RemoveQuestDeclarations(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
            RemoveQuestDeclarations(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"),
                removeMergedQuestFeature: true);
            RemoveDuplicateOpenXRLoader(path);
        }

        private static void RemoveQuestDeclarations(string path, bool removeMergedQuestFeature = false)
        {
            if (!File.Exists(path))
                return;

            var manifest = new XmlDocument { PreserveWhitespace = true };
            manifest.Load(path);
            var questElements = manifest.GetElementsByTagName("*").OfType<XmlElement>()
                .Where(IsQuestDeclaration).ToArray();

            foreach (var element in questElements)
                element.ParentNode.RemoveChild(element);

            if (removeMergedQuestFeature)
            {
                // The Meta API layer's AAR declares this feature after Unity's manifest callbacks.
                var feature = manifest.CreateElement("uses-feature");
                var name = manifest.CreateAttribute("android", "name", AndroidNamespace);
                name.Value = "com.oculus.experimental.enabled";
                feature.Attributes.Append(name);
                var remove = manifest.CreateAttribute("tools", "node", ToolsNamespace);
                remove.Value = "remove";
                feature.Attributes.Append(remove);
                manifest.DocumentElement.AppendChild(feature);
            }

            if (questElements.Length > 0 || removeMergedQuestFeature)
                manifest.Save(path);
        }

        private static void RemoveDuplicateOpenXRLoader(string unityLibrary)
        {
            var pluginPath = Path.Combine(unityLibrary, "libs", "OVRPlugin.aar");
            if (!File.Exists(pluginPath))
                return;

            var temporaryPath = pluginPath + ".pico.tmp";
            try
            {
                using (var plugin = new ZipArchive(File.OpenRead(pluginPath), ZipArchiveMode.Read))
                {
                    var metaLoader = plugin.GetEntry("jni/arm64-v8a/libopenxr_loader.so");
                    if (metaLoader == null)
                        return;

                    var nativeLibraries = Path.Combine(unityLibrary, "src", "main", "jniLibs", "arm64-v8a");
                    if (!File.Exists(Path.Combine(nativeLibraries, "libopenxr_pico.so")) ||
                        !File.Exists(Path.Combine(nativeLibraries, "libopenxr_loader.so")))
                        throw new BuildFailedException("PICO OpenXR libraries were not exported to Android Gradle.");

                    // Rebuild Gradle's copy so archive entry sizes and CRCs are compatible with Jetifier.
                    using var filtered = new ZipArchive(File.Create(temporaryPath), ZipArchiveMode.Create);
                    foreach (var entry in plugin.Entries)
                    {
                        if (entry == metaLoader || entry.Name.Length == 0)
                            continue;

                        using var source = entry.Open();
                        using var destination = filtered.CreateEntry(entry.FullName).Open();
                        source.CopyTo(destination);
                    }
                }

                File.Replace(temporaryPath, pluginPath, null);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        private static bool IsQuestDeclaration(XmlElement element)
        {
            var name = element.GetAttribute("name", AndroidNamespace);
            return element.NamespaceURI == HorizonNamespace ||
                   name.StartsWith("com.oculus.", StringComparison.Ordinal) ||
                   name.StartsWith("oculus.software.", StringComparison.Ordinal);
        }
    }
}
#endif
