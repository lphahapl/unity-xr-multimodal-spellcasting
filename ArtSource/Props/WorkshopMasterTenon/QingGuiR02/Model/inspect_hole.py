import bpy,sys,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02_Source.blend'))
s=bpy.context.scene;s.cycles.samples=32;s.cycles.device='GPU';pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
cam=bpy.data.objects['Ortho'];cam.location=(.09,-.40,.35);cam.rotation_euler=(Vector((.035,0,.327))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.075
bpy.data.objects['QingGuiR02_GoldWire_HIGH'].hide_render=True;s.render.filepath=str(P/'SourcePreview/03b_Aperture_BodyOnly.png');bpy.ops.render.render(write_still=True)
print('APERTURE_BODY_ONLY_READY',flush=True)
