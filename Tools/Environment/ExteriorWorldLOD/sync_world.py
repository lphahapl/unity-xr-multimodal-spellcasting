from pathlib import Path
import shutil,uuid
R=Path('D:/UnityProjects/NarutoVR');C=Path('D:/Temp/NarutoVR-Exterior-20261108')
assert (C/'exterior-task-owner.txt').exists()
for folder in ['Assets/scripts/logic/Environment','Assets/Art/Environment/Opening/ArchitectureLOD','Assets/Art/Environment/Opening/NatureLOD','Assets/Art/Environment/Opening/ExteriorLOD','Assets/Art/Environment/Opening/SurfaceMaterialsLOD']:
    src=R/folder
    if not src.exists():continue
    for p in src.rglob('*'):
        if not p.is_file():continue
        if p.suffix=='.cs' and not p.with_name(p.name+'.meta').exists():
            p.with_name(p.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
    for p in src.rglob('*'):
        if p.is_file():
            dst=C/p.relative_to(R);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dst)
for p in (R/'Tools/Environment/ExteriorWorldLOD').glob('*.cs'):
    if p.name=='QuestExteriorProbe.cs':continue # Optional device fixture belongs in clone runtime, never Editor/main Assets.
    shutil.copy2(p,C/'Assets/Editor'/p.name)
shader=R/'Assets/Shaders/ExteriorTerrain.shader'
if not shader.with_name(shader.name+'.meta').exists():
    shader.with_name(shader.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
for p in [shader,shader.with_name(shader.name+'.meta')]:
    shutil.copy2(p,C/p.relative_to(R))
# Reset only the prior owned prefab in the disposable clone for repeatable authoring.
for suffix in ['', '.meta']:
    rel='Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab'+suffix
    shutil.copy2(R/rel,C/rel)
print('Synced task-scoped inputs; existing main scene and prefab untouched.')
