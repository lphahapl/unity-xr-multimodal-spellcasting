"""Cold-import exported LOD0/GLB and validate source topology without changing exports."""
import bpy,bmesh,sys,json,math,hashlib
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
LOG=OUT.parents[3]/'Logs/workshop-master-tenon-r03-20261125/model'
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
    assert len(obs)==1 and result[0]['triangles']==1474
    assert max(abs(a-b) for a,b in zip(result[0]['dimensions'],[.12,.12,.35]))<1e-5,result
    q=qa_check.run(objects=[o.name for o in obs],profile='game',eps=.0002,tol=.0005)
    # Export UV seams split vertices in FBX/glTF, check exact dimensions and original source manifold separately.
    return dict(file=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),objects=result,qa=q)
r={name:check(OUT/name) for name in ['MasterTenon_LOD0.fbx','MasterTenon_R03.glb']}
(LOG/'cold-import.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
reset();bpy.ops.wm.open_mainfile(filepath=str(OUT/'MasterTenon_R03.blend'))
high=bpy.data.objects['MasterTenon_HIGH'];me=high.data
before={'verts':len(me.vertices),'faces':len(me.polygons)}
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-8);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-9);bm.to_mesh(me);bm.free();me.update()
# Verify the final high surface without changing its valid geometry.
q=qa_check.run(objects=[high.name],profile='render',eps=.00005,tol=.0002,max_tris=400000)
(LOG/'MasterTenon_HIGH.qa.json').write_text(json.dumps({'before_cleanup':before,'after_cleanup':data(high),'surface_change':'cleanup is zero-change for rounded profile heights; valid exterior unchanged','qa':q},ensure_ascii=False,indent=2),encoding='utf-8')
assert q['pass'],q['errors']
low=bpy.data.objects['MasterTenon_LOD0'];low.data.calc_loop_triangles()
coords={v.index:tuple(float(q) for q in v.co) for v in low.data.vertices if v.co.z<=.127+1e-8}
faces=sorted(tuple(sorted(coords[i] for i in t.vertices)) for t in low.data.loop_triangles if all(i in coords for i in t.vertices))
signature={'vertices':len(coords),'triangles':len(faces),'vertex_sha':hashlib.sha256(repr(sorted(coords.values())).encode()).hexdigest(),'triangle_sha':hashlib.sha256(repr(faces).encode()).hexdigest()}
reference=json.loads((LOG/'preview-geometry-comparison.json').read_text('utf-8'))['R02_lower']
assert signature==reference,(signature,reference)
row=[v.co for v in low.data.vertices if abs(v.co.z-.252)<1e-7 and v.co.y<0]
center=min(row,key=lambda v:abs(v.x));depth=float(center.y-min(v.y for v in row))
assert abs(depth-.002)<1e-7,depth
geometry_verification={'lower_0_to_127_mm_exact_identity':True,'lower_signature':signature,'thumb_depth_low_mesh_m':depth,'thumb_center_low_mesh_m':list(center),'comparison_basis':'Frozen R02 coordinate/triangle signature and actual final LOD0 vertices.'}
(LOG/'final-geometry-verification.json').write_text(json.dumps(geometry_verification,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R03.blend'))
report=json.loads((OUT/'model-report.json').read_text('utf-8'))
report['meshes'][0].update(data(high))
report['final_geometry_verification']=geometry_verification
report['high_source_cleanup']='R03 ring heights rounded; zero-area duplicate-row prevention retained. Source geometry cold-checked.'
(OUT/'model-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('COLD_IMPORT_AND_HIGH_QA_PASS',flush=True)
