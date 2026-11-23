"""Build the five A1 castings in metres using Blender 5.2.
Run in a clean background Blender. The design plane uses Unity X/Z and elevation.
Run atlas.py between the geometry pass and the final pass (-- finish).
"""
from pathlib import Path
import math
import sys
import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

SRC = Path(__file__).resolve().parent
ROOT = SRC.parents[4]
DEST = ROOT / "Assets/Art/Environment/Opening/ConcentricGauge/Bronze"
CACHE = SRC / ".work"
CACHE.mkdir(exist_ok=True)
DEST.mkdir(parents=True, exist_ok=True)
NAMES = ["Bed", "Outer", "Inner", "Box", "Lid"]
HEIGHT = [.99, .99, 1.008, .9, 1.036]
TAU = math.tau
CAST, RUB, DARK, FIELD, LINING, BAND, ENGRAVE = range(7)
finish = "finish" in sys.argv
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1
bpy.context.preferences.filepaths.save_version = 0

class Casting:
    def __init__(self, name):
        self.name = name
        self.verts, self.faces, self.finishes = [], [], []
        self.parts = {}

    def face(self, indices, style):
        self.faces.append(indices)
        self.finishes.append(style)

    def loft(self, name, rings, styles, closed=False, caps=(CAST, CAST)):
        start = len(self.verts)
        n = len(rings[0])
        for ring in rings:
            assert len(ring) == n
            self.verts.extend(ring)
        for j, style in enumerate(styles):
            for i in range(n):
                q = (i+1) % n
                a, b = start+j*n, start+((j+1)%len(rings))*n
                self.face([a+i, a+q, b+q, b+i], style(i) if callable(style) else style)
        if not closed:
            for j, style in [(0,caps[0]),(len(rings)-1,caps[1])]:
                center=len(self.verts)
                self.verts.append(tuple(np.mean(rings[j],axis=0)))
                for i in range(n):
                    a=start+j*n+i
                    b=start+j*n+(i+1)%n
                    self.face([center,b,a] if j==0 else [center,a,b],style)

        self.parts[name] = (start, len(self.verts))

    def object(self):
        me=bpy.data.meshes.new(self.name)
        me.from_pydata(self.verts,[],self.faces)
        me.update()
        attr=me.attributes.new("CastFinish","INT","FACE")
        attr.data.foreach_set("value",np.asarray(self.finishes,dtype=np.int32))
        bm=bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(me)
        bm.free()
        ob=bpy.data.objects.new(self.name,me)
        scene.collection.objects.link(ob)
        for key,(a,b) in self.parts.items():
            ob.vertex_groups.new(name=key).add(list(range(a,b)),1,"REPLACE")
        for poly in me.polygons:
            poly.use_smooth=True
        me.set_sharp_from_angle(angle=math.radians(35))
        return ob

def circle(r,z,n=96,c=(0,0)):
    return [(c[0]+r*math.cos(i*TAU/n),c[1]+r*math.sin(i*TAU/n),z) for i in range(n)]

def lathe(obj,name,profile,styles,n=96,c=(0,0),closed=False,caps=(CAST,CAST)):
    obj.loft(name,[circle(r,z,n,c) for r,z in profile],styles,closed,caps)

def rounded(w,d,r,z,c=(0,0),n=3):
    ring=[]
    for corner,(sx,sy) in enumerate([(1,1),(-1,1),(-1,-1),(1,-1)]):
        for j in range(n+1):
            a=(corner+j/n)*math.pi/2
            ring.append((c[0]+sx*(w-r)+r*math.cos(a),c[1]+sy*(d-r)+r*math.sin(a),z))
    return ring

def case(obj,name,profile,styles,c=(0,0),caps=(CAST,CAST)):
    obj.loft(name,[rounded(w,d,r,h,c) for w,d,r,h in profile],styles,caps=caps)

def radial_shell(obj, name, profile, finishes, steps, closed=True, caps=(DARK, RUB)):
    """Bridge turned sections to four-corner square sections without subdividing flats.
    Each half-circle includes both sides of the chased channel and exact 45° corners.
    Profile rows are (radius or half-width, height, square, has_channel).
    """
    half = steps + 4
    count = 2 * half
    corners = {side + 2 + quarter for side in (0, half)
               for quarter in (steps // 4, 3 * steps // 4)}
    channel = {i % count for center in (0, half) for i in range(center-2, center+3)}
    start = len(obj.verts)
    rows = []
    for radius, height, square, grooved in profile:
        floor = math.asin(.0017 / radius)
        shoulder = math.asin(.003 / radius)
        angles = np.interp(range(steps+1), [0, steps//4, 3*steps//4, steps],
                           [shoulder, math.pi/4, 3*math.pi/4, math.pi-shoulder])
        half_angles = [0, floor, *angles, math.pi-floor]
        indices = corners | (channel if grooved else set()) if square else range(count)
        row = {}
        for i in sorted(indices):
            side, j = divmod(i, half)
            a = half_angles[j] + side * math.pi
            x, y = math.cos(a), math.sin(a)
            r = radius/max(abs(x), abs(y)) if square else radius
            drop = .00145 if grooved and j in (0, 1, half-1) else 0
            row[i] = len(obj.verts)
            obj.verts.append((r*x, r*y, height-drop))
        rows.append(row)

    for row_index, finish in enumerate(finishes):
        a = rows[row_index]
        b = rows[(row_index+1) % len(rows)]
        common = sorted(a.keys() & b.keys())
        def paint(i):
            if profile[row_index][3] and profile[(row_index+1) % len(rows)][3]:
                if i % half in (0, half-1): return DARK
                if i % half in (1, half-2): return RUB
            return finish
        for first, end in zip(common, common[1:]+[common[0]+count]):
            keys = [k % count for k in range(first, end+1)]
            bottom = [a[k] for k in keys if k in a]
            top = [b[k] for k in keys if k in b]
            if len(bottom) == len(top):
                obj.face([bottom[0], bottom[1], top[1], top[0]], paint(first))
                continue
            # A fan from each corner meets at the midpoint of the curved edge.
            # Collinear vertices on a square side have no silhouette or shading role.
            dense, sparse = (bottom, top) if len(bottom)>2 else (top, bottom)
            mid = len(dense)//2
            for j in range(len(dense)-1):
                anchor = sparse[0 if j<mid else 1]
                obj.face([dense[j], dense[j+1], anchor], paint(first))
            obj.face([sparse[0], sparse[1], dense[mid]], paint(first))
    if not closed:
        obj.face(list(reversed(rows[0].values())),caps[0])
        obj.face(list(rows[-1].values()),caps[1])
    obj.parts[name] = (start,len(obj.verts))

def bow(obj,name,x,y,seat,grip,w,along_x):
    # A forged rectangular bow swept around two radiused shoulders. The crown's
    # mid-section is the physical pinch target. Octagonal cross-section rolls edges.
    radius=.010
    bend_z=grip-radius
    path=[(-w,seat,(0,1))]
    for i in range(5):
        a=math.pi-i*math.pi/8
        path.append((-w+radius+radius*math.cos(a),bend_z+radius*math.sin(a),(math.sin(a),-math.cos(a))))
    for i in range(5):
        a=math.pi/2-i*math.pi/8
        path.append((w-radius+radius*math.cos(a),bend_z+radius*math.sin(a),(math.sin(a),-math.cos(a))))
    path.append((w,seat,(0,-1)))
    rings=[]
    for u,z,(du,dz) in path:
        ring=[]
        for v,h in [(-.0054,-.004),(-.004,-.0054),(.004,-.0054),(.0054,-.004),(.0054,.004),(.004,.0054),(-.004,.0054),(-.0054,.004)]:
            p=u-dz*h
            ring.append((x+(p if along_x else v),y+(v if along_x else p),z+du*h))
        rings.append(ring)
    obj.loft(name,rings,[lambda i: RUB if i in (0,2,4,6) else CAST]*(len(rings)-1))
    for s in (-1,1):
        c=(x+(s*w if along_x else 0),y+(0 if along_x else s*w))
        lathe(obj,name+" ferrule "+str(s),[(.0095,seat-.0012),(.0095,seat+.0008),(.0083,seat+.0026),(.0075,seat+.003)], [DARK,RUB,BAND],12,c,caps=(DARK,BAND))

bed=Casting("Bed")
radial_shell(bed, "Bearing and square pin", [
    (.189,-.020,False,False), (.193,-.0095,False,False),
    (.193,-.0025,False,False), (.1915,-.001,False,False),
    (.183,-.001,False,False), (.1808,-.003,False,False),
    (.014,-.003,True,False), (.012,.002,True,False),
    (.012,.054,True,False), (.0104,.056,True,False)],
    [DARK,CAST,RUB,BAND,RUB,DARK,CAST,CAST,RUB],32,closed=False)
for sign in (-1,1):
    rings=[]
    for h,s in [(-.002,1),(.0033,1),(.0045,.83)]:
        rings.append([(sign*(.187+(x-.187)*s),y*s,h) for x,y in [(.1809,0),(.186,.0045),(.192,.0045),(.192,-.0045),(.186,-.0045)]])
    bed.loft("Fixed index "+str(sign),rings,[CAST,RUB],caps=(DARK,RUB))

outer=Casting("Outer")
radial_shell(outer, "X channel and graduated track", [
    (.021,.0005,True,False), (.176,.0005,False,False),
    (.180,.003,False,False), (.180,.013,False,False), (.1785,.0155,False,True),
    (.175,.0165,False,True), (.171,.015,False,True), (.1695,.016,False,True),
    (.092,.016,False,True), (.089,.0167,False,True), (.085,.015,False,True),
    (.0225,.015,True,True), (.021,.0135,True,False)],
    [DARK,DARK,CAST,RUB,RUB,BAND,DARK,FIELD,CAST,RUB,CAST,DARK,DARK],32)
bow(outer,"Outer grip",.155,0,.01545,.055,.023,False)

inner=Casting("Inner")
radial_shell(inner, "Square aperture", [
    (.022,.0003,True,False), (.080,.0003,False,False),
    (.084,.003,False,False), (.084,.012,False,False),
    (.082,.014,False,True), (.077,.013,False,True),
    (.0755,.014,False,True), (.045,.014,False,True),
    (.028,.0153,True,True), (.024,.0153,True,True), (.022,.013,True,False)],
    [DARK,DARK,CAST,RUB,BAND,DARK,FIELD,CAST,RUB,RUB,DARK],24)
bow(inner,"Inner grip",0,.068,.0135,.062,.0195,True)

box=Casting("Box")
# The inside returns down to a solid, visible tray floor.
case(box,"Open casket",[(.100,.088,.017,0),(.106,.094,.020,.003),(.106,.094,.020,.007),(.102,.090,.017,.011),(.103,.091,.018,.017),(.104,.092,.019,.087),(.106,.094,.020,.089),(.107,.095,.020,.091),(.107,.095,.020,.103),(.1058,.0938,.019,.105),(.100,.088,.016,.105),(.0985,.0865,.015,.103),(.096,.084,.014,.057),(.092,.080,.012,.051)], [RUB,BAND,RUB,RUB,CAST,RUB,DARK,BAND,RUB,BAND,RUB,LINING,LINING],caps=(DARK,LINING))
case(box,"Leaf rest",[(.029,.048,.006,.050),(.029,.048,.006,.054),(.026,.046,.005,.057),(.025,.046,.005,.0665),(.0235,.0445,.004,.0691)],[DARK,RUB,CAST,RUB],c=(0,-.0275),caps=(DARK,LINING))

# A four-lobed escutcheon lies on the player's side. Raised border and domed boss.
flower=[]
for scale,depth in [(1,-.091), (1,-.094),(.83,-.0952),(.79,-.0945),(.33,-.0945),(.13,-.0995)]:
    ring=[]
    for i in range(24):
        a=i*TAU/24
        r=.020*(1+.18*math.cos(4*a)-.05*math.cos(8*a))
        ring.append((scale*r*math.cos(a),depth,.082+scale*r*math.sin(a)*.82))
    flower.append(ring)
box.loft("Ruyi clasp",flower,[CAST,RUB,DARK,BAND,RUB],caps=(DARK,RUB))
for x in (-.014,.014):
    lathe_rings=[]
    for r,y in [(.0023,-.0948),(.0023,-.0958),(.0015,-.097)]:
        lathe_rings.append([(x+r*math.cos(i*TAU/8),y,.082+r*math.sin(i*TAU/8)) for i in range(8)])
    box.loft("Clasp tack "+str(x),lathe_rings,[CAST,RUB],caps=(DARK,RUB))

lid=Casting("Lid")
case(lid,"Vaulted cover",[(.0972,.0852,.015,-.035),(.0972,.0852,.015,-.0315),(.1055,.0935,.019,-.03125),(.108,.096,.021,-.029),(.108,.096,.021,-.025),(.107,.095,.021,-.023),(.107,.095,.021,-.019),(.104,.092,.021,-.0175),(.098,.086,.023,-.002),(.094,.082,.023,.0035),(.087,.075,.022,.009),(.075,.063,.020,.0135),(.058,.047,.016,.017),(.034,.027,.011,.019)], [DARK,DARK,RUB,BAND,RUB,DARK,RUB,CAST,CAST,CAST,CAST,CAST,CAST],caps=(LINING,CAST))
lathe(lid,"Lotus button",[(.015,.0184),(.017,.022),(.007,.026),(.007,.030),(.011,.032),(.0135,.0385),(.012,.042),(.0085,.045),(.003,.047)], [RUB,RUB,CAST,RUB,CAST,CAST,CAST,RUB],16,caps=(DARK,RUB))
objs=[i.object() for i in [bed,outer,inner,box,lid]]
for o in objs:
    bpy.context.view_layer.objects.active=o
    o.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(56),island_margin=.0015,correct_aspect=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    o.select_set(False)
# One non-overlapping UV atlas, packed across all five castings.
for o in objs: o.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True,margin_method="FRACTION",margin=.0025,shape_method="CONVEX")
bpy.ops.object.mode_set(mode="OBJECT")

coords=[]; uvs=[]; tags=[]; items=[]; normals=[]
for index,o in enumerate(objs):
    me=o.data
    me.uv_layers.active.name="BronzeAtlas"
    mod=o.modifiers.new("Fixed tessellation","TRIANGULATE")
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.modifier_apply(modifier=mod.name)
    me=o.data
    for f in me.polygons:
        coords.append([tuple(me.vertices[j].co) for j in f.vertices])
        uvs.append([tuple(me.uv_layers.active.data[j].uv) for j in f.loop_indices])
        tags.append(me.attributes["CastFinish"].data[f.index].value)
        items.append(index)
        normals.append(tuple(f.normal))
np.savez_compressed(CACHE/"triangles.npz",xyz=coords,uv=uvs,finish=tags,item=items,normal=normals)
if not finish:
    print("Geometry prepared for atlas rasterization.")
else:
    mat=bpy.data.materials.new("CG_A1_Bronze")
    nt=mat.node_tree
    bs=next(n for n in nt.nodes if n.type=="BSDF_PRINCIPLED")
    def image_node(suffix,data):
        im=bpy.data.images.load(str(DEST/("CG_A1_"+suffix+".png")))
        if data: im.colorspace_settings.name="Non-Color"
        im.pack()
        n=nt.nodes.new("ShaderNodeTexImage"); n.image=im
        return n
    albedo=image_node("BaseColor",False)
    mask=image_node("MetalSmooth",True)
    norm=image_node("Normal",True)
    sep=nt.nodes.new("ShaderNodeSeparateColor")
    inv=nt.nodes.new("ShaderNodeMath"); inv.operation="SUBTRACT"; inv.inputs[0].default_value=1
    nmap=nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(albedo.outputs["Color"],bs.inputs["Base Color"])
    nt.links.new(mask.outputs["Color"],sep.inputs[0]); nt.links.new(sep.outputs[0],bs.inputs["Metallic"])
    nt.links.new(mask.outputs["Alpha"],inv.inputs[1]);nt.links.new(inv.outputs[0],bs.inputs["Roughness"])
    nt.links.new(norm.outputs["Color"],nmap.inputs["Color"]);nt.links.new(nmap.outputs[0],bs.inputs["Normal"])
    mask.image.alpha_mode="CHANNEL_PACKED"
    for i,ob in enumerate(objs):
        ob.data.transform(Matrix.Rotation(math.pi,4,"Z"))
        ob.data.materials.append(mat)
        ob.data.attributes.remove(ob.data.attributes["CastFinish"])
        for sel in bpy.context.selected_objects: sel.select_set(False)
        ob.select_set(True); bpy.context.view_layer.objects.active=ob
        bpy.ops.export_scene.fbx(filepath=str(DEST/(ob.name+".fbx")),use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",bake_space_transform=True,apply_scale_options="FBX_SCALE_ALL",use_tspace=True,bake_anim=False,path_mode="STRIP",use_custom_props=False)
        ob.location=(-.36 if i>=3 else 0,0,HEIGHT[i])
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            sp=area.spaces.active;sp.region_3d.view_location=(-.13,0,1)
            sp.region_3d.view_distance=.86
            sp.region_3d.view_rotation=Vector((-.18,-.8,-.52)).to_track_quat("-Z","Y")
            sp.shading.color_type="TEXTURE";sp.clip_start=.001
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.save_as_mainfile(filepath=str(SRC/"ConcentricGauge_A1_Bronze.blend"))
