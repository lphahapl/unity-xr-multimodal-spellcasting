"""Make matched, unobstructed waist/hem captures from two independently imported game assets."""
from pathlib import Path
import bpy,hashlib,json,sys
from mathutils import Vector

output=Path(__file__).resolve().parent
version=sys.argv[-1]
src=output.parents[1]/'Rebuild'/version/f'ShenShang_{version}.glb'
out=output/version;out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=10;scene.cycles.use_denoising=True
scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True
world=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
world.inputs[0].default_value=(.30,.29,.28,1);world.inputs[1].default_value=.66
scene.view_settings.view_transform='AgX'
for name,pos,energy,size in [('rear left',(-1.38,1.9,2.65),95,1.65),('rear right',(1.32,1.2,1.64),51,1.8),('forward fill',(.6,-.9,2.9),60,1.6)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1.26))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Comparison camera');cam=bpy.data.objects.new('Camera',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO'
shots=[('01_Back',(0,.01,.978),(0,2.8,.13),.685,700,585),
       ('02_Oblique_Left',(0,.005,.92),(1.8,2.4,.23),.62,720,555),
       ('03_Oblique_Right',(0,.005,.92),(-1.8,2.4,.23),.62,720,555),
       ('04_High_Rear',(0,.005,.922),(.28,2.4,1.22),.64,720,555),
       ('05_Low_Rear',(0,.005,.90),(-.26,2.4,-.95),.64,720,555),
       ('06_Front',(0,.005,.925),(0,-2.8,.17),.67,720,555),
       ('07_Profile',(0,.005,.925),(2.8,0,.06),.61,720,555)]
for name,point,v,scale,width,height in shots:
    cam.location=Vector(point)+Vector(v);cam.rotation_euler=(-Vector(v)).to_track_quat('-Z','Y').to_euler()
    d.ortho_scale=scale;scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.resolution_percentage=100
    scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
(out/'Cameras.json').write_text(json.dumps({'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'views':shots,'renderEngine':scene.render.engine,'pose':'as exported, no toolbelt'},indent=2),encoding='utf8')
