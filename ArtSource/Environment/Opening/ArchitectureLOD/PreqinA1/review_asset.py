"""Read the saved blend; generate independent material, eave, and topology evidence."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
HERE=Path(__file__).resolve().parent
scene=bpy.context.scene
objects=sorted([o for o in scene.objects if o.type=='MESH'],key=lambda o:o.name)
if '--qa' in sys.argv:
    sys.path.insert(0,str(Path.home()/'.codex/skills/blender-asset-qa/scripts'))
    import qa_check
    findings={}
    for o in objects:
        o.hide_set(False)
        findings[o.name]=qa_check.run(objects=[o.name],profile='game',eps=.0003)
        o.hide_set(True)
        print(o.name,findings[o.name]['errors'][:4],flush=True)
    (HERE/'geometry-qa.json').write_text(json.dumps(findings,indent=2),encoding='utf8')
else:
    scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
    scene.render.threads_mode='FIXED';scene.render.threads=8
    scene.world=bpy.data.worlds.new('Sandstone bounce');scene.world.use_nodes=True
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.60,.68,.78,1);bg.inputs[1].default_value=.5
    bpy.ops.object.light_add(type='SUN',location=(0,0,8));key=bpy.context.object;key.data.energy=3.2;key.data.angle=.12
    key.rotation_euler=Vector((.38,.77,-.88)).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO'
    for o in objects:o.hide_render=True
    def shot(name,file,aim,vec,size,w=900,h=630):
        o=bpy.data.objects[name];o.hide_render=False;o.hide_set(False)
        cam.location=Vector(aim)+Vector(vec)
        cam.rotation_euler=(-Vector(vec)).to_track_quat('-Z','Y').to_euler()
        cam.data.ortho_scale=size
        scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.resolution_percentage=100
        scene.render.filepath=str(HERE/(file+'.jpg'));scene.render.image_settings.file_format='JPEG';scene.render.image_settings.quality=92
        bpy.ops.render.render(write_still=True);o.hide_render=True
    for i in range(3):
        shot('CalibrationHall_'+str(i)+'_Grounded_LOD0','Hall'+str(i),(0,0,1.65),(7,-11,6),10 if i<2 else 11)
    shot('CalibrationHall_0_Grounded_LOD0','EaveDetail',(1.7,-2.36,2.78),(2,-5,1.4),2.2)
    shot('CalibrationHall_0_Grounded_LOD0','UnderEave',(1.8,-2.32,2.75),(1.6,-4,-1.3),2.5)
    for k,d in [('Wake',23),('Corridor',14),('Trial',27)]:
        shot('Roof_'+k+'_Support_LOD0','Shell'+k,(0,0,-1.2),(12,-15,10),d)
    scene.render.engine='CYCLES'
    mat=bpy.data.materials[0];node=mat.node_tree.nodes.new('ShaderNodeWireframe');node.use_pixel_size=True;node.inputs[0].default_value=.8
    mix=mat.node_tree.nodes.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(.61,.58,.49,1);mix.inputs[2].default_value=(.04,.045,.03,1)
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');mat.node_tree.links.new(node.outputs[0],mix.inputs[0]);mat.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    for lod in range(3):
        shot('CalibrationHall_0_Grounded_LOD'+str(lod),'WireLOD'+str(lod),(0,0,1.55),(7,-11,6),10,670,460)
