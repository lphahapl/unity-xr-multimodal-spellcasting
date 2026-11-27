import bpy,bmesh,json
from pathlib import Path
P=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'));o=bpy.data.objects['QingGuiR02_Body_HIGH'];bm=bmesh.new();bm.from_mesh(o.data)
bad=[]
for e in bm.edges:
    if not e.is_manifold:bad.append({'length':e.calc_length(),'verts':[list(v.co) for v in e.verts],'faces':len(e.link_faces),'areas':[f.calc_area() for f in e.link_faces]})
print('BAD_EDGES',json.dumps(bad),flush=True)
