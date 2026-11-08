"""Executed by build.py; project separated source parts and pack the three Unity maps."""
import hashlib
def position_parts(exploded):
    for obj in [*high_objects,low]:
        me=obj.data; original=me.attributes['CastPosition'].data
        positions=np.empty(len(me.vertices)*3,np.float32);original.foreach_get('vector',positions)
        if exploded:
            pairs=np.empty(len(me.vertices),np.int32)
            me.attributes['ProjectionPair'].data.foreach_get('value',pairs)
            positions.reshape(-1,3)[:,0]+=pairs*3.5
        me.vertices.foreach_set('co',positions);me.update()

def high_channel(role):
    for mat,(bs,ramp,metal,smoothness) in materials.items():
        nt=mat.node_tree; out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
        if role not in ('base','mask'):
            nt.links.new(bs.outputs[0],out.inputs['Surface']);continue
        em=nt.nodes.get('Projection input') or nt.nodes.new('ShaderNodeEmission')
        em.name='Projection input'
        if role=='base': nt.links.new(ramp.outputs[0],em.inputs[0])
        else:
            for link in list(em.inputs[0].links):nt.links.remove(link)
            em.inputs[0].default_value=(metal,smoothness,0.,1.)
        nt.links.new(em.outputs[0],out.inputs[0])

nodes=lowmat.node_tree.nodes
target=nodes.new('ShaderNodeTexImage');target.name='Projection output'
def bake(role,size):
    im=bpy.data.images.new(role+str(size),width=size,height=size,alpha=True)
    im.colorspace_settings.name='sRGB' if role=='base' else 'Non-Color'
    target.image=im;nodes.active=target
    high_channel(role)
    active(low)
    for obj in high_objects:obj.select_set(True)
    bpy.ops.object.bake(type={'ao':'AO','normal':'NORMAL'}.get(role,'EMIT'),
        use_selected_to_active=True,use_clear=True,cage_extrusion=.018,max_ray_distance=.045,
        margin=4 if size==1024 else 1,margin_type='EXTEND',normal_space='TANGENT',
        normal_r='POS_X',normal_g='POS_Y',normal_b='POS_Z')
    print('PROJECTED', role,size,flush=True)
    return im
def pixels(im):
    data=np.empty(im.size[0]*im.size[1]*4,np.float32);im.pixels.foreach_get(data)
    return data.reshape(im.size[1],im.size[0],4)
def save(im,name):
    im.file_format='PNG';im.filepath_raw=str(TEX/('BrokenArmillary_'+name+'.png'))
    im.save()
position_parts(True)
test=bake('normal',128);assert pixels(test)[:,:,:2].std()>.005;bpy.data.images.remove(test)
albedo=bake('base',1024);save(albedo,'BaseColor')
normal=bake('normal',1024);save(normal,'Normal')
packed=bake('mask',1024)
position_parts(False)
ao=bake('ao',1024)
raw=pixels(packed);raw[:,:,3]=raw[:,:,1];raw[:,:,1]=pixels(ao)[:,:,0]
packed.pixels.foreach_set(raw.ravel());packed.update();save(packed,'Mask')
bpy.data.images.remove(ao);high_channel('restore');nodes.remove(target)
nt=lowmat.node_tree;link=nt.links.new;bs=nodes.get('Principled BSDF')
def texture(im):
    node=nodes.new('ShaderNodeTexImage');node.image=im;return node
link(texture(albedo).outputs[0],bs.inputs['Base Color'])
nm=nodes.new('ShaderNodeNormalMap');link(texture(normal).outputs[0],nm.inputs['Color']);link(nm.outputs[0],bs.inputs['Normal'])
p=texture(packed);sp=nodes.new('ShaderNodeSeparateColor');link(p.outputs[0],sp.inputs[0]);link(sp.outputs[0],bs.inputs['Metallic'])
inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
link(p.outputs['Alpha'],inv.inputs[1]);link(inv.outputs[0],bs.inputs['Roughness'])
# Source coordinates are still editable on high parts; the runtime mesh needs only geometry, normals, tangents and UVs.
source_mesh=low.data;source_mesh.name='BrokenArmillary_Source'
export_mesh=source_mesh.copy();export_mesh.name='BrokenArmillary';low.data=export_mesh
for attr in ('CastPosition','ProjectionPair'):export_mesh.attributes.remove(export_mesh.attributes[attr])
for ob in high_objects:ob.hide_set(True);ob.hide_render=True
active(low);bpy.context.view_layer.update()
out=OUT/'BrokenArmillary.fbx'
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
    use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE',
    use_custom_props=False)
low.data=source_mesh;bpy.data.meshes.remove(export_mesh);source_mesh.name='BrokenArmillary'
for im in (albedo,normal,packed):im.pack()
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        rv=area.spaces.active.region_3d;rv.view_distance=4.0;rv.view_location=(0,0,1.37)
        rv.view_rotation=Vector((-.10,1,-.045)).to_track_quat('-Z','Y')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'BrokenArmillary.blend'))
source={}
for o in high_objects:
    o.data.calc_loop_triangles();source[o.name]=len(o.data.loop_triangles)
low.data.calc_loop_triangles()
report={'blender':bpy.app.version_string,'meters_per_unit':1,'source_parts_triangles':source,
        'high_triangles':sum(source.values()),'low_triangles':len(low.data.loop_triangles),
        'low_vertices':len(low.data.vertices),'baked_uv':low.data.uv_layers.active.name,'material_slots':len(low.data.materials),
        'low_model_blender_xyz_m':list(low.dimensions),'existing_plinth_center_top_unity_m':[2.6,.805,64.65],'approach_yaw_unity_deg':180,
        'cage_extrusion_m':.018,'ray_distance_m':.045,'projection_pair_spacing_m':3.5,
        'projection':'Cycles selected high parts to active triangulated low, exploded pairs for color/tangent/PBR, assembled for AO',
        'uv':'Unique metric strips and end caps, packed at 1K, tangent +X/+Y/+Z, 4px extend, .0036 pack margin',
        'images':'BaseColor sRGB; Normal linear; Mask linear R=metal, G=AO, B=0, A=smoothness',
        'axle_break_centerlines_blender_m':[[.24,-.075,1.57],[.61,-.075,1.89]],
        'sha256':{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in [out,*sorted(TEX.glob('*.png'))]}}
(HERE/'bake_report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('EXPORTED',json.dumps(report),flush=True)
