using System;
using System.Collections.Generic;

namespace NarutoVR.SealCombat
{
    /// <summary>编辑器与运行时共用的配方校验，统一检查技能标识、口令和手印冲突。</summary>
    public static class SkillConfigurationValidator
    {
        /// <summary>按输入列表下标返回错误，冲突双方同时标记，避免依赖技能遍历顺序。</summary>
        public static string[] GetErrors(IReadOnlyList<SkillDefinition> skills, SkillMatchSettings settings, string cancelPhrase)
        {
            if(skills==null)throw new ArgumentNullException(nameof(skills));
            var errors=new string[skills.Count];
            string cancel=SkillPhrase.Normalize(cancelPhrase);
            for(int i=0;i<skills.Count;i++)
            {
                var skill=skills[i];
                if(!skill){errors[i]="缺少技能资产";continue;}
                errors[i]=skill.ValidateDefinition();
                if(errors[i]!=null)continue;
                string phrase=SkillPhrase.Normalize(skill.phrase);
                if(skill.triggerMode!=SkillTriggerMode.GestureOnly && (phrase.Length==0 || phrase.Contains(cancel) || cancel.Contains(phrase)))
                    errors[i]="口令为空或与取消口令冲突";
                if(skill.triggerMode==SkillTriggerMode.VoiceAndGesture && skill.matchWindow+settings.maximumVoiceLatency>settings.historySeconds+.000001f)
                    errors[i]="组合窗口加语音延迟预算超过骨骼历史上限";
            }
            for(int i=0;i<skills.Count;i++)
                for(int j=i+1;j<skills.Count;j++)
                {
                    var a=skills[i];var b=skills[j];if(!a||!b)continue;
                    string conflict=null;
                    if(a.skillId==b.skillId)conflict="技能标识重复";
                    string ap=SkillPhrase.Normalize(a.phrase),bp=SkillPhrase.Normalize(b.phrase);
                    if(a.triggerMode!=SkillTriggerMode.GestureOnly && b.triggerMode!=SkillTriggerMode.GestureOnly &&
                        ap.Length>0&&bp.Length>0&&(ap.Contains(bp)||bp.Contains(ap)))conflict="完整口令重复或包含冲突";
                    if (Interferes(a, b) || Interferes(b, a))
                        conflict = "纯结印配方会在另一印序内提前或同时触发";
                    if(conflict!=null){errors[i]=conflict;errors[j]=conflict;}
                }
            return errors;
        }

        // Only a proper prefix shared by two automatic spells has a hold-to-commit rule.
        private static bool Interferes(SkillDefinition automatic, SkillDefinition other)
        {
            if (automatic.triggerMode != SkillTriggerMode.GestureOnly || other.triggerMode == SkillTriggerMode.VoiceOnly) return false;
            var needle = automatic.GetSealSequence();
            var sequence = other.GetSealSequence();
            for (int i = 0; i <= sequence.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && sequence[i + j] == needle[j]) j++;
                if (j == needle.Length && other.reserveFinalSeal && other.triggerMode == SkillTriggerMode.VoiceAndGesture &&
                    needle.Length == 1 && i > 0 && i == sequence.Length - 1) continue;
                if (j == needle.Length)
                    return !(i == 0 && needle.Length < sequence.Length && other.triggerMode == SkillTriggerMode.GestureOnly);
            }
            return false;
        }
    }
}
