"""C4 approved source -> game UVs, real ray thickness, Cycles maps, three LODs and exports."""
import bpy,bmesh,sys,math,json,time,hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[4];LOG=ROOT/'Logs/workshop-master-tenon-qinggui-r01-20261125/model'
TEX=OUT/'Textures';TEX.mkdir(exist_ok=True);CHECK=OUT/'Checks';CHECK.mkdir(exist_ok=True)
sys.path.insert(0,str(OUT));from qinggui_geometry import build_body,ring,section,Z
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
def js(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def select(obs,active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=active or obs[-1]
def stats(ob):
    ob.data.calc_loop_triangles();bpy.context.view_layer.update()
    return {'name':ob.name,'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices),'dimensions_m':list(ob.dimensions),'origin_m':list(ob.location),'materials':len(ob.data.materials),'uv_layers':len(ob.data.uv_layers)}

bpy.ops.wm.open_mainfile(filepath=str(OUT/'QingGuiR01_Source.blend'))
s=bpy.context.scene;high=bpy.data.objects['QingGui_HIGH'];s.render.engine='CYCLES';s.cycles.device='GPU'
pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for d in pref.devices:d.use=d.type=='CUDA'
low=build_body('QingGui_LOD0');low.data.materials.clear()
# Redistribute the side atlas by real section perimeter. Old parameter U is decoded before replacement.
uv=low.data.uv_layers.active
def physical_u(t,z):
    pp,_=ring(z);dist=[0.]
    for k in range(len(pp)):dist.append(dist[-1]+math.dist(pp[k],pp[(k+1)%len(pp)]))
    n=len(pp);t=max(0,min(n,t));i=min(n-1,int(t));f=t-i
    return .5+2*(dist[i]+f*(dist[i+1]-dist[i])-dist[-1]/2)
for poly in low.data.polygons:
    vv=[low.data.vertices[i].co for i in poly.vertices]
    hole=all(abs(((v.x-.041)/.0065)**2+((v.z-.329)/.012)**2-1)<.30 for v in vv) and abs(poly.normal.y)<.85
    if hole:
        aa=[(math.atan2((v.z-.329)/.012,(v.x-.041)/.0065)/(2*math.pi))%1 for v in vv]
        if max(aa)-min(aa)>.5:aa=[a+1 if a<.5 else a for a in aa]
        for li,v,a in zip(poly.loop_indices,vv,aa):uv.data[li].uv=(.70+.23*a,.76+.18*(v.y+.032)/.064)
    else:
        for li in poly.loop_indices:
            old=uv.data[li].uv.copy();v=low.data.vertices[low.data.loops[li].vertex_index].co
            if old.y<.74:uv.data[li].uv=(physical_u((old.x-.02)/.96*28,max(0,min(.35,float(v.z)))),.02+2*v.z)
mat=bpy.data.materials.new('QingGui_GamePBR');mat.use_nodes=True;low.data.materials.append(mat)

# Per-vertex true mesh ray distance. AO is independent and is never used as a substitute.
high.hide_set(False);bpy.context.view_layer.update();tree=BVHTree.FromObject(high,bpy.context.evaluated_depsgraph_get())
thick=np.empty(len(high.data.vertices),np.float32);miss=[];retry=0;start=time.time()
base=high.data.attributes.get('BaseNormal')
for i,v in enumerate(high.data.vertices):
    no=v.normal.normalized();hit=tree.ray_cast(v.co-no*.000015,-no,.6)
    if hit[0] is None and base:
        no=Vector(base.data[i].vector).normalized();hit=tree.ray_cast(v.co-no*.000015,-no,.6);retry+=1
    if hit[0] is None:thick[i]=.12;miss.append(i)
    else:thick[i]=max(0,float(hit[3])+.000015)
    if i%100000==0:print('THICKNESS_RAYS',i,len(thick),flush=True)
a=high.data.attributes.get('Thickness') or high.data.attributes.new('Thickness','FLOAT','POINT');a.data.foreach_set('value',np.clip(thick/.12,0,1))
ray_report={'method':'BVH inward ray from each actual final high vertex, epsilon15um; opposite surface distance + epsilon','normalization_metres':[0,.12],'decode':'thickness_metres = R * 0.12','raw_min_m':float(thick.min()),'raw_max_m':float(thick.max()),'percentiles_m':{str(q):float(np.percentile(thick,q)) for q in [1,5,25,50,75,95,99]},'rays':len(thick),'base_normal_retry_count':retry,'unresolved_miss_count':len(miss),'miss_fallback':'0.12m conservative opaque value, explicitly reported','clamped_above_120mm':int(np.count_nonzero(thick>.12)),'seconds':time.time()-start,'independent_of_AO':True}
js(LOG/'thickness-ray-measurement.json',ray_report)
assert len(miss)<len(thick)*.005,ray_report
print('THICKNESS_COMPLETE',ray_report,flush=True)

src=high.data.materials[0].node_tree;nodes=src.nodes;links=src.links
out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
def source(sock):return bs.inputs[sock].links[0].from_socket
base_socket=source('Base Color');rough_socket=source('Roughness');metal_socket=source('Metallic')
sub=nodes.new('ShaderNodeMath');sub.operation='SUBTRACT';sub.inputs[0].default_value=1;links.new(metal_socket,sub.inputs[1]);jade_socket=sub.outputs[0]
at=nodes.new('ShaderNodeAttribute');at.attribute_name='Thickness'
s.render.bake.use_selected_to_active=True;s.render.bake.cage_extrusion=.003;s.render.bake.max_ray_distance=.012;s.render.bake.margin=16;s.render.bake.normal_space='TANGENT'
images={}
def bake(name,kind,socket=None,size=2048):
    im=bpy.data.images.new('QingGui_'+name,size,size,alpha=False);im.colorspace_settings.name='sRGB' if name=='BaseColor' else 'Non-Color'
    t=mat.node_tree.nodes.new('ShaderNodeTexImage');t.image=im;mat.node_tree.nodes.active=t
    if socket:
        em=nodes.new('ShaderNodeEmission');links.new(socket,em.inputs['Color']);links.new(em.outputs[0],out.inputs['Surface'])
    select([high,low],low);high.hide_render=False;low.hide_render=False;s.cycles.samples=32 if name=='AO' else 16
    bpy.ops.object.bake(type=kind)
    if socket:links.new(bs.outputs[0],out.inputs['Surface']);nodes.remove(em)
    im.filepath_raw=str((CHECK if name=='ProjectionCheck' else TEX)/f'QingGui_{name}.png');im.file_format='PNG';im.save();images[name]=im;print('BAKED',name,flush=True)
start=time.time()
# A small projection inspection is retained as an actual intermediate output.
bake('ProjectionCheck','EMIT',base_socket,512)
bake('BaseColor','EMIT',base_socket);bake('NormalGL','NORMAL');bake('AO','AO');bake('Roughness','EMIT',rough_socket);bake('Metallic','EMIT',metal_socket);bake('Thickness','EMIT',at.outputs['Fac']);bake('JadeMask','EMIT',jade_socket)
def pix(im):
    a=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(a);return a.reshape(-1,4)
def pack(name,channels):
    im=bpy.data.images.new('QingGui_'+name,2048,2048,alpha=True);im.colorspace_settings.name='Non-Color';a=np.ones((2048*2048,4),np.float32)
    for c,v in channels.items():a[:,c]=v
    im.pixels.foreach_set(a.ravel());im.filepath_raw=str(TEX/f'QingGui_{name}.png');im.file_format='PNG';im.save();images[name]=im
ao=pix(images['AO'])[:,0];rough=pix(images['Roughness'])[:,0];metal=pix(images['Metallic'])[:,0]
pack('ORM',{0:ao,1:rough,2:metal});pack('MetallicSmoothness',{0:metal,1:np.zeros_like(metal),2:np.zeros_like(metal),3:1-rough})
mn=mat.node_tree.nodes;ml=mat.node_tree.links;mn.clear();mo=mn.new('ShaderNodeOutputMaterial');mb=mn.new('ShaderNodeBsdfPrincipled');ml.new(mb.outputs[0],mo.inputs[0]);mb.inputs['IOR'].default_value=1.53
for name,slot in [('BaseColor','Base Color'),('Metallic','Metallic'),('Roughness','Roughness')]:
    t=mn.new('ShaderNodeTexImage');t.image=images[name];ml.new(t.outputs[0],mb.inputs[slot])
t=mn.new('ShaderNodeTexImage');t.image=images['NormalGL'];nm=mn.new('ShaderNodeNormalMap');ml.new(t.outputs[0],nm.inputs['Color']);ml.new(nm.outputs[0],mb.inputs['Normal'])
high.hide_render=True;high.hide_set(True)

lods=[low]
for name,target in [('QingGui_LOD1',1800),('QingGui_LOD2',600)]:
    ob=low.copy();ob.data=low.data.copy();ob.name=name;s.collection.objects.link(ob);select([ob]);ob.data.calc_loop_triangles()
    mod=ob.modifiers.new('Controlled LOD reduction','DECIMATE');mod.ratio=target/len(ob.data.loop_triangles);mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    lods.append(ob)
for ob in lods:
    q=qa_check.run(objects=[ob.name],profile='game',eps=.00015,tol=.0002);js(LOG/f'{ob.name}.qa.json',q);assert q['pass'],q['errors']
    ob.hide_render=ob!=low;ob.hide_set(ob!=low)
assert stats(low)['triangles']<=6000
select([low]);bpy.ops.export_scene.fbx(filepath=str(OUT/'QingGui_LOD0.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,path_mode='COPY')
bpy.ops.export_scene.gltf(filepath=str(OUT/'QingGuiR01.glb'),export_format='GLB',use_selection=True,export_tangents=True)
select(lods);bpy.ops.export_scene.fbx(filepath=str(OUT/'QingGui_LODs.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,path_mode='COPY')
for ob in lods[1:]:ob.hide_render=True;ob.hide_set(True)
select([low]);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'QingGuiR01.blend'))
report={'status':'BAKED_EXPORTED_PENDING_COLD_READ_AND_VISUAL_REVIEW','asset':'QingGuiR01','blender':bpy.app.version_string,'unit':'metres','axes':{'up_blender':'+Z','up_unity':'+Y','front':'-Y','right':'+X'},'origin':'bottom insertion center','budget_temporary':True,'meshes':[stats(high)]+[stats(o) for o in lods],'section_profile':[{'z_m':float(z),'left_x_m':section(z)[0],'right_x_m':section(z)[1],'width_x_m':section(z)[1]-section(z)[0],'depth_y_m':section(z)[2],'center_x_m':(section(z)[0]+section(z)[1])/2,'unity_axes':'height Y; width X; depth Z; centerX same'} for z in sorted(set([0,.003,.006,.05,.101]+list(Z)))],'grip_suggestion':{'blender_xyz_m':[-.0015,0,.230],'unity_xyz_m':[-.0015,.230,0],'width_m':.085,'depth_m':.046,'note':'comfort/finger clearance needs actual hand and new collision fit; do not infer latch compatibility'},'thickness':ray_report,'source_geometry':'real cut seats and bronze layer geometry, continuous 4K source masks plus continuous intended relief normals; runtime maps2K','maps':{name:{'path':str(im.filepath_raw),'resolution':list(im.size),'colorspace':im.colorspace_settings.name} for name,im in images.items() if name!='ProjectionCheck'},'bake':{'engine':'Cycles','high_to_low':True,'cage_m':.003,'max_ray_m':.012,'margin_px':16,'seconds':time.time()-start},'uv':'body perimeter in metres U and vertical height V each at 2UV/m; caps/hole separate islands; final triangle normals fixed before bake','limitations':['No formal Assets changed.','New crown/shoulder latch fit not inherited; Unity owner verifies.','Source Cycles SSS is not Unity URP subsurface shading; opaque game material requires supplied Thickness/JadeMask shader integration.','User aesthetic and headset acceptance pending.']}
js(OUT/'model-report.json',report);print('DELIVERY_BUILD_READY',json.dumps([stats(o) for o in lods]),flush=True)
