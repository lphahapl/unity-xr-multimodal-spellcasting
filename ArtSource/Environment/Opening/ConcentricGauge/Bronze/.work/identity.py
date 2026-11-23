from io_scene_fbx.parse_fbx import parse
from pathlib import Path
import json,sys
root=Path(r"D:/UnityProjects/NarutoVR/Assets/Art/Environment/Opening/ConcentricGauge/Bronze")
d={}
for f in root.glob("*.fbx"):
    data,_=parse(str(f))
    objs=next(x for x in data.elems if x.id==b"Objects")
    d[f.stem]=[(node.id.decode(),node.props[1].decode().split(chr(0))[0]) for node in objs.elems if node.id in {b"Geometry",b"Model",b"Material"}]
Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work",sys.argv[-1]+".json").write_text(json.dumps(d,sort_keys=True,indent=2))

