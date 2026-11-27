using System;
using System.IO;
using System.Linq;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NarutoVR.SealCombat.Editor
{
    public static class GlyphLabSetup
    {
        public const string ScenePath = "Assets/Scene/GlyphLab.unity";
        const string AssetFolder = "Assets/Art/Glyphs";

        [MenuItem("Tools/NarutoVR/画符/打开墨规笔试验场")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式打开墨规笔试验场");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                Verify(EditorSceneManager.OpenScene(ScenePath));
                return;
            }
            BuildDraft();
        }

        // Explicit batch rebuild for the generated lab. The normal menu preserves the existing scene.
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出播放模式");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
                Verify(EditorSceneManager.OpenScene(ScenePath), checkStageBindings: false);
            BuildDraft();
        }

        static void BuildDraft()
        {
            // Only publish the finished, checked scene. An interrupted build can retry without
            // mistaking a raw LinearRpgTest clone for a playable GlyphLab.
            string temporaryScene = $"Assets/Scene/GlyphLabBuild_{Guid.NewGuid():N}.unity";
            if (!AssetDatabase.CopyAsset(LinearRpgSceneSetup.ScenePath, temporaryScene))
                throw new IOException("无法创建墨规笔试验场副本");
            try
            {
                Scene draft = EditorSceneManager.OpenScene(temporaryScene);
                Configure(draft);
                Verify(draft);
                if (!EditorSceneManager.SaveScene(draft, ScenePath))
                    throw new IOException("墨规笔试验场保存失败");
            }
            finally
            {
                if (SceneManager.GetActiveScene().path == temporaryScene)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(temporaryScene);
            }
            Debug.Log("[GlyphLab] 已接入裸手玩家、千钧符、两块物理镇石和方矩铜栅：" + ScenePath);
        }

        static void Verify(Scene scene, bool checkStageBindings = true)
        {
            var roots = scene.GetRootGameObjects();
            var pen = roots.SelectMany(go => go.GetComponentsInChildren<GlyphPenController>()).Single();
            var story = roots.SelectMany(go => go.GetComponentsInChildren<GlyphStoryPanel>()).Single();
            var rpg = pen.GetComponent<LinearRpgController>();
            var weights = roots.SelectMany(go => go.GetComponentsInChildren<GlyphHeavyBody>()).ToArray();
            if (!rpg || !rpg.chapter || !rpg.chapter.isActiveAndEnabled || !rpg.locomotion ||
                !rpg.CheckpointAt(0).gameObject.activeInHierarchy || rpg.StageAt(0).startsInCombat ||
                pen.activeTemplates == null || !pen.activeTemplates.Any(t => t && t.effect is QianjunEffectDefinition) ||
                !pen.penRoot || !pen.nib || !story.template || !story.board || story.onCompleted.GetPersistentEventCount() < 2 ||
                !weights.Any(w => w.localAxis == Vector3.zero) || !weights.Any(w => w.localAxis != Vector3.zero))
                throw new InvalidOperationException("墨规笔试验场接线不完整：" + scene.path);
            if (checkStageBindings)
                foreach (var shop in rpg.GetComponentsInChildren<RpgSkillShop>(true))
                    shop.ValidateConfiguration(rpg);
        }

        internal static void BindApprovedPen(GlyphPenController pen)
        {
            if (pen.penRoot && pen.nib) return;
            const string path = "Assets/Art/Props/MoGuiPen/MoGuiPen.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset || !asset.transform.Find("Nib"))
                throw new InvalidOperationException("正式墨规笔或笔尖资源缺失：" + path);
            var model = pen.penRoot && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pen.penRoot) == path
                ? pen.penRoot.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(asset, pen.transform);
            pen.penRoot = model.transform;
            pen.nib = model.transform.Find("Nib");
            PrefabUtility.RecordPrefabInstancePropertyModifications(pen);
            EditorUtility.SetDirty(pen);
        }

        static void Configure(Scene scene)
        {
            var player = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<LinearRpgController>(true)).Single();
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Glyphs");

            var qianjun = Asset<GlyphTemplate>("Qianjun", template =>
            {
                template.symbolId = "glyph.qianjun";
                template.displayName = "千钧符";
                SetDefaultFeedback(template);
                template.effect = Asset<QianjunEffectDefinition>("QianjunTether", null);
                template.SetShape(
                    new[] { new Vector2(-.85f, .85f), new Vector2(.85f, .85f) },
                    new[] { new Vector2(0, .85f), new Vector2(0, -.2f) },
                    new[] { new Vector2(-.29f, -.2f), new Vector2(.29f, -.2f), new Vector2(.59f, -.85f),
                        new Vector2(-.59f, -.85f), new Vector2(-.29f, -.2f) });
            });
            var square = Asset<GlyphTemplate>("SquareRule", template =>
            {
                template.symbolId = "glyph.square-rule";
                template.displayName = "方直之矩";
                SetDefaultFeedback(template);
                template.SetShape(new[] { new Vector2(-.8f, .8f), new Vector2(-.8f, -.8f), new Vector2(.8f, -.8f) });
            });
            var stage = Asset<RpgStageDefinition>("WorkshopStage", definition =>
            {
                definition.stageId = "glyph.workshop";
                definition.displayName = "墨规笔试验场";
                definition.instructions = "临摹千钧符操纵镇石，再按右侧铜扣绘方矩开启铜栅。";
                definition.rewardCurrency = 0;
                definition.startsInCombat = false;
            });

            var pen = player.gameObject.AddComponent<GlyphPenController>();
            pen.activeTemplates = new[] { qianjun };
            BindApprovedPen(pen);
            var lab = new GameObject("墨规笔试验场");
            var spawn = player.CheckpointAt(0);
            lab.transform.position = spawn.transform.position;
            player.chapter.gameObject.SetActive(false);
            var flow = lab.AddComponent<ObjectiveChapterFlow>();
            var signal = lab.AddComponent<SignalChapterObjective>();
            flow.rpg = player;
            flow.stages = new[] { new ChapterObjectiveStage { stage = stage, objectives = new ChapterObjective[] { signal } } };
            flow.completedText = "铜栅已开启，可继续练习千钧符。";
            player.chapter = flow;
            player.ConfigureRoute(new[] { stage }, new[] { spawn });
            // The copied shop sells items gated by trial.01–03. The workshop has its own route
            // and no shop. Disabled modules are intentionally still validated by the RPG.
            foreach (var shop in player.GetComponentsInChildren<RpgSkillShop>(true))
                UnityEngine.Object.DestroyImmediate(shop);
            player.autoLoad = false;
            player.saveFileName = "glyph-workshop-v1.json";
            if (player.skills.voice) player.skills.voice.autoStartInEditor = false;

            // SkillShowcaseSetup places each target's sights and shooting-lane markings
            // beside the target under the courtyard. Navigation nodes have their own parent.
            var courtyard = scene.GetRootGameObjects().Single(root => root.name == "试炼场景 · FireballRange");
            foreach (Transform prop in courtyard.transform)
            {
                bool shootingProp = prop.GetComponent<SkillTrainingTarget>() ||
                    prop.name is "靶心" or "能量环" or "前进引导箭柄" or "前进引导箭翼" or
                    "左轨迹引导" or "右轨迹引导" or "场景题字" or "试炼说明" or
                    "场景装饰阵法 · 不产生领域效果" or "远景符光" ||
                    prop.name.EndsWith("标牌", StringComparison.Ordinal);
                if (shootingProp) prop.gameObject.SetActive(false);
            }
            foreach (var view in player.GetComponentsInChildren<RpgSessionView>(true))
            {
                view.showDesktopControls = false;
                if (view.statusText && view.statusText.canvas) view.statusText.canvas.gameObject.SetActive(false);
            }
            player.skills.showDesktopControls = false;
            if (player.skills.view)
            {
                player.skills.view.showDesktopControls = false;
                if (player.skills.view.statusText && player.skills.view.statusText.canvas)
                    player.skills.view.statusText.canvas.gameObject.SetActive(false);
            }

            var stone = Required<Material>("Assets/Materials/Chapter01/CourtyardStone.mat");
            var bronze = Required<Material>("Assets/Art/Environment/Opening/ConcentricGauge/Bronze/CG_A1_Bronze.mat");
            var gold = GuideMaterial();
            var font = Required<Font>("Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf");
            AddStone(lab.transform, "自由镇石", new Vector3(-.55f, .36f, 1.92f), Vector3.zero, stone);
            AddStone(lab.transform, "导轨镇石", new Vector3(.9f, .36f, 2.0f), Vector3.forward, stone);
            foreach (float offset in new[] { -.39f, .39f })
                Cube(lab.transform, "铜轨", new Vector3(.9f + offset, .035f, 2.32f), new Vector3(.075f, .07f, 2.6f), bronze);
            foreach (float along in new[] { 1.03f, 3.61f })
                Cube(lab.transform, "轨端止挡", new Vector3(.9f, .13f, along), new Vector3(.85f, .26f, .1f), bronze);
            AddGuide(lab.transform, font, qianjun, bronze, gold);
            AddStoryStation(lab.transform, player, square, signal, bronze, stone, gold, font);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void AddStone(Transform parent, string name, Vector3 position, Vector3 axis, Material material)
        {
            var cube = Cube(parent, name, position, new Vector3(.63f, .69f, .65f), material);
            var rb = cube.AddComponent<Rigidbody>();
            rb.mass = 4.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (axis != Vector3.zero) rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;
            cube.AddComponent<GlyphHeavyBody>().localAxis = axis;
        }

        static void AddGuide(Transform parent, Font font, GlyphTemplate template, Material bronze, Material gold)
        {
            var guide = Child(parent, "千钧符图谱", new Vector3(-1.55f, 1.34f, 1.75f));
            guide.localRotation = Quaternion.Euler(0, -32, 0);
            Cube(guide, "图谱底板", Vector3.zero, new Vector3(.85f, .62f, .045f), bronze);
            // A real example teaches all three strokes, including the closed weight at the bottom.
            foreach (var stroke in template.Preview())
            {
                var path = Child(guide, "图样笔画", new Vector3(-.235f, .02f, -.03f));
                var line = path.gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.sharedMaterial = gold;
                line.startWidth = line.endWidth = .009f;
                line.numCornerVertices = 3;
                line.positionCount = stroke.Length;
                for (int i = 0; i < stroke.Length; i++)
                    line.SetPosition(i, new Vector3(stroke[i].x, stroke[i].y, 0) * .153f);
            }
            Label(guide, font, "千钧符", new Vector3(0, .237f, -.032f), .68f, .085f, 36);
            Label(guide, font, "上腹轻捏取笔，停笔辨符\n笔尖指石，前推或回拉\n转身走远，牵绳拖石\n持笔手放低并停住，收笔", new Vector3(.157f, -.034f, -.032f), .465f, .37f, 25);
        }

        static void AddStoryStation(Transform parent, LinearRpgController player, GlyphTemplate template,
            SignalChapterObjective signal, Material bronze, Material stone, Material gold, Font font)
        {
            var stand = Child(parent, "方矩机关台", new Vector3(2.22f, 0, 2.6f));
            stand.localRotation = Quaternion.Euler(0, 12, 0);
            Cube(stand, "立柱", new Vector3(0,.58f,.1f), new Vector3(.23f,1.16f,.23f), stone);
            var boardRoot = Child(stand, "方矩定点画板", new Vector3(0, 1.36f, 0));
            var story = boardRoot.gameObject.AddComponent<GlyphStoryPanel>();
            story.board = boardRoot.gameObject.AddComponent<GlyphBoard>();
            story.template = template;
            var buttonRoot = Child(stand, "展开画板铜扣", new Vector3(-.27f, 1.18f, -.02f));
            var cap = Cube(buttonRoot, "铜扣", Vector3.zero, new Vector3(.085f,.085f,.027f), gold);
            var button = buttonRoot.gameObject.AddComponent<ZhunwangPressButton>();
            button.cap = cap.transform;
            button.touchRadius = .045f;
            button.handInput = player.wristMenu;
            UnityEventTools.AddPersistentListener(button.onPressed, story.Show);
            Label(stand, font, "右食指按铜扣 · 展开方矩板", new Vector3(0, .98f, -.05f), .71f, .095f, 26);

            var frame = Child(parent, "试验铜栅", new Vector3(2.15f, 0, 4.0f));
            foreach (float side in new[] { -.74f,.74f })
                Cube(frame, "石门柱", new Vector3(side,1.05f,0), new Vector3(.17f,2.1f,.2f), stone);
            Cube(frame, "横梁", new Vector3(0, 2.13f, 0), new Vector3(1.65f, .14f, .22f), bronze);
            var gate = Child(frame, "方矩联动铜栅", Vector3.zero).gameObject;
            for (int i = -3; i <= 3; i++)
                Cube(gate.transform, "竖栅", new Vector3(.19f*i,1.05f,0), new Vector3(.06f,2.1f,.09f), bronze);
            Cube(gate.transform, "铜栅横撑", new Vector3(0,.79f,0), new Vector3(1.4f,.08f,.11f), bronze);
            UnityEventTools.AddBoolPersistentListener(story.onCompleted, gate.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(story.onCompleted, buttonRoot.gameObject.SetActive, false);
            UnityEventTools.AddPersistentListener(story.onCompleted, signal.Complete);
        }

        static Transform Child(Transform parent, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            return go.transform;
        }

        static GameObject Cube(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void Label(Transform parent, Font font, string content, Vector3 at, float width, float height, int fontSize)
        {
            var go = new GameObject("说明", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            const float metresPerPixel = .001f;
            go.transform.localScale = Vector3.one * metresPerPixel;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height) / metresPerPixel;
            var text = new GameObject("文字", typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.color = new Color(1f,.94f,.79f);
            text.text = content;
        }

        static T Required<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("缺少试验场资源：" + path);

        static void SetDefaultFeedback(GlyphTemplate template)
        {
            template.completionVfx = Required<GameObject>(AssetFolder + "/GlyphGoldDust.prefab")
                .GetComponent<ParticleSystem>();
            template.completionSfx = Required<AudioClip>("Assets/Art/VFX/Skills/SealConfirmation/LvChime.wav");
        }

        static Material GuideMaterial()
        {
            const string path = AssetFolder + "/GuideInk.mat";
            var previous = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (previous) return previous;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                { color = new Color(1f,.63f,.19f) };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static T Asset<T>(string name, Action<T> configure) where T : ScriptableObject
        {
            string path = AssetFolder + "/" + name + ".asset";
            var previous = AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous) return previous;
            var value = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(value);
            AssetDatabase.CreateAsset(value, path);
            AssetDatabase.SaveAssetIfDirty(value);
            return value;
        }
    }
}
