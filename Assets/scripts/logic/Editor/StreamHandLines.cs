#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace NarutoVR.SealCombat.Editor
{
    // Play-only renderer. The empty scene stays unchanged on disk.
    internal sealed class StreamHandLines
    {
        readonly List<XRHandSubsystem> sources=new();
        readonly LineRenderer[,] lines=new LineRenderer[2,5];
        readonly Transform origin;
        XRHandSubsystem source;
        double retry;
        public int Updates {get;private set;}
        public int BothVisible {get;private set;}
        public StreamHandLines(Transform origin) {
            this.origin=origin;
            var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("f95c8cec0450dc54e8eb0deb6a517a16"));
            var parent=new GameObject("诊断用双手骨骼");
            for(int side=0;side<2;side++)for(int finger=0;finger<5;finger++){
                var line=new GameObject("指骨").AddComponent<LineRenderer>();
                line.transform.SetParent(parent.transform,false); line.sharedMaterial=mat;line.widthMultiplier=.006f;
                line.enabled=false;lines[side,finger]=line;
            }
        }
        public void Poll() {
            if(source!=null && source.running)return;
            if(Time.realtimeSinceStartupAsDouble<retry)return;
            retry=Time.realtimeSinceStartupAsDouble+1;
            Detach();sources.Clear();SubsystemManager.GetSubsystems(sources);
            source=sources.Find(s=>s.running);
            if(source!=null)source.updatedHands+=Update;
        }
        public void Detach() { if(source!=null)source.updatedHands-=Update;source=null; }
        void Update(XRHandSubsystem sender,XRHandSubsystem.UpdateSuccessFlags flags,XRHandSubsystem.UpdateType phase) {
            if(phase!=XRHandSubsystem.UpdateType.Dynamic)return;
            Updates++;
            bool left=Draw(0,sender.leftHand,(flags&XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints)!=0);
            bool right=Draw(1,sender.rightHand,(flags&XRHandSubsystem.UpdateSuccessFlags.RightHandJoints)!=0);
            if(left&&right)BothVisible++;
        }
        bool Draw(int side,XRHand hand,bool fresh) {
            bool visible=false;
            for(int finger=0;finger<5;finger++){
                var line=lines[side,finger]; if(!line)continue;
                var id=(XRHandFingerID)finger;
                int lo=id.GetFrontJointID().ToIndex(), hi=id.GetBackJointID().ToIndex();
                bool valid=fresh && hand.isTracked;
                line.positionCount=hi-lo+2;
                for(int j=lo-1; valid && j<=hi;j++){
                    var joint=j<lo?XRHandJointID.Wrist:XRHandJointIDUtility.FromIndex(j);
                    valid=hand.GetJoint(joint).TryGetPose(out var pose);
                    if(valid)line.SetPosition(j-lo+1,origin?origin.TransformPoint(pose.position):pose.position);
                }
                line.enabled=valid;visible|=valid;
            }
            return visible;
        }
    }
}
#endif
