"""Deterministic project-relative GUIDs for this isolated Unity art package."""
from pathlib import Path
from uuid import UUID, uuid5
import re

SRC=Path(__file__).resolve().parent
ROOT=SRC.parents[3]
ART=ROOT/'Assets/Art/Environment/Opening/PreqinGateFittings'
PHOTO=ART.parent/'PhotoArchitecture'
NAMESPACE=UUID('1a6fe7e8-76cc-45b0-a915-30bda8c8f4ed')

def guid(path):
    return re.search(r'^guid: ([0-9a-f]{32})$',Path(str(path)+'.meta').read_text(),re.M)[1]

def meta(path,template):
    file=Path(str(path)+'.meta')
    identifier=uuid5(NAMESPACE,path.relative_to(ROOT).as_posix()).hex
    if file.exists():assert guid(path)==identifier
    text=Path(str(template)+'.meta').read_text()
    file.write_text(re.sub(r'(?m)^guid: [0-9a-f]+$', 'guid: '+identifier,text),encoding='utf8')
    return identifier
