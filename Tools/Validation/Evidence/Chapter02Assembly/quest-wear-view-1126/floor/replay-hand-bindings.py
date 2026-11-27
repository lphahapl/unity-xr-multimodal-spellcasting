"""Read-only source and geometry replay for the Chapter 2 physical pen and ration pouch."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import sys

import numpy as np


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def docs(text):
    documents = re.split(r"^--- !u!\d+ &(-?\d+)(?: stripped)?\s*$", text, flags=re.M)
    return dict(zip(documents[1::2], documents[2::2]))


def ref(doc, field):
    return re.search(r"^  " + field + r": \{fileID: (-?\d+)", doc, re.M)[1]


def vector(doc, field):
    entry = re.search(r"^  " + field + r": \{([^}]+)\}", doc, re.M)[1]
    return np.array([float(value) for value in re.findall(r"[xyz]: ([-+.0-9eE]+)", entry)])


def scalar(text, field):
    return float(re.search(r"^  " + field + r": ([-+.0-9eE]+)$", text, re.M)[1])


def one(documents, fragment):
    return next(value for value in documents.values() if fragment in value)


def rotation(yaw, pitch):
    y, p = math.radians(yaw), math.radians(pitch)
    return np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]]) @ np.array([
        [1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])


parser = argparse.ArgumentParser()
parser.add_argument("--project", type=Path, default=Path("D:/UnityProjects/NarutoVR"))
args = parser.parse_args()
project = args.project
files = {}
def read(path):
    file = project/path
    files[str(path)] = sha(file)
    return file.read_text("utf-8-sig")


scene = read("Assets/Scene/Chapter02_SuspendedPath.unity")
sdk = Path("Library/PackageCache/com.meta.xr.sdk.interaction@61177ebcfd9a/Runtime")
s = docs(scene)
source = one(s, "leftHand: {fileID: 1508473943}")
lab = one(s, "leftTrackedHand: {fileID: 0}")
space = ref(lab, "recordingOrigin")
assert space == "79008832" and ref(lab, "rightTrackedHand") == "0"
transformer = one(s, "TrackingSpace: {fileID: "+space+"}")
assert transformer == s["379468230"] and scene.count("_trackingToWorldTransformer: {fileID: 379468230}") == 2
rig = docs(read(sdk/"UnityXR/Prefabs/XROriginCameraRig.prefab"))
rig_source = re.search(r"m_CorrespondingSourceObject: \{fileID: (\d+), guid: f9f107e8ecae49048bda9d4fd7e1d1ec", s[space])[1]
rig_space_go = ref(rig[rig_source], "m_GameObject")
assert "m_Name: TrackingSpace" in rig[rig_space_go]
assert ref(one(rig, "m_CameraFloorOffsetObject:"), "m_CameraFloorOffsetObject") == rig_space_go
eye_go = next(k for k, v in rig.items() if "m_Name: CenterEyeAnchor\n" in v)
assert ref(one(rig, "m_GameObject: {fileID: "+eye_go+"}\n  serializedVersion: 2\n  m_LocalRotation:"), "m_Father") == rig_source

sdk_assembly_flags = read("Library/Bee/artifacts/1300b0aE.dag/Oculus.Interaction.UnityXR.rsp")
assert "-define:ISDK_OPENXR_HAND" in sdk_assembly_flags
sdk_pose = read(sdk/"OpenXR/OpenXRHandPrimitives.cs")
assert "HandStart = 0" in sdk_pose and "HandIndexTip = HandStart + 10" in sdk_pose
visuals = {}
for side in ["Left", "Right"]:
    path = sdk/"UnityXR/Prefabs/Hands"/("UnityXRHandVisual"+side+".prefab")
    visual = docs(read(path))
    guid = re.search("guid: ([a-f0-9]+)", read(str(path)+".meta"))[1]
    visual_doc = one(visual, "_openXRJointTransforms:")
    assert scalar(visual_doc, "_updateRootPose") == scalar(visual_doc, "_updateRootScale") == 1
    transforms = re.findall(r"^  - \{fileID: (\d+)\}$", visual_doc.split("_openXRJointTransforms:", 1)[1], re.M)
    tip_id = transforms[10]
    tip_name = re.search(r"  m_Name: (.+)", visual[ref(visual[tip_id], "m_GameObject")])[1]
    mesh_id = ref(visual_doc, "_openXRSkinnedMeshRenderer")
    assert len(transforms) == 26 and tip_name == "XRHand_IndexTip" and "SkinnedMeshRenderer:" in visual[mesh_id]
    hand = ref(source, side.lower()+"Hand")
    bind = one(s, "m_SourcePrefab: {fileID: 100100000, guid: "+guid)
    assert re.search(r"propertyPath: _hand\n      value: *\n      objectReference: \{fileID: "+hand+r"\}", bind)
    visuals[side] = {"prefab":str(path), "handFileId":hand, "skinFileId":mesh_id, "indexTipFileId":tip_id, "indexTipName":tip_name}

markers = {
    sdk/"UnityXR/Scripts/FromUnityXRHandDataSource.cs": ["_dataAsset.Root = hand.rootPose", "PoseUtils.Delta(_dataAsset.Root, pose)", "Config.TrackingToWorldTransformer = TrackingToWorldTransformer"],
    sdk/"Scripts/Input/Hands/Hand.cs": ["_jointPosesCache.GetWorldJointPose(handJointId)", "TrackingToWorldTransformer?.Transform"],
    sdk/"Scripts/Input/Hands/HandJointsCache.cs": ["base.Update(dataVersion, data.Root, data.JointPoses, data.HandScale, trackingSpace)"],
    sdk/"Scripts/Input/TransformTrackingToWorldTransformer.cs": ["trackingToWorldSpace.TransformPoint(pose.position)"],
    sdk/"Scripts/Interaction/Visuals/HandVisual.cs": ["Root.position = handRootPose.position", "Root.localScale = Hand.Scale / parentScale", "Joints[i].SetPose(localJoints[i], Space.Self)"],
    "Assets/scripts/logic/SealLab/SealLab.cs": ["hand.GetJointPose((HandJointId)i", "recordingOrigin.InverseTransformPoint(pose.position)", "xrHandsSource.Capture(true, recordingOrigin"],
    "Assets/scripts/logic/SealLab/SealXRHandsSource.cs": ["SealLab.CaptureHand(hand, origin", "XRHandJointID.Wrist => (int)HandJointId.HandWristRoot", "snapshot.jointValid[i] &= available && valid[i]"],
    "Assets/scripts/logic/Rpg/PlayerInputController.cs": ["f.rightTip=World(hand.joints[tip].position)", "result.leftIndex = World(frame.left.joints[index].position)", "Tracking.recordingOrigin.TransformPoint(position)"],
    "Assets/scripts/logic/Rpg/Glyphs/GlyphPenController.cs": ["carriedBelt.StepFromGlyph(hand, now)"],
    "Assets/scripts/logic/Interaction/Items/ToolBeltInteraction.cs": ["!input.tipUnavailable && input.gripPoseValid && input.pinchValid", "if (!Held) PickNearest(input.rightTip)", "float sq = (tip - item.GripPosition).sqrMagnitude", "(tip - dock.opening.position).sqrMagnitude", "TryTake(nearest, false)"],
    "Assets/scripts/logic/Interaction/Items/TravelRationInteraction.cs": ["if (leftDown && TryLiftBag(0, left))", "if (rightDown) TryLiftBag(1, right)", "new PinchSample((a + b) * .5f", "(sample.Position - pouch.GripPosition).sqrMagnitude > bagReach * bagReach", "sharedBelt.TryTake(pouch, true)"],
}
lines = {}
for path, pieces in markers.items():
    content = read(path)
    lines[str(path)] = {piece: content[:content.index(piece)].count("\n")+1 for piece in pieces}

pen_prefab = docs(read("Assets/Art/Props/ShenShangToolBelt/ChapterToolBelt.prefab"))
pen_collider = pen_prefab["6575151201179753195"]
pen_grip_file = ref(one(pen_prefab, "grip: {fileID: 5337688327506670471}"), "grip")
pen_grip_src = re.search(r"m_CorrespondingSourceObject: \{fileID: (\d+),", pen_prefab[pen_grip_file])[1]
pen = docs(read("Assets/Art/Props/MoGuiPen/MoGuiPen.prefab"))
pen_grip = vector(pen[pen_grip_src], "m_LocalPosition")
assert np.all(pen_grip == 0)
instance = pen_prefab[ref(pen_prefab[pen_grip_file], "m_PrefabInstance")]
for axis in "xyz":
    assert re.search(r"propertyPath: m_LocalPosition\."+axis+r"\n      value: 0\n", instance)
    assert re.search(r"propertyPath: m_LocalRotation\."+axis+r"\n      value: 0\n", instance)
assert "propertyPath: m_LocalScale" not in instance and np.all(vector(pen[ref(pen[pen_grip_src], "m_Father")], "m_LocalScale") == 1)
pouch = docs(read("Assets/Art/Props/TravelRation/TravelRationPouch.prefab"))
pouch_collider = one(pouch, "BoxCollider:")
pouch_grip = vector(pouch[ref(one(pouch, "  grip:"), "grip")], "m_LocalPosition")
assert ref(pouch_collider, "m_GameObject") == ref(pouch[ref(pouch[ref(one(pouch,"  grip:"),"grip")],"m_Father")],"m_GameObject")
assert scalar(pen_collider,"m_IsTrigger") == scalar(pouch_collider,"m_IsTrigger") == 0
assert scalar(pen_collider,"m_Enabled") == scalar(pouch_collider,"m_Enabled") == 1
geometry = {}
cases = []
for item, collider, grip, reach in [
    ("PhysicalMoGuiPen", pen_collider, pen_grip, scalar(one(pen_prefab,"  reach:"),"reach")),
    ("TravelRationPouch", pouch_collider, pouch_grip, scalar(one(s,"  bagReach:"),"bagReach"))]:
    center, half = vector(collider, "m_Center"), vector(collider, "m_Size") / 2
    low, high = center-half, center+half
    geometry[item] = {"grip":grip.tolist(),"boxCenter":center.tolist(),"boxSize":(half*2).tolist(),"grabRadius":reach}
    if item == "PhysicalMoGuiPen":
        examples = [("tail-top touch", [0,high[1],low[2]],False), ("nib-end top touch",[0,high[1],high[2]],False), ("middle-top touch",[0,high[1],0],True)]
        turn = rotation(31,0)
    else:
        examples = [("bottom seam touch",[0,low[1],high[2]],False),("grip seam touch",[0,grip[1],high[2]],True)]
        turn = rotation(-41,-90)
    for label, local, expected in examples:
        target = np.array(local)
        closest = np.clip(target,low,high)
        surface_gap = float(np.linalg.norm(target-closest))
        anchor_gap = float(np.linalg.norm(target-grip))
        accepted = anchor_gap <= reach
        assert accepted == expected and surface_gap < 1e-12
        for floor in (-33.0,0.0,80.0):
            corners = [turn @ np.array([x,y,z]) for x in [low[0],high[0]] for y in [low[1],high[1]] for z in [low[2],high[2]]]
            translation = np.array([3.4,floor-min(c[1] for c in corners),-8.6])
            tip_world = translation + turn @ target
            assert abs(np.linalg.norm(tip_world - (translation+turn@grip)) - anchor_gap) < 1e-10
            cases.append({"item":item,"contact":label,"floorY":floor,"worldContactY":float(tip_world[1]),"surfaceGapMm":round(1000*surface_gap,4),"gripGapMm":round(1000*anchor_gap,4),"radiusMm":round(1000*reach,4),"existingPickupAccepts":bool(accepted)})

worst = 0.
cases_tracking = 0
for side, direction in [("Left",-1),("Right",1)]:
    for floor in [-33.,0.,80.]:
        for local_floor in [0.,1.65]:
            for origin_yaw in [0.,67.,-136.]:
                for posture in [.08,.45,1.02]:
                    tr = np.array([-7.2,floor+local_floor,3.1])
                    rot = rotation(origin_yaw,0)
                    root = np.array([direction*.2,posture-local_floor,.3])
                    joint = np.array([direction*.21,-local_floor,.37])
                    wrist_rot = rotation(-direction*41.,25)
                    baked = wrist_rot.T@(joint-root)
                    visual = tr + rot@root + rot@wrist_rot@baked
                    sdk_input = tr + rot@(root+wrist_rot@baked)
                    capture = rot.T@(sdk_input-tr)
                    tip = tr + rot@capture
                    assert abs(tip[1]-floor)<1e-10
                    worst = max(worst,float(np.linalg.norm(tip-visual)))
                    cases_tracking += 1
assert worst < 1e-10
out = Path(__file__).resolve().parent
report = {"kind":"static binding + deterministic numerical replay (no Unity Play and no HMD)",
          "sceneSHA":files["Assets/Scene/Chapter02_SuspendedPath.unity"], "files":files,"lineMarkers":lines,
          "trackingSpace":{"sceneTransform":space,"sdkPrefabTransform":rig_source,"sdkGameObject":rig_space_go,"transformer":"379468230"},
          "officialHands":visuals,"rigidSpaceReplays":cases_tracking,"maximumVisualToInputErrorM":worst,
          "itemGeometry":geometry,"actualPrefabContactReplays":cases}
(out/"result.json").write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding="utf-8")
print(report["sceneSHA"], cases_tracking, worst)
for case in cases:
    if case["floorY"] == 80:
        print(case)
