"""Capture the current terrain-fit geometry and calibrated doorway marks once, before rebuilding.
Blender rebuilds only need fitted_supports.json.gz. The legacy kit prefix is not part of it.
"""
import gzip, json, re
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent
ART = HERE.parents[4] / 'Assets/Art/Environment/Opening/ArchitectureLOD'
out = HERE / 'fitted_supports.json.gz'
if out.exists():
    raise SystemExit('Retaining the captured source')
result = {}
for asset in sorted((ART / 'Meshes').glob('*.asset')):
    kit = asset.stem.replace('_Grounded', '').replace('_Support', '')
    old = json.loads((ART / (kit + '.json')).read_text())
    first = len(old['vertices']) // 3
    raw = asset.read_text()
    data = np.frombuffer(bytes.fromhex(re.search('_typelessdata: (.+)', raw)[1]), '<f4').reshape(-1,12)
    assert np.allclose(data[:first,:3].ravel(), old['vertices'], atol=1e-5)
    indices = np.frombuffer(bytes.fromhex(re.search('m_IndexBuffer: (.+)',raw)[1]),'<u2').reshape(-1,3)
    tail = indices[indices.min(axis=1)>=first].astype(int) - first
    data = data[first:]
    result[asset.stem] = dict(pos=data[:,:3].tolist(), reg=(np.floor(data[:,10:12]*[3,2]).astype(int) @ [1,3]).tolist(),tri=tail.tolist())
with gzip.open(out,'wt',encoding='utf8') as f:
    json.dump(result,f,separators=(',',':'))
