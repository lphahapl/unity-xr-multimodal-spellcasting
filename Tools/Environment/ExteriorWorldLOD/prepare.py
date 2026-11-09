"""Task-owned isolated editor preparation; never deletes user project data."""
from pathlib import Path
import shutil, hashlib, json
ROOT=Path('D:/UnityProjects/NarutoVR')
CLONE=Path('D:/Temp/NarutoVR-Exterior-20261108')
LOG=ROOT/'Logs/exterior-world-lod-20261109'
LOG.mkdir(parents=True,exist_ok=True)
assert (CLONE/'exterior-task-owner.txt').exists()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
paths=['Assets/Scene/Chapter01_Awakening.unity','Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab']
snapshot={p:sha(ROOT/p) for p in paths}
if not (LOG/'initial-state.json').exists():
    (LOG/'initial-state.json').write_text(json.dumps(snapshot,indent=2),encoding='utf-8')
    for p in paths:
        out=LOG/'backup'/p;out.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/p,out)
for folder in ['Assets/Scripts','Assets/Art/Environment/Opening','Assets/Materials/Chapter01','Assets/Shaders','Packages','ProjectSettings']:
    source=ROOT/folder; target=CLONE/folder
    # Remove obsolete scripts from this task's disposable clone only, retaining backup.
    if folder=='Assets/Scripts':
        for p in list(target.rglob('*')):
            if p.is_file() and not (source/p.relative_to(target)).exists():
                dest=LOG/'clone-obsolete'/p.relative_to(CLONE);dest.parent.mkdir(parents=True,exist_ok=True)
                shutil.move(str(p),str(dest))
    for p in source.rglob('*'):
        if not p.is_file():continue
        q=target/p.relative_to(source)
        if not q.exists() or sha(p)!=sha(q):
            q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
for p in ['Assets/Scene/Chapter01_Awakening.unity','Assets/Scene/Chapter01_Awakening.unity.meta']:
    shutil.copy2(ROOT/p,CLONE/p)
print('Prepared isolated clone; baseline:',snapshot)
