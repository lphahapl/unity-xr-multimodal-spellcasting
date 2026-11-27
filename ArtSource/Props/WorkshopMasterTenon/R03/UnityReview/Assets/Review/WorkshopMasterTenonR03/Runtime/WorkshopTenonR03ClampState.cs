using UnityEngine;

namespace NarutoVR.WorkshopTenonR03Review
{
    // Independent mechanical preview only. No enemy, chapter, Take lock or XR event binding.
    public sealed class WorkshopTenonR03ClampState : MonoBehaviour
    {
        public Rigidbody specimen;
        public Transform[] pins;
        public Vector3[] closedPositions,openPositions;
        public bool shoulderProposal;
        public bool showPanel=true;
        public bool IsOpen {get;private set;}
        public void SetOpen(bool value)
        {
            IsOpen=value;
            for(int i=0;i<pins.Length;i++)pins[i].localPosition=value?openPositions[i]:closedPositions[i];
        }
        public void ResetSpecimen()
        {
            if(!specimen.isKinematic){specimen.linearVelocity=Vector3.zero;specimen.angularVelocity=Vector3.zero;}
            specimen.isKinematic=true;specimen.useGravity=false;
            specimen.position=transform.position;specimen.rotation=transform.rotation;
            specimen.transform.SetPositionAndRotation(transform.position,transform.rotation);
        }
        void OnGUI()
        {
            if(!showPanel)return;
            GUILayout.BeginArea(new Rect(12,12,430,135),GUI.skin.box);
            GUILayout.Label(shoulderProposal?"R03 shoulder-pin proposal / NOT chapter implementation":"Current installed top latch / unchanged dimensions");
            if(GUILayout.Button("Closed"))SetOpen(false);
            if(GUILayout.Button("Retract 57.5 mm"))SetOpen(true);
            if(GUILayout.Button("Reset specimen"))ResetSpecimen();
            GUILayout.EndArea();
        }
    }
}
