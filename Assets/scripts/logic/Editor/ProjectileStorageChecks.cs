#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NarutoVR.SealCombat.Editor
{
    public static class ProjectileStorageChecks
    {
        public static int Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("储存配置检查需要编辑模式");
            var root=new GameObject("储存超时检查") {hideFlags=HideFlags.HideAndDontSave};
            var visual=new GameObject("无渲染测试预置体") {hideFlags=HideFlags.HideAndDontSave};
            var definition=ScriptableObject.CreateInstance<ProjectileEffectDefinition>();
            var skill=ScriptableObject.CreateInstance<SkillDefinition>(); skill.skillId = "fixture.projectilestoragechecks.skill"; skill.cooldownSeconds = 0;
            SkillController effect=null;GameObject ball=null;
            try {
                effect=root.AddComponent<SkillController>();effect.head=root.transform;effect.playerRoot=root.transform;
                double now=100;effect.GameClock=()=>now;effect.HandInputClock=()=>now;
                effect.WristPoseOverride=_=>new Pose(new Vector3(10000,1,10000),Quaternion.identity);
                definition.visualPrefab=visual;skill.effectDefinition=definition;skill.displayName="储存检查";skill.durationSeconds=2;
                if(definition.heldLifetime!=60)throw new Exception("新投射物默认不是60秒");
                if(!effect.TryCast(skill))throw new Exception("生成失败");
                ball=(GameObject)UnityEditor.EditorUtility.EntityIdToObject(effect.HeldProjectileInstanceId);
                if(effect.HeldRemainingSeconds!=60)throw new Exception("效果持续时间不应截短储存超时");
                if(effect.TryCast(skill))throw new Exception("储存仍应限制为一发");
                definition.heldLifetime=3;now=112;
                if(effect.HeldRemainingSeconds!=48)throw new Exception("已有持球必须沿用生成时超时快照");
                now=160;
                if(effect.HeldRemainingSeconds!=0)throw new Exception("期限边界剩余时间错误");
                Object.DestroyImmediate(ball);ball=null;
                now=200;if(!effect.TryCast(skill))throw new Exception("新储存生成失败");
                ball=(GameObject)UnityEditor.EditorUtility.EntityIdToObject(effect.HeldProjectileInstanceId);
                if(effect.HeldRemainingSeconds!=3)throw new Exception("新持球未采用自定义N秒");
                definition.heldLifetime=float.NaN;
                if(definition.ValidateDefinition()==null)throw new Exception("无效超时应拒绝");
                return 7;
            } finally {
                if(ball)Object.DestroyImmediate(ball);
                Object.DestroyImmediate(root);Object.DestroyImmediate(visual);Object.DestroyImmediate(definition);Object.DestroyImmediate(skill);
            }
        }
    }
}
#endif
