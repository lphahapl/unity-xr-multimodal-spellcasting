"""Deterministic R02 jade master tenon: real relief high mesh -> Cycles PBR bake."""
import bpy,bmesh,sys,math,json,hashlib,time
from pathlib import Path
import numpy as np
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[4]
OUT=Path(__file__).resolve().parent
LOG=ROOT/'Logs/workshop-master-tenon-r02-20261125/model'
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

def profile(corner,side,width=.12,r=.009):
    # Counterclockwise rounded square; exact maximal section 120 x 120 mm.
    pts=[];a=width/2-r
    centers=[(a,a),(-a,a),(-a,-a),(a,-a)]
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

# Explicit low/high shared ring profile. Shoulder is visual only, not a stop across the larger socket.
PROFILE_Z=np.array([0,.003,.006,.101,.105,.108,.110,.112,.116,.121,.127,.297,.333,.339,.344,.348,.35])
PROFILE_W=np.array([.101,.104,.106,.106,.106,.110,.115,.117,.117,.119,.120,.120,.120,.119,.116,.110,.104])
PROFILE_R=np.array([.003,.003,.003,.003,.003,.004,.005,.006,.006,.008,.009,.009,.009,.009,.010,.011,.012])
def pchip_slopes(xs,ys):
    h=np.diff(xs);delta=np.diff(ys)/h;m=np.zeros(len(xs));m[0]=delta[0];m[-1]=delta[-1]
    for k in range(1,len(xs)-1):
        if delta[k-1]*delta[k]>0:
            w1=2*h[k]+h[k-1];w2=h[k]+2*h[k-1];m[k]=(w1+w2)/(w1/delta[k-1]+w2/delta[k])
    return m
SW=pchip_slopes(PROFILE_Z,PROFILE_W);SR=pchip_slopes(PROFILE_Z,PROFILE_R)
def smooth_value(z,ys,slopes):
    k=max(0,min(len(PROFILE_Z)-2,int(np.searchsorted(PROFILE_Z,z)-1)));h=PROFILE_Z[k+1]-PROFILE_Z[k];t=max(0,min(1,(z-PROFILE_Z[k])/h))
    return float((2*t**3-3*t*t+1)*ys[k]+(t**3-2*t*t+t)*h*slopes[k]+(-2*t**3+3*t*t)*ys[k+1]+(t**3-t*t)*h*slopes[k+1])
def section(z):return smooth_value(z,PROFILE_W,SW),smooth_value(z,PROFILE_R,SR)

def relief(x,y,z,arc=0.):
    width,radius=section(z)
    # Continuous pair of framing cuts and connected geometric cloud/thunder band.
    d=1.;reference_d=1.
    if .305<z<.331:
        center=.318
        d=min(abs(z-.3075),abs(z-.329))
        cell=.030;local=(arc*16%1-.5)*cell
        path=[(-.015,-.0105),(-.011,-.0105),(-.011,.0075),(.011,.0075),(.011,-.0055),(-.005,-.0055),(-.005,.002),(.004,.002),(.004,-.001)]
        for a,b in zip(path,path[1:]):d=min(d,segdist(local,z-center,a,b))
    # Connected reference line plus alternating 5/10 mm marks; kept on the flat face.
    side_x=x if abs(y)>=abs(x) else y
    if max(abs(x),abs(y))>width/2-.0005 and .017<z<.299:
        reference_d=segdist(side_x,z,(-.012,.018),(-.012,.295))
        d=min(d,reference_d)
        for i in range(28):
            h=.020+i*.010
            length=.021 if i%5==0 else (.014 if i%2==0 else .009)
            d=min(d,segdist(side_x,z,(-.012,h),(-.012+length,h)))
    groove=max(.00048*math.exp(-(d/.00067)**2),.00090*math.exp(-(reference_d/.00110)**2))
    edge=max(0,(abs(side_x)-(width/2-.007))/.007)
    wear=.000075*edge*math.exp(-((z-.012)/.018)**2)*(0.5+0.5*math.sin(side_x*710+z*1370))
    return groove+wear,min(1,groove/.00048)

def geometry(name,corner,side,zs,detailed=False):
    ring=profile(corner,side);n=len(ring);verts=[];grooves=[]
    for z in zs:
        width,radius=section(z)
        current=profile(corner,side,width,radius)
        for j,(X,Y) in enumerate(current):
            dep,g=relief(X,Y,z,j/n) if detailed else (0,0)
            # Approximate outward normal of rounded-square perimeter, including corners.
            bound=width/2-radius
            normal=Vector((X-max(-bound,min(bound,X)),Y-max(-bound,min(bound,Y)),0)).normalized()
            if normal.length<.5:normal=Vector((0,math.copysign(1,Y),0)) if abs(Y)>abs(X) else Vector((math.copysign(1,X),0,0))
            X-=dep*normal.x;Y-=dep*normal.y
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
        select([ob])
        tr=ob.modifiers.new('Final triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tr.name)
        normals=[]
        for poly in ob.data.polygons:
            cap=all(abs(ob.data.vertices[vi].co.z-zs[0])<1e-7 for vi in poly.vertices) or all(abs(ob.data.vertices[vi].co.z-zs[-1])<1e-7 for vi in poly.vertices)
            for li in poly.loop_indices:
                vi=ob.data.loops[li].vertex_index;v=ob.data.vertices[vi].co;z=float(v.z)
                if cap:normal=Vector((0,0,1 if z>.17 else -1))
                else:
                    j=vi%n;w,rr=section(z);bb=w/2-rr
                    normal=Vector((v.x-max(-bb,min(bb,v.x)),v.y-max(-bb,min(bb,v.y)),0)).normalized()
                    if normal.length<.5:normal=Vector((0,math.copysign(1,v.y),0)) if abs(v.y)>abs(v.x) else Vector((math.copysign(1,v.x),0,0))
                    za=max(0,z-.00001);zb=min(.35,z+.00001)
                    a=profile(corner,side,*section(za))[j];b=profile(corner,side,*section(zb))[j]
                    normal.z=-(normal.x*(b[0]-a[0])+normal.y*(b[1]-a[1]))/(zb-za)
                    normal.normalize()
                normals.append(tuple(normal))
        ob.data.normals_split_custom_set(normals)
    return ob

lowzs=PROFILE_Z.tolist()
high=geometry('MasterTenon_HIGH',8,80,sorted(set(round(float(z),7) for z in np.r_[np.linspace(0,.35,501),lowzs])),True)
low=geometry('MasterTenon_LOD0',6,1,lowzs)
lod1=geometry('MasterTenon_LOD1',3,1,[0,.006,.101,.105,.110,.116,.127,.333,.344,.35])
lod2=geometry('MasterTenon_LOD2',1,1,[0,.006,.105,.116,.127,.333,.35])

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
# Only a 1.5 mm end inlay; no dark encircling upper hoop.
metal=mathnode('MULTIPLY',mathnode('GREATER_THAN',z,.009),mathnode('LESS_THAN',z,.0133))
# Physical Object coordinates, uniformly scaled in metres. Rotation-independent domain warp:
# generated-coordinate aspect stretching used by R01 is intentionally absent.
coord=tex.outputs['Object']
warp=n.new('ShaderNodeTexNoise');warp.inputs['Scale'].default_value=42;warp.inputs['Detail'].default_value=3.2;warp.inputs['Roughness'].default_value=.7;links.new(coord,warp.inputs['Vector'])
scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=.006;links.new(warp.outputs['Color'],scale.inputs[0])
add=n.new('ShaderNodeVectorMath');add.operation='ADD';links.new(coord,add.inputs[0]);links.new(scale.outputs[0],add.inputs[1])
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=95;noise.inputs['Detail'].default_value=3.2;noise.inputs['Roughness'].default_value=.62;links.new(add.outputs[0],noise.inputs['Vector'])
cloud=n.new('ShaderNodeTexNoise');cloud.inputs['Scale'].default_value=27;cloud.inputs['Detail'].default_value=2;links.new(coord,cloud.inputs['Vector'])
factor=mathnode('ADD',mathnode('MULTIPLY',noise.outputs['Fac'],.6),mathnode('MULTIPLY',cloud.outputs['Fac'],.4))
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.20;ramp.color_ramp.elements[0].color=(.46,.48,.402,1);ramp.color_ramp.elements[1].position=.80;ramp.color_ramp.elements[1].color=(.73,.72,.635,1);links.new(factor,ramp.inputs['Fac'])
fine=n.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=940;fine.inputs['Detail'].default_value=2;links.new(coord,fine.inputs['Vector'])
bronze=n.new('ShaderNodeValToRGB');bronze.color_ramp.elements[0].color=(.035,.045,.027,1);bronze.color_ramp.elements[1].color=(.13,.09,.035,1);links.new(fine.outputs['Fac'],bronze.inputs[0])
mix=n.new('ShaderNodeMixRGB');links.new(metal,mix.inputs[0]);links.new(ramp.outputs['Color'],mix.inputs[1]);links.new(bronze.outputs['Color'],mix.inputs[2])
groove=n.new('ShaderNodeAttribute');groove.attribute_name='Groove'
dirt=n.new('ShaderNodeMixRGB');links.new(mathnode('MULTIPLY',groove.outputs['Fac'],.44),dirt.inputs[0]);links.new(mix.outputs[0],dirt.inputs[1]);dirt.inputs[2].default_value=(.24,.23,.16,1)
base=dirt.outputs[0]
rough=mathnode('ADD',.19,mathnode('ADD',mathnode('MULTIPLY',fine.outputs['Fac'],.075),mathnode('MULTIPLY',metal,.22)))
links.new(base,bs.inputs['Base Color']);links.new(metal,bs.inputs['Metallic']);links.new(rough,bs.inputs['Roughness'])
bs.inputs['IOR'].default_value=1.53;bs.inputs['Subsurface Weight'].default_value=.07;bs.inputs['Subsurface Radius'].default_value=(.009,.014,.011)
high.data.materials.append(mat)
lowmat=bpy.data.materials.new('MasterTenon_PBR');lowmat.use_nodes=True
for o in [low,lod1,lod2]:o.data.materials.append(lowmat)

for o in [lod1,lod2]:o.hide_render=True;o.hide_set(True)
scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.002;scene.render.bake.max_ray_distance=.006;scene.render.bake.margin=16
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
lowmat.diffuse_color=(.64,.63,.54,1)
high.hide_render=True;high.hide_set(True)
bpy.context.view_layer.update()
for ob in [low,lod1,lod2]:
    q=qa_check.run(objects=[ob.name],profile='game',eps=.0002,tol=.0005)
    savejson(LOG/f'{ob.name}.qa.json',q)
    assert q['pass'],q['errors']
    ob.hide_set(True)
low.hide_set(False)
select([low]);bpy.ops.export_scene.fbx(filepath=str(OUT/'MasterTenon_LOD0.fbx'),use_selection=True,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,path_mode='COPY',embed_textures=False)
bpy.ops.export_scene.gltf(filepath=str(OUT/'MasterTenon_R02.glb'),export_format='GLB',use_selection=True,export_tangents=True,export_materials='EXPORT')
select([low,lod1,lod2]);bpy.ops.export_scene.fbx(filepath=str(OUT/'MasterTenon_LODs.fbx'),use_selection=True,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,path_mode='COPY',embed_textures=False)
for o in [lod1,lod2]:o.hide_render=True;o.hide_set(True)
select([low]);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R02.blend'))
report=dict(version='R02',status='aesthetic_review_candidate',blender=bpy.app.version_string,unit='metres',axis='Blender +Z; Unity +Y',origin='insertion end center',envelope_metres=[.12,.12,.35],max_section_metres=.12,shaft_width_metres=.106,shaft_length_metres=.105,shoulder_transition_metres=[.105,.127],shoulder_is_mechanical_stop=False,old_seated_bottom_y=.92394,old_socket_mouth_y=1.028,nominal_socket_metres=.153,per_side_shaft_clearance_metres=.0235,temporary_budget=True,meshes=[mesh_stats(o) for o in [high,low,lod1,lod2]],bake=dict(engine='Cycles',selected_to_active=True,source=high.name,target=low.name,dimensions=[2048,2048],cage_extrusion=.002,max_ray_distance=.006,margin=16,seconds=time.time()-start),limitations=['Opaque runtime PBR approximates jade surface; high-source subsurface scatter is not a custom Quest shader.','Geometry clearance only, actual socket fit validated separately by parent.','No headset or aesthetic acceptance claimed.'])
report['section_profile']=[dict(z_metres=float(z),width_metres=float(w),corner_radius_metres=float(r)) for z,w,r in zip(PROFILE_Z,PROFILE_W,PROFILE_R)]
report['proposed_compound_box_colliders_blender_XYZ']=[dict(name='insert',center=[0,0,.0525],size=[.100,.100,.105]),dict(name='shoulder',center=[0,0,.116],size=[.110,.110,.022]),dict(name='grip',center=[0,0,.231],size=[.112,.112,.208]),dict(name='top',center=[0,0,.341],size=[.102,.102,.018])]
report['collision_note']='Conservative interior boxes for separate Unity draft; parent may instead derive tight convex section hulls. Not an accepted collision design.'
savejson(OUT/'model-report.json',report)
savejson(LOG/'candidate-ready.json',dict(files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [OUT/'MasterTenon_LOD0.fbx',OUT/'MasterTenon_LODs.fbx',OUT/'MasterTenon_R02.glb',OUT/'MasterTenon_R02.blend']},report=report))
print('PREVIEW_BUILD_READY_NOT_FROZEN',flush=True)

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
render('06_TipCalibration',(.22,-.35,.18),(0,0,.105),.23)
key.data.energy=3;fill.data.energy=1;rim.data.energy=18
render('07_SideLightJade',(.28,-.45,.37),(0,0,.280),.22)
render('08_EndInlay',(.20,-.3,.11),(0,0,.020),.13)
key.data.energy=12;fill.data.energy=4;rim.data.energy=12
render('09_LowSameView',(.44,-.64,.43))
low.hide_render=True;high.hide_render=False;high.hide_set(False)
render('10_HighSameView',(.44,-.64,.43))
high.hide_render=True;high.hide_set(True);low.hide_render=False
select([low]);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R02.blend'))
savejson(LOG/'final-file-manifest.json',{p.relative_to(OUT).as_posix():{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in OUT.rglob('*') if p.is_file() and p.suffix!='.blend1'})
print('BUILD_AND_REVIEW_COMPLETE',flush=True)
