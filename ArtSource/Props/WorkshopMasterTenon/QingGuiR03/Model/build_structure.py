"""R03 real closed jade core, fitted bronze shell and independent wires.
Read-only geometry definitions from frozen R02B. Metres; +Z; front -Y.
"""
import bpy,bmesh,sys,json,math,hashlib,types
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
import numpy as np
SOURCE=Path(__file__).resolve().parent;P=SOURCE/('RevisionA' if '--revision-a' in sys.argv else '.');ROOT=SOURCE.parents[4];LOG=ROOT/'Logs/workshop-master-tenon-qinggui-r03-20261126/model';LOG=LOG/'revision-a' if '--revision-a' in sys.argv else LOG;PRE=P/'StructurePreview'
P.mkdir(parents=True,exist_ok=True);LOG.mkdir(parents=True,exist_ok=True);PRE.mkdir(exist_ok=True)
OLD=SOURCE.parents[1]/'QingGuiR02/Model/RevisionB'
sys.dont_write_bytecode=True;sys.path.insert(0,str(OLD))
from qinggui_geometry import build_body,section,ring,point
from qinggui_detail import border_distance
sys.path.insert(0,str(SOURCE));from qinggui_wires_r03 import build_wires
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check,review_shots
bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
# Form the solids BEFORE cutting their holes: offsetting a previous Boolean
# fan changed actual face slopes around its long narrow triangles.
geom=types.ModuleType('r03_unperforated');geom.__file__=str(OLD/'qinggui_geometry.py')
source=(OLD/'qinggui_geometry.py').read_text('utf-8').replace('bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)','ob.modifiers.remove(mod);bpy.data.objects.remove(cutter,do_unlink=True)')
exec(compile(source,geom.__file__,'exec'),geom.__dict__)
base=geom.build_body('StructureReference',dense=False);base.data.update()
from rebuild_cap import rebuild
cap_report=rebuild(base);print('CAP_REBUILT',cap_report,flush=True)
# The shaft calibration remains inward-only. No copper is added below105mm.
for v in base.data.vertices:
    x,y,z=v.co
    if .082<z<.104 and abs(y)>.050:
        dist=min(abs(x) if .083<=z<=.103 else 1,math.hypot(max(0,.005-abs(x),abs(x)-.013),z-.0975))
        f=max(0,min(1,(.00085-dist)/.00045));v.co.y-=math.copysign(.00010*f*f*(3-2*f),y)
base.data.update()
coords=[v.co.copy() for v in base.data.vertices];normals=[Vector() for v in coords];counts=[0]*len(coords)
for i,p in enumerate(coords):
    z=max(0,min(.35,float(p.z)));pp,_=ring(z);best=(1e10,0.)
    for j in range(28):
        a=Vector(pp[j]);b=Vector(pp[(j+1)%28]);d=b-a;f=max(0,min(1,(Vector((p.x,p.y))-a).dot(d)/d.length_squared))
        q=point(z,j+f);dist=(Vector((q.x,q.y))-Vector((p.x,p.y))).length_squared
        if dist<best[0]:best=(dist,j+f)
    t=best[1];da=point(z,(t+.0001)%28)-point(z,(t-.0001)%28);dz=point(min(.35,z+.00001),t)-point(max(0,z-.00001),t)
    normals[i]=da.cross(dz).normalized()
    if z<1e-8:normals[i]=Vector((0,0,-1))
    elif z>.3498:normals[i]=normals[i].lerp(Vector((0,0,1)),min(1,(z-.3498)/.0002)).normalized()
for i,p in enumerate(coords):
    th=math.atan2((p.z-.329)/.012,(p.x-.041)/.0065);rim=Vector((.041+.0065*math.cos(th),p.y,.329+.012*math.sin(th)));out=Vector((math.cos(th)/.0065,0,math.sin(th)/.012)).normalized();dd=(p-rim).dot(out)
    if -.00008<dd<.00106 and .315<p.z<.343:
        rr=max(0,min(1,1-dd/.00105));normals[i]=(-out*rr+Vector((0,math.copysign(math.sqrt(max(0,1-rr*rr)),p.y),0))).normalized()
def field(p):return min(border_distance(float(p.x),float(p.z)),float(p.z)-.10530)
def tangent(p,n):
    d=1e-6;g=Vector(((field(p+Vector((d,0,0)))-field(p-Vector((d,0,0))))/(2*d),0,(field(p+Vector((0,0,d)))-field(p-Vector((0,0,d))))/(2*d)))
    return (g-n*g.dot(n)).normalized()

def clip(threshold,positive):
    points={};ns={};faces=[]
    def add(key,p,n):points[key]=p;ns[key]=n
    for poly in base.data.polygons:
        seq=[(('v',int(i)),coords[i],normals[i],field(coords[i])-threshold) for i in poly.vertices];out=[]
        for a,b in zip(seq,seq[1:]+seq[:1]):
            ia=a[3]>=0 if positive else a[3]<=0;ib=b[3]>=0 if positive else b[3]<=0
            if ia:out.append(a[:3])
            if ia!=ib:
                f=a[3]/(a[3]-b[3]);key=('e',*sorted([a[0][1],b[0][1]]));out.append((key,a[1].lerp(b[1],f),a[2].lerp(b[2],f).normalized()))
        clean=[]
        for v in out:
            if not clean or (v[1]-clean[-1][1]).length>1e-10:clean.append(v)
        if len(clean)>2 and (clean[0][1]-clean[-1][1]).length<1e-10:clean.pop()
        if len(clean)>=3:
            keys=[]
            for key,p,n in clean:add(key,p,n);keys.append(key)
            faces.append(tuple(keys))
    edges={}
    for f in faces:
        for a,b in zip(f,f[1:]+f[:1]):edges.setdefault(tuple(sorted([a,b])),[]).append((a,b))
    boundary=[v[0] for v in edges.values() if len(v)==1]
    return points,ns,faces,boundary

def mesh_object(name,verts,faces,custom_normals=None,layers=None):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new(name,me);s.collection.objects.link(ob)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-9);bmesh.ops.triangulate(bm,faces=bm.faces[:])
    for _ in range(5):
        tiny=[p for p in bm.faces if p.calc_area()<1e-10 and min(v.co.z for v in p.verts)>.105]
        if not tiny:break
        bmesh.ops.collapse(bm,edges=list({min(p.edges,key=lambda e:e.calc_length()) for p in tiny}),uvs=True)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(me);bm.free();me.update()
    for f in me.polygons:f.use_smooth=True
    if custom_normals is not None:
        kd=KDTree(len(verts))
        for i,p in enumerate(verts):kd.insert(p,i)
        kd.balance();source_indices=[kd.find(v.co)[1] for v in me.vertices];vn=[custom_normals[i] for i in source_indices]
        a=me.attributes.new('SurfaceNormal','FLOAT_VECTOR','POINT');a.data.foreach_set('vector',np.asarray(vn,np.float32).ravel())
        cn=[tuple(vn[lp.vertex_index]) for lp in me.loops]
        if layers is not None:
            labels=[layers[i] for i in source_indices]
            for poly in me.polygons:
                if len({labels[i] for i in poly.vertices})>1:
                    for li in poly.loop_indices:cn[li]=tuple(poly.normal)
        me.normals_split_custom_set(cn)
    # Temporary usable UVs for structure preview. Replaced/unwrapped before final bake.
    uv=me.uv_layers.new(name='UVMap')
    for f in me.polygons:
        axis=max(range(3),key=lambda i:abs(f.normal[i]));keep=[i for i in range(3) if i!=axis]
        for li in f.loop_indices:
            p=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(.5+2*p[keep[0]],.05+2*p[keep[1]])
    return ob

# Jade is a complete closed solid: visible field plus fitted receiving pocket,
# joined by actual perpendicular shoulder walls (not a flat cap across the grip).
neg=clip(-.00010,False);pos=clip(-.00010,True);v=[];f=[];ids={};jade_normals=[];jade_layers=[]
for tag,patch,depth in [('outer',neg,0.),('pocket',pos,-.00095)]:
    points,ns,faces,edges=patch
    for k,p in points.items():ids[tag,k]=len(v);v.append(tuple(p+ns[k]*depth));jade_normals.append(ns[k]);jade_layers.append(tag)
    for face in faces:f.append(tuple(ids[tag,k] for k in face))
for a,b in neg[3]:f.append((ids['outer',b],ids['outer',a],ids['pocket',a],ids['pocket',b]))
jade=mesh_object('QingGuiR03_JadeCore_LOD0',v,f,jade_normals,jade_layers)

# Bronze is a separate watertight 1.6mm shell. The perimeter has an explicit
# rounded lip, planar termination thickness and an internal face, not color fade.
patch=clip(.00018,True);points,ns,faces,edges=patch;boundary={k for a,b in edges for k in (a,b)}
v=[];f=[];ids={};custom=[];outer=.00080;inner=-.00080;r=.00018
for tag,depth in [('outer',outer),('inner',inner)]:
    for k,p in points.items():ids[tag,k]=len(v);v.append(tuple(p+ns[k]*depth));custom.append(ns[k] if tag=='outer' else -ns[k])
    for face in faces:
        seq=tuple(ids[tag,k] for k in face);f.append(seq if tag=='outer' else seq[::-1])
# Boundary reference is at +0.18mm; arcs extend toward zero so the finished lip
# meets the intended C4 edge. Each boundary edge gets the same layer topology.
layers=['outer']
for q,(angle,depthsign) in enumerate([(math.pi/6,1),(math.pi/3,1),(math.pi/2,1),(math.pi/2,-1),(math.pi/3,-1),(math.pi/6,-1)]):
    tag='lip'+str(q);layers.append(tag)
    for k in boundary:
        n=ns[k];p=points[k];t=tangent(p,n)
        depth=(outer-r+r*math.cos(angle)) if depthsign==1 else (inner+r-r*math.cos(angle))
        ids[tag,k]=len(v);v.append(tuple(p+n*depth-t*(r*math.sin(angle))))
        custom.append((n*(depthsign*math.cos(angle))-t*math.sin(angle)).normalized())
layers.append('inner')
for a,b in edges:
    for ta,tb in zip(layers,layers[1:]):f.append((ids[ta,b],ids[ta,a],ids[tb,a],ids[tb,b]))
bronze=mesh_object('QingGuiR03_BronzeFrame_LOD0',v,f,custom)

def cut_new_aperture(ob,offset,fillet):
    cv=[];cf=[];count=64
    levels=[(-1,0.,-.006)]+[(-1,float(a),fillet*(1-math.cos(a))) for a in np.linspace(0,math.pi/2,7)]+[(0,math.pi/2,0.)]+[(1,float(a),-fillet*(1-math.cos(a))) for a in np.linspace(math.pi/2,0,7)]+[(1,0.,.006)]
    def fy(x,z):
        pp,_=ring(z)
        for j in [6,20]:
            a,b=pp[j],pp[(j+1)%28]
            if a[1]<0:
                t=max(0,min(1,(x-a[0])/(b[0]-a[0])));return point(z,j+t).y-offset
    for side,theta,off in levels:
        extra=fillet*(1-math.sin(theta))
        for k in range(count):
            a=2*math.pi*k/count;x=.041+(.0065+extra)*math.cos(a);z=.329+(.012+extra)*math.sin(a)
            y=0 if side==0 else (fy(x,z) if side<0 else -fy(x,z))+off;cv.append((x,y,z))
    for j in range(len(levels)-1):
        for k in range(count):cf.append((j*count+k,j*count+(k+1)%count,(j+1)*count+(k+1)%count,(j+1)*count+k))
    cf.extend([tuple(range(count-1,-1,-1)),tuple((len(levels)-1)*count+k for k in range(count))])
    cutter=mesh_object('NewContinuousHoleTool',cv,cf)
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Hole after real shell formation','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=1e-8);bmesh.ops.triangulate(bm,faces=bm.faces[:])
    for _ in range(5):
        tiny=[p for p in bm.faces if p.calc_area()<1e-10 and min(v.co.z for v in p.verts)>.105]
        if not tiny:break
        bmesh.ops.collapse(bm,edges=list({min(p.edges,key=lambda e:e.calc_length()) for p in tiny}),uvs=True)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(ob.data);bm.free();ob.data.update()
    for poly in ob.data.polygons:poly.use_smooth=True
    # The cutter controls new hole positions. Shading has separate exterior,
    # bore and inner-skin domains, rather than transferring across shell layers.
    cn=[]
    basis=ob.data.attributes.get('SurfaceNormal')
    for poly in ob.data.polygons:
        for li in poly.loop_indices:
            vi=ob.data.loops[li].vertex_index;p=ob.data.vertices[vi].co;theta=math.atan2((p.z-.329)/.012,(p.x-.041)/.0065)
            rim=Vector((.041+.0065*math.cos(theta),p.y,.329+.012*math.sin(theta)));out=Vector((math.cos(theta)/.0065,0,math.sin(theta)/.012)).normalized();delta=(p-rim).dot(out)
            no=Vector(basis.data[vi].vector).normalized() if basis and Vector(basis.data[vi].vector).length>.3 else ob.data.vertices[vi].normal.copy()
            if -.00008<delta<fillet+.00005 and .315<p.z<.343:
                if poly.normal.dot(-out)>.4:
                    no=-out
                elif no.y*p.y>0:
                    rr=max(0,min(1,1-delta/fillet));no=(-out*rr+no*math.sqrt(max(0,1-rr*rr))).normalized()
            cn.append(tuple(no))
    ob.data.normals_split_custom_set(cn)
# Aperture annuli exist before splitting the solids. No Boolean or fan offset.

def mat(name,color,metal,rough):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1);b=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Base Color'].default_value=(*color,1);b.inputs['Metallic'].default_value=metal;b.inputs['Roughness'].default_value=rough;return m
jm=mat('QingGuiR03_JadeCore',(.76,.745,.67),0,.21);bm=mat('QingGuiR03_BronzeFrame',(.13,.077,.037),1,.36)
jade.data.materials.clear();bronze.data.materials.clear();jade.data.materials.append(jm);bronze.data.materials.append(bm)
for ob in [jade,bronze]:
    for poly in ob.data.polygons:poly.material_index=0
# Project existing approved wire paths onto the assembled external surface.
support=mesh_object('WireSupport', [tuple(x.co) for o in [jade,bronze] for x in o.data.vertices],[])
sv=[];sf=[]
for o in [jade,bronze]:
    start=len(sv);sv.extend(tuple(x.co) for x in o.data.vertices);sf.extend(tuple(start+i for i in p.vertices) for p in o.data.polygons)
bpy.data.objects.remove(support,do_unlink=True);support=mesh_object('WireSupport',sv,sf)
wire,wire_report=build_wires(support,'QingGuiR03_GoldWire_LOD0',0);wire.data.materials[0].name='QingGuiR03_GoldWire'
assert len(wire_report)==18,('All approved wire paths required',[(r['path'],r['samples']) for r in wire_report])
bpy.data.objects.remove(support,do_unlink=True);bpy.data.objects.remove(base,do_unlink=True)
objects=[jade,bronze,wire]
for ob in objects:ob.location=(0,0,0)
top=max(v.co.z for o in objects for v in o.data.vertices);crown_scale=(.35-.345)/(top-.345)
for ob in objects:
    saved=[n.vector.copy() for n in ob.data.corner_normals]
    for v in ob.data.vertices:
        if v.co.z>.345:v.co.z=.345+(v.co.z-.345)*crown_scale
    for lp,n in zip(ob.data.loops,saved):
        if ob.data.vertices[lp.vertex_index].co.z>.345:n.z/=crown_scale;n.normalize()
    ob.data.normals_split_custom_set([tuple(n) for n in saved]);ob.data.update()
bpy.context.view_layer.update()
def stats(ob):
    me=ob.data;me.calc_loop_triangles();b=bmesh.new();b.from_mesh(me);vol=b.calc_volume(signed=True);record={'name':ob.name,'triangles':len(me.loop_triangles),'vertices':len(me.vertices),'dimensions_m':list(ob.dimensions),'boundary_edges':sum(e.is_boundary for e in b.edges),'nonmanifold_edges':sum(not e.is_manifold for e in b.edges),'volume_m3':vol,'material':ob.data.materials[0].name};b.free();return record
records=[stats(o) for o in objects]
report={'status':'STRUCTURE_CANDIDATE_NOT_FINAL_BAKE','asset':'QingGuiR03','axes':{'blender_up':'+Z','front':'-Y','right':'+X','unity_up':'+Y','origin':'bottom insertion center'},'meshes':records,'construction':{'bronze_shell_wall_m':.0016,'exposed_lip_m':.0008,'bronze_edge_round_m':.00018,'jade_receiving_pocket_depth_m':.00095,'hidden_normal_clearance_m':.00015,'visible_joint_nominal_m':.00010,'bronze_lowest_height_m':min(x.co.z for x in bronze.data.vertices)},'UV_status':'Structure candidate temporary planar charts; final atlas pending; old B Thickness must not be used','wire_paths':wire_report,'limitations':['Structure review only; no final maps or jade optics claim.','No new decorative layout; fine motifs temporarily omitted in clay, must preserve at detail stage.']}
(P/'structure-report.json').write_text(json.dumps(report,indent=2),'utf-8')
for o in s.objects:o.select_set(False)
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=jade
bpy.ops.export_scene.fbx(filepath=str(P/'QingGuiR03_StructureCandidate.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',path_mode='COPY')
bpy.ops.export_scene.gltf(filepath=str(P/'QingGuiR03_StructureCandidate.glb'),use_selection=True,export_format='GLB',export_yup=True)
q=qa_check.run(objects=[o.name for o in objects],profile='game',eps=.00001,tol=.00020,max_tris=50000)
(LOG/'structure-qa.json').write_text(json.dumps(q,indent=2),'utf-8');print('STRUCTURE_QA',q['pass'],q['errors'],q['warnings'],flush=True)
print('STRUCTURE_RECORDS',records,flush=True)
s.render.engine='CYCLES';s.cycles.samples=40;s.cycles.use_denoising=True;s.cycles.device='GPU';pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
for device in pref.devices:device.use=device.type=='CUDA'
s.view_settings.view_transform='AgX';s.render.resolution_x=1000;s.render.resolution_y=1200;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Neutral');s.world.use_nodes=True;bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.12,.14,.16,1);bg.inputs[1].default_value=.35
for name,loc,power,size in [('Key',(.35,-.5,.6),15,.5),('Fill',(-.4,-.15,.25),5,.4),('Rim',(.2,.35,.45),14,.35)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.size=size;o=bpy.data.objects.new(name,ld);s.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.19))-o.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('StructureCamera',bpy.data.cameras.new('StructureCamera'));s.collection.objects.link(cam);s.camera=cam;cam.data.type='ORTHO';cam.data.clip_start=.001
def render(name,loc,target=(0,0,.175),scale=.41):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True);print('VIEW_READY',name,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'))
render('01_Assembled45',(.48,-.72,.40))
render('02_CrownTailClose',(.09,.40,.35),(.035,0,.319),.09)
render('03_BackHeld',(.20,.8,.27))
wire.hide_render=True;bronze.location.x=.12;jade.location.x=-.065
render('04_ExplodedClay',(.50,-.80,.40),(.025,0,.175),.52)
bronze.location.x=0;jade.location.x=0
bronze.hide_render=True;render('05_JadeReceivingPocket',(.12,.40,.35),(.035,0,.319),.095)
bronze.hide_render=False;jade.hide_render=True;render('06_BronzeInterior',(-.30,.40,.35),(.025,0,.319),.11)
jade.hide_render=False;wire.hide_render=False
print('STRUCTURE_PREVIEW_COMPLETE',flush=True)
