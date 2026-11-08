"""Build the editable instrument and its hand retopology in meters.

Blender background: blender -b -t 8 --python-exit-code 1 --python build.py
The stations of the broken axle and each calendar ring are independent parts.
"""
from pathlib import Path
from math import sin, cos, pi, radians, ceil, sqrt
from mathutils import Vector
import bpy
import bmesh
import numpy as np
import json

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[3] / 'Assets/Art/Environment/Opening/BrokenArmillary'
TEX = OUT / 'Textures'
TEX.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 12
scene.render.threads_mode = 'FIXED'
scene.render.threads = 8
scene.world = bpy.data.worlds.new('Observatory light')
scene.world.color = (.25,.25,.25)
def collection(name):
    c = bpy.data.collections.new(name); scene.collection.children.link(c)
    return c
hi = collection('01 | Editable cast components and cut graduations')
lo = collection('02 | Game model - one shared atlas')
lowmat = bpy.data.materials.new('BrokenArmillary')
lowmat.use_nodes = True
materials = {}
def alloy(name, colors, metal, smooth):
    mat = bpy.data.materials.new(name); mat.use_nodes = True
    nt = mat.node_tree; n = nt.nodes; link = nt.links.new
    bs = n.get('Principled BSDF')
    coord = n.new('ShaderNodeAttribute'); coord.attribute_name = 'CastPosition'
    coarse = n.new('ShaderNodeTexNoise'); coarse.inputs['Scale'].default_value = 4.7
    coarse.inputs['Detail'].default_value = 4.
    grain = n.new('ShaderNodeTexNoise'); grain.inputs['Scale'].default_value = 48.
    grain.inputs['Detail'].default_value = 2.
    for node in [coarse, grain]: link(coord.outputs['Vector'], node.inputs['Vector'])
    mix = n.new('ShaderNodeMixRGB'); mix.inputs[0].default_value = .22
    link(coarse.outputs['Fac'], mix.inputs[1]); link(grain.outputs['Fac'], mix.inputs[2])
    ramp = n.new('ShaderNodeValToRGB'); stops = ramp.color_ramp.elements
    for idx, (p,col) in enumerate(colors):
        el = stops[idx] if idx < 2 else stops.new(p)
        el.position = p; el.color = (*col,1)
    link(mix.outputs[0],ramp.inputs[0]); link(ramp.outputs[0],bs.inputs['Base Color'])
    bs.inputs['Metallic'].default_value=metal; bs.inputs['Roughness'].default_value=1-smooth
    mat.diffuse_color = (*colors[-2][1],1)
    materials[mat]=(bs,ramp,metal,smooth)
    return mat
bronze = alloy('Body | russet cast bronze and tide oxidation',[
    (.27,(.040,.080,.064)),(.43,(.123,.072,.033)),(.56,(.285,.166,.078)),(.71,(.205,.114,.052))],.65,.38)
lip = alloy('Worn arris | polished old brass',[
    (.28,(.120,.074,.033)),(.45,(.270,.163,.071)),(.57,(.470,.319,.134)),(.72,(.370,.225,.080))],.70,.53)
dark = alloy('Recess | browned bronze bearing',[
    (.27,(.035,.065,.054)),(.46,(.062,.045,.028)),(.57,(.155,.101,.050)),(.71,(.103,.067,.035))],.57,.31)
cut = alloy('Interrupted metal | dull copper fracture',[
    (.28,(.170,.103,.056)),(.46,(.340,.211,.112)),(.57,(.530,.335,.158)),(.73,(.410,.244,.111))],.56,.28)
groove = alloy('Engraved register | dark oxide',[
    (.25,(.016,.027,.023)),(.45,(.034,.029,.019)),(.60,(.060,.058,.029)),(.73,(.075,.066,.038))],.31,.26)

def active(obj):
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
def join(objs):
    active(objs[0])
    for ob in objs: ob.select_set(True)
    if len(objs)>1: bpy.ops.object.join()
    return bpy.context.object

group_ids = {}
parts = {}
high = False
current = None
uv_offset = 0
def begin(name):
    global current
    current=name
    if name not in group_ids: group_ids[name] = len(group_ids)
def mesh(name, vertices, faces, uvs, shades):
    me=bpy.data.meshes.new(name); me.from_pydata(vertices,[],faces); me.update()
    ob=bpy.data.objects.new(name,me); (hi if high else lo).objects.link(ob)
    if high:
        for mat in dict.fromkeys(shades): me.materials.append(mat)
        for p,m in zip(me.polygons,shades): p.material_index=me.materials.find(m.name)
    else:
        me.materials.append(lowmat); uv=me.uv_layers.new(name='BakedUV')
        for poly, pts in zip(me.polygons,uvs):
            for idx, pt in zip(poly.loop_indices,pts): uv.data[idx].uv=pt
    bm=bmesh.new();bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    for f in me.polygons: f.use_smooth = True
    me.set_sharp_from_angle(angle=radians(37))
    rest=me.attributes.new('CastPosition','FLOAT_VECTOR','POINT')
    rest.data.foreach_set('vector',np.asarray(vertices,dtype=np.float32).ravel())
    ids=me.attributes.new('ProjectionPair','INT','POINT')
    ids.data.foreach_set('value',np.full(len(vertices),group_ids[current],dtype=np.int32))
    parts.setdefault(current,[]).append(ob)
    return ob

def chartbase():
    global uv_offset
    uv_offset+=8.
    return uv_offset

def sweep(name, rows, wrap, close_profile, mats, caps=None, split=14, end_mats=None):
    """Metric strips, split around a revolution. Every chamfer or lip has its own row."""
    n=len(rows[0]); k=len(rows); verts=[v for row in rows for v in row]
    faces=[];uvs=[];shades=[]
    def face(indices, coords, mat):
        faces.append(indices); uvs.append(coords); shades.append(mat)
    for j in range(n if close_profile else n-1):
        jj=(j+1)%n; distance=0
        for i in range(k if wrap else k-1):
            if i%split==0:
                start=chartbase();distance=0
            ii=(i+1)%k
            p,q,r,s = rows[i][j],rows[ii][j],rows[ii][jj],rows[i][jj]
            advance = ((q-p).length+(r-s).length)*.5
            width = ((s-p).length+(r-q).length)*.5
            face([i*n+j,ii*n+j,ii*n+jj,i*n+jj],
                 [(start+distance,0),(start+distance+advance,0),(start+distance+advance,width),(start+distance,width)],mats[j])
            distance+=advance
    def cap(indices,mat):
        vv=[verts[idx] for idx in indices]; c=sum(vv,Vector())/len(vv)
        # Average the full boundary. Three adjacent points on a jagged axle end
        # describe a tooth face, not the cylinder's end plane.
        nrm=sum(((p-c).cross(vv[(i+1)%len(vv)]-c) for i,p in enumerate(vv)),Vector()).normalized()
        u=(vv[0]-c)-nrm*(vv[0]-c).dot(nrm)
        u.normalize();v=nrm.cross(u);offset=chartbase()
        face(indices,[(offset+(p-c).dot(u),(p-c).dot(v)) for p in vv],mat)
    if not wrap:
        cap(list(range(n)),caps[0]);cap(list(range((k-1)*n,k*n)),caps[1])
    if not close_profile:
        cap([i*n for i in range(k)],end_mats[0]);cap([i*n+n-1 for i in range(k)],end_mats[1])
    return mesh(name,verts,faces,uvs,shades)

X=Vector((1,0,0));Y=Vector((0,1,0));Z=Vector((0,0,1))
center=Vector((0,0,1.46)); phi=radians
# Each band has a cast lip, blunt web and aged rim surfaces. Fine ring ticks only exist on the high mesh.
def band(name,C,U,V,r,width,depth,a,b,steps,mat=bronze,broken=False,marks=False):
    begin(name); N=U.cross(V).normalized(); half=width/2; d=depth/2
    if high:
        bevel=min(.007,width*.10,depth*.16); l=min(.013,width*.16)
        profile=[(-half+bevel,-d),(-half+l,-d),(-half+l+.003,-d+.003),(half-l-.003,-d+.003),
                 (half-l,-d),(half-bevel,-d),(half,-d+bevel),(half,d-bevel),
                 (half-bevel,d),(half-l,d),(half-l-.003,d-.003),(-half+l+.003,d-.003),
                 (-half+l,d),(-half+bevel,d),(-half,d-bevel),(-half,-d+bevel)]
        shades=[lip,dark,mat,dark,lip,lip,mat,lip,lip,dark,mat,dark,lip,lip,dark,lip]
    else:
        profile=[(-half,-d),(half,-d),(half,d),(-half,d)]; shades=[mat]*4
    full=abs(b-a-360)<.001; count=steps*(5 if high else 1)
    # The jagged end profile spans the physical band thickness, with a visibly offset right sector.
    def position(theta,dr,dy,edge):
        skew=0.
        if broken and edge:
            skew=edge*(dr/half*.020 + dy/d*.014 + (dr/half)*(dy/d)*.018)
        t=phi(theta)+skew/r
        return C+(U*cos(t)+V*sin(t))*(r+dr)+N*dy
    rows=[[position(a+(b-a)*i/count,dr,dy, -1 if i==0 else (1 if i==count else 0)) for dr,dy in profile] for i in range(count+(not full))]
    sweep(name,rows,full,True,shades,[cut,cut],split=14)
    if not high or not marks:return
    # Shallow dark inlays occupy the same engraved intervals on both faces (front/back close approaches).
    tick=ceil((a+1)/5)*5
    while tick<b-1:
        major=tick%15==0; w=.014 if major else .008
        inner=-half+.016; outer=half-.020 if major else .006
        for side in (-1,1):
            points=[]
            for theta in (phi(tick)-w/(2*r),phi(tick)+w/(2*r)):
                radial=U*cos(theta)+V*sin(theta)
                points.append([C+radial*(r+off)+N*((d+layer)*side) for off,layer in
                    [(inner,-.003),(outer,-.003),(outer,.0023),(inner,.0023)]])
            sweep('Graduation',points,False,True,[groove,lip,groove,lip],[groove,groove])
        tick+=5

def lathe(name,A,B,profile,segments,mat,detail_profile=None,tooth=0.,capm=None,phase=0):
    begin(name); D=(B-A).normalized()
    E=D.cross(Y if abs(D.y)<.9 else X).normalized();F=D.cross(E)
    seq=detail_profile if high and detail_profile else profile
    seg = segments*4 if high else segments
    rows=[]
    for i in range(seg):
        a=2*pi*i/seg+phase
        ring=E*cos(a)+F*sin(a)
        row=[]
        for t,radius,s in seq:
            wear=tooth*(.62*cos(3*a)+.38*sin(2*a)) if t==1 else 0.
            row.append(A+(B-A)*t+D*wear+ring*radius)
        rows.append(row)
    sweep(name,rows,True,False,[m or mat for _,_,m in seq],split=max(4,seg//2),end_mats=capm or (mat,mat))

def bearing(name,A,B,r,seg=12):
    p=[(0,.76*r,lip),(.13,r,bronze),(.81,r,bronze),(1,.80*r,lip)]
    h=[(0,.75*r,lip),(.05,.86*r,lip),(.10,.97*r,lip),(.17,r,dark),(.22,r,bronze),
       (.70,r,dark),(.74,.92*r,dark),(.79,r,lip),(.85,r,lip),(.96,.86*r,lip),(1,.8*r,lip)]
    lathe(name,A,B,p,seg,bronze,h)
def stem(name,x,y,height,rad):
    A=Vector((x,y,0)); B=A+Z*height
    prof=[(0,rad,dark),(.12,rad,lip),(.22,rad*.85,bronze),(.28,rad*.63,lip),
          (.40,rad*.51,bronze),(.82,rad*.40,bronze),(.89,rad*.53,lip),(1,rad*.53,bronze)]
    hp=[(0,rad*.96,dark),(.025,rad,dark),(.09,rad,lip),(.115,rad*.98,lip),(.13,rad*.89,dark),
        (.19,rad*.89,bronze),(.22,rad*.84,lip),(.23,rad*.73,dark),(.25,rad*.73,bronze),
        (.277,rad*.65,lip),(.29,rad*.63,lip),(.33,rad*.53,bronze),(.37,rad*.53,lip),
        (.40,rad*.50,bronze),(.79,rad*.40,bronze),(.82,rad*.40,lip),(.85,rad*.46,dark),
        (.89,rad*.54,lip),(.92,rad*.54,bronze),(.95,rad*.51,lip),(1,rad*.51,bronze)]
    lathe(name,A,B,prof,8,bronze,hp,phase=pi/8)

def instrument():
    band('01 Cast meridian, broken',center,X,Z,1.205,.146,.116,78,379,39,broken=True,marks=True)
    offset=center+Vector((.033,-.008,.050))
    band('02 Detached meridian sector',offset,X,Z,1.205,.146,.116,32,62,5,broken=True,marks=True)
    # Positive height and the offset bearing keep the fallen sector legible from the approach.
    U=Vector((.52,0,-.8542)).normalized(); V=Vector((.8542*.41,-sqrt(1-.41**2),.52*.41)).normalized()
    band('03 Inclined equinoctial circle',center,U,V,1.035,.085,.071,0,360,36,marks=True)
    U=Vector((.87,0,.493)).normalized();V=Vector((-.493*.29,sqrt(1-.29**2),.87*.29)).normalized()
    band('04 Horizon bow',center+Vector((0,.045,0)),U,V,.968,.059,.054,112,402,24,dark)
    stem('05 West bearing standard',-1.025,0,.94,.225)
    stem('06 East saddle',.88,0,.58,.22)
    stem('07 Lower meridian shoe',0,0,.27,.26)
    # The bronze pin changes axis, leaves a clear air gap, then meets an offset sleeve.
    A=Vector((-1.09,-.075,.99));B=Vector((.24,-.075,1.57))
    C=Vector((.61,-.075,1.89));D=Vector((1.041,-.075,2.075))
    for name,start,end,r in [('08 Survey axle, retained',A,B,.063),('09 Survey axle, displaced',D,C,.066)]:
        pp=[(0,r*1.09,lip),(.08,r*1.09,bronze),(.15,r,bronze),(.91,r,dark),(1,r*.86,cut)]
        hh=[(0,r*1.04,lip),(.015,r*1.09,lip),(.069,r*1.09,dark),(.082,r*.99,lip),
            (.111,r*.99,lip),(.122,r*.95,bronze),(.88,r*.94,dark),(.91,r,dark),(.98,r*.85,cut),(1,r*.85,cut)]
        lathe(name,start,end,pp,10,bronze,hh,tooth=.032,capm=(bronze,cut))
    axis=(B-A).normalized()
    for name,mid,r,width in [('10 Left trunnion',A+axis*.012,.167,.21),('11 Center measuring boss',A+axis*1.12,.14,.16),
                             ('12 Displaced socket',D-axis*.057,.113,.13)]:
        bearing(name,mid-axis*width/2,mid+axis*width/2,r)
    hub=Vector((-.055,-.02,1.438))
    bearing('13 Engraved pin head',hub+Y*.11,hub-Y*.16,.122,12)

for high in (True,False):
    parts={}; uv_offset=0; instrument()
    if high:
        high_objects=[join(objects) for objects in parts.values()]
        for ob,name in zip(high_objects,parts):ob.name=name
    else:
        low=join([o for pieces in parts.values() for o in pieces])
        low.name='BrokenArmillary'; low.data.name='BrokenArmillary'
        active(low);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.average_islands_scale()
        bpy.ops.uv.pack_islands(rotate=True,margin=.0036,margin_method='FRACTION',shape_method='AABB')
        bpy.ops.object.mode_set(mode='OBJECT')
        mod=low.modifiers.new('Freeze topology before tangent bake','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
        low.data.calc_loop_triangles()
        print('GAME TRIANGLES',len(low.data.loop_triangles),flush=True)
        assert 900<=len(low.data.loop_triangles)<=2200
exec(compile((HERE/'bake.py').read_text(encoding='utf8'),str(HERE/'bake.py'),'exec'))
