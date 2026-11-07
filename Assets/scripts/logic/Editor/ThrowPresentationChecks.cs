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
                return 2 + CheckGazePreview();
            }
            finally
            {
                if(root) Object.DestroyImmediate(root);
                if(ring) Object.DestroyImmediate(ring);
            }
        }

        static int CheckGazePreview()
        {
            var root=new GameObject("火球瞄准检查"){hideFlags=HideFlags.HideAndDontSave};
            var visual=new GameObject("测试持球预置体"){hideFlags=HideFlags.HideAndDontSave};
            var world=new GameObject("火球瞄准物理场"){hideFlags=HideFlags.HideAndDontSave};
            var definition=ScriptableObject.CreateInstance<ProjectileEffectDefinition>();
            var skill=ScriptableObject.CreateInstance<SkillDefinition>();
            GameObject ball=null;
            try
            {
                var effects=root.AddComponent<SkillEffects>();
                root.transform.position=new Vector3(240,1.7f,910);
                effects.head=effects.playerRoot=root.transform; effects.hitLayers=1<<30; effects.aimDistance=8;
                double clock=100; bool tracked=true;
                effects.GameClock=effects.HandInputClock=()=>clock;
                Vector3 wrist=root.transform.position+new Vector3(.5f,-.24f,.32f);
                effects.WristPoseOverride=_=>tracked?new Pose(wrist,Quaternion.Euler(0,130,0)):(Pose?)null;
                effects.ThrowGripProvider=(_,__)=>new ThrowGripSample{observed=clock,fistValid=true,fistOpen=true};
                definition.releaseMode=ProjectileReleaseMode.GripThrow; definition.throwLocalOffset=Vector3.zero;
                definition.visualPrefab=visual; skill.effectDefinition=definition;
                Require(effects.TryActivate(skill),"生成持球失败");
                ball=(GameObject)typeof(SkillEffects).GetField("heldObject",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(effects);
                var tick=typeof(SkillEffects).GetMethod("UpdateHeld",BindingFlags.NonPublic|BindingFlags.Instance);
                void Tick() { clock+=.02; tick.Invoke(effects,new object[]{clock}); }
                void CheckPreview(Vector3 expected)
                {
                    Require(effects.TryGetThrowPreview(out var aim) && Vector3.Distance(aim.gazePoint,expected)<.002f &&
                        Vector3.Distance(aim.origin,wrist)<.002f && Vector3.Dot(aim.direction,(expected-wrist).normalized)>.99999f,
                        "持球点应指向视线目标，腕骨旋转不能改变准星。");
                }
                GameObject Box(string label,Vector3 relative,Vector3 size,Transform parent,bool trigger=false)
                {
                    var go=new GameObject(label){layer=30};go.transform.SetParent(parent);
                    go.transform.position=root.transform.position+relative;
                    var box=go.AddComponent<BoxCollider>();box.size=size;box.isTrigger=trigger;return go;
                }
                CheckPreview(root.transform.position+Vector3.forward*8);
                root.transform.rotation=Quaternion.Euler(-12,28,0);
                CheckPreview(root.transform.position+root.transform.forward*8);
                for(int i=0;i<36;i++)Tick();
                Require(!effects.ThrowArmed&&effects.LaunchCount==0&&effects.HasHeldObject,"未握持的静态抬腕瞄准不得发射。");
                root.transform.rotation=Quaternion.identity;

                Box("忽略玩家",Vector3.forward*.8f,new Vector3(.3f,.3f,.08f),root.transform);
                Box("忽略触发器",Vector3.forward*1.2f,new Vector3(.3f,.3f,.08f),world.transform,true);
                var target=Box("准星靶",Vector3.forward*4,new Vector3(.5f,.5f,.3f),world.transform);
                target.AddComponent<SkillTrainingTarget>();Physics.SyncTransforms();
                Vector3 point=root.transform.position+Vector3.forward*3.85f;
                CheckPreview(point);
                Require(effects.TryGetThrowPreview(out var clear) && !clear.blocked && clear.endpoint.z<point.z &&
                    clear.endpoint.z>point.z-.14f,"准星应停在火球半径首先碰到靶面的位置。");
                var wall=Box("只遮挡出手路径的墙",new Vector3(.36f,-.17f,1.35f),new Vector3(.26f,.3f,.08f),world.transform);
                Physics.SyncTransforms();
                Require(effects.TryGetThrowPreview(out var blocked) && blocked.blocked &&
                    Vector3.Distance(blocked.gazePoint,point)<.002f && blocked.endpoint.z<wall.transform.position.z &&
                    Vector3.Dot(blocked.direction,clear.direction)>.99999f,
                    "视线能看到靶、手边弹道被墙拦住时，线必须停在墙前且不可偏转绕墙。");
                wall.transform.position=wrist;Physics.SyncTransforms();
                Require(!effects.TryGetThrowPreview(out _),"出手点被动态障碍占据时不能显示可用弹道。");
                Object.DestroyImmediate(wall);Physics.SyncTransforms();

                wrist=root.transform.position+new Vector3(.5f,-.65f,.32f);Tick();
                Require(!effects.TryGetThrowPreview(out _),"放下手应隐藏准星。");
                wrist.y=root.transform.position.y-.24f;Tick();CheckPreview(point);
                effects.ThrowInputSuppressed=true;
                Require(!effects.TryGetThrowPreview(out _),"UI占用时不能显示准星。");effects.ThrowInputSuppressed=false;
                clock+=.25;
                Require(!effects.TryGetThrowPreview(out _),"过期观测不能保留准星。");
                Tick();CheckPreview(point);
                tracked=false;Tick();Require(!effects.TryGetThrowPreview(out _),"持球手失踪时准星必须消失。");
                return 6;
            }
            finally
            {
                if(ball)Object.DestroyImmediate(ball);
                Object.DestroyImmediate(world);Object.DestroyImmediate(root);Object.DestroyImmediate(visual);
                Object.DestroyImmediate(skill);Object.DestroyImmediate(definition);Physics.SyncTransforms();
            }
        }
        static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
    }
}
#endif
