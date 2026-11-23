#if UNITY_EDITOR
using System;
using UnityEngine;
using NarutoVR.SealCombat.Rpg;
using Object=UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class EncounterMovementChecks
    {
        public static int Run() => CheckEncounterMotion() + CheckWallGlancing() + CheckExplorationChoices()
            + CheckExplorationMarks() + CheckGroundDwell(ExplorationMovement.Teleport) + CheckGroundDwell(ExplorationMovement.AutoWalk);

        static int CheckExplorationMarks()
        {
            using var f = new Fixture();
            var move = f.C;
            var go = new GameObject("既有战斗点", typeof(LineRenderer));
            go.transform.SetParent(f.Root.transform, false);
            go.transform.localPosition = Vector3.forward * 3;
            var line = go.GetComponent<LineRenderer>();
            var node = go.AddComponent<NavigationNode>();
            NavigationNodeFixture.SetRegistered(node, true);
            var marks = new OpeningNavigationMarks(f.Root.transform, move, null, null, null);
            var marker = f.Root.transform.Find("地面落点纹样");
            var filter = marker.GetComponent<MeshFilter>();
            var renderer = marker.GetComponent<MeshRenderer>();
            try
            {
                move.SetEncounterCombat(true);
                marks.Refresh(true, false, default);
                Require(renderer.enabled && filter.sharedMesh.GetIndexCount(0) > 0 && !line.forceRenderingOff,
                    "战斗预设点保留候选纹样和原有标记");
                move.SetEncounterCombat(false);
                foreach (var mode in new[] { ExplorationMovement.Teleport, ExplorationMovement.AutoWalk })
                {
                    move.SetExplorationMode(mode);
                    marks.Refresh(true, false, default);
                    Require(!renderer.enabled && line.forceRenderingOff, mode + "空闲时无预设点");
                    var point = f.Root.transform.position + new Vector3(.83f, 0, 2.17f);
                    Require(Physics.Raycast(point + Vector3.up, Vector3.down, out var floor, 2), "空白地面命中");
                    marks.Refresh(true, true, floor);
                    Require(renderer.enabled && filter.sharedMesh.vertexCount == 4 && filter.sharedMesh.GetIndexCount(0) == 0 &&
                        filter.sharedMesh.GetIndexCount(1) == 6, mode + "只显示一个探索落点");
                    marks.Refresh(true, false, default);
                    Require(!renderer.enabled, mode + "取消时收起唯一落点");
                }
                move.SetEncounterCombat(true);
                marks.Refresh(true, false, default);
                Require(renderer.enabled && filter.sharedMesh.GetIndexCount(0) > 0 && !line.forceRenderingOff,
                    "切回战斗恢复候选和原有线圈");
                return 10;
            }
            finally
            {
                NavigationNodeFixture.SetRegistered(node, false);
                Object.DestroyImmediate(filter.sharedMesh);
            }
        }

        static int CheckGroundDwell(ExplorationMovement mode)
        {
            using var f = new Fixture(splitGround: true);
            var move = f.C;
            move.SetEncounterCombat(false);
            move.SetExplorationMode(mode);
            move.exploreSpeed = 3;
            Vector3 start = move.PlayerFloorPosition;
            double time = 1;
            int warps = 0;
            move.MovementCommitted += m => { if (m.Kind == PlayerMovementKind.Navigation) warps++; };
            void Sample(float x, float z, bool pointing = true, bool guideOnly = false, bool blocked = false)
            {
                time += .02;
                Vector3 wrist = move.head.position + new Vector3(-.25f, -.35f, .18f);
                move.ProcessInput(new NavigationHandInput {
                    leftValid = true, leftObserved = time, leftWrist = wrist, leftIndex = wrist,
                    leftNavigationPointing = pointing, leftIndexAim = guideOnly, leftPoseUnconfirmed = guideOnly,
                    leftOpen = !pointing && !guideOnly, leftRayValid = pointing || guideOnly,
                    leftRayOrigin = wrist, leftRayDirection = (start + new Vector3(x, 0, z) - wrist).normalized
                }, false, blocked, time);
            }
            Sample(0, 5, false);
            for (int i = 0; i < 20; i++) Sample(0, 5, false, guideOnly: true);
            Require(move.TryGetGroundAimGuide(out _, out var end) && Mathf.Abs(end.z - start.z - 5) < .02f &&
                !move.TryGetTeleportPreview(out _, out _) && move.DwellProgress == 0 && move.PlayerFloorPosition == start,
                "仅伸直食指可预瞄，未确认的手型不会选中落点");
            Sample(0, 5, false, guideOnly: true, blocked: true);
            Require(!move.TryGetGroundAimGuide(out _, out _), "界面接管立即收起探索引导线");
            for (int i = 0; i < 24; i++) Sample(i % 6 < 3 ? -1.2f : 1.2f, 6);
            Require(move.PlayerFloorPosition == start, "明显切换瞄准点须重新驻留");
            for (int i = 0; i < 24; i++) Sample(i % 2 == 0 ? -.03f : .03f, i % 2 == 0 ? 5.76f : 6.24f);
            Require(move.PlayerFloorPosition.z > start.z && warps == (mode == ExplorationMovement.Teleport ? 1 : 0),
                mode + "在同层地砖接缝和远处小角度抖动下仍能出发");
            if (mode == ExplorationMovement.Teleport)
            {
                Require(Vector3.Distance(move.PlayerFloorPosition, start + new Vector3(-.03f, 0, 5.76f)) < .015f,
                    "传送至已展示的落点");
                return 5;
            }
            Vector3 beforeHitch = move.PlayerFloorPosition;
            time += .22;
            Sample(0, 6, false);
            Require(move.PlayerFloorPosition == beforeHitch && move.NavigationActive, "长帧收到新鲜样本时保留路线，不补跳步长");
            for (int i = 0; i < 10; i++) Sample(0, 6, false);
            for (int i = 0; i < 145; i++) Sample(2, 6);
            Require(move.PlayerFloorPosition.x > start.x + .4f, "途中收指后，到站可直接驻留选下一点");
            return 6;
        }

        static int CheckExplorationChoices()
        {
            using var f = new Fixture();
            var move = f.C;
            move.SetEncounterCombat(false);
            double time = 8;
            var start = move.PlayerFloorPosition;
            var goal = start + Vector3.forward * 2;
            int warps = 0;
            move.MovementCommitted += movement => { if (movement.Kind == PlayerMovementKind.Navigation) warps++; };
            void Sample(bool point = false, bool fist = false, bool blocked = false, bool handValid = true, bool ground = false)
            {
                time += .02;
                var wrist = move.head.position + new Vector3(-.3f, -.25f, .25f);
                move.ProcessInput(new NavigationHandInput {
                    leftValid = handValid, leftObserved = time, leftWrist = wrist, leftIndex = wrist,
                    leftPointing = point, leftClosed = fist, leftOpen = !point && !fist, leftRayValid = point,
                    leftRayOrigin = wrist, leftRayDirection = ground ? (goal - wrist).normalized : Vector3.right
                }, false, blocked, time);
            }
            Sample(); Sample(fist:true);
            Require(move.PlayerFloorPosition == start, "原地握拳不会起跑");
            Sample(point:true);
            var walked = move.PlayerFloorPosition;
            Sample(fist:true);
            float runDistance = (move.PlayerFloorPosition - walked).x;
            Require(walked.x > start.x && Mathf.Abs(walked.z-start.z)<.001f &&
                runDistance > (walked.x-start.x)*1.5f, "缺少拳头方向样本时沿最近一次有效指向加速");
            Sample(handValid:false); var stopped = move.PlayerFloorPosition; Sample(fist:true);
            Require(move.PlayerFloorPosition == stopped, "追踪恢复后握拳不能自行起跑");
            move.SetExplorationMode(ExplorationMovement.Teleport);
            Sample();
            for (int i=0; i<30; i++) Sample(point:true,ground:true);
            Require(warps==1 && Vector3.Distance(move.PlayerFloorPosition,goal)<.01f, "纯传送在驻留后一次到点");
            goal += Vector3.right;
            for (int i=0; i<22; i++) Sample(point:true,ground:true);
            Require(warps==1, "持续同一次指向不能连跳");
            move.SetExplorationMode(ExplorationMovement.AutoWalk);
            Sample(); var beforeAuto = move.PlayerFloorPosition;
            for (int i=0; i<20; i++) Sample(point:true,ground:true);
            Require(move.PlayerFloorPosition.x > beforeAuto.x && move.PlayerFloorPosition.x < goal.x-.5f && warps==1, "选点自动行走逐步出发");
            for (int i=0; i<75; i++) Sample();
            Require(Vector3.Distance(move.PlayerFloorPosition,goal)<.015f && !move.NavigationActive && warps==1, "松开指向后自动走到终点并停止");
            goal += Vector3.forward;
            for (int i=0; i<23; i++) Sample(point:true,ground:true);
            Sample(blocked:true); stopped=move.PlayerFloorPosition;
            for (int i=0; i<25; i++) Sample();
            Require(stopped.z > beforeAuto.z && move.PlayerFloorPosition==stopped, "界面接管立即终止路线");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(f.Root.transform);
            wall.transform.position = stopped + new Vector3(0,.8f,.48f);
            wall.transform.localScale = new Vector3(2,1.6f,.1f);
            Physics.SyncTransforms();
            move.SetExplorationMode(ExplorationMovement.Teleport); Sample();
            for (int i=0; i<35; i++) Sample(point:true,ground:true);
            Require(move.PlayerFloorPosition==stopped && warps==1, "探索传送不能选择墙后地面");
            return 9;
        }

        static int CheckEncounterMotion()
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
            c.SetEncounterCombat(false); c.SetEncounterCombat(true);
            c.SetEncounterCombat(false); c.SetEncounterCombat(true); c.SetEncounterCombat(false);
            var restart=c.rigRoot.position;
            f.Tick(8,true,.16f);
            Require(!c.NavigationActive && c.rigRoot.position==restart,"多次切换不消费上个模式的伸指");
            f.Tick(8.04,false,.16f); f.Tick(8.08,true,.16f); f.Tick(8.12,true,.16f);
            Require(c.NavigationActive && c.rigRoot.position.z>restart.z,"多次切换后有效收指、再指向应恢复探索行走");
            return 14;
        }
        static int CheckWallGlancing()
        {
            using var f = new Fixture();
            var c = f.C;
            c.SetEncounterCombat(false);
            var start = c.rigRoot.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(f.Root.transform, false);
            wall.transform.localPosition = new Vector3(.34f, .8f, 0);
            wall.transform.localScale = new Vector3(.1f, 1.6f, 1.2f);
            Physics.SyncTransforms();
            c.head.localRotation = Quaternion.Euler(0, 30, 0);
            float reportedTravel = 0f;
            c.ExplorationTravelled += metres => reportedTravel += metres;
            int frame = 0;
            void Walk(int frames)
            {
                for (int i = 0; i < frames; i++) f.Tick(10 + frame++ * .02, true, .16f);
            }
            Walk(32);
            Vector3 along = c.rigRoot.position - start;
            Require(along.z > .44f && along.x > .04f && along.x <= .29f - c.bodyRadius + .002f,
                $"斜向贴墙应保留切向步行且不穿墙，实际 {along:F3}");
            Require(reportedTravel >= along.magnitude - .002f && reportedTravel <= c.exploreSpeed * .62f + .002f,
                "脚步累计应采用贴边后的实际位移");
            Walk(65);
            Require(c.rigRoot.position.x > start.x + .4f && c.rigRoot.position.z > start.z + 1.3f,
                "擦过有限墙角后应恢复原头向行走");
            c.rigRoot.position = start;
            c.head.localRotation = Quaternion.Euler(0, 90, 0);
            Walk(50);
            Vector3 atWall = c.rigRoot.position;
            Walk(12);
            Require(atWall.x <= start.x + .29f - c.bodyRadius + .002f && c.rigRoot.position == atWall &&
                Mathf.Abs(atWall.z - start.z) < .002f, "正对薄墙必须停下，不能自行横移");
            // A real HMD can put the body just inside the outer capsule before a virtual step.
            c.rigRoot.position = start + Vector3.right * .08f;
            c.head.localRotation = Quaternion.identity;
            Walk(20);
            Require(c.rigRoot.position.z > start.z + .3f && Mathf.Abs(c.rigRoot.position.x - start.x - .08f) < .002f,
                "身体已轻蹭墙面时仍能平行前进");
            c.rigRoot.position = start + Vector3.right * .18f;
            Vector3 embedded = c.rigRoot.position;
            Walk(12);
            Require(c.rigRoot.position == embedded, "深度占位不能借贴边穿出障碍");

            var endWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            endWall.transform.SetParent(f.Root.transform, false);
            endWall.transform.localPosition = new Vector3(0, .8f, .45f);
            endWall.transform.localScale = new Vector3(2, 1.6f, .1f);
            Physics.SyncTransforms();
            c.rigRoot.position = start;
            c.head.localRotation = Quaternion.Euler(0, 30, 0);
            Walk(50);
            Vector3 corner = c.rigRoot.position - start;
            Require(corner.z > .12f && corner.z <= .4f - c.bodyRadius + .002f && corner.x <= .29f - c.bodyRadius + .002f,
                "贴墙进入内角时，第二面墙也必须阻挡");
            c.head.localRotation = Quaternion.Euler(0, 180, 0);
            Walk(16);
            Require(c.rigRoot.position.z < start.z, "转向可从内角退出");
            return 8;
        }
        static void Require(bool ok,string label){if(!ok)throw new InvalidOperationException("遭遇移动："+label);}
        sealed class Fixture:IDisposable
        {
            public readonly GameObject Root;public readonly RpgLocomotionController C;
            public Fixture(bool splitGround = false)
            {
                Root=new GameObject("临时遭遇移动检查");Root.transform.position=new Vector3(900,0,900);
                void Floor(float x, float width)
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ground.transform.SetParent(Root.transform, false);
                    ground.transform.localPosition = new Vector3(x, -.1f, 0);
                    ground.transform.localScale = new Vector3(width, .2f, 14);
                }
                if (splitGround) { Floor(-3.5f, 7); Floor(3.5f, 7); }
                else Floor(0, 14);
                var rig=new GameObject("Rig");rig.transform.SetParent(Root.transform,false);
                var head=new GameObject("Head");head.transform.SetParent(rig.transform,false);head.transform.localPosition=Vector3.up*1.6f;
                C=rig.AddComponent<RpgLocomotionController>();C.rigRoot=rig.transform;C.head=head.transform;C.encounterModesEnabled=true;C.useRayNavigation=true;
                Physics.SyncTransforms();
            }
            public void Tick(double time,bool point,float rightDistance,bool owned=false)
            {
                Vector3 head=C.head.position;
                C.ProcessInput(new NavigationHandInput {leftObserved=time,rightObserved=time,leftValid=true,rightValid=true,
                    leftRayValid=point,leftRayDirection=C.head.forward,leftPointing=point,leftWrist=head+new Vector3(-.45f,-.28f,.2f),leftIndex=head+new Vector3(-.45f,-.22f,.27f),
                    rightWrist=head+new Vector3(.08f,-.32f,rightDistance),rightMotionOwnedBySpell=owned},false,false,time);
            }
            public void Dispose(){Object.DestroyImmediate(Root);}
        }
    }
}
#endif
