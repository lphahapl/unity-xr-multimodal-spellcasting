using System.Collections;
using System.Linq;
using NarutoVR.SealCombat.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NarutoVR.SealCombat.Editor
{
    public static partial class ChapterSupplyChecks
    {
        public static void Ink()
        {
            if (!System.IO.File.Exists(InkRubbingLabBuilder.ScenePath)) InkRubbingLabBuilder.Build();
            Start("ink", InkRubbingLabBuilder.ScenePath);
        }

        static partial void ResolveLab(string name, ref IEnumerator test)
        {
            if (name == "ink") test = Grey();
        }
        static IEnumerator Grey()
        {
            var lab=Object.FindFirstObjectByType<ItemLabControls>();
            yield return Wait(()=>lab.player.CanAct && lab.Initialised && lab.slots.All(s=>!s.IsSnapping),"greybox belt ready");
            var ink=lab.items.OfType<PineInkPrototype>().Single();
            var plate=lab.items.Single(i=>i.Kind==GameItemKind.BronzeRubbing);
            Check(ink.Dock && plate.Dock && !ink.testRecoveryOnBreak,"two independent greybox items seated, prototype recovery defaults off");
            Check(lab.TryTake(ink,true),"ink taken with standard belt claim");
            var state=lab.player.Player;state.Respawn(60,10,lab.player.GameTime);
            Check(!ink.BreakOne(state) && ink.Remaining==2,"closed sleeve rejects break");
            void Grip(Vector3 point,bool down) => ink.StepGrip(new TravelRationInteraction.PinchSample(point,ink.transform.rotation,down ? .012f : .065f),state);
            var handle=ink.sleeve.position;
            Vector3 WorldOffset(Vector3 local) => ink.transform.TransformVector(local);
            Grip(handle,false);Grip(handle,true);Grip(handle+WorldOffset(Vector3.up * (ink.slideTravel * .4f)),true);
            Check(!ink.SleeveOpen && ink.Remaining==2,"partial slide does not unlock a segment");
            Grip(handle+WorldOffset(Vector3.up * (ink.slideTravel * 1.1f)),true);
            Check(ink.SleeveOpen && state.Mana==10,"sliding cover and exposing ink do not recover DuLi");
            var part=ink.segments[1].position;
            Grip(part,false);Grip(part,true);Grip(part+WorldOffset(Vector3.up * .04f),true);
            Check(ink.Remaining==2,"straight extraction is not a scored-line break");
            var rest=ink.segments[1].localRotation;
            Grip(part+WorldOffset(Vector3.right * .003f),true);
            Check(Quaternion.Angle(rest,ink.segments[1].localRotation)>5f && ink.Remaining==2,"partial bend rotates visible segment around score");
            ink.StepGrip(default,state);
            Check(Quaternion.Angle(rest,ink.segments[1].localRotation)<.01f && ink.Remaining==2,"tracking loss restores unsnapped segment");
            Grip(part,false);Grip(part,true);
            ink.StepGrip(new TravelRationInteraction.PinchSample(part,ink.transform.rotation*Quaternion.Euler(0,0,40),.012f),state);
            Grip(part+WorldOffset(Vector3.right * .04f),true);
            Check(ink.Remaining==1 && state.Mana==10,"one segment per tracked pinch; unapproved recovery stays disabled");
            ink.testRecoveryOnBreak=true;float before=state.Mana;
            Check(ink.BreakOne(state) && Mathf.Approximately(state.Mana-before,ink.recovery.amount) && !ink.BreakOne(state),"opt-in break settlement uses existing DuLi effect once; no sniff settlement");
            lab.ReleaseAuxiliary(ink,true);yield return Wait(()=>lab.slots.All(s=>!s.IsSnapping),"ink release");
            Check(lab.TryTake(plate,true),"rubbing taken with same belt claim");
            var target=lab.slots.OfType<ToolBeltSlot>().First(t=>t!=plate.PickupSource && !t.SnappedObject && !t.penHolster && t.width>=.031f && t.depth>=.045f && t.thickness>=.002f);
            plate.MoveHeld(target.opening.position,target.opening.rotation);lab.ReleaseAuxiliary(plate,true);
            yield return Wait(()=>plate.Dock==target && !target.IsSnapping,"rubbing moved to other legal pocket");
            Check(plate.LastDock==target && !(plate is ConsumableItem),"rubbing changes actual dock without consumption or special effect");
        }
    }
}
