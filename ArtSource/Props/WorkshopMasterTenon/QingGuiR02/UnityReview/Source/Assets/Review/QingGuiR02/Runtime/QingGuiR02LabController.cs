using NarutoVR.SealCombat.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NarutoVR.QingGuiR02Review
{
    // Desktop review controls call the current item API; this is not simulated Quest tracking.
    public sealed class QingGuiR02LabController : MonoBehaviour
    {
        public GameItemBase tenon;
        public QingGuiR02ReviewZone socket;
        public Transform displayOrigin, inspectionOrigin;
        public Transform handScaleReference;
        public string status="QingGuiR02 independent review. No chapter unlock or inventory registration.";
        public bool keyboardEnabled=true, showPanel=true;
        Vector3 gripTarget;
        Quaternion gripRotation=Quaternion.identity;
        public bool Take()
        {
            if(socket.IsSnapping)return false;
            if(socket.SnappedObject==tenon.gameObject)socket.ReleaseObject();
            bool accepted=tenon.Take();
            if(accepted) {gripTarget=tenon.GripPosition;gripRotation=tenon.transform.rotation;status="Taken via GameItemBase.Take";}
            return accepted;
        }
        public void MoveTo(Vector3 rootPosition,Quaternion rotation)
        {
            gripRotation=rotation;
            gripTarget=rootPosition+rotation*tenon.grip.localPosition;
            tenon.MoveHeld(gripTarget,gripRotation);
            status="Moved via GameItemBase.MoveHeld";
        }
        public void Inspect() {if(tenon.IsHeld)MoveTo(inspectionOrigin.position,inspectionOrigin.rotation);}
        public void AboveOpening() {if(tenon.IsHeld)MoveTo(socket.alignment.position,socket.alignment.rotation);}
        public void Drop()
        {
            tenon.Drop();status="Released via GameItemBase.Drop: gravity and collisions restored";
        }
        public bool ReleaseAndInsert()
        {
            Drop(); bool ok=socket.TryInsertAssigned();
            status=ok?"SnapZone aligning, then inserting into actual socket":socket.LastFailure;
            return ok;
        }
        public void ResetToDisplay()
        {
            if(socket.SnappedObject)socket.ReleaseObject();
            tenon.Drop();
            var rb=tenon.GetComponent<Rigidbody>();rb.isKinematic=false;rb.useGravity=true;
            rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;
            tenon.transform.SetPositionAndRotation(displayOrigin.position,displayOrigin.rotation);
            Physics.SyncTransforms();status="Returned to separate station 03 (review reset only)";
        }
        void Update()
        {
            if(keyboardEnabled && Keyboard.current!=null) {
                var k=Keyboard.current;
                if(k.digit1Key.wasPressedThisFrame)Take();
                if(k.digit2Key.wasPressedThisFrame)Inspect();
                if(k.digit3Key.wasPressedThisFrame)Drop();
                if(k.digit4Key.wasPressedThisFrame)Take();
                if(k.digit5Key.wasPressedThisFrame)AboveOpening();
                if(k.digit6Key.wasPressedThisFrame)ReleaseAndInsert();
                if(k.rKey.wasPressedThisFrame)ResetToDisplay();
                if(tenon.IsHeld) {
                    float d=.12f*Time.deltaTime;
                    gripTarget+=new Vector3((k.rightArrowKey.isPressed?d:0)-(k.leftArrowKey.isPressed?d:0),(k.pageUpKey.isPressed?d:0)-(k.pageDownKey.isPressed?d:0),(k.upArrowKey.isPressed?d:0)-(k.downArrowKey.isPressed?d:0));
                    float a=30f*Time.deltaTime*((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0));
                    gripRotation*=Quaternion.Euler(0,a,0);
                }
            }
            if(tenon.IsHeld)tenon.MoveHeld(gripTarget,gripRotation);
        }
        void OnGUI()
        {
            if(!showPanel)return;
            GUILayout.BeginArea(new Rect(12,12,455,260),GUI.skin.box);
            GUILayout.Label("WORKSHOP MASTER TENON QingGuiR02 | 1:1 desktop review");
            GUILayout.Label("03 TAKE and 04 CONSOLE are separate example stations.");
            GUILayout.Label("1 Take | 2 Move near hand | 3 Drop | 4 Take again");
            GUILayout.Label("5 Above socket | 6 Release / insert | R Reset");
            GUILayout.Label("Arrow / PageUp / PageDown: move held item; Q / E: yaw");
            GUILayout.Label("No chapter distance, unlock, hand tracking or belt registration.");
            if(GUILayout.Button("1 Take"))Take();
            if(GUILayout.Button("2 Inspect"))Inspect();
            if(GUILayout.Button("5 Above real opening"))AboveOpening();
            if(GUILayout.Button("6 Release and insert"))ReleaseAndInsert();
            GUILayout.Label(status+" | completions "+socket.completedCount);
            GUILayout.EndArea();
        }
    }
}
