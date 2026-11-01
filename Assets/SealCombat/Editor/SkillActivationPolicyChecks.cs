#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NarutoVR.SealCombat.Editor
{
    public static class SkillActivationPolicyChecks
    {
        sealed class Policy : ISkillActivationPolicy
        {
            public bool result, throwStatus, throwActivation;
            public int calls;
            public bool OwnsCooldowns => true;
            public string ActivationStatus => throwStatus?throw new InvalidOperationException("仅诊断失败"):"测试状态";
            public bool TryActivate(SkillDefinition skill) { calls++; if(throwActivation)throw new InvalidOperationException("执行失败"); return result; }
        }
        public static int Run()
        {
            var root=new GameObject("激活接口等价检查") {hideFlags=HideFlags.HideAndDontSave};
            var skill=ScriptableObject.CreateInstance<SkillDefinition>();
            try {
                var controller=root.AddComponent<SkillTestController>();
                skill.skillId="policy.test";skill.displayName="接口检查";
                var execute=(Func<SkillDefinition,bool>)Delegate.CreateDelegate(typeof(Func<SkillDefinition,bool>),controller,
                    typeof(SkillTestController).GetMethod("ActivateTraced",BindingFlags.Instance|BindingFlags.NonPublic));
                var policy=new Policy();controller.ActivationPolicy=policy;
                foreach(bool expected in new[]{false,true}) {
                    policy.result=expected;
                    if(execute(skill)!=expected)throw new Exception("新接口改变执行结果");
                    controller.ActivationOverride=_=>expected;
                    int count=policy.calls;
                    if(execute(skill)!=expected||policy.calls!=count)throw new Exception("兼容回调优先级或调用次数改变");
                    controller.ActivationOverride=null;
                }
                policy.result=true;policy.throwStatus=true;
                if(!execute(skill))throw new Exception("诊断失败不应改变已成功激活");
                policy.throwActivation=true;
                bool propagated=false;
                try { execute(skill); } catch(InvalidOperationException e) { propagated=e.Message=="执行失败"; }
                if(!propagated)throw new Exception("实际执行异常语义改变");
                return 6;
            } finally { Object.DestroyImmediate(root);Object.DestroyImmediate(skill); }
        }
    }
}
#endif
