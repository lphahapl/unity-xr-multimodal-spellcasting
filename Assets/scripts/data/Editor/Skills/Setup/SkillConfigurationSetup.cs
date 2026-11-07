using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>创建和校验技能目录及效果配置，提供旧字段迁移与本机语音词表导出入口。</summary>
    public static class SkillConfigurationSetup
    {
        public const string Root="Assets/ScriptableObjects";
        public const string CatalogPath=Root+"/技能目录配置/试炼技能目录.asset";
        public const string VoicePath="Tools/SkillVoice/skill-voice-config.json";
        public static string[] ScenePaths=>new[]{SkillTestSceneSetup.ScenePath}.Concat(SkillShowcaseSetup.ScenePaths).ToArray();
        [Serializable] private sealed class VoiceExport
        {
            public int schema=1,unityPort,servicePort;
            public string cancelPhrase;
            public Keyword[] keywords;
        }
        [Serializable] private sealed class Keyword { public string phrase,tokens; }

        private static readonly string[] DefaultNames = { "子印加持", "护体咒加持", "火球术" };
        private const string RepairMarker = "Assets/Configuration/initial-migration-repaired.json";

        // New defaults use independent skill copies. Never attach an effect to a shared legacy
        // SkillDefinition: its different scenes may still use different scene-level parameters.
        public static SkillCatalog EnsureDefaults()
        {
            EnsureFolder(Root + "/技能目录配置");
            EnsureFolder(Root + "/护盾效果配置");
            EnsureFolder(Root + "/投射物效果配置");
            var shield = AssetDatabase.LoadAssetAtPath<ShieldEffectDefinition>(Root + "/护盾效果配置/默认护盾.asset");
            if (!shield)
            {
                shield = ScriptableObject.CreateInstance<ShieldEffectDefinition>();
                shield.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ShieldPath);
                AssetDatabase.CreateAsset(shield, Root + "/护盾效果配置/默认护盾.asset");
            }
            var projectile = AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>(Root + "/投射物效果配置/默认火球.asset");
            if (!projectile)
            {
                projectile = ScriptableObject.CreateInstance<ProjectileEffectDefinition>();
                projectile.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.FireballPath);
                projectile.impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ImpactPath);
                AssetDatabase.CreateAsset(projectile, Root + "/投射物效果配置/默认火球.asset");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<SkillCatalog>();
                foreach (var source in LoadLegacyDefaults())
                {
                    var effect = source.effectDefinition ? source.effectDefinition :
                        source.effectKind == SkillEffectKind.SelfBuff ? (SkillEffectDefinition)shield : projectile;
                    catalog.skills.Add(CopySkill(source, Root + "/技能定义配置/默认配置/" + source.name + ".asset", effect));
                }
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static SkillDefinition[] LoadLegacyDefaults() => DefaultNames.Select(name =>
            AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/ScriptableObjects/技能定义配置/历史配置/" + name + ".asset") ??
            throw new InvalidOperationException("缺少原技能资产：" + name + "；请先创建基础技能测试场景。")).ToArray();

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (path != "Assets" && (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..")))
                throw new ArgumentException("配置资源路径必须位于 Assets。", nameof(path));
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static SkillDefinition CopySkill(SkillDefinition source, string path, SkillEffectDefinition effect)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (existing) return existing; // Generated paths become user-owned configuration after creation.
            EnsureFolder(Path.GetDirectoryName(path));
            var copy = UnityEngine.Object.Instantiate(source); copy.name = Path.GetFileNameWithoutExtension(path);
            copy.effectDefinition = effect;
            if (string.IsNullOrWhiteSpace(copy.phraseTokens))
            {
                string label = SkillPhrase.Normalize(copy.phrase);
                if (label == "护体咒") copy.phraseTokens = "h ù t ǐ zh òu";
                if (label == "火球术") copy.phraseTokens = "h uǒ q iú sh ù";
            }
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }
        [MenuItem("Tools/NarutoVR/技能配置/迁移现有技能场景")]
        public static void MigrateMenu()=>Debug.Log($"[技能配置] 迁移完成：本次更新 {Migrate()} 个场景。");
        public static int Migrate()
        {
            RequireCleanEditor();var catalog=EnsureDefaults();int changed=0;
            foreach(string path in ScenePaths)
            {
                if(!File.Exists(path))continue;
                var scene=SceneManager.GetSceneByPath(path);bool wasOpen=scene.IsValid()&&scene.isLoaded;
                if(!wasOpen)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var controller=Controller(scene);
                    if(Bind(controller,catalog))
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if(!EditorSceneManager.SaveScene(scene))throw new Exception("保存迁移失败："+path);
                        changed++;
                    }
                }
                finally{if(!wasOpen)EditorSceneManager.CloseScene(scene,true);}
            }
            AssetDatabase.SaveAssets();return changed;
        }
        /// <summary>One-time conversion only. Existing catalogs, effect assets and populated view fields are user-owned.</summary>
        public static bool Bind(SkillTestController controller, SkillCatalog fallback = null, string migrationRoot = null)
        {
            if (!controller) throw new ArgumentNullException(nameof(controller));
            bool changed = false;
            if (!controller.catalog)
            {
                fallback = fallback ? fallback : EnsureDefaults();
                var source = controller.skills ?? new List<SkillDefinition>();
                if (source.Count == 0 || CanShareCatalog(source, controller.effects, fallback)) controller.catalog = fallback;
                else controller.catalog = MigrateLegacyCatalog(controller, source, migrationRoot ?? Root + "/迁移");
                changed = true;
            }
            var view = controller.view ? controller.view : controller.GetComponent<SkillTestView>();
            if (!view)
            {
                view = controller.gameObject.AddComponent<SkillTestView>();
                view.showDesktopControls = controller.showDesktopControls;
                view.AssignDefaultShortcuts(controller.ConfiguredSkills); changed = true;
            }
            // A component added manually may be empty. Fill its missing fields from the old
            // serialized references before synchronizing compatibility references back.
            if (!view.statusText && controller.statusText) { view.statusText = controller.statusText; changed = true; }
            if (!view.chineseFont && controller.chineseFont) { view.chineseFont = controller.chineseFont; changed = true; }
            if (controller.view != view) { controller.view = view; changed = true; }
            if (view.controller != controller) { view.controller = controller; changed = true; }
            if (controller.statusText != view.statusText) { controller.statusText = view.statusText; changed = true; }
            if (controller.chineseFont != view.chineseFont) { controller.chineseFont = view.chineseFont; changed = true; }
            // Migrated recipes are copies, while existing shortcut bindings still point at the
            // original assets. Resolve only unique stable IDs and retain the user's chosen keys.
            if (view.shortcuts != null && controller.ConfiguredSkills != null)
                foreach (var shortcut in view.shortcuts)
                {
                    if (shortcut == null || !shortcut.skill || controller.ConfiguredSkills.Contains(shortcut.skill) ||
                        string.IsNullOrWhiteSpace(shortcut.skill.skillId)) continue;
                    var matches = controller.ConfiguredSkills.Where(skill => skill && skill.skillId == shortcut.skill.skillId).ToArray();
                    if (matches.Length != 1) continue;
                    shortcut.skill = matches[0]; changed = true;
                }
            if (changed) { EditorUtility.SetDirty(controller); EditorUtility.SetDirty(view); }
            return changed;
        }

        private static bool CanShareCatalog(IReadOnlyList<SkillDefinition> source, SkillEffects legacy, SkillCatalog target)
        {
            if (target.skills == null || source.Count != target.skills.Count) return false;
            for (int i = 0; i < source.Count; i++)
            {
                var old = source[i]; var candidate = target.skills[i];
                if (!SameRecipe(old, candidate)) return false;
                if (old.effectDefinition)
                {
                    if (old.effectDefinition != candidate.effectDefinition) return false;
                }
                else if (!LegacyMatches(legacy, old.effectKind, candidate.effectDefinition)) return false;
            }
            return true;
        }

        private static bool SameRecipe(SkillDefinition a, SkillDefinition b) => a && b &&
            a.skillId == b.skillId && a.displayName == b.displayName && a.triggerMode == b.triggerMode &&
            SameOptionalInput(a.phrase, b.phrase, a.triggerMode == SkillTriggerMode.GestureOnly) &&
            SameOptionalInput(a.sealName, b.sealName, a.triggerMode == SkillTriggerMode.VoiceOnly) &&
            (a.phraseTokens == b.phraseTokens || string.IsNullOrWhiteSpace(a.phraseTokens)) &&
            a.matchWindow == b.matchWindow && a.voiceDiscount == b.voiceDiscount && a.thresholdFloor == b.thresholdFloor &&
            a.leftHand == b.leftHand && a.cooldownSeconds == b.cooldownSeconds && a.durationSeconds == b.durationSeconds;

        // Unity's serialized copy converts a null string to "". Only an unused input may
        // treat these representations as equivalent; required input and nonempty text stay exact.
        private static bool SameOptionalInput(string a, string b, bool unused) =>
            a == b || (unused && string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b));

        private static bool LegacyMatches(SkillEffects legacy, SkillEffectKind kind, SkillEffectDefinition effect)
        {
            if (!legacy || !effect) return false;
            if (kind == SkillEffectKind.SelfBuff && effect is ShieldEffectDefinition shield)
                return shield.capacity == legacy.shieldCapacity && shield.visualPrefab == legacy.shieldPrefab &&
                    (shield.visualPrefab || shield.material == legacy.buffMaterial);
            if (kind == SkillEffectKind.HeldFireball && effect is ProjectileEffectDefinition ball)
                return ball.radius == legacy.ballRadius && ball.damage == legacy.projectileDamage && ball.speed == legacy.projectileSpeed &&
                    ball.flightLifetime == legacy.projectileLifetime && ball.heldLifetime == legacy.heldLifetime &&
                    ball.heldOffset == legacy.heldOffset && ball.armDelay == legacy.armDelay && ball.trackingLossGrace == legacy.trackingLossGrace &&
                    ball.pushDistance == legacy.pushDistance && ball.pushMinimumSeconds == legacy.pushMinimumSeconds &&
                    ball.pushMaximumSeconds == legacy.pushMaximumSeconds && ball.visualPrefab == legacy.fireballPrefab &&
                    ball.impactPrefab == legacy.impactPrefab && (ball.visualPrefab || ball.material == legacy.fireMaterial);
            return false;
        }

        private static SkillCatalog MigrateLegacyCatalog(SkillTestController controller, IReadOnlyList<SkillDefinition> source, string migrationRoot)
        {
            if (source.Any(skill => !skill)) throw new InvalidOperationException("旧技能列表含空引用，请先修复后迁移。");
            if (source.Any(skill => !skill.effectDefinition) && !controller.effects) throw new InvalidOperationException("缺少旧效果执行器，无法保留旧场景效果参数。");
            string scenePath = controller.gameObject.scene.path;
            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) throw new InvalidOperationException("请先保存旧场景，再生成稳定的迁移配置路径。");
            migrationRoot = migrationRoot.Replace('\\', '/').TrimEnd('/');
            if (!migrationRoot.StartsWith("Assets/", StringComparison.Ordinal) || migrationRoot.Contains("..")) throw new ArgumentException("迁移输出必须位于明确的 Assets 子目录。", nameof(migrationRoot));
            string directory = migrationRoot + "/" + sceneGuid;
            string path = MigrationPath(directory, "SkillCatalog", "技能目录.asset");
            var existing = AssetDatabase.LoadAssetAtPath<SkillCatalog>(path);
            if (existing) return existing;
            EnsureFolder(Path.GetDirectoryName(path));
            var catalog = ScriptableObject.CreateInstance<SkillCatalog>();
            catalog.name = Path.GetFileNameWithoutExtension(scenePath) + "技能目录";
            for (int i = 0; i < source.Count; i++)
            {
                var old = source[i];
                // Explicit effect assets already carry the user's configuration: never replace them.
                if (old.effectDefinition) { catalog.skills.Add(old); continue; }
                SkillEffectDefinition effect = LegacyEffect(controller.effects, old.effectKind, directory);
                catalog.skills.Add(CopySkill(old, MigrationPath(directory, "SkillDefinition", $"技能-{i:000}.asset"), effect));
            }
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        // Production migrations group by concrete asset type; isolated callers retain their root.
        private static string MigrationPath(string directory, string type, string file)
        {
            const string prefix = Root + "/迁移/";
            return directory.StartsWith(prefix, StringComparison.Ordinal)
                ? Root + "/" + ConfigurationAssetOrganizer.FolderName(type) + "/迁移配置/" + directory.Substring(prefix.Length) + "配置/" + file
                : directory + "/" + file;
        }

        private static SkillEffectDefinition LegacyEffect(SkillEffects source, SkillEffectKind kind, string directory)
        {
            if (kind == SkillEffectKind.SelfBuff)
            {
                string path = MigrationPath(directory, "ShieldEffectDefinition", "旧场景护盾.asset");
                EnsureFolder(Path.GetDirectoryName(path));
                var asset = AssetDatabase.LoadAssetAtPath<ShieldEffectDefinition>(path);
                if (!asset)
                {
                    asset = ScriptableObject.CreateInstance<ShieldEffectDefinition>();
                    asset.capacity = source.shieldCapacity; asset.visualPrefab = source.shieldPrefab; asset.material = source.buffMaterial;
                    AssetDatabase.CreateAsset(asset, path);
                }
                return asset;
            }
            if (kind == SkillEffectKind.HeldFireball)
            {
                string path = MigrationPath(directory, "ProjectileEffectDefinition", "旧场景投射物.asset");
                EnsureFolder(Path.GetDirectoryName(path));
                var asset = AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>(path);
                if (!asset)
                {
                    asset = ScriptableObject.CreateInstance<ProjectileEffectDefinition>();
                    asset.radius = source.ballRadius; asset.damage = source.projectileDamage; asset.speed = source.projectileSpeed;
                    asset.flightLifetime = source.projectileLifetime; asset.heldLifetime = source.heldLifetime; asset.heldOffset = source.heldOffset;
                    asset.armDelay = source.armDelay; asset.trackingLossGrace = source.trackingLossGrace;
                    asset.pushDistance = source.pushDistance; asset.pushMinimumSeconds = source.pushMinimumSeconds; asset.pushMaximumSeconds = source.pushMaximumSeconds;
                    asset.visualPrefab = source.fireballPrefab; asset.impactPrefab = source.impactPrefab; asset.material = source.fireMaterial;
                    AssetDatabase.CreateAsset(asset, path);
                }
                return asset;
            }
            throw new InvalidOperationException("无法迁移未知旧效果类型：" + kind);
        }

        /// <summary>
        /// Explicit one-time repair for the first migration shipped in this task. It accepts only
        /// the known three sample skills/default effect references; regular Migrate never calls it.
        /// After the marker is written, old scene fields cannot overwrite edited new configuration.
        /// </summary>
        public static int RepairInitialMigration()
        {
            RequireCleanEditor();
            if (File.Exists(RepairMarker)) return 0;
            var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath);
            var originals = LoadLegacyDefaults();
            var shield = AssetDatabase.LoadAssetAtPath<ShieldEffectDefinition>(Root + "/护盾效果配置/默认护盾.asset");
            var projectile = AssetDatabase.LoadAssetAtPath<ProjectileEffectDefinition>(Root + "/投射物效果配置/默认火球.asset");
            if (!catalog || !shield || !projectile) throw new InvalidOperationException("未找到首版迁移的默认资产，不能执行此定向修复。");
            if (shield.capacity != 100 || shield.visualPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ShieldPath) || shield.material ||
                projectile.radius != .065f || projectile.damage != 25 || projectile.speed != 9 || projectile.flightLifetime != 4 ||
                projectile.heldLifetime != 10 || projectile.heldOffset != new Vector3(0,.05f,.08f) || projectile.armDelay != .25f ||
                projectile.trackingLossGrace != .5f || projectile.pushDistance != .12f || projectile.pushMinimumSeconds != .15f ||
                projectile.pushMaximumSeconds != .6f || projectile.visualPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.FireballPath) ||
                projectile.impactPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ImpactPath) || projectile.material)
                throw new InvalidOperationException("首版默认效果数值或视觉已另行修改，定向修复停止；请人工选择要保留的配置。");
            var replacement = new SkillDefinition[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                var expected = originals[i].effectKind == SkillEffectKind.SelfBuff ? (SkillEffectDefinition)shield : projectile;
                if (originals[i].effectDefinition && originals[i].effectDefinition != expected)
                    throw new InvalidOperationException("原技能已引用用户效果资产，定向修复停止：" + originals[i].name);
                replacement[i] = AssetDatabase.LoadAssetAtPath<SkillDefinition>(Root + "/技能定义配置/默认配置/" + originals[i].name + ".asset");
                if (replacement[i] && (!SameRecipe(originals[i], replacement[i]) || replacement[i].effectDefinition != expected))
                    throw new InvalidOperationException("默认技能副本已另行编辑，定向修复停止：" + originals[i].name);
            }
            bool originalCatalog = catalog.skills.SequenceEqual(originals);
            bool partlyRepaired = replacement.All(item => item) && catalog.skills.SequenceEqual(replacement);
            if (!originalCatalog && !partlyRepaired) throw new InvalidOperationException("默认目录已另行编辑，不能按首版样例修复。");
            // Validate every scene before changing any shared skill reference.
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath)) continue;
                WithScene(scenePath, scene =>
                {
                    var controller = Controller(scene);
                    string catalogPath = AssetDatabase.GetAssetPath(controller.catalog).Replace('\\', '/');
                    if (!controller.skills.SequenceEqual(originals) || (controller.catalog != catalog &&
                        !catalogPath.StartsWith(Root + "/技能目录配置/迁移配置/", StringComparison.Ordinal)))
                        throw new InvalidOperationException("场景已使用其它配置，定向修复停止：" + scenePath);
                });
            }
            for (int i = 0; i < originals.Length; i++)
            {
                var expected = originals[i].effectKind == SkillEffectKind.SelfBuff ? (SkillEffectDefinition)shield : projectile;
                replacement[i] = CopySkill(originals[i], Root + "/技能定义配置/默认配置/" + originals[i].name + ".asset", expected);
            }
            catalog.skills = replacement.ToList(); EditorUtility.SetDirty(catalog);
            foreach (var original in originals) { original.effectDefinition = null; EditorUtility.SetDirty(original); }
            int changed = 0;
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath)) continue;
                WithScene(scenePath, scene =>
                {
                    var controller = Controller(scene);
                    if (controller.catalog == catalog) controller.catalog = null;
                    Bind(controller, catalog);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存修复失败：" + scenePath);
                    changed++;
                });
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(RepairMarker, "{\"schema\":1,\"repair\":\"preserve-legacy-scene-effects\"}", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(RepairMarker);
            return changed;
        }

        private static void WithScene(string path, Action<Scene> action)
        {
            var scene = SceneManager.GetSceneByPath(path); bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { action(scene); }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        [MenuItem("Tools/NarutoVR/技能配置/验证当前场景")]
        public static void ValidateCurrent()
        {
            var c=Controller(SceneManager.GetActiveScene());ValidateController(c);
            Debug.Log($"[技能配置] {c.ConfiguredSkills.Count} 个技能配置有效，语音导出也可用。");
        }
        public static void ValidateController(SkillTestController c)
        {
            var errors=c.ConfigurationErrors();
            if(!c.catalog)errors.Add("尚未迁移共享技能目录");
            if(!c.view||c.view.controller!=c||!c.view.statusText||!c.view.chineseFont)errors.Add("中文界面接线缺失");
            if(!c.voice)errors.Add("语音接收器缺失");
            if(c.lab&&c.ConfiguredSkills!=null)
                foreach(var skill in c.ConfiguredSkills)
                    if(skill&&skill.triggerMode!=SkillTriggerMode.VoiceOnly&&!c.lab.profiles.Any(p=>p&&p.sealName==skill.sealName))errors.Add($"{skill.displayName} 所需 {skill.sealName} 印未加入骨骼实验台");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            BuildVoiceJson(c.catalog,c.voice.receivePort,c.voice.servicePort);
        }
        [MenuItem("Tools/NarutoVR/技能配置/导出当前场景语音配置")]
        public static void ExportCurrent()
        {
            var c=Controller(SceneManager.GetActiveScene());ValidateController(c);
            Directory.CreateDirectory(Path.GetDirectoryName(VoicePath));
            File.WriteAllText(VoicePath,BuildVoiceJson(c.catalog,c.voice.receivePort,c.voice.servicePort),new UTF8Encoding(false));
            Debug.Log("[技能配置] 语音配置已导出："+VoicePath+"。请重启Python语音服务和当前播放，使用同一版目录。");
        }
        public static string BuildVoiceJson(SkillCatalog catalog,int receivePort,int servicePort)
        {
            if(!catalog)throw new InvalidOperationException("缺少技能目录");
            var errors=catalog.Validate();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            if(receivePort<1024||receivePort>65535||servicePort<1024||servicePort>65535||receivePort==servicePort)throw new InvalidOperationException("语音收发端口无效或相同");
            var words=new List<Keyword>();
            foreach(var skill in catalog.skills)
                if(skill.triggerMode!=SkillTriggerMode.GestureOnly)words.Add(new Keyword{phrase=SkillPhrase.Normalize(skill.phrase),tokens=skill.phraseTokens});
            words.Add(new Keyword{phrase=SkillPhrase.Normalize(catalog.cancelPhrase),tokens=catalog.cancelTokens});
            string tokenPath="Tools/SkillVoice/models/sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20/tokens.txt";
            HashSet<string> tokens=File.Exists(tokenPath)?new HashSet<string>(File.ReadLines(tokenPath).Where(l=>!string.IsNullOrWhiteSpace(l)).Select(l=>l.Split(' ')[0])):null;
            foreach(var word in words)
            {
                if(word.phrase=="all"||word.phrase.Any(char.IsControl)||word.phrase.IndexOfAny(new[]{'@',':','#'})>=0)throw new InvalidOperationException("口令包含保留字符："+word.phrase);
                if(string.IsNullOrWhiteSpace(word.tokens))throw new InvalidOperationException("缺少口令声学拼音："+word.phrase);
                var parts=word.tokens.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                foreach(string part in parts)
                    if(part[0]=='@'||part[0]==':'||part[0]=='#'||(tokens!=null&&!tokens.Contains(part)))throw new InvalidOperationException($"{word.phrase} 的拼音token无效：{part}");
                word.tokens=string.Join(" ",parts);
            }
            return JsonUtility.ToJson(new VoiceExport{cancelPhrase=SkillPhrase.Normalize(catalog.cancelPhrase),unityPort=receivePort,servicePort=servicePort,keywords=words.ToArray()},true);
        }
        public static SkillTestController Controller(Scene scene)=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkillTestController>(true)).Single();
        private static void RequireCleanEditor()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出播放模式");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("当前场景有未保存修改，请先保存再迁移");
        }
    }

    [CustomEditor(typeof(SkillCatalog))]
    public sealed class SkillCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var catalog=(SkillCatalog)target;
            foreach(string error in catalog.Validate())EditorGUILayout.HelpBox(error,MessageType.Error);
            EditorGUILayout.HelpBox("修改后停止并重新播放；口令变更还需导出语音配置并重启服务。效果参数在各技能引用的效果资产里调整。",MessageType.Info);
        }
    }
    [CustomEditor(typeof(SkillDefinition))]
    public sealed class SkillDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var skill=(SkillDefinition)target;
            if(skill.ValidateDefinition() is string error)EditorGUILayout.HelpBox(error,MessageType.Error);
            if(!skill.effectDefinition)EditorGUILayout.HelpBox("当前仍使用旧场景效果参数；新技能请引用独立效果配置资产。",MessageType.Warning);
        }
    }
    [CustomEditor(typeof(SkillEffects))]
    public sealed class SkillEffectsEditor : UnityEditor.Editor
    {
        private bool legacy;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("技能数值和视觉请配置 SkillDefinition 引用的效果资产。此组件负责当前玩家的锚点、碰撞与效果生命周期。",MessageType.Info);
            foreach(string field in new[]{"lab","head","playerRoot","aimDistance","hitLayers","playerLayers"})EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            legacy=EditorGUILayout.Foldout(legacy,"旧场景兼容参数（效果资产为空时使用）",true);
            if(legacy)DrawPropertiesExcluding(serializedObject,"m_Script","lab","head","playerRoot","aimDistance","hitLayers","playerLayers");
            serializedObject.ApplyModifiedProperties();
        }
    }
}


