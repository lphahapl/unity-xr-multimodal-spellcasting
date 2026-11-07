using System;
using UnityEngine;

namespace NarutoVR.SealCombat
{
    /// <summary>匹配器的缓存、时效、保持与消歧参数；启动时复制为本次运行的配置快照。</summary>
    [Serializable]
    public sealed class SkillMatchSettings
    {
        [Header("缓存与输入时序")]
        [InspectorName("骨骼历史上限（秒）"), Range(5,10)] public float historySeconds = 10;
        [InspectorName("骨骼缓存容量（帧）"), Range(64,4096), Tooltip("固定内存上限；默认640槽覆盖60Hz下10秒。容量不足时实际历史长度会缩短。")]
        public int capacity = 640;
        [InspectorName("最大语音结果延迟（秒）"), Range(.1f,2)] public float maximumVoiceLatency = 2;
        [InspectorName("退出旧印后重置保持（秒）"), Range(.05f,1)] public float exitSeconds = .15f;
        [InspectorName("组合技能同印重新就绪（秒）"), Range(.1f,5), Tooltip("组合输入被消费或取消后，持续保持合格的同一印这么久可配合一道新口令再施法。持有手上物品时不计时；纯结印仍需退出原印。")]
        public float comboHeldRearmSeconds = 1f;
        [InspectorName("连续骨骼最大间隔（秒）"), Range(.03f,.5f)] public float maximumSampleGap = .12f;
        [InspectorName("竞争印最低分差"), Range(0,1), Tooltip("0.05表示必须领先其他有效模板5分。")]
        public float ambiguityMargin = .05f;

        /// <summary>复制标量设置，避免运行中修改共享资产改变本轮匹配规则。</summary>
        public SkillMatchSettings Snapshot() => (SkillMatchSettings)MemberwiseClone();
        public string Validate()
        {
            if(!InRange(historySeconds,5,10))return "骨骼历史必须为5至10秒";
            if(capacity<64 || capacity>4096)return "骨骼缓存容量必须为64至4096帧";
            if(!InRange(maximumVoiceLatency,.1f,2))return "最大语音延迟必须为0.1至2秒";
            if(!InRange(exitSeconds,.05f,1))return "退出旧印保持必须为0.05至1秒";
            if(!InRange(comboHeldRearmSeconds,.1f,5))return "组合技能同印重新就绪时长必须为0.1至5秒";
            if(!InRange(maximumSampleGap,.03f,.5f))return "连续骨骼最大间隔必须为0.03至0.5秒";
            if(!InRange(ambiguityMargin,0,1))return "竞争印分差必须为0至1";
            return null;
        }
        private static bool InRange(float value,float min,float max)=>float.IsFinite(value)&&value>=min&&value<=max;
    }
}
