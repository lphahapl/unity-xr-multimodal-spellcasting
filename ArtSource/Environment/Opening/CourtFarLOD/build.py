"""Make distant meshes, preserving the existing MountainPine and B01 baked UV atlases."""
from pathlib import Path
import bpy,bmesh,sys,math,json
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
import numpy as np
root=Path(__file__).resolve().parents[4];prod=Path(__file__).resolve().parent
def load_mesh(path):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/path))
    ob=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    if ob.matrix_world.determinant()<0:raise ValueError('Negative import transform')
    world=ob.matrix_world.copy();ob.parent=None;ob.data.transform(world);ob.matrix_world=Matrix.Identity(4)
    return ob
def islands(bm):
    visited=set()
    for vertex in bm.verts:
        if vertex in visited:continue
        stack=[vertex];visited.add(vertex);group=[]
        while stack:
            v=stack.pop();group.append(v)
            for e in v.link_edges:
                n=e.other_vert(v)
                if n not in visited:visited.add(n);stack.append(n)
        yield group
def repair(ob,remove_specks=False):
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000015)
    if remove_specks:
        pieces=sorted(islands(bm),key=len,reverse=True)
        # Source includes a few disconnected scan scraps near the soil around the root.
        for piece in pieces[1:]:bmesh.ops.delete(bm,geom=piece,context='VERTS')
    bmesh.ops.dissolve_degenerate(bm,dist=.000002,edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.to_mesh(ob.data);bm.free();ob.data.update()
def simplify(ob,budget):
    donor=ob.data.copy();donor.calc_loop_triangles()
    tree=BVHTree.FromPolygons([v.co for v in donor.vertices],[tuple(t.vertices) for t in donor.loop_triangles],all_triangles=True)
    original_mat=donor.materials[0]
    ob.data.remesh_voxel_size=.14 if budget>100 else .22;ob.data.remesh_voxel_adaptivity=0
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.voxel_remesh()
    repair(ob,True)
    n=len(ob.data.polygons);mod=ob.modifiers.new('Distant canopy reduction','DECIMATE');mod.ratio=min(1,budget/n);mod.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=mod.name);repair(ob,True)
    ob.data.materials.clear();ob.data.materials.append(original_mat);ob.data.update()
    for u in list(ob.data.uv_layers):ob.data.uv_layers.remove(u)
    uv=ob.data.uv_layers.new(name='MountainPineAtlasUV');sourceuv=donor.uv_layers.active
    # Keep every distant triangle within an actual nearby donor atlas island. Holding
    # the three original UV corners prevents a long distant face from crossing atlas seams.
    for face in ob.data.polygons:
        nearest,normal,triindex,d=tree.find_nearest(face.center)
        tr=donor.loop_triangles[triindex];face.material_index=0;face.use_smooth=True
        a,b,c=[sourceuv.data[l].uv.copy() for l in tr.loops]
        for j,li in enumerate(face.loop_indices):uv.data[li].uv=(a,b,c)[j]
    # Pin the outer footprint, crown and ground contact so changing tier does not lift the trunk.
    co=np.empty(len(ob.data.vertices)*3,'f');ob.data.vertices.foreach_get('co',co);p=co.reshape(-1,3);lo=p.min(0);hi=p.max(0)
    targetlo=np.array([-3.45,-1.60,0.0]);targethi=np.array([3.43,1.62,4.83])
    p[:]=(p-lo)/(hi-lo)*(targethi-targetlo)+targetlo;ob.data.vertices.foreach_set('co',co);ob.data.update()
def wall(ob):
    # Keep the four non-tiled source components with their original UVs. Only the
    # high-frequency corrugations are simplified; the underlying triangular coping stays.
    repair(ob);part=ob.copy();part.data=ob.data.copy();bpy.context.collection.objects.link(part)
    for obj,just_roof in ((ob,False),(part,True)):
        bm=bmesh.new();bm.from_mesh(obj.data);roof=set(max(islands(bm),key=len));drop=[v for v in bm.verts if (v in roof)!=just_roof]
        bmesh.ops.delete(bm,geom=drop,context='VERTS');bm.to_mesh(obj.data);bm.free();obj.data.update()
    donor=part.data.copy();donor.calc_loop_triangles();sourceuv=donor.uv_layers.active
    tree=BVHTree.FromPolygons([v.co for v in donor.vertices],[tuple(t.vertices) for t in donor.loop_triangles],all_triangles=True)
    profile=[(-.235,1.358),(.235,1.358),(.235,1.392),(0,1.590),(-.235,1.392)]
    vertices=[(x,y,z) for x in (-.9985,.9985) for y,z in profile];N=len(profile)
    faces=[tuple(reversed(range(N))),tuple(range(N,2*N))]+[(j,(j+1)%N,(j+1)%N+N,j+N) for j in range(N)]
    mesh=bpy.data.meshes.new('Joined pantile silhouette');mesh.from_pydata(vertices,[],faces);mesh.update();mesh.materials.append(donor.materials[0]);part.data=mesh
    repair(part);uv=part.data.uv_layers.new(name=ob.data.uv_layers.active.name)
    for f in part.data.polygons:
        result=tree.find_nearest(f.center);tr=donor.loop_triangles[result[2]]
        for n,li in enumerate(f.loop_indices):uv.data[li].uv=sourceuv.data[tr.loops[n]].uv
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);part.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.join();repair(ob)
def export(ob,name):
    ob.name=name;ob.data.name=name;bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.export_scene.fbx(filepath=str(prod/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    m=ob.data;m.calc_loop_triangles();v=np.array([v.co[:] for v in m.vertices])
    uv=m.uv_layers.active;texcoords=np.array([l.uv[:] for l in uv.data]);assert np.isfinite(texcoords).all()
    tri=np.array([t.vertices[:] for t in m.loop_triangles]);np.savez_compressed(Path(sys.argv[-1])/(name+'.npz'),vertices=v,tri=tri)
    print('FAR_MADE',name,len(tri),v.min(0),v.max(0),flush=True)
    return {'triangles':len(tri),'minimum_blender':v.min(0).tolist(),'maximum_blender':v.max(0).tolist(),'existingUV':uv.name,'uvMin':texcoords.min(0).tolist(),'uvMax':texcoords.max(0).tolist(),'materials':[s.name for s in m.materials]}
outputs={}
for key,budget in [('CourtPineFar',186),('CourtPineHorizon',88),('CourtWallFar',64)]:
    p='Assets/Art/Environment/Opening/'+('CourtyardInfill/B01/WallStraight_LOD2.fbx' if key=='CourtWallFar' else 'MountainPine/MountainPine.fbx')
    obj=load_mesh(p)
    if key=='CourtWallFar':wall(obj)
    else:simplify(obj,budget)
    outputs[key]=export(obj,key);outputs[key]['source']=p
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(prod/(key+'.blend')),compress=True)
(prod/'geometry.json').write_text(json.dumps(outputs,indent=2),encoding='utf8')


