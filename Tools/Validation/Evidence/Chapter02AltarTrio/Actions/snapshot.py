from pathlib import Path
import hashlib, json
repo=Path(r'D:/UnityProjects/NarutoVR')
lab=Path(r'D:/Temp/NarutoVR-A4ProductionArt-20261125')
ev=repo/'Tools/Validation/Evidence/Chapter02AltarTrio/Actions'
globsets=[
    'Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller*',
    'Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx*',
    'Assets/Art/Enemies/CanJiOu/BallastStone.prefab*',
    'Assets/Art/Enemies/CanJiOu/BallastStoneA4/T25-A4-Unity.fbx*',
    'Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab*',
    'Assets/Art/Enemies/ZhiHuoCanJi/*.prefab*',
    'Assets/Art/Enemies/ZhiHuoCanJi/*/*.controller*',
    'Assets/ScriptableObjects/敌人目录配置/*.asset',
    'Assets/ScriptableObjects/敌人定义配置/*.asset',
    'Assets/ScriptableObjects/敌人战斗配置/*.asset',
    'Assets/Art/Environment/SuspendedPath/Navigation/Chapter02AltarStoneCarrier_NavMesh.asset*',
    'Assets/scripts/logic/Enemies/*.cs',
    'Assets/scripts/logic/Enemies/Spawning/*.cs',
    'Assets/scripts/logic/Rpg/CombatRoomEncounter*.cs',
    'Assets/scripts/logic/Rpg/RpgDamageReceiver.cs']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
out={}
for patt in globsets:
    for source in repo.glob(patt):
        rel=source.relative_to(repo);q=lab/rel
        out[str(rel.as_posix())]={'source':sha(source),'playCopy':sha(q),'same':sha(q)==sha(source)}
data={'assets':out,'copySceneBeforeInstall':next(l.split()[0] for l in (ev/'asset-hashes.txt').read_text(encoding='utf-8').splitlines() if l.endswith('Assets/Scene/Chapter02_SuspendedPath.unity')),'installerAtPlay':sha(lab/'Assets/Editor/Chapter02AltarTrioInstaller.cs'),'installerCurrent':sha(repo/'Tools/Validation/Evidence/Chapter02AltarTrio/SceneHandoff/Chapter02AltarTrioInstaller.cs')}
(ev/'behavior-hashes.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(out),[(k,v) for k,v in out.items() if not v['same']])

