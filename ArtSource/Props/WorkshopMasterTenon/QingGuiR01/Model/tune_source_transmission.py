"""Only source jade transport changes; all baked PBR channels and geometry unchanged."""
import bpy,json
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
for file in ['QingGuiR01.blend','QingGuiR01_Source.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(OUT/file));ob=bpy.data.objects['QingGui_HIGH'];nt=ob.data.materials[0].node_tree
    bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED');out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
    bs.inputs['Subsurface Radius'].default_value=(1,.75,.48);bs.inputs['Subsurface Scale'].default_value=.0065
    bs.inputs['Transmission Weight'].links[0].from_node.inputs[1].default_value=.02
    absorb=nt.nodes.new('ShaderNodeVolumeAbsorption');absorb.inputs['Color'].default_value=(.78,.74,.63,1);absorb.inputs['Density'].default_value=18;nt.links.new(absorb.outputs['Volume'],out.inputs['Volume'])
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/file))
s=bpy.context.scene;cam=s.camera;original=(cam.location.copy(),cam.rotation_euler.copy(),cam.data.ortho_scale)
pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
s.cycles.device='GPU';s.cycles.samples=64
def render(name):
    s.render.filepath=str(OUT/'SourcePreview'/f'{name}.png');bpy.ops.render.render(write_still=True);print('SOURCE_FINAL_VIEW',name,flush=True)
for name,loc in [('01_Front',(0,-.8,.175)),('02_Right',(.8,0,.175)),('03_Back',(0,.8,.175)),('04_Front45',(.48,-.72,.40))]:
    cam.location=loc;cam.rotation_euler=(Vector((0,0,.175))-cam.location).to_track_quat('-Z','Y').to_euler();render(name)
lamps=[o for o in s.objects if o.type=='LIGHT'];energies=[o.data.energy for o in lamps]
for o in lamps:o.data.energy=0
bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');strength=bg.inputs['Strength'].default_value;bg.inputs['Strength'].default_value=0
ld=bpy.data.lights.new('Backlight controlled test','AREA');ld.shape='DISK';ld.size=.11;ld.energy=5;lo=bpy.data.objects.new('Backlight controlled test',ld);s.collection.objects.link(lo);lo.location=(0,.13,.26);lo.rotation_euler=(Vector((0,0,.23))-lo.location).to_track_quat('-Z','Y').to_euler()
cam.location=(.14,-.75,.25);cam.rotation_euler=(Vector((0,0,.19))-cam.location).to_track_quat('-Z','Y').to_euler()
render('06a_Backlight_Normal_5W');ld.energy=25;render('06b_Backlight_Stress_25W');ld.energy=0;render('07_NoLight')
for o,e in zip(lamps,energies):o.data.energy=e
bg.inputs['Strength'].default_value=strength;bpy.data.objects.remove(lo,do_unlink=True);cam.location,cam.rotation_euler,cam.data.ortho_scale=original
# Keep the editable source with the ordinary lighting rig, not the pressure-test light.
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'QingGuiR01_Source.blend'))
report={'transmission_weight':.02,'subsurface_radius_rgb':[1,.75,.48],'subsurface_scale_m':.0065,'volume_absorption_color':[.78,.74,.63,1],'volume_absorption_density':18,'normal_backlight_watts':5,'stress_backlight_watts':25,'same_backlight_camera':True,'game_baked_channels_changed':False,'no_emission':True}
(OUT.parents[4]/'Logs/workshop-master-tenon-qinggui-r01-20261125/model/source-transmission-tuning.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SOURCE_TUNING_COMPLETE',flush=True)
