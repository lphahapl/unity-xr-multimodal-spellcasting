"""Verify exported FBXs and render the runtime LOD0 material from four views."""
import bpy,sys,os,json
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
STAGE=Path(os.environ.get('PAVILION_STAGE','D:/Temp/FurnishPavilionB_bake'))
sys.path.insert(0,str(Path.home()/'.codex/skills/blender-asset-qa/scripts'))
import qa_check
scene=bpy.context.scene
names=['ArcCabinet','BenchDressing','HangingDivider']
report={}
for name in names:
    ob=bpy.data.objects[name];ob.hide_set(False);ob.hide_render=False
    result=qa_check.run(objects=[name],profile='game',eps=.00018,tol=.0032)
    report[name]={'qa':result,'roundtrip':[]}
for ob in scene.objects:
    if ob.type=='MESH':ob.hide_render=True;ob.hide_set(True)
scene.cycles.samples=4;scene.cycles.use_denoising=True
scene.render.resolution_x=760;scene.render.resolution_y=540;scene.render.resolution_percentage=100
scene.render.film_transparent=True
scene.world=bpy.data.worlds.new('Diffuse studio');scene.world.use_nodes=True
back=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
back.inputs[0].default_value=(.55,.57,.59,1);back.inputs[1].default_value=.70
for xyz,power,size in [((-3,-4,5),490,3.0),((4,-2,3),280,2.8),((-.5,2,4),430,2.2)]:
    bpy.ops.object.light_add(type='AREA',location=xyz);lamp=bpy.context.object
    lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size
    lamp.rotation_euler=(Vector((0,0,.5))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO'
baked=bpy.data.materials['Pavilion_Baked']
for name in names:
    for tier in range(3):
        bpy.ops.import_scene.fbx(filepath=str(STAGE/f'{name}_LOD{tier}.fbx'),use_custom_normals=True)
        ob=next(x for x in bpy.context.selected_objects if x.type=='MESH')
        ob.data.calc_loop_triangles();assert len(ob.data.uv_layers)>0
        report[name]['roundtrip'].append({'level':tier,'tris':len(ob.data.loop_triangles),'dimensions':list(ob.dimensions),'materials':len(ob.material_slots)})
        if tier:
            bpy.data.objects.remove(ob,do_unlink=True);continue
        ob.data.materials.clear();ob.data.materials.append(baked)
        verts=[ob.matrix_world@v.co for v in ob.data.vertices]
        c=Vector([(min(p[i] for p in verts)+max(p[i] for p in verts))*.5 for i in range(3)])
        directions=[(.74,-1.5,.65),(0,-1,.02),(-1.3,-.7,.48),(-.65,1.35,.7)]
        if name=='BenchDressing':directions=[(.7,-1.25,1.2),(.01,-.03,1),(-1.1,-.6,.76),(-.5,1.2,.85)]
        for i,d in enumerate(directions):
            cam.location=c+Vector(d).normalized()*5
            q=(c-cam.location).to_track_quat('-Z','Y');cam.rotation_euler=q.to_euler()
            pts=[q.inverted()@(v-c) for v in verts]
            w=max(p.x for p in pts)-min(p.x for p in pts);h=max(p.y for p in pts)-min(p.y for p in pts)
            cam.data.ortho_scale=1.16*max(w,h*760/540)
            scene.render.filepath=str(STAGE/f'{name}_{i}.png')
            bpy.ops.render.render(write_still=True)
        ob.hide_render=True;ob.hide_set(True)
(HERE/'mesh-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
