"""Sync only the updated board assets; preserve existing material and asset GUIDs."""
from pathlib import Path
import shutil, hashlib, json, re, os

workspace=Path(__file__).resolve().parent.parent
output=Path(os.environ.get('MODULAR_BOARD_OUT',str(workspace/'棋盘模型-v2-X轴前进')))
source=output/'Unity/Assets/ModularBoard'
target=Path(r'D:\unity_training\Runnner&&Lucy\Assets\ModularBoard')
backup=workspace/'棋盘模型概念图'/('模型零件轴与碰撞体修改前备份' if output.name.startswith('棋盘模型-v3') else '模型X轴修改前备份')
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path): return re.search(r'^guid: (\w+)',path.read_text(encoding='utf-8-sig'),re.M).group(1)

materials={str(p.relative_to(target)):digest(p) for p in (target/'Materials').glob('*') if p.is_file()}
report=[]
for folder,pattern in (('Models','*.fbx'),('Prefabs','*.prefab'),('Editor','*.cs')):
    for path in (source/folder).glob(pattern):
        relative=path.relative_to(source)
        dest=target/relative
        assert dest.exists(),dest
        source_meta=Path(str(path)+'.meta'); target_meta=Path(str(dest)+'.meta')
        assert source_meta.exists() and target_meta.exists()
        assert guid(source_meta)==guid(target_meta),(dest,'asset GUID differs')
        saved=backup/relative
        saved.parent.mkdir(parents=True,exist_ok=True)
        if not saved.exists(): shutil.copy2(dest,saved)
        if not Path(str(saved)+'.meta').exists(): shutil.copy2(target_meta,str(saved)+'.meta')
        before=digest(dest)
        shutil.copy2(path,dest)
        report.append({'file':str(relative),'before':before,'after':digest(dest),'guid':guid(target_meta)})
assert all(digest(target/p)==v for p,v in materials.items()),'Material files changed'
assert len(report)==22,len(report)
(output/'project-sync.json').write_text(json.dumps({'target':str(target),'updated':report,'materials_unchanged':True},ensure_ascii=False,indent=2),encoding='utf-8')
print('Updated',len(report),'model/prefab/editor assets. All asset GUIDs and existing URP materials retained.')
