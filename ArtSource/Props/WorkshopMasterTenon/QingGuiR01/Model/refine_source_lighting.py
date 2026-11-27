"""Refine wall masking and document true source material under controlled light."""
import bpy,sys,json
import numpy as np
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent;sys.path.insert(0,str(OUT))
from qinggui_geometry import section
from qinggui_detail import material
bpy.ops.wm.open_mainfile(filepath=str(OUT/'QingGuiR01_Source.blend'))
s=bpy.context.scene;ob=bpy.data.objects['QingGui_HIGH'];inn=[];outer=[]
for v in ob.data.vertices:
    l,r,d,rr=section(max(0,min(.35,float(v.co.z))));inn.append(d/2-rr-.003);outer.append(d/2-rr*.3)
for name,vals in [('FrontInner',inn),('FrontOuter',outer)]:
    a=ob.data.attributes.get(name) or ob.data.attributes.new(name,'FLOAT','POINT');a.data.foreach_set('value',vals)
mat,_,_=material();ob.data.materials.clear();ob.data.materials.append(mat)
pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
s.cycles.device='GPU';s.cycles.samples=64
cam=s.camera;original_camera=(cam.location.copy(),cam.rotation_euler.copy(),cam.data.ortho_scale)
lamps=[o for o in s.objects if o.type=='LIGHT'];energies=[o.data.energy for o in lamps]
bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');oldstrength=bg.inputs['Strength'].default_value
def render(name):
    s.render.filepath=str(OUT/'SourcePreview'/f'{name}.png');bpy.ops.render.render(write_still=True);print('MATERIAL_PROBE_READY',name,flush=True)
render('04_Front45')
for o in lamps:o.data.energy=0
bg.inputs['Strength'].default_value=.10
ld=bpy.data.lights.new('Narrow controlled strip','AREA');ld.shape='RECTANGLE';ld.size=.015;ld.size_y=.28;ld.energy=4
lo=bpy.data.objects.new('Narrow controlled strip',ld);s.collection.objects.link(lo);lo.location=(-.25,-.38,.30);lo.rotation_euler=(Vector((0,0,.22))-lo.location).to_track_quat('-Z','Y').to_euler()
render('05_StripSideLight')
bg.inputs['Strength'].default_value=0;ld.shape='DISK';ld.size=.11;ld.energy=25;lo.location=(.0,.13,.26);lo.rotation_euler=(Vector((0,0,.23))-lo.location).to_track_quat('-Z','Y').to_euler()
cam.location=(.14,-.75,.25);cam.rotation_euler=(Vector((0,0,.19))-cam.location).to_track_quat('-Z','Y').to_euler()
render('06_Backlight')
ld.energy=0;render('07_NoLight')
for o,e in zip(lamps,energies):o.data.energy=e
bg.inputs['Strength'].default_value=oldstrength;bpy.data.objects.remove(lo,do_unlink=True)
cam.location,cam.rotation_euler,cam.data.ortho_scale=original_camera
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'QingGuiR01_Source.blend'))
print('SOURCE_REFINED_SAVED',flush=True)
