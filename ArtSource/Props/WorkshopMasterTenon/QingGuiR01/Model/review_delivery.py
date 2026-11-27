"""Actual baked PBR and wire review; never substitutes source beauty for game low mesh."""
import bpy,sys
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent;PRE=OUT/'Previews';PRE.mkdir(exist_ok=True)
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import review_shots
bpy.ops.wm.open_mainfile(filepath=str(OUT/'QingGuiR01.blend'))
s=bpy.context.scene;low=bpy.data.objects['QingGui_LOD0'];high=bpy.data.objects['QingGui_HIGH'];lods=[bpy.data.objects['QingGui_LOD1'],bpy.data.objects['QingGui_LOD2']]
for ob in [high]+lods:ob.hide_render=True;ob.hide_set(True)
low.hide_render=False;low.hide_set(False)
review_shots.run(objects=[low.name],out=str(PRE/'QA_material.png'),views=['front','right','back','three_quarter'],mode='material',res=512)
review_shots.run(objects=[low.name],out=str(PRE/'QA_wire.png'),views=['front','right','back','three_quarter'],mode='wire',res=512)
s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True;s.cycles.device='GPU'
pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
s.render.resolution_x=1000;s.render.resolution_y=1200;s.render.resolution_percentage=100
s.world.use_nodes=True;bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.12,.14,.16,1);bg.inputs['Strength'].default_value=.35
cam=bpy.data.objects['Ortho'];s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.41
def render(name,loc,target=(0,0,.175),scale=.41):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(PRE/f'{name}.png');bpy.ops.render.render(write_still=True);print('LOW_VIEW_READY',name,flush=True)
for name,loc in [('01_Front',(0,-.8,.175)),('02_Right',(.8,0,.175)),('03_Back',(0,.8,.175)),('04_Front45',(.48,-.72,.40))]:render(name,loc)
render('05_GoldJadeClose',(.17,-.40,.34),(0,-.02,.276),.19)
render('06_ApertureClose',(.09,-.40,.35),(.035,0,.327),.075)
render('07_ShoulderAlignment',(.18,-.40,.20),(0,0,.115),.19)
low.hide_render=True;high.hide_render=False;high.hide_set(False);render('08_HighSameView',(.48,-.72,.40));high.hide_render=True;high.hide_set(True)
for i,ob in enumerate(lods,1):
    ob.hide_set(False);ob.hide_render=False;render(f'LOD{i}_SameView',(.48,-.72,.40));ob.hide_render=True;ob.hide_set(True)
low.hide_render=False;low.hide_set(False)
# Exact edges instead of the QA Wireframe modifier's acute triangle mitres.
cu=bpy.data.curves.new('ExactEdges','CURVE');cu.dimensions='3D';cu.bevel_depth=.00007;cu.bevel_resolution=0
for e in low.data.edges:
    sp=cu.splines.new('POLY');sp.points.add(1)
    for i,vi in enumerate(e.vertices):sp.points[i].co=(*low.data.vertices[vi].co,1)
wire=bpy.data.objects.new('ExactEdges',cu);s.collection.objects.link(wire);wire.color=(.02,.025,.03,1);low.color=(.68,.72,.72,1)
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='OBJECT';s.display.shading.light='STUDIO';s.display.shading.show_shadows=True
render('09_CleanWire45',(.48,-.72,.40));render('10_CleanWireFront',(0,-.8,.175))
print('REVIEW_COMPLETE',flush=True)
