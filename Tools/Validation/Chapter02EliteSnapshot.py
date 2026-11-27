"""Copy the official chapter scene and its serialized asset dependencies to the isolated Unity project."""
from pathlib import Path
from hashlib import sha256
import json
import re
import shutil
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
copy_root, report = (Path(x).resolve() for x in sys.argv[1:])
if root == copy_root or root in copy_root.parents or not (copy_root / "ProjectSettings/ProjectVersion.txt").is_file():
    raise ValueError("Expected a separate Unity project")
scene = Path("Assets/Scene/Chapter02_SuspendedPath.unity")
guid = re.compile(rb"guid: ([a-fA-F0-9]{32})")
ids = {}
for filename in subprocess.check_output(["rg", "--files", "Assets", "-g", "*.meta"], cwd=root).decode("utf-8").splitlines():
    meta = Path(filename)
    with (root / meta).open("rb") as stream:
        match = guid.search(stream.read(192))
    if match:
        ids[match[1]] = Path(str(meta)[:-5])
todo, known, external = [scene], {}, set()
while todo:
    asset = todo.pop()
    if asset in known or not (root / asset).is_file():
        continue
    body = (root / asset).read_bytes()
    meta = Path(str(asset) + ".meta")
    links = set(guid.findall((root / meta).read_bytes()))
    if body.startswith(b"%YAML") or asset.suffix in {".shadergraph", ".asmref", ".shader", ".vfx"}:
        links.update(guid.findall(body))
    for key in links:
        if key in ids:
            todo.append(ids[key])
        else:
            external.add(key.decode())
    known[asset] = sha256(body).hexdigest()
    for item in [asset, meta] + [Path(str(p) + ".meta") for p in asset.parents]:
        if not (root / item).is_file():
            continue
        src, dst = root / item, copy_root / item
        dst.parent.mkdir(parents=True, exist_ok=True)
        if not dst.exists() or src.read_bytes() != dst.read_bytes():
            if item.suffix == ".cs":
                raise ValueError("Scene depends on an unsynchronised script: " + str(item))
            shutil.copy2(src, dst)
    if sha256((copy_root / asset).read_bytes()).hexdigest() != known[asset]:
        raise ValueError(str(asset))
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps(dict(scene=str(scene), sceneSHA256=known[scene], count=len(known),
    assets={k.as_posix(): v for k, v in sorted(known.items())}, nonProjectGUIDs=sorted(external)),
    ensure_ascii=False, indent=2), encoding="utf-8")
print(len(known), known[scene], [e for e in external if not e.startswith("0000000000000000")])
