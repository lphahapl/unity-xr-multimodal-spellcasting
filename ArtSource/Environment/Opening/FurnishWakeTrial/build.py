"""Rebuild the canal-side measuring station and bake its relief in headless Blender. Geometry in metres."""
from math import cos, sin, pi
from pathlib import Path
import bpy, numpy as np, json, os
from mathutils import Vector
HERE = Path(__file__).resolve().parent
STAGE = Path(os.environ.get('ROOM_PROP_STAGE', HERE.parents[3] / 'Assets/Art/Environment/Opening/FurnishWakeTrial'))
STAGE.mkdir(exist_ok=True, parents=True)
NAMES = ('WakeClothesChest','WakeTeaStand','TrialSpareTargets','TrialMendingBench')
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
lowmat=bpy.data.materials.new('RoomFurnish_Baked');lowmat.use_nodes=True



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


worn = material('Fawn homespun quilt',(.17,.119,.082),(.53,.447,.321),0,.17,(80,72,44))
glaze = material('Speckled olive celadon',(.115,.139,.083),(.45,.436,.281),.04,.68,(24,25,23))
wicker = material('Walnut rattan',(.066,.028,.009),(.32,.183,.058),0,.32,(72,12,40))
straw = material('Twisted wheat straw',(.12,.064,.016),(.54,.342,.108),0,.23,(52,57,41))
def ellipse(name,cx,cy,cz,rx,ry,rad,mat,high,vertical=False):
    seg=40 if high else 16
    line=[(cx+rx*cos(2*pi*i/seg),cy if vertical else cy+ry*sin(2*pi*i/seg),cz+ry*sin(2*pi*i/seg) if vertical else cz) for i in range(seg)]
    return tube(name,line,rad,mat,7 if high else 5,True)

def cloth_panel(name,high,x,z,width,height,mat,profile_width=None):
    rows,cols=12,14
    pts=[];faces=[]
    for side in (0,1):
        for i in range(rows+1):
            t=i/rows
            w=width*(profile_width(t) if profile_width else (1-.13*t))
            for j in range(cols+1):
                u=2*j/cols-1
                pts.append((x+.5*u*w,.135+side*.013+.024*sin(u*9.4+t)+.007*sin(2*t+u),z+height*t+.009*cos(u*7)*(1-t)))
    st=(cols+1)*(rows+1)
    for side in (0,1):
        for i in range(rows):
            for j in range(cols):
                a=side*st+i*(cols+1)+j
                f=(a,a+1,a+cols+2,a+cols+1);faces.append(f if side==0 else f[::-1])
    e=list(range(cols+1))+[i*(cols+1)+cols for i in range(1,rows+1)]+[rows*(cols+1)+j for j in range(cols-1,-1,-1)]+[i*(cols+1) for i in range(rows-1,0,-1)]
    for k,a in enumerate(e):
        b=e[(k+1)%len(e)];faces.append((a,b,b+st,a+st))
    return skin(name,pts,faces,mat)

def soft_fold(name,x,y,z,size,mat,high):
    ob=box(name,(x,y,z),size,mat,high, min(size)*.40,True)
    # Pillow and folded quilt have rounded corners adapted to mobile silhouette.
    return ob

def clothes(high):
    w=.86;d=.46
    for i,x in enumerate((-.35,.35)):
        box('Chest foot '+str(i),(x,0,.018),(.051,.391,.036),wood,high,.002,True)
    box('Rattan case body',(0,0,.247),(w,d,.463),wicker,high,.026,True)
    box('Lapped lid',(0,-.003,.494),(w+.009,d+.009,.072),wicker,high,.023,True)
    for i,x in enumerate((-.373,.373)):
        box('Case brass band '+str(i),(x,-.003,.495),(.034,d+.013,.077),bronze,high,.005,True)
        box('Front binding '+str(i),(x,-.231,.25),(.024,.012,.435),bronze,high,.005)
    box('Lower leather binding',(0,-.231,.042),(.829,.013,.035),wood,high,.003)
    box('Hasp tab',(0,-.242,.471),(.073,.020,.101),bronze,high,.005,True)
    ellipse('Hasp ring',0,-.253,.445,.020,.024,.0048,light,high,True)
    for layer in range(3):
        soft_fold('Quilt fold '+str(layer),-.055+layer*.008,-.005,.551+layer*.039,(.611-layer*.012,.341,.065),worn,high)
    box('Quilt indigo border',(.029,-.005,.640),(.151,.343,.057),linen,high,.019,True)
    box('Bronze peg rail',(0,.196,1.705),(.94,.070,.092),wood,high,.008,True)
    for i,x in enumerate((-.34,-.172,.126,.347)):
        tube('Clothes hook '+str(i),[(x,.18,1.699),(x,.097,1.736)],.015,bronze,12 if high else 7)
    cloth_panel('Hanging coat',high,-.218,.984,.36,.714,linen,lambda t:1-.27*t-.33*max(0,(t-.8)/.2))
    for k,side in enumerate((-1,1)):
        sleeve=beam('Wide linen sleeve '+str(k),(-.218+.12*side,.131,1.588),(-.218+.25*side,.092,1.156),.092,.068,linen,high,.022,True)
        beam('Sleeve binding '+str(k),(-.218+.248*side,.088,1.18),(-.218+.254*side,.088,1.155),.098,.07,cord,high,.006,True)
    tube('Coat collar',[(-.298,.10,1.617),(-.215,.093,1.519),(-.148,.104,1.641),(-.166,.162,1.718),(-.249,.16,1.706)],.012,linen,8 if high else 5,True)
    for i in range(4):
        h=1.462-i*.092;t=(h-.984)/.714;w=.36*(1-.27*t)
        curve=[]
        for k in range(6):
            px=-.227+.042*k/5;u=(px+.218)/(.5*w)
            surface_y=.135+.024*sin(u*9.4+t)+.007*sin(2*t+u)
            curve.append((px,surface_y-.003*sin(pi*k/5),h+.001*k/5))
        tube('Cloth clasp '+str(i),curve,.005,cord,8 if high else 5)
    hx,hz=.284,1.474
    rings=[(0,-.09),(.046,-.078),(.14,-.037),(.224,.022),(.239,.038),(.237,.055),(.144,.005),(.034,-.055),(0,-.061)]
    hat=profile('Field hat',rings,[straw,reed,straw,reed,willow,willow,straw,straw],48 if high else 20,'dial',(hx,.137,hz))
    ellipse('Wicker bound rim',hx,.175,hz,.235,.235,.007,willow,high,True)
    if high:
        for i in range(29):
            a=i*2*pi/29
            rvals=[(.034,-.083),(.08,-.065),(.14,-.038),(.21,.011),(.233,.029)]
            t=tube('Hat radial splint',[(hx+r*cos(a),.137+h,hz+r*sin(a)) for r,h in rvals],.0043,wicker,5)
            t['bake_host']='Field hat'
    tube('Hat cord',[(hx-.08,.155,hz-.21),(hx-.018,.118,hz-.284),(hx+.018,.118,hz-.28),(hx+.08,.154,hz-.206)],.0068,cord,7 if high else 5)

def tea(high):
    for i,x in enumerate((-.171,.171)):
        for j,y in enumerate((-.139,.139)):
            box('Tea cabinet leg '+str((i,j)),(x,y,.307),(.054,.055,.614),endwood,high,.005,True)
    box('Tea cabinet',(0,0,.361),(.354,.286,.546),wood,high,.008,True)
    box('Door raised panel',(0,-.15,.371),(.294,.022,.451),endwood,high,.008,True)
    box('Inset tea panel',(0,-.165,.368),(.239,.010,.378),wood,high,.007)
    ellipse('Cabinet pull',-.082,-.177,.428,.017,.023,.004,bronze,high,True)
    box('Tea top',(0,-.002,.646),(.446,.362,.059),wood,high,.008,True)
    cx,cy=.063,.010
    shapes=[(0,.0),(.054,0),(.061,.012),(.078,.036),(.087,.086),(.073,.128),(.042,.162),(.038,.19),(.043,.208),(.039,.219),(0,.221)]
    profile('Celadon pot',shapes,[glaze]*(len(shapes)-1),44 if high else 20,'up',(cx,cy,.676))
    profile('Teapot lid',[(0,0),(.043,.001),(.045,.007),(.023,.014),(.011,.02),(.013,.03),(0,.035)],[glaze]*6,32 if high else 14,'up',(cx,cy,.888))
    handle=[(cx+.055+.077*sin(pi*j/16),cy,.747+.116*j/16) for j in range(17)]
    tube('Glazed pot handle',handle,.009,glaze,10 if high else 6)
    spout=[(cx-.055,cy,.74),(cx-.086,cy,.764),(cx-.112,cy,.813),(cx-.117,cy,.842),(cx-.129,cy,.849)]
    tube('Curved pot spout',spout,.014,glaze,14 if high else 8)
    profile('Tea bowl',[(0,0),(.027,0),(.030,.006),(.044,.046),(.047,.056),(.043,.056),(.039,.047),(.025,.015),(0,.011)],[glaze]*8,34 if high else 16,'up',(-.118,-.061,.676))

def twisted_face(j,cx,cz):
    """Compress radial straw coils onto the front of each static practice mat."""
    mat=material('Bound straw face '+str(j),(.105,.055,.017),(.55,.318,.101),0,.21,(54,48,63))
    shader,grain,metal,smooth,glow=shades[mat]
    tree=mat.node_tree
    def op(operation,a,b=None):
        node=tree.nodes.new('ShaderNodeMath');node.operation=operation
        for i,value in enumerate([a,b] if b is not None else [a]):
            if isinstance(value,(int,float)):node.inputs[i].default_value=value
            else:tree.links.new(value,node.inputs[i])
        return node.outputs[0]
    pos=tree.nodes.new('ShaderNodeAttribute');pos.attribute_name='WorkshopCraftPosition'
    xyz=tree.nodes.new('ShaderNodeSeparateXYZ');tree.links.new(pos.outputs['Vector'],xyz.inputs[0])
    dx=op('SUBTRACT',xyz.outputs[0],cx);dz=op('SUBTRACT',xyz.outputs[2],cz)
    radius=op('SQRT',op('ADD',op('MULTIPLY',dx,dx),op('MULTIPLY',dz,dz)))
    angle=op('ARCTAN2',dz,dx)
    wave=op('COSINE',op('SUBTRACT',op('MULTIPLY',radius,268),angle))
    twist=op('COSINE',op('ADD',op('MULTIPLY',angle,51),op('MULTIPLY',radius,180)))
    pattern=op('ADD',.64,op('ADD',op('MULTIPLY',wave,.29),op('MULTIPLY',twist,.075)))
    dyed=tree.nodes.new('ShaderNodeMixRGB');dyed.blend_type='MULTIPLY';dyed.inputs[0].default_value=1
    tree.links.new(grain.outputs[0],dyed.inputs[1]);tree.links.new(pattern,dyed.inputs[2])
    tree.links.new(dyed.outputs[0],shader.inputs['Base Color'])
    bump=tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.38;bump.inputs['Distance'].default_value=.003
    tree.links.new(pattern,bump.inputs['Height']);tree.links.new(bump.outputs['Normal'],shader.inputs['Normal'])
    shades[mat]=(shader,dyed,metal,smooth,glow)
    return mat

def targets(high):
    for j,x in enumerate((-.746,.746)):
        for k,y in enumerate((-.183,.169)):
            box('Spare stand foot '+str((j,k)),(x,y,.308),(.080,.082,.616),endwood,high,.006,True)
    for i,y in enumerate((-.175,.172)):
        for j,z in enumerate((.112,.41)):
            box('Spare long rail '+str((i,j)),(0,y,z),(1.448,.065,.082),wood,high,.005,True)
    for i,x in enumerate((-.56,-.29,0,.29,.56)):
        box('Rack floor slat '+str(i),(x,0,.126),(.091,.302,.039),wood,high,.002)
    for j,(x,y,r) in enumerate(((-.323,-.050,.354),(.326,.048,.338))):
        z=.445+r
        shape=[(0,-.044),(.91*r,-.044),(.981*r,-.036),(r,-.019),(r,.055),(.95*r,.067),(0,.067)]
        host='Spare straw round '+str(j)
        face=twisted_face(j,x,z) if high else straw
        profile(host,shape,[face,face,willow,straw,willow,straw],48 if high else 26,'dial',(x,y,z))
        for f in (.15,.50,.90):
            ellipse(host+' lash '+str(f),x,y-.045,z,r*f,r*f,.004,willow,high,True)
        if high:
            steps=920
            spiral=[]
            for i in range(steps):
                a=2*pi*14*i/(steps-1)
                rad=.008+(r*.962-.008)*i/(steps-1)
                spiral.append((x+rad*cos(a),y-.046,z+rad*sin(a)))
            t=tube('Continuous twisted straw',spiral,.004,face,6);t['bake_host']=host
    for i,(rx,ry) in enumerate(((.237,.102),(.234,.102),(.213,.088))):
        ellipse('Hemp coil '+str(i),-.065,-.015,.156+i*.019,rx,ry,.0123,cord,high)
    tube('Rope tail',[(-.063,-.103,.189),(.127,-.124,.168),(.222,-.084,.146),(.274,-.069,.144)],.01,cord,8 if high else 5)

def bench(high):
    for j,x in enumerate((-.607,.607)):
        for k,y in enumerate((-.161,.17)):
            box('Bench leg '+str((j,k)),(x,y,.345),(.071,.072,.69),endwood,high,.006,True)
        box('Bench cross rail '+str(j),(x,.001,.183),(.056,.34,.062),wood,high,.003)
    box('Bench lower stretcher',(0,.013,.183),(1.214,.052,.065),wood,high,.004,True)
    for i in range(3):box('Bench plank '+str(i),(0,-.147+i*.142,.696),(1.395,.138,.064),wood,high,.007,True)
    for layer in range(2):soft_fold('Practice pad '+str(layer),-.31,-.012,.748+.027*layer,(.627,.337,.046),worn,high)
    box('Peg box bottom',(.379,-.022,.747),(.438,.297,.034),wood,high,.003)
    for i,y in enumerate((-.168,.124)):box('Peg box long side '+str(i),(.379,y,.797),(.447,.016,.113),wood,high,.003,True)
    for i,x in enumerate((.167,.593)):box('Peg box end '+str(i),(x,-.022,.797),(.017,.285,.111),wood,high,.003,True)
    for i in range(7):
        x=.244+.045*i
        tube('Practice peg '+str(i),[(x,-.089,.766),(x+.009*(i%3),.013+i%2*.032,.875+i%3*.014)],.0147,ruler,12 if high else 7)
    box('Cloth rail',(.624,.171,.969),(.116,.061,.512),endwood,high,.004,True)
    tube('Bench cloth peg',[(.622,.15,1.155),(.624,.091,1.178)],.013,bronze,12 if high else 6)
    cloth_panel('Wiping cloth',high,.624,.782,.179,.387,linen,lambda t:(1-.79*t))

highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    collection=bpy.data.collections.new('Source relief' if high else 'Runtime meshes')
    scene.collection.children.link(collection)
    for name,fn in zip(NAMES,(clothes,tea,targets,bench)):
        fn(high)
        obj=join_asset(name+('_HIGH' if high else ''),collection)
        table[name]=obj
        if not high:
            obj.data.materials.clear();obj.data.materials.append(lowmat)
            for face in obj.data.polygons:face.material_index=0
for ob in highs.values():ob.hide_set(True)
# Independent rectangles are intentionally allocated by visible physical area.
for name,rect in zip(NAMES,[(.006,.006,.488,.514),(.505,.006,.488,.514),(.006,.532,.488,.461),(.505,.532,.488,.461)]):
    obj=lows[name];active(obj)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.0,island_margin=.008,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for loop in obj.data.uv_layers.active.data:
        loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=obj.modifiers.new('Freeze game triangles','TRIANGULATE');bake_mod(obj,tri)
    print('TRIS',name,len(obj.data.polygons),flush=True)
exec((HERE/'bake.py').read_text(encoding='utf8'))


