using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NarutoVR.SealCombat.Items;
using NarutoVR.SealCombat.Rpg;
using NeuGameMRTools.Runtime.Components;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static class RationPickupChecks
    {
        // Run in a private Play session on either the chapter player or the standalone food lab.
        public static IEnumerator Run(LinearRpgController player, Action<string> record)
        {
            var grab = Object.FindObjectsByType<TravelRationInteraction>(FindObjectsSortMode.None).Single(x => x.player == player);
            var supply = player.GetComponent<ChapterRationSupply>();
            var deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (!player.CanAct || (grab.sharedBelt && !grab.sharedBelt.Initialised))
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Ration test setup");
                yield return null;
            }
            if (!grab.pouch && (!supply || !supply.TryGrant())) throw new InvalidOperationException("Ration QA grant failed");
            var bag = grab.pouch;
            var home = bag.home as ToolBeltSlot;
            if (!home) throw new InvalidOperationException("Missing authored ration pocket");
            while (bag.Dock != home || home.IsSnapping)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Ration initial dock");
                yield return null;
            }
            player.Player.Respawn(20, 10, player.GameTime);
            var head = player.locomotion.head;
            var normal = head.forward;
            var rotation = head.rotation;
            var holdAt = head.TransformPoint(new Vector3(-.2f, -.15f, .42f));
            var rightAt = head.TransformPoint(new Vector3(.24f, -.15f, .38f));
            var mouthAt = grab.mouth.transform.position - grab.mouth.transform.forward * .022f;
            var left = default(PlayerHandObservation);
            var right = default(PlayerHandObservation);
            double t = Time.realtimeSinceStartupAsDouble;
            void Check(bool ok, string what)
            {
                if (!ok) throw new InvalidOperationException("干粮远抓：" + what);
                record("干粮远抓：" + what);
            }
            PlayerHandObservation Pose(Vector3 pos, Vector3 target, bool pinched)
            {
                var dir = (target - pos).normalized;
                if (dir.sqrMagnitude < .5f) dir = normal;
                return new PlayerHandObservation {
                    rightValid=true, rightPointing=true, rightOpen=!pinched, rightPalmValid=true,
                    rightTip=pos, rightWrist=pos-dir*.08f, rightIndexRayValid=true, rightIndexDirection=dir,
                    rayAnchorValid=true, rayValid=true, rayDirection=dir, rayOrigin=pos,
                    gripPoseValid=true, gripPosition=pos, gripRotation=rotation,
                    pinchValid=true, systemPinch=true, systemPressed=pinched, pinchDistance=pinched ? .012f : .06f,
                    headPosition=head.position, headForward=normal
                };
            }
            void Sample(bool useLeft, Vector3 pos, Vector3 target, bool down)
            {
                if (useLeft) left=Pose(pos,target,down);
                else right=Pose(pos,target,down);
                t+=.016;
                left.rightObserved=right.rightObserved=t;
                Physics.SyncTransforms();
                player.Input.Pickups.Evaluate(left,right,t);
            }
            void Pinch(bool useLeft, Vector3 pos, Vector3 target)
            {
                Sample(useLeft,pos,target,false);
                Sample(useLeft,pos,target,true);
            }
            TravelRationInteraction.PinchSample Finger(Vector3 pos,bool down) => new(pos,rotation,down ? .012f : .06f);
            void Move(Vector3 l,bool downL,Vector3 r,bool downR,float dt=.016f) =>
                grab.Step(Finger(l,downL),Finger(r,downR),dt,false);

            // Real belt pocket. The shooting hand remains outside all old proximity radii.
            var eyeToBag = (head.position - bag.GripPosition).normalized;
            Pinch(true,bag.GripPosition+eyeToBag*.26f,bag.GripPosition);
            Check(grab.BagHand==0 && bag.IsHeld && bag.PickupSource==home &&
                (!grab.sharedBelt || grab.sharedBelt.OwnsHeldItem(bag)),"左手从中央袋口远取原干粮包，沿用原所有者");
            Move(holdAt,true,rightAt,false);
            var foldedTarget=bag.biscuits[3];
            Check(!bag.Open && !grab.TryPickup(foldedTarget,false,Pose(rightAt,foldedTarget.position,true)),
                "未展开时不能隔袋取饼");
            bag.AdvanceOpening(1f);
            var piece=bag.biscuits[3];
            Pinch(false,rightAt,piece.position);
            Check(grab.HasServing && bag.InPouch==3 && bag.ConsumedMask==0,"持袋时另一手射线取出一块，尚未消耗");
            grab.Step(Finger(holdAt,true),default,.5f,false);
            Move(holdAt,true,rightAt,false);
            Check(grab.HasServing && player.Player.Health==20,"短时失追后张手不会误吃或放掉饼");
            Move(holdAt,true,rightAt,true);
            Move(holdAt,true,rightAt,false);
            Check(!grab.HasServing && bag.InPouch==4 && bag.ConsumedMask==0,"重新确认捏合再松手，原饼归包");

            Pinch(false,rightAt,piece.position);
            Check(grab.HasServing,"远取归还的同一饼");
            for(int i=0;i<6;i++) Move(holdAt,true,mouthAt,true,.08f);
            var mouth=grab.mouth.GetComponent<SnapZone>();
            Check(mouth.IsSnapping && mouth.SnappedObject,"嘴部停留后进入原有最后吸附");
            // Advance only the final animation, with actual completion event and settlement.
            typeof(SnapZone).GetMethod("Advance",BindingFlags.NonPublic|BindingFlags.Instance)
                .Invoke(mouth,new object[]{1f});
            Move(holdAt,true,mouthAt,true);
            Move(holdAt,true,mouthAt,true,.5f);
            Check(!grab.HasServing && bag.InPouch==3 && player.Player.Health==35 && bag.ConsumedMask!=0,
                "一块仅结算一次 +15 气血，持续捏合不重发");

            var horizontal = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var farRelease = head.position + horizontal * .80f - Vector3.up * .15f;
            Move(farRelease,true,rightAt,false);
            Move(farRelease,false,rightAt,false);
            Check(!bag.Dock && !bag.IsHeld && !player.Input.InteractionOwnsHand,"将原包拉远松手，世界内掉落");
            Pinch(false,rightAt,bag.GripPosition);
            Check(grab.BagHand==1 && bag.IsHeld,"右手射线从世界重新取得同一干粮包");
            rotation = home.opening.rotation * Quaternion.Euler(bag.stowEuler);
            Move(holdAt,false,home.opening.position,true);
            Move(holdAt,false,home.opening.position,false);
            Check(bag.Dock==home && !player.Input.InteractionOwnsHand,"右手将包放入原中央口并交回所有权");
        }
    }
}
