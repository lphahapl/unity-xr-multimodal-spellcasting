"""Selected-to-active Cycles passes. Executed from build.py."""
import json
import hashlib
import numpy as np

def pose(explode):
    for ob in [*highs,low]:
        mesh=ob.data
        points=np.empty(len(mesh.vertices)*3,np.float32)
        mesh.attributes["CotRestPosition"].data.foreach_get("vector",points)
        if explode:
            ids=np.empty(len(mesh.vertices),np.int32)
            mesh.attributes["CotBakePart"].data.foreach_get("value",ids)
            points.reshape(-1,3)[:,0]+=ids*3.5
        mesh.vertices.foreach_set("co",points);mesh.update()
    bpy.context.view_layer.update()

def select_pair():
    for ob in highs:ob.hide_set(False);ob.hide_render=False
    set_active(low)
    for ob in highs:ob.select_set(True)

def channel(role):
    for mat,(bs,pigment,gloss) in materials.items():
        nt=mat.node_tree
        out=next(n for n in nt.nodes if n.type=="OUTPUT_MATERIAL")
        if role not in ("BaseColor","Smoothness"):
            nt.links.new(bs.outputs[0],out.inputs["Surface"]);continue
        emit=nt.nodes.get("Selected pigment") or nt.nodes.new("ShaderNodeEmission");emit.name="Selected pigment"
        for link in list(emit.inputs[0].links):nt.links.remove(link)
        if role=="BaseColor":nt.links.new(pigment.outputs[0],emit.inputs[0])
        else:emit.inputs[0].default_value=(gloss,gloss,gloss,1)
        nt.links.new(emit.outputs[0],out.inputs["Surface"])

def bake(role,res=1024):
    print("Start",role,res,flush=True)
    img=bpy.data.images.new("WakeCot_"+role+str(res),width=res,height=res,alpha=True)
    img.colorspace_settings.name="sRGB" if role=="BaseColor" else "Non-Color"
    nodes=lowmat.node_tree.nodes
    target=nodes.get("Bake Target") or nodes.new("ShaderNodeTexImage");target.name="Bake Target";target.image=img;nodes.active=target
    channel(role);select_pair()
    bpy.ops.object.bake(type={"Normal":"NORMAL","AO":"AO"}.get(role,"EMIT"),
        use_selected_to_active=True,cage_extrusion=.014,max_ray_distance=.033,
        margin=5 if res==1024 else 1,margin_type="EXTEND",normal_space="TANGENT",
        normal_r="POS_X",normal_g="POS_Y",normal_b="POS_Z")
    return img

def save(img,destination):
    img.filepath_raw=str(destination);img.file_format="PNG";img.save()

def rgba(img):
    data=np.empty(len(img.pixels),np.float32);img.pixels.foreach_get(data)
    return data.reshape(-1,4)

pose(True)
probe=bake("Normal",128);save(probe,WORK/"projection.png")
assert rgba(probe)[:,:2].std()>.003
bpy.data.images.remove(probe)
normal=bake("Normal");save(normal,TEX/"WakeCot_Normal.png")
color=bake("BaseColor");save(color,TEX/"WakeCot_BaseColor.png")
smooth=bake("Smoothness")
pose(False)
ao=bake("AO");mask=bpy.data.images.new("WakeCot_Mask",width=1024,height=1024,alpha=True)
mask.colorspace_settings.name="Non-Color"
# The cot consists entirely of non-metals. URP Lit samples the baked contact AO
# in G and per-material smoothness in A. The material itself stays opaque.
pixels=np.zeros((1024*1024,4),np.float32)
pixels[:,1]=rgba(ao)[:,0];pixels[:,3]=rgba(smooth)[:,0]
mask.pixels.foreach_set(pixels.ravel());mask.update();save(mask,TEX/"WakeCot_Mask.png")
channel("restore");bpy.data.images.remove(smooth);bpy.data.images.remove(ao)
nt=lowmat.node_tree;nt.nodes.remove(nt.nodes["Bake Target"])
bs=next(n for n in nt.nodes if n.type=="BSDF_PRINCIPLED")
def sampler(img):
    n=nt.nodes.new("ShaderNodeTexImage");n.image=img;return n
nt.links.new(sampler(color).outputs[0],bs.inputs["Base Color"])
nrm=nt.nodes.new("ShaderNodeNormalMap");nt.links.new(sampler(normal).outputs[0],nrm.inputs["Color"]);nt.links.new(nrm.outputs[0],bs.inputs["Normal"])
inv=nt.nodes.new("ShaderNodeMath");inv.operation="SUBTRACT";inv.inputs[0].default_value=1
nt.links.new(sampler(mask).outputs["Alpha"],inv.inputs[1]);nt.links.new(inv.outputs[0],bs.inputs["Roughness"])
for ob in highs:ob.hide_set(True);ob.hide_render=True
set_active(low)
out=ASSETS/"WakeCot.fbx"
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={"MESH"},
     axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_UNITS",bake_space_transform=True,
     use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode="STRIP",mesh_smooth_type="FACE")
for img in (normal,color,mask):img.pack();img.filepath="//../../../../Assets/Art/Environment/Opening/WakeCot/Textures/"+Path(img.filepath).name
for a in bpy.context.screen.areas:
    if a.type=="VIEW_3D":
        s=a.spaces.active.region_3d;s.view_distance=3.5;s.view_location=(0,0,.38)
        s.view_rotation=Vector((-2.1,3.7,-1.4)).to_track_quat("-Z","Y")
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/"WakeCot_pipeline.blend"),compress=True)
for ob in highs:ob.data.calc_loop_triangles()
report={"blender":bpy.app.version_string,"high_tri":sum(len(o.data.loop_triangles) for o in highs),
        "high_editable_parts":len(highs),"low_tri":len(low.data.loop_triangles),"material":"WakeCot_Baked",
        "meters_blender_xyz":list(low.dimensions),"atlas":1024,"bake":"Cycles selected high → active low",
        "normal":"tangent +X +Y +Z, OpenGL","mask":"R metallic (wood/rush/linen=0), G assembled AO, A smoothness",
        "project":"isolated 3.5m per CotBakePart, except assembled AO; 0.014 m projection cage, 0.033 m ray, 5px dilation",
        "files":{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in [out,*sorted(TEX.glob("*.png"))]}}
(WORK/"build.json").write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding="utf8")
print("FINISHED",report,flush=True)
