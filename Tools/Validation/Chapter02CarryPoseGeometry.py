"""读取正式镇石 Prefab 的 Unity YAML，输出实际网格几何和独立的物理碰撞体。"""
import argparse
import hashlib
import json
import re
from pathlib import Path


def read_vector(block, name):
    value = re.search(r"^  " + name + r": \{([^}]+)\}", block, re.M)[1]
    return [float(part.split(":")[1]) for part in value.split(",")]


def source_id(block, name):
    return int(re.search(r"^  " + name + r": \{fileID: ([-\d]+)", block, re.M)[1])


def inspect(path):
    text = path.read_text(encoding="utf-8-sig")
    blocks = re.split(r"(?m)^--- !u!(\d+) &(-?\d+)\r?\n", text)
    names, poses, meshes, boxes = {}, {}, {}, []
    for index in range(1, len(blocks), 3):
        typ, identifier, content = blocks[index : index + 3]
        if typ == "1":
            raw = re.search(r"^  m_Name: (.*)$", content, re.M)[1].strip()
            names[int(identifier)] = json.loads(raw) if raw.startswith('"') else raw
        elif typ == "4":
            poses[source_id(content, "m_GameObject")] = {
                "transformId": int(identifier), "parentTransformId": source_id(content, "m_Father"),
                "position": read_vector(content, "m_LocalPosition"), "rotationXYZW": read_vector(content, "m_LocalRotation"),
                "size": read_vector(content, "m_LocalScale"),
            }
        elif typ == "33":
            line = re.search(r"^  m_Mesh: \{([^}]+)\}",content,re.M)[1]
            if "fileID: 10202," not in line or "0000000000000000e000000000000000" not in line:
                raise ValueError("镇石可视几何已不再是 Unity 内置立方体")
            meshes[source_id(content, "m_GameObject")] = "Unity unit cube"
        elif typ == "65":
            boxes.append({"owner":source_id(content,"m_GameObject"),"center":read_vector(content,"m_Center"),
                          "size":read_vector(content,"m_Size"),"type":"BoxCollider"})
    solids = []
    for owner, mesh in meshes.items():
        pose = poses[owner]
        solids.append({"name":names[owner],"mesh":mesh,**pose})
    for b in boxes:
        b["owner"] = names[b["owner"]]
    return {"prefab":str(path),"sha256":hashlib.sha256(path.read_bytes()).hexdigest(),"units":"Unity local metres",
            "solids":solids,"colliders":boxes}


if __name__ == "__main__":
    args = argparse.ArgumentParser()
    args.add_argument("prefab",type=Path)
    args.add_argument("output",type=Path)
    opts = args.parse_args()
    opts.output.write_text(json.dumps(inspect(opts.prefab),ensure_ascii=False,indent=2),encoding="utf-8")
