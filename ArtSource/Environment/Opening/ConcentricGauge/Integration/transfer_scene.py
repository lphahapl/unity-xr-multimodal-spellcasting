"""Apply the isolated Unity serialization of the eight instances to a current scene."""
import argparse
import hashlib
import os
from pathlib import Path
import re
import tempfile

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n", re.M)
ART = Path("Assets/Art/Environment/Opening/ConcentricGauge")
NAMES = ("Bench", "Stele", "Plinth", "Bed", "Outer", "Inner", "Box", "Lid")
SCENES = ("Chapter01_Courtyard", "Chapter01_Awakening")

def blocks(text):
    headers = list(HEADER.finditer(text))
    return {m[2]: (m[1], text[m.start():headers[i+1].start() if i+1 < len(headers) else len(text)])
            for i, m in enumerate(headers)}

def one(pattern, text):
    found = re.findall(pattern, text, re.M)
    if len(found) != 1:
        raise ValueError(f"Expected one match for {pattern}, got {len(found)}")
    return found[0]

def transfer(copy, target, evidence, name):
    dest = target / "Assets/Scene" / (name + ".unity")
    raw = dest.read_bytes()
    newline = "\r\n" if b"\r\n" in raw else "\n"
    text = raw.decode("utf-8").replace("\r\n", "\n")
    source = blocks(text)
    staged = blocks((copy / "Assets/Scene" / ("A1Staged_"+name+".unity")).read_text("utf-8"))
    expected = blocks((copy / "Baseline" / (name+".unity")).read_text("utf-8"))
    guids = {one(r"^guid: (\w+)$", (copy/ART/"Prefabs"/f"A1_{n}.prefab.meta").read_text()): n for n in NAMES}
    edits = {}
    additions = {}
    attach = {}
    for key,(typ, block) in staged.items():
        if typ != "1001":
            continue
        guid = one(r"^  m_SourcePrefab: \{fileID: \d+, guid: (\w+), type: 3\}", block)
        if guid not in guids:
            continue
        parent = one(r"m_TransformParent: \{fileID: (\d+)\}",block)
        child = [i for i,(kind,b) in staged.items() if kind == "4" and
                 f"\n  m_PrefabInstance: {{fileID: {key}}}\n" in b]
        if len(child)!=1 or key in source or child[0] in source:
            raise ValueError("Instance ID collision or missing transform: "+guids[guid])
        additions[key] = block
        additions[child[0]] = staged[child[0]][1]
        attach.setdefault(parent,[]).append(child[0])
    if len(additions) != 2*len(NAMES):
        raise ValueError("Staging did not produce eight new instances")
    # The dedicated parent can acquire other children between snapshot and publication.
    for parent, children in attach.items():
        original = source[parent][1]
        child_list = r"^  m_Children:(?: \[\]|\n(?:  - \{fileID: -?\d+\}\n?)+)"
        if re.sub(child_list,"",original,flags=re.M) != re.sub(child_list,"",expected[parent][1],flags=re.M):
            raise ValueError("Anchor transform changed since freeze: "+parent)
        old = one(child_list,original).rstrip("\n")
        new = old.replace(" []","") + "".join("\n  - {fileID: "+c+"}" for c in children)
        edits[parent]=original.replace(old,new,1)
    for row in (evidence/(name+"-renderer-edits.tsv")).read_text("utf-8-sig").splitlines():
        key, before, after, path = row.split("\t")
        original = source[key][1]
        flag = "\n  m_Enabled: "+before+"\n"
        if source[key][0] != "23" or flag not in original or expected[key][1] != original:
            raise ValueError("Changed renderer: "+path)
        edits[key]=original.replace(flag,"\n  m_Enabled: "+after+"\n",1)
    for key, replacement in edits.items():
        text=text.replace(source[key][1],replacement,1)
    roots = one(r"(^--- !u!1660057539 &\d+\n)", text)
    text=text.replace(roots,"".join(additions.values())+roots,1)
    patched=text.replace("\n",newline).encode("utf-8")
    fd, scratch=tempfile.mkstemp(dir=dest.parent,suffix=".tmp")
    try:
        with os.fdopen(fd,"wb") as f:
            f.write(patched)
        if dest.read_bytes()!=raw:
            raise RuntimeError("Scene changed during transfer: "+str(dest))
        os.replace(scratch,dest)
    finally:
        Path(scratch).unlink(missing_ok=True)
    print(name,"changed blocks",len(edits),"added blocks",len(additions),
          "sha256",hashlib.sha256(patched).hexdigest())

if __name__=="__main__":
    parser=argparse.ArgumentParser()
    for opt in ("copy","target","evidence"):
        parser.add_argument("--"+opt,type=Path,required=True)
    args=parser.parse_args()
    for scene in SCENES:
        transfer(args.copy,args.target,args.evidence,scene)

