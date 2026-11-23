"""Rebuild the three metrology furnishings in headless Blender. Geometry in metres."""
from math import cos, sin, pi
from pathlib import Path
import bpy, numpy as np, json, os
from mathutils import Vector
HERE = Path(__file__).resolve().parent
STAGE = Path(os.environ.get('MEASURE_STAGE', 'D:/Temp/MeasureRoomProps20261119'))
STAGE.mkdir(exist_ok=True, parents=True)
NAMES = ('PlumbSquareBoard', 'GraduatedRuleRack', 'DividerPartsTray')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=5
scene.render.threads_mode='FIXED'
scene.render.threads=6
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
wood = material('Blackened elm', (.022,.010,.005), (.21,.092,.034), .025,.32,(.8,42,70))
endwood = material('Smoked upright', (.029,.012,.005), (.23,.105,.040), .025,.31,(54,45,1.1))
ruler = material('Oiled boxwood', (.18,.09,.034), (.57,.37,.16), .015,.43,(1.7,46,78))
bronze = material('Weathered measuring bronze', (.085,.064,.031), (.43,.30,.126), .7,.38)
light = material('Polished gauge edge', (.25,.17,.07), (.59,.42,.23), .77,.47)
ink = material('Scored numeral marks', (.015,.011,.006), (.047,.027,.012), .015,.31)
cord = material('Waxed flax', (.13,.078,.033), (.42,.288,.137), .0,.23,(15,15,55))
felt = material('Muted indigo felt', (.013,.030,.033), (.06,.095,.088), .0,.16,(90,90,45))
lowmat=bpy.data.materials.new('MeasureRoom_Baked');lowmat.use_nodes=True

def engraved_scale(host, x0, x1, y, z, pitch, height, high):
    if not high: return
    for i in range(1,round((x1-x0)/pitch)):
        mark_height=height*(1 if i%5==0 else .64 if i%2==0 else .43)
        part=box('Incised graduation',(x0+i*pitch,y,z+mark_height/2),(.0032,.001,mark_height),ink,False)
        part['bake_host']=host

def peg(name,x,y,z,rad,depth,high):
    return profile(name,[(0,-depth),(rad*.76,-depth),(rad,-depth*.72),(rad,depth*.14),(0,depth*.14)],
                   [bronze,light,bronze,bronze],32 if high else 10,'dial',(x,y,z))

def board(high):
    # An open timber frame shows the original workshop wall between three tools.
    for x in (-.98,.98):
        box('Frame stile '+str(x),(x,.0,.59),(.083,.088,1.18),endwood,high,.005,coarse=True)
    for i,z in enumerate((.057,.264,1.005,1.12)):
        box('Frame batten '+str(i),(0,.018,z),(1.902,.058,.083 if i!=1 else .059),wood,high,.004,coarse=True)
    # A triangular square and a second, reversed carpenter's L.
    box('Large square leg',(-.777,-.074,.639),(.065,.045,.813),ruler,high,.003,coarse=True)
    box('Large square base',(-.431,-.071,.265),(.632,.041,.066),ruler,high,.003,coarse=True)
    beam('Large square brace',(-.782,-.077,.997),(-.164,-.077,.283),.040,.033,ruler,high,.002,True)
    engraved_scale('Large square base',-.747,-.115,-.092,.236,.023,.045,high)
    for i,(x,z) in enumerate(((-.777,1.005),(-.777,.264),(.492,1.005))):
        box('Timber suspension clip '+str(i),(x,-.036,z),(.058,.15,.09),bronze,high,.003)
        peg('Clip pin '+str(i),x,-.099,z,.016,.009,high)
    box('Small square leg',(.489,-.080,.735),(.070,.051,.58),ruler,high,.003,True)
    box('Small square arm',(.697,-.081,1.0),(.350,.045,.057),ruler,high,.003,True)
    engraved_scale('Small square arm',.532,.861,-.104,.976,.024,.039,high)
    box('Plumb peg socket',(.125,-.012,1.12),(.105,.09,.10),bronze,high,.004)
    peg('Plumb belaying pin',.125,-.015,1.12,.031,.109,high)
    profile('Plumb line',[(0,0),(.004,0),(.004,.682),(0,.682)],[cord]*3,12 if high else 6,'up',(.125,-.106,.44))
    shape=[(0,.0),(.047,.098),(.059,.137),(.057,.160),(.041,.179),(.028,.19),(.021,.213),(0,.215)]
    profile('Turned plumb bob',shape,[light,bronze,light,bronze,bronze,light,bronze],
            64 if high else 16,'up',(.125,-.106,.255))

def rack(high):
    for x in (-.72,.72):
        box('Rack post '+str(x),(x,.0,.621),(.116,.105,1.242),endwood,high,.007,True)
    for k,l in enumerate((2.01,1.92,2.13,1.99,2.17)):
        z=.105+k*.245
        for x in (-.72,.72):
            n=f'Corbel {x} {k}'
            box(n,(x,-.080,z),(.103,.243,.054),wood,high,.005,True)
            box(f'Stop {x} {k}',(x,-.184,z+.037),(.074,.040,.045),bronze,high,.002)
        n=f'Boxwood rule {k}'
        box(n,(.014*(k%3-1),-.113,z+.062),(l,.058,.073),ruler if k!=3 else bronze,high,.003,True)
        engraved_scale(n,-l*.46,l*.46,-.143,z+.028,.027,.049,high)
        if high:
            for s in (-1,1):
                part=box('Protective copper tip',(s*(l/2-.014)+.014*(k%3-1),-.143,z+.061),(.026,.002,.064),bronze,False)
                part['bake_host']=n

def tray(high):
    box('Tray board',(0,0,.016),(.678,.442,.032),wood,high,.004,True)
    for s in (-1,1):
        box('Long rim '+str(s),(0,s*.211,.046),(.681,.027,.070),wood,high,.004,True)
        box('Short rim '+str(s),(s*.326,0,.047),(.030,.398,.064),endwood,high,.004,True)
    box('Protective textile bed',(0,0,.0325),(.624,.393,.008),felt,high,.003)
    box('Inner separator',(.048,0,.047),(.016,.391,.027),wood,high,.002,True)
    # Divider lies flat on the liner, with a rounded hinge and tapered arms.
    for s in (-1,1):
        a=(-.143+s*.009,.095,.051);b=(-.143+s*.094,-.153,.049)
        beam('Divider leg '+str(s),a,b,.017,.016,bronze,high,.0033,True)
        c=(-.143+s*.103,-.183,.046)
        beam('Divider tip '+str(s),b,c,.009,.012,light,high,.001)
    profile('Compass pivot',[(0,0),(.024,0),(.029,.004),(.028,.012),(.018,.016),(0,.016)],
            [bronze,light,bronze,light,bronze],48 if high else 12,'up',(-.143,.095,.047))
    for k in range(3):
        z=.064+k*.013
        box('Standard mass '+str(k),(.097+k*.079,-.076,z),(.065,.128,.055+k*.026),bronze,high,.004,True)
        if high:
            for j in range(k+1):
                glyph=box('Weight witness mark',(.089+k*.079+j*.008,-.076,z+(.055+k*.026)/2),(.004,.047,.001),ink,False)
                glyph['bake_host']='Standard mass '+str(k)
    for k in range(2):
        x=.135+k*.114
        profile('Round weight '+str(k),[(0,0),(.039,0),(.041,.005),(.041,.027),(.029,.029),(.012,.036),(0,.036)],
                [bronze,light,bronze,light,bronze,light],40 if high else 14,'up',(x,.092,.036))

highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    collection=bpy.data.collections.new('Source relief' if high else 'Runtime meshes')
    scene.collection.children.link(collection)
    for name,fn in zip(NAMES,(board,rack,tray)):
        fn(high)
        obj=join_asset(name+('_HIGH' if high else ''),collection)
        table[name]=obj
        if not high:
            obj.data.materials.clear();obj.data.materials.append(lowmat)
            for face in obj.data.polygons:face.material_index=0
for ob in highs.values():ob.hide_set(True)
# Independent rectangles are intentionally allocated by visible physical area.
for name,rect in zip(NAMES,[(.007,.008,.490,.985),(.511,.008,.482,.703),(.511,.725,.482,.266)]):
    obj=lows[name];active(obj)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.0,island_margin=.020,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for loop in obj.data.uv_layers.active.data:
        loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=obj.modifiers.new('Freeze game triangles','TRIANGULATE');bake_mod(obj,tri)
    print('TRIS',name,len(obj.data.polygons),flush=True)
exec((HERE/'bake.py').read_text(encoding='utf8'))


