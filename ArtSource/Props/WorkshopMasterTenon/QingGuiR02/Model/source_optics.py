import bpy,sys,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;PRE=P/'SourcePreview';bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02_Source.blend'));s=bpy.context.scene
body=bpy.data.objects['QingGuiR02_Body_HIGH'];nt=body.data.materials[0].node_tree;b=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
# Reuse the existing 1-Bronze factor from the source shader's SSS link.
mul=b.inputs['Subsurface Weight'].links[0].from_node;jade=mul.inputs[0].links[0].from_socket;coat=nt.nodes.new('ShaderNodeMath');coat.operation='MULTIPLY';coat.inputs[1].default_value=.18;nt.links.new(jade,coat.inputs[0]);nt.links.new(coat.outputs[0],b.inputs['Coat Weight'])
bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR02_Source.blend'))
s.cycles.samples=64;s.cycles.device='GPU';pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
cam=bpy.data.objects['Ortho'];bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND')
def render(name,loc,target=(0,0,.175),scale=.41):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(PRE/f'{name}.png');bpy.ops.render.render(write_still=True);print('OPTICS_VIEW',name,flush=True)
for name,loc in [('07_Front',(0,-.8,.175)),('08_Right',(.8,0,.175)),('09_Back',(0,.8,.175))]:render(name,loc)
for o in s.objects:
    if o.type=='LIGHT':o.hide_render=True
ld=bpy.data.lights.new('PolishStrip','AREA');ld.shape='RECTANGLE';ld.size=.015;ld.size_y=.28;ld.energy=4;o=bpy.data.objects.new('PolishStrip',ld);s.collection.objects.link(o);o.location=(-.25,-.38,.30);o.rotation_euler=(Vector((0,0,.23))-o.location).to_track_quat('-Z','Y').to_euler();bg.inputs['Strength'].default_value=.1
render('10_PolishStrip',(.48,-.72,.40))
o.location=(0,.13,.26);o.rotation_euler=(Vector((0,0,.23))-o.location).to_track_quat('-Z','Y').to_euler();ld.shape='DISK';ld.size=.11;ld.energy=5;bg.inputs['Strength'].default_value=0
render('11_Backlight_5W',(.14,-.75,.25),(0,0,.19));ld.energy=25;render('12_Backlight_25W_Stress',(.14,-.75,.25),(0,0,.19));ld.energy=0;render('13_NoLight',(.14,-.75,.25),(0,0,.19))
