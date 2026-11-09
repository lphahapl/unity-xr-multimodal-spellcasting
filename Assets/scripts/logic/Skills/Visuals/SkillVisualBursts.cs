using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat
{
    /// <summary>
    /// 一次技能施放持有的瞬时视觉集合。调用方提供游戏时钟并负责 Tick/Clear，
    /// 因而收剑可以留下余波，取消技能又能立即清理；不包含伤害、碰撞或技能规则。
    /// </summary>
    internal sealed class SkillVisualBursts
    {
        const int Capacity = 32;
        readonly Scene scene;
        readonly List<(GameObject instance, double expires)> active = new();

        public SkillVisualBursts(Scene scene) => this.scene = scene;

        /// <summary>是否还有待回收的视觉，使所属施放在收剑后仍可接受取消与清理。</summary>
        public bool IsAlive => active.Count > 0;

        /// <summary>在接触点播放独立副本；空引用表示不需要该层反馈。</summary>
        public void Play(GameObject prefab, Vector3 position, Quaternion rotation, double now, float lifetime)
        {
            if (!prefab) return;
            if (active.Count >= Capacity)
            {
                Remove(active[0].instance);
                active.RemoveAt(0);
            }
            var instance = Object.Instantiate(prefab, position, rotation);
            if (instance.scene != scene) SceneManager.MoveGameObjectToScene(instance, scene);
            instance.SetActive(true);
            active.Add((instance, now + lifetime));
        }

        /// <summary>使用技能时钟回收到期实例，与所属技能遵循同一时间基准。</summary>
        public void Tick(double now)
        {
            for (int i = active.Count - 1; i >= 0; --i)
            {
                var item = active[i];
                if (item.instance && now < item.expires) continue;
                Remove(item.instance);
                active.RemoveAt(i);
            }
        }

        /// <summary>取消施放或退出场景时立即隐藏并销毁本集合创建的所有实例。</summary>
        public void Clear()
        {
            foreach (var item in active) Remove(item.instance);
            active.Clear();
        }

        static void Remove(GameObject instance)
        {
            if (!instance) return;
            instance.SetActive(false);
            if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance);
        }
    }
}
