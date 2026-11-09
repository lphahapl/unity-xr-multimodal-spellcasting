"""Diagnostic software mesh renders; not a substitute for Unity / headset acceptance."""
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
ROOT=Path(__file__).resolve().parents[3]
art=ROOT/'Assets/Art/Environment/Opening/MountainPolish'
log=ROOT/'Logs/exterior-polish-20261108/mountains'
sheet=Image.new('RGB',(1500,1080),(171,191,196));draw=ImageDraw.Draw(sheet)
for slot,path in enumerate(sorted(art.glob('*.obj'))):
    verts=[];faces=[]
    for line in path.read_text().splitlines():
        if line.startswith('v '):verts.append([float(n) for n in line.split()[1:]])
        if line.startswith('f '):faces.append([int(n.split('/')[0])-1 for n in line.split()[1:]])
    p=np.array(verts);f=np.array(faces)
    right=np.array([.86,0,.50]);up=np.array([-.20,.91,.35]);view=np.cross(right,up)
    projected=np.stack([p@right,-p@up],1)
    projected-=projected.min(0);scale=min(680/(projected[:,0].max()+.01),275/(projected[:,1].max()+.01));projected*=scale
    ox=30+(slot%2)*750;oy=50+(slot//2)*350
    projected+=np.array([ox,oy+35])
    light=np.array([-.35,.85,-.45]);light/=np.linalg.norm(light)
    normals=np.cross(p[f[:,1]]-p[f[:,0]],p[f[:,2]]-p[f[:,0]])
    normals/=np.linalg.norm(normals,axis=1,keepdims=True)
    order=np.argsort((p[f].mean(1))@view)
    for k in order:
        lit=.43+max(0,float(normals[k]@light))*.57
        # Neutral slate material diagnostic, no baked fake illumination in asset.
        rgb=tuple(int(v*lit) for v in (124,137,137))
        draw.polygon([tuple(q) for q in projected[f[k]]],fill=rgb)
    draw.text((ox,oy),path.stem+' / '+str(len(f))+' triangles',fill=(31,42,43))
draw.text((780,790),'Original terrain meshes / orthographic diagnostic',fill=(31,42,43))
draw.text((780,815),'4 ridgelines + 1 bedded bluff; smooth OBJ normals',fill=(31,42,43))
draw.text((780,840),'Mountain prefab: 8 ridge instances / 18,432 triangles / 2 materials',fill=(31,42,43))
draw.text((780,865),'Unity material/lighting review required',fill=(31,42,43))
sheet.save(log/'mesh-contact-sheet.png')
print(log/'mesh-contact-sheet.png')
