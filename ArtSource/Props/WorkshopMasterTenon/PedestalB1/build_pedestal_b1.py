"""Build the B1 retrieval pedestal in metres, then bake the relief into Unity meshes."""
from pathlib import Path
from math import pi
import bpy, bmesh, math, json, sys
import numpy as np
from mathutils import Vector

out=Path(sys.argv[-1]) if '--' in sys.argv else Path(__file__).parent
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=8
scene.cycles.use_denoising=True

def active(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[-1]

def polish(mat,name,colours,metal,rough,scale,fine=.00022):
    mat.name=name
    mat.diffuse_color=(*colours[1],1)
    mat.use_nodes=True
    n=mat.node_tree.nodes
    link=mat.node_tree.links.new
    p=n.get('Principled BSDF')
    p.inputs['Metallic'].default_value=float(metal) if isinstance(metal,(int,float)) else .5
    p.inputs['Roughness'].default_value=rough
    coords=n.new('ShaderNodeTexCoord')
    broad=n.new('ShaderNodeTexNoise')
    broad.inputs['Scale'].default_value=scale
    broad.inputs['Detail'].default_value=3.4
    broad.inputs['Roughness'].default_value=.72
    link(coords.outputs['Object'],broad.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[-1])
    for i,point in enumerate((.26,.50,.74)):
        el=ramp.color_ramp.elements[0] if i==0 else ramp.color_ramp.elements.new(point)
        el.position=point
        el.color=(*colours[i],1)
    link(broad.outputs['Fac'],ramp.inputs[0])
    link(ramp.outputs[0],p.inputs['Base Color'])
    grain=n.new('ShaderNodeTexNoise')
    grain.inputs['Scale'].default_value=490
    grain.inputs['Detail'].default_value=2.0
    link(coords.outputs['Object'],grain.inputs['Vector'])
    bump=n.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value=.33
    bump.inputs['Distance'].default_value=fine
    link(grain.outputs['Fac'],bump.inputs['Height'])
    link(bump.outputs['Normal'],p.inputs['Normal'])
    if isinstance(metal,tuple):
        oxide=n.new('ShaderNodeMapRange')
        oxide.inputs['From Min'].default_value=.26
        oxide.inputs['From Max'].default_value=.72
        oxide.inputs['To Min'].default_value=metal[0]
        oxide.inputs['To Max'].default_value=metal[1]
        link(broad.outputs['Fac'],oxide.inputs['Value'])
        link(oxide.outputs['Result'],p.inputs['Metallic'])
    mat['Rough']=rough
    return mat

def material(name,*args):
    return polish(bpy.data.materials.new(name),name,*args)

bronze=material('旧铜 · 灰绿包浆',[(.046,.076,.062),(.109,.145,.112),(.182,.169,.116)],(.33,.74),.53,34)
edge=material('旧铜 · 手磨凸边',[(.178,.105,.050),(.37,.235,.118),(.49,.337,.18)],(.30,.73),.43,36,.00015)
dark=material('旧铜 · 阴刻与滑槽',[(.024,.039,.032),(.051,.07,.058),(.093,.101,.075)],(.28,.62),.65,45)
stone=material('浅灰赭石',[(.055,.043,.031),(.112,.091,.067),(.196,.164,.122)],0,.79,44,.00052)
deck=material('旧铜 · 盘内磨面',[(.061,.048,.036),(.108,.091,.069),(.195,.153,.105)],.27,.72,56,.00042)
jade=material('暖白玉磨条',[(.27,.222,.156),(.47,.415,.313),(.66,.591,.467)],0,.37,37,.000085)

def mesh_object(name,verts,faces,mats,slots=None):
    me=bpy.data.meshes.new(name)
    me.from_pydata(verts,[],faces)
    me.update()
    for mat in mats:me.materials.append(mat)
    if slots:
        for i,m in enumerate(slots):me.polygons[i].material_index=m
    obj=bpy.data.objects.new(name,me)
    scene.collection.objects.link(obj)
    return obj

def clean(obj):
    b=bmesh.new()
    b.from_mesh(obj.data)
    bmesh.ops.remove_doubles(b,verts=list(b.verts),dist=.000012)
    bmesh.ops.recalc_face_normals(b,faces=list(b.faces))
    bmesh.ops.triangulate(b,faces=list(b.faces),quad_method='BEAUTY')
    b.to_mesh(obj.data)
    b.free()
    obj.data.update()

def ring(distance,corner,height):
    t=distance
    c=min(corner,t*.44)
    return [(-t+c,-t,height),(t-c,-t,height),(t,-t+c,height),(t,t-c,height),
            (t-c,t,height),(-t+c,t,height),(-t,t-c,height),(-t,-t+c,height)]

def profile(name,levels):
    """Levels progress along the cross section from underside to inside floor."""
    verts=[]
    faces=[]
    mats=[bronze,edge,deck,dark]
    slots=[]
    for z,h,c,slot in levels:verts.extend(ring(h,c,z))
    faces.append(tuple(range(7,-1,-1)));slots.append(levels[0][3])
    for j in range(len(levels)-1):
        for i in range(8):
            ni=(i+1)%8
            faces.append((j*8+i,j*8+ni,(j+1)*8+ni,(j+1)*8+i))
            slots.append(levels[j+1][3])
    j=len(levels)-1
    faces.append(tuple(j*8+i for i in range(8)));slots.append(levels[j][3])
    o=mesh_object(name,verts,faces,mats,slots)
    clean(o)
    return o

def bevel_box(name,size,origin,mat,width=.0018):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o=bpy.context.object
    o.name=name
    o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for v in o.data.vertices:v.co+=Vector(origin)
    o.data.materials.append(mat)
    if width:
        bevel=o.modifiers.new('Soft cast edge','BEVEL')
        bevel.width=width
        bevel.segments=2
        bevel.affect='EDGES'
        bpy.ops.object.modifier_apply(modifier=bevel.name)
    clean(o)
    return o

def front_prism(name,outline,face_y,depth,mat,rotation=0,bevel=.0016):
    # outline is in the panel plane, y can be constant or a function of z.
    fn=face_y if callable(face_y) else lambda z:face_y
    count=len(outline)
    verts=[(x,fn(z)+offset,z) for offset in (0,depth) for x,z in outline]
    faces=[tuple(reversed(range(count))),tuple(range(count,2*count))]
    faces.extend((i,(i+1)%count,(i+1)%count+count,i+count) for i in range(count))
    if rotation:
        c,s=math.cos(rotation),math.sin(rotation)
        verts=[(c*x-s*y,s*x+c*y,z) for x,y,z in verts]
    o=mesh_object(name,verts,faces,[mat])
    if bevel:
        active([o])
        b=o.modifiers.new('Cast relief bevel','BEVEL'); b.width=bevel; b.segments=2
        bpy.ops.object.modifier_apply(modifier=b.name)
    clean(o)
    return o

def rounded_rect(cx,cz,hw,hh,cut):
    return [(cx-hw+cut,cz-hh),(cx+hw-cut,cz-hh),(cx+hw,cz-hh+cut),(cx+hw,cz+hh-cut),
            (cx+hw-cut,cz+hh),(cx-hw+cut,cz+hh),(cx-hw,cz+hh-cut),(cx-hw,cz-hh+cut)]

body=[]
detail=[]
def include(o, high_only=False):
    (detail if high_only else body).append(o)
    return o

# Four heavy corner shoes with a real front toe opening.
for i in (-1,1):
    for j in (-1,1):
        include(bevel_box('角脚踏阶',(.151,.147,.082),(i*.3495,j*.3495,.041),bronze,.008))
        include(bevel_box('角脚细唇',(.164,.163,.023),(i*.337,j*.337,.079),edge,.003))
# The thin plinth forms the arch above the open toe gap.
include(profile('低阶承重座',[(.071,.378,.018,0),(.078,.412,.023,0),(.094,.420,.022,1),
    (.109,.405,.019,0),(.125,.377,.019,0)]))
include(profile('微收器身',[(.103,.371,.019,0),(.118,.376,.019,1),(.485,.315,.018,0),
    (.510,.329,.018,0),(.553,.348,.017,0)]))
include(profile('方盘及实心低窝',[(.549,.344,.017,0),(.572,.354,.017,0),(.607,.386,.021,0),
    (.626,.418,.024,0),(.635,.425,.024,1),(.645,.422,.024,1),(.650,.414,.022,1),
    (.650,.390,.018,0),(.643,.386,.017,1),(.620,.383,.017,0),(.614,.376,.016,0),
    (.613,.073,.006,2),(.604,.069,.005,3),(.586,.067,.004,3),(.584,.060,.004,3)]))

for side in range(4):
    turn=side*pi/2
    def plane(z):return -(.374-(z-.12)*.158)
    def band(name,pts,face,depth,mat,bevel=.002,only=False):
        return include(front_prism(name,pts,face,depth,mat,turn,bevel),only)
    # Broad matte recessed stone and bronze rails, without face-on rectangular seams.
    field=[(-.277,.141),(.277,.141),(.284,.149),(.240,.449),(.231,.460),(-.231,.460),(-.240,.449),(-.284,.149)]
    band('内收石纹大面',field,lambda z:plane(z)-.004,.017,stone,.0028)
    for sign in (-1,1):
        rail=[(.270,.123),(.324,.123),(.278,.493),(.228,.493)]
        band('斜收铜护棱',[(sign*x,z) for x,z in rail],lambda z:plane(z)-.013,.028,bronze,.005)
    band('暖白玉准条',rounded_rect(0,.455,.233,.009,.005),lambda z:plane(z)-.011,.015,jade,.0022)
    band('横边校准',rounded_rect(0,.137,.287,.006,.003),lambda z:plane(z)-.012,.016,edge,.0015)
    band('方彝上裙',rounded_rect(0,.540,.317,.049,.011),-.371,.035,bronze,.0038)
    band('上裙下沿',rounded_rect(0,.496,.311,.004,.002),-.377,.012,edge,.0012)
    band('上裙上沿',rounded_rect(0,.581,.315,.0035,.0018),-.376,.014,edge,.0011)
    band('纹样凹底',rounded_rect(0,.540,.302,.034,.005),-.3735,.006,dark,.0014)
    if side:
        # Smaller low thunder patterns at the corners, enough negative space for the front to read first.
        for sign in (-1,1):
            for row in (-1,1):
                offset=sign*.235
                square=[(.029,-.011),(-.021,-.011),(-.021,.011),(.023,.011),(.023,-.003),(-.009,-.003),(-.009,.004),(.010,.004)]
                for p,q in zip(square,square[1:]):
                    x0,z0=offset+sign*p[0],.539+row*.016+p[1]*.67
                    x1,z1=offset+sign*q[0],.539+row*.016+q[1]*.67
                    a=Vector((x1-x0,z1-z0)).normalized()
                    n=Vector((-a.y,a.x))*.0017
                    band('边雷纹',[(x0+n.x,z0+n.y),(x1+n.x,z1+n.y),(x1-n.x,z1-n.y),(x0-n.x,z0-n.y)],-.376,.005,edge,.0006,True)

# High relief follows a hand-drawn geometric mask. Its heavy brow and hooked
# scrolls are baked; the silhouette of the casting remains on the low mesh.
def ribbons(path,width,face=-.384,depth=.013):
    for i,(p,q) in enumerate(zip(path,path[1:])):
        a=Vector(q)-Vector(p)
        if a.length<.0005:continue
        a.normalize()
        n=Vector((-a.y,a.x))*width*.5
        p=Vector(p)-a*width*.22
        q=Vector(q)+a*width*.22
        include(front_prism('铸铜浮纹', [tuple(p+n),tuple(q+n),tuple(q-n),tuple(p-n)],face,depth,edge,0,.00165),True)

for sign in (-1,1):
    def strokes(points,d):ribbons([(sign*x,.542+z) for x,z in points],d)
    strokes([(.027,-.003),(.042,.021),(.070,.027),(.115,.026),(.119,.012),(.090,.012),(.089,.005),(.124,.007),(.148,.015),(.168,.026),(.203,.028)],.0125)
    strokes([(.025,-.010),(.045,-.018),(.081,-.019),(.082,-.007),(.056,-.008),(.054,.001),(.078,.004)],.010)
    strokes([(.096,-.014),(.123,-.017),(.142,-.025),(.182,-.024),(.183,-.008),(.169,-.010),(.168,-.016)],.010)
    for row in (-1,1):
        for col in (0,1):
            cx=.224+col*.038
            cz=row*.016
            strokes([(cx+.014,cz-.009),(cx-.014,cz-.009),(cx-.014,cz+.009),(cx+.014,cz+.009),
                     (cx+.014,cz-.002),(cx-.005,cz-.002),(cx-.005,cz+.004),(cx+.005,cz+.004)],.0037)
    include(front_prism('兽目铜铸眼窝',rounded_rect(sign*.072,.549,.012,.007,.003),
                        -.384,.011,bronze,0,.0014),True)
    include(front_prism('兽目点',rounded_rect(sign*.074,.549,.003,.003,.001),
                        -.3865,.005,edge,0,.0009),True)
include(front_prism('兽鼻纵脊',[(-.014,.517),(.014,.517),(.018,.570),(.008,.579),(-.008,.579),(-.018,.570)],
                    -.3855,.014,edge,0,.003),True)
ribbons([(-.024,.530),(-.015,.522),(.015,.522),(.024,.530)],.0046,-.385,.013)
# Quiet square curls flank the toe opening.
for sign in (-1,1):
    x=sign*.304
    ribbons([(x+.013,.099),(x-.013,.099),(x-.013,.125),(x+.013,.125),(x+.013,.108),(x-.005,.108),
             (x-.005,.118),(x+.005,.118)],.004,-.406,.013)

# Guides run in X. The slightly recessed dark line lies between raised slide rails.
for sign in (-1,1):
    include(bevel_box('滑槽暗底',(.198,.026,.005),(sign*.177,0,.613),dark,.001))
    for d in (-1,1):
        include(bevel_box('滑槽唇',(.192,.0045,.008),(sign*.174,d*.017,.6145),bronze,.0014))
for sign in (-1,1):
    include(bevel_box('暖白玉口左右',(.013,.156,.018),(sign*.077,0,.6275),jade,.0025))
    include(bevel_box('暖白玉口前后',(.143,.013,.018),(0,sign*.077,.6275),jade,.0025))
    # Two dark load lands stay below the fixed .653 holder and take the old whitebox part.
    include(bevel_box('榫底低托脚',(.089,.018,.071),(0,sign*.036,.6144),dark,.0016))

pin_parts=[
    bevel_box('铜销底滑块',(.074,.030,.016),(0,0,.008),bronze,.003),
    bevel_box('铜销顶盖',(.054,.042,.018),(0,0,.021),bronze,.0045),
    bevel_box('伸出销舌',(.034,.025,.011),(-.038,0,.009),edge,.002),
    bevel_box('铜销顶面暗槽',(.025,.006,.0045),(.003,0,.0307),dark,.00055)
]
pin_detail=[]
for dz in (-.009,.009):
    pin_detail.append(bevel_box('铜销细刻',(.024,.002,.00095),(0,dz,.0303),edge,.00043))

def copy_mesh(o):
    n=o.copy();n.data=o.data.copy();scene.collection.objects.link(n);return n
def join_mesh(name,objects):
    active(objects)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name
    obj.data.name=name
    return obj
def low_pair(label,parts,fine):
    high=join_mesh(label+'_High',[copy_mesh(o) for o in parts]+list(fine))
    low=join_mesh(label,list(parts))
    active([low])
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.17,island_margin=.009,area_weight=.05,correct_aspect=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    return low,high

stand,stand_h=low_pair('PedestalBody',body,detail)
pin,pin_h=low_pair('BronzePin',pin_parts,pin_detail)

def output_image(name,size,linear=False):
    img=bpy.data.images.new(name,width=size,height=size,alpha=True,float_buffer=False)
    img.colorspace_settings.name='Non-Color' if linear else 'sRGB'
    return img
def dest(img,file):
    img.filepath_raw=str(out/file);img.file_format='PNG';img.save()
    return img
def pick_image(obj,img):
    for mat in obj.data.materials:
        nodes=mat.node_tree.nodes
        for n in nodes:n.select=False
        n=nodes.get('Bake target') or nodes.new('ShaderNodeTexImage')
        n.name='Bake target';n.image=img;n.select=True;nodes.active=n

def set_mask_emission(high,enable):
    for mat in high.data.materials:
        ns=mat.node_tree.nodes
        p=ns.get('Principled BSDF')
        err=ns.get('Mask output')
        if not err:
            err=ns.new('ShaderNodeEmission');err.name='Mask output'
            pack=ns.new('ShaderNodeCombineColor')
            pack.inputs['Red'].default_value=p.inputs['Metallic'].default_value
            if p.inputs['Metallic'].is_linked:
                mat.node_tree.links.new(p.inputs['Metallic'].links[0].from_socket,pack.inputs['Red'])
            pack.inputs['Green'].default_value=1.-float(mat['Rough'])
            pack.inputs['Blue'].default_value=1.
            mat.node_tree.links.new(pack.outputs['Color'],err.inputs['Color'])
        target=ns.get('Material Output').inputs['Surface']
        mat.node_tree.links.new((err if enable else p).outputs[0],target)

def baked_material(label,color,normal,orm):
    mat=bpy.data.materials.new(label+'Atlas')
    mat.use_nodes=True
    nd=mat.node_tree.nodes
    l=mat.node_tree.links.new
    p=nd.get('Principled BSDF')
    for i,img in enumerate([color,normal,orm]):
        nm=nd.new('ShaderNodeTexImage');nm.image=img;nm.location=(-550,220-i*290)
        if i==0:l(nm.outputs['Color'],p.inputs['Base Color'])
        elif i==1:
            n=nd.new('ShaderNodeNormalMap')
            l(nm.outputs['Color'],n.inputs['Color']);l(n.outputs['Normal'],p.inputs['Normal'])
        else:
            s=nd.new('ShaderNodeSeparateColor')
            l(nm.outputs['Color'],s.inputs[0]);l(s.outputs['Green'],p.inputs['Roughness']);l(s.outputs['Blue'],p.inputs['Metallic'])
    return mat

def bake(label,low,high,size):
    settings=scene.render.bake
    settings.use_selected_to_active=True
    settings.cage_extrusion=.015 if size==2048 else .0055
    settings.max_ray_distance=.032 if size==2048 else .014
    settings.margin=11 if size==2048 else 7
    settings.use_clear=True
    images={}
    for suffix,kind in [('BaseColor','DIFFUSE'),('NormalGL','NORMAL'),('LinearProperties','EMIT')]:
        img=output_image(label+suffix,size,suffix!='BaseColor')
        pick_image(low,img)
        active([high,low])
        if kind=='DIFFUSE':
            settings.use_pass_direct=False;settings.use_pass_indirect=False;settings.use_pass_color=True
        if kind=='EMIT':set_mask_emission(high,True)
        bpy.ops.object.bake(type=kind)
        if kind=='EMIT':set_mask_emission(high,False)
        if kind!='EMIT':dest(img,label+'_'+suffix+'.png')
        images[suffix]=img
    q=np.empty(size*size*4,dtype=np.float32)
    images['LinearProperties'].pixels.foreach_get(q)
    q=q.reshape(-1,4)
    alpha=q[:,3]
    assert float(np.max(q[:,0]))>.6 and float(np.max(alpha))>.9,'Bake mask came back blank'
    orm=np.ones_like(q)
    orm[:,1]=1.-q[:,1]
    orm[:,2]=q[:,0]
    mask=np.ones_like(q)
    mask[:,0]=q[:,0]
    mask[:,3]=q[:,1]
    orm_image=output_image(label+'ORM',size,True)
    orm_image.pixels.foreach_set(orm.ravel())
    dest(orm_image,label+'_ORM.png')
    unity_image=output_image(label+'Mask',size,True)
    unity_image.pixels.foreach_set(mask.ravel())
    dest(unity_image,label+'_Mask.png')
    one_mat=baked_material(label,images['BaseColor'],images['NormalGL'],orm_image)
    low.data.materials.clear();low.data.materials.append(one_mat)
    for p in low.data.polygons:p.material_index=0
    return {'pixels':size,'bakeCoverage':float((alpha>.5).mean()),'cageM':settings.cage_extrusion,
            'rayDistanceM':settings.max_ray_distance,'materialSlots':1}

r={}
for label,lo,hi,size in [('B1Body',stand,stand_h,2048),('B1Pin',pin,pin_h,512)]:
    r[label]=bake(label,lo,hi,size)
    for o,k in [(lo,'low'),(hi,'high')]:
        o.data.calc_loop_triangles()
        r[label][k+'Triangles']=len(o.data.loop_triangles)
    print('BAKED',label,r[label],flush=True)
    active([lo])
    bpy.ops.export_scene.fbx(filepath=str(out/(label+'.fbx')),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,
        apply_scale_options='FBX_SCALE_ALL',use_tspace=True,bake_anim=False)

for o in [stand_h,pin_h]:
    o.hide_render=True;o.hide_set(True)
    o['role']='editable high-poly baking source'
root=bpy.data.objects.new('取用台 B1',None);scene.collection.objects.link(root)
stand.parent=root
pin_copy=copy_mesh(pin)
pin_copy.name='LeftSlidingPin';pin.name='RightSlidingPin'
for obj,sign in ((pin,1),(pin_copy,-1)):
    obj.parent=root
    obj.rotation_euler.z=0 if sign>0 else pi
    for f,z in [(1,.121),(35,.121),(57,.207),(95,.207)]:
        obj.location=(sign*z,0,.616);obj.keyframe_insert(data_path='location',frame=f)
scene.frame_end=95
scene.render.fps=30
scene.frame_set(1)
active([root,stand,pin,pin_copy])
bpy.ops.export_scene.gltf(filepath=str(out/'Pedestal_B1.glb'),use_selection=True,
    export_format='GLB',export_extras=True,export_cameras=False,export_lights=False,
    export_tangents=True,export_animation_mode='SCENE',export_anim_scene_split_object=False)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Pedestal_B1.blend'),compress=True)

def shot(name,pos,target,scale):
    c=bpy.data.cameras.new(name)
    camera=bpy.data.objects.new(name,c);scene.collection.objects.link(camera)
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    c.type='ORTHO';c.ortho_scale=scale
    scene.camera=camera;scene.render.filepath=str(out/(name+'.jpg'))
    bpy.ops.render.render(write_still=True)
def light(name,pos,target,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    ob=bpy.data.objects.new(name,d);scene.collection.objects.link(ob)
    ob.location=pos;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()

scene.world=bpy.data.worlds.new('Neutral stone studio');scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background')
bg.inputs[0].default_value=(.235,.24,.245,1);bg.inputs[1].default_value=.42
light('Large warm softbox',(-1.30,-1.8,2.0),(0,0,.33),190,1.45,(1,.92,.78))
light('Low relief rake',(1.5,-.75,1.10),(0,-.25,.51),85,.78,(.79,.88,1))
light('Copper rim',(1.28,1.05,1.68),(0,0,.42),135,1.25,(1,.88,.67))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.0015))
floor=bpy.context.object
m=bpy.data.materials.new('Matte photo ground');m.diffuse_color=(.16,.146,.121,1);floor.data.materials.append(m)
scene.view_settings.view_transform='AgX'
scene.render.resolution_x=1080;scene.render.resolution_y=920
scene.render.image_settings.file_format='JPEG';scene.render.image_settings.quality=92
scene.cycles.samples=24
scene.frame_set(1)
shot('Hero',(-1.40,-2.12,1.65),(0,0,.31),1.32)
shot('FrontRelief',(0,-2.1,.63),(0,-.32,.529),.72)
shot('LockedTop',(-.57,-1.13,2.15),(0,0,.59),1.01)
scene.frame_set(85)
shot('OpenTop',(-.57,-1.13,2.15),(0,0,.59),1.01)
r['boundsMeters']=[.85,.85,.65]
r['blenderVersion']=bpy.app.version_string
r['localUnityHolderY']=.653
r['xPositions']={'locked':.121,'open':.207}
r['brush']='Procedural copper weathering with actual selected-to-active Cycles normal, diffuse and material property bakes'
(out/'BakeReport.json').write_text(json.dumps(r,indent=2),encoding='utf8')
