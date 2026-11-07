#if UNITY_EDITOR
using System;
using System.Reflection;
using NarutoVR.SealCombat.Rpg;
namespace NarutoVR.SealCombat.Editor
{
    internal static class NavigationNodeFixture
    {
        // Runtime-only MonoBehaviours do not receive Unity messages in EditMode fixtures.
        // Invoke the actual idempotent hook directly; SendMessage asserts ShouldRunBehaviour in Unity 6.3.
        internal static void SetRegistered(NavigationNode node,bool registered)
        {
            var hook=typeof(NavigationNode).GetMethod(registered?"OnEnable":"OnDisable",BindingFlags.Instance|BindingFlags.NonPublic);
            if(hook==null) throw new MissingMethodException("导航节点生命周期入口不存在");
            hook.Invoke(node,null);
        }
    }
}
#endif
