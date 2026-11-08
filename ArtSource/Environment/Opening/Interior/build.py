"""Blender 5.2. Run in a separate process with --background --python build.py.

The high models are made first. The low meshes rebuild the major solids, and
share a unique atlas. Blender coordinates are X across, Z up, -Y towards viewer.
"""
from pathlib import Path
from math import cos, sin, pi
import json
import sys
import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[3]
WORK = PROJECT / "Logs/chapter01-interior/model-work"
WORK.mkdir(parents=True, exist_ok=True)
ASSETS = PROJECT / "Assets/Art/Environment/Opening/Interior"
ASSETS.mkdir(exist_ok=True)
TEXTURES = ASSETS / "Textures"
TEXTURES.mkdir(exist_ok=True)
NAMES = ("TargetGaugeFace", "TargetTrestle", "WorkshopWallLamp")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.render.engine = "CYCLES"
scene.cycles.samples = 6
scene.render.threads_mode = "FIXED"
scene.render.threads = 8
shades = {}; group_ids = {}


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


wood = material("Smoked elm / long fibres", (.038,.016,.008), (.235,.103,.042), .03, .26, (53,57,1.35))
bronze = material("Oxidised copper", (.102,.063,.027), (.385,.219,.086), .60,.39)
bright = material("Burnished brass edges", (.293,.141,.044), (.64,.37,.127), .74,.48)
ivory = material("Chalk inlay", (.46,.38,.25), (.91,.79,.55), .04,.34)
ink = material("Charred rule engraving", (.012,.014,.012), (.049,.043,.029), .04,.27)
red = material("Cinnabar socket", (.235,.019,.008), (.58,.081,.022), .16,.37)
amber = material("Oiled lamp silk", (.52,.23,.077), (.95,.64,.26), .04,.34, (10,10,40),1)
lowmat = bpy.data.materials.new("GaugeWorkshop_Baked"); lowmat.use_nodes = True
parts = []


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


def box(name, pos, dims, mat, high, radius=.004, rot=None, coarse=False):
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
    return box(name,(a+b)*.5,(w,d,span.length),mat,high,radius,span.to_track_quat("Z","Y"),coarse)


def radial_tick(angle, r, width, length, mat):
    # Thin raised/engraved graduations project onto the smooth, single low plate.
    a=(cos(angle)*r,-.0023,sin(angle)*r)
    b=(cos(angle)*(r+length),-.0023,sin(angle)*(r+length))
    return beam("Cut tick",a,b,width,.004,mat,True,.00075)


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


def stud(x,y,z,high,r=.012,host=None):
    if high:
        nail=profile("Hammered nail",[(0,-.0025),(r*.57,-.0024),(r,-.0007),(r+.0004,.0008),(0,.001)],
                     [bright,bright,bronze,bronze],16,"dial",(x,y,z))
        if host:nail["bake_host"]=host


def dial(high):
    if not high:
        return profile("Dial silhouette",[(0,0),(.3015,0),(.3075,.005),(.3075,.050),(.3015,.056),(0,.056)],
                       [lowmat]*5,40)
    rings=[(0,-.004),(.039,-.004),(.045,-.006),(.053,-.006),(.060,-.001),
           (.109,0),(.112,.0015),(.117,.0015),(.121,0),(.163,0),(.165,-.0011),
           (.169,-.0011),(.171,0),(.208,0),(.213,.0017),(.218,.0017),(.223,0),
           (.274,0),(.276,-.001),(.284,-.001),(.287,0),(.290,-.002),(.299,-.002),
           (.306,.002),(.3075,.005),(.3075,.048),(.304,.053),(.299,.056),(0,.056)]
    mats=[red,bronze,bright,ink,ivory,bronze,ink,bronze,ivory,bronze,bright,bronze,ivory,
          bronze,ink,bronze,ivory,bronze,ink,bronze,bright,bronze,bright,bronze,wood,bronze,wood,wood]
    profile("Gauge cut in turned timber",rings,mats,144)
    for i in range(48):
        major=i%4==0; radial_tick(i*2*pi/48,.237 if major else .251,.007 if major else .0038,.034 if major else .019,ink)
    for i in range(8):
        a=(i+.5)*2*pi/8;stud(.296*cos(a),-.002,.296*sin(a),True,.005)


def trestle(high):
    for s in (-1,1):
        box("Splayed shoe",(s*.278,0,.051),(.135,.47,.102),wood,high,.012,coarse=True)
        beam("Sloping upright",(s*.278,.008,.087),(s*.153,.006,1.76),.092,.112,wood,high,.0055,coarse=True)
        beam("Back stay",(s*.278,.186,.085),(s*.215,.052,.85),.057,.061,wood,high,.004)
        box("Foot bronze strap",(s*.278,0,.109),(.137,.075,.030),bronze,high,.003)
        if high:
            for z in (.26,.82,1.52):
                x=s*(.278-.125*(z-.087)/1.673)
                # This almost-vertical beam presents its 92 mm dimension to
                # the muzzle; seat the nail shoulder on that actual face.
                stud(x,-.040,z,True,.016,"Sloping upright")
    box("Tie stretcher",(0,.003,.356),(.581,.080,.082),wood,high,.006,coarse=True)
    box("Head crossrail",(0,-.021,1.56),(.472,.105,.114),wood,high,.007,coarse=True)
    box("Axle yoke",(0,-.076,1.60),(.249,.125,.098),bronze,high,.005,coarse=True)
    if high:
        for s in (-1,1):
            stud(s*.195,-.078,1.57,True,.014,"Head crossrail")
            for i in range(3):
                nick=box("Joinery witness nick",(s*.155+i*.011,-.074,1.53),(.004,.002,.025),ivory,True,.0005)
                nick["bake_host"]="Head crossrail"


def lamp(high):
    box("Wall plate",(0,-.018,0),(.159,.036,.36),bronze,high,.012,coarse=True)
    box("Stepped plate",(0,-.043,-.011),(.106,.020,.252),wood,high,.009)
    beam("Lower corbel",(0,-.040,-.123),(0,-.216,-.027),.053,.052,bronze,high,.004,coarse=True)
    box("Wick arm",(0,-.148,-.023),(.072,.213,.049),bronze,high,.005)
    y=-.236
    if high:
        foot=[(0,-.067),(.088,-.067),(.099,-.060),(.118,-.052),(.136,-.029),(.139,-.020),
              (.132,-.015),(.105,-.005),(0,-.005)]
        shade=[(0,.194),(.071,.194),(.118,.17),(.164,.153),(.157,.142),(.133,.144),(0,.149)]
    else:
        foot=[(0,-.067),(.096,-.063),(.138,-.026),(.135,-.016),(.105,-.005),(0,-.005)]
        shade=[(0,.194),(.072,.194),(.163,.152),(.157,.142),(0,.149)]
    profile("Flared wick bowl",foot,[bronze if i%3 else bright for i in range(len(foot)-1)],72 if high else 16,"lamp",(0,y,0))
    profile("Octagonal eave",shade,[bronze if i!=2 else bright for i in range(len(shade)-1)],8,"lamp",(0,y,0))
    glass=[(0,-.004),(.074,-.004),(.089,.014),(.084,.120),(.065,.148),(0,.148)]
    glass_mats=[amber]*5
    if high:
        # The two binding lips are turned directly into the high chimney.
        # Their highlights project continuously across low-poly facets.
        glass=glass[:3]+[(.0885,.023),(.0898,.025),(.0898,.028),(.0880,.030),
                         (.0858,.083),(.0871,.085),(.0871,.088),(.0854,.090)]+glass[3:]
        glass_mats=[amber,amber,amber,bronze,bright,bronze,amber,bronze,bright,bronze,amber,amber,amber]
    profile("Amber mica chimney",glass,glass_mats,72 if high else 24,"lamp",(0,y,0))
    for i in range(4):
        a=pi*.25+i*pi*.5; x,dy=.104*cos(a),.104*sin(a)
        beam("Shade guard",(x,y+dy,-.012),(x*.95,y+dy*.95,.15),.018,.018,bronze,high,.003)
    if high:
        for z in (-.134,.124):
            for x in (-.049,.049):stud(x,-.036,z,True,.009,"Wall plate")


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


def make_group(high):
    col=bpy.data.collections.new("HIGH | sculpt and hardware" if high else "LOW | game silhouettes")
    scene.collection.children.link(col); result={}
    for name,fn in zip(NAMES,(dial,trestle,lamp)):
        fn(high); obj=join_asset(name+("_HIGH" if high else ""),col); result[name]=obj
        if not high:
            obj.data.materials.clear(); obj.data.materials.append(lowmat)
            for face in obj.data.polygons: face.material_index=0
    return result


highs=make_group(True)
lows=make_group(False)
for obj in highs.values():obj.hide_set(True)
for name,obj in lows.items():
    active(obj)
    if name==NAMES[0]:
        uv=obj.data.uv_layers.new(name="BakeUV")
        for poly in obj.data.polygons:
            pts=[obj.data.vertices[obj.data.loops[i].vertex_index].co for i in poly.loop_indices]
            if all(abs(p.y)<.0001 for p in pts):mode="face"
            elif all(abs(p.y-.056)<.0001 for p in pts):mode="back"
            else:mode="edge"
            from math import atan2
            ang=[(atan2(p.z,p.x)/(2*pi))%1. for p in pts]
            if max(ang)-min(ang)>.5:ang=[a+1 if a<.5 else a for a in ang]
            for li,p,a in zip(poly.loop_indices,pts,ang):
                if mode=="face":coord=(.253+p.x*.754,.755+p.z*.754)
                elif mode=="back":coord=(.092-p.x*.253,.411+p.z*.253)
                else:coord=(.015+a*.474,.497+p.y*.281)
                uv.data[li].uv=coord
    else:
        bpy.ops.object.mode_set(mode="EDIT");bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.02,island_margin=.023,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode="OBJECT")
        rect=(.515,.018,.469,.965) if name==NAMES[1] else (.016,.014,.475,.307)
        for loop in obj.data.uv_layers.active.data:
            loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=obj.modifiers.new("Final game triangles before bake","TRIANGULATE");bake_mod(obj,tri)
    obj.data.calc_loop_triangles()
    print("LOW",name,len(obj.data.loop_triangles),flush=True)

exec((HERE/"bake.py").read_text(encoding="utf-8"))
