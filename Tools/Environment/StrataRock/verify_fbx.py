from pathlib import Path
import bpy,json,hashlib
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-polish-20261108/strata-rock'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Art/Environment/Opening/StrataRock/StrataRock.fbx'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
ob=meshes[0];ob.data.calc_loop_triangles();p=[ob.matrix_world@v.co for v in ob.data.vertices]
r={'objects':len(meshes),'triangles':len(ob.data.loop_triangles),'materials':[m.name for m in ob.data.materials],'uv_layers':[x.name for x in ob.data.uv_layers],'bounds_blender_xyz':[[min(v[i] for v in p) for i in range(3)],[max(v[i] for v in p) for i in range(3)]],'normals_count':len(ob.data.corner_normals),'source':'Fresh process FBX reimport; Unity verification deferred to parent'}
assert 2500<=r['triangles']<=3500
assert len(r['materials'])==1 and r['uv_layers']==['StrataRockAtlasUV']
assert abs(r['bounds_blender_xyz'][1][0]-r['bounds_blender_xyz'][0][0]-3)<.0001
assert abs(r['bounds_blender_xyz'][0][2])<.0001
r['passed']=True;(LOG/'fbx-readback.json').write_text(json.dumps(r,indent=2),encoding='utf8');print(json.dumps(r,indent=2))
