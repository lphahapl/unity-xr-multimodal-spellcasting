using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Creates an independent RPG copy once. Existing scenes and generated user-owned assets are never rebuilt.</summary>
    public static class LinearRpgSceneSetup
    {
        public const string ScenePath = "Assets/Scene/LinearRpgTest.unity";
        public const string ConfigurationRoot = "Assets/ScriptableObjects";
        internal const string FireballEffectPath = ConfigurationRoot + "/投射物效果配置/火球术投射物.asset";
        const string SkillFolder = ConfigurationRoot + "/技能定义配置/";
        public const string VoiceConfigurationPath = "Assets/Configuration/LinearRpg/skill-voice-config.json";
        const string SourceScenePath = "Assets/Scene/FireballRange.unity";
        const float GridSpacing = 1.5f;

        sealed class Configuration
        {
            public SkillCatalog catalog;
            public SkillShopCatalog shop;
            public RpgPlayerDefinition player;
            public RpgStageDefinition[] stages;
            public Material markerMaterial;
            public readonly List<Object> created = new List<Object>();
        }

        [MenuItem("Tools/NarutoVR/线性RPG/创建或打开测试场景")]
        public static void Build()
        {
            RequireCleanEditMode();
            if (File.Exists(ScenePath))
            {
                Scene loaded = SceneManager.GetSceneByPath(ScenePath);
                if (loaded.IsValid() && loaded.isLoaded) SceneManager.SetActiveScene(loaded);
                else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[线性RPG] 已打开现有测试场景；保留场景及配置中的自定义修改。");
                return;
            }
            if (!File.Exists(SourceScenePath))
                throw new InvalidOperationException("缺少庭院模板：" + SourceScenePath + "。本构建器不会重建或修改旧展示场景。");

            var source = AssetDatabase.LoadAssetAtPath<SkillCatalog>(SkillConfigurationSetup.CatalogPath);
            if (!source) throw new InvalidOperationException("缺少原技能目录：" + SkillConfigurationSetup.CatalogPath);
            EnsureFolder("Assets/Scene");
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new InvalidOperationException("无法复制庭院模板到独立场景：" + ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Opening a scene may unload assets held only by managed stack references.
                source = AssetDatabase.LoadAssetAtPath<SkillCatalog>(SkillConfigurationSetup.CatalogPath);
                Configuration configuration = CreateConfiguration(source);
                ConfigureScene(scene, configuration);
                if (scene.path != ScenePath) throw new InvalidOperationException("拒绝保存非RPG目标场景。实际路径：" + scene.path);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("无法保存独立RPG场景：" + ScenePath);
                foreach (Object asset in configuration.created) AssetDatabase.SaveAssetIfDirty(asset);
                Debug.Log("[线性RPG] 独立庭院关卡已创建：" + ScenePath + "。编辑器播放时自动读取场景技能目录；手动启动语音服务可使用 " + VoiceConfigurationPath + "。");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("RPG场景创建未完成；未保存本次场景修改。原庭院和原配置未改动。新复制场景为 " + ScenePath + "。", error);
            }
        }

        [MenuItem("Tools/NarutoVR/线性RPG/修复未完成场景的配置引用")]
        public static void RepairIncomplete()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("仅修复当前打开的LinearRpgTest场景");
            // Keep the current in-memory construction as a reviewable scene copy, including any unsaved edits.
            string backup = "Assets/Scene/LinearRpgTest_BeforeReferenceRepair_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new InvalidOperationException("无法备份当前新场景");
            var config = CreateConfiguration(AssetDatabase.LoadAssetAtPath<SkillCatalog>(SkillConfigurationSetup.CatalogPath));
            var sessions = Components<LinearRpgController>(scene);
            if (sessions.Length == 0) ConfigureScene(scene, config);
            else
            {
                if (sessions.Length != 1) throw new InvalidOperationException("场景主控重复，请检查备份");
                var session = sessions[0];
                session.playerDefinition = config.player; session.stages = config.stages;
                session.skills.catalog = config.catalog; session.skills.skills = new List<SkillDefinition>(config.catalog.skills);
                foreach (var node in Components<NavigationNode>(scene))
                    if (node.TryGetComponent<LineRenderer>(out var line)) line.sharedMaterial = config.markerMaterial;
                string json = SkillConfigurationSetup.BuildVoiceJson(config.catalog, session.skills.voice.receivePort, session.skills.voice.servicePort);
                if (!File.Exists(VoiceConfigurationPath))
                {
                    EnsureFolder(Path.GetDirectoryName(VoiceConfigurationPath));
                    File.WriteAllText(VoiceConfigurationPath, json, new UTF8Encoding(false));
                }
                SkillConfigurationSetup.ValidateController(session.skills);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("修复后保存失败");
            foreach (var asset in config.created) AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log("[线性RPG] 已重绑独立配置，原始内存场景副本：" + backup);
        }

        static void RequireCleanEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请停止播放模式，再创建或打开线性RPG测试场景。");
            var dirty = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) dirty.Add(string.IsNullOrEmpty(scene.path) ? scene.name + "（未命名场景）" : scene.path);
            }
            if (dirty.Count != 0)
                throw new InvalidOperationException("以下场景有未保存修改；构建器不会自动保存或丢弃，请先自行处理：\n" + string.Join("\n", dirty));
        }

        static Configuration CreateConfiguration(SkillCatalog source)
        {
            var result = new Configuration();
            EnsureFolder(ConfigurationRoot);
            SkillDefinition zi = RequireSkill(source, "buff.zi");
            SkillDefinition voice = RequireSkill(source, "buff.voice");
            SkillDefinition fire = RequireSkill(source, "fireball");
            if (!(fire.effectDefinition is ProjectileEffectDefinition projectile) ||
                !(zi.effectDefinition is ShieldEffectDefinition shield) || voice.effectDefinition != shield)
                throw new InvalidOperationException("原目录的火球应引用投射物效果，子印和护体咒应共用同一护盾效果。");

            var sharedShield = Clone(shield, ConfigurationRoot + "/护盾效果配置/子印与护体咒共用护盾.asset", null, result);
            var fireEffect = Clone(projectile, FireballEffectPath, null, result);
            var hiddenEffect = Clone(shield, ConfigurationRoot + "/护盾效果配置/金刚护体强化护盾.asset",
                copy => copy.capacity = 150, result);
            SkillDefinition CopySkill(SkillDefinition original, string fileName, string id,
                SkillEffectDefinition effect, Action<SkillDefinition> configure)
            {
                string path = SkillFolder + fileName;
                var skill = Clone(original, path, copy =>
                {
                    copy.skillId = id;
                    copy.effectDefinition = effect;
                    configure(copy);
                }, result);
                if (skill.skillId != id || skill.effectDefinition != effect)
                    throw new InvalidOperationException($"技能资源占用冲突：{path} 应为 {id}，并引用 {AssetDatabase.GetAssetPath(effect)}。");
                return skill;
            }
            void ConfigureShield(SkillDefinition skill)
            {
                skill.manaCost = 15;
                skill.cooldownGroup = "shield";
            }
            SkillDefinition copiedZi = CopySkill(zi, "子印加持.asset", "buff.zi", sharedShield, ConfigureShield);
            SkillDefinition copiedVoice = CopySkill(voice, "护体咒加持.asset", "buff.voice", sharedShield, ConfigureShield);
            SkillDefinition copiedFire = CopySkill(fire, "火球术.asset", "fireball", fireEffect, copy => copy.manaCost = 20);
            SkillDefinition hidden = CopySkill(voice, "金刚护体.asset", "buff.hidden", hiddenEffect, copy =>
            {
                copy.displayName = "金刚护体";
                copy.triggerMode = SkillTriggerMode.VoiceOnly;
                copy.phrase = "金刚护体";
                copy.phraseTokens = "j īn g āng h ù t ǐ";
                copy.sealName = string.Empty;
                copy.effectKind = SkillEffectKind.SelfBuff;
                copy.manaCost = 25;
                copy.cooldownSeconds = 8;
                copy.cooldownGroup = "shield.hidden";
            });
            result.catalog = Clone(source, ConfigurationRoot + "/技能目录配置/线性RPG技能目录.asset", copy =>
                copy.skills = new List<SkillDefinition> { copiedZi, copiedVoice, copiedFire, hidden }, result);
            result.player = NewAsset<RpgPlayerDefinition>(ConfigurationRoot + "/玩家定义配置/默认玩家.asset", null, result);
            result.stages = new[]
            {
                Stage("trial.01", "火球基础", "寅印与“火球术”匹配后，右手轻推发射。击破本阶段训练靶，再前往下一阶段。", 40,
                    new[] { "buff.zi", "buff.voice" }, false, result),
                Stage("trial.02", "护盾与闪避", "子印或“护体咒”施放护盾；右手张掌短划闪避。击破训练靶并应对训练攻击。", 40,
                    Array.Empty<string>(), true, result),
                Stage("trial.03", "综合试炼", "用导航、护盾、闪避与火球完成最后的训练；可在电脑菜单购买隐藏法术及升级火球。", 60,
                    Array.Empty<string>(), true, result)
            };
            result.shop = NewAsset<SkillShopCatalog>(ConfigurationRoot + "/技能商店配置/训练场技能商店.asset", shop =>
            {
                shop.offers = new[] {
                    new SkillShopOffer { offerId="unlock.hidden", displayName="购买隐藏术式", skillId="buff.hidden", kind=SkillOfferKind.Unlock, cost=60, requiredStage=result.stages[0] },
                    new SkillShopOffer { offerId="upgrade.fireball", displayName="升级火球", skillId="fireball", kind=SkillOfferKind.Upgrade, cost=30, maximumLevel=3 }
                };
            }, result);
            result.markerMaterial = NewAsset<Material>("Assets/Materials/LinearRpg/导航点标记.mat", null, result,
                () =>
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                    if (!shader) throw new InvalidOperationException("缺少用于节点标记的Unlit着色器。");
                    var material = new Material(shader);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.12f, 0.82f, 0.95f, 1));
                    if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.12f, 0.82f, 0.95f, 1));
                    return material;
                });
            ValidateConfiguration(result.catalog, source, copiedZi, copiedVoice, copiedFire, hidden);
            foreach (Object asset in result.created) AssetDatabase.SaveAssetIfDirty(asset);
            return result;
        }

        static RpgStageDefinition Stage(string id, string name, string instructions, int reward,
            string[] unlocks, bool hazards, Configuration configuration)
        {
            string folder = ConfigurationRoot + "/关卡阶段配置/";
            string path = folder + name + ".asset";
            // 已发布的训练场按 GUID 引用旧阶段；只在原文件确实存在时沿用它。
            string legacyPath = folder + "阶段_" + id + ".asset";
            if (File.Exists(legacyPath))
            {
                if (File.Exists(path)) throw new InvalidOperationException($"训练阶段有两个配置文件：{legacyPath}、{path}");
                path = legacyPath;
            }
            var definition = NewAsset<RpgStageDefinition>(path, stage =>
            {
                stage.stageId = id;
                stage.displayName = name;
                stage.instructions = instructions;
                stage.rewardCurrency = reward;
                stage.unlockSkillIds = unlocks;
                stage.enableHazards = hazards;
            }, configuration);
            if (definition.stageId != id) throw new InvalidOperationException($"阶段配置 {path} 的 ID 与 {id} 不符");
            return definition;
        }

        static SkillDefinition RequireSkill(SkillCatalog source, string id)
        {
            SkillDefinition result = source.skills == null ? null : source.skills.SingleOrDefault(s => s && s.skillId == id);
            if (!result) throw new InvalidOperationException("原技能目录缺少配方：" + id);
            return result;
        }

        static void ValidateConfiguration(SkillCatalog catalog, SkillCatalog source, params SkillDefinition[] required)
        {
            List<string> errors = catalog.Validate();
            foreach (SkillDefinition skill in required)
                if (catalog.skills == null || !catalog.skills.Contains(skill))
                    errors.Add($"RPG目录缺少专用配方 {skill.skillId}：{AssetDatabase.GetAssetPath(skill)}");
            if (catalog.skills != null)
            {
                foreach (SkillDefinition skill in catalog.skills.Where(item => item))
                    if (!AssetDatabase.Contains(skill) || !skill.effectDefinition || !AssetDatabase.Contains(skill.effectDefinition) ||
                        source.skills.Any(original => original && (original == skill || original.effectDefinition == skill.effectDefinition)))
                        errors.Add("RPG配方及效果必须引用独立资产：" + skill.skillId);
            }
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        static T Clone<T>(T source, string path, Action<T> prepare, Configuration configuration) where T : Object
        {
            return NewAsset(path, prepare, configuration, () => Object.Instantiate(source));
        }

        static T NewAsset<T>(string path, Action<T> prepare, Configuration configuration, Func<T> factory = null) where T : Object
        {
            Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing)
            {
                if (existing is T typed) return typed;
                throw new InvalidOperationException("已有资源类型与构建需求冲突；不会覆盖：" + path);
            }
            if (File.Exists(path)) throw new InvalidOperationException("资源文件已存在但无法加载；不会覆盖：" + path);
            T created = factory != null ? factory() : ScriptableObject.CreateInstance(typeof(T)) as T;
            if (!created) throw new InvalidOperationException("不能创建配置资源：" + path);
            created.name = Path.GetFileNameWithoutExtension(path);
            prepare?.Invoke(created);
            EnsureFolder(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(created, path);
            configuration.created.Add(created);
            return created;
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (path == "Assets" || AssetDatabase.IsValidFolder(path)) return;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains(".."))
                throw new ArgumentException("生成路径必须在项目Assets内。", nameof(path));
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new InvalidOperationException("无法创建配置目录：" + path);
        }

        static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static void ConfigureScene(Scene scene, Configuration configuration)
        {
            SkillInputController skills = Components<SkillInputController>(scene).Single();
            if (!skills.effects || !skills.lab || !skills.voice || !skills.view)
                throw new InvalidOperationException("庭院模板缺少既有技能、手部、语音或界面接线。");
            Transform head = skills.effects.head;
            Transform rig = skills.effects.playerRoot;
            if (!head || !rig || head == rig || !head.IsChildOf(rig))
                throw new InvalidOperationException("庭院模板必须显式配置同一Rig中的头部与玩家根节点；构建器不会替换Rig。");
            Font font = skills.chineseFont ? skills.chineseFont : skills.view.chineseFont;
            if (!font) throw new InvalidOperationException("庭院模板缺少中文字体。");

            foreach (SkillScenario scenario in Components<SkillScenario>(scene))
            {
                if (scenario.worldText && scenario.worldText.canvas &&
                    (!skills.statusText || scenario.worldText.canvas != skills.statusText.canvas))
                    Object.DestroyImmediate(scenario.worldText.canvas.gameObject);
                Object.DestroyImmediate(scenario);
            }
            foreach (Transform transform in Components<Transform>(scene).Where(t => t &&
                (t.name == "试炼说明" || t.name.Contains("墙后靶"))).OrderByDescending(Depth).ToArray())
                if (transform) Object.DestroyImmediate(transform.gameObject);

            skills.catalog = configuration.catalog;
            skills.skills = new List<SkillDefinition>(configuration.catalog.skills);
            skills.enableOpenHandCancel = false;
            skills.showDesktopControls = false;
            skills.view.showDesktopControls = false;
            skills.view.shortcuts.Clear();
            skills.view.title = "结印与语音 · 实时输入诊断";
            skills.view.maximumSkillRows = 4;
            skills.view.controller = skills;
            skills.view.chineseFont = font;
            skills.effects.playerRoot = rig;
            skills.effects.head = head;
            NarutoVR.Editor.OfficialHandVisualSetup.Install(skills.lab);

            SkillTrainingTarget[] targets = Components<SkillTrainingTarget>(scene)
                .Where(t => t && t.transform.position.z > rig.position.z)
                .OrderBy(t => t.transform.position.z).ToArray();
            if (targets.Length != 3) throw new InvalidOperationException("移除墙后靶后应恰好保留三个前方训练靶，实际为：" + targets.Length);
            foreach (SkillTrainingTarget target in targets)
            { target.health = 60; target.resetAfterSeconds = 0; }

            Collider floor = Components<Collider>(scene).SingleOrDefault(c => c && c.enabled && !c.isTrigger && c.name == "庭院石台");
            if (!floor) throw new InvalidOperationException("缺少庭院石台真实地面碰撞体，拒绝猜测导航楼层。");
            Physics.SyncTransforms();
            Vector3 origin = FloorAt(floor, rig.position);
            var root = new GameObject("RPG 会话与玩家");
            SceneManager.MoveGameObjectToScene(root, scene);
            var movementRoot = new GameObject("移动与导航");
            movementRoot.transform.SetParent(root.transform, false);
            RpgLocomotionController locomotion = movementRoot.AddComponent<RpgLocomotionController>();
            locomotion.useRayNavigation=true;
            locomotion.rigRoot = rig; locomotion.head = head;
            locomotion.rigFloorOffset = origin.y - rig.position.y;
            locomotion.floorLayers = 1 << floor.gameObject.layer;
            locomotion.obstacleLayers = ~(1 << 2);
            NavigationNode[,] grid = CreateGrid(movementRoot.transform, origin, floor, locomotion, configuration.markerMaterial);
            var controller = root.AddComponent<LinearRpgController>();
            var trainingRoot = new GameObject("章节与训练");
            trainingRoot.transform.SetParent(root.transform, false);
            var training = trainingRoot.AddComponent<TrainingChapterFlow>();
            controller.chapter = training;
            training.rpg = controller;
            training.initialSkills = new[] { "fireball" };
            controller.handPoseSettings = OfficialHandPoseSetup.EnsureProfile();
            controller.skills = skills;
            controller.locomotion = locomotion;
            controller.playerDefinition = configuration.player;
            controller.stages = configuration.stages;
            training.TargetGroup.targets = targets;
            training.TargetGroup.healthByStage = new[] {60f, 90f, 120f};
            controller.checkpointNodes = new[] { grid[1, 1], grid[1, 2], grid[2, 2] };
            controller.autoLoad = true;
            controller.lowHandsBelowHead = 0.55f;
            controller.saveFileName = "linear-rpg-v2.json";
            var growthRoot = new GameObject("成长与物品");
            growthRoot.transform.SetParent(root.transform, false);
            var shop = growthRoot.AddComponent<RpgSkillShop>();
            shop.catalog = configuration.shop;
            var hazard = trainingRoot.AddComponent<RpgTrainingHazard>();
            hazard.controller = controller;
            hazard.interval = 6;
            hazard.warningSeconds = 1.5f;
            hazard.radius = 0.65f;
            hazard.damage = 25;

            var view = RpgInteractionSetup.PresentationGroup(controller).AddComponent<RpgSessionView>();
            view.controller = controller;
            view.chineseFont = font;
            view.showDesktopControls = true;
            view.statusText = WorldHud(view.transform, rig.position, font);
            RpgInteractionSetup.Configure(controller);
            Selection.activeObject = root;

            // Independent service configuration; never replace the original training service's file.
            string voiceJson = SkillConfigurationSetup.BuildVoiceJson(configuration.catalog, skills.voice.receivePort, skills.voice.servicePort);
            if (!File.Exists(VoiceConfigurationPath))
            {
                EnsureFolder(Path.GetDirectoryName(VoiceConfigurationPath));
                File.WriteAllText(VoiceConfigurationPath, voiceJson, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(VoiceConfigurationPath, ImportAssetOptions.ForceSynchronousImport);
            }
            else Debug.Log("[线性RPG] 保留已有独立语音配置：" + VoiceConfigurationPath);
            SkillConfigurationSetup.ValidateController(skills);
        }

        static int Depth(Transform value)
        {
            int result = 0; while (value.parent) { result++; value = value.parent; } return result;
        }

        static Vector3 FloorAt(Collider floor, Vector3 position)
        {
            if (!floor.Raycast(new Ray(position + Vector3.up * 4, Vector3.down), out RaycastHit hit, 8) ||
                Vector3.Angle(hit.normal, Vector3.up) > 35)
                throw new InvalidOperationException("预设导航点不在合法庭院地面上：" + position);
            return hit.point;
        }

        static NavigationNode[,] CreateGrid(Transform parent, Vector3 origin, Collider floor,
            RpgLocomotionController locomotion, Material markerMaterial)
        {
            var result = new NavigationNode[3, 3];
            for (int z = 0; z < 3; z++) for (int x = 0; x < 3; x++)
            {
                var item = new GameObject("导航点 " + x + "-" + z);
                item.transform.SetParent(parent, false);
                item.transform.position = FloorAt(floor, origin + new Vector3((x - 1) * GridSpacing, 0, (z - 1) * GridSpacing));
                NavigationNode node = item.AddComponent<NavigationNode>();
                node.nodeId = "courtyard." + x + "." + z;
                node.overrideLayers = true;
                node.floorLayers = locomotion.floorLayers;
                node.obstacleLayers = locomotion.obstacleLayers;
                result[x, z] = node;
                var line = item.AddComponent<LineRenderer>();
                line.sharedMaterial = markerMaterial;
                line.useWorldSpace = false; line.loop = true;
                line.widthMultiplier = 0.018f;
                line.positionCount = 24;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / line.positionCount;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.18f, 0.055f, Mathf.Sin(angle) * 0.18f));
                }
            }
            for (int z = 0; z < 3; z++) for (int x = 0; x < 3; x++)
            {
                if (x > 0) result[x, z].neighbors.Add(result[x - 1, z]);
                if (x < 2) result[x, z].neighbors.Add(result[x + 1, z]);
                if (z > 0) result[x, z].neighbors.Add(result[x, z - 1]);
                if (z < 2) result[x, z].neighbors.Add(result[x, z + 1]);
            }
            return result;
        }

        static Text WorldHud(Transform parent, Vector3 playerOrigin, Font font)
        {
            var panel = new GameObject("线性RPG · 世界状态面板", typeof(RectTransform), typeof(Canvas));
            panel.transform.SetParent(parent, false);
            panel.transform.position = playerOrigin + new Vector3(2.65f, 1.75f, 1.75f);
            panel.transform.localScale = Vector3.one * 0.00115f;
            Vector3 normal = panel.transform.position - playerOrigin; normal.y = 0;
            panel.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 1280);
            var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
            var background = new GameObject("状态面板底色", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(panel.transform, false);
            Stretch(background.GetComponent<RectTransform>(), 0);
            background.GetComponent<Image>().color = new Color(0.015f, 0.03f, 0.06f, 0.9f);
            background.GetComponent<Image>().raycastTarget = false;

            Text status = Label(panel.transform, "关卡实时状态", font, 27);
            var statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0, 0.3f); statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(30, 20); statusRect.offsetMax = new Vector2(-30, -30);
            status.text = "线性RPG · 火球基础\n\n播放后显示生命、能量、护盾、关卡、导航与存档状态。\n电脑右侧菜单均为显式模拟输入。";
            Text help = Label(panel.transform, "导航与施法说明", font, 25);
            help.rectTransform.anchorMin = Vector2.zero; help.rectTransform.anchorMax = new Vector2(1, 0.3f);
            help.rectTransform.offsetMin = new Vector2(30, 20); help.rectTransform.offsetMax = new Vector2(-30, -10);
            help.color = new Color(0.4f, 0.9f, 1);
            help.text = "左食指指向落点，短暂停留后传送；移开或放松后再次选择。放下左手退出导航。\n" +
                "导航准备期间，右手张掌短划闪避；持火球前推优先发射，侧后闪保留手中火球。\n" +
                "双手放低保持取消待施法与未发射物；已生效护盾和飞行火球保留。\n真实语音需加载本关卡独立语音配置。";
            return status;
        }

        static Text Label(Transform parent, string name, Font font, int size)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            Text text = item.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.color = Color.white; text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * padding; rect.offsetMax = Vector2.one * -padding;
        }
    }
}
