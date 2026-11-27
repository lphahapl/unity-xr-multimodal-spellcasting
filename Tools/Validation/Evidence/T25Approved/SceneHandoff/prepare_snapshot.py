"""将正式资源复制到专用测试副本；输出生产及副本散列。须在该副本 Unity 退出后执行。"""
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys

here = Path(__file__).resolve().parent
root = here.parents[4]
replica = Path(sys.argv[1]).resolve()
report = Path(sys.argv[2]).resolve()
if replica.name != "NarutoVR-CarrierQA-20261125" or replica == root or not (replica/"ProjectSettings/ProjectVersion.txt").is_file():
    raise ValueError("须指定工奴独立验证副本")
report.mkdir(parents=True, exist_ok=True)
def digest(file):
    return sha256(file.read_bytes()).hexdigest()
def copy(src):
    dest = replica/src.relative_to(root)
    dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.is_file() or digest(dest)!=digest(src):
        shutil.copy2(src, dest)

scene = Path("Assets/Scene/Chapter02_SuspendedPath.unity")
original = digest(root/scene)
for folder in ("Assets/scripts","Assets/Art/Enemies/CanJiOu/BallastStoneA4","Assets/AddressableAssetsData"):
    for f in (root/folder).rglob("*"):
        if f.is_file() and (folder!="Assets/scripts" or f.suffix in (".cs",".meta",".asmdef")):
            copy(f)
files={scene}
for rel in ("ProjectSettings/NavMeshAreas.asset","ProjectSettings/TagManager.asset","ProjectSettings/DynamicsManager.asset",
    "Assets/Art/Enemies/CanJiOu/BallastStoneA4.meta","Assets/Art/Enemies/CanJiOu/BallastStone.prefab","Assets/Art/Enemies/CanJiOu/BallastStone.prefab.meta",
    "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx","Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx.meta",
    "Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller","Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset",
    "Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset","Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset.meta",
    "Assets/scripts/logic/Enemies/EnemyController.cs","Assets/scripts/logic/Enemies/StoneCarrier.cs",
    "Assets/scripts/data/Enemies/Spawning/StoneCarryPose.cs","Assets/scripts/data/Enemies/Spawning/StoneCarrierProfile.cs",
    "Assets/scripts/logic/Enemies/BallastBasket.cs","Assets/scripts/logic/Enemies/BallastStone.cs","Assets/Art/Glyphs/QianjunTether.asset",
    "Assets/scripts/logic/Enemies/StoneCarrierPost.cs","Assets/scripts/data/Enemies/Spawning/StoneCarryPose.cs.meta",
    "Assets/scripts/logic/Rpg/Glyphs/QianjunEffectDefinition.cs","Assets/scripts/logic/Rpg/CombatRoomEncounter.cs",
    "Assets/scripts/logic/Rpg/ObjectiveChapterFlow.cs","Assets/scripts/logic/Rpg/WorkshopTenon/WorkshopTenonObjective.cs"):
    copy(root/rel); files.add(Path(rel))
files.update(f.relative_to(root) for f in (root/"Assets/Art/Enemies/CanJiOu/BallastStoneA4").rglob("*") if f.is_file())
for name in ("T25AltarRoomInstaller.cs","T25ApprovedTransit.cs"):
    shutil.copy2(here/name,replica/"Assets/Editor"/name)
subprocess.run([sys.executable,str(root/"Tools/Validation/Chapter02CarrierMirror.py"),str(replica),str(report/"assets")],cwd=root,check=True)
records=[dict(path=f.as_posix(),production=digest(root/f),replica=digest(replica/f)) for f in sorted(files)]
if original!=digest(root/scene) or any(r["production"]!=r["replica"] for r in records):
    raise RuntimeError("复制期间输入改变，需重新建立快照")
(report/"input-hashes.json").write_text(json.dumps(records,indent=2,ensure_ascii=False),encoding="utf8")
print(len(records),"verified inputs",original)
