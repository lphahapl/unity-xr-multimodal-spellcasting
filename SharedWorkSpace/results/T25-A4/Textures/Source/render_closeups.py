"""Blender 5.2 headless. Render correctly scaled stone and bent bronze coupons with the delivered maps."""
from pathlib import Path
import math
import sys
import json
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUTPUT=ROOT/"Previews"
OUTPUT.mkdir(exist_ok=True)


def pbr(name):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    links=mat.node_tree.links
    surface=nodes.get("Principled BSDF")
    def image(suffix,data):
        node=nodes.new("ShaderNodeTexImage")
        node.image=bpy.data.images.load(str(ROOT/name/f"{name}_{suffix}.png"),check_existing=True)
        node.image.colorspace_settings.name="Non-Color" if data else "sRGB"
        node.extension="REPEAT"
        return node.outputs["Color"]
    links.new(image("BaseColor",False),surface.inputs["Base Color"])
    separate=nodes.new("ShaderNodeSeparateColor")
    links.new(image("ORM",True),separate.inputs["Color"])
    links.new(separate.outputs["Green"],surface.inputs["Roughness"])
    links.new(separate.outputs["Blue"],surface.inputs["Metallic"])
    normal=nodes.new("ShaderNodeNormalMap")
    normal.space="TANGENT"
    normal.inputs["Strength"].default_value=.85 if name=="Stone" else .65
    links.new(image("NormalGL",True),normal.inputs["Color"])
    links.new(normal.outputs["Normal"],surface.inputs["Normal"])
    return mat


def bevel(obj,width):
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new("样块边缘小圆角","BEVEL")
    mod.width=width; mod.segments=4
    mod.affect='EDGES'
    mod.limit_method='ANGLE'; mod.angle_limit=.35
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons: poly.use_smooth=True
    mod=obj.modifiers.new("保留大平面法线","WEIGHTED_NORMAL")
    mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)


def stone():
    bpy.ops.mesh.primitive_cube_add(size=1,location=(0,0,.075))
    obj=bpy.context.object; obj.name="StoneCoupon"
    obj.scale=(.57,.47,.15)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    uv=obj.data.uv_layers.active
    for face in obj.data.polygons:
        axis=max(range(3), key=lambda d: abs(face.normal[d]))
        for loop in face.loop_indices:
            x,y,z=obj.data.vertices[obj.data.loops[loop].vertex_index].co
            u,v=((x,y) if axis==2 else (x,z) if axis==1 else (y,z))
            uv.data[loop].uv=(u+.52,v+.52)
    obj.data.materials.append(pbr("Stone"))
    bevel(obj,.009)
    return obj


def bronze():
    width=.145; thick=.006; radius=.085
    profile=[(-.14,.01,-math.pi/2),(-.14,.055,-math.pi/2)]
    for i in range(1,31):
        a=(math.pi/2)*i/30
        profile.append((-.14+radius*(1-math.cos(a)),.055+radius*math.sin(a),a-math.pi/2))
    profile.append((.191,.055+radius,0))
    verts=[]; arc=[0.]
    for i,(y,z,phi) in enumerate(profile):
        if i: arc.append(arc[-1]+math.hypot(y-profile[i-1][0],z-profile[i-1][1]))
        for x,t in [(-width/2,0),(width/2,0),(width/2,-thick),(-width/2,-thick)]:
            verts.append((x,y+t*math.sin(phi),z+t*math.cos(phi)))
    faces=[]; uvs=[]
    for k in range(len(profile)-1):
        for j in range(4):
            n=(j+1)%4
            faces.append([4*k+j,4*k+n,4*k+4+n,4*k+4+j])
            us=([0,width] if j==0 else [width,width+thick] if j==1 else [width,0] if j==2 else [0,-thick])
            uvs.append([(.42+u/.5,.22+v/.5) for u,v in [(us[0],arc[k]),(us[1],arc[k]),(us[1],arc[k+1]),(us[0],arc[k+1])]])
    for k,order in [(0,[3,2,1,0]),(len(profile)-1,[0,1,2,3])]:
        faces.append([4*k+j for j in order])
        uvs.append([(.42+(0 if j in (0,3) else width)/.5,.22+(arc[k]+(0 if j<2 else thick))/.5) for j in order])
    mesh=bpy.data.meshes.new("Bronze bent strap"); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new("BronzeCoupon",mesh)
    bpy.context.collection.objects.link(obj)
    uv=mesh.uv_layers.new(name="UV0")
    for face,coords in zip(mesh.polygons,uvs):
        for loop,coord in zip(face.loop_indices,coords): uv.data[loop].uv=coord
    mesh.materials.append(pbr("Bronze"))
    bevel(obj,.001)
    obj.location.z=-.01
    return obj


def aim(ob,target):
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()


def render(name):
    bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene
    obj=stone() if name=="Stone" else bronze()
    scene.render.engine="CYCLES"
    scene.cycles.samples=52
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    scene.render.threads_mode='FIXED'; scene.render.threads=6
    scene.render.resolution_percentage=100
    scene.render.resolution_x=1024; scene.render.resolution_y=768
    scene.render.image_settings.file_format="PNG"; scene.render.image_settings.color_mode="RGB"
    scene.view_settings.view_transform="AgX"
    scene.view_settings.exposure=-.55 if name=="Stone" else -.60
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value=(.49,.51,.53,1)
    bg.inputs["Strength"].default_value=.27
    scale=1 if name=="Stone" else .60
    def area(label,at,target,power,short,long,colour):
        data=bpy.data.lights.new(label,"AREA"); data.shape="RECTANGLE"
        data.energy=power*scale*scale; data.size=short*scale; data.size_y=long*scale
        data.color=colour
        lamp=bpy.data.objects.new(label,data); scene.collection.objects.link(lamp)
        lamp.location=Vector(at)*scale
        aim(lamp,target)
    focus=(0,-.014,.094) if name=="Stone" else (0,.016,.085)
    area("掠光窄条",(-.65,-.70,.61),focus,30,.23,.81,(1,.93,.82))
    area("冷中性回光",(.52,.40,.65),focus,12,.38,.71,(.76,.86,1))
    back=(-.21,.77,.75) if name=="Stone" else (.63,.87,.53)
    area("后侧柔光",back,focus,26,.66,.89,(1,.97,.90))
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003 if name=="Stone" else -.0004))
    backdrop=bpy.context.object
    dark=bpy.data.materials.new("台面")
    dark.use_nodes=True
    floorbsdf=dark.node_tree.nodes.get("Principled BSDF")
    floorbsdf.inputs["Base Color"].default_value=(.022,.026,.028,1)
    floorbsdf.inputs["Roughness"].default_value=.83
    backdrop.data.materials.append(dark)
    camera=bpy.data.cameras.new("macro")
    ob=bpy.data.objects.new("macro",camera);scene.collection.objects.link(ob)
    ob.location=(.49,-.95,.83) if name=="Stone" else (.37,-.56,.44)
    aim(ob,focus)
    camera.type="ORTHO"; camera.ortho_scale=.78 if name=="Stone" else .455
    scene.camera=ob; scene.render.filepath=str(OUTPUT / (name+"_Closeup.png"))
    bpy.ops.render.render(write_still=True)
    # 日志供独立的资产检查脚本复核同一个样块；样块不进生产工程。
    qa=Path.home()/".codex/skills/blender-asset-qa/scripts"
    if qa.exists():
        sys.path.insert(0,str(qa))
        import qa_check
        report=qa_check.run(objects=[obj.name], profile="render")
        (ROOT/"Source"/(name+"-swatch-qa.json")).write_text(json.dumps(report,ensure_ascii=False,indent=2,default=str),encoding="utf8")


materials=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else ("Stone","Bronze")
for material in materials:
    if material not in ("Stone","Bronze"): raise ValueError(material)
    render(material)
