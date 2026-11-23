"""Rebuild the canal-side measuring station and bake its relief in headless Blender. Geometry in metres."""
from math import cos, sin, pi
from pathlib import Path
import bpy, numpy as np, json, os
from mathutils import Vector
HERE = Path(__file__).resolve().parent
STAGE = Path(os.environ.get('COURT_PROP_STAGE', HERE.parents[3] / 'Assets/Art/Environment/Opening/FurnishCourtB'))
STAGE.mkdir(exist_ok=True, parents=True)
NAMES = ('CanalMeasuringStation',)
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
lowmat=bpy.data.materials.new('CanalStation_Baked');lowmat.use_nodes=True



from math import atan2
import bmesh

willow = material('Steamed willow strips', (.10,.045,.011),(.48,.28,.092), 0,.29,(22,22,100))
reed = material('Split bamboo',(.18,.09,.026),(.66,.43,.17),.01,.33,(42,53,1))
linen = material('Indigo homespun',(.008,.016,.025),(.044,.085,.12),0,.17,(120,110,65))

def skin(name, verts, faces, mat):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    mesh.materials.append(mat);parts.append(obj)
    return obj

def tube(name, path, radius, mat, sides=8, closed=False):
    points=[Vector(p) for p in path]
    verts=[];faces=[]
    for k,pt in enumerate(points):
        prev=points[(k-1)%len(points) if closed else max(0,k-1)]
        nxt=points[(k+1)%len(points) if closed else min(k+1,len(points)-1)]
        tangent=(nxt-prev).normalized();axis=Vector((0,0,1)) if abs(tangent.z)<.85 else Vector((0,1,0))
        u=tangent.cross(axis).normalized();v=tangent.cross(u)
        for j in range(sides):
            ang=2*pi*j/sides
            verts.append(tuple(pt+radius*(cos(ang)*u+sin(ang)*v)))
    for k in range(len(points) if closed else len(points)-1):
        a=k*sides;b=((k+1)%len(points))*sides
        for j in range(sides):faces.append((a+j,a+(j+1)%sides,b+(j+1)%sides,b+j))
    if not closed:
        faces.extend([tuple(range(sides))[::-1],tuple(range((len(points)-1)*sides,len(points)*sides))])
    return skin(name,verts,faces,mat)

def basket(high):
    cx,cy,z0=-.43,-.042,.513
    segments=64 if high else 24
    rings=[(.02,.0),(.155,.001),(.177,.018),(.214,.091),(.226,.148),(.222,.159),(.209,.157),(.203,.100),(.167,.033),(.144,.018),(.015,.018)]
    ob=profile('Basket vessel',rings,[willow]*(len(rings)-1),segments,'up',(cx,cy,z0))
    # Close the spun base independently on the outside and inside.
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary]);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(ob.data);bm.free()
    def radius(f):
        return float(np.interp(.015+.135*f,[.001,.018,.091,.148,.159],[.155,.177,.214,.226,.222]))
    if high:
        # Alternating upright splints and horizontal weft project to the vessel.
        for i in range(76):
            ang=2*pi*i/76
            line=[]
            for j in range(17):
                f=j/16
                r=radius(f)+.0032*cos(pi*(i+j))
                line.append((cx+r*cos(ang),cy+r*sin(ang),z0+.015+.135*f))
            t=tube('Upright willow',line,.0031,reed,5);t['bake_host']='Basket vessel'
        for i in range(16):
            f=(i+.5)/16
            line=[]
            for j in range(152):
                ang=2*pi*j/152
                r=radius(f)+.0026*cos(ang*38+pi*i)
                line.append((cx+r*cos(ang),cy+r*sin(ang),z0+.015+.135*f))
            t=tube('Woven willow',line,.0038,willow,5,True);t['bake_host']='Basket vessel'
    for n,rz in enumerate(((.221,.15),(.223,.159),(.17,.018))):
        r,z=rz
        line=[(cx+r*cos(2*pi*i/segments),cy+r*sin(2*pi*i/segments),z0+z) for i in range(segments)]
        tube('Bound basket hoop '+str(n),line,.0061,reed,7 if high else 5,True)

def sandal(name,x,y,z,high):
    n=40 if high else 16
    rx,ry=.052,.127
    verts=[];faces=[]
    for h in (0,.012):
        for i in range(n):
            a=2*pi*i/n
            verts.append((x+rx*cos(a)*(1+.17*sin(a)),y+ry*sin(a),z+h+.009*(sin(a)**4)))
    faces.extend([tuple(range(n))[::-1],tuple(range(n,2*n))])
    for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
    skin(name+' sole',verts,faces,reed)
    for j in (-1,1):
        tube(name+' thong '+str(j),[(x+rx*j*.86,y-.015,z+.012),(x+.022*j,y+.015,z+.068),(x,y+.081,z+.023)],.0075,cord,8 if high else 5)
    if high:
        for j in range(-7,8):
            f=j/8
            span=rx*(1-f*f)**.5*(1+.17*f)
            a=(x-span,y+ry*f,z+.015+.008*f**4)
            b=(x+span,y+ry*f,z+.015+.008*f**4)
            t=tube('Sole braid',[a,b],.0026,willow,5);t['bake_host']=name+' sole'

def apron(high):
    cx=-.45
    # Panel with a gathered bib, flared lower edge and continuous rolled hem.
    rows=12; cols=14
    verts=[];faces=[]
    for side in (0,1):
        for r in range(rows+1):
            t=r/rows
            width=.242-.047*t-.066*max(0,(t-.69)/.31)
            for c in range(cols+1):
                u=2*c/cols-1
                x=cx+width*u
                y=.117+side*.012+.022*sin(u*10+1.2*t)*(.85-.3*t)+.009*cos(t*7+u)
                z=.875+.515*t+.009*cos(u*8)*(1-t)+.012*u*t
                verts.append((x,y,z))
    stride=(cols+1)*(rows+1)
    for side in (0,1):
        for r in range(rows):
            for c in range(cols):
                a=side*stride+r*(cols+1)+c;face=(a,a+1,a+cols+2,a+cols+1)
                faces.append(face if side==0 else face[::-1])
    edge=list(range(cols+1))+[r*(cols+1)+cols for r in range(1,rows+1)]+[rows*(cols+1)+c for c in range(cols-1,-1,-1)]+[r*(cols+1) for r in range(rows-1,0,-1)]
    for k,a in enumerate(edge):
        b=edge[(k+1)%len(edge)];faces.append((a,b,b+stride,a+stride))
    skin('Indigo wrap',verts,faces,linen)
    for s in (-1,1):
        tube('Apron neck tie '+str(s),[(cx+.123*s,.122,1.395+s*.009),(cx+.125*s,.137,1.590),(cx+.119*s,.175,1.60),(cx+.105*s,.173,1.55)],.008,linen,7 if high else 5)

def station(high):
    for x in (-.753,.753):
        for y in (-.18,.17):
            box('Foot '+str((x,y)),(x,y,.237),(.075,.078,.474),endwood,high,.004,True)
        box('Under shelf end bearer '+str(x),(x,-.005,.100),(.068,.405,.055),wood,high,.003,True)
        box('End rail '+str(x),(x,-.005,.425),(.069,.408,.061),wood,high,.004,True)
    for k in range(3):
        y=-.162+k*.155
        box('Counter plank '+str(k),(0,y,.484),(1.65,.149,.058),wood,high,.004,True)
        box('Low shelf slat '+str(k),(0,y,.140),(1.51,.135,.035),wood,high,.003,True)
    for i,x in enumerate((-.73,.74)):
        box('High stile '+str(i),(x,.187,1.024),(.057,.055,1.025 if i==1 else 1.142),endwood,high,.004,True)
    box('Hanging bar',(-.439,.165,1.573),(.682,.054,.064),wood,high,.004,True)
    for i,x in enumerate((-.574,-.329)):
        tube('Garment peg '+str(i),[(x,.16,1.56),(x,.112,1.602)],.014,bronze,12 if high else 6)
    for i,(x,top) in enumerate(((.014,1.92),(.263,1.69),(.494,1.47))):
        a=Vector((x+.068,-.05,.515));b=Vector((x,.145,top))
        direction=(b-a).normalized()
        tube('Marked measuring pole '+str(i),[a,b],.036,reed,24 if high else 12)
        for k,f in enumerate((.028,.45,.885,.975)):
            p=a.lerp(b,f);tube('Bamboo ferrule '+str((i,k)),[p-direction*.006,p+direction*.006],.039,bronze if k in (0,3) else willow,16 if high else 8)
        if high:
            for k in range(1,int((b-a).length/.055)-1):
                p=a+direction*.055*k;p.y-=.034
                length=.036 if k%5==0 else .022 if k%2==0 else .013
                tick=box('Graduation',(p.x+.003,p.y,p.z),(length,.005,.003),ink,False)
                tick['bake_host']='Marked measuring pole '+str(i)
    for x in (-.019,.701):
        box('Cradle support '+str(x),(x,-.071,.714),(.04,.067,.402),endwood,high,.003,True)
    box('Pole cradle',(.34,-.08,.887),(.773,.070,.051),wood,high,.004,True)
    box('Rear rod rail',(.37,.148,1.193),(.792,.081,.047),wood,high,.003,True)
    basket(high);apron(high)
    for k,(x,y,z) in enumerate(((-.50,-.202,.662),(-.38,-.195,.666))):
        start=len(parts);sandal('Sandshoe '+str(k),x,y,z,high)
        from mathutils import Matrix
        centre=Vector((x,y,z))
        tilt=Matrix.Rotation(.52 if k==0 else .42,3,'X')
        for ob in parts[start:]:
            for v in ob.data.vertices:v.co=centre+tilt@(v.co-centre)

highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    collection=bpy.data.collections.new('Source relief' if high else 'Runtime meshes')
    scene.collection.children.link(collection)
    for name,fn in zip(NAMES,(station,)):
        fn(high)
        obj=join_asset(name+('_HIGH' if high else ''),collection)
        table[name]=obj
        if not high:
            obj.data.materials.clear();obj.data.materials.append(lowmat)
            for face in obj.data.polygons:face.material_index=0
for ob in highs.values():ob.hide_set(True)
# Independent rectangles are intentionally allocated by visible physical area.
for name,rect in zip(NAMES,[(.008,.008,.984,.984)]):
    obj=lows[name];active(obj)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.0,island_margin=.008,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for loop in obj.data.uv_layers.active.data:
        loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=obj.modifiers.new('Freeze game triangles','TRIANGULATE');bake_mod(obj,tri)
    print('TRIS',name,len(obj.data.polygons),flush=True)
exec((HERE/'bake.py').read_text(encoding='utf8'))


