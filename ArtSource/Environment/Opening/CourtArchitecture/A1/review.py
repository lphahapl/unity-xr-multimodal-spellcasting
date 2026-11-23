"""Render and test the published FBX in a fresh Blender process, using the runtime maps."""
import sys, json
from pathlib import Path
import bpy
from mathutils import Vector
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import roof
sys.path.insert(0,str(Path.home()/'.codex/skills/blender-asset-qa/scripts'))
import qa_check

def review(model,mode):
    scene=bpy.context.scene
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    dest=roof.DEST/model
    bpy.ops.import_scene.fbx(filepath=str(dest/(model+'.fbx')),use_custom_normals=True)
    obs=sorted(bpy.context.scene.objects,key=lambda o:o.name)
    data={}
    for i,o in enumerate(obs):
        assert o.name==f'{model}_Tier{i}'
        assert len(o.material_slots)==1 and o.material_slots[0].name==o.name+'_Baked'
        o.data.materials[0].name='FBX_'+o.name
        o.data.materials[0]=roof.material(o.name,dest)
        assert [uv.name for uv in o.data.uv_layers]==['Surface','Lightmap']
        if mode=='qa':
            r=qa_check.run(objects=[o.name],profile='game',eps=.0002,tol=.003)
            data[o.name]=r
            print(o.name,r['errors'],r['warnings'],flush=True)
        o.hide_render=i!=0
    out=HERE/'Review'
    if mode=='qa':
        (out/(model+'_FBX_QA.json')).write_text(json.dumps(data,indent=2))
        return
    scene.render.engine='CYCLES'
    scene.cycles.samples=12
    scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new('Diffuse daylight')
    scene.world.use_nodes=True
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
    bg.inputs[0].default_value=(.8,.85,.90,1)
    bg.inputs[1].default_value=.75
    sun=bpy.data.lights.new('Raking sun','SUN')
    sun.energy=3.25
    sun.angle=.13
    sun.color=(1,.944,.86)
    lamp=bpy.data.objects.new(sun.name,sun)
    scene.collection.objects.link(lamp)
    lamp.rotation_euler=Vector((.6,.55,-.84)).to_track_quat('-Z','Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=400,location=(0,0,-.002 if model=='CourtGatehouse' else -.08))
    floor=bpy.context.object
    mat=bpy.data.materials.new('Neutral stone ground')
    mat.diffuse_color=(.15,.145,.132,1)
    floor.data.materials.append(mat)
    cam=bpy.data.cameras.new('Asset review')
    camera=bpy.data.objects.new('Review camera',cam)
    scene.collection.objects.link(camera)
    scene.camera=camera
    cam.type='ORTHO'
    scene.view_settings.view_transform='AgX'
    scene.render.resolution_x=1080
    scene.render.resolution_y=688
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='JPEG'
    scene.render.image_settings.quality=94
    x,y,eave,crown,_=roof.MODELS[model]
    gate=model=='CourtGatehouse'
    if mode=='wire':
        floor.hide_render=True
        mat=obs[0].data.materials[0]
        nodes=mat.node_tree.nodes
        links=mat.node_tree.links
        wire=nodes.new('ShaderNodeWireframe')
        wire.use_pixel_size=True
        wire.inputs['Size'].default_value=.65
        mix=nodes.new('ShaderNodeMixRGB')
        mix.inputs[1].default_value=(.6,.62,.65,1)
        mix.inputs[2].default_value=(.015,.012,.01,1)
        links.new(wire.outputs[0],mix.inputs[0])
        emit=nodes.new('ShaderNodeEmission')
        links.new(mix.outputs[0],emit.inputs[0])
        links.new(emit.outputs[0],next(n for n in nodes if n.type=='OUTPUT_MATERIAL').inputs['Surface'])
        scene.cycles.samples=3
        scene.cycles.use_denoising=False
        scene.render.image_settings.file_format='PNG'
        scene.render.resolution_x=640
        scene.render.resolution_y=400
        centre=Vector((0,0,crown*.45))
        for title,direction in [('three_quarter',(.5,-1,.5)),('bottom',(.04,-.02,-1))]:
            camera.location=centre+Vector(direction).normalized()*32
            camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
            cam.ortho_scale=2.85*x
            scene.render.filepath=str(out/(model+'_wire_'+title+'.png'))
            bpy.ops.render.render(write_still=True)
        return
    views=[
        ('overview',(0,0,crown*(.46 if gate else .38)),(.48,-1,.30 if gate else .61),2.62*x if not gate else 10.85),
        ('eave',((2.04 if gate else .40*x),-y+.33,eave-.12),(.68,-1,-.32),4.25 if gate else 4.95)
    ]
    if mode in ('tip_before','tip_after'):
        scene.cycles.samples=48
        scene.render.image_settings.quality=97
        floor.hide_render=True
        views=[(mode,(3.29,-y+.08,eave-.05),(.24,-1,-.22),1.38)]
    for name,target,direction,scale in views:
        centre=Vector(target)
        camera.location=centre+Vector(direction).normalized()*32
        camera.rotation_euler=(centre-camera.location).to_track_quat('-Z','Y').to_euler()
        cam.ortho_scale=scale
        if name=='eave':floor.hide_render=True
        scene.render.filepath=str(out/(model+'_'+name+'.jpg'))
        bpy.ops.render.render(write_still=True)

if __name__=='__main__':review(*sys.argv[-2:])

