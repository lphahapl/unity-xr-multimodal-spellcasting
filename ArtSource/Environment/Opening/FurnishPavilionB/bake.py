"""Executed by build.py after both model tiers have been built."""
import hashlib
from mathutils import Vector

def separate_bake_parts(exploded):
    # Isolate the cage for each rail, glass insert, face, peg and support.
    # The retained rest position also locks material grain during this operation.
    for obj in [*highs.values(),*lows.values()]:
        mesh=obj.data
        ids=mesh.attributes["WorkshopBakePart"].data
        rest=mesh.attributes["WorkshopCraftPosition"].data
        group=[0]*len(mesh.vertices)
        for p in mesh.polygons:
            for vi in p.vertices:group[vi]=ids[p.index].value
        for v in mesh.vertices:
            v.co=rest[v.index].vector+Vector((group[v.index]*3.0 if exploded else 0.,0,0))
        mesh.update()

def image(name, res, color):
    out=bpy.data.images.new(name,width=res,height=res,alpha=True)
    out.colorspace_settings.name="sRGB" if color else "Non-Color"
    return out


def read_pixels(img):
    values=np.empty(img.size[0]*img.size[1]*4,dtype=np.float32)
    img.pixels.foreach_get(values)
    return values.reshape(img.size[1],img.size[0],4)


def channel(settings):
    for mat,(bs,color,metal,smooth,glow) in shades.items():
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        out=next(n for n in nodes if n.type=="OUTPUT_MATERIAL")
        if settings in ("normal","ao","restore"):
            links.new(bs.outputs[0],out.inputs["Surface"])
        else:
            em=nodes.get("Bake source") or nodes.new("ShaderNodeEmission")
            em.name="Bake source"
            for conn in list(em.inputs["Color"].links): links.remove(conn)
            if settings=="albedo":links.new(color.outputs[0],em.inputs["Color"])
            elif settings=="mask":em.inputs["Color"].default_value=(metal,1.,glow,1.)
            elif settings=="smooth":em.inputs["Color"].default_value=(smooth,smooth,smooth,1.)
            links.new(em.outputs[0],out.inputs["Surface"])


def bake_map(role,res,detail=True):
    out=image(role+str(res),res,role=="albedo")
    nodes=lowmat.node_tree.nodes
    target=nodes.get("Bake Target") or nodes.new("ShaderNodeTexImage")
    target.name="Bake Target";target.image=out; nodes.active=target
    channel(role)
    for nidx,name in enumerate(NAMES):
        for key in NAMES:
            highs[key].hide_render=(key!=name);highs[key].hide_set(key!=name)
            lows[key].hide_render=(key!=name);lows[key].hide_set(key!=name)
        active(lows[name]);highs[name].select_set(True)
        kind={"ao":"AO","normal":"NORMAL"}.get(role,"EMIT")
        bpy.ops.object.bake(type=kind, use_selected_to_active=True, use_clear=nidx==0,
            cage_extrusion=.0028, max_ray_distance=.013, margin=6 if detail else 1,
            margin_type="EXTEND", normal_space="TANGENT", normal_r="POS_X",normal_g="POS_Y",normal_b="POS_Z")
    print("BAKED",role,res,flush=True)
    return out


def save(img,path,relocate=False):
    img.filepath_raw=str(path);img.file_format="PNG";img.save()
    if relocate: img.filepath=bpy.path.relpath(str(path),start=str(HERE))


separate_bake_parts(True)
test=bake_map('normal',128,False)
assert np.std(read_pixels(test)[:,:,:2])>.002
bpy.data.images.remove(test)
res=1024
albedo=bake_map('albedo',res);save(albedo,STAGE/(PREFIX+'_BaseColor.png'))
normal=bake_map('normal',res);save(normal,STAGE/(PREFIX+'_Normal.png'))
mask=bake_map('mask',res);gloss=bake_map('smooth',res)
separate_bake_parts(False)
ao=bake_map('ao',res)
pixels=read_pixels(mask);pixels[:,:,1]=read_pixels(ao)[:,:,0];pixels[:,:,2]=0;pixels[:,:,3]=read_pixels(gloss)[:,:,0]
mask.pixels.foreach_set(pixels.ravel());mask.update();save(mask,STAGE/(PREFIX+'_Mask.png'))
for im in (ao,gloss):bpy.data.images.remove(im)
channel('restore')
nt=lowmat.node_tree
nt.nodes.remove(nt.nodes['Bake Target'])
dest=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
def texture(im):
    node=nt.nodes.new('ShaderNodeTexImage');node.image=im;return node
nt.links.new(texture(albedo).outputs[0],dest.inputs['Base Color'])
nrm=nt.nodes.new('ShaderNodeNormalMap');nt.links.new(texture(normal).outputs[0],nrm.inputs['Color'])
nt.links.new(nrm.outputs[0],dest.inputs['Normal'])
packed=texture(mask);split=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(packed.outputs[0],split.inputs[0])
nt.links.new(split.outputs[0],dest.inputs['Metallic'])
invert=nt.nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
nt.links.new(packed.outputs['Alpha'],invert.inputs[1]);nt.links.new(invert.outputs[0],dest.inputs['Roughness'])
report={'blender':bpy.app.version_string,'bake_resolution':res,'cage_m':.0028,'ray_m':.013,
'method':'Cycles selected-to-active, per component exploded color, tangent normal, roughness, metallic; assembled AO. Source materials procedurally textured. Unique shared 1K atlas.','meshes':{}}
for name in NAMES:
    hi=highs[name];lo=lows[name]
    hi.hide_render=True;hi.hide_set(True);lo.hide_set(False);lo.hide_render=False
    hi.data.calc_loop_triangles()
    record={'high_tri':len(hi.data.loop_triangles),'LOD_tri':[], 'dimensions_xyz_blender_m':list(lo.dimensions)}
    report['meshes'][name]=record
    for tier,ratio in enumerate((1,.63,.34)):
        ob=lo
        if tier:
            ob=lo.copy();ob.data=lo.data.copy();scene.collection.objects.link(ob)
            mod=ob.modifiers.new('Collapse small bevels','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
            bake_mod(ob,mod)
        active(ob);ob.data.calc_loop_triangles()
        record['LOD_tri'].append(len(ob.data.loop_triangles))
        bpy.ops.export_scene.fbx(filepath=str(STAGE/(name+f'_LOD{tier}.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE')
        if tier:bpy.data.objects.remove(ob,do_unlink=True)
    assert record['LOD_tri'][0]>record['LOD_tri'][1]>record['LOD_tri'][2]
    hi.location.x=lo.location.x=NAMES.index(name)*2.5
assert sum(m['LOD_tri'][0] for m in report['meshes'].values())<=BUDGET
for im in (albedo,normal,mask):im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/(PREFIX+'_pipeline.blend')),compress=True)
(HERE/'bake-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report),flush=True)
