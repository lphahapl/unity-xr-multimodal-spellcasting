from pathlib import Path
import sys
# The Blender entry point inspect.py is not Python's stdlib inspect module.
sys.path=[p for p in sys.path if Path(p).resolve()!=Path(__file__).resolve().parent]
from PIL import Image
import numpy as np,json,hashlib
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-polish-20261108/mountain-pine';TEX=ROOT/'Assets/Art/Environment/Opening/MountainPine/Textures'
report={}
for role in ('BaseColor','Normal','Mask'):
 im=Image.open(TEX/('MountainPine_'+role+'.png'));a=np.asarray(im).reshape(-1,4)
 assert im.size==(1024,1024)
 report[role]={'size':im.size,'std':a.std(axis=0).tolist(),'min':a.min(axis=0).tolist(),'max':a.max(axis=0).tolist()}
(LOG/'texture-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8')
p=LOG/'report.json';r=json.loads(p.read_text(encoding='utf8'))
r['source_identity']={'confirmed_visual':'Bent-trunk pine with tiered canopy, not rock','expected_sha256':'cf0dbaf62daba266d09fb846135b990b1f9668c9249f733b040bf8a5804079a5','source_bytes':58983916}
r['canopy_detail']={'method':'Green-source-color mask; elongated Voronoi needle-like relief in two spatial directions, original source normal retained; cluster color variation and matte roughness. High shader baked onto new low UV.','runtime_cost':'One URP Lit slot, existing three 1024 textures; no procedural runtime nodes, no alpha cards, no added needle geometry.','visual_limit':'Original solid flattened canopy lobes remain. Baked fine surface detail does not reconstruct individual pine needles or change source silhouette.'}
r['previews']={'raw_source':'high-front.png, high-back.png','enhanced_high_low_same_camera':'comparison-high.png, comparison-low.png','low_other_views':'low-back.png, low-left.png, low-top.png, wireframe.png'}
p.write_text(json.dumps(r,indent=2),encoding='utf8')
manifest=[]
for folder in ('Assets/Art/Environment/Opening/MountainPine','ArtSource/Environment/Opening/MountainPine','Tools/Environment/MountainPine'):
 for f in sorted((ROOT/folder).rglob('*')):
  if f.is_file():manifest.append({'path':str(f),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
(LOG/'delivery-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
print(json.dumps({'texture_audit':'passed','files':len(manifest),'source_confirmed':r['source_sha256']==r['source_identity']['expected_sha256']}))
