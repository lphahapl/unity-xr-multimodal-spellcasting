"""Measure exported-game surface sections, independently of profile parameter claims."""
import bpy,bmesh,json,sys,hashlib
from pathlib import Path
import numpy as np
from mathutils.kdtree import KDTree
OUT=Path(__file__).resolve().parent;LOG=OUT.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'QingGuiR02.blend'))
ob=bpy.data.objects['QingGuiR02_Body_LOD0'];me=ob.data
vs=np.array([tuple(v.co) for v in me.vertices]);edges=np.array([tuple(e.vertices) for e in me.edges]);a=vs[edges[:,0]];b=vs[edges[:,1]];dd=b-a
zs=[0,.003,.006,.05,.101,.105,.110,.115,.120,.125,.130,.135,.140,.145,.155,.170,.190,.210,.230,.250,.270,.280,.295,.308,.320,.330,.339,.345,.349,.350]
sections=[]
for z in zs:
    cross=((a[:,2]-z)*(b[:,2]-z)<=0)&(abs(dd[:,2])>1e-10)
    t=(z-a[cross,2])/dd[cross,2];pts=a[cross]+t[:,None]*dd[cross]
    on=vs[abs(vs[:,2]-z)<1e-6];pts=np.vstack([pts,on])
    mn=pts.min(axis=0);mx=pts.max(axis=0)
    sections.append({'height_m':z,'min_x_m':float(mn[0]),'max_x_m':float(mx[0]),'width_x_m':float(mx[0]-mn[0]),'min_depth_blender_y_m':float(mn[1]),'max_depth_blender_y_m':float(mx[1]),'depth_m':float(mx[1]-mn[1]),'center_x_m':float((mx[0]+mn[0])/2),'center_depth_m':float((mx[1]+mn[1])/2),'point_count':len(pts)})
bm=bmesh.new();bm.from_mesh(me);volume=bm.calc_volume(signed=True);bm.free()
# The protected insertion surface is compared to frozen R03, within explicit float tolerance.
with bpy.data.libraries.load(str(OUT.parents[1]/'R03/MasterTenon_R03.blend'),link=False) as (x,y):y.objects=['MasterTenon_LOD0']
old=y.objects[0];ov=[v.co.copy() for v in old.data.vertices if v.co.z<=.1050001];nv=[v.co.copy() for v in me.vertices if v.co.z<=.1050001]
tree=KDTree(len(nv))
for i,v in enumerate(nv):tree.insert(v,i)
tree.balance();distances=[float(tree.find(v)[2]) for v in ov]
result={'method':'Triangle-edge/height plane intersection on final game mesh; axes Blender XYZ -> Unity XZY','sections':sections,'volume_m3':volume,'protected_0_105_mm':{'reference':'frozen R03 functional insertion surface, also retained by QingGuiR01','reference_vertices':len(ov),'current_vertices':len(nv),'max_nearest_vertex_difference_m':max(distances),'tolerance_m':5e-7,'pass':max(distances)<5e-7},'grip_height_m':.230,'latch_compatibility':'not inferred from this static measurement'}
assert result['protected_0_105_mm']['pass'],result
(LOG/'measured-interface.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
report=json.loads((OUT/'model-report.json').read_text('utf-8'));report['measured_mesh_section_profile']=sections;report['protected_insertion_surface']=result['protected_0_105_mm'];report['game_mesh_volume_m3']=volume;report['normal_bake_revision']='Hole interior uses independent CORNER radial normals; NormalGL and embedded GLB were re-exported after correction.'
(OUT/'model-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('INTERFACE_MEASURED',result['protected_0_105_mm'],flush=True)
