"""Regular-grid retopology of the baked gorge sculpt, with nested density and three LODs.
Blender 5.2; the mirror pair in XY compensates Unity FBX's imported horizontal axes.
"""
from pathlib import Path
import sys,json
import bpy
import numpy as np

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
RUNTIME=ROOT/'Assets/Art/Environment/SuspendedPath/ApprovedGorge'
def make_surface(name,axis,h,material):
    n=len(axis)
    x,z=np.meshgrid(axis,axis)
    xyz=np.stack((-x,-z,h),axis=-1).astype(np.float32)
    j,i=np.meshgrid(np.arange(n-1),np.arange(n-1),indexing='ij')
    a=(j*n+i).ravel()
    tri=np.stack((a,a+1,a+n+1,a,a+n+1,a+n),axis=-1).astype(np.int32).reshape(-1,3)
    mesh=bpy.data.meshes.new(name);mesh.vertices.add(n*n)
    mesh.vertices.foreach_set('co',xyz.ravel());mesh.loops.add(tri.size)
    mesh.loops.foreach_set('vertex_index',tri.ravel());mesh.polygons.add(len(tri))
    mesh.polygons.foreach_set('loop_start',np.arange(len(tri),dtype=np.int32)*3)
    mesh.update(calc_edges=True)
    m=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(m)
    mesh.materials.append(material)
    mesh.polygons.foreach_set('use_smooth',np.ones(len(tri),dtype=bool))
    uv0=mesh.uv_layers.new(name='Baked relief')
    p=np.linspace(0,1,n,dtype=np.float32);u,v=np.meshgrid(p,p)
    uv=np.stack((u,v),axis=-1).reshape(-1,2)
    uv0.uv.foreach_set('vector',uv[tri.ravel()].ravel())
    # Metric top projection for external tools. The Unity shader uses metric
    # triplanar projection for vertical faces in the same 3.2m scale.
    detail=mesh.uv_layers.new(name='Detail 3.2m')
    local=np.stack((x/3.2,z/3.2),axis=-1).reshape(-1,2)
    detail.uv.foreach_set('vector',local[tri.ravel()].ravel())
    mesh.uv_layers.active_index=0
    assert not mesh.validate()
    e1=uv[tri[:,1]]-uv[tri[:,0]];e2=uv[tri[:,2]]-uv[tri[:,0]]
    uv_area=.5*(e1[:,0]*e2[:,1]-e1[:,1]*e2[:,0])
    return m,{'triangles':len(tri),'vertices':n*n,'uv_sets':len(mesh.uv_layers),
        'boundary_edges':4*(n-1),'unique_atlas_area':float(uv_area.sum(dtype=np.float64)),
        'unique_atlas_layout':'Two consistently wound triangles in each exclusive cell of the regular unit square; shared edges have zero area.',
        'all_uv_triangles_positive':bool((uv_area>0).all()),
        'upward_faces':bool((np.cross(xyz.reshape(-1,3)[tri[:,1]]-xyz.reshape(-1,3)[tri[:,0]],xyz.reshape(-1,3)[tri[:,2]]-xyz.reshape(-1,3)[tri[:,0]])[:,2]>0).all())}
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat=bpy.data.materials.new('Gorge · baked strata')
    tree=mat.node_tree;bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    tex=tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(RUNTIME/'GorgeSurface.png'))
    tex.image.alpha_mode='CHANNEL_PACKED';tex.image.pack()
    tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color']);bsdf.inputs['Roughness'].default_value=.96
    checks={}
    for index,intervals in enumerate([176,112,64]):
        src=np.load(HERE/f'Surface{intervals}.npz')
        if index==0:
            h=src['height_m']
            corners=h[[0,0,-1,-1],[0,-1,0,-1]]
            (RUNTIME/'GorgeCorners.txt').write_text(','.join(f'{v:.5f}' for v in corners)+'\n')
        name=f'Gorge_LOD{index}';obj,checks[name]=make_surface(name,src['axis'],src['height_m'],mat)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(filepath=str(RUNTIME/(name+'.fbx')),use_selection=True,
          axis_forward='-Z',axis_up='Y',object_types={'MESH'},apply_unit_scale=True,
          bake_space_transform=True,use_mesh_modifiers=False,mesh_smooth_type='FACE',add_leaf_bones=False)
        if index:obj.hide_set(True);obj.hide_render=True
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'GorgeProduction.blend'),compress=True)
    (HERE/'MeshQA.json').write_text(json.dumps(checks,indent=2)+'\n')
if __name__=='__main__':main()
