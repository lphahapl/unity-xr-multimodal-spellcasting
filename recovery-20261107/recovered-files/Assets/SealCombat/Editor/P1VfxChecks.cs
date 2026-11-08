using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    /// <summary>Runs in edit mode or Play. Uses the actual impact prefab and real overlap hits.</summary>
    public static class P1VfxChecks
    {
        public static int Run()
        {
            int checks = 0;
            void Require(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException("P1 特效池：" + message);
                checks++;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillVisualAssets.ImpactPath);
            Require(prefab, "缺少火球命中爆散预置体");
            var root = new GameObject("P1 特效池验证（临时）");
            VfxPoolManager pool = null;
            GameObject targetObject = null;
            try
            {
                root.SetActive(false);
                pool = root.AddComponent<VfxPoolManager>();
                pool.impactPrefab = prefab;
                pool.capacity = 1;
                var effects = root.AddComponent<SkillEffects>();
                effects.vfxPool = pool;
                root.SetActive(true);
                pool.Prewarm();
                pool.Prewarm();
                Require(pool.PrewarmedCount == 1 && pool.AvailableCount == 1 && pool.ActiveCount == 0, "重复预热不应增加实例");

                pool.overrideImpactTint = true;
                pool.impactTint = new Color(.1f, .8f, .5f, 1);
                var position = new Vector3(1456, 1532, 1540);
                int baseColor = Shader.PropertyToID("_BaseColor");
                var sourceMaterial = prefab.GetComponentInChildren<Renderer>(true).sharedMaterial;
                var materialColor = sourceMaterial.GetColor(baseColor);
                Require(pool.TryPlayImpact(prefab, position, Quaternion.identity, effects, out var first), "首次出借失败");
                var visual = first.Visual;
                Require(visual && visual.activeSelf && visual.transform.position == position && pool.ActiveCount == 1,
                    "出借应在世界坐标显示已预热实例");
                Require(!visual.GetComponent<SkillVisualLifetime>().enabled, "池化实例仍会被原有寿命组件销毁");
                visual.GetComponent<SkillVisualLifetime>().Finish();
                Require(first.Visual && visual.activeSelf, "一次性销毁入口不得销毁池化实例");
                var renderer = visual.GetComponentInChildren<Renderer>(true);
                var material = renderer.sharedMaterial;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Require(properties.GetColor(baseColor) == pool.impactTint && material == sourceMaterial && material.GetColor(baseColor) == materialColor,
                    "着色应保存在渲染实例属性块");

                Require(!pool.TryPlayImpact(prefab, position, Quaternion.identity, effects, out _) &&
                    pool.SkippedAtCapacity == 1 && pool.PrewarmedCount == 1, "满池应跳过视觉且不增长");
                Require(first.Release() && !first.Release() && !first.Visual && !visual.activeSelf, "重复归还不得减少计数");
                pool.overrideImpactTint = false;
                Require(pool.TryPlayImpact(prefab, position, Quaternion.identity, effects, out var second) && second.Visual == visual,
                    "再次命中应复用同一实例");
                renderer.GetPropertyBlock(properties);
                Require(!properties.HasColor(baseColor) && material.GetColor(baseColor) == materialColor, "复用时遗留了上一发实例颜色");
                Require(!first.Release() && second.Visual && pool.ActiveCount == 1, "过期租约影响了新的一发");
                pool.CancelOwned(root.transform);
                Require(second.Visual, "其它技能所有者的取消不应关闭当前爆散");
                effects.ClearAll();
                Require(!second.Visual && pool.AvailableCount == 1, "清理技能没有归还所属爆散");

                targetObject = new GameObject("P1 火球命中靶（临时）");
                targetObject.transform.position = position;
                targetObject.AddComponent<BoxCollider>();
                var target = targetObject.AddComponent<SkillTrainingTarget>();
                Physics.SyncTransforms();
                GameObject PlayProjectile()
                {
                    var ball = new GameObject("P1 火球命中验收（临时）");
                    try
                    {
                        ball.transform.position = position;
                        var projectile = ball.AddComponent<SkillProjectile>();
                        projectile.impactPrefab = prefab;
                        projectile.ImpactPool = pool;
                        projectile.ImpactOwner = effects;
                        GameObject result = null;
                        projectile.ImpactCreated = item => result = item;
                        projectile.Initialize(Vector3.forward, root.transform, ~0, 0, null, null);
                        return result;
                    }
                    finally { if (ball) Object.DestroyImmediate(ball); }
                }

                Require(PlayProjectile() == visual && target.CurrentHealth == 75 && pool.ActiveCount == 1,
                    "真实碰撞没有结算一次伤害、播放池中的视觉");
                Require(!PlayProjectile() && target.CurrentHealth == 50 && pool.SkippedAtCapacity == 2,
                    "满池必须继续结算第二次伤害");
                pool.CollectExpired(double.PositiveInfinity);
                Require(pool.AvailableCount == 1 && !visual.activeSelf, "到期没有归还");
                pool.TryPlayImpact(prefab, position, Quaternion.identity, effects, out var last);
                pool.DisposePool();
                Require(pool.PrewarmedCount == 0 && pool.ActiveCount == 0 && !last.Release(), "卸载没有清空对象池或租约");
                return checks;
            }
            finally
            {
                if (pool) pool.DisposePool();
                if (targetObject) Object.DestroyImmediate(targetObject);
                if (root) Object.DestroyImmediate(root);
            }
        }
    }
}
