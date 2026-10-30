using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealLab;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Lab=NarutoVR.SealLab.SealLab;

namespace NarutoVR.SealCombat.Editor
{
    public static class SkillArchitectureChecks
    {
        [MenuItem("Tools/NarutoVR/技能配置/运行架构检查")]
        public static void Menu()=>Debug.Log($"[技能配置] {Run()} 项架构检查通过；核心匹配 {SkillCoreChecks.Run()} 项通过。");
        public static int Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("配置检查需要编辑模式");
            var owned=new List<Object>();
            T Own<T>(T value)where T:Object{owned.Add(value);return value;}
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<SkillCatalog>(SkillConfigurationSetup.CatalogPath);
                Require(source,"缺少迁移后的目录");
                var catalog=Own(Object.Instantiate(source));catalog.skills=new List<SkillDefinition>(source.skills);
                var fourth=Own(ScriptableObject.CreateInstance<SkillDefinition>());
                fourth.skillId="test.fourth";fourth.displayName="第四技能";fourth.triggerMode=SkillTriggerMode.VoiceOnly;
                fourth.phrase="雷电";fourth.phraseTokens="l éi d iàn";fourth.cooldownSeconds=0;
                fourth.effectDefinition=source.skills[0].effectDefinition;catalog.skills.Add(fourth);
                Require(catalog.Validate().Count==0,"新增第四配方应无需修改代码");
                string json=SkillConfigurationSetup.BuildVoiceJson(catalog,47831,47832);
                Require(json.Contains("雷电")&&json.Contains("l éi d iàn"),"新增口令必须进入导出词表");
                int activated=0;var engine=new SkillMatchEngine(catalog.skills,catalog.matching,catalog.cancelPhrase);
                engine.TryActivate=s=>{Require(s==fourth,"第四口令选错配方");activated++;return true;};
                engine.Tick(1);engine.Voice("雷，电！",1,1);Require(activated==1,"新配置口令未激活");
                int passed=1;

                // Input snapshots must stay stable when assets are edited in the Inspector.
                fourth.phrase="后来修改";catalog.matching.historySeconds=5;
                engine.Tick(2);engine.Voice("雷电",2,2);
                Require(activated==2&&engine.HistoryLimitSeconds==10&&engine.ActiveRecipes.Last().Phrase=="雷电","匹配器或UI配方未冻结配置");passed++;
                fourth.phrase="雷电";catalog.matching.historySeconds=10;
                catalog.cancelPhrase="终止，术式！";catalog.cancelTokens="zh ōng zh ǐ sh ù sh ì";
                var cancelEngine=new SkillMatchEngine(catalog.skills,catalog.matching,catalog.cancelPhrase);
                cancelEngine.Tick(10);long epoch=cancelEngine.Epoch;cancelEngine.Voice("终止术式",10,10);
                Require(cancelEngine.Epoch==epoch+1,"自定义取消未使用共同归一化");
                cancelEngine.Tick(11);cancelEngine.Voice("终止术式",9,11);Require(cancelEngine.Epoch==epoch+1,"取消前的旧语音不应再次清理");passed++;

                catalog.matching.historySeconds=5;Require(catalog.Validate().Any(x=>x.Contains("超过骨骼历史")),"缓存与组合窗口必须联动验证");passed++;
                catalog.matching.historySeconds=10;
                fourth.skillId=catalog.skills[0].skillId;Require(catalog.Validate().Any(x=>x.Contains("标识重复")),"重复ID必须报错");fourth.skillId="test.fourth";passed++;
                fourth.phrase="终止术式火球";Require(catalog.Validate().Any(x=>x.Contains("取消口令冲突")),"包含取消词的技能必须拒绝");fourth.phrase="雷电";passed++;
                fourth.phraseTokens="";bool rejected=false;
                try{SkillConfigurationSetup.BuildVoiceJson(catalog,47831,47832);}catch(InvalidOperationException){rejected=true;}
                Require(rejected,"缺声学tokens不应悄悄导出旧词表");passed++;
                fourth.phraseTokens="INVALID_TOKEN";rejected=false;
                try{SkillConfigurationSetup.BuildVoiceJson(catalog,47831,47832);}catch(InvalidOperationException){rejected=true;}
                Require(rejected,"模型不支持的拼音必须报错");passed++;
                fourth.phraseTokens="l éi d iàn";rejected=false;
                try{SkillConfigurationSetup.BuildVoiceJson(catalog,47831,47831);}catch(InvalidOperationException){rejected=true;}
                Require(rejected,"收发端口相同必须报错");passed++;
                var view=Own(new GameObject("快捷键配置验收")).AddComponent<SkillTestView>();
                view.shortcuts.Add(new SkillTestView.Shortcut{skill=fourth,key=UnityEngine.InputSystem.Key.Escape});
                Require(view.ConfigurationErrors(catalog.skills).Any(x=>x.Contains("取消保留键")),"Esc必须保留给取消");
                view.shortcuts[0].key=UnityEngine.InputSystem.Key.F6;
                view.shortcuts.Add(new SkillTestView.Shortcut{skill=catalog.skills.First(s=>s.triggerMode==SkillTriggerMode.VoiceOnly),key=UnityEngine.InputSystem.Key.F6});
                Require(view.ConfigurationErrors(catalog.skills).Any(x=>x.Contains("重复")),"重复快捷键必须拒绝");
                view.shortcuts[1].key=UnityEngine.InputSystem.Key.F7;
                Require(view.ConfigurationErrors(catalog.skills).Count==0,"不同有效快捷键不应被拒绝");passed++;
                return passed;
            }
            finally{for(int i=owned.Count-1;i>=0;i--)if(owned[i])Object.DestroyImmediate(owned[i]);}
        }
        public static int RunController()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("控制器检查需要播放模式");
            GameObject root=null;SkillCatalog catalog=null;SkillDefinition extra=null;ShieldEffectDefinition effect=null;
            try
            {
                root=new GameObject("架构验收_临时玩家");root.transform.position=new Vector3(250,100,100);root.SetActive(false);
                var lab=root.AddComponent<Lab>();lab.recordingOrigin=root.transform;
                var effects=root.AddComponent<SkillEffects>();effects.head=root.transform;effects.playerRoot=root.transform;
                var voice=root.AddComponent<SkillVoiceReceiver>();voice.enabled=false; // No port binding is needed to inspect the copied whitelist.
                var view=root.AddComponent<SkillTestView>();view.showDesktopControls=false;
                var controller=root.AddComponent<SkillTestController>();controller.lab=lab;controller.effects=effects;controller.voice=voice;controller.view=view;
                catalog=Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillCatalog>(SkillConfigurationSetup.CatalogPath));
                catalog.skills=new List<SkillDefinition>(catalog.skills);catalog.cancelPhrase="终止术式";
                effect=ScriptableObject.CreateInstance<ShieldEffectDefinition>();effect.capacity=87;
                extra=ScriptableObject.CreateInstance<SkillDefinition>();extra.skillId="test.extra";extra.displayName="目录新增技能";
                extra.phrase="雷电";extra.triggerMode=SkillTriggerMode.VoiceOnly;extra.effectDefinition=effect;
                catalog.skills.Add(extra);controller.catalog=catalog;
                root.SetActive(true);controller.SendMessage("Start"); // Synchronous fixture is destroyed before Unity calls automatic Start.
                Require(controller.Engine!=null&&controller.Recipes.Count==4,"控制器仍限制三个配方");
                Require(voice.AcceptsPhrase("雷电")&&voice.AcceptsPhrase("终止，术式")&&!voice.AcceptsPhrase("其他口令"),"接收器白名单未从目录复制");
                Require(view.BuildHUD(Time.realtimeSinceStartupAsDouble).Contains("目录新增技能"),"新增技能未自动出现在HUD");
                int passed=1;
                controller.enabled=false;
                double endedWhileDisabled=Time.realtimeSinceStartupAsDouble;long disabledEpoch=controller.Engine.Epoch;
                controller.enabled=true;
                Require(controller.Engine.Epoch==disabledEpoch+1,"重新启用必须推进输入分界");
                controller.ReceiveVoice("雷电",endedWhileDisabled);
                Require(effects.ActivationCount==0,"禁用期间说出的迟到口令不应在重新启用后触发");passed++;
                controller.ReceiveVoice("雷电",Time.realtimeSinceStartupAsDouble);
                Require(effects.ActivationCount==1&&effects.ShieldPoints==87,"新增目录→控制器→效果执行失败");passed++;
                controller.ReceiveVoice("终止术式",Time.realtimeSinceStartupAsDouble-5);
                Require(effects.ShieldPoints==87,"过期自定义取消不应移除新效果");passed++;
                controller.ReceiveVoice("终止，术式！",Time.realtimeSinceStartupAsDouble+.0000001);
                Require(effects.ShieldPoints==0,"有效自定义取消没有结束效果");passed++;
                controller.ScheduleVoice("雷电");Require(controller.ScheduledPhrase!=null,"模拟命令未排程");
                controller.enabled=false;Require(controller.ScheduledPhrase==null&&!voice.AcceptsPhrase("雷电"),"禁用后应清模拟输入并关闭接收白名单");
                controller.enabled=true;Require(voice.AcceptsPhrase("雷电"),"重启控制器应恢复冻结白名单");passed++;
                return passed;
            }
            finally
            {
                if(root)Object.DestroyImmediate(root);if(extra)Object.DestroyImmediate(extra);if(effect)Object.DestroyImmediate(effect);if(catalog)Object.DestroyImmediate(catalog);
            }
        }
        private static void Require(bool value,string detail){if(!value)throw new Exception(detail);}
    }
}
