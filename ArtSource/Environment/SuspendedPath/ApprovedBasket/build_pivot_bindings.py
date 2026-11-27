from pathlib import Path
import bpy, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from math import pi,sin,cos
import bmesh,sys,json
project=Path(sys.argv[sys.argv.index('--')+1]).resolve()
src=project/'ArtSource/Environment/SuspendedPath/ApprovedBasket'
dst=project/'Assets/Art/Environment/SuspendedPath/ApprovedBasket'
bpy.ops.wm.read_factory_settings(use_empty=True)
with bpy.data.libraries.load(str(project/'ArtSource/Environment/SuspendedPath/Pivot/HunyuanPivotTower.blend')) as (a,b):
    b.objects=['HunyuanPivotTower_LOD0']
orig=b.objects[0];objmat=orig.matrix_world
vertices=[objmat @ v.co for v in orig.data.vertices]
tree=BVHTree.FromPolygons(vertices,[p.vertices[:] for p in orig.data.polygons])
bpy.data.objects.remove(orig,do_unlink=True)
bronze=bpy.data.materials.new('HammeredBronze');bronze.diffuse_color=(.32,.21,.105,1);bronze.use_nodes=True
from pathlib import Path
node=bronze.node_tree.nodes.new('ShaderNodeTexImage')
node.image=bpy.data.images.load(str(project/'Assets/Art/Environment/Opening/PreqinGateFittings/GateBronze_BaseColor.png'));node.image.pack()
bs=next(x for x in bronze.node_tree.nodes if x.type=='BSDF_PRINCIPLED')
bronze.node_tree.links.new(node.outputs[0],bs.inputs['Base Color']);bs.inputs['Metallic'].default_value=.70
lods=[];stats={}
def closest(n,t,s,z):
    p=n*5+t*s+Vector((0,0,z))
    hit,_,_,_=tree.ray_cast(p,-n,12)
    if hit is None:raise RuntimeError(('surface missing',s,z))
    return hit
for tier in (-1,0,1,2):
    verts=[];faces=[];coord=[]
    def append_ring_faces(sections,uv_step):
        base=len(verts);K=len(sections[0])
        for sec in sections:verts.extend(sec)
        def face(idx,uv):faces.append(idx);coord.append(uv)
        for it in range(len(sections)-1):
            for i in range(K):
                j=(i+1)%K
                face([base+it*K+i,base+it*K+j,base+(it+1)*K+j,base+(it+1)*K+i],[(i/K,it*uv_step),(float(i+1)/K,it*uv_step),(float(i+1)/K,(it+1)*uv_step),(i/K,(it+1)*uv_step)])
        for it,reverse in ((0,True),(len(sections)-1,False)):
            ix=list(range(K));ix=ix[::-1] if reverse else ix
            face([base+it*K+i for i in ix],[(.5+.48*cos(2*pi*i/K),.5+.48*sin(2*pi*i/K)) for i in ix])
    samples=[70,24,16,11][tier+1]
    for phi in (0,pi*.5,pi,pi*1.5):
        n=Vector((cos(phi),sin(phi),0));t=Vector((-sin(phi),cos(phi),0));up=Vector((0,0,1))
        for side in (-1,1):
            rings=[]
            levels=np.linspace(4.5,46.1,samples)
            def offset(z):return side*(.78-(z-4.5)*.009)
            for z in levels:
                s=offset(z)
                profile=[(-.084,-.011),(.084,-.011),(.084,.037),(.072,.052),(-.072,.052),(-.084,.037)] if tier<=0 else [(-.084,-.011),(.084,-.011),(.084,.05),(-.084,.05)]
                rings.append([closest(n,t,s+d,z)+n*depth for d,depth in profile])
            append_ring_faces(rings,(levels[1]-levels[0])/.6)
            if tier<2:
                for z in np.linspace(5,45.65,21 if tier<0 else (14 if tier==0 else 7)):
                    i=min(len(levels)-2,max(0,int((z-levels[0])/(levels[1]-levels[0]))))
                    w=(z-levels[i])/(levels[i+1]-levels[i])
                    def front(ring):
                        return (ring[3]+ring[4])*.5 if len(ring)==6 else (ring[2]+ring[3])*.5
                    p=front(rings[i]).lerp(front(rings[i+1]),float(w))
                    # Domed rivets penetrate the strap base and keep individual silhouettes at eye level.
                    sides=20 if tier<0 else (8 if tier==0 else 5)
                    sections=[]
                    for rad,depth in ([(0.040,-.008),(.042,.009),(.033,.025),(.014,.036)] if tier<0 else [(.042,-.008),(.032,.023)]):
                        sections.append([p+depth*n+rad*(t*cos(2*pi*k/sides)+up*sin(2*pi*k/sides)) for k in range(sides)])
                    append_ring_faces(sections,.30)
    # Collars conform to the tower shell, including its asymmetric relief.
    for z in (5.0,23.0,44.9):
        slices=[(z-.15,-.132),(z-.15,.101),(z-.121,.127),(z+.121,.127),(z+.15,.101),(z+.15,-.132)]
        seg=96 if tier<0 else [36,24,16][tier]
        b=len(verts)
        for h,depth in slices:
            for k in range(seg):
                phi=k*2*pi/seg;n=Vector((cos(phi),sin(phi),0))
                verts.append(closest(n,Vector((0,0,0)),0,h)+n*depth)
        for j in range(len(slices)):
            jj=(j+1)%len(slices)
            for k in range(seg):
                l=(k+1)%seg
                faces.append([b+j*seg+k,b+j*seg+l,b+jj*seg+l,b+jj*seg+k]);coord.append([(k/seg*18,j*.12),((k+1)/seg*18,j*.12),((k+1)/seg*18,(j+1)*.12),(k/seg*18,(j+1)*.12)])
    mesh=bpy.data.meshes.new('PivotBindings');mesh.from_pydata(verts,[],faces);mesh.materials.append(bronze);uv=mesh.uv_layers.new()
    for f,uvs in zip(mesh.polygons,coord):
        for k,u in zip(f.loop_indices,uvs):uv.data[k].uv=u
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    name='PivotBindings_'+('HIGH' if tier<0 else 'LOD'+str(tier))
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);stats[name]=len(mesh.polygons)
    if tier>=0:lods.append(o)
    else:o.hide_set(True);o.hide_render=True
bpy.ops.object.select_all(action='DESELECT')
for o in lods:o.select_set(True)
bpy.context.view_layer.objects.active=lods[0]
bpy.ops.export_scene.fbx(filepath=str(dst/'PivotBindings.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',use_tspace=True,mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE')
for o in lods[1:]:o.hide_set(True);o.hide_render=True
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(src/'PivotBindings.blend'),compress=True)
(src/'pivot_counts.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
