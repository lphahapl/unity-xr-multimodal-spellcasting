"""Make the high study, then rebuild its silhouette by hand. Run with Blender in background."""
from pathlib import Path
from math import sin, cos, pi, copysign
import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
WORK = ROOT / "Logs/chapter01-wakecot"
ASSETS = ROOT / "Assets/Art/Environment/Opening/WakeCot"
TEX = ASSETS / "Textures"
WORK.mkdir(parents=True,exist_ok=True)
TEX.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.render.engine = "CYCLES"
scene.cycles.samples = 6
scene.render.threads_mode = "FIXED"
scene.render.threads = 10
lowmat = bpy.data.materials.new("WakeCot_Baked")
lowmat.use_nodes = True
materials = {}

def surface(name,a,b,freq,gloss):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*b,1)
    nt = mat.node_tree
    bs = next(n for n in nt.nodes if n.type=="BSDF_PRINCIPLED")
    bs.inputs["Roughness"].default_value=1-gloss
    pos=nt.nodes.new("ShaderNodeAttribute");pos.attribute_name="CotRestPosition"
    scale=nt.nodes.new("ShaderNodeVectorMath");scale.operation="MULTIPLY"
    nt.links.new(pos.outputs["Vector"],scale.inputs[0]);scale.inputs[1].default_value=freq
    noise=nt.nodes.new("ShaderNodeTexNoise");noise.inputs["Scale"].default_value=3.8
    noise.inputs["Detail"].default_value=2.4;noise.inputs["Distortion"].default_value=.37
    nt.links.new(scale.outputs[0],noise.inputs[0])
    color=nt.nodes.new("ShaderNodeValToRGB");color.name="Pigment"
    ramp=color.color_ramp
    ramp.elements[0].position=.22;ramp.elements[0].color=(*a,1)
    ramp.elements[1].position=.79;ramp.elements[1].color=(*b,1)
    nt.links.new(noise.outputs["Fac"],color.inputs[0]);nt.links.new(color.outputs[0],bs.inputs["Base Color"])
    bump=nt.nodes.new("ShaderNodeBump");bump.inputs["Strength"].default_value=.21
    bump.inputs["Distance"].default_value=.00055
    nt.links.new(noise.outputs["Fac"],bump.inputs["Height"]);nt.links.new(bump.outputs[0],bs.inputs["Normal"])
    materials[mat]=(bs,color,gloss)
    return mat

elm_a=(.055,.020,.007);elm_b=(.248,.104,.037)
elm_x=surface("01 | waxed elm, long rail",elm_a,elm_b,(1.1,38,67),.34)
elm_y=surface("02 | waxed elm, cross rail",elm_a,elm_b,(36,1.1,65),.34)
elm_z=surface("03 | waxed elm, hoof foot",elm_a,elm_b,(51,41,1.4),.34)
peg=surface("04 | end grain, driven square peg",(.037,.013,.004),(.141,.052,.016),(39,11,27),.28)
reed=surface("05 | split rush, warp",(.230,.135,.046),(.590,.422,.169),(38,1.6,30),.18)
weft=surface("06 | split rush, weft",(.185,.106,.039),(.455,.306,.105),(2,43,30),.19)
linen=surface("07 | washed indigo linen",(.021,.054,.059),(.086,.167,.163),(31,70,55),.14)
flax=surface("08 | unbleached binding",(.264,.175,.084),(.513,.368,.171),(74,35,55),.16)
stripe=surface("09 | darker indigo selvage",(.010,.024,.031),(.037,.070,.081),(49,59,43),.13)

def set_active(o):
    bpy.ops.object.select_all(action="DESELECT");o.select_set(True);bpy.context.view_layer.objects.active=o

def apply(o,mod):
    set_active(o);bpy.ops.object.modifier_apply(modifier=mod.name)

def bevel(o,r,segments):
    if r:
        mod=o.modifiers.new("Hand eased arris","BEVEL");mod.width=r;mod.segments=segments
        apply(o,mod)

def mesh_object(name,verts,faces,ids,slots,smooth=False):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob)
    for m in slots:mesh.materials.append(m)
    for f,i in zip(mesh.polygons,ids):f.material_index=i;f.use_smooth=smooth
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
    return ob

def box(name,loc,dim,mat,r,segments):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    ob=bpy.context.object;ob.name=name;ob.dimensions=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(mat);bevel(ob,r,segments);return ob

def loft(name,rings,ids,slots,smooth=False):
    width=len(rings[0]);verts=[p for ring in rings for p in ring];faces=[];mats=[]
    for i in range(len(rings)-1):
        for j in range(width):
            j2=(j+1)%width;faces.append((i*width+j,i*width+j2,(i+1)*width+j2,(i+1)*width+j));mats.append(ids(i,j))
    faces += [tuple(reversed(range(width))),tuple((len(rings)-1)*width+j for j in range(width))]
    mats += [0,0];return mesh_object(name,verts,faces,mats,slots,smooth)

def leg(x,y,high):
    # Four square hooves turn out just above the floor, with a chamfered shoulder.
    profile=[(0,.061,.049),(.018,.062,.047),(.032,.060,.043),(.052,.054,.028),
             (.092,.045,.014),(.145,.047,0),(.350,.056,0),(.416,.064,0),(.526,.064,0)]
    if not high:profile=[profile[i] for i in [0,2,4,5,8]]
    corners=[(-1,-.80),(-.80,-1),(.80,-1),(1,-.80),(1,.80),(.80,1),(-.80,1),(-1,.80)]
    rings=[[(x+copysign(p,x)+u*w,y+copysign(p*.58,y)+v*w,z) for u,v in corners] for z,w,p in profile]
    return loft("Hoof leg",rings,lambda i,j:0,[elm_z if high else lowmat])

def apron(axis,s,high):
    length=1.14 if axis=="X" else .443
    # Restrained bracket apron, broad flat middle and curved shoulders around the posts.
    ts=[-1,-.92,-.86,-.79,-.72,-.57,-.4,0,.4,.57,.72,.79,.86,.92,1]
    if not high:ts=[-1,-.92,-.79,-.64,0,.64,.79,.92,1]
    rings=[]
    for t in ts:
        amp=max(0,min(1,(abs(t)-.62)/.30));bottom=.440-.083*amp*amp*(3-2*amp)
        pairs=[(-.027,bottom),(.027,bottom),(.027,.525),(-.027,.525)]
        rings.append([(length*t,s*.423+across,z) if axis=="X" else
                      (s*1.104+across,length*t,z) for across,z in pairs])
    ob=loft("Bracket apron",rings,lambda i,j:0,[elm_x if axis=="X" else elm_y] if high else [lowmat])
    if high:bevel(ob,.004,3)
    return ob

def mat_surface(high):
    # Relief and two orientations of actual rush. Twill reverses at each broad chevron.
    nx,ny=(342,138) if high else (8,4)
    verts=[];faces=[];mats=[]
    for j in range(ny+1):
        b=j/ny*2-1
        for i in range(nx+1):
            a=i/nx*2-1
            x=a*(1.157-.019*abs(b)**18);y=b*(.455-.020*abs(a)**28)
            edge=max(abs(a),abs(b))
            z=.605+.005*(1-a*a)*(1-b*b) - .006*(abs(a)**18+abs(b)**16)
            # An over/under crossing approx every 20 mm, with separate flatter border binding.
            if high:
                xx=(x+1.16)*49;yy=(y+.46)*47
                trend=1 if int((x+1.17)/.242)%2==0 else -1
                stagger=int(xx)+trend*int(yy)
                warp=(stagger%4)<2
                crown=sin(pi*(xx%1)) if warp else sin(pi*(yy%1))
                z += .0021*crown if edge<.945 else .0007*sin(x*280+y*290)
            verts.append((x,y,z))
            if i<nx and j<ny:
                k=j*(nx+1)+i
                faces.append((k,k+1,k+nx+2,k+nx+1))
                if not high: mid=0
                else:
                    aa=(i+.5)/nx*2-1;bb=(j+.5)/ny*2-1;ed=max(abs(aa),abs(bb))
                    xx=(aa*1.157+1.16)*49;yy=(bb*.455+.46)*47
                    turn=1 if int((aa*1.157+1.17)/.242)%2==0 else -1
                    mid=0 if (int(xx)+turn*int(yy))%4<2 else 1
                    if ed>.945:mid=2 if ed>.968 else 3
                    if .868<abs(aa)<.900:mid=2
                    if .84<abs(aa)<.851:mid=3
                mats.append(mid)
    perimeter=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]+[ny*(nx+1)+i for i in reversed(range(nx))]+[j*(nx+1) for j in reversed(range(1,ny))]
    start=len(verts)
    for i,k in enumerate(perimeter):
        x,y,z=verts[k];verts.append((x*.999,y*.997,.568))
        f=(i+1)%len(perimeter);faces.append((k,perimeter[f],start+f,start+i));mats.append(2 if high else 0)
    # Fan to centre keeps Blender tessellation predictable even around the rounded corners.
    centre=len(verts);verts.append((0,0,.568))
    for i in range(len(perimeter)):
        faces.append((centre,start+(i+1)%len(perimeter),start+i));mats.append(0)
    return mesh_object("Bound rush mat",verts,faces,mats,[reed,weft,linen,flax] if high else [lowmat])

def bolster(high):
    # A low stuffed cloth bolster: pinched ends, softened ridge and narrow woven end bands.
    sides,rows=(80,90) if high else (12,7)
    rings=[]
    for i in range(rows+1):
        t=i/rows*2-1;at=abs(t)
        taper=1-.20*at**7+.046*cos(at*16)*at**4
        ring=[]
        for j in range(sides):
            a=j*2*pi/sides;u=copysign(abs(cos(a))**.70,cos(a));v=copysign(abs(sin(a))**.74,sin(a))
            wave=1+( .052*cos(a*7+.46*t)+.021*cos(a*11-t))*at**4
            if high:wave+=.016*cos(12*a+1.1*sin(t*4))*(.25+at*.75)
            # Blender +X imports to Unity -X with this project's standard FBX basis.
            ring.append((.685+.229*u*taper*wave+.013*t,.341*t,.662+.069*v*taper*wave+.003*cos(t*3)))
        rings.append(ring)
    def band(i,j):
        if not high:return 0
        t=abs((i+.5)/rows*2-1)
        if .790<t<.820 or .894<t<.923:return 1
        if .833<t<.885:return 2
        if .755<t<.78:return 2 if (j//3)%2 else 1
        return 0
    return loft("Indigo bolster",rings,band,[linen,flax,stripe] if high else [lowmat],True)

def create_tier(high):
    group=bpy.data.collections.new("HIGH | editable parts" if high else "LOW | final atlas")
    scene.collection.children.link(group);objects=[]
    def put(obj,name):
        obj.name=name+(" HIGH" if high else " LOW")
        for c in tuple(obj.users_collection):c.objects.unlink(obj)
        group.objects.link(obj);objects.append(obj)
        set_active(obj);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        mesh=obj.data
        tag=mesh.attributes.new("CotBakePart","INT","POINT")
        tag.data.foreach_set("value",[len(objects)]*len(mesh.vertices))
        rest=mesh.attributes.new("CotRestPosition","FLOAT_VECTOR","POINT")
        rest.data.foreach_set("vector",[v for pt in mesh.vertices for v in pt.co])
    def stock(name,xyz,dims,mat,r,low_bevel=False):
        put(box(name,xyz,dims,mat if high else lowmat,r if high or low_bevel else 0,4 if high else 1),name)
    for sx in [-1,1]:
        for sy in [-1,1]:put(leg(sx*1.103,sy*.417,high),f"Hoof {sx} {sy}")
    for s in [-1,1]:
        put(apron("X",s,high),f"Long bracket apron {s}");put(apron("Y",s,high),f"Short bracket apron {s}")
        stock(f"Long low rail {s}",(0,s*.419,.205),(2.207,.055,.066),elm_x,.004)
        stock(f"Cross low rail {s}",(s*1.10,0,.204),(.055,.94,.064),elm_y,.004)
        stock(f"Long top lip {s}",(0,s*.4825,.541),(2.45,.065,.072),elm_x,.007,True)
        stock(f"Short top lip {s}",(s*1.185,0,.541),(.080,.900,.072),elm_y,.007,True)
        for sx in [-1,1]:
            stock(f"Mortise wedge {s} {sx}",(sx*1.105,s*.452,.463),(.058,.019,.033),peg,.003)
    stock("Supporting deck",(0,0,.539),(2.316,.920,.058),elm_x,.004)
    put(mat_surface(high),"Reed mat");put(bolster(high),"Cloth headrest")
    return objects

highs=create_tier(True)
for ob in highs:ob.hide_set(True);ob.hide_render=True
lows=create_tier(False)
set_active(lows[0])
for ob in lows:ob.select_set(True)
bpy.ops.object.join();low=bpy.context.object;low.name="WakeCot";low.data.name="WakeCot"
low.data.materials.clear();low.data.materials.append(lowmat)
for face in low.data.polygons:face.material_index=0
bpy.ops.object.mode_set(mode="EDIT");bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=.94,island_margin=.014,scale_to_bounds=True)
bpy.ops.object.mode_set(mode="OBJECT")
# Guard the outside edge of the 1K atlas as well as the space between charts.
for loop in low.data.uv_layers.active.data:loop.uv=loop.uv*.988+Vector((.006,.006))
low.data.uv_layers.active.name="CotBakeUV"
apply(low,low.modifiers.new("Fixed bake triangulation","TRIANGULATE"))
low.data.calc_loop_triangles()
print("LOW",len(low.data.loop_triangles),flush=True)
exec((HERE/"bake.py").read_text(encoding="utf8"))
