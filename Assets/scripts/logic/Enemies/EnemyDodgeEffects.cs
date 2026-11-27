using NarutoVR.SealCombat.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NarutoVR.SealCombat.Enemies
{
    /// <summary>一次闪避只通知同场已启用的敌人。回池或随后生成的实例不继承上一轮的效果。</summary>
    public static class EnemyDodgeEffects
    {
        internal static readonly EventKey<(Scene scene, Transform head, Transform rig)> LockRequested = new("闪避锁定本次敌方攻击");
        internal static readonly EventKey<(Scene scene, Transform source, float scale, float seconds)> SlowRequested = new("极限闪避减慢同场敌人");
        internal static readonly EventKey<Transform> SlowCleared = new("结束该玩家的敌方减速");

        public static void LockCurrentAttacks(Scene scene, Transform playerHead, Transform playerRig)
        {
            if (scene.IsValid() && (playerHead || playerRig))
                EventCenter.Publish(LockRequested, (scene, playerHead, playerRig));
        }

        public static void SlowEnemies(Scene scene, Transform source, float scale, float seconds)
        {
            if (scene.IsValid() && source && float.IsFinite(scale) && scale > 0 && scale < 1 &&
                float.IsFinite(seconds) && seconds > 0)
                EventCenter.Publish(SlowRequested, (scene, source, scale, seconds));
        }

        public static void ClearSlow(Transform source)
        {
            if (source) EventCenter.Publish(SlowCleared, source);
        }
    }
}
