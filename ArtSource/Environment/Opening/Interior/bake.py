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
            v.co=rest[v.index].vector+Vector((group[v.index]*1.5 if exploded else 0.,0,0))
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
            cage_extrusion=.012, max_ray_distance=.032, margin=5 if detail else 1,
            margin_type="EXTEND", normal_space="TANGENT", normal_r="POS_X",normal_g="POS_Y",normal_b="POS_Z")
    print("BAKED",role,res,flush=True)
    return out


def save(img,path,relocate=False):
    img.filepath_raw=str(path);img.file_format="PNG";img.save()
    if relocate: img.filepath=bpy.path.relpath(str(path),start=str(HERE))


separate_bake_parts(True)
test=bake_map("normal",128,False)
pix=read_pixels(test);assert (pix[:,:,:2].std(axis=(0,1))>.002).all()
save(test,WORK/"projection-128.png");bpy.data.images.remove(test)
albedo=bake_map("albedo",1024);save(albedo,TEXTURES/"GaugeWorkshop_BaseColor.png")
normal=bake_map("normal",1024);save(normal,TEXTURES/"GaugeWorkshop_Normal.png")
mask=bake_map("mask",1024); gloss=bake_map("smooth",1024)
separate_bake_parts(False)
ao=bake_map("ao",1024)
data=read_pixels(mask);data[:,:,1]=read_pixels(ao)[:,:,0];data[:,:,3]=read_pixels(gloss)[:,:,0]
mask.pixels.foreach_set(data.ravel());mask.update();save(mask,TEXTURES/"GaugeWorkshop_Mask.png")
# The wick alone gets modest emission. A small separate URP emission map avoids
# a second material or a point light. R/G/B/A mask remains in linear space.
em=image("Gauge emissive",256,True)
small=data[:,:,2].reshape(256,4,256,4).mean(axis=(1,3))
rgb=np.ones((256,256,4),np.float32)
rgb[:,:,:3]=small[:,:,None]*np.array([.35,.15,.035])
em.pixels.foreach_set(rgb.ravel());em.update();save(em,TEXTURES/"GaugeWorkshop_Emission.png")
for img in (ao,gloss):bpy.data.images.remove(img)
channel("restore")
nt=lowmat.node_tree
dest=next(n for n in nt.nodes if n.type=="BSDF_PRINCIPLED")
nt.nodes.remove(nt.nodes["Bake Target"])
def texture(img):
    n=nt.nodes.new("ShaderNodeTexImage");n.image=img;return n
nt.links.new(texture(albedo).outputs["Color"],dest.inputs["Base Color"])
nrm=nt.nodes.new("ShaderNodeNormalMap");nt.links.new(texture(normal).outputs[0],nrm.inputs["Color"])
nt.links.new(nrm.outputs["Normal"],dest.inputs["Normal"])
packed=texture(mask); split=nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(packed.outputs[0],split.inputs[0])
nt.links.new(split.outputs["Red"],dest.inputs["Metallic"])
inv=nt.nodes.new("ShaderNodeMath");inv.operation="SUBTRACT";inv.inputs[0].default_value=1
nt.links.new(packed.outputs["Alpha"],inv.inputs[1]);nt.links.new(inv.outputs[0],dest.inputs["Roughness"])
nt.links.new(texture(em).outputs[0],dest.inputs["Emission Color"]);dest.inputs["Emission Strength"].default_value=.30
report={"blender":bpy.app.version_string,"atlas":[1024,1024],"emission":[256,256],
    "normal":"MikkTSpace tangent +X +Y +Z (OpenGL)","cage_m":.012,"ray_m":.032,
    "projection_groups":group_ids,"projection":"WorkshopBakePart * 1.5 m X in bake space. AO uses assembled parts.",
    "channels":"R metallic, G Cycles AO, B wick mask, A smoothness","meshes":{}}
for name in NAMES:
    lo=lows[name];hi=highs[name];hi.hide_render=True;hi.hide_set(True);lo.hide_set(False);lo.hide_render=False
    active(lo);hi.data.calc_loop_triangles();lo.data.calc_loop_triangles()
    report["meshes"][name]={"high_tri":len(hi.data.loop_triangles),"low_tri":len(lo.data.loop_triangles),
        "low_vertices":len(lo.data.vertices),"materials":len(lo.data.materials),"dims_blender":list(lo.dimensions)}
    f=ASSETS/(name+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(f),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_UNITS",bake_space_transform=True,
        use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode="STRIP",mesh_smooth_type="FACE")
    report["meshes"][name]["fbx_sha256"]=hashlib.sha256(f.read_bytes()).hexdigest()
for name,offset in zip(NAMES,[(0,-.139,1.60),(0,0,0),(.76,0,1.07)]):
    highs[name].location=offset;lows[name].location=offset
for img in (albedo,normal,mask,em):img.pack()
bpy.context.view_layer.update();active(lows[NAMES[1]])
for area in bpy.context.screen.areas:
    if area.type=="VIEW_3D":
        rv=area.spaces.active.region_3d;rv.view_distance=3.4;rv.view_location=(.17,0,.93)
        rv.view_rotation=Vector((-.9,3,-.6)).to_track_quat("-Z","Y")
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/"WorkshopProps_pipeline.blend"))
(WORK/"report.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print("FINISHED",json.dumps(report),flush=True)
