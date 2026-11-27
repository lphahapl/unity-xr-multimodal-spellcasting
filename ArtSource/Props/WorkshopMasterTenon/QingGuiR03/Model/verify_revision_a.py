"""Cold exports, geometry invariants and optional exact-position GLB adjacency audit."""
import bpy,bmesh,sys,json,hashlib,struct,types
from pathlib import Path
SOURCE=Path(__file__).resolve().parent;P=SOURCE/'RevisionA';LOG=SOURCE.parents[4]/'Logs/workshop-master-tenon-qinggui-r03-20261126/model/revision-a'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
probe=types.ModuleType('qa_probe');probe.__file__=qa_check.__file__
code=Path(qa_check.__file__).read_text('utf-8').replace('    zf = _describe(zf_pairs)',"    globals()['_pairs']=[{'triangles':[i,j],'shared_positions':sum(any((verts[a]-verts[b]).length < 1e-9 for b in tris[j]) for a in tris[i]),'coordinates':[[list(verts[v]) for v in tris[k]] for k in (i,j)]} for i,j in sorted(zf_pairs)]\n    zf = _describe(zf_pairs)")
exec(compile(code,probe.__file__,'exec'),probe.__dict__)
def clear():
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
def signature(o):
    m=o.data;m.calc_loop_triangles()
    packed=[struct.pack('<3f',*v.co) for v in m.vertices]
    return {'vertices':len(m.vertices),'triangles':len(m.loop_triangles),'coordinates_sha256':hashlib.sha256(b''.join(packed)).hexdigest(),'topology_sha256':hashlib.sha256(b''.join(struct.pack('<3I',*t.vertices) for t in m.loop_triangles)).hexdigest(),'vertex_cloud_sha256':hashlib.sha256(b''.join(sorted(packed))).hexdigest(),'triangle_geometry_sha256':hashlib.sha256(b''.join(sorted(b''.join(sorted(packed[i] for i in t.vertices)) for t in m.loop_triangles))).hexdigest(),'corner_normals_sha256':hashlib.sha256(b''.join(struct.pack('<3f',*n.vector) for n in m.corner_normals)).hexdigest()}
clear();bpy.ops.import_scene.fbx(filepath=str(SOURCE/'QingGuiR03_StructureCandidate.fbx'));previous={o.name:signature(o) for o in bpy.context.scene.objects if o.type=='MESH'}
records={}
for file in ['QingGuiR03_StructureCandidate.fbx','QingGuiR03_StructureCandidate.glb']:
    clear()
    if file.endswith('fbx'):bpy.ops.import_scene.fbx(filepath=str(P/file))
    else:bpy.ops.import_scene.gltf(filepath=str(P/file))
    obs=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(obs)==3
    raw=probe.run(objects=[o.name for o in obs],profile='game',eps=1e-5,tol=.0002,max_tris=60000);q=raw;extra={}
    if not q['pass'] and file.endswith('glb'):
        pairs=probe._pairs;assert pairs and all(p['shared_positions']>=1 for p in pairs),pairs;assert all(e.startswith('z-fighting') for e in q['errors']),q['errors'];extra={'raw_export_qa':raw,'seam_pair_evidence':pairs}
        copies=[]
        for o in obs:
            c=o.copy();c.data=o.data.copy();bpy.context.collection.objects.link(c);copies.append(c);bm=bmesh.new();bm.from_mesh(c.data)
            before=sorted(tuple(sorted(tuple(v.co) for v in f.verts)) for f in bm.faces);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-9);after=sorted(tuple(sorted(tuple(v.co) for v in f.verts)) for f in bm.faces);assert before==after;bm.to_mesh(c.data);bm.free()
        q=qa_check.run(objects=[o.name for o in copies],profile='game',eps=1e-5,tol=.0002,max_tris=60000)
        extra['seam_diagnostic']='Exact-coordinate adjacency restored in diagnostic copy only; triangle positions unchanged and runtime eps10um unchanged.'
    assert q['pass'],q['errors']
    items=[]
    for o in obs:
        st=signature(o);r={'name':o.name,'signature':st,'materials':[m.name for m in o.data.materials],'uv_layers':len(o.data.uv_layers),'dimensions_m':list(o.dimensions)}
        if file.endswith('fbx') and 'GoldWire' not in o.name:
            r['vertex_order_equal']=st['coordinates_sha256']==previous[o.name]['coordinates_sha256'];r['coordinates_unchanged_from_first']=st['vertex_cloud_sha256']==previous[o.name]['vertex_cloud_sha256'];r['triangle_geometry_unchanged_from_first']=st['triangle_geometry_sha256']==previous[o.name]['triangle_geometry_sha256'];r['normal_changed']=st['corner_normals_sha256']!=previous[o.name]['corner_normals_sha256'];assert r['coordinates_unchanged_from_first'] and r['triangle_geometry_unchanged_from_first'],r
        items.append(r)
    records[file]={'sha256':hashlib.sha256((P/file).read_bytes()).hexdigest(),'qa':q,'objects':items,**extra};print('VERIFIED',file,q['warnings'],flush=True)
(LOG/'cold-exports.json').write_text(json.dumps(records,indent=2),'utf-8');print('REVISION_A_COLD_PASS')
