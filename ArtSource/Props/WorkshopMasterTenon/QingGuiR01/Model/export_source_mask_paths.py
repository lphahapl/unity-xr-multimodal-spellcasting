"""Export physical curve centers/half-widths without constructing any mesh."""
import sys,json
from pathlib import Path
OUT=Path(__file__).resolve().parent;sys.path.insert(0,str(OUT))
from qinggui_detail import trees
paths=[[([float(x),float(z)],float(w)) for (x,z),w in pts] for pts in trees(True)]
(OUT/'source-mask-paths.json').write_text(json.dumps(paths),encoding='utf-8')
print('SOURCE_MASK_PATHS_EXPORTED',[len(p) for p in paths])
