"""只读比对原三敌副本与主工程实际引用的剪辑、持姿、场景和双手。不会启动 Unity。"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess


ROOT = Path(__file__).resolve().parents[5]
QA = Path("D:/Temp/NarutoVR-CarrierQA-20261125")
SCENE = Path("Assets/Scene/Chapter02_SuspendedPath.unity")
LOCKED_SCENE = "c56d7380bef12e6104f59a28f1926009e74b425db6c9c62621ee414c142022d2"
CONTROLLER = Path("Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller")
PROFILE = Path("Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset")
REFERENCE = r"\{fileID: (-?\d+), guid: ([a-f0-9]{32}), type: \d+\}"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def resolve_asset(guid):
    paths = subprocess.check_output(["rg", "--crlf", "-l", "-g", "*.meta", "^guid: " + guid + "$", "Assets"],
                                    cwd=ROOT, text=True, encoding="utf-8").splitlines()
    if len(paths) != 1:
        raise ValueError(f"GUID {guid} 对应 {len(paths)} 个项目内源文件")
    return Path(paths[0]).with_suffix("")


def verify(expected_clip, expected_pose, published_r02=False):
    hashes = {}

    def compare(path):
        path = Path(path)
        value = digest(ROOT / path)
        if value != digest(QA / path):
            raise ValueError(f"主工程与副本文件不同：{path}")
        hashes[path.as_posix()] = value
        return value

    if compare(SCENE) != LOCKED_SCENE:
        raise ValueError("输入场景与指定 c56d7380 不符")
    blocks = (QA / SCENE).read_text(encoding="utf-8-sig").split("--- !u!")
    altar, = [b for b in blocks if "  - enemyId: stone-carrier" in b]
    registrations = re.findall(r"  - enemyId: (\S+)\s+point: \{fileID: (\d+)\}\s+isSummoned: (\d+)", altar)
    if sorted(x[0] for x in registrations) != ["canjiou", "stone-carrier", "zhihuocanji"] or any(x[2] != "0" for x in registrations):
        raise ValueError("祭坛编成不是三个正常租约")
    if "  showEnemiesBeforeEngagement: 1" not in altar or "  previewDistance: 32" not in altar:
        raise ValueError("祭坛预展示配置不符")

    for path in [CONTROLLER, PROFILE, "Packages/packages-lock.json", "Packages/manifest.json",
                 "Assets/Art/Enemies/CanJiOu/BallastStone.prefab", "Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab",
                 "Assets/scripts/logic/Enemies/StoneCarrier.cs", "Assets/scripts/logic/Rpg/CombatRoomEncounter.Spawning.cs",
                 "Assets/scripts/logic/Enemies/BallastBasket.cs", "Assets/Art/Glyphs/QianjunTether.asset"]:
        compare(path)
    profile = (QA / PROFILE).read_text(encoding="utf-8-sig")
    pose_path = resolve_asset(re.search("  carryPose: " + REFERENCE, profile)[2])
    pose_hash = compare(pose_path)
    pose = (QA / pose_path).read_text(encoding="utf-8-sig")
    file_id, guid = re.search("  clip: " + REFERENCE, pose).groups()
    states = (QA / CONTROLLER).read_text(encoding="utf-8-sig").split("--- !u!")
    carry, = [state for state in states if re.search(r"^  m_Name: StoneCarry$", state, re.M)]
    if re.search("  m_Motion: " + REFERENCE, carry).groups() != (file_id, guid):
        raise ValueError("Animator StoneCarry 与石根持姿引用了不同子剪辑")
    clip_path = resolve_asset(guid)
    clip_hash = compare(clip_path)
    for asset in (pose_path, clip_path, PROFILE, CONTROLLER):
        compare(str(asset) + ".meta")
    if expected_clip and clip_hash != expected_clip.lower():
        raise ValueError("当前实际剪辑不是交付版本")
    if expected_pose and pose_hash != expected_pose.lower():
        raise ValueError("当前实际石根持姿不是交付版本")

    hands = {}
    for side in ("Left", "Right"):
        pattern = f"com.meta.xr.sdk.interaction@*/Runtime/UnityXR/Prefabs/Hands/UnityXRHandVisual{side}.prefab"
        matches = list((ROOT / "Library/PackageCache").glob(pattern))
        matches = [p for p in matches if (QA / p.relative_to(ROOT)).is_file()]
        prefab, = matches
        path = prefab.relative_to(ROOT)
        compare(path)
        compare(str(path) + ".meta")
        hand_guid = re.search(r"^guid: (\w+)", Path(str(prefab) + ".meta").read_text(), re.M)[1]
        if sum(f"m_SourcePrefab: {{fileID: 100100000, guid: {hand_guid}," in b for b in blocks) != 1:
            raise ValueError(f"未找到唯一的官方 {side} 手实例")
        hands[side] = hand_guid
    result = dict(scene=LOCKED_SCENE, registrations=registrations, previewDistanceMetres=32, handPrefabGUIDs=hands,
                  clip=clip_path.as_posix(), clipFileID=file_id, clipGuid=guid, clipSha256=clip_hash,
                  pose=pose_path.as_posix(), poseSha256=pose_hash, files=hashes, playExecuted=False)
    if published_r02:
        published = ROOT / "Tools/Validation/Evidence/Chapter02Assembly/back-carry-walk/R02/runtime-sha256.json"
        release = json.loads(published.read_text(encoding="utf-8-sig"))
        source = ROOT / "ArtSource/Enemies/CanJiOu/BackCarry/R02/R02-Manifest.json"
        art = json.loads(source.read_text(encoding="utf-8-sig"))
        if (release["revision"] != "R02" or release["chapterSceneSha256"] != LOCKED_SCENE
                or release["files"][pose_path.as_posix()] != pose_hash
                or art["files_sha256"][clip_path.name] != release["files"][clip_path.as_posix()]
                or art["files_sha256"][clip_path.name] != clip_hash
                or digest(source.parent / clip_path.name) != clip_hash):
            raise ValueError("美术来源、发布清单、场景或运行时剪辑不匹配")
        for asset, expected in release["files"].items():
            if compare(asset) != expected:
                raise ValueError(f"发布清单与正式安装不同：{asset}")
        result["publication"] = dict(revision=release["revision"], publishedAt=release["publishedAt"],
                                     sourceManifest=source.relative_to(ROOT).as_posix(),
                                     runtimeManifest=published.relative_to(ROOT).as_posix(), sha256=digest(published),
                                     assetCount=len(release["files"]))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("receipt", type=Path)
    parser.add_argument("--clip-sha")
    parser.add_argument("--pose-sha")
    parser.add_argument("--published-r02", action="store_true", help="核对已归档的 R02 发布清单及美术 FBX")
    args = parser.parse_args()
    result = verify(args.clip_sha, args.pose_sha, args.published_r02)
    args.receipt.parent.mkdir(parents=True, exist_ok=True)
    with args.receipt.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(result["clip"], result["clipSha256"], "scene", result["scene"])
