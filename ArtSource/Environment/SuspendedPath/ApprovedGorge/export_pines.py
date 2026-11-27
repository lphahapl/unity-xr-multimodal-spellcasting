"""Export the mid-distance canopy mesh with two elevated, crossed azimuths per tree."""
from pathlib import Path
import bpy, math, numpy as np
here=Path(__file__).resolve().parent
runtime=here.parents[3]/'Assets/Art/Environment/SuspendedPath/ApprovedGorge'
bpy.ops.wm.read_factory_settings(use_empty=True)
trees=np.load(here/'PineStands.npz')['trees']
vertices=[];faces=[];uvs=[];colors=[]
for x,z,base,height,width,theta,tint in trees:
    for view in range(2):
        angle=theta+view*math.pi/2
        # The photo face points west, toward the A2 outlook, broadly
        # aligned with the valley; the other face carries the side bake.
        extent=height*width*(.65 if view==0 else .53)
        dx=math.sin(angle)*extent;dz=math.cos(angle)*extent
        a=len(vertices)
        for along,up in [(1,0),(-1,0),(-1,1),(1,1)]:
            vertices.append((-(x+along*dx),-(z+along*dz),base+(up*1.045-.022)*height))
            colors.append((min(.99,tint*.87),min(.99,tint*.89),min(.99,tint*.84),1))
        faces.extend([(a,a+1,a+2),(a,a+2,a+3)])
        u=view*.5
        uvs.extend([(u,0),(u+.5,0),(u+.5,1),(u,0),(u+.5,1),(u,1)])
mesh=bpy.data.meshes.new('PineStands');mesh.from_pydata(vertices,[],faces);mesh.update()
uv=mesh.uv_layers.new(name='Pine atlas')
uv.uv.foreach_set('vector',np.array(uvs,dtype=np.float32).ravel())
color=mesh.color_attributes.new(name='Tree exposure',type='FLOAT_COLOR',domain='POINT')
color.data.foreach_set('color',np.array(colors,dtype=np.float32).ravel())
assert not mesh.validate()
obj=bpy.data.objects.new('PineStands',mesh);bpy.context.collection.objects.link(obj)
mat=bpy.data.materials.new('Pine canopies')
tree=mat.node_tree
bsdf=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
atlas=tree.nodes.new('ShaderNodeTexImage')
atlas.image=bpy.data.images.load(str(runtime/'PineAtlas.png'))
atlas.image.pack()
tree.links.new(atlas.outputs['Color'],bsdf.inputs['Base Color'])
tree.links.new(atlas.outputs['Alpha'],bsdf.inputs['Alpha'])
bsdf.inputs['Roughness'].default_value=.88
mesh.materials.append(mat)
obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(filepath=str(runtime/'PineStands.fbx'),use_selection=True,
    axis_forward='-Z',axis_up='Y',object_types={'MESH'},apply_unit_scale=True,
    bake_space_transform=True,mesh_smooth_type='FACE',add_leaf_bones=False,colors_type='LINEAR')
print('Pine stands',len(trees),'trees',len(faces),'triangles')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(here/'PineStands.blend'),compress=True)
