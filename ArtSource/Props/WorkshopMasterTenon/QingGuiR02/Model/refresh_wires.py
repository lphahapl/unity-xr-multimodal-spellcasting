"""Refresh only separate wire geometry after local spacing review; body remains identical."""
import bpy,sys,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;sys.dont_write_bytecode=True;sys.path.insert(0,str(P));from qinggui_wires import build_wires
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02_Source.blend'));body=bpy.data.objects['QingGuiR02_Body_HIGH'];bpy.data.objects.remove(bpy.data.objects['QingGuiR02_GoldWire_HIGH'],do_unlink=True)
wire,paths=build_wires(body,'QingGuiR02_GoldWire_HIGH',3)
(P.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/wire-source-paths.json').write_text(json.dumps(paths,indent=2),'utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR02_Source.blend'))
s=bpy.context.scene;s.cycles.samples=48;s.cycles.device='GPU';pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
cam=bpy.data.objects['Ortho']
for name,loc,target,scale in [('01_Front45',(.48,-.72,.40),(0,0,.175),.41),('02_GoldContact',(.21,-.24,.28),(-.015,-.022,.263),.08),('03_Aperture',(.09,-.40,.35),(.035,0,.327),.075),('04_Shoulder',(.18,-.40,.20),(0,0,.115),.19)]:
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(P/f'SourcePreview/{name}.png');bpy.ops.render.render(write_still=True);print('REFRESH_VIEW',name,flush=True)
