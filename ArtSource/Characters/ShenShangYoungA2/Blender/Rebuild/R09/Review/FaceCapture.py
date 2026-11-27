"""Orthographic captures of the frozen, reimported GLB. Clay uses no colour or normal maps."""
from pathlib import Path
import hashlib,json,bpy,sys,shutil
from mathutils import Vector,Matrix
review=Path(__file__).resolve().parent;ver=sys.argv[-1];src=review.parents[1]/ver/f'ShenShang_{ver}.glb'
dest=review/'MatchedFace'/ver;dest.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(src));s=bpy.context.scene
s.render.engine='CYCLES';s.cycles.samples=13;s.cycles.use_denoising=True;s.view_settings.view_transform='AgX';s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Neutral portrait studio');s.world.use_nodes=True;b=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');b.inputs[0].default_value=(.33,.31,.29,1);b.inputs[1].default_value=.57
for nm,xyz,en,sz in [('soft key',(-1,-1.5,2.55),66,.84),('fill',(1.25,-.88,2.05),32,1.15),('rim',(.4,1.05,2.48),57,.76)]:
 data=bpy.data.lights.new(nm,'AREA');data.energy=en;data.size=sz;lamp=bpy.data.objects.new(nm,data);s.collection.objects.link(lamp);lamp.location=xyz;lamp.rotation_euler=(Vector((0,-.02,1.585))-lamp.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('Fixed face camera',bpy.data.cameras.new('Face camera'));s.collection.objects.link(cam);s.camera=cam;cam.data.type='ORTHO'
views=[('01_Front',(0,-.015,1.59),(0,-2.8,0),.386,620,740),('02_Profile',(0,-.015,1.59),(2.8,0,0),.386,620,740),('03_ThreeQuarter',(0,-.015,1.59),(-1.48,-2.24,.02),.386,620,740),('04_Hairline',(0,-.015,1.644),(-.40,-2.5,.48),.259,760,505),('05_Temple',(0,-.006,1.63),(1.21,-1.65,.38),.255,760,505),('06_Eyes_Nose_Lips',(0,-.094,1.557),(-.60,-2.75,.03),.162,660,560),('07_Nape',(0,.018,1.619),(-.04,2.15,.69),.315,600,565),('08_Full',(0,0,.88),(0,-3,0),1.88,500,720)]
additional=[('09_Right_Temple',(0,-.006,1.63),(-1.21,-1.65,.38),.255,760,505),('10_Right_Profile',(0,-.015,1.59),(-2.8,0,0),.386,620,740),('11_Crown_Rear',(0,.027,1.64),(-.56,1.28,1.16),.306,650,590),('12_Nape_Straight',(0,.012,1.586),(0,2.6,0),.309,660,620)]
registration=json.loads((review/'A2_Cameras.json').read_text('utf8'))
def registered(name,c,suffix):
 tgt=Vector(c['target']);direction=Vector(c['view']);basis=Matrix([c['right'],c['up'],c['view']]).transposed().to_4x4();basis.translation=tgt+direction*2.75;cam.matrix_world=basis;cam.data.ortho_scale=c['metresPerPixel']*c['height'];s.render.resolution_x=c['width'];s.render.resolution_y=c['height'];s.render.filepath=str(dest/('A2_'+name+suffix+'.png'));bpy.ops.render.render(write_still=True)
def take(v,suffix):
 name,point,offset,scale,width,height=v;cam.location=Vector(point)+Vector(offset);cam.rotation_euler=(-Vector(offset)).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.resolution_x=width;s.render.resolution_y=height;s.render.filepath=str(dest/(name+suffix+'.png'));bpy.ops.render.render(write_still=True)
if ver=='R08':
 old=review.parents[1]/'R08/Review/MatchedFace/R08'
 for pic in old.glob('*.png'):shutil.copyfile(pic,dest/pic.name)
else:
 for v in views:take(v,'')
for v in additional:take(v,'')
for nm,cal in registration.items():registered(nm,cal,'')
for material in [m for m in bpy.data.materials if m.use_nodes]:
 nt=material.node_tree;old=next((n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'),None);output=next((n for n in nt.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output),None)
 if not old or not output:continue
 plain=nt.nodes.new('ShaderNodeBsdfPrincipled');plain.inputs['Base Color'].default_value=(.39,.375,.35,1);plain.inputs['Roughness'].default_value=.64
 if old.inputs['Alpha'].is_linked:nt.links.new(old.inputs['Alpha'].links[0].from_socket,plain.inputs['Alpha'])
 nt.links.new(plain.outputs[0],output.inputs['Surface'])
if ver!='R08':
 for v in views[:6]:take(v,'_Clay')
for nm,cal in registration.items():registered(nm,cal,'_Clay')
(dest/'Cameras.json').write_text(json.dumps({'version':ver,'GLB_SHA256':hashlib.sha256(src.read_bytes()).hexdigest(),'views':views+additional,'registeredViews':registration,'clay':'identical studio with untextured neutral PBR; strand cutouts retain opacity only','surfaces':'frozen GLB reimported'},indent=2),encoding='utf8')
