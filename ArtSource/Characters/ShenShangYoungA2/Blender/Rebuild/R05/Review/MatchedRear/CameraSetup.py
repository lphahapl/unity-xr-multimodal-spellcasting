import bpy,sys,json,math,hashlib
from pathlib import Path
from mathutils import Vector
version=sys.argv[-1];root=Path('D:/UnityProjects/NarutoVR/ArtSource/Characters/ShenShangYoungA2/Blender/Rebuild');review=root/'R05/Review';out=review/'MatchedRear'/version;out.mkdir(parents=True,exist_ok=True)
src=root/version/('ShenShang_'+version+'.glb');bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(src));s=bpy.context.scene
s.render.engine='CYCLES';s.cycles.samples=8;s.cycles.use_denoising=True;s.world=bpy.data.worlds.new('Grey studio');s.world.use_nodes=True;b=s.world.node_tree.nodes.get('Background');b.inputs[0].default_value=(.30,.29,.28,1);b.inputs[1].default_value=.66
s.view_settings.view_transform='AgX'
def lamp(n,p,e,size):
 data=bpy.data.lights.new(n,'AREA');data.energy=e;data.shape='DISK';data.size=size;o=bpy.data.objects.new(n,data);s.collection.objects.link(o);o.location=p;o.rotation_euler=(Vector((0,0,1.26))-o.location).to_track_quat('-Z','Y').to_euler()
lamp('Back-left key',(-1.38,1.9,2.65),95,1.65);lamp('Back-right fill',(1.32,1.2,1.64),51,1.8);lamp('Crown rim',(.6,-.9,2.9),60,1.6)
d=bpy.data.cameras.new('Rear orthographic inspection');cam=bpy.data.objects.new('Camera',d);s.collection.objects.link(cam);s.camera=cam;d.type='ORTHO'
shots=[('01_Back_Full',(0,0,.875),(0,3,0),1.86,570,820),('02_Top_Rear_Head',(0,.017,1.614),(-.08,2.0,1.44),.334,620,595),('03_Nape_Straight',(0,.012,1.586),(-.03,2.6,.015),.309,660,620),('04_Shoulders_Left',(0,.008,1.361),(1.15,2.35,.6),.72,700,645),('05_Shoulders_Right',(0,.008,1.361),(-1.15,2.35,.60),.72,700,645),('06_Back_Waist_Hem',(0,.01,.978),(0,2.8,.13),.685,700,585),('07_Neck_Shoulders',(0,.008,1.40),(0,2.65,1.04),.63,700,580)]
def take(shot,name=None):
 n,point,v,scale,w,h=shot;v=Vector(v);cam.location=Vector(point)+v;cam.rotation_euler=(-v).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale;s.render.resolution_percentage=100;s.render.resolution_x=w;s.render.resolution_y=h;s.render.filepath=str(out/((name or n)+'.png'));bpy.ops.render.render(write_still=True)
for shot in shots:take(shot)
# Cycles normally renders both sides. For the stress pass explicitly discard back-facing fragments from every material.
for m in bpy.data.materials:
 if not m.use_nodes:continue
 net=m.node_tree;outn=next((n for n in net.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output),None)
 if not outn or not outn.inputs['Surface'].is_linked:continue
 orig=outn.inputs['Surface'].links[0].from_socket;geo=net.nodes.new('ShaderNodeNewGeometry');tr=net.nodes.new('ShaderNodeBsdfTransparent');mix=net.nodes.new('ShaderNodeMixShader');net.links.new(geo.outputs['Backfacing'],mix.inputs[0]);net.links.new(orig,mix.inputs[1]);net.links.new(tr.outputs[0],mix.inputs[2]);net.links.new(mix.outputs[0],outn.inputs['Surface'])
for i in [0,5,6]:take(shots[i],shots[i][0]+'_CullBack')
(out/'Cameras.json').write_text(json.dumps({'input':src.name,'GLB_SHA256':hashlib.sha256(src.read_bytes()).hexdigest(),'projection':'orthographic','cameraSpace':'Blender Z-up after glTF import','shots':shots,'forcedCulling':['01','06','07'],'toolbeltPresent':False},indent=2))
