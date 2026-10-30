using System.Collections.Generic;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    [CreateAssetMenu(menuName="NarutoVR/技能/技能目录与输入规则")]
    public sealed class SkillCatalog : ScriptableObject
    {
        [Header("一、可用技能（同一目录供多个场景共享）")]
        [InspectorName("技能列表")] public List<SkillDefinition> skills = new();
        [Header("二、匹配规则（进入播放时复制，不在运行中修改资产）")]
        [InspectorName("时序与消歧设置")] public SkillMatchSettings matching = new();
        [Header("三、语音取消命令")]
        [InspectorName("完整取消口令")] public string cancelPhrase = SkillPhrase.DefaultCancel;
        [InspectorName("取消口令声学拼音 tokens"), Tooltip("sherpa模型的声母/带声调韵母，空格分隔。导出时核对模型tokens.txt。")]
        public string cancelTokens = "q ǔ x iāo sh ī f ǎ";

        public List<string> Validate()
        {
            var issues=new List<string>();
            if(matching==null)issues.Add("未配置匹配规则");
            else if(matching.Validate() is string error)issues.Add(error);
            if(SkillPhrase.Normalize(cancelPhrase).Length==0)issues.Add("取消口令不能为空");
            if(skills==null || skills.Count==0){issues.Add("技能目录为空");return issues;}
            if(matching==null || matching.Validate()!=null || SkillPhrase.Normalize(cancelPhrase).Length==0)return issues;
            string[] errors=SkillConfigurationValidator.GetErrors(skills,matching,cancelPhrase);
            for(int i=0;i<errors.Length;i++)if(errors[i]!=null)issues.Add($"技能[{i}] {(skills[i]?skills[i].displayName:"空")}：{errors[i]}");
            return issues;
        }
        public string[] GetVoicePhrases()
        {
            var phrases=new List<string>();
            if(skills!=null)foreach(var skill in skills)
                if(skill&&skill.triggerMode!=SkillTriggerMode.GestureOnly)
                {
                    string phrase=SkillPhrase.Normalize(skill.phrase);
                    if(phrase.Length>0&&!phrases.Contains(phrase))phrases.Add(phrase);
                }
            string cancel=SkillPhrase.Normalize(cancelPhrase);
            if(cancel.Length>0&&!phrases.Contains(cancel))phrases.Add(cancel);
            return phrases.ToArray();
        }
    }
}
