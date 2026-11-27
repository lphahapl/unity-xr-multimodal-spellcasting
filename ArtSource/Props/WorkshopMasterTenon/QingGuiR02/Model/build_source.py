import bpy,sys,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[4];LOG=ROOT/'Logs/workshop-master-tenon-qinggui-r02-20261126/model';PRE=OUT/'SourcePreview';PRE.mkdir(exist_ok=True)
sys.dont_write_bytecode=True;sys.path.insert(0,str(OUT))
from qinggui_geometry import build_body
from qinggui_detail import sculpt,material
from qinggui_wires import build_wires
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True
p=bpy.context.preferences.addons['cycles'].preferences;p.compute_device_type='CUDA';p.get_devices()
for d in p.devices:d.use=d.type=='CUDA'
s.cycles.device='GPU';s.view_settings.view_transform='AgX'
ob=build_body('QingGuiR02_Body_HIGH',dense=True);detail=sculpt(ob);mat,_,_=material();ob.data.materials.clear();ob.data.materials.append(mat);print('BODY_HIGH_READY',flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'QingGuiR02_BodyCheckpoint.blend'))
wire,paths=build_wires(ob,'QingGuiR02_GoldWire_HIGH',3);print('GOLD_HIGH_READY',len(wire.data.vertices),flush=True)
(LOG/'wire-source-paths.json').write_text(json.dumps(paths,indent=2),encoding='utf-8')
s.render.resolution_x=1000;s.render.resolution_y=1200;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
s.world=bpy.data.worlds.new('World');s.world.use_nodes=True;bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.12,.14,.16,1);bg.inputs['Strength'].default_value=.35
def light(name,loc,power,size):
    ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.size=size;o=bpy.data.objects.new(name,ld);s.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.19))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key',(.35,-.5,.6),15,.5);light('Fill',(-.4,-.15,.25),5,.4);light('Rim',(.2,.35,.45),14,.35)
cd=bpy.data.cameras.new('Ortho');cam=bpy.data.objects.new('Ortho',cd);s.collection.objects.link(cam);s.camera=cam;cd.type='ORTHO';cd.clip_start=.001
def render(name,loc,target=(0,0,.175),scale=.41):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cd.ortho_scale=scale;s.render.filepath=str(PRE/f'{name}.png');bpy.ops.render.render(write_still=True);print('SOURCE_VIEW_READY',name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'QingGuiR02_Source.blend'))
render('01_Front45',(.48,-.72,.40))
render('02_GoldContact',(.21,-.24,.28),(-.015,-.022,.263),.08)
render('03_Aperture',(.09,-.40,.35),(.035,0,.327),.075)
render('04_Shoulder',(.18,-.40,.20),(0,0,.115),.19)
q=qa_check.run(objects=[ob.name,wire.name],profile='render',eps=.00001,tol=.0002,max_tris=2000000)
(LOG/'source-high-qa.json').write_text(json.dumps(q,indent=2),encoding='utf-8')
print('HIGH_QA_RESULT',q['pass'],q['errors'],q['warnings'],flush=True)
clay=bpy.data.materials.new('ReviewClay');clay.use_nodes=True;clay.diffuse_color=(.52,.52,.52,1);b=next(n for n in clay.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Base Color'].default_value=(.52,.52,.52,1);b.inputs['Roughness'].default_value=.35
ob.data.materials[0]=clay;wire.data.materials[0]=clay
render('05_Clay45',(.48,-.72,.40));render('06_ClayShoulder',(.18,-.40,.20),(0,0,.13),.12)
print('SOURCE_PREVIEW_COMPLETE',flush=True)
