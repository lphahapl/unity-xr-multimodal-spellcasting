using System.IO;
using UnityEditor;
using UnityEngine;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>只创建天空专属目录中缺失的材质；重复执行不会覆盖已经调好的材质，也不操作场景。</summary>
    public static class ThinCloudSkyAssets
    {
        public const string Root = "Assets/Art/Environment/Opening/Skybox";
        public const string TexturePath = Root + "/CirrusDensity.png";

        [MenuItem("Tools/NarutoVR/环境/创建缺失的天空材质")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("请退出 Play 后创建预览资源。");
            if (!(AssetImporter.GetAtPath(TexturePath) is TextureImporter importer))
                throw new FileNotFoundException("缺少独立云层密度图。", TexturePath);
            // 密度是线性数据。镜像平铺使 AI 图边缘连续；只在导入端压缩，不篡改源图。
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Mirror;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 1;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            { name = "Android", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 });
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            { name = "Standalone", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.BC4 });
            importer.SaveAndReimport();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(ThinCloudSkyPreview.MaterialPath);
            if (!material)
            {
                var shader = Shader.Find("NarutoVR/Environment/青灰薄云天空");
                if (!shader) throw new System.InvalidOperationException("天空 Shader 尚未成功导入。");
                material = new Material(shader) { name = "青灰薄云" };
                material.EnableKeyword("_CLOUDS_ON"); material.EnableKeyword("_SECOND_LAYER");
                AssetDatabase.CreateAsset(material, ThinCloudSkyPreview.MaterialPath);
            }
            // 修复缺失引用时不覆盖用户自选的另一张云图或已调整的材质参数。
            if (!material.GetTexture("_CloudMap"))
            {
                var density = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                if (!density) throw new System.InvalidOperationException("云密度图未导入为 Texture2D。");
                material.SetTexture("_CloudMap", density);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssetIfDirty(material);
            Debug.Log("[青灰薄云] 资源已就绪；从 环境/青灰薄云预览 打开独立窗口。正式场景未应用天空。");
        }

    }
}
