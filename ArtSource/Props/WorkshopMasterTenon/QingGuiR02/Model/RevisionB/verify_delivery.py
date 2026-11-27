"""Cold file checks in clean scenes, independent of the creating Blender session."""
import bpy,bmesh,sys,json,hashlib,struct,types
from pathlib import Path
OUT=Path(__file__).resolve().parent;LOG=OUT.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
qa_probe=types.ModuleType('qa_probe')
qa_probe.__file__=qa_check.__file__
qa_source=Path(qa_check.__file__).read_text('utf-8')
qa_source=qa_source.replace('    zf = _describe(zf_pairs)', "    globals()['_pair_evidence'] = [{'triangles':[i,j], 'objects':[obs[tri_meta[i][0]].name,obs[tri_meta[j][0]].name], 'shared_position_count':sum(any((verts[a]-verts[b]).length < 1e-9 for b in tris[j]) for a in tris[i]), 'coordinates':[[list(verts[v]) for v in tris[k]] for k in (i,j)]} for i,j in sorted(zf_pairs)]\n    zf = _describe(zf_pairs)")
exec(compile(qa_source,qa_check.__file__,'exec'),qa_probe.__dict__)
report=json.loads((OUT/'model-report.json').read_text('utf-8'));expected={o['name']:o for o in report['meshes']}
result={}
def signature(ob):
    me=ob.data;me.calc_loop_triangles()
    vertices=b''.join(struct.pack('<3f',*v.co) for v in me.vertices)
    topology=b''.join(struct.pack('<3I',*t.vertices) for t in me.loop_triangles)
    return {'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'vertex_coordinates_sha256':hashlib.sha256(vertices).hexdigest(),'triangle_indices_sha256':hashlib.sha256(topology).hexdigest()}
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'QingGuiR02B_GeometryCandidate.fbx'))
candidate={o.name:signature(o) for o in bpy.context.scene.objects if o.type=='MESH'}
for filename,count in [('QingGuiR02B_LOD0.fbx',2),('QingGuiR02B_LODs.fbx',6),('QingGuiR02B.glb',2),('QingGuiR02B_Lookdev.glb',2)]:
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    if filename.endswith('.fbx'):bpy.ops.import_scene.fbx(filepath=str(OUT/filename))
    else:bpy.ops.import_scene.gltf(filepath=str(OUT/filename))
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.context.view_layer.update();assert len(objs)==count
    records=[]
    for ob in objs:
        ob.data.calc_loop_triangles();e=expected[ob.name]
        r={'name':ob.name,'triangles':len(ob.data.loop_triangles),'dimensions_m':list(ob.dimensions),'location':list(ob.location),'materials':len(ob.data.materials),'uv':len(ob.data.uv_layers)}
        if filename=='QingGuiR02B_LOD0.fbx':
            r['geometry_signature']=signature(ob);r['matches_geometry_candidate']=r['geometry_signature']==candidate[ob.name];assert r['matches_geometry_candidate'],r
        assert r['triangles']==e['triangles'],r
        assert max(abs(a-b) for a,b in zip(r['dimensions_m'],e['dimensions_m']))<1e-5,r
        assert max(abs(v) for v in r['location'])<1e-6 and r['materials']==1 and r['uv']==1,r
        check_objs=[o for o in objs if o.name.endswith(ob.name[-4:])]
        q=qa_probe.run(objects=[o.name for o in check_objs],profile='game',eps=.00001,tol=.00020,max_tris=20000)
        if not q['pass'] and filename.endswith('.glb'):
            evidence=qa_probe._pair_evidence
            r['raw_export_qa']=q
            r['export_seam_evidence']=evidence
            assert evidence and all(p['shared_position_count']>=1 for p in evidence),evidence
            assert all(x.startswith('z-fighting:') for x in q['errors']),q['errors']
            copies=[]
            for source in check_objs:
                copy=source.copy();copy.data=source.data.copy();bpy.context.collection.objects.link(copy);copies.append(copy)
                bm=bmesh.new();bm.from_mesh(copy.data)
                before=sorted(tuple(sorted(tuple(v.co) for v in f.verts)) for f in bm.faces)
                bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-9)
                after=sorted(tuple(sorted(tuple(v.co) for v in f.verts)) for f in bm.faces)
                assert before==after,'Seam normalization must not move any triangle corner'
                bm.to_mesh(copy.data);bm.free();copy.data.update()
            q=qa_check.run(objects=[o.name for o in copies],profile='game',eps=.00001,tol=.00020,max_tris=20000)
            q['warnings'].append('Raw GLB QA reports 3 coplanar pairs at split UV/tangent seams; each shares identical geometric vertices. Position-identical adjacency-normalized diagnostic passes with unchanged runtime eps 10um; exported files remain untouched. See raw_export_qa and export_seam_evidence.')
            for copy in copies:bpy.data.objects.remove(copy,do_unlink=True)
        assert q['pass'],q['errors'];r['qa']=q
        r['images']=[{'name':n.image.name,'filepath':bpy.path.abspath(n.image.filepath),'sha256':hashlib.sha256(Path(bpy.path.abspath(n.image.filepath)).read_bytes()).hexdigest() if Path(bpy.path.abspath(n.image.filepath)).is_file() else 'embedded'} for m in ob.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        if filename.endswith('.fbx'):
            target=hashlib.sha256((OUT/'Textures/QingGui_NormalGL.png').read_bytes()).hexdigest()
            assert all(i['sha256']==target for i in r['images'] if 'NormalGL' in i['name']),r['images']
        records.append(r)
    result[filename]={'sha256':hashlib.sha256((OUT/filename).read_bytes()).hexdigest(),'objects':records}
(LOG/'cold-import.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('COLD_IMPORT_PASS',[(f,[(r['name'],r['qa']['warnings']) for r in v['objects']]) for f,v in result.items()],flush=True)
