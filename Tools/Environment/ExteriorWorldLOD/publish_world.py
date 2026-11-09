"""Parent-only publication after actual Unity and visual acceptance; never saves the open Editor scene."""
from pathlib import Path
import hashlib,json,shutil,datetime,os

R=Path('D:/UnityProjects/NarutoVR');C=Path('D:/Temp/NarutoVR-Exterior-20261108')
L=R/'Logs/exterior-world-lod-20261109';I=L/'integration';OUT=L/'publication'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
baseline=read(L/'initial-state.json');proof=read(I/'merge-proof.json')
review=read(I/'world-parent-review.json');visual=read(I/'visual-acceptance.json')
runtime=read(L/'runtime-final/runtime-acceptance.json');android=read(L/'shader-android/shader-android-result.json')
assert review['passed'] and visual['passed'] and runtime['passed'] and android['passed']
scene='Assets/Scene/Chapter01_Awakening.unity'
prefab='Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab'
candidate=C/'Assets/Scene/QA_WorldExteriorMerged.unity'
for rel,expected in baseline.items():assert sha(R/rel)==expected,('Concurrent main edit',rel)
assert proof['baseSha256']==baseline[scene]==review['baseSha256']
assert sha(candidate)==proof['candidateSha256']==review['candidateSha256']
assert sha(C/prefab)==proof['prefabSHA256']==review['prefabSHA256']==visual['prefabSHA256']
assert visual['candidateSha256']==proof['candidateSha256']
for rel,expected in visual['assetHashes'].items():
    assert sha(C/rel)==expected,('Accepted art changed',rel)
for rel in ['Assets/scripts/logic/Environment/ArmillaryRingMotion.cs','Assets/scripts/logic/Environment/SceneTravelGate.cs','Assets/Shaders/ExteriorTerrain.shader']:
    assert sha(R/rel)==sha(C/rel),('Main/accepted runtime differs',rel)
    assert visual['sourceHashes'][rel]==sha(C/rel),('Acceptance source differs',rel)
roots=['Assets/Art/Environment/Opening/'+name for name in ['ArchitectureLOD','NatureLOD','ExteriorLOD','SurfaceMaterialsLOD','ConnectivityLOD']]
files=[]
for rel in roots:
    root=C/rel;assert root.is_dir()
    files.extend(p for p in root.rglob('*') if p.is_file())
    # Folder GUIDs have no art references; preserve an existing main folder identity.
    folder_meta=C/(rel+'.meta')
    if folder_meta.exists() and not (R/(rel+'.meta')).exists():files.append(folder_meta)
files.extend([C/prefab,C/(prefab+'.meta')])
OUT.mkdir(parents=True,exist_ok=True)
record={'time':datetime.datetime.now().astimezone().isoformat(),'baseSceneSHA256':baseline[scene],
        'candidateSceneSHA256':proof['candidateSha256'],'acceptedPrefabSHA256':proof['prefabSHA256'],
        'method':'Task asset directories plus existing owned prefab; exact explicit fields only in main scene. No Editor SaveScene. No deletions.',
        'files':[]}
for src in sorted(set(files)):
    rel=src.relative_to(C);dst=R/rel
    assert str(dst.resolve()).lower().startswith(str((R/'Assets').resolve()).lower()+os.sep)
    previous=sha(dst) if dst.exists() else None
    if previous!=sha(src):
        if dst.exists():
            backup=OUT/'before'/rel;backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dst,backup)
        dst.parent.mkdir(parents=True,exist_ok=True)
        staging=dst.with_name(dst.name+'.exterior-staging');shutil.copy2(src,staging);os.replace(staging,dst)
    assert sha(dst)==sha(src)
    record['files'].append({'path':rel.as_posix(),'before':previous,'after':sha(dst),'bytes':dst.stat().st_size})
# Guard again immediately before the only scene mutation.
assert sha(R/scene)==baseline[scene]
backup=OUT/'before'/scene;backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(R/scene,backup)
staging=(R/scene).with_name('Chapter01_Awakening.unity.exterior-staging')
shutil.copy2(candidate,staging);os.replace(staging,R/scene)
assert sha(R/scene)==proof['candidateSha256'] and sha(R/prefab)==proof['prefabSHA256']
record['files'].append({'path':scene,'before':baseline[scene],'after':sha(R/scene),'bytes':(R/scene).stat().st_size})
for folder in ['architecture','nature','connectivity']:
    src=C/'Logs/exterior-world-lod-20261109'/folder
    if src.exists():
        for p in src.rglob('*'):
            if p.is_file():
                dst=L/folder/p.relative_to(src);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
record['allPublishedBytesMatchAcceptedClone']=all(sha(R/f['path'])==f['after'] for f in record['files'])
record['openUserSceneSaved']=False
(OUT/'publication.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'publishedFiles':len(record['files']),'sceneSHA256':sha(R/scene),'prefabSHA256':sha(R/prefab),'allHashesMatch':record['allPublishedBytesMatchAcceptedClone']}))
