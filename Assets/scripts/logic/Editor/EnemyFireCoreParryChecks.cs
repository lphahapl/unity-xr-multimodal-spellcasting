using System;
using System.Collections.Generic;
using System.Linq;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyFireCoreParryChecks
    {
        [MenuItem("NarutoVR/战斗检查/衡印双射手弹反（Play）")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("请在 Play 中运行弹反检查");
            Vector3 origin = new Vector3(120, 50, 180);
            using var player = new EnemyAttackChecks.RpgFixture(origin, "buff.zi", "buff.voice", "shield.left");
            var objects = new List<GameObject>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab");
            var clock = typeof(LinearRpgController).GetProperty(nameof(LinearRpgController.GameTime));
            void TimeAt(double now) => clock.SetValue(player.Controller, now);
            EnemyController Shooter(Vector3 offset)
            {
                var instance = Object.Instantiate(prefab, origin + offset, Quaternion.identity);
                objects.Add(instance);
                var enemy = instance.GetComponent<EnemyController>();
                enemy.tickAutomatically = false;
                enemy.SetTarget(player.Receiver.HitTransform);
                return enemy;
            }
            EnemyFireCore Shoot(EnemyController enemy, float speed)
            {
                var barrel = enemy.Entity.RangedAttack;
                barrel.speed = speed;
                var previous = Object.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None);
                if (!enemy.Entity.BeginStrike(0, out var reason)) throw new InvalidOperationException(reason);
                enemy.Entity.EvaluateStrike(enemy.Entity.Attacks[0].activeStart, false, 1.4f, ~0);
                barrel.SendMessage("LateUpdate");
                var core = Object.FindObjectsByType<EnemyFireCore>(FindObjectsSortMode.None).Except(previous).Single();
                objects.Add(core.gameObject);
                Physics.SyncTransforms();
                return core;
            }
            void Expect(bool ok, string label)
            {
                if (!ok) throw new InvalidOperationException("衡印弹反失败：" + label);
                Debug.Log("[衡印弹反通过] " + label);
            }
            try
            {
                var a = Shooter(new Vector3(-3, 2, 5));
                var b = Shooter(new Vector3(2, 2, 4));
                float full = a.Entity.Combat.Health;
                var first = Shoot(a, 6);
                var second = Shoot(b, 9);
                float mana = player.Controller.Player.Mana;
                TimeAt(1);
                Expect(player.Controller.DebugCast("shield.left") && player.Controller.Player.Mana == mana - 15 &&
                    !player.Controller.DebugCast("buff.voice"), "手印开盾、扣 15 度力、口令服从共享冷却");
                TimeAt(1.30);
                first.Advance(2); second.Advance(2);
                Vector3 firstContact = first.transform.position, secondContact = second.transform.position;
                first.Advance(.1f); second.Advance(.1f);
                Expect(Mathf.Abs(Vector3.Distance(firstContact, first.transform.position) - .6f) < .001f &&
                    Mathf.Abs(Vector3.Distance(secondContact, second.transform.position) - .9f) < .001f &&
                    player.Effects.ShieldPoints == 100, "两枚交叉来弹在 0.300 秒边界弹反，各自保持 6 / 9 m/s");
                second.Advance(2);
                Expect(b.Entity.Combat.Health == full - 80 && a.Entity.Combat.Health == full, "后发火核先返回，只扣射手 B 80 血");
                first.Advance(2); first.Advance(2);
                Expect(a.Entity.Combat.Health == full - 80 && b.Entity.Combat.Health == full - 80 &&
                    a.CurrentBehaviour == EnemyController.Behaviour.Hit, "射手 A 单次反伤 80 并进入受击行动锁");
                var animator = a.Entity.Animator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                bool enteredHit = false;
                for (int frame = 0; frame < 18; frame++)
                {
                    animator.Update(1f / 60);
                    a.Entity.ReadReactionAnimation(EnemyReaction.Hit, out bool hit, out _);
                    enteredHit |= hit;
                }
                Expect(enteredHit, "掷火残机正式 Animator 进入配置的后仰受击动作");
                a.Tick(2.99f);
                Expect(a.CurrentBehaviour == EnemyController.Behaviour.Hit, "2.99 秒仍硬直");
                a.Tick(.011f);
                Expect(a.CurrentBehaviour == EnemyController.Behaviour.Idle, "3 秒后可恢复行动");
                TimeAt(1.301);
                Shoot(a, 6).Advance(2);
                Expect(player.Effects.ShieldPoints == 100 - a.Entity.Attacks[0].damage && player.Controller.Player.Health == 100,
                    "0.301 秒的地面火核只扣原始攻击伤害对应的盾量");
                TimeAt(5);
                Expect(player.Controller.DebugCast("buff.voice"), "口令衡印成功开盾");
                var miss = Shoot(a, 6);
                miss.Advance(2);
                var original = a.transform.position;
                a.transform.position += Vector3.right * 2;
                Physics.SyncTransforms();
                miss.Advance(2);
                Expect(a.Entity.Combat.Health == full - 80 && player.Effects.ShieldPoints == 100, "原主移开后直线火核脱靶");
                a.transform.position = original;
                var blocked = Shoot(a, 6); blocked.Advance(2);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                objects.Add(wall); wall.transform.position = blocked.transform.position + blocked.transform.forward * 2;
                Physics.SyncTransforms(); blocked.Advance(2); wall.SetActive(false);
                Expect(a.Entity.Combat.Health == full - 80, "回程遮挡使火核在墙面终止");
                var stale = Shoot(b, 9); stale.Advance(2); b.ResetForSpawn(); stale.Advance(2);
                Expect(b.Entity.Combat.Health == full && a.Entity.Combat.Health == full - 80, "原主复用后火核作废，其他射手及新生命均未受伤");
                var orphan = Shoot(b, 9); orphan.Advance(2); b.gameObject.SetActive(false); orphan.Advance(2);
                Expect(a.Entity.Combat.Health == full - 80, "原主退场后不转给另一名射手");
                TimeAt(9);
                Expect(player.Controller.DebugCast("buff.zi"), "规印护盾仍可用");
                Shoot(a, 6).Advance(2);
                Expect(player.Effects.ShieldPoints == 50 - a.Entity.Attacks[0].damage && a.Entity.Combat.Health == full - 80, "规印沿用普通挡伤");
                player.Effects.CancelLatest();
                Shoot(a, 6).Advance(2);
                Expect(player.Controller.Player.Health == 100 - a.Entity.Attacks[0].damage, "撤盾后原火核正常扣玩家生命");
            }
            finally
            {
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i]) Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
