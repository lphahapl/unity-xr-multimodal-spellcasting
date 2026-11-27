import bpy,sys,json
import numpy as np
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;sys.dont_write_bytecode=True;sys.path.insert(0,str(P));from qinggui_detail import border_distance,material
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'));body=bpy.data.objects['QingGuiR02_Body_HIGH'];me=body.data;attr=me.attributes['HoleSafeNormal']
coords=np.empty((len(me.vertices),3),np.float32);me.vertices.foreach_get('co',coords.ravel());vn=np.empty_like(coords);me.vertices.foreach_get('normal',vn.ravel())
near=np.array([z>.105 and abs(border_distance(float(x),float(z)))<.001 and ((x-.041)/.0065)**2+((z-.329)/.012)**2>1.7 for x,y,z in coords])
vi=np.empty(len(me.loops),np.int32);me.loops.foreach_get('vertex_index',vi);norms=np.empty((len(me.loops),3),np.float32);attr.data.foreach_get('vector',norms.ravel());mask=near[vi];norms[mask]=vn[vi[mask]];attr.data.foreach_set('vector',norms.ravel());count=int(mask.sum())
mat,_,_=material();body.data.materials.clear();body.data.materials.append(mat);bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'))
s=bpy.context.scene;s.cycles.samples=48;s.cycles.device='GPU';pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
cam=bpy.data.objects['Ortho']
for name,loc,target,scale in [('04b_CopperJadeClose',(.18,-.40,.20),(.008,-.043,.130),.075),('03_Aperture',(.09,-.40,.35),(.035,0,.327),.075),('04_Shoulder',(.18,-.40,.20),(0,0,.115),.19)]:
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(P/f'SourcePreview/{name}.png');bpy.ops.render.render(write_still=True);print('EDGE_REVIEW',name,flush=True)
print('ACTUAL_STEP_NORMAL_CORNERS',count,flush=True)
