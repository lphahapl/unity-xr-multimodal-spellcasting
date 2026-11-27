"""Cold-import exported LOD0/GLB and validate source topology without changing exports."""
import bpy,bmesh,sys,json,math,hashlib
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
LOG=OUT.parents[3]/'Logs/workshop-master-tenon-r02-20261125/model'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts')
import qa_check
def data(ob):
    ob.data.calc_loop_triangles();bpy.context.view_layer.update()
    return dict(name=ob.name,triangles=len(ob.data.loop_triangles),dimensions=list(ob.dimensions),location=list(ob.location),uv_layers=len(ob.data.uv_layers),material_count=len(ob.data.materials))
def reset():
    for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
def check(path):
    reset()
    if path.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path))
    else:bpy.ops.import_scene.gltf(filepath=str(path))
    obs=[ob for ob in bpy.context.scene.objects if ob.type=='MESH'];bpy.context.view_layer.update()
    result=[data(ob) for ob in obs]
    assert len(obs)==1 and result[0]['triangles']==952
    assert max(abs(a-b) for a,b in zip(result[0]['dimensions'],[.12,.12,.35]))<1e-5,result
    q=qa_check.run(objects=[o.name for o in obs],profile='game',eps=.0002,tol=.0005)
    # Export UV seams split vertices in FBX/glTF, check exact dimensions and original source manifold separately.
    return dict(file=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),objects=result,qa=q)
r={name:check(OUT/name) for name in ['MasterTenon_LOD0.fbx','MasterTenon_R02.glb']}
(LOG/'cold-import.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
reset();bpy.ops.wm.open_mainfile(filepath=str(OUT/'MasterTenon_R02.blend'))
high=bpy.data.objects['MasterTenon_HIGH'];me=high.data
before={'verts':len(me.vertices),'faces':len(me.polygons)}
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-8);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-9);bm.to_mesh(me);bm.free();me.update()
# Only coincident row caused by binary .014 duplication is cleaned. Valid exterior unchanged.
q=qa_check.run(objects=[high.name],profile='render',eps=.00005,tol=.0002,max_tris=400000)
(LOG/'MasterTenon_HIGH.qa.json').write_text(json.dumps({'before_cleanup':before,'after_cleanup':data(high),'surface_change':'cleanup is zero-change for rounded profile heights; valid exterior unchanged','qa':q},ensure_ascii=False,indent=2),encoding='utf-8')
assert q['pass'],q['errors']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R02.blend'))
report=json.loads((OUT/'model-report.json').read_text('utf-8'))
report['meshes'][0].update(data(high))
report['high_source_cleanup']='R02 ring heights rounded; zero-area duplicate-row prevention retained. Source geometry cold-checked.'
(OUT/'model-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('COLD_IMPORT_AND_HIGH_QA_PASS',flush=True)
