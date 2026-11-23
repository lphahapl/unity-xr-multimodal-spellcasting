"""Build the modular courtyard infill in headless Blender. Geometry in metres."""
from math import cos, sin, pi
from pathlib import Path
import bpy, numpy as np, json, os
from mathutils import Vector
HERE = Path(__file__).resolve().parent
STAGE = Path(os.environ.get('INFILL_STAGE', 'D:/Temp/CourtyardInfill_20261119'))
STAGE.mkdir(exist_ok=True, parents=True)
NAMES = ()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=6
scene.render.threads_mode='FIXED'
scene.render.threads=12
shades,group_ids,parts={},{},[]
def material(name, lo, hi, metal, smooth, grain=(12, 12, 12), glow=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*hi, 1)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    shader = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = 1 - smooth
    pos = nodes.new("ShaderNodeAttribute"); pos.attribute_name="WorkshopCraftPosition"
    mapping = nodes.new("ShaderNodeVectorMath"); mapping.operation = "MULTIPLY"
    links.new(pos.outputs["Vector"], mapping.inputs[0])
    mapping.inputs[1].default_value = grain
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.5
    noise.inputs["Detail"].default_value = 3.0
    links.new(mapping.outputs[0], noise.inputs["Vector"])
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = .20; ramp.color_ramp.elements[0].color = (*lo,1)
    ramp.color_ramp.elements[1].position = .82; ramp.color_ramp.elements[1].color = (*hi,1)
    links.new(noise.outputs["Fac"], ramp.inputs[0])
    links.new(ramp.outputs[0], shader.inputs["Base Color"])
    bump = nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = .2
    bump.inputs["Distance"].default_value = .00038
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs[0], shader.inputs["Normal"])
    if glow:
        shader.inputs["Emission Color"].default_value = (*hi, 1)
        shader.inputs["Emission Strength"].default_value = .22
    shades[mat] = (shader, ramp, metal, smooth, glow)
    return mat

def active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True); bpy.context.view_layer.objects.active = obj

def bake_mod(obj, mod):
    active(obj); bpy.ops.object.modifier_apply(modifier=mod.name)

def soften(obj, width, segments):
    if not width: return
    m = obj.modifiers.new("Chamfer", "BEVEL")
    m.width = width; m.segments = segments; m.affect = "EDGES"
    bake_mod(obj,m)

def box(name, pos, dims, mat, high, radius=.004, coarse=False, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj=bpy.context.object; obj.name=name
    obj.dimensions=dims
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if rot is not None:
        obj.rotation_mode="QUATERNION"; obj.rotation_quaternion=rot
    if high or coarse: soften(obj, radius, 4 if high else 1)
    obj.data.materials.append(mat); parts.append(obj)
    return obj

def beam(name, a,b,w,d,mat,high, radius=.004,coarse=False):
    a,b=Vector(a),Vector(b); span=b-a
    return box(name,(a+b)*.5,(w,d,span.length),mat,high,radius,coarse,span.to_track_quat("Z","Y"))

def profile(name, rings, ring_mats, segments, plane="dial", centre=(0,0,0)):
    # A solid of revolution. Each ring is (radius, axial coordinate).
    verts=[]; faces=[]; mats=[]; stacks=[]
    for r,h in rings:
        row=[]
        for i in range(1 if r==0 else segments):
            x,z=r*cos(2*pi*i/segments),r*sin(2*pi*i/segments)
            pt=(x,h,z) if plane=="dial" else (x,z,h)
            row.append(len(verts)); verts.append(tuple(Vector(centre)+Vector(pt)))
        stacks.append(row)
    for i,(a,b) in enumerate(zip(stacks,stacks[1:])):
        for j in range(segments):
            k=(j+1)%segments
            ids=[a[j%len(a)],a[k%len(a)],b[k%len(b)],b[j%len(b)]]
            faces.append(list(dict.fromkeys(ids)));mats.append(ring_mats[i])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces)
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    for mat in dict.fromkeys(mats):mesh.materials.append(mat)
    for p,m in zip(mesh.polygons,mats):p.material_index=list(mesh.materials).index(m)
    import bmesh
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
    parts.append(obj);return obj

def join_asset(name, collection):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        host="Gauge plate" if name.startswith("TargetGaugeFace") else obj.get("bake_host",obj.name.split(".")[0])
        group=group_ids.setdefault(host,len(group_ids)+1)
        attribute=obj.data.attributes.new("WorkshopBakePart","INT","FACE")
        for value in attribute.data:value.value=group
        obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name;obj.data.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    coords=obj.data.attributes.new("WorkshopCraftPosition","FLOAT_VECTOR","POINT")
    for v,p in zip(obj.data.vertices,coords.data):p.vector=v.co
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj); parts.clear()
    return obj
import math, bmesh, sys
from mathutils.geometry import tessellate_polygon
MASON=material('Qinghui split stone',(.074,.071,.06),(.35,.342,.288),.015,.24,(6,7,13))
DRESS=material('Quarried edge',(.096,.094,.081),(.37,.355,.301),.016,.34,(16,14,12))
TILE=material('Burnished grey tile',(.03,.036,.04),(.138,.154,.157),.035,.39,(13,17,27))
WOOD=material('Smoked elm',(.030,.014,.007),(.20,.11,.060),.02,.32,(24,22,1.3))
BRONZE=material('Cast bronze',(.078,.064,.033),(.32,.26,.127),.72,.39,(16,17,16))
PAPER=material('Old mica lamp panels',(.24,.153,.056),(.62,.482,.20),.035,.37,(22,18,36))
INK=material('Sooted joints',(.025,.028,.028),(.067,.069,.060),.02,.24)
# Mortar is layered into the high-model colour and height on vertical masonry.
tree=MASON.node_tree; nodes,links=tree.nodes,tree.links
bs,rock,metal,smooth,glow=shades[MASON]
geo=nodes.new('ShaderNodeNewGeometry')
attr=next(n for n in nodes if n.type=='ATTRIBUTE')
pos=nodes.new('ShaderNodeSeparateXYZ'); links.new(attr.outputs['Vector'],pos.inputs[0])
u=nodes.new('ShaderNodeMath');u.operation='ADD';links.new(pos.outputs['X'],u.inputs[0]);links.new(pos.outputs['Y'],u.inputs[1])
coord=nodes.new('ShaderNodeCombineXYZ');links.new(u.outputs[0],coord.inputs['X']);links.new(pos.outputs['Z'],coord.inputs['Y'])
brick=nodes.new('ShaderNodeTexBrick');links.new(coord.outputs[0],brick.inputs['Vector'])
brick.inputs['Scale'].default_value=1;brick.inputs['Mortar Size'].default_value=.005
brick.inputs['Mortar Smooth'].default_value=.8;brick.inputs['Brick Width'].default_value=.47;brick.inputs['Row Height'].default_value=.21
brick.inputs['Mortar'].default_value=(.031,.03,.025,1)
links.new(rock.outputs[0],brick.inputs['Color1']);links.new(rock.outputs[0],brick.inputs['Color2'])
links.new(brick.outputs[0],bs.inputs['Base Color'])
bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.004;bump.inputs['Strength'].default_value=.40
bump.invert=True;links.new(brick.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])
shades[MASON]=(bs,brick,metal,smooth,0)
def solid(name,v,f,mat):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],f);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
    ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob)
    mesh.materials.append(mat);parts.append(ob);return ob
def sweep(name,outline,length,turn,mat,high,rad=0):
    # West-to-east straight or west-to-north, with one shared internal miter ring.
    return swept_surface(name,outline,length,turn,mat,high,rad,0)
def swept_surface(name,outline,length,turn,mat,high,rad,flutes,relief=.02):
    half=length/2
    n=max(1,round(half*flutes)) if turn else max(1,round(length*flutes))
    steps=(6 if high else 3)*n if flutes else 1
    ends=2 if turn else 1
    vs=[];fs=[];w=len(outline)
    for k in range(ends*steps+1):
        outgoing=turn and k>steps
        f=(k-steps if outgoing else k)/steps
        wave=(.5+.5*cos(2*pi*(k/steps*n)))**3 if flutes else 0
        for item in outline:
            t,z=item[:2];amp=item[2] if len(item)>2 else 0
            if turn and outgoing: x,y=(-t,t+(half-t)*f)
            elif turn:x,y=(-half+(half-t)*f,t)
            else:x,y=(-half+length*f,t)
            vs.append((x,y,z+wave*amp*relief))
        if k:
            for j in range(w):
                a=(k-1)*w+j;b=(k-1)*w+(j+1)%w
                fs.append((a,b,b+w,a+w))
    # Concave roof profiles require planar triangulated ends, not fan caps.
    fs.append(tuple(reversed(range(w))));fs.append(tuple(range(len(vs)-w,len(vs))))
    ob=solid(name,vs,fs,mat)
    if high and rad:soften(ob,rad,3)
    return ob
def section_box(name,length,halfwidth,z0,z1,turn,mat,high,rad=.003):
    return sweep(name,[(-halfwidth,z0),(halfwidth,z0),(halfwidth,z1),(-halfwidth,z1)],length,turn,mat,high,rad)
def roof(name,length,r,eave,rise,thick,turn,high,pitch=6):
    def h(t):return eave+rise*(1-abs(t/r))**1.23
    ts=[-r,-.67*r,-.33*r,0,.33*r,.67*r,r]
    pts=[(t,h(t),1) for t in ts]+[(t,h(t)-thick,0) for t in reversed(ts)]
    swept_surface(name,pts,length,turn,TILE,high,0,pitch,.013 if r<1 else .025)
    # A narrow continuous ridge conceals the tile ends on both slopes.
    sweep(name+' crest',[(-.032,h(0)-.004),(.032,h(0)-.004),(.034,h(0)+.017),(.015,h(0)+.038),(-.015,h(0)+.038),(-.034,h(0)+.017)],length-.012,turn,TILE,high,.002)
def wall(high,kind):
    turn=kind=='corner';L=.40 if kind=='end' else 2
    section_box('Plinth',L,.171,0,.14,turn,DRESS,high)
    section_box('Coursed face',L,.150,.139,1.30,turn,MASON,high)
    section_box('Coping stringer',L,.185,1.297,1.36,turn,DRESS,high)
    roof('Coping pantiles',L,.235,1.392,.19,.041,turn,high,7)
    if kind=='end':
        for s in (-1,1):
            box('End pier flange '+str(s),(0,s*.157,.714),(.32,.025,1.16),DRESS,high,.005,True)
def gallery(high,turn):
    L=3
    section_box('Stone bay sole',L,1.47,0,.095,turn,DRESS,high)
    for i,(x,y) in enumerate(((-.78,-1.365),(-.78,1.365),(.78,-1.365),(.78,1.365)) if not turn else ((-.83,-1.365),(-.83,1.365),(1.365,-.81),(1.365,.82))):
        profile(f'Column foot {i}',[(0,.090),(.114,.09),(.120,.12),(.111,.18),(.089,.20),(0,.20)],[DRESS]*5,16 if high else 8,'up',(x,y,0))
        profile(f'Elm column {i}',[(0,.194),(.081,.194),(.076,2.71),(.082,2.79),(0,2.79)],[WOOD]*4,24 if high else 10,'up',(x,y,0))
        box(f'Column capital {i}',(x,y,2.735),(.21,.204,.13),WOOD,high,.005,True)
    for s in (-1,1):
        t=1.355*s
        sweep('Eave plate '+str(s),[(t-.076,2.756),(t+.076,2.756),(t+.076,2.887),(t-.076,2.887)],2.986,turn,WOOD,high,.003)
    for k,x in enumerate((-.8,.8) if not turn else (-.7,)):
        box(f'Tie beam {k}',(x,0,2.785),(.103,2.71,.13),WOOD,high,.006,True)
    if turn:box('Corner tie',(0,.71,2.797),(2.72,.102,.13),WOOD,high,.006,True)
    roof('Grey canopy',3,1.48,2.906,.68,.093,turn,high,5)
def stairs(high):
    n=3;depth=.32;rise=.15;w=3
    outline=[(-.48,0),(.48,0),(.48,n*rise)]
    for k in range(n-1,-1,-1):
        start=-.48+k*depth
        outline.append((start,(k+1)*rise))
        if k:outline.append((start,k*rise))
    ob=sweep('Three stone treads',outline,w,False,DRESS,high)
    if high:soften(ob,.005,3)
    for k in range(3):
        if high:
            y=-.48+k*depth+.051;z=(k+1)*rise
            for x in (-1.19,-.62,0,.62,1.19):
                part=box('Tread chisel', (x,y,z-.0001),(.35,.009,.0015),INK,False)
                part['bake_host']='Three stone treads'
def edge(high,turn):
    sweep('Landing nosing',[(-.16,0),(.16,0),(.16,.45),(-.166,.45),(-.181,.438),(-.181,.39),(-.16,.378)],2,turn,DRESS,high,.003)
def crown(high):
    section_box('Level stone bearing',2.79,.369,0,.064,False,DRESS,high)
    sweep('Header ogee',[(-.38,.055),(.38,.055),(.46,.125),(.483,.157),(-.483,.157),(-.46,.125)],3.03,False,DRESS,high,.004)
    roof('Crown tiles',3.20,.51,.194,.30,.057,False,high,7)
    for s in (-1,1):
        # Gable end plates follow the stone silhouette, catching the ridge in profile.
        beam('Gable finial '+str(s),(s*1.54,0,.49),(s*1.605,0,.562),.041,.055,TILE,high,.003,True)
def lantern(high):
    octa=8 if not high else 16
    def ring(n,levels,mat):
        profile(n,levels,[mat]*(len(levels)-1),octa,'up')
    ring('Stone octagonal foot',[(0,0),(.176,0),(.184,.024),(.172,.049),(.141,.075),(0,.075)],DRESS)
    ring('Stone pedestal',[(0,.073),(.13,.073),(.106,.105),(.092,.183),(.130,.216),(0,.216)],DRESS)
    ring('Bronze lamp sill',[(0,.205),(.143,.205),(.155,.220),(.15,.245),(.115,.255),(0,.255)],BRONZE)
    box('Mica light box',(0,0,.321),(.173,.173,.14),PAPER,high,.002,True)
    for x in (-.09,.09):
        for y in (-.09,.09):box(f'Bronze corner {x} {y}',(x,y,.324),(.018,.018,.158),BRONZE,high,.002)
    for side in (-1,1):
        box('Light guard EW '+str(side),(0,side*.088,.31),(.177,.009,.012),BRONZE,high,.001)
        box('Light guard NS '+str(side),(side*.089,0,.338),(.009,.177,.010),BRONZE,high,.001)
    ring('Stone flared cover',[(0,.391),(.12,.391),(.176,.401),(.171,.418),(.078,.483),(.048,.490),(0,.49)],DRESS)
    ring('Bronze top bud',[(0,.48),(.033,.48),(.037,.506),(.024,.53),(0,.56)],BRONZE)
FAMILIES={
'B01':{'WallStraight':lambda h:wall(h,'straight'),'WallCorner':lambda h:wall(h,'corner'),'WallEnd':lambda h:wall(h,'end')},
'B02':{'GalleryStraight':lambda h:gallery(h,False),'GalleryCorner':lambda h:gallery(h,True)},
'B03':{'StairFlight':stairs,'PlatformEdge':lambda h:edge(h,False),'PlatformCorner':lambda h:edge(h,True)},
'B04':{'CliffGateCrown':crown}, 'B05':{'CanalLantern':lantern}}
key=sys.argv[sys.argv.index('--')+1]
NAMES=tuple(FAMILIES[key])
STAGE=STAGE/key;STAGE.mkdir(exist_ok=True)
lowmat=bpy.data.materials.new(key+'_Infill');lowmat.use_nodes=True
highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    col=bpy.data.collections.new('Source high detail' if high else 'Runtime meshes')
    scene.collection.children.link(col)
    for name,fn in FAMILIES[key].items():
        fn(high);ob=join_asset(name+('_HIGH' if high else ''),col);table[name]=ob
        if not high:
            ob.data.materials.clear();ob.data.materials.append(lowmat)
            for p in ob.data.polygons:p.material_index=0
for ob in highs.values():ob.hide_set(True)
for i,(name,ob) in enumerate(lows.items()):
    active(ob)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.04,island_margin=.011,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    # Separate silhouettes own non-overlapping rectangles of the same atlas.
    weights={'B01':(.40,.42,.18),'B02':(.5,.5),'B03':(.48,.26,.26),'B04':(1,),'B05':(1,)}[key]
    for item in ob.data.uv_layers.active.data:
        u,v=item.uv;item.uv=(sum(weights[:i])+.004+(weights[i]-.008)*u,.007+.986*v)
    bake_mod(ob,ob.modifiers.new('Triangulated bake target','TRIANGULATE'))
exec((HERE/'bake.py').read_text(encoding='utf8'))


