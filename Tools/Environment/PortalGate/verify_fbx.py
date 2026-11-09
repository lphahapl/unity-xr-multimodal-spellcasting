from pathlib import Path
import bpy,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Art/Environment/Opening/PortalGate/PortalGate.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
report={'objects':[]}
for ob in bpy.context.scene.objects:
 r={'name':ob.name,'type':ob.type,'parent':ob.parent.name if ob.parent else None,'world_location':list(ob.matrix_world.translation)}
 if ob.type=='MESH':
  ob.data.calc_loop_triangles();p=[ob.matrix_world@v.co for v in ob.data.vertices]
  r.update(triangles=len(ob.data.loop_triangles),materials=[m.name for m in ob.data.materials],uv_layers=[x.name for x in ob.data.uv_layers],bounds=[[min(v[i] for v in p) for i in range(3)],[max(v[i] for v in p) for i in range(3)]])
 report['objects'].append(r)
assert len(meshes)==1
assert len(meshes[0].data.loop_triangles)<=5000
assert {o.name for o in bpy.context.scene.objects}>={'PortalArrival','PortalTrigger'}
report['passed']=True
(ROOT/'Logs/exterior-polish-20261108/portal/fbx-readback.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report,indent=2))
