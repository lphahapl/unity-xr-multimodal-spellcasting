"""Independent CLI blockout, no final bake or formal project writes."""
import bpy,sys,json,hashlib,bmesh
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[4]
LOG=ROOT/'Logs/workshop-master-tenon-qinggui-r01-20261125/model';PRE=OUT/'SourcePreview'
PRE.mkdir(exist_ok=True);LOG.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(OUT));from qinggui_geometry import build_body,section,Z
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
s=bpy.context.scene;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True
prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
for d in prefs.devices:d.use=d.type=='CUDA'
s.cycles.device='GPU';s.view_settings.view_transform='AgX'
from qinggui_detail import sculpt,material
ob=build_body('QingGui_HIGH',dense=True);detail=sculpt(ob);mat,sockets,source_nodes=material();ob.data.materials.clear();ob.data.materials.append(mat)
print('HIGH_DETAIL_READY',detail,flush=True)
bpy.context.view_layer.update();ob.data.calc_loop_triangles()
q=qa_check.run(objects=[ob.name],profile='render',eps=.00010,tol=.0002,max_tris=2000000)
(LOG/'source-high-qa.json').write_text(json.dumps(q,indent=2),encoding='utf-8')
report={'stage':'HIGH_DETAIL_SOURCE_CANDIDATE_NOT_BAKED','triangles':len(ob.data.loop_triangles),'dimensions_m':list(ob.dimensions),'origin':list(ob.location),'aperture':{'center_m':[.041,0,.329],'front_size_m':[.013,.024],'real_boolean_through_hole':True},'section_profile':[{'z_m':float(z),'left_m':section(z)[0],'right_m':section(z)[1],'depth_m':section(z)[2]} for z in Z],'actual_grip_230_mm':list(section(.230)),'qa_pass':q['pass'],'detail':detail,'limitations':['High source preview only; final game bake and thickness ray measurement pending.','No new latch/Unity/headset validation.']}
(LOG/'source-high-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
s.render.resolution_x=1000;s.render.resolution_y=1200;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
s.world.use_nodes=True;bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.12,.14,.16,1);bg.inputs['Strength'].default_value=.35
def light(name,loc,power,size):
    ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.size=size;o=bpy.data.objects.new(name,ld);s.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.19))-o.location).to_track_quat('-Z','Y').to_euler()
light('Key',(.35,-.5,.6),15,.5);light('Fill',(-.4,-.15,.25),5,.4);light('Rim',(.2,.35,.45),14,.35)
cd=bpy.data.cameras.new('Ortho');cam=bpy.data.objects.new('Ortho',cd);s.collection.objects.link(cam);s.camera=cam;cd.type='ORTHO';cd.ortho_scale=.41;cd.clip_start=.001
for name,loc in [('01_Front',(0,-.8,.175)),('02_Right',(.8,0,.175)),('03_Back',(0,.8,.175)),('04_Front45',(.48,-.72,.40))]:
    cam.location=loc;cam.rotation_euler=(Vector((0,0,.175))-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(PRE/f'{name}.png');bpy.ops.render.render(write_still=True);print('SOURCE_VIEW_READY',name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'QingGuiR01_Source.blend'))
print('SOURCE_PREVIEW_COMPLETE',json.dumps(report),flush=True)
