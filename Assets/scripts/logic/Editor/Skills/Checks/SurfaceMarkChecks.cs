using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>用真实碰撞面、正式技能资产和离屏相机验证场景贴花；结束后关闭临时场景。</summary>
    internal static class SurfaceMarkChecks
    {
        internal static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请在编辑模式校验场景贴花");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var pool = new SurfaceMarkPool();
            var randomState = UnityEngine.Random.state;
            try
            {
                var scorch = AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>(
                    "Assets/ScriptableObjects/投射物效果配置/火球术投射物.asset").surfaceMark;
                var sword = AssetDatabase.LoadAssetAtPath<ThunderSwordEffectDefinition>(
                    "Assets/ScriptableObjects/雷光剑配置/雷光剑效果.asset");
                var cut = sword.surfaceMark;
                Check(scorch && cut && sword.ValidateDefinition() == null, "正式技能痕迹配置");
                foreach (var guid in AssetDatabase.FindAssets("t:BreathEffectDefinition"))
                    Check(AssetDatabase.LoadAssetAtPath<BreathEffectDefinition>(AssetDatabase.GUIDToAssetPath(guid)).surfaceMark == scorch, "火焰吐息接线");

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var box = wall.GetComponent<BoxCollider>();
                wall.transform.position = new Vector3(70, 61, 72);
                wall.transform.localScale = new Vector3(2, 2, .2f);
                Physics.SyncTransforms();
                var front = wall.transform.position - Vector3.forward * .1f;
                var diagonal = new Vector3(.4f, 0, 1).normalized;
                Check(Physics.SphereCast(front - diagonal, .055f, diagonal, out var sweep, 2) && sweep.collider == box, "斜射扫掠");
                Check(SurfaceContact.TryResolve(box, front - diagonal + diagonal * sweep.distance, diagonal,
                    .055f, sweep, out var hit) && Vector3.Dot(hit.Normal, Vector3.back) > .99f, "斜射表面法线");
                Check(SurfaceContact.TryResolve(box, front + Vector3.forward * .01f, diagonal,
                    .055f, null, out var overlap) && Mathf.Abs(overlap.Point.z - front.z) < .001f, "初始重叠补查表面");
                Check(SurfaceContact.TryResolve(box, front - Vector3.forward * .01f, Vector3.right,
                    .055f, null, out var skim) && Vector3.Dot(skim.Normal, Vector3.back) > .99f, "沿墙擦过的接触面");
                int projectileMarks = 0;
                var projectile = new GameObject("重叠火球").AddComponent<SkillProjectile>();
                projectile.transform.position = front + Vector3.forward * .01f;
                projectile.MarkSurface = _ => projectileMarks++;
                projectile.Initialize(diagonal, null, ~0, 0, null, null);
                Check(projectileMarks == 1, "火球真实初始重叠仅通知一次");
                box.isTrigger = true;
                Check(!scorch.Accepts(box), "触发区过滤");
                box.isTrigger = false;

                var camera = new GameObject("痕迹检查相机").AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = front - Vector3.forward;
                camera.orthographic = true;
                camera.orthographicSize = .65f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 3;
                camera.clearFlags = CameraClearFlags.SolidColor;
                var light = new GameObject("痕迹检查灯").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.identity;
                var clean = Capture(camera, "clean");

                pool.Stamp(overlap, scorch, diagonal, 0, 2);
                var projector = FindActive(scene).Single();
                Check(projector.material == scorch.material && projector.IsValid(), "焦痕材质和投影 Pass");
                var burned = Capture(camera, "scorch");
                int changed = PixelDifference(clean, burned);
                Check(changed > 100, "焦痕渲染可见，变化像素 " + changed);
                var position = projector.transform.position;
                wall.transform.position += Vector3.right;
                pool.Tick(1, 2);
                Check(Vector3.Distance(projector.transform.position, position + Vector3.right) < .001f, "随移动表面");
                pool.Tick(scorch.holdSeconds + scorch.fadeSeconds * .5f, 2);
                Check(Mathf.Abs(projector.fadeFactor - .5f) < .001f, "半程透明度");
                pool.Tick(scorch.holdSeconds + scorch.fadeSeconds + 1, 2);
                Check(!projector.gameObject.activeSelf, "到期回池");
                wall.transform.position -= Vector3.right;
                Physics.SyncTransforms();
                Check(PixelDifference(clean, Capture(camera, "restored")) < changed / 4, "到期恢复原画面");

                var detector = new GameObject("测试剑刃").AddComponent<MeleeHitDetector>();
                detector.bladeBase = detector.transform;
                detector.bladeTip = new GameObject("剑尖").transform;
                detector.bladeTip.SetParent(detector.transform, false);
                detector.bladeTip.localPosition = Vector3.forward * .4f;
                detector.transform.position = front - Vector3.forward * .25f - Vector3.right * .15f;
                detector.SurfaceTouched += (s, h) => pool.Stamp(h, cut, Vector3.right, 10, 2, s);
                detector.Sample(null, null, ~0, 0);
                Check(FindActive(scene).Length == 0, "没有挥击时抵墙");
                var strike = new SkillStrike("surface-check", 1, 1, null);
                detector.Sample(strike, null, ~0, 0);
                detector.transform.position += Vector3.right * .3f;
                detector.Sample(strike, null, ~0, 0);
                var slash = FindActive(scene).Single();
                Check(slash == projector && slash.size.y > .25f && Vector3.Dot(slash.transform.up, Vector3.right) > .99f, "一挥一痕、水平长轴、复用");
                int slashPixels = PixelDifference(clean, Capture(camera, "slash"));
                Check(slashPixels > 10, "水平剑痕渲染可见，变化像素 " + slashPixels);
                var spray = new object();
                for (int i = 0; i < 8; i++) pool.Stamp(overlap, scorch, diagonal, 11 + i *.1, 2, spray, true);
                Check(FindActive(scene).Length == 2, "持续喷射原位复用");
                pool.Stamp(overlap, cut, Vector3.up, 12, 2);
                Check(FindActive(scene).Length == 2 && FindActive(scene).Any(p => p.material == cut.material &&
                    Vector3.Dot(p.transform.up, Vector3.up) > .99f), "容量上限与竖向剑波");
                pool.Tick(12, 0);
                Check(FindActive(scene).Length == 0, "运行时调低预算释放超限贴花");
                return $"火球斜射/初始重叠/擦墙、实体过滤、移动表面、单挥接触、横竖方向、池上限、持续刷新及消退通过。焦痕变化 {changed} 像素，剑痕 {slashPixels} 像素。";
            }
            finally
            {
                pool.Clear();
                UnityEngine.Random.state = randomState;
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static DecalProjector[] FindActive(Scene scene) => scene.GetRootGameObjects()
            .Where(go => go.activeInHierarchy).Select(go => go.GetComponent<DecalProjector>()).Where(p => p).ToArray();

        static Color32[] Capture(Camera camera, string label)
        {
            var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                // 新建场景首次绘制时，URP 可能尚未提交物体。第二次绘制作为样本。
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs/surface-marks");
                File.WriteAllBytes("Logs/surface-marks/" + label + ".png", texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                RenderTexture.active = old;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
            }
        }

        static int PixelDifference(Color32[] a, Color32[] b)
        {
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 28) changed++;
            return changed;
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }
    }
}
