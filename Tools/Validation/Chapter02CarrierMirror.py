"""把指定二章场景及运输原型用到的项目 GUID 依赖复制进独立验证工程。"""
from pathlib import Path
from collections import deque
import hashlib
import re
import shutil
import subprocess
import sys

root = Path.cwd().resolve()
target = Path(sys.argv[1]).resolve()
report = Path(sys.argv[2]).resolve() if len(sys.argv)>2 else root / 'Logs/CH02-carrier-integration-20261125'
if target == root or not (target / 'ProjectSettings/ProjectVersion.txt').is_file():
    raise SystemExit('须指定已有的独立 Unity 验证工程')
report.mkdir(parents=True, exist_ok=True)
proc = subprocess.run(['rg', '--no-heading', '--color', 'never', '-g', '*.meta',
                       '^guid: [0-9a-f]{32}', 'Assets'], capture_output=True, check=True)
mapping = {}
for line in proc.stdout.decode('utf-8').splitlines():
    path, guid = line.rsplit(':guid: ', 1)
    mapping[guid.strip()] = Path(path[:-5])

pending = deque(Path(p) for p in [
    'Assets/Scene/Chapter02_SuspendedPath.unity',
    'Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset', 'Assets/Art/Glyphs/QianjunTether.asset',
    'Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab', 'Assets/Art/Enemies/CanJiOu/BallastStone.prefab',
    'Assets/Art/Enemies/CanJiOu/StoneCarrierStation.prefab'])
find_guids = re.compile(rb'\bguid:\s*([\da-f]{32})')
visited, extras, manifest = set(), set(), {}

def copy(relative):
    if relative in manifest:
        return
    original, replica = root / relative, target / relative
    replica.parent.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(original.read_bytes()).digest()
    if not replica.exists() or hashlib.sha256(replica.read_bytes()).digest() != digest:
        shutil.copy2(original, replica)
    if hashlib.sha256(replica.read_bytes()).digest() != digest:
        raise RuntimeError(str(relative))
    manifest[relative] = digest.hex()

while pending:
    path = pending.popleft()
    if path in visited:
        continue
    visited.add(path)
    if not (root / path).is_file():
        continue
    copy(path)
    data = (root / path).read_bytes()
    meta = Path(str(path) + '.meta')
    copy(meta)
    for parent in path.parents:
        folder_meta = Path(str(parent) + '.meta')
        if (root / folder_meta).is_file():
            copy(folder_meta)
    payloads = [(root / meta).read_bytes()]
    if data.startswith(b'%YAML') or path.suffix in {'.shadergraph', '.shadersubgraph'}:
        payloads.append(data)
    for payload in payloads:
        for raw in find_guids.findall(payload):
            guid = raw.decode()
            if guid in mapping:
                pending.append(mapping[guid])
            elif not guid.startswith('0000000000000000'):
                extras.add(guid)

for relative in ['Packages/manifest.json', 'Packages/packages-lock.json']:
    copy(Path(relative))
(report / 'asset-hashes.txt').write_text('\n'.join(f'{h}  {p.as_posix()}' for p, h in sorted(manifest.items()))+'\n', encoding='utf-8')
(report / 'external-guids.txt').write_text('\n'.join(sorted(extras))+'\n', encoding='utf-8')
print('Assets', len(visited), 'identical files', len(manifest), 'external or package GUIDs', len(extras))
