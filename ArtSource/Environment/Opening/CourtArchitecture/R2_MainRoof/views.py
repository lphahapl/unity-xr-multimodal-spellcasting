"""Render the roof and its two shared ceramic ends in assembly, using the shipping maps."""
from pathlib import Path
import math
import sys
import bpy
from mathutils import Matrix,Vector
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build as b

def load_meshes(path):
    before=set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return sorted(set(bpy.context.scene.objects)-before,key=lambda o:o.name)

def ceramic():
    mat=bpy.data.materials.new('Shared grey earthenware')
    mat.use_nodes=True
    nt=mat.node_tree
    bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.89
    for name in ('BaseColor','Normal'):
        tex=nt.nodes.new('ShaderNodeTexImage')
        tex.image=bpy.data.images.load(str(b.CAP/('GreyEarthenware_'+name+'.png')))
        if name=='Normal':
            tex.image.colorspace_settings.name='Non-Color'
            normal=nt.nodes.new('ShaderNodeNormalMap')
            normal.inputs['Strength'].default_value=.65
            nt.links.new(tex.outputs[0],normal.inputs['Color'])
            nt.links.new(normal.outputs[0],bs.inputs['Normal'])
        else:nt.links.new(tex.outputs[0],bs.inputs['Base Color'])
    return mat

def technical_material(ob,wire):
    mat=bpy.data.materials.new('Topology' if wire else 'Component')
    mat.use_nodes=True
    nt=mat.node_tree
    bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(.39,.45,.49,1) if ob.name.startswith(b.MODEL) else (.67,.30,.13,1)
    if wire:
        edge=nt.nodes.new('ShaderNodeWireframe')
        edge.use_pixel_size=True
        edge.inputs['Size'].default_value=.68
        mix=nt.nodes.new('ShaderNodeMixRGB')
        mix.inputs[1].default_value=(.75,.80,.82,1)
        mix.inputs[2].default_value=(.012,.014,.017,1)
        nt.links.new(edge.outputs[0],mix.inputs[0])
        emit=nt.nodes.new('ShaderNodeEmission')
        nt.links.new(mix.outputs[0],emit.inputs[0])
        nt.links.new(emit.outputs[0],next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL').inputs['Surface'])
    ob.data.materials[0]=mat

def render():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    roofs=load_meshes((b.DEST if '--published' in sys.argv else b.HERE)/(b.MODEL+'.fbx'))
    caps=load_meshes(b.CAP/'PreqinRidgeCap.fbx')
    capmat=ceramic()
    tiers=[]
    for i,(r,c) in enumerate(zip(roofs,caps)):
        r.data.materials[0]=b.roof.material(r.name,b.DEST)
        c.data.materials[0]=capmat
        basis=c.matrix_world.copy()
        other=c.copy()
        scene.collection.objects.link(other)
        for sign,obj in [(-1,c),(1,other)]:
            obj.matrix_world=(Matrix.Translation((sign*b.REAR,0,b.SEAT)) @
                              Matrix.Rotation(sign*math.pi/2,4,'Z') @
                              Matrix.Diagonal((1.60,1.0,1.56,1.0)) @ basis)
        tiers.append([r,c,other])
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.world=bpy.data.worlds.new('Open shade')
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get('Background')
    bg.inputs[0].default_value=(.77,.84,.94,1)
    bg.inputs[1].default_value=.80
    light=bpy.data.lights.new('Afternoon','SUN')
    light.energy=3.7
    light.angle=.12
    light.color=(1,.934,.845)
    sun=bpy.data.objects.new(light.name,light)
    scene.collection.objects.link(sun)
    sun.rotation_euler=Vector((-.60,.78,-.99)).to_track_quat('-Z','Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=400,location=(0,0,-.14))
    floor=bpy.context.object
    ground=bpy.data.materials.new('Ground')
    ground.diffuse_color=(.18,.175,.16,1)
    floor.data.materials.append(ground)
    camera=bpy.data.cameras.new('Asset camera')
    eye=bpy.data.objects.new(camera.name,camera)
    scene.collection.objects.link(eye)
    scene.camera=eye
    camera.type='ORTHO'
    scene.view_settings.view_transform='AgX'
    scene.render.image_settings.file_format='JPEG'
    scene.render.image_settings.quality=96
    scene.render.resolution_percentage=100
    views=[
        ('B2_Overview',0,(0,0,2.14),(1.0,-.76,.45),26.7,1320,790),
        ('B2_Gable',0,(b.WALL,0,3.45),(1,-.155,.17),12.4,1320,730),
        ('B2_Cap',0,(b.REAR+.09,-.15,b.CROWN-.16),(1,-.70,.245),2.38,1080,720),
        ('B2_Eave',0,(b.X-.17,-3.5,.72),(1,-.30,-.18),2.95,1080,670),
        ('B2_Tier1',1,(0,0,2.14),(1,-.76,.45),26.7,880,530),
        ('B2_Tier2',2,(0,0,2.14),(1,-.76,.45),26.7,880,530)]
    if '--topology' in sys.argv:
        views=[('QA_components',0,(0,0,2.14),(1,-.68,.46),26.3,960,560),
               ('QA_wire',0,(0,0,2.14),(.89,-.92,.57),26.7,1024,610),
               ('QA_wire_under',0,(0,0,1.8),(.24,-.16,-1),28.2,1024,910)]
        scene.cycles.samples=5
        scene.cycles.use_denoising=False
    for name,tier,target,direction,width,sx,sy in views:
        if '--view' in sys.argv and name not in sys.argv[sys.argv.index('--view')+1:]:continue
        for i,objs in enumerate(tiers):
            for ob in objs:ob.hide_render=i!=tier
        floor.hide_render=name in ('B2_Eave','QA_wire_under')
        if '--topology' in sys.argv:
            scene.cycles.use_denoising=name=='QA_components'
            for ob in tiers[tier]:technical_material(ob,name.startswith('QA_wire'))
        eye.location=Vector(target)+Vector(direction).normalized()*34
        eye.rotation_euler=(Vector(target)-eye.location).to_track_quat('-Z','Y').to_euler()
        camera.ortho_scale=width
        scene.render.resolution_x=sx
        scene.render.resolution_y=sy
        scene.render.filepath=str(b.HERE/(name+'.jpg'))
        bpy.ops.render.render(write_still=True)

if __name__=='__main__':render()
