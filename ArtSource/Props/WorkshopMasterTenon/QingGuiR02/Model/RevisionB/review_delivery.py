"""Render actual final two-renderer game mesh and source optics, same baseline cameras."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;PRE=P/'Previews';PRE.mkdir(exist_ok=True);LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import review_shots
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02B.blend'));s=bpy.context.scene
body=bpy.data.objects['QingGuiR02_Body_LOD0'];wire=bpy.data.objects['QingGuiR02_GoldWire_LOD0']
high=[bpy.data.objects['QingGuiR02_Body_HIGH'],bpy.data.objects['QingGuiR02_GoldWire_HIGH']]
meshes=[o for o in s.objects if o.type=='MESH']
def show(obs):
    for o in meshes:o.hide_set(o not in obs);o.hide_render=o not in obs
show([body,wire]);review_shots.run(objects=[body.name,wire.name],out=str(PRE/'QA_material.png'),views=['front','right','back','three_quarter'],mode='material',res=512)
review_shots.run(objects=[body.name,wire.name],out=str(PRE/'QA_wire.png'),views=['front','right','back','three_quarter'],mode='wire',res=512)
s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True;s.cycles.device='GPU';pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
s.render.resolution_x=1000;s.render.resolution_y=1200;s.render.resolution_percentage=100
bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.12,.14,.16,1);bg.inputs['Strength'].default_value=.35
cam=bpy.data.objects['Ortho'];s.camera=cam;cam.data.type='ORTHO'
def render(name,loc,target=(0,0,.175),scale=.41):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(PRE/f'{name}.png');bpy.ops.render.render(write_still=True);print('REVIEW_VIEW',name,flush=True)
for name,loc in [('01_Front',(0,-.8,.175)),('02_Right',(.8,0,.175)),('03_Back',(0,.8,.175)),('04_Front45',(.48,-.72,.40))]:render(name,loc)
render('05_GoldContact',(.21,-.24,.28),(-.015,-.022,.263),.08);render('06_Aperture',(.09,-.4,.35),(.035,0,.327),.075);render('07_Shoulder',(.18,-.4,.20),(0,0,.115),.19)
render('B_Game_BackHeld',(.20,.80,.27),(0,0,.175),.41)
render('B_Game_BackCrownCopperTail',(.09,.40,.35),(.035,0,.319),.09)
render('B_Game_FrontCrownCopperTail',(.09,-.40,.35),(.035,0,.319),.09)
render('B_Game_BackShoulder',(.18,.40,.20),(0,0,.115),.19)
render('B_Game_LeftCopperWrap',(.30,.35,.25),(.040,0,.215),.13)
show(high);render('08_HighSameView',(.48,-.72,.40));render('09_HighAperture',(.09,-.4,.35),(.035,0,.327),.075)
for level in [1,2]:
    show([bpy.data.objects[f'QingGuiR02_Body_LOD{level}'],bpy.data.objects[f'QingGuiR02_GoldWire_LOD{level}']]);render(f'LOD{level}_SameView',(.48,-.72,.40))
show([body,wire]);cu=bpy.data.curves.new('ExactEdges','CURVE');cu.dimensions='3D';cu.bevel_depth=.00006;cu.bevel_resolution=0
for ob in [body,wire]:
    for e in ob.data.edges:
        sp=cu.splines.new('POLY');sp.points.add(1)
        for i,vi in enumerate(e.vertices):sp.points[i].co=(*ob.data.vertices[vi].co,1)
edges=bpy.data.objects.new('ExactEdges',cu);s.collection.objects.link(edges);edges.color=(.02,.025,.03,1);body.color=(.68,.72,.72,1);wire.color=(.8,.65,.25,1)
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='OBJECT';s.display.shading.light='STUDIO';s.display.shading.show_shadows=True;render('10_CleanWire45',(.48,-.72,.40));render('11_CleanWireFront',(0,-.8,.175))
print('REVIEW_COMPLETE',flush=True)
