#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NarutoVR.SealCombat.Enemies;
using NarutoVR.SealCombat.Rpg;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EnemyDeathPlaybackChecks
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        const string PrefabPath = "Assets/Art/Enemies/CanJiOu/CanJiOu_Enemy.prefab";
        static readonly int Dead = Animator.StringToHash("Base Layer.Dead");

        /// <summary>在隔离 Play 场景使用正式模型和 NavMesh，推进 Animator、自队列和出生租约。</summary>
        public static async Task Run(CombatRoomEncounter room, Action<string> record)
        {
            if (!Application.isPlaying || !room || room.spawnPoints.Length==0) throw new InvalidOperationException("请在有出生点的隔离 Play 房间运行");
            var spawn = room.spawnPoints[0];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var managerRoot = new GameObject("死亡播放检查池");
            managerRoot.SetActive(false);
            var manager = managerRoot.AddComponent<EnemyManager>(); manager.catalog = room.enemyManager.catalog;
            managerRoot.SetActive(true);
            var owned = new List<GameObject>();
            var originalScale = Time.timeScale;
            void Check(bool ok,string label) { if (!ok) throw new InvalidOperationException(label); record("PASS "+label); }
            EnemyController Standalone()
            {
                var go = Object.Instantiate(prefab,spawn.point.position,spawn.point.rotation);
                owned.Add(go); go.SetActive(true);
                var c=go.GetComponent<EnemyController>(); c.tickAutomatically=false;
                return c;
            }
            async Task<EnemyLease> Spawn()
            {
                var pending = manager.SpawnEnemyAsync(spawn.enemyId,spawn.point.position,spawn.point.rotation,null);
                await EnemyPoolingChecks.WithTimeout(pending);
                var lease = await pending; lease.Controller.tickAutomatically=false; return lease;
            }
            void Kill(EnemyController c)
            {
                Check(c.Entity.ReceiveHit(Guid.NewGuid().ToString("N"),10000,0) && c.IsDead && c.Entity.Combat.Dead,
                    "真实伤害提交死亡，实体仍在场");
            }
            void Sample(EnemyController c, float seconds)
            {
                c.Entity.Animator.Update(seconds);
                c.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
            }
            float Enter(EnemyController c)
            {
                for (int i=0;i<40;i++)
                {
                    Sample(c,.025f);
                    var a=c.Entity.Animator;
                    var s=a.GetCurrentAnimatorStateInfo(0);
                    if (s.fullPathHash!=Dead || s.normalizedTime<.12f) continue;
                    var clips=a.GetCurrentAnimatorClipInfo(0);
                    if (clips.Length==0 || !clips[0].clip) throw new InvalidOperationException("Dead 状态没有活动剪辑");
                    Check(!clips[0].clip.isLooping && s.normalizedTime<.9f,
                        $"正式 Dead 片段 {clips[0].clip.name}，长度 {clips[0].clip.length:F3}s，当前 {s.normalizedTime:F3}");
                    return s.length;
                }
                throw new InvalidOperationException("正式 Animator 未进入 Dead 状态");
            }
            async Task EndFrame() { int frame=Time.frameCount; while(Time.frameCount<=frame+1) await Task.Yield(); }
            int TrackedInstances()
            {
                object session = typeof(EnemyManager).GetField("session", Private).GetValue(manager);
                object instances = session.GetType().GetField("AllInstances").GetValue(session);
                return (int)instances.GetType().GetProperty("Count").GetValue(instances);
            }
            try
            {
                var single=Standalone(); single.Entity.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                Kill(single); float length=Enter(single);
                Check(single && single.gameObject.activeInHierarchy, "非池死亡动画开头保留原实例");
                Sample(single,length*.45f);
                Check(single && single.gameObject.activeInHierarchy && single.Entity.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime<1,
                    "正式剪辑中段仍在场");
                Sample(single,length);
                await EndFrame();
                Check(!single,"非池独立敌人播放结束后销毁原实例");

                var old=await Spawn(); var pooled=old.Controller;
                var originalCulling=AnimatorCullingMode.CullCompletely;
                pooled.Entity.Animator.cullingMode=originalCulling;
                pooled.Entity.Animator.updateMode=AnimatorUpdateMode.UnscaledTime;
                Kill(pooled);
                Check((bool)typeof(EnemyLease).GetProperty("Defeated").GetValue(old) && manager.IsCurrent(old),"致死当帧封存遭遇击败事实，与稍后的收尸分离");
                var defeatedLife=pooled.Entity.Combat;
                pooled.ResetForSpawn(); pooled.InitializeSpawn(null,false);
                Check(pooled.IsDead && ReferenceEquals(pooled.Entity.Combat,defeatedLife) && pooled.Entity.Combat.Dead,
                    "尚未换发租约时公开复位和出生入口不能复活旧生命");
                Check(pooled.Entity.Animator.cullingMode==AnimatorCullingMode.AlwaysAnimate,"屏外死亡片段仍推进");
                length=Enter(pooled);
                Check(manager.IsCurrent(old) && !pooled.CanReceiveHit,"播放中仍由本次租约持有，但停止受击");
                float t=pooled.Entity.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                pooled.SetSuspended(true); Sample(pooled,length*2);
                Check(manager.IsCurrent(old) && Mathf.Abs(pooled.Entity.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime-t)<.001f,
                    "显式暂停冻结实际动画及清理");
                pooled.SetSuspended(false);
                Time.timeScale=0; await EndFrame();
                // Unity 在新帧应用 timeScale，切换当帧可能已分配了上一个非零 deltaTime。
                t=pooled.Entity.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                await EndFrame();
                Check(manager.IsCurrent(old) && Mathf.Abs(pooled.Entity.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime-t)<.002f,
                    "游戏暂停期间保持当前位置及租约");
                Time.timeScale=originalScale;
                var playback=typeof(EnemyController).GetField("deathPlayback",Private);
                var finished=typeof(EnemyController).GetMethod("FinishDeathPlayback",Private);
                object oldCompletion=playback.GetValue(pooled);
                pooled.Entity.Animator.speed=.1f;
                Sample(pooled,length*1.2f);
                Check(manager.IsCurrent(old) && pooled.Entity.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime<.9f,
                    "慢放超过一段剪辑的标称秒数仍保留未播毕的实例");
                pooled.Entity.Animator.speed=1;
                Sample(pooled,length);
                Check(!manager.IsCurrent(old) && manager.PooledCount==1 && !pooled.gameObject.activeSelf,
                    "自然播毕回池一次并撤销当前租约");
                Check((bool)typeof(EnemyLease).GetProperty("Defeated").GetValue(old),"旧出生的死亡事实可在回收后读取");
                Check(pooled.Entity.Animator.cullingMode==originalCulling && pooled.Entity.Animator.updateMode==AnimatorUpdateMode.UnscaledTime,
                    "回池时归还 Animator 原剔除与更新模式");
                var reused=await Spawn();
                finished.Invoke(pooled,new[]{oldCompletion});
                Check(ReferenceEquals(pooled,reused.Controller) && manager.IsCurrent(reused) && !pooled.IsDead &&
                    !(bool)typeof(EnemyLease).GetProperty("Defeated").GetValue(reused) && !manager.Recycle(old),
                    "旧完成回调与旧租约均不能回收复用的新生命");
                Check(manager.Recycle(reused) && !(bool)typeof(EnemyLease).GetProperty("Defeated").GetValue(reused),
                    "普通回收存活敌人不记作击败");
                var interrupted=await Spawn(); Kill(interrupted.Controller);
                object interruptedCompletion=playback.GetValue(interrupted.Controller);
                Check(manager.Recycle(interrupted),"强制清场可立即收回播放中的敌人");
                var afterReset=await Spawn(); finished.Invoke(afterReset.Controller,new[]{interruptedCompletion});
                Check(manager.IsCurrent(afterReset) && !afterReset.Controller.IsDead,"强制清场之后的迟到完成不影响新出生");
                Check(manager.Recycle(afterReset),"清理下一组用例的存活实例");
                foreach (bool componentOnly in new[]{false,true})
                {
                    var disabled=await Spawn(); var actor=disabled.Controller; Kill(actor);
                    object callback=playback.GetValue(actor);
                    var actorRoot=actor.gameObject;
                    if(componentOnly) actor.enabled=false;
                    else actorRoot.SetActive(false);
                    string mode=componentOnly?"单独禁用控制器":"关闭整只敌人";
                    Check(manager.ActiveCount==0 && !manager.Recycle(disabled),mode+"立即撤销死亡租约");
                    Check(TrackedInstances()==1,mode+"后在 Unity 实际销毁前继续登记克隆");
                    actorRoot.SetActive(true); actor.enabled=true;
                    Check(actor.IsDead && actor.Entity.Combat.Dead && !actor.CanReceiveHit,mode+"后同帧重新启用不能复活");
                    finished.Invoke(actor,new[]{callback});
                    await EndFrame();
                    Check(!actorRoot && manager.PooledCount==0,mode+"后整只失效实例在帧末销毁且没有重复入池");
                    Check(TrackedInstances()==0,mode+"后长期会话下次更新已移出销毁记录");
                }
                if (!manager.catalog.TryGet(spawn.enemyId,out var definition)) throw new InvalidOperationException("出生类型没有敌人定义");
                var capacity=definition.maxInactive;
                var overflow=new List<EnemyLease>();
                for (int i=0;i<=capacity;i++) overflow.Add(await Spawn());
                foreach (var lease in overflow) manager.Recycle(lease);
                Check(manager.PooledCount==capacity && TrackedInstances()==capacity+1,"池满销毁前所有待卸载克隆仍在会话登记");
                await EndFrame();
                Check(manager.PooledCount==capacity && TrackedInstances()==capacity,"池满后持续会话只保留实际闲置克隆");
                afterReset=await Spawn();
                Kill(afterReset.Controller);
                manager.enabled=false;
                Check(manager.ActiveCount==0 && !manager.IsCurrent(afterReset),"关闭会话立即使播放中的租约失效");

                var retained=Standalone();
                var sceneOwner=typeof(EnemyController).GetMethod("SetSceneRespawnOwner",Private);
                sceneOwner.Invoke(retained,new object[]{true});
                Kill(retained); length=Enter(retained); Sample(retained,length);
                Check(retained && !retained.gameObject.activeSelf,"房间预放置敌人播毕隐藏并保留引用");
                retained.gameObject.SetActive(true);
                Check(!retained.IsDead && !retained.Entity.Combat.Dead,"房间重新启用得到新生命");
                Kill(retained); retained.enabled=false;
                Check(!retained.gameObject.activeSelf,"预置死者的控制器被禁用后关闭整只敌人");
                retained.gameObject.SetActive(true);
                Check(retained.isActiveAndEnabled && !retained.IsDead,"预置死者外部禁用后仍可由房间重生");
                var disabledSingle=Standalone(); Kill(disabledSingle); disabledSingle.enabled=false;
                disabledSingle.enabled=true;
                Check(disabledSingle.IsDead && !disabledSingle.CanReceiveHit,"独立死者的控制器同帧重开不能复活");
                await EndFrame();
                Check(!disabledSingle,"独立死者的控制器被禁用后整只物体销毁");
                var broken=Standalone(); broken.Entity.Animator.enabled=false;
                Kill(broken); await EndFrame();
                Check(!broken,"缺少可用 Animator 的独立死者在下一次收尾清理");
            }
            finally
            {
                Time.timeScale=originalScale;
                if (managerRoot) Object.Destroy(managerRoot);
                foreach (var go in owned) if (go) Object.Destroy(go);
            }
        }
    }
}
#endif
