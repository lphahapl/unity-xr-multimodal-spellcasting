#if UNITY_EDITOR
using System;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NarutoVR.SealCombat.Editor
{
    public static class ThrowPresentationChecks
    {
        public static int Run()
        {
            var root=new GameObject("投掷表现检查"){hideFlags=HideFlags.HideAndDontSave};
            var ring=new GameObject("检查用护盾腕饰"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var effects=root.AddComponent<SkillEffects>(); effects.head=root.transform;
                effects.WristPoseOverride=_=>null;
                typeof(SkillEffects).GetField("buffObject",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(effects,ring);
                var update=typeof(SkillEffects).GetMethod("UpdateBuffVisual",BindingFlags.NonPublic|BindingFlags.Instance);
                update.Invoke(effects,null);
                if(ring.activeSelf) throw new Exception("左手无追踪时护盾腕饰仍在头显前方悬空显示");
                effects.WristPoseOverride=_=>new Pose(Vector3.one,Quaternion.identity); update.Invoke(effects,null);
                if(!ring.activeSelf || ring.transform.position!=Vector3.one) throw new Exception("恢复有效左腕后应重新贴腕显示");
                return 2;
            }
            finally
            {
                if(root) Object.DestroyImmediate(root);
                if(ring) Object.DestroyImmediate(ring);
            }
        }
    }
}
#endif
