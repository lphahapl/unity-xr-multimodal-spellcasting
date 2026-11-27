"""Deterministic R01 jade master tenon: real relief high mesh -> Cycles PBR bake."""
import bpy,bmesh,sys,math,json,hashlib,time
from pathlib import Path
import numpy as np
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[4]
OUT=Path(__file__).resolve().parent
LOG=ROOT/'Logs/workshop-master-tenon-r01-20261125/model'
TEX=OUT/'Textures';PRE=OUT/'Previews'
for p in [LOG,TEX,PRE]:p.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts')
import qa_check,review_shots

def savejson(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
def select(obs,active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=active or obs[-1]
def mesh_stats(o):
    o.data.calc_loop_triangles()
    return dict(name=o.name,verts=len(o.data.vertices),triangles=len(o.data.loop_triangles),dimensions=list(o.dimensions),materials=len(o.data.materials),uv_layers=len(o.data.uv_layers))

scene=bpy.context.scene
# Headless factory-startup only; no live session is touched.
for o in list(bpy.data.objects):bpy.data.objects.remove(o,do_unlink=True)
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.engine='CYCLES';scene.cycles.samples=32
try:
    pref=bpy.context.preferences.addons['cycles'].preferences
    pref.compute_device_type='CUDA';pref.get_devices()
    for d in pref.devices:d.use=d.type=='CUDA'
    scene.cycles.device='GPU'
except Exception:scene.cycles.device='CPU'
scene.view_settings.view_transform='AgX'

def profile(corner,side):
    # Counterclockwise rounded square; exact maximal section 120 x 120 mm.
    pts=[];r=.003
    centers=[(.057,.057),(-.057,.057),(-.057,-.057),(.057,-.057)]
    for k,(cx,cy) in enumerate(centers):
        for j in range(corner+1):
            a=(k*90+j*90/corner)*math.pi/180
            pts.append((cx+r*math.cos(a),cy+r*math.sin(a)))
        end=np.array(pts[-1]);nc=centers[(k+1)%4];a=(k+1)*math.pi/2
        start=np.array([nc[0]+r*math.cos(a),nc[1]+r*math.sin(a)])
        for j in range(1,side):pts.append(tuple(end+(start-end)*j/side))
    return pts

def segdist(x,z,a,b):
    vx,vz=b[0]-a[0],b[1]-a[1]
    t=max(0,min(1,((x-a[0])*vx+(z-a[1])*vz)/(vx*vx+vz*vz)))
    return math.hypot(x-a[0]-t*vx,z-a[1]-t*vz)

def relief(x,y,z):
    # Tool-cut calibration ticks at insertion end, restrained double arch on grip.
    s=x if abs(y)>=abs(x) else y
    if max(abs(x),abs(y))<.0596:return 0.,0.
    d=1.
    if .027<z<.095:
        for i,h in enumerate([.038,.048,.058,.068,.078]):
            d=min(d,segdist(s,z,(-.035,h),(-.010 if i%2==0 else -.019,h)))
        d=min(d,segdist(s,z,(-.035,.033),(-.035,.085)))
    if .190<z<.295:
        # Restrained square cloud/thunder meanders. No writing or pseudo-characters.
        pattern=[(-.016,-.014),(-.016,.014),(.016,.014),(.016,-.009),(-.009,-.009),(-.009,.006),(.007,.006),(.007,-.001),(-.001,-.001)]
        for center,mirror in [(.224,1),(.268,-1)]:
            pts=[(mirror*x,center+zz) for x,zz in pattern]
            for a,b in zip(pts,pts[1:]):d=min(d,segdist(s,z,a,b))
    groove=.00058*math.exp(-(d/.0008)**2)
    # Limited abrasion around insertion edges, not random damage across the jade.
    edge=max(0,(abs(s)-.049)/.011)
    wear=.00010*edge*math.exp(-((z-.018)/.021)**2)*(0.5+0.5*math.sin(s*700+z*1300))
    return groove+wear,min(1,groove/.00058)

def geometry(name,corner,side,zs,detailed=False):
    ring=profile(corner,side);n=len(ring);verts=[];grooves=[]
    for z in zs:
        # Entry chamfer and softened top are inside the insertion envelope.
        inset=.002*(1-min(z/.006,1))+.0018*max(0,(z-.347)/.003)
        for x,y in ring:
            X=x*(1-inset/.06);Y=y*(1-inset/.06)
            dep,g=relief(X,Y,z) if detailed else (0,0)
            if abs(X)>abs(Y):X-=math.copysign(dep,X)
            else:Y-=math.copysign(dep,Y)
            verts.append((X,Y,z));grooves.append(g)
    faces=[];uvs=[]
    distances=[0.]
    for j in range(n):distances.append(distances[-1]+math.dist(ring[j],ring[(j+1)%n]))
    total=distances[-1]
    for iz in range(len(zs)-1):
        for j in range(n):
            q=(j+1)%n
            faces.append((iz*n+j,iz*n+q,(iz+1)*n+q,(iz+1)*n+j))
            u0=.02+.96*distances[j]/total;u1=.02+.96*distances[j+1]/total
            v0=.02+.70*zs[iz]/.35;v1=.02+.70*zs[iz+1]/.35
            uvs.append([(u0,v0),(u1,v0),(u1,v1),(u0,v1)])
    # Top and bottom triangular fan caps with distinct UV islands.
    for iz,top in [(0,False),(len(zs)-1,True)]:
        c=len(verts);verts.append((0,0,zs[iz]));grooves.append(0)
        cx=.19 if not top else .50;cy=.86
        for j in range(n):
            q=(j+1)%n;inds=(c,iz*n+j,iz*n+q) if top else(c,iz*n+q,iz*n+j)
            faces.append(inds)
            uvs.append([(cx+verts[k][0]*1.75,cy+verts[k][1]*1.75) for k in inds])
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    uv=me.uv_layers.new(name='UVMap')
    for p,coords in zip(me.polygons,uvs):
        for li,co in zip(p.loop_indices,coords):uv.data[li].uv=co
        p.use_smooth=len(p.vertices)==4
    attr=me.attributes.new('Groove','FLOAT','POINT')
    attr.data.foreach_set('value',grooves)
    ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob)
    if not detailed:
        select([ob]);m=ob.modifiers.new('Area weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True;m.weight=40
        bpy.ops.object.modifier_apply(modifier=m.name)
        tr=ob.modifiers.new('Final triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tr.name)
    return ob

lowzs=[0,.002,.006,.014,.020,.027,.029,.292,.300,.306,.310,.312,.344,.347,.35]
high=geometry('MasterTenon_HIGH',8,96,sorted(set(round(float(z),7) for z in np.r_[np.linspace(0,.35,401),lowzs])),True)
low=geometry('MasterTenon_LOD0',5,1,lowzs)
lod1=geometry('MasterTenon_LOD1',3,1,[0,.006,.020,.029,.300,.312,.347,.35])
lod2=geometry('MasterTenon_LOD2',1,1,[0,.006,.029,.300,.347,.35])

# One shared PBR atlas: source materials represented by procedural masks.
mat=bpy.data.materials.new('Source_Jade_OldBronze');mat.use_nodes=True
nt=mat.node_tree;n=nt.nodes;links=nt.links;n.clear()
out=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');links.new(bs.outputs['BSDF'],out.inputs['Surface'])
tex=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');links.new(tex.outputs['Generated'],sep.inputs[0])
def mathnode(op,a,b=None):
    m=n.new('ShaderNodeMath');m.operation=op
    for i,v in enumerate([a,b]):
        if v is None:continue
        if isinstance(v,(float,int)):m.inputs[i].default_value=v
        else:links.new(v,m.inputs[i])
    return m.outputs[0]
z=sep.outputs['Z']
band0=mathnode('MULTIPLY',mathnode('GREATER_THAN',z,.040),mathnode('LESS_THAN',z,.080))
band1=mathnode('MULTIPLY',mathnode('GREATER_THAN',z,.860),mathnode('LESS_THAN',z,.890))
metal=mathnode('MAXIMUM',band0,band1)
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=6.5;noise.inputs['Detail'].default_value=4;noise.inputs['Roughness'].default_value=.62;links.new(tex.outputs['Generated'],noise.inputs['Vector'])
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.23;ramp.color_ramp.elements[0].color=(.26,.43,.38,1);ramp.color_ramp.elements[1].position=.80;ramp.color_ramp.elements[1].color=(.82,.85,.72,1);links.new(noise.outputs['Fac'],ramp.inputs['Fac'])
fine=n.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=160;fine.inputs['Detail'].default_value=2;links.new(tex.outputs['Generated'],fine.inputs['Vector'])
bronze=n.new('ShaderNodeValToRGB');bronze.color_ramp.elements[0].position=.18;bronze.color_ramp.elements[0].color=(.025,.050,.035,1);bronze.color_ramp.elements[1].position=.82;bronze.color_ramp.elements[1].color=(.16,.12,.058,1);links.new(fine.outputs['Fac'],bronze.inputs[0])
mix=n.new('ShaderNodeMixRGB');links.new(metal,mix.inputs[0]);links.new(ramp.outputs['Color'],mix.inputs[1]);links.new(bronze.outputs['Color'],mix.inputs[2])
groove=n.new('ShaderNodeAttribute');groove.attribute_name='Groove'
dirt=n.new('ShaderNodeMixRGB');links.new(mathnode('MULTIPLY',groove.outputs['Fac'],.43),dirt.inputs[0]);links.new(mix.outputs[0],dirt.inputs[1]);dirt.inputs[2].default_value=(.19,.29,.23,1)
base=dirt.outputs[0]
rough=mathnode('ADD',.23,mathnode('ADD',mathnode('MULTIPLY',fine.outputs['Fac'],.13),mathnode('MULTIPLY',metal,.13)))
links.new(base,bs.inputs['Base Color']);links.new(metal,bs.inputs['Metallic']);links.new(rough,bs.inputs['Roughness'])
bs.inputs['IOR'].default_value=1.53;bs.inputs['Subsurface Weight'].default_value=.055;bs.inputs['Subsurface Radius'].default_value=(.009,.014,.011)
high.data.materials.append(mat)
lowmat=bpy.data.materials.new('MasterTenon_PBR');lowmat.use_nodes=True
for o in [low,lod1,lod2]:o.data.materials.append(lowmat)

for o in [lod1,lod2]:o.hide_render=True;o.hide_set(True)
scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.003;scene.render.bake.max_ray_distance=.008;scene.render.bake.margin=16
scene.cycles.bake_type='NORMAL';scene.render.bake.normal_space='TANGENT'
images={}
def bake(key,kind,socket=None,size=2048):
    im=bpy.data.images.new('Tenon_'+key,width=size,height=size,alpha=False,float_buffer=False)
    im.colorspace_settings.name='sRGB' if key=='BaseColor' else 'Non-Color'
    ln=lowmat.node_tree.nodes.new('ShaderNodeTexImage');ln.image=im;lowmat.node_tree.nodes.active=ln
    prev=None
    if socket is not None:
        em=n.new('ShaderNodeEmission');links.new(socket,em.inputs['Color']);links.new(em.outputs[0],out.inputs['Surface'])
    select([high,low],low);high.hide_render=False;low.hide_render=False
    scene.cycles.samples=16 if key!='AO' else 32
    bpy.ops.object.bake(type=kind)
    if socket is not None:links.new(bs.outputs['BSDF'],out.inputs['Surface']);n.remove(em)
    im.filepath_raw=str(TEX/f'MasterTenon_{key}.png');im.file_format='PNG';im.save();images[key]=im
    print('BAKED',key,flush=True)
    return im

start=time.time()
bake('BaseColor','EMIT',base)
bake('Normal','NORMAL')
bake('AO','AO')
bake('Roughness','EMIT',rough)
bake('Metallic','EMIT',metal)

def pixels(im):
    a=np.empty(im.size[0]*im.size[1]*4,dtype=np.float32);im.pixels.foreach_get(a);return a.reshape(-1,4)
def packed(key,channels):
    im=bpy.data.images.new('Tenon_'+key,2048,2048,alpha=True);im.colorspace_settings.name='Non-Color'
    a=np.ones((2048*2048,4),np.float32)
    for i,v in channels.items():a[:,i]=v
    im.pixels.foreach_set(a.ravel());im.filepath_raw=str(TEX/f'MasterTenon_{key}.png');im.file_format='PNG';im.save();images[key]=im
ao=pixels(images['AO'])[:,0];rougha=pixels(images['Roughness'])[:,0];metala=pixels(images['Metallic'])[:,0]
packed('ORM',{0:ao,1:rougha,2:metala});packed('MetallicSmoothness',{0:metala,1:np.zeros_like(metala),2:np.zeros_like(metala),3:1-rougha})

ln=lowmat.node_tree.nodes;ll=lowmat.node_tree.links;ln.clear()
lout=ln.new('ShaderNodeOutputMaterial');lbs=ln.new('ShaderNodeBsdfPrincipled');ll.new(lbs.outputs[0],lout.inputs['Surface'])
for key,sock in [('BaseColor','Base Color'),('Roughness','Roughness'),('Metallic','Metallic')]:
    t=ln.new('ShaderNodeTexImage');t.image=images[key];ll.new(t.outputs['Color'],lbs.inputs[sock])
t=ln.new('ShaderNodeTexImage');t.image=images['Normal'];nm=ln.new('ShaderNodeNormalMap');ll.new(t.outputs['Color'],nm.inputs['Color']);ll.new(nm.outputs['Normal'],lbs.inputs['Normal'])
lbs.inputs['IOR'].default_value=1.53
lowmat.diffuse_color=(.55,.69,.57,1)
high.hide_render=True;high.hide_set(True)
bpy.context.view_layer.update()
for ob in [low,lod1,lod2]:
    q=qa_check.run(objects=[ob.name],profile='game',eps=.0002,tol=.0005)
    savejson(LOG/f'{ob.name}.qa.json',q)
    assert q['pass'],q['errors']
    ob.hide_set(True)
low.hide_set(False)
select([low]);bpy.ops.export_scene.fbx(filepath=str(OUT/'MasterTenon_LOD0.fbx'),use_selection=True,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,path_mode='COPY',embed_textures=False)
bpy.ops.export_scene.gltf(filepath=str(OUT/'MasterTenon_R01.glb'),export_format='GLB',use_selection=True,export_tangents=True,export_materials='EXPORT')
select([low,lod1,lod2]);bpy.ops.export_scene.fbx(filepath=str(OUT/'MasterTenon_LODs.fbx'),use_selection=True,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,path_mode='COPY',embed_textures=False)
for o in [lod1,lod2]:o.hide_render=True;o.hide_set(True)
select([low]);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R01.blend'))
report=dict(version='R01',status='aesthetic_review_candidate',blender=bpy.app.version_string,unit='metres',axis='Blender +Z; Unity +Y',origin='insertion end center',envelope_metres=[.12,.12,.35],max_section_metres=.12,nominal_socket_metres=.146,per_side_clearance_metres=.013,temporary_budget=True,meshes=[mesh_stats(o) for o in [high,low,lod1,lod2]],bake=dict(engine='Cycles',selected_to_active=True,source=high.name,target=low.name,dimensions=[2048,2048],cage_extrusion=.003,max_ray_distance=.008,margin=16,seconds=time.time()-start),limitations=['Opaque runtime PBR approximates jade surface; high-source subsurface scatter is not a custom Quest shader.','Geometry clearance only, actual socket fit validated separately by parent.','No headset or aesthetic acceptance claimed.'])
savejson(OUT/'model-report.json',report)
savejson(LOG/'candidate-ready.json',dict(files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [OUT/'MasterTenon_LOD0.fbx',OUT/'MasterTenon_LODs.fbx',OUT/'MasterTenon_R01.glb',OUT/'MasterTenon_R01.blend']},report=report))
print('CANDIDATE_READY',flush=True)

# Multi-angle contact sheets supplied by mandatory QA skill.
review_shots.run(objects=[low.name],out=str(PRE/'QA_material.png'),views=['front','right','back','three_quarter'],mode='material',res=512)
review_shots.run(objects=[low.name],out=str(PRE/'QA_wire.png'),views=['front','right','back','three_quarter'],mode='wire',res=512)

# Actual PBR product views in neutral lighting, same camera scale for high/low pair.
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world.use_nodes=True;wn=scene.world.node_tree.nodes;bg=next(q for q in wn if q.type=='BACKGROUND');bg.inputs['Color'].default_value=(.11,.14,.16,1);bg.inputs['Strength'].default_value=.45
def lamp(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.18))-o.location).to_track_quat('-Z','Y').to_euler();return o
key=lamp('Soft_Key',(.4,-.45,.65),12,.45,(1,.97,.94));fill=lamp('Cool_Fill',(-.35,-.15,.3),4,.35,(.77,.88,1));rim=lamp('Edge_Rim',(.2,.35,.45),12,.30,(.85,1,.94))
cd=bpy.data.cameras.new('Review_Camera');cam=bpy.data.objects.new('Review_Camera',cd);scene.collection.objects.link(cam);scene.camera=cam;cd.type='ORTHO';cd.ortho_scale=.46;cd.clip_start=.001
def render(name,loc,target=(0,0,.175),scale=.46):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cd.ortho_scale=scale;scene.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
low.hide_render=False
render('01_Front45',(.44,-.64,.43))
render('02_Back45',(-.44,.64,.40))
render('03_Front',(0,-.75,.175))
render('04_Right',(.75,0,.175))
render('05_Back',(0,.75,.175))
render('06_TipCalibration',(.22,-.35,.13),(0,0,.063),.185)
key.data.energy=3;fill.data.energy=1;rim.data.energy=18
render('07_SideLightJade',(.28,-.45,.37),(0,0,.245),.25)
render('08_BronzeWear',(.20,-.3,.11),(0,0,.027),.12)
key.data.energy=12;fill.data.energy=4;rim.data.energy=12
render('09_LowSameView',(.44,-.64,.43))
low.hide_render=True;high.hide_render=False;high.hide_set(False)
render('10_HighSameView',(.44,-.64,.43))
high.hide_render=True;high.hide_set(True);low.hide_render=False
select([low]);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R01.blend'))
savejson(LOG/'final-file-manifest.json',{p.relative_to(OUT).as_posix():{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in OUT.rglob('*') if p.is_file() and p.suffix!='.blend1'})
print('BUILD_AND_REVIEW_COMPLETE',flush=True)
