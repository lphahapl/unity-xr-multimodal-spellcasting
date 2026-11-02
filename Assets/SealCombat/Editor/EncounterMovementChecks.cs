#if UNITY_EDITOR
using System;
using UnityEngine;
using NarutoVR.SealCombat.Rpg;
using Object=UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EncounterMovementChecks
    {
        public static int Run()
        {
            using var f=new Fixture();
            var c=f.C;
            var initial=c.rigRoot.position;
            c.SetEncounterCombat(false);
            f.Tick(1,true,.16f);f.Tick(1.04,true,.16f);
            Require(c.NavigationActive && c.rigRoot.position.z>initial.z,"探索指向沿水平视线步行");var last=c.rigRoot.position;
            f.Tick(1.08,false,.16f);Require(!c.NavigationActive && c.rigRoot.position==last,"收指立即停止");
            c.head.localRotation=Quaternion.Euler(55,0,0); c.pitchSlowsWalk=true;
            f.Tick(1.12,true,.16f);float slow=c.rigRoot.position.z-last.z;
            Require(slow>0 && slow<c.exploreSpeed*.039f && Mathf.Abs(c.rigRoot.position.y-initial.y)<.001f,"抬低头减速但不沿斜向爬升");
            c.head.localRotation=Quaternion.identity; c.SetEncounterCombat(true);f.Tick(1.16,true,.16f);
            Require(c.EncounterDebugDetails.Contains("等待有效收指") && !c.NavigationActive,"战斗切换不继承旧的伸指");
            f.Tick(1.20,false,.16f); c.NotifyProjectileLaunched(1.21);
            f.Tick(1.25,false,.36f,true); f.Tick(1.30,false,.60f);
            Require(!c.EncounterSlideActive,"投掷的前推不能连带闪避");
            f.Tick(1.35,false,.16f); f.Tick(1.40,false,.16f); f.Tick(1.50,false,.34f);
            Require(c.EncounterSlideActive,"空手主动回胸后，新快推可发起滑退");
            var before=c.rigRoot.position;c.head.localRotation=Quaternion.Euler(0,90,0);
            f.Tick(1.55,false,.34f);var step=c.rigRoot.position-before;
            Require(step.z<0 && Mathf.Abs(step.x)<.001f && step.magnitude<c.dodgeDistance && c.DodgeRemaining>0,
                "后退分帧移动，方向锁在起手头向且只成功移动才计冷却");
            for(int i=1;i<=8;i++)f.Tick(1.55+i*.05,false,.34f);
            Require(!c.EncounterSlideActive && Mathf.Abs((c.rigRoot.position-before).magnitude-c.dodgeDistance)<.005f,"完整滑退距离");
            c.head.localRotation=Quaternion.identity;c.NotifyProjectileLaunched(4);c.throwDodgeRecovery=.25f;
            f.Tick(4.10,false,.50f);f.Tick(4.30,false,.52f);f.Tick(4.36,false,.52f);
            Require(!c.EncounterSlideActive,"保护计时结束不能让静止或仍伸着的手自动闪避");
            f.Tick(4.46,false,.72f);Require(c.EncounterSlideActive,"发射保护计时结束，稳定后无须回胸也可用新动作闪避");
            c.SetEncounterCombat(false);f.Tick(5,false,.16f);f.Tick(5.10,false,.35f);
            Require(!c.EncounterSlideActive && !c.TryDebugDodge(Vector2.up,5.12),"非战斗空手快推和调试闪避均被拦下");
            c.SetEncounterCombat(true);f.Tick(6,false,.16f);f.Tick(6.05,false,.16f);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="测试阻挡";wall.transform.SetParent(f.Root.transform);
            wall.transform.position=c.rigRoot.position+new Vector3(0,.8f,-.57f);
            wall.transform.localScale=new Vector3(2,1.6f,.1f);Physics.SyncTransforms();
            var wallStart=c.rigRoot.position;f.Tick(6.15,false,.36f);for(int i=1;i<=8;i++)f.Tick(6.15+i*.05,false,.36f);
            Require(!c.EncounterSlideActive && c.rigRoot.position.z>wallStart.z-.5f,"背靠墙滑退不可穿过薄墙");
            return 12;
        }
        static void Require(bool ok,string label){if(!ok)throw new InvalidOperationException("遭遇移动："+label);}
        sealed class Fixture:IDisposable
        {
            public readonly GameObject Root;public readonly RpgLocomotionController C;
            public Fixture()
            {
                Root=new GameObject("临时遭遇移动检查");Root.transform.position=new Vector3(900,0,900);
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(Root.transform,false);
                ground.transform.localPosition=Vector3.down*.1f;ground.transform.localScale=new Vector3(14,.2f,14);
                var rig=new GameObject("Rig");rig.transform.SetParent(Root.transform,false);
                var head=new GameObject("Head");head.transform.SetParent(rig.transform,false);head.transform.localPosition=Vector3.up*1.6f;
                C=rig.AddComponent<RpgLocomotionController>();C.rigRoot=rig.transform;C.head=head.transform;C.encounterModesEnabled=true;C.useRayNavigation=true;
                Physics.SyncTransforms();
            }
            public void Tick(double time,bool point,float rightDistance,bool owned=false)
            {
                Vector3 head=C.head.position;
                C.ProcessInput(new NavigationHandInput {leftObserved=time,rightObserved=time,leftValid=true,rightValid=true,
                    leftPointing=point,leftWrist=head+new Vector3(-.45f,-.28f,.2f),leftIndex=head+new Vector3(-.45f,-.22f,.27f),
                    rightWrist=head+new Vector3(.08f,-.32f,rightDistance),rightMotionOwnedBySpell=owned},false,false,time);
            }
            public void Dispose(){Object.DestroyImmediate(Root);}
        }
    }
}
#endif
