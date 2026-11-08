"""Package the finished deliverables, excluding local editor caches."""
from pathlib import Path
import zipfile, hashlib, json

workspace=Path(__file__).resolve().parent.parent
root=workspace/'棋盘模型-v1'
excluded={'Library','Temp','Logs','UserSettings','obj','.vs'}
files=[p for p in root.rglob('*') if p.is_file() and not any(s in excluded for s in p.relative_to(root).parts) and p.suffix not in ('.blend1','.log','.tmp')]
assert (root/'ModularBoard_v1.unitypackage').is_file()
assert (root/'unity-validation.txt').is_file()
assert len(list((root/'FBX').glob('*.fbx')))==10
assert len(list((root/'GLB').glob('*.glb')))==10
assert len(list((root/'Unity/Assets/ModularBoard/Prefabs').glob('*.prefab')))==10
manifest={str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.name!='sha256-manifest.json'}
(root/'sha256-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
files=[p for p in files if p.name!='sha256-manifest.json']+[root/'sha256-manifest.json']
archive=workspace/'棋盘模型-v1-完整交付.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files: z.write(p, str(Path(root.name)/p.relative_to(root)))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
print(json.dumps({'archive':str(archive),'files':len(files),'bytes':archive.stat().st_size},ensure_ascii=False))
