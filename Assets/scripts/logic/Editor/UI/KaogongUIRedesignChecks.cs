#if UNITY_EDITOR
using System;
using System.IO;
using NarutoVR.SceneArt;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Editor
{
    public static class KaogongUIRedesignChecks
    {
        private const string LoadingOverlayPrefabPath = "Assets/Prefabs/SceneLoadingOverlay.prefab";
        private const string ReportPath = "Logs/kaogong-ui-redesign-check.json";

        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public bool hardwareTested;
            public string utc;
            public int atlasSprites;
            public int wristMenuChecks;
            public int wristTapChecks;
            public int wristLifecycleChecks;
            public int wristEaseChecks;
            public int kaogongUiChecks;
            public string detail;
        }

        [MenuItem("Tools/NarutoVR/界面/运行考工 UI 重设计检查")]
        public static void RunInteractive()
        {
            var report = Execute();
            Debug.Log($"[考工UI] 检查完成: passed={report.passed}, detail={report.detail}");
        }

        public static void RunBatch()
        {
            Report report;
            try
            {
                report = Execute();
            }
            catch (Exception ex)
            {
                report = new Report
                {
                    passed = false,
                    hardwareTested = false,
                    utc = DateTime.UtcNow.ToString("O"),
                    detail = ex.ToString()
                };
                WriteReport(report);
                Debug.LogException(ex);
                EditorApplication.Exit(1);
                return;
            }

            WriteReport(report);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        public static int RunAllChecks(out Report summary)
        {
            try
            {
                summary = Execute();
                WriteReport(summary);
                return summary.kaogongUiChecks + summary.wristMenuChecks + summary.wristTapChecks + summary.wristLifecycleChecks + summary.wristEaseChecks;
            }
            catch (Exception ex)
            {
                summary = new Report
                {
                    passed = false,
                    hardwareTested = false,
                    utc = DateTime.UtcNow.ToString("O"),
                    detail = ex.ToString()
                };
                WriteReport(summary);
                throw;
            }
        }

        private static Report Execute()
        {
            int checks = 0;
            void Check(bool ok, string why)
            {
                if (!ok) throw new Exception("考工 UI 验收失败：" + why);
                checks++;
            }

            KaogongUIAtlasPostprocessor.EnsureAtlasConfigured();
            KaogongUISprites.Invalidate();

            Sprite[] nativeSubSprites = Resources.LoadAll<Sprite>(KaogongUISprites.AtlasResourcePath);
            Check(nativeSubSprites != null && nativeSubSprites.Length == KaogongUISprites.Manifest.Length,
                $"图集原生切片数量应为 {KaogongUISprites.Manifest.Length}，实际为 {nativeSubSprites?.Length ?? 0}");

            for (int i = 0; i < KaogongUISprites.Manifest.Length; i++)
            {
                var entry = KaogongUISprites.Manifest[i];
                var sprite = KaogongUISprites.Get(entry.Name);
                Check(sprite != null, $"图集精灵缺失：{entry.Name}");
                Check(Mathf.Abs(sprite.rect.width - entry.Rect.width) < 1f && Mathf.Abs(sprite.rect.height - entry.Rect.height) < 1f,
                    $"图集精灵尺寸偏差：{entry.Name}");
                Check((sprite.border - entry.Border).sqrMagnitude < 0.01f,
                    $"图集九宫格 border 偏差：{entry.Name} 期望 {entry.Border} 实际 {sprite.border}");
            }

            // 既有准望匣（腕部 UI）核心回归
            int menuCount = WristMenuChecks.Run();
            int tapCount = WristTapChecks.Run();
            int lifecycleCount = WristViewLifecycleChecks.Run();
            int easeCount = WristMenuEaseChecks.Run();

            // 准望匣 UI 天工金石风格与清晰度检查
            var viewHost = new GameObject("考工 UI 测试宿主 · 准望匣") { hideFlags = HideFlags.DontSave };
            try
            {
                var view = viewHost.AddComponent<WristMenuView>();
                view.EnsureBuilt();
                Check(view.Targets.Count == 16, $"WristMenuView 目标数应为 16，实际为 {view.Targets.Count}");
                Check(view.OwnedRoot != null, "WristMenuView 应生成 OwnedRoot");

                var scalers = view.OwnedRoot.GetComponentsInChildren<CanvasScaler>(true);
                Check(scalers.Length >= 4, $"腕部画布 CanvasScaler 数量不足：{scalers.Length}");
                for (int i = 0; i < scalers.Length; i++)
                    Check(scalers[i].dynamicPixelsPerUnit >= 2.5f,
                        $"腕部文字锐度不足：{scalers[i].name} dynamicPixelsPerUnit={scalers[i].dynamicPixelsPerUnit}");

                int slicedButtons = 0;
                int bronzeOrJadeFrames = 0;
                foreach (var img in view.OwnedRoot.GetComponentsInChildren<Image>(true))
                {
                    if (!img.sprite) continue;
                    if (img.sprite.name == "Button_Normal" && img.type == Image.Type.Sliced) slicedButtons++;
                    if ((img.sprite.name == "Frame_Bronze_Main" || img.sprite.name == "Frame_Jade_Plaque") && img.type == Image.Type.Sliced)
                        bronzeOrJadeFrames++;
                }
                Check(slicedButtons >= 6, $"准望匣九宫格按钮过少：{slicedButtons}");
                Check(bronzeOrJadeFrames >= 4, $"准望匣青铜/温玉九宫格底框过少：{bronzeOrJadeFrames}");

                foreach (var g in view.OwnedRoot.GetComponentsInChildren<Graphic>(true))
                {
                    Color c = g.color;
                    if (c.a <= 0.05f) continue;
                    bool pureWhite = c.r > 0.995f && c.g > 0.995f && c.b > 0.995f;
                    bool pureBlack = c.r < 0.005f && c.g < 0.005f && c.b < 0.005f;
                    // 图集与教学照片自身采用近白底避免偏色；仅校验无贴图纯白板/纯黑板及文字
                    if ((!g.TryGetComponent<Image>(out var img) || img.sprite == null) && !g.TryGetComponent<RawImage>(out _))
                    {
                        Check(!pureWhite, $"发现未贴图纯白刺眼色块：{g.name}");
                        Check(!pureBlack, $"发现未贴图纯黑 OLED 拖影色块：{g.name}");
                    }
                }

                // 校验准望匣实物展示控件（ZhunwangWristDisplay）字体锐度与倒下玉牌底框
                var zhunwangPrefab = AssetDatabase.LoadAssetAtPath<ZhunwangWristDisplay>("Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.prefab");
                Check(zhunwangPrefab != null, "未找到 Zhunwang_ButtonSplit.prefab");
                var zhunwangInstance = UnityEngine.Object.Instantiate(zhunwangPrefab);
                try
                {
                    zhunwangInstance.gameObject.hideFlags = HideFlags.DontSave;
                    zhunwangInstance.Present(null, false, false);
                    var displayScaler = zhunwangInstance.vitalsAnchor ? zhunwangInstance.vitalsAnchor.GetComponent<CanvasScaler>() : null;
                    Check(displayScaler != null && displayScaler.dynamicPixelsPerUnit >= 2.5f,
                        $"准望匣刻度 CanvasScaler 锐度不足：{displayScaler?.dynamicPixelsPerUnit}");
                    if (zhunwangInstance.recoveryHint)
                    {
                        var holder = zhunwangInstance.recoveryHint.transform.parent;
                        var holderImg = holder ? holder.GetComponent<Image>() : null;
                        Check(holder && holder.name == "倒下提示牌底" && holderImg && holderImg.sprite && holderImg.sprite.name == "Button_Hover" && holderImg.type == Image.Type.Sliced,
                            "准望匣倒下提示须使用 Button_Hover 九宫格底牌");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(zhunwangInstance.gameObject);
                }

                // 渲染离线 UI 预览供人工核查排版、清晰度与错金九宫格边角
                var launcherPose = new Pose(new Vector3(0f, 1.4f, 0.45f), Quaternion.identity);
                view.ShowLauncher(launcherPose);
                var launcher = view.OwnedRoot.transform.Find("术式启动器");
                Check(launcher && launcher.gameObject.activeInHierarchy, "术式启动器应实际显示");
                CaptureWorldCanvas(launcher, 0.56f, 36f, 1200, 840, "Logs/kaogong-ui-preview-launcher.png");
                var heading = launcher.Find("启动器标题")?.GetComponent<Text>();
                Check(heading && heading.canvasRenderer.GetMesh() is { vertexCount: >= 4 },
                    "术式启动器标题须生成可见中文网格");
                view.ShowPanel(
                    new Pose(new Vector3(0f, 1.4f, 0.6f), Quaternion.identity),
                    "考工印谱 · 风火双印",
                    "左手结印定准，右手扬符引雷。\n青铜错金主框、九宫格温玉标签与素缣分割线布局预览。",
                    "试炼记录 · 顺滑清晰");
                var panel = view.OwnedRoot.transform.Find("独立术式面板");
                Check(panel && panel.gameObject.activeInHierarchy, "独立术式面板应实际显示");
                CaptureWorldCanvas(panel, 0.80f, 38f, 1280, 940, "Logs/kaogong-ui-preview-panel.png");

                view.ReleaseVisuals(true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewHost);
            }

            // 加载模块：现有 Canvas 架构下的轻量化空间悬停玉牌与浑仪加载进度框
            var overlayPrefab = AssetDatabase.LoadAssetAtPath<SceneLoadingOverlay>(LoadingOverlayPrefabPath);
            Check(overlayPrefab != null, $"未找到加载蒙版预制体：{LoadingOverlayPrefabPath}");
            var overlay = UnityEngine.Object.Instantiate(overlayPrefab);
            var loadingCamera = new GameObject("KaogongUI_LoadingPreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                overlay.gameObject.hideFlags = HideFlags.DontSave;
                var camera = loadingCamera.AddComponent<Camera>();
                overlay.Show(camera, null);
                overlay.SampleForValidation(0.55f, 0.35f);

                var canvas = overlay.GetComponent<Canvas>();
                Check(canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == camera,
                    "加载画布须由 Show 绑定有效观察相机并使用 ScreenSpaceCamera");
                Check(canvas.planeDistance >= 1.2f, $"加载画布焦距应处于舒适区：{canvas.planeDistance}");

                var plaque = overlay.transform.Find("考工悬停玉牌") as RectTransform;
                Check(plaque != null, "加载画布未创建空间悬停玉牌容器");

                var plaqueBg = plaque.Find("温玉底牌")?.GetComponent<Image>();
                Check(plaqueBg && plaqueBg.sprite && plaqueBg.sprite.name == "Frame_Jade_Plaque" && plaqueBg.type == Image.Type.Sliced,
                    "加载玉牌须使用 Frame_Jade_Plaque 九宫格切片");

                var ring = plaque.Find("浑仪齿轮环")?.GetComponent<Image>();
                Check(ring && ring.sprite && ring.sprite.name == "Ring_Astrolabe",
                    "浑仪齿轮环须绑定 Ring_Astrolabe 精灵");

                var arc = plaque.Find("浑仪进度弧")?.GetComponent<Image>();
                Check(arc && arc.sprite && arc.sprite.name == "Ring_Astrolabe"
                    && arc.type == Image.Type.Filled && arc.fillMethod == Image.FillMethod.Radial360,
                    "浑仪进度弧须使用 Ring_Astrolabe 360° 径向填充");
                Check(Mathf.Abs(arc.fillAmount - 0.55f) < 0.01f, $"浑仪进度弧未同步至 55%：{arc.fillAmount}");

                var bar = overlay.GetComponentInChildren<SceneLoadingProgressBar>(true);
                Check(bar && bar.Fill && bar.Percentage, "SceneLoadingProgressBar 序列化绑定不可断裂");
                Check(bar.Fill.type == Image.Type.Filled && Mathf.Abs(bar.Fill.fillAmount - 0.55f) < 0.01f,
                    $"水平进度条未同步至 55%：{bar.Fill?.fillAmount}");
                Check(bar.Fill.sprite && (bar.Fill.sprite.name == "Gauge_Fill_Inner" || bar.Fill.sprite.name == "Gauge_Fill"),
                    $"水平进度条未使用考工游标贴图：{bar.Fill.sprite?.name}");
                Check(bar.Percentage.text == "55%", $"百分比文字应为 55%，实际为：{bar.Percentage.text}");

                foreach (var graphic in plaque.GetComponentsInChildren<Graphic>(true))
                {
                    Check(graphic.material && graphic.material.shader && graphic.material.shader.name == "NarutoVR/UI/Scene Loading Overlay",
                        $"加载控件 {graphic.name} 未沿用立体叠加着色器：{graphic.material?.shader?.name}");
                }
                CaptureScreenCameraOverlay(overlay, camera, "Logs/kaogong-ui-preview-loading.png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                UnityEngine.Object.DestroyImmediate(loadingCamera);
            }

            // 校验无预制体回退模式下的 SceneLoadingOverlay 容错创建
            var minimalHost = new GameObject("考工加载最小宿主") { hideFlags = HideFlags.DontSave };
            try
            {
                var minimal = minimalHost.AddComponent<SceneLoadingOverlay>();
                minimal.SampleForValidation(0.8f, 0.25f);
                var plaque = minimal.transform.Find("考工悬停玉牌");
                Check(plaque != null, "空宿主 SceneLoadingOverlay 应自动补齐悬停玉牌与浑仪结构");
                var pct = plaque.Find("进度百分比")?.GetComponent<Text>();
                Check(pct && pct.text == "80%", $"空宿主百分比异常：{pct?.text}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(minimalHost);
            }

            var glyphHost = new GameObject("考工 UI 测试宿主 · 墨规画板") { hideFlags = HideFlags.DontSave };
            try
            {
                glyphHost.transform.position = new Vector3(0f, 1.38f, .60f);
                var board = glyphHost.AddComponent<GlyphBoard>();
                board.Open(null, false);
                var panel = glyphHost.transform.Find("墨规画板");
                var frame = panel ? panel.Find("青铜板")?.GetComponent<Image>() : null;
                Check(frame && frame.sprite && frame.sprite.name == "Frame_Bronze_Main" && frame.type == Image.Type.Sliced,
                    "墨规画板须使用青铜九宫格外框");
                Check(panel.Find("温玉符名匾")?.GetComponent<Image>()?.sprite?.name == "Frame_Jade_Plaque",
                    "墨规画板须使用温玉符名匾");
                var writing = panel.Find("书写面") as RectTransform;
                Check(writing && writing.rect.size == new Vector2(2 * GlyphBoard.HalfWidth, 2 * GlyphBoard.HalfHeight) * 1000f,
                    "换装须保留书写尺寸");

                var reticle = panel.Find("笔尖落点");
                var tip = new Vector3(.032f, .057f, .047f);
                board.Sample(glyphHost.transform.TransformPoint(tip), true, 1.0);
                Check(reticle && reticle.gameObject.activeSelf && board.PointCount == 0, "接近画板显示笔尖投影，未贴板不采样");
                tip.z = .005f;
                board.Sample(glyphHost.transform.TransformPoint(tip), true, 1.05);
                Check(board.PointCount == 1, "实际落笔保持原采样行为");

                var button = panel.Find("撤销").GetComponent<Image>();
                var buttonTip = new Vector3(-GlyphBoard.ButtonSpacing, GlyphBoard.ButtonY, .014f);
                board.Sample(glyphHost.transform.TransformPoint(buttonTip), true, 1.1);
                Check(button.sprite && button.sprite.name == "Button_Hover" && board.PointCount == 1,
                    "悬停高亮不触发撤销");
                buttonTip.z = .004f;
                board.Sample(glyphHost.transform.TransformPoint(buttonTip), true, 1.15);
                Check(board.PointCount == 0 && Mathf.Abs(button.rectTransform.anchoredPosition.y - GlyphBoard.ButtonY * 1000f + 2f) < .01f,
                    "触碰撤销并显示 2 毫米下沉");
                board.Sample(default, false, 1.2);
                Check(!reticle.gameObject.activeSelf && button.sprite && button.sprite.name == "Button_Normal",
                    "失追时复位笔尖投影和按钮外观");

                void PreviewPoint(float x, float y, double time)
                    => board.Sample(glyphHost.transform.TransformPoint(new Vector3(x, y, .005f)), true, time);
                PreviewPoint(-.101f, .074f, 1.3);
                PreviewPoint(.102f, .074f, 1.35);
                board.Sample(default, false, 1.40);
                PreviewPoint(0f, .074f, 1.45);
                PreviewPoint(0f, -.013f, 1.50);
                CaptureWorldCanvas(panel, .64f, 41f, 1080, 980, "Logs/kaogong-ui-preview-glyph.png");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(glyphHost);
            }

            return new Report
            {
                passed = true,
                hardwareTested = false,
                utc = DateTime.UtcNow.ToString("O"),
                atlasSprites = KaogongUISprites.Manifest.Length,
                wristMenuChecks = menuCount,
                wristTapChecks = tapCount,
                wristLifecycleChecks = lifecycleCount,
                wristEaseChecks = easeCount,
                kaogongUiChecks = checks,
                detail = $"atlas={KaogongUISprites.Manifest.Length}, wristMenu={menuCount}, wristTap={tapCount}, wristLifecycle={lifecycleCount}, wristEase={easeCount}, uiChecks={checks}"
            };
        }

        private static void CaptureWorldCanvas(Transform canvasRoot, float distance, float fov, int width, int height, string outputPath)
        {
            if (!canvasRoot) return;
            var camGo = new GameObject("KaogongUI_WorldPreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                var cam = camGo.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(canvasRoot.position - canvasRoot.forward * distance, canvasRoot.rotation);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.065f, 0.06f, 1f);
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.05f;
                cam.cullingMask = 1 << canvasRoot.gameObject.layer;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        private static void CaptureScreenCameraOverlay(SceneLoadingOverlay overlay, Camera camera, string outputPath)
        {
            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            Texture2D pixels = null;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.05f, 0.05f, 1f);
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.1f;
                camera.cullingMask = 1 << overlay.gameObject.layer;
                camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases();

                camera.Render();
                RenderTexture.active = rt;
                pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void WriteReport(Report report)
        {
            var dir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }
    }
}
#endif
