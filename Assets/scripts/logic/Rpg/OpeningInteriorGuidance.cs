using UnityEngine;

namespace NarutoVR.SealCombat.Rpg
{
    [DisallowMultipleComponent]
    public sealed class OpeningInteriorGuidance : MonoBehaviour
    {
        public RpgStageDefinition movement, nearShot, sideShot, shield;
        public GameObject floorInlay, tigerBoard, ratBoard, shieldLauncher;
        public SkillTrainingTarget primary, secondary;
        public Material activeFace, restingFace;
        public GameObject primarySight, secondarySight;

        public void Show(OpeningSection section, OpeningLesson line)
        {
            var stage = section?.stage;
            floorInlay.SetActive(stage && stage == movement);
            tigerBoard.SetActive(stage && (stage == nearShot || stage == sideShot));
            ratBoard.SetActive(stage && stage == shield);
            if (shieldLauncher) shieldLauncher.SetActive(stage && stage == shield);

            var aim = line?.action == OpeningAction.HitTarget ? line.target : null;
            primary.targetRenderer.sharedMaterial = aim == primary ? activeFace : restingFace;
            secondary.targetRenderer.sharedMaterial = aim == secondary ? activeFace : restingFace;
            primarySight.SetActive(aim == primary);
            secondarySight.SetActive(aim == secondary);
        }
    }
}
