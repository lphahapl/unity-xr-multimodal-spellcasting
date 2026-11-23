using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>创建和校验技能目录及效果配置，提供本机语音词表导出入口。</summary>
    public static class SkillConfigurationSetup
    {
        public const string Root="Assets/ScriptableObjects";
        public const string CatalogPath=Root+"/技能目录配置/线性训练场技能目录.asset";
        public const string VoicePath="Tools/SkillVoice/skill-voice-config.json";
        [Serializable] private sealed class VoiceExport
        {
            public int schema=1,unityPort,servicePort;
            public string cancelPhrase;
            public Keyword[] keywords;
        }
        [Serializable] private sealed class Keyword { public string phrase,tokens; }

        public static SkillCatalog LoadTrainingCatalog() =>
            AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath) ??
            throw new InvalidOperationException("缺少技能目录：" + CatalogPath);

        [MenuItem("Tools/NarutoVR/技能配置/验证当前场景")]
        public static void ValidateCurrent()
        {
            var c=Controller(SceneManager.GetActiveScene());
            using var source = AuthoredSkillCatalog.Open(c);
            ValidateController(c, source.Catalog);
            Debug.Log($"[技能配置] {source.Catalog.skills.Count} 个技能配置有效，语音导出也可用。");
        }
        public static void ValidateController(SkillInputController c)
        {
            using var source = AuthoredSkillCatalog.Open(c);
            ValidateController(c, source.Catalog);
        }
        static void ValidateController(SkillInputController c, SkillCatalog catalog)
        {
            var errors=c.ConfigurationErrors(catalog);
            if(!c.view||c.view.controller!=c||!c.view.statusText||!c.view.chineseFont)errors.Add("中文界面接线缺失");
            if(!c.voice)errors.Add("语音接收器缺失");
            if(c.lab&&catalog.skills!=null)
                foreach(var skill in catalog.skills)
                    if (skill)
                        foreach (var name in skill.GetSealSequence())
                            if (!c.lab.profiles.Any(p => p && p.sealName == name))
                                errors.Add($"{skill.displayName} 所需的{name}未加入骨骼实验台");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            BuildVoiceJson(catalog,c.voice.receivePort,c.voice.servicePort);
        }
        [MenuItem("Tools/NarutoVR/技能配置/导出当前场景语音配置")]
        public static void ExportCurrent()
        {
            var c=Controller(SceneManager.GetActiveScene());
            using var source = AuthoredSkillCatalog.Open(c);
            ValidateController(c, source.Catalog);
            Directory.CreateDirectory(Path.GetDirectoryName(VoicePath));
            File.WriteAllText(VoicePath,BuildVoiceJson(source.Catalog,c.voice.receivePort,c.voice.servicePort),new UTF8Encoding(false));
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
        public static SkillInputController Controller(Scene scene)=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkillInputController>(true)).Single();
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
            var skill = (SkillDefinition)target;
            if (skill.effectDefinition is BreathEffectDefinition { mode: BreathCastMode.Sustained })
            {
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject, "m_Script", nameof(skill.cooldownSeconds));
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("停喷冷却在上方「效果配置资产」中调整。", MessageType.Info);
            }
            else DrawDefaultInspector();
            if(skill.ValidateDefinition() is string error)EditorGUILayout.HelpBox(error,MessageType.Error);
        }
    }
    [CustomEditor(typeof(BreathEffectDefinition))]
    public sealed class BreathEffectDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var breath = (BreathEffectDefinition)target;
            serializedObject.Update();
            if (breath.mode == BreathCastMode.Sustained)
                DrawPropertiesExcluding(serializedObject, "m_Script", nameof(breath.speed));
            else
                DrawPropertiesExcluding(serializedObject, "m_Script", nameof(breath.manaPerSecond),
                    nameof(breath.damageInterval), nameof(breath.minCooldownSeconds),
                    nameof(breath.cooldownPerSecond), nameof(breath.maxCooldownSeconds));
            serializedObject.ApplyModifiedProperties();
            if (breath.ValidateDefinition() is string error)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    [CustomEditor(typeof(SkillController))]
    public sealed class SkillEffectsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("技能数值和视觉请配置 SkillDefinition 引用的效果资产。此组件负责当前玩家的锚点、碰撞与效果生命周期。",MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }
}


