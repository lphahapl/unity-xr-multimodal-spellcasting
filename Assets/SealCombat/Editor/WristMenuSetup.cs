#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    public static class WristMenuSetup
    {
        [MenuItem("Tools/NarutoVR/线性RPG/应用点腕开关与轻投掷配置")]
        public static void TapMenu() => Debug.Log(ConfigureTap());
        public static string ConfigureTap()
        {
            string backup=Configure();
            var scene=SceneManager.GetActiveScene();
            var menu=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WristMenuController>(true)).Single();
            menu.settings.activation=WristMenuActivation.TapWrist;
            menu.settings.pinchClose=.03f;
            var effect=AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>("Assets/SealCombat/Configuration/LinearRpg/效果_fireball.asset");
            if(!effect) throw new IOException("火球配置未找到");
            var t=effect.throwing;
            t.gripSeconds=.04f; t.releaseSeconds=.015f; t.releaseTimeout=.26f;
            t.minimumSpeed=.45f; t.minimumDistance=.035f;
            t.pinchCloseDistance=.03f; t.pinchOpenDistance=.04f; t.allowPartialFistRelease=true;
            EditorUtility.SetDirty(effect); AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("点腕配置保存失败");
            return "已启用抬左腕＋右指点腕开关，轻捏确认；降低当前RPG火球运动门槛。"+backup;
        }
        [MenuItem("Tools/NarutoVR/线性RPG/配置腕饰菜单与独立教学卡")]
        public static void Menu() => Debug.Log(Configure());
        [MenuItem("Tools/NarutoVR/线性RPG/应用易操作抬腕配置")]
        public static void EaseMenu() => Debug.Log(ConfigureEase());
        public static string ConfigureEase()
        {
            string backup = Configure();
            var scene = SceneManager.GetActiveScene();
            var menu = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WristMenuController>(true)).Single();
            var s = menu.settings;
            s.openHold=.24f; s.raiseBelowHead=.5f; s.lowerBelowHead=.66f; s.attentionAngle=60; s.stableRadius=.12f;
            s.requireWristFacing=false; s.poseGrace=.09f; s.lowerHold=.12f; s.reopenRelease=.1f; s.neutralHold=.18f;
            EditorUtility.SetDirty(menu); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("抬腕配置保存失败");
            return "已应用胸前握拳、短停留、无需扭腕、放腕确认；右手不再参与左腕释放判定。"+backup;
        }
        public static string Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LinearRpgSceneSetup.ScenePath)
                throw new InvalidOperationException("需要当前 LinearRpgTest 编辑模式场景");
            var rpg = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LinearRpgController>(true)).Single();
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath,"../../NarutoVR_Backups/wrist-menu-scene-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(backup);
            File.Copy(Path.GetFullPath(scene.path),Path.Combine(backup,"before-disk.unity"));
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new IOException("无法保存当前用户修改，尚未配置腕饰菜单");
            File.Copy(Path.GetFullPath(scene.path),Path.Combine(backup,"before-with-current-edits.unity"));
            if (!rpg.wristMenu)
            {
                var go = new GameObject("左腕术式菜单与教学卡");
                SceneManager.MoveGameObjectToScene(go,scene);
                rpg.wristMenu = go.AddComponent<WristMenuController>();
                rpg.wristMenu.view = go.AddComponent<WristMenuView>();
                rpg.wristMenu.view.font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            }
            EditorUtility.SetDirty(rpg); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("腕饰菜单配置保存失败");
            return "已接入腕饰菜单。结印/咒语卡放腕保留，画符预览放腕收起；三类均可独立修改。场景备份："+backup;
        }

        [MenuItem("Tools/NarutoVR/线性RPG/生成腕饰教学卡预览")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("预览需要编辑模式");
            var root = new GameObject("腕饰临时预览") { hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("腕饰预览相机") { hideFlags = HideFlags.HideAndDontSave };
            var render = RenderTexture.GetTemporary(1600,1000,24);
            var old = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                root.layer = 30;
                var view = root.AddComponent<WristMenuView>();
                view.EnsureBuilt();
                // Isolated render above the scene; no editor focus and no scene save.
                var origin = new Vector3(0,1000,0);
                view.ShowLauncher(new Pose(origin+new Vector3(-.36f,-.12f,1.1f),Quaternion.identity));
                view.ShowPanel(new Pose(origin+new Vector3(.22f,.02f,1.1f),Quaternion.identity),"结印教学",
                    "火球术 · 已解锁\n触发方式：咒语＋结印\n需要结印：寅\n完整口令：火球术\n组合窗口：8 秒；顺序不限\n消耗 20 MP · 冷却 3 秒\n握住火球，甩手后松开。",
                    "2 / 4　放腕保留 · 高亮后轻捏确认\n松开后可再次点击；速查卡不暂停战斗");
                view.SetDecoration(new Pose(origin+new Vector3(-.36f,-.26f,1.1f),Quaternion.identity),true);
                view.SetHover("next",.7f);
                // Presentation contact sheet: arming hint and opened menu are shown side by side in time.
                view.SetOpeningHint(new Pose(origin+new Vector3(-.36f,.08f,1.1f),Quaternion.identity),"抬左腕 · 右指点腕饰开关",0,true);
                var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = origin;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.018f,.025f,.045f);
                camera.fieldOfView = 43; camera.cullingMask = 1<<30; camera.targetTexture = render;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
                pixels = new Texture2D(1600,1000,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs/wrist-ui-20261031"); File.WriteAllBytes("Logs/wrist-ui-20261031/preview.png",pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; RenderTexture.ReleaseTemporary(render);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
#endif
