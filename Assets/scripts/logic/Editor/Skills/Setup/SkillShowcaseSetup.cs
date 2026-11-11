#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>按共享技能配置搭建不同展示场景，并接入对应视觉与试炼说明。</summary>
    public static class SkillShowcaseSetup
    {
        public static readonly string[] ScenePaths =
        {
            "Assets/Scene/FireballRange.unity",
            "Assets/Scene/ShieldTrial.unity",
            "Assets/Scene/ComboSanctum.unity"
        };
        private const string MaterialsPath = "Assets/Materials/Showcase";
        private const string MeshPath = "Assets/Materials/Showcase/低面环形.asset";
        private static Material stone, dark, metal, cyan, orange, violet, pale;
        private static Mesh torus;
        private static Font font;

        [MenuItem("Tools/NarutoVR/技能展示/创建三个独立试炼场景")]
        public static void BuildAll()
        {
            RequireEditMode();
            if (!File.Exists(SkillTestSceneSetup.ScenePath)) throw new InvalidOperationException("请先创建现有 SkillTest 场景；展示构建器不会修改旧场景。");
            SkillVisualAssets.EnsureAssets(); EnsureMaterials();
            var previous = SceneManager.GetActiveScene();
            try
            {
                for (int i = 0; i < ScenePaths.Length; i++)
                {
                    if (File.Exists(ScenePaths[i])) { Debug.Log("[技能展示] 保留已有自定义场景：" + ScenePaths[i]); continue; }
                    if (!AssetDatabase.CopyAsset(SkillTestSceneSetup.ScenePath, ScenePaths[i])) throw new InvalidOperationException("复制场景失败：" + ScenePaths[i]);
                    var scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Additive);
                    try
                    {
                        SceneManager.SetActiveScene(scene);
                        BuildScene(scene, (SkillScenarioKind)i);
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存场景失败：" + ScenePaths[i]);
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            Debug.Log("[技能展示] 三个独立试炼场景已就绪；已有场景内容保持原样。");
        }

        private static void BuildScene(Scene scene, SkillScenarioKind kind)
        {
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SkillInputController>(true)).Single();
            // New showcases choose the shared VFX configuration explicitly; the base lab can keep basic visuals.
            controller.catalog = SkillConfigurationSetup.EnsureDefaults();
            if(controller.view)
            {
                controller.view.shortcuts.Clear();
                controller.view.AssignDefaultShortcuts(controller.ConfiguredSkills);
            }
            if (!controller.effects || !controller.lab || controller.ConfiguredSkills.Count == 0) throw new InvalidOperationException("复制场景缺少技能、骨骼或效果绑定。");
            font = controller.chineseFont;
            if (!font) throw new InvalidOperationException("原场景缺少中文字体，无法生成可读试炼说明。");
            controller.effects.fireballPrefab = LoadPrefab(SkillVisualAssets.FireballPath);
            controller.effects.shieldPrefab = LoadPrefab(SkillVisualAssets.ShieldPath);
            controller.effects.impactPrefab = LoadPrefab(SkillVisualAssets.ImpactPath);
            controller.statusText.canvas.sortingOrder = 100;
            // Only copied training targets are replaced; the original scene and tracking rig are never touched.
            foreach (var target in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SkillTrainingTarget>(true)).ToArray())
                Object.DestroyImmediate(target.gameObject);
            var root = new GameObject("试炼场景 · " + kind).transform;
            var scenario = root.gameObject.AddComponent<SkillScenario>();
            scenario.kind = kind; scenario.effects = controller.effects; scenario.chineseFont = font;
            scenario.autoRunOnFirstShield = kind == SkillScenarioKind.ShieldTrial;
            SetLighting(scene, kind);
            if (kind == SkillScenarioKind.FireballRange) BuildFireball(root, scenario);
            else if (kind == SkillScenarioKind.ShieldTrial) BuildShield(root, scenario);
            else BuildCombo(root, scenario);
            scenario.worldText = WorldPanel(root, "试炼说明", scenario.title + "\n\n" + scenario.instructions,
                new Vector3(2.85f, 1.8f, 1.75f), new Vector2(940, kind == SkillScenarioKind.FireballRange ? 650 : 1000), .0016f, cyan, 34);
            if (controller.statusText) controller.statusText.text = scenario.title + "\n\n原有三种配方与骨骼追踪保留。\n\n播放后显示实时语音、匹配、护盾和施法状态。";
        }

        [MenuItem("Tools/NarutoVR/技能展示/修复头显面板遮挡")]
        public static void TuneHudOrder()
        {
            RequireEditMode();
            RequireCleanScenes();
            foreach (string path in ScenePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool wasOpen = scene.IsValid() && scene.isLoaded;
                if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkillInputController>(true)).Single();
                    var canvas = controller.statusText.canvas;
                    // World-space instruction panels must not draw over the nearer HUD text.
                    if (canvas.sortingOrder != 100)
                    {
                        canvas.sortingOrder = 100;
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("保存头显面板排序失败：" + path);
                    }
                }
                finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
            }
        }

        private static void BuildFireball(Transform root, SkillScenario scenario)
        {
            scenario.title = "焰道 · 火球靶场";
            scenario.instructions = "寅印 ＋ “火球术” → 右手火球\n准备后向前轻推发射\n\n三只漂浮靶：3米 / 5米 / 7米\n青色地面箭头指向射击方向\n侧墙后另有阻挡测试靶\n\n取消：张开双手，或“取消施法”";
            Box(root, "庭院石台", new Vector3(0, -.28f, 3.6f), new Vector3(13, .5f, 13), stone, true);
            for (int z = -1; z <= 9; z++)
                for (int x = -4; x <= 4; x++)
                    Box(root, "石板", new Vector3(x * 1.1f, -.005f, z * 1.1f), new Vector3(1.04f, .04f, 1.04f), (x + z) % 3 == 0 ? metal : dark);
            for (int i = 0; i < 3; i++) Box(root, "后庭台阶", new Vector3(0, .06f + i * .12f, 8.1f + i * .42f), new Vector3(8.4f - i * .3f, .12f, .5f), stone);
            Box(root, "背墙", new Vector3(0, 1.0f, 10), new Vector3(12.5f, 2, .35f), dark, true);
            Box(root, "左侧阻挡测试墙", new Vector3(-3.65f, 1.25f, 4.3f), new Vector3(.35f, 2.5f, 3.7f), stone, true);
            Box(root, "右侧矮墙", new Vector3(5, .65f, 4.5f), new Vector3(.35f, 1.3f, 7.5f), stone, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Pillar(root, new Vector3(side * 3.5f, 0, 7.9f), 3.65f, orange);
                Box(root, "门柱外翼", new Vector3(side * 3.8f, 3.75f, 7.9f), new Vector3(1.2f, .24f, .8f), dark).transform.localRotation = Quaternion.Euler(0, 0, -side * 12);
                for (int z = 0; z < 4; z++) Lantern(root, new Vector3(side * 4.7f, 0, z * 2.3f + .4f), orange);
            }
            Box(root, "忍者庭院门梁", new Vector3(0, 3.56f, 7.9f), new Vector3(8.8f, .38f, .75f), dark);
            Box(root, "门梁火纹", new Vector3(0, 3.81f, 7.78f), new Vector3(8.4f, .05f, .08f), orange);
            Header(root, "焰 道  /  火 球 靶 场", new Vector3(0, 4.35f, 8.5f), orange);
            float[] distances = { 3, 5, 7 }; float[] offsets = { -1.1f, 0, 1.6f };
            for (int i = 0; i < distances.Length; i++)
            {
                float z = Mathf.Sqrt(distances[i] * distances[i] - offsets[i] * offsets[i]);
                Target(root, new Vector3(offsets[i], 1.6f, z), $"靶 {i + 1:00} · {distances[i]:0} 米", orange);
            }
            Target(root, new Vector3(-4.7f, 1.5f, 4.8f), "墙后靶 · 阻挡测试", violet);
            for (int i = 0; i < 7; i++) Arrow(root, new Vector3(0, .035f, .3f + i * 1.05f), cyan);
            Line(root, "左轨迹引导", new Vector3(-2.25f, .035f, -.7f), new Vector3(-2.25f, .035f, 8), cyan, .025f);
            Line(root, "右轨迹引导", new Vector3(2.25f, .035f, -.7f), new Vector3(2.25f, .035f, 8), cyan, .025f);
            Sigil(root, new Vector3(0, .015f, -.25f), .7f);
            SkyMarks(root, orange);
        }

        private static void BuildShield(Transform root, SkillScenario scenario)
        {
            scenario.title = "澄心 · 护盾试炼";
            scenario.instructions = "子印 或 “护体咒” → 自身护盾\n首次获得护盾后自动开始\n0.5 秒后首击，此后每 2.5 秒\n每次 30 点，最多 4 次\n\nF8：准备 / 停止一轮测试\n未吸收伤害只报告，不扣玩家生命\n玩家生命系统尚未接入";
            var center = new Vector3(0, 0, 3);
            Cylinder(root, "圆形试炼台基座", center + Vector3.down * .22f, new Vector3(12, .2f, 12), stone, true);
            Cylinder(root, "青蓝内台", center + Vector3.down * .005f, new Vector3(10.8f, .015f, 10.8f), dark);
            Ring(root, center + Vector3.up * .035f, 5.65f, cyan);
            Ring(root, center + Vector3.up * .04f, 4.85f, pale);
            Ring(root, center + Vector3.up * .04f, 3.8f, cyan);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2 / 12;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 5.4f;
                Pillar(root, point, i % 3 == 0 ? 3.2f : 2.35f, cyan);
                Line(root, "放射能量线", center + new Vector3(Mathf.Cos(angle), .045f, Mathf.Sin(angle)) * 1.4f,
                    point + Vector3.up * .045f, i % 2 == 0 ? cyan : pale, .035f);
            }
            Sigil(root, center + Vector3.up * .04f, 2.8f);
            Ring(root, new Vector3(0, .04f, .1f), .9f, cyan);
            Header(root, "澄 心  /  护 盾 试 炼", new Vector3(0, 3.8f, 7.5f), cyan);
            WorldPanel(root, "护盾规则", "100 点护盾\n30 → 30 → 30 → 余量\n\n只结算护盾吸收", new Vector3(-2.3f, 1.45f, 2.6f), new Vector2(850, 460), .0016f, cyan, 39);
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3(-1.5f + i, .1f, 5.2f);
                Cylinder(root, "护盾刻度底座", point, new Vector3(.7f, .08f, .7f), metal);
                Ring(root, point + Vector3.up * .1f, .28f, i == 3 ? violet : cyan);
            }
            Target(root, new Vector3(0, 1.6f, 7), "保留火球测试靶", cyan);
            SkyMarks(root, cyan);
        }

        private static void BuildCombo(Transform root, SkillScenario scenario)
        {
            scenario.title = "合契 · 组合施法试炼";
            scenario.instructions = "同一 8 秒窗口，顺序不限\n\n① 先完成寅印，再念“火球术”\n② 先念“火球术”，再完成寅印\n\n匹配后火球立即生成到右手\n向前轻推，命中远端训练靶\n\n地面阵法为场景美术；没有领域效果\n当前按关键词触发，请直接念口令；\n否定句也可能命中。";
            var center = new Vector3(0, 0, 4);
            Cylinder(root, "几何阵台", center + Vector3.down * .25f, new Vector3(14, .23f, 14), stone, true);
            Cylinder(root, "阵台暗面", center + Vector3.down * .005f, new Vector3(13.3f, .015f, 13.3f), dark);
            Sigil(root, center + Vector3.up * .025f, 4.5f);
            Ring(root, center + Vector3.up * .04f, 6.6f, violet);
            Ring(root, center + Vector3.up * .045f, 5.9f, cyan);
            Vector3[] vertices = new Vector3[8];
            for (int i = 0; i < vertices.Length; i++)
            {
                float a = i * Mathf.PI * .25f;
                vertices[i] = center + new Vector3(Mathf.Cos(a) * 5.5f, .05f, Mathf.Sin(a) * 5.5f);
                Pillar(root, vertices[i] - Vector3.up * .05f, i % 2 == 0 ? 3.8f : 2.8f, i % 2 == 0 ? violet : cyan);
            }
            for (int i = 0; i < vertices.Length; i++) Line(root, "八向几何契线", vertices[i], vertices[(i + 3) % 8], i % 2 == 0 ? violet : cyan, .028f);
            Header(root, "合 契  /  八 秒 双 向 施 法", new Vector3(0, 4.8f, 9.7f), violet);
            WorldPanel(root, "先印后咒说明", "01  先印后咒\n\n完成寅印\n↓\n8 秒内念“火球术”", new Vector3(-2.65f, 1.9f, 2.75f), new Vector2(900, 570), .00165f, violet, 37);
            Target(root, new Vector3(0, 1.7f, 8.3f), "组合验证靶 · 正前方", cyan);
            Target(root, new Vector3(-2.25f, 1.7f, 7.4f), "左侧训练靶", violet);
            Target(root, new Vector3(2.25f, 1.7f, 7.4f), "右侧训练靶", cyan);
            for (int i = 0; i < 6; i++) Arrow(root, new Vector3(0, .065f, .4f + i * 1.05f), cyan);
            SkyMarks(root, violet);
        }

        private static void SetLighting(Scene scene, SkillScenarioKind kind)
        {
            Color background = kind == SkillScenarioKind.FireballRange ? new Color(.012f, .025f, .055f) :
                kind == SkillScenarioKind.ShieldTrial ? new Color(.012f, .045f, .08f) : new Color(.035f, .018f, .075f);
            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .43f, .55f); RenderSettings.ambientIntensity = 1;
            foreach (var light in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Light>(true)))
            {
                if (light.type != LightType.Directional) { light.enabled = false; continue; }
                light.color = new Color(.58f, .72f, 1); light.intensity = 1.05f; light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(35, -35, 0);
            }
            foreach (var camera in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)))
            { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = background; }
        }

        private static void EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialsPath); AssetDatabase.Refresh();
            stone = Material("月下青石", new Color(.16f, .22f, .30f), false);
            dark = Material("玄色石面", new Color(.045f, .065f, .11f), false);
            metal = Material("石板银边", new Color(.24f, .34f, .42f), false);
            cyan = Material("青色能量线", new Color(.08f, .86f, 1f), true);
            orange = Material("橙红火纹", new Color(1f, .28f, .055f), true);
            violet = Material("紫色契纹", new Color(.68f, .25f, 1f), true);
            pale = Material("淡青符光", new Color(.57f, .95f, 1f), true);
            torus = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (!torus) { torus = CreateTorus(); AssetDatabase.CreateAsset(torus, MeshPath); }
        }

        private static Material Material(string name, Color color, bool unlit)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            if (!shader) throw new InvalidOperationException("缺少场景材质 Shader。");
            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .32f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }
        private static GameObject Box(Transform p, string n, Vector3 at, Vector3 scale, Material m, bool collision = false) => Primitive(PrimitiveType.Cube, p, n, at, scale, m, collision);
        private static GameObject Cylinder(Transform p, string n, Vector3 at, Vector3 scale, Material m, bool collision = false) => Primitive(PrimitiveType.Cylinder, p, n, at, scale, m, collision);

        private static void Pillar(Transform parent, Vector3 at, float height, Material accent)
        {
            Box(parent, "石柱基座", at + Vector3.up * .12f, new Vector3(.85f, .24f, .85f), stone);
            Box(parent, "低面石柱", at + Vector3.up * (height * .5f), new Vector3(.46f, height, .46f), dark);
            Box(parent, "石柱银脊", at + Vector3.up * (height * .5f) + Vector3.back * .24f, new Vector3(.06f, height * .8f, .025f), accent);
            Box(parent, "柱冠", at + Vector3.up * height, new Vector3(.75f, .2f, .75f), metal);
            Ring(parent, at + Vector3.up * (height + .14f), .32f, accent);
        }

        private static void Lantern(Transform parent, Vector3 at, Material accent)
        {
            Cylinder(parent, "灯座", at + Vector3.up * .28f, new Vector3(.55f, .28f, .55f), dark);
            var glow = Box(parent, "几何火灯", at + Vector3.up * .7f, new Vector3(.22f, .35f, .22f), accent);
            glow.transform.localRotation = Quaternion.Euler(0, 45, 8);
            Box(parent, "灯顶", at + Vector3.up * .95f, new Vector3(.5f, .08f, .5f), metal);
        }

        private static void Target(Transform parent, Vector3 position, string label, Material accent)
        {
            var target = Box(parent, label, position, new Vector3(.65f, .65f, .16f), metal, true);
            target.transform.localRotation = Quaternion.Euler(0, 0, 45);
            GameObjectUtility.SetStaticEditorFlags(target, 0);
            var health = target.AddComponent<SkillTrainingTarget>(); health.targetRenderer = target.GetComponent<Renderer>(); health.health = 100; health.resetAfterSeconds = 2;
            Ring(parent, position + Vector3.back * .1f, .49f, accent, true);
            var disc = Cylinder(parent, "靶心", position + Vector3.back * .11f, new Vector3(.22f, .018f, .22f), accent);
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            WorldPanel(parent, label + "标牌", label, position + Vector3.down * .77f, new Vector2(850, 120), .0013f, accent, 38, false);
        }

        private static void Arrow(Transform parent, Vector3 at, Material accent)
        {
            Box(parent, "前进引导箭柄", at, new Vector3(.035f, .018f, .45f), accent);
            foreach (float side in new[] { -1f, 1f })
            {
                var wing = Box(parent, "前进引导箭翼", at + new Vector3(side * .095f, 0, .17f), new Vector3(.035f, .018f, .27f), accent);
                wing.transform.localRotation = Quaternion.Euler(0, side * -45, 0);
            }
        }

        private static void Line(Transform parent, string name, Vector3 start, Vector3 end, Material material, float width)
        {
            Vector3 delta = end - start;
            var bar = Box(parent, name, (start + end) * .5f, new Vector3(width, .015f, delta.magnitude), material);
            bar.transform.localRotation = Quaternion.LookRotation(delta, Vector3.up);
        }

        private static void Ring(Transform parent, Vector3 position, float radius, Material material, bool vertical = false)
        {
            var go = new GameObject("能量环", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = Vector3.one * radius;
            if (vertical) go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            go.GetComponent<MeshFilter>().sharedMesh = torus;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static void Sigil(Transform parent, Vector3 at, float scale)
        {
            var prefab = LoadPrefab(SkillVisualAssets.SigilPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "场景装饰阵法 · 不产生领域效果"; go.transform.localPosition = at; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one * scale;
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private static void SkyMarks(Transform parent, Material accent)
        {
            for (int i = 0; i < 18; i++)
            {
                float x = Mathf.Sin(i * 2.399f) * (7 + i % 3);
                var mark = Box(parent, "远景符光", new Vector3(x, 4.8f + (i % 5) * .85f, 12 + (i % 4)), new Vector3(.035f, .13f, .035f), accent);
                mark.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }

        private static void Header(Transform parent, string text, Vector3 at, Material accent) => WorldPanel(parent, "场景题字", text, at, new Vector2(1600, 200), .0023f, accent, 60, false);

        private static Text WorldPanel(Transform parent, string name, string text, Vector3 at, Vector2 size, float scale, Material accent, int fontSize, bool background = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false);
            go.transform.localPosition = at; go.transform.localScale = Vector3.one * scale;
            Vector3 normal = at - new Vector3(0, at.y, 0); if (normal.sqrMagnitude > .01f) go.transform.localRotation = Quaternion.LookRotation(normal, Vector3.up);
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 1;
            if (background)
            {
                var bg = new GameObject("半透石墨面板", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(go.transform, false);
                Stretch(bg.GetComponent<RectTransform>(), 0); bg.GetComponent<Image>().color = new Color(.015f, .03f, .06f, .88f); bg.GetComponent<Image>().raycastTarget = false;
            }
            var label = new GameObject("中文说明", typeof(RectTransform), typeof(Text)); label.transform.SetParent(go.transform, false);
            Stretch(label.GetComponent<RectTransform>(), background ? 28 : 0);
            var result = label.GetComponent<Text>(); result.font = font; result.fontSize = fontSize; result.text = text;
            result.color = accent.HasProperty("_BaseColor") ? accent.GetColor("_BaseColor") : Color.cyan;
            result.alignment = background ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            result.horizontalOverflow = HorizontalWrapMode.Wrap; result.verticalOverflow = VerticalWrapMode.Overflow; result.raycastTarget = false;
            return result;
        }
        private static void Stretch(RectTransform rect, float padding) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * padding; rect.offsetMax = Vector2.one * -padding; }

        private static Mesh CreateTorus()
        {
            const int major = 48, minor = 6; var vertices = new Vector3[major * minor]; var indices = new int[major * minor * 6];
            for (int i = 0; i < major; i++) for (int j = 0; j < minor; j++)
            {
                float a = i * Mathf.PI * 2 / major, b = j * Mathf.PI * 2 / minor;
                vertices[i * minor + j] = new Vector3(Mathf.Cos(a) * (1 + .018f * Mathf.Cos(b)), .018f * Mathf.Sin(b), Mathf.Sin(a) * (1 + .018f * Mathf.Cos(b)));
                int at = (i * minor + j) * 6, nextI = (i + 1) % major, nextJ = (j + 1) % minor;
                indices[at] = i * minor + j; indices[at + 1] = nextI * minor + nextJ; indices[at + 2] = nextI * minor + j;
                indices[at + 3] = i * minor + j; indices[at + 4] = i * minor + nextJ; indices[at + 5] = nextI * minor + nextJ;
            }
            var mesh = new Mesh { name = "低面环形 48x6", vertices = vertices, triangles = indices }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static GameObject LoadPrefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : throw new InvalidOperationException("缺少效果Prefab：" + path);
        private static void RequireEditMode() { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。"); }
        private static void RequireCleanScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("预览不会覆盖未保存场景，请先保存当前修改。");
        }

        [MenuItem("Tools/NarutoVR/技能展示/打开火球靶场")]
        public static void OpenFireball() => Open(0);
        [MenuItem("Tools/NarutoVR/技能展示/打开护盾试炼")]
        public static void OpenShield() => Open(1);
        [MenuItem("Tools/NarutoVR/技能展示/打开组合施法试炼")]
        public static void OpenCombo() => Open(2);
        private static void Open(int index) { RequireEditMode(); RequireCleanScenes(); if (!File.Exists(ScenePaths[index])) BuildAll(); EditorSceneManager.OpenScene(ScenePaths[index]); }

        [MenuItem("Tools/NarutoVR/技能展示/生成三张场景构图预览")]
        public static void PreviewAll()
        {
            RequireEditMode(); RequireCleanScenes(); var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in ScenePaths)
                { if (!File.Exists(path)) throw new InvalidOperationException("请先BuildAll：" + path); var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single); RenderPreview(scene); }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static string ScenePreview(string scenePath)
        {
            RequireEditMode(); RequireCleanScenes();
            if (!ScenePaths.Contains(scenePath)) throw new ArgumentException("预览仅接受三个展示场景路径。", nameof(scenePath));
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try { return RenderPreview(EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)); }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        private static string RenderPreview(Scene scene)
        {
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SkillInputController>(true)).Single();
            var hud = controller.statusText ? controller.statusText.canvas : null;
            bool hudEnabled = hud && hud.enabled;
            var particles = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ParticleSystem>(true)).Where(x => x.gameObject.activeInHierarchy).ToArray();
            var go = new GameObject("仅验收构图的临时相机"); var camera = go.AddComponent<Camera>();
            var texture = new RenderTexture(1600, 900, 24); var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                if (hud) hud.enabled = false;
                foreach (var particle in particles) { particle.Simulate(1.5f, false, true, true); particle.Pause(false); }
                camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.transform.position = new Vector3(7, 5, -7); camera.transform.LookAt(new Vector3(0, 1, 4));
                camera.fieldOfView = 57; camera.aspect = 1600f / 900; camera.nearClipPlane = .1f; camera.farClipPlane = 70;
                camera.clearFlags = CameraClearFlags.SolidColor;
                var sourceCamera = controller.effects.head ? controller.effects.head.GetComponent<Camera>() : null;
                camera.backgroundColor = sourceCamera ? sourceCamera.backgroundColor : new Color(.01f, .02f, .05f);
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); string path = "Logs/preview-" + Path.GetFileNameWithoutExtension(scene.path) + ".png";
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log("[技能展示] 静态构图预览（非Quest性能测量）：" + path); return path;
            }
            finally
            {
                foreach (var particle in particles) if (particle) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (hud) hud.enabled = hudEnabled;
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
