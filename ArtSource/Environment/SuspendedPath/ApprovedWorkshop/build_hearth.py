"""Build two game LODs of the workshop's front-loading masonry forge."""
import bpy,math,random,pathlib,bmesh,json
from mathutils import Vector
root=pathlib.Path(__file__).resolve().parents[4];out=root/'Assets/Art/Environment/SuspendedPath/ApprovedWorkshop'
src=pathlib.Path(__file__).parent; bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
names=['Ashlar','BlackenedClay','ForgedBand','Fuel','Ember']; colors=[(.33,.26,.20),(.22,.14,.09),(.12,.13,.12),(.038,.032,.027),(.8,.2,.045)]
mats=[]
for name,color in zip(names,colors):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);mats.append(m)
def make(lod):
    vs=[]; faces=[]; slots=[]
    def add(v,f,mi):
        s=len(vs);vs.extend(v);faces.extend([[i+s for i in p] for p in f]);slots.extend([mi]*len(f))
    quads=[[3,2,1,0],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]]
    def brick(x,y,z,sx,sy,sz,mat):
        add([(x+i*sx/2,y+j*sy/2,z+k*sz/2) for k in (-1,1) for i,j in [(-1,-1),(1,-1),(1,1),(-1,1)]],quads,mat)
    def taper(z1,z2,x1,y1,x2,y2,yoff,mat):
        v=[(i*dx,j*dy+oy,h) for dx,dy,oy,h in [(x1,y1,0,z1),(x2,y2,yoff,z2)] for i,j in [(-1,-1),(1,-1),(1,1),(-1,1)]]
        add(v,quads,mat)
    def column(rows,x,y,z,w,d,h,mat,jitter):
        rng=random.Random(11)
        for i in range(rows):
            border=rng.uniform(-jitter,jitter)
            left=w*(.51+border);right=w-left
            brick(x-w/2+left/2,y,z+(i+.5)*h/rows,left-.009,d,h/rows-.006,mat)
            brick(x+w/2-right/2,y,z+(i+.5)*h/rows,right-.009,d,h/rows-.006,mat)
    # Slightly splayed stone base. Horizontal recesses separate individual masonry courses.
    brick(0,0,.115,2.08,1.43,.23,0)
    if lod==0:
        column(2,0,0,.225,1.96,1.36,.46,0,.16)
    else:brick(0,0,.455,1.96,1.36,.46,0)
    brick(0,.00,.681,1.98,1.45,.075,2)
    brick(0,.10,.758,1.75,1.12,.11,3)
    # Arched firemouth faces local -Y. Keep solid side and back walls behind the stone arch.
    for k in (-1,1):
        brick(k*.743,.025,.996,.37,1.15,.56,0)
        brick(k*.746,-.558,.991,.40,.175,.58,1)
    brick(0,.471,1.18,1.17,.155,.968,1)
    num=13 if lod==0 else 9; inner=.55;outer=.925;zc=1.26
    for i in range(num):
        lo=i*math.pi/num+.003;hi=(i+1)*math.pi/num-.003
        v=[(rr*math.cos(ang),y,zc+rr*math.sin(ang)) for y in (-.63,.60) for rr,ang in [(inner,lo),(outer,lo),(outer,hi),(inner,hi)]]
        add(v,quads,0 if i%4 else 1)
    # Smoke gathers in an offset hood. Four nested masonry courses end at a hollow, soot-lined flue.
    taper(2.02,2.76,.77,.55,.42,.37,.10,1)
    brick(0,.11,2.72,.90,.81,.105,2)
    tiers=4 if lod==0 else 2
    for j in range(tiers):
        z=2.75+(j+.5)*1.46/tiers; width=.80-j*.014
        brick(0,.10,z,width,.69-j*.013,1.46/tiers+.018,1)
    for side in (-1,1):
        brick(side*.42,.10,4.244,.13,.84,.15,0)
        brick(0,.10+side*.353,4.244,.70,.132,.15,0)
    brick(0,.10,4.223,.745,.632,.018,3)
    # Dark and glowing fractured coals bedded in the firemouth. Clinker rims interrupt the emissive fill.
    rng=random.Random(185)
    for i in range(38 if lod==0 else 16):
        cols=8 if lod==0 else 5
        x=-.48+(i%cols)*.96/(cols-1)+rng.uniform(-.022,.022)
        y=-.46+(i//cols)*.14+rng.uniform(-.024,.024)
        rx=rng.uniform(.072,.108);ry=rng.uniform(.055,.098)
        z=.831+rng.uniform(-.012,.018);rz=rng.uniform(.068,.128);N=7 if lod==0 else 5
        ring=[(x+rx*math.cos(2*math.pi*j/N),y+ry*math.sin(2*math.pi*j/N),z+rng.uniform(-.03,.03)) for j in range(N)]
        vv=ring+[(x-rx*.17,y+ry*.12,z+rz),(x,y,z-rz*.87)]
        s=len(vs);vs.extend(vv)
        for j in range(N):
            nxt=(j+1)%N
            faces.append([s+j,s+nxt,s+N]);slots.append(4 if rng.random()<.35 else 3)
            faces.append([s+nxt,s+j,s+N+1]);slots.append(4 if rng.random()<.68 else 3)
    me=bpy.data.meshes.new('WorkshopHearth_LOD'+str(lod));me.from_pydata(vs,[],faces);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();me.update()
    ob=bpy.data.objects.new(me.name,me);bpy.context.collection.objects.link(ob)
    for m in mats:me.materials.append(m)
    uv=me.uv_layers.new(name='MetricPBR')
    for poly,mi in zip(me.polygons,slots):
        poly.material_index=mi;n=poly.normal;axis=max(range(3),key=lambda j:abs(n[j]))
        for li in poly.loop_indices:
            co=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=((co.y,co.z) if axis==0 else (co.x,co.z) if axis==1 else (co.x,co.y))
    if lod==0:
        bpy.context.view_layer.objects.active=ob;ob.select_set(True)
        bevel=ob.modifiers.new('Stone edge chips','BEVEL');bevel.width=.015;bevel.segments=1;bevel.limit_method='ANGLE';bevel.angle_limit=.65
        bpy.ops.object.modifier_apply(modifier=bevel.name)
    # Collapse tiny bevel corner pairs on narrow stone courses. Give every bevel strip
    # its own metric projection, so that glancing stone faces sample the same photo density.
    mesh=ob.data
    clean=bmesh.new()
    clean.from_mesh(mesh)
    bmesh.ops.remove_doubles(clean,verts=list(clean.verts),dist=.00001)
    bmesh.ops.dissolve_degenerate(clean,edges=list(clean.edges),dist=.00001)
    bmesh.ops.triangulate(clean,faces=[face for face in clean.faces if len(face.verts)>4])
    clean.to_mesh(mesh)
    clean.free()
    mesh.update()
    uv=mesh.uv_layers.active
    for face in mesh.polygons:
        n=face.normal
        axis=max(range(3),key=lambda j:abs(n[j]))
        for li in face.loop_indices:
            co=mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv=((co.y,co.z) if axis==0 else (co.x,co.z) if axis==1 else (co.x,co.y))
    return ob
near,far=make(0),make(1)
mouth=bpy.data.objects.new('MouthFacing',None);mouth.location=(0,-.8,.85);bpy.context.collection.objects.link(mouth)
tip=bpy.data.objects.new('ChimneyExit',None);tip.location=(0,.10,4.31);bpy.context.collection.objects.link(tip)
for ob in bpy.context.scene.objects:ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/'WorkshopHearth.fbx'),use_selection=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='FACE',path_mode='STRIP')
for m,name in zip(mats,['Ashlar','Plaster',None,None,None]):
    if not name:continue
    m.use_nodes=True;t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(root/'Assets/Art/Environment/SuspendedPath/Surfaces/Workshop'/f'{name}_BaseSmooth.png'),check_existing=True)
    m.node_tree.links.new(t.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
far.hide_set(True);far.hide_render=True
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(src/'WorkshopHearth.blend'))
for ob in [near,far]:ob.data.calc_loop_triangles();print(ob.name,len(ob.data.loop_triangles))
