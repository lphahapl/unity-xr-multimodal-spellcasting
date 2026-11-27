"""Cold file checks in clean scenes, independent of the creating Blender session."""
import bpy,sys,json,hashlib
from pathlib import Path
OUT=Path(__file__).resolve().parent;LOG=OUT.parents[4]/'Logs/workshop-master-tenon-qinggui-r01-20261125/model'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
report=json.loads((OUT/'model-report.json').read_text('utf-8'));expected={o['name']:o for o in report['meshes']}
result={}
for filename,count in [('QingGui_LOD0.fbx',1),('QingGui_LODs.fbx',3),('QingGuiR01.glb',1)]:
    for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
    if filename.endswith('.fbx'):bpy.ops.import_scene.fbx(filepath=str(OUT/filename))
    else:bpy.ops.import_scene.gltf(filepath=str(OUT/filename))
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.context.view_layer.update();assert len(objs)==count
    records=[]
    for ob in objs:
        ob.data.calc_loop_triangles();e=expected[ob.name]
        r={'name':ob.name,'triangles':len(ob.data.loop_triangles),'dimensions_m':list(ob.dimensions),'location':list(ob.location),'materials':len(ob.data.materials),'uv':len(ob.data.uv_layers)}
        assert r['triangles']==e['triangles'],r
        assert max(abs(a-b) for a,b in zip(r['dimensions_m'],e['dimensions_m']))<1e-5,r
        assert max(abs(v) for v in r['location'])<1e-6 and r['materials']==1 and r['uv']==1,r
        q=qa_check.run(objects=[ob.name],profile='game',eps=.00015,tol=.0002);assert q['pass'],q['errors'];r['qa']=q
        r['images']=[{'name':n.image.name,'filepath':bpy.path.abspath(n.image.filepath),'sha256':hashlib.sha256(Path(bpy.path.abspath(n.image.filepath)).read_bytes()).hexdigest() if Path(bpy.path.abspath(n.image.filepath)).is_file() else 'embedded'} for m in ob.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        if filename.endswith('.fbx'):
            target=hashlib.sha256((OUT/'Textures/QingGui_NormalGL.png').read_bytes()).hexdigest()
            assert all(i['sha256']==target for i in r['images'] if 'NormalGL' in i['name']),r['images']
        records.append(r)
    result[filename]={'sha256':hashlib.sha256((OUT/filename).read_bytes()).hexdigest(),'objects':records}
(LOG/'cold-import.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('COLD_IMPORT_PASS',[(f,[(r['name'],r['qa']['warnings']) for r in v['objects']]) for f,v in result.items()],flush=True)
