"""Round-trip six FBXs, compare mating profiles across LODs and render the assembly.

blender -b --factory-startup -P B02_A1_verify.py -- STAGE [PREVIOUS_FBX_DIRECTORY]
Reads the packed B02_A1.blend and FBXs from STAGE.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from io_scene_fbx import parse_fbx

sys.path.insert(0,str(Path.home()/".codex/skills/blender-asset-qa/scripts"))
import qa_check

args=sys.argv[sys.argv.index("--")+1:]
stage=Path(args[0])
bpy.ops.wm.open_mainfile(filepath=str(stage/"B02_A1.blend"))
mat=bpy.data.materials["B02_Infill"]
mat.use_fake_user=True
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o,do_unlink=True)
scene=bpy.context.scene
report={"models":{}, "assembly":{}}
models={}
sockets={}

def roof_intersections(mesh):
    """The ridge belongs to the single folded roof shell, including its return."""
    points=[v.co for v in mesh.vertices]
    links=[set() for _ in points]
    for a,b in mesh.edge_keys:
        links[a].add(b)
        links[b].add(a)
    seed=max(range(len(points)),key=lambda i:points[i].z)
    shell={seed}
    pending=[seed]
    while pending:
        new=links[pending.pop()]-shell
        pending.extend(new)
        shell.update(new)
    faces=[tuple(p.vertices) for p in mesh.polygons if p.vertices[0] in shell]
    tree=BVHTree.FromPolygons(points,faces,all_triangles=True)
    return sum(a<b and set(faces[a]).isdisjoint(faces[b]) for a,b in tree.overlap(tree))

for path in sorted(stage.glob("Gallery*_LOD?.fbx")):
    asset,tier=path.stem.split("_LOD")
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True)
    ob,=bpy.context.selected_objects
    root,_=parse_fbx.parse(str(path))
    nodes=next(e for e in root.elems if e.id==b"Objects")
    ids={n.id.decode():[n.props[0],n.props[1].split(b"\0")[0].decode()] for n in nodes.elems
         if n.id in (b"Geometry",b"Material",b"Model")}
    assert len(ob.material_slots)==1 and ids["Material"][1]=="B02_Infill"
    ob.data.materials[0]=mat
    # Express every reading in the same Blender metre coordinate system.
    ob.data.transform(ob.matrix_world)
    ob.matrix_world.identity()
    ob.location=(0,0,0)
    ob.rotation_euler=(0,0,0)
    ob.scale=(1,1,1)
    bpy.context.view_layer.update()
    ob.name=path.stem
    points=[v.co.copy() for v in ob.data.vertices]
    dims=[round(max(p[i] for p in points)-min(p[i] for p in points),6) for i in range(3)]
    turn=asset.endswith("Corner")
    assert min(p.x for p in points)>=-1.500002
    assert max(p.y if turn else p.x for p in points)<=1.500002
    for side in ("in","out"):
        profile={}
        for p in points:
            axial=(-p.x if side=="in" else p.y if turn else p.x)
            if abs(axial-1.5)<.000002:
                t=-p.x if turn and side=="out" else p.y
                profile[round(t,5),round(p.z,5)]=(t,p.z)
        sockets[path.stem+"_"+side]=profile
    result=qa_check.run(objects=[ob.name],eps=.00018,tol=.0032)
    crossings=roof_intersections(ob.data)
    assert crossings==0,(path.stem,crossings)
    report["models"][path.stem]={"ids":ids,"bounds_xyz_m":dims,"qa":result,
                                 "roof_shell_intersecting_triangle_pairs":crossings}
    print(path.stem,result["errors"],result["warnings"],flush=True)
    models[path.stem]=ob
    ob.hide_render=True
ref=sockets["GalleryStraight_LOD0_in"]
assert len(ref)>30
assert all(profile.keys()==ref.keys() for profile in sockets.values())
max_error=max(math.dist(v[key],ref[key]) for v in sockets.values() for key in ref)
for name in ("GalleryStraight","GalleryCorner"):
    tags=[f"{name}_LOD{i}" for i in range(3)]
    tris=[report["models"][tag]["qa"]["objects"][tag]["tris"] for tag in tags]
    assert all(n<=limit for n,limit in zip(tris,(2800,1200,500))), (name,tris)
    assert tris[0] > tris[1] > tris[2]
report["assembly"]={"socket_cross_section_vertices":len(ref),"matched_sockets":len(sockets),
    "max_transverse_profile_deviation_m":max_error,
    "comparison_precision_m":.00001,"straight_pitch_m":3.0,"turn_degrees":90,
    "pan_pitch_m":.25,"lip_thickness_m":.018,"mouth_relief_LOD0_m":.060}
if len(args)>1:
    errors=[]
    old_tris={}
    for tag in models:
        path=Path(args[1])/(tag+".fbx")
        bpy.ops.import_scene.fbx(filepath=str(path))
        old,=bpy.context.selected_objects
        old.data.calc_loop_triangles()
        old_tris[tag]=len(old.data.loop_triangles)
        verts=[old.matrix_world@v.co for v in old.data.vertices]
        for side in ("in","out"):
            socket={}
            for x,y,z in verts:
                elbow=tag.startswith("GalleryCorner") and side=="out"
                axis=-x if side=="in" else y if elbow else x
                if abs(axis-1.5)<.000002:
                    t=-x if elbow else y
                    socket[round(t,5),round(z,5)]=(t,z)
            current=sockets[tag+"_"+side]
            assert current.keys()==socket.keys(),(tag,side)
            errors.extend(math.dist(current[key],socket[key]) for key in socket)
        bpy.data.objects.remove(old,do_unlink=True)
    report["previous_A1"]={"triangles":old_tris,"matched_socket_points":len(errors),
                           "max_socket_change_m":max(errors)}
(stage/"B02_A1_verification.json").write_text(json.dumps(report,indent=2),encoding="utf8")
assert all(x["qa"]["pass"] for x in report["models"].values())

# Three joined LOD0 modules, from the approach and around the outside of the bend.
straight=models["GalleryStraight_LOD0"]
corner=models["GalleryCorner_LOD0"]
straight.location.x=-3
straight.hide_render=corner.hide_render=False
outgoing=straight.copy()
scene.collection.objects.link(outgoing)
outgoing.location=(0,3,0)
outgoing.rotation_euler.z=math.pi/2
scene.render.engine="CYCLES"
scene.cycles.samples=12
scene.cycles.use_denoising=True
scene.render.threads_mode="FIXED"
scene.render.threads=10
scene.world=bpy.data.worlds.new("Overcast courtyard")
scene.world.use_nodes=True
back=next(n for n in scene.world.node_tree.nodes if n.type=="BACKGROUND")
back.inputs[0].default_value=(.62,.69,.81,1)
back.inputs[1].default_value=.55
def lamp(pos,target,energy,color,size):
    bpy.ops.object.light_add(type="AREA",location=pos)
    ob=bpy.context.object
    ob.data.energy=energy
    ob.data.size=size
    ob.data.color=color
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat("-Z","Y").to_euler()
lamp((-3,-5,8),(-1,0,2),820,(1,.85,.68),4)
lamp((2,-3,3),(0,0,2.6),125,(1,.94,.86),3)
lamp((4,5,7),(0,0,2),690,(.81,.88,1),4)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.007))
floor=bpy.context.object
floor_mat=bpy.data.materials.new("Studio floor")
floor_mat.diffuse_color=(.24,.225,.21,1)
floor.data.materials.append(floor_mat)
bpy.ops.object.camera_add()
cam=bpy.context.object
scene.camera=cam
cam.data.type="ORTHO"
scene.render.resolution_x=1060
scene.render.resolution_y=660
scene.render.resolution_percentage=100
def shot(name,pos,target,width):
    cam.location=pos
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat("-Z","Y").to_euler()
    cam.data.ortho_scale=width
    scene.render.filepath=str(stage/(name+".png"))
    bpy.ops.render.render(write_still=True)
shot("assembly",(6,-9,6.4),(-.92,.86,1.75),11.6)
shot("bearing",(-1.83,-4.18,1.57),(-.73,-1.36,2.79),1.75)
shot("corner",(4,-5,5.45),(.82,-.82,2.92),3.8)
near_straight,near_corner=straight.data,corner.data
for tier in (1,2):
    straight.data=models[f"GalleryStraight_LOD{tier}"].data
    outgoing.data=straight.data
    corner.data=models[f"GalleryCorner_LOD{tier}"].data
    shot(f"assembly_LOD{tier}",(6,-9,6.4),(-.92,.86,1.75),11.6)
# Restore LOD0 for the close wireframe.
straight.data=near_straight
outgoing.data=straight.data
corner.data=near_corner
# Cycles draws evaluated triangle edges even in headless render mode.
nodes,links=mat.node_tree.nodes,mat.node_tree.links
wire=nodes.new("ShaderNodeWireframe")
wire.use_pixel_size=True
wire.inputs[0].default_value=1.15
mix=nodes.new("ShaderNodeMixRGB")
mix.inputs[1].default_value=(.50,.53,.55,1)
mix.inputs[2].default_value=(.010,.015,.019,1)
links.new(wire.outputs[0],mix.inputs[0])
bs=next(n for n in nodes if n.type=="BSDF_PRINCIPLED")
links.new(mix.outputs[0],bs.inputs["Base Color"])
shot("wire",(-1.83,-4.18,1.57),(-.73,-1.36,2.79),1.75)
