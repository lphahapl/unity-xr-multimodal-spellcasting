"""Round-trip B02 R2 exports with the two actual courtyard approaches.
blender -b --factory-startup -P B02_R2_verify.py -- STAGE
"""
import math
import json
import sys
import re
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
STAGE=Path(sys.argv[sys.argv.index("--")+1]).resolve()
B02=ROOT/"Assets/Art/Environment/Opening/CourtyardInfill/B02"
sys.path.insert(0,str(Path.home()/".codex/skills/blender-asset-qa/scripts"))
import qa_check

def courtyard_placements():
    text=(ROOT/"Assets/Scene/Chapter01_Courtyard.unity").read_text("utf8")
    records={}
    for key in ("GalleryCorner","GalleryStraight"):
        guid=re.search(r"guid: (\w+)",(B02/(key+".prefab.meta")).read_text())[1]
        instances=[]
        for b in re.split(r"^--- !u!1001 &",text,flags=re.M)[1:]:
            b=b.split("--- !u!",1)[0]
            if "m_SourcePrefab: {fileID: 100100000, guid: "+guid not in b:continue
            props=dict(re.findall(r"propertyPath: ([^\n]+)\n\s+value: (.*)",b))
            instances.append({"type":key,"parent":re.search(r"m_TransformParent: \{fileID: (\d+)",b)[1],
                "position":[float(props.get("m_LocalPosition."+i,0)) for i in "xyz"],
                "scale":[float(props.get("m_LocalScale."+i,1)) for i in "xyz"],
                "quaternion":[float(props.get("m_LocalRotation."+i,1 if i=="w" else 0)) for i in "xyzw"]})
        records[key]=instances
    corner,=records["GalleryCorner"]
    def distance(t):
        return math.dist(t["position"],corner["position"])
    return [corner]+sorted(records["GalleryStraight"],key=distance)[:4]

def place_relative(ob,entry,corner):
    x,h,z=entry["position"]
    cx,ch,cz=corner["position"]
    # Blender local +X maps to Unity -X, +Y to Unity -Z for these FBXs.
    # With the original 90-degree corner instance, world +Z is local X.
    assert abs(corner["quaternion"][1]-.7071068)<1e-5
    ob.location=(z-cz,cx-x,h-ch)
    ob.rotation_euler.z=math.pi/2-2*math.atan2(entry["quaternion"][1],entry["quaternion"][3])
    ob.scale=entry["scale"][0],entry["scale"][2],entry["scale"][1]

def measure_ray(ob,x,y,top=8):
    bpy.context.view_layer.update()
    inv=ob.matrix_world.inverted()
    a=inv@Vector((x,y,top))
    d=inv.to_3x3()@Vector((0,0,-1))
    hit,pt,normal,index=ob.ray_cast(a,d.normalized())
    return (ob.matrix_world@pt).z if hit else None

def self_crossings(m):
    # Distinct structural members intentionally seat into one another. Reject
    # self-crossing triangles within any connected shell.
    v=[p.co for p in m.vertices]
    links=[set() for p in v]
    for a,b in m.edge_keys:links[a].add(b);links[b].add(a)
    shell={}
    for i in range(len(v)):
        if i in shell:continue
        stack=[i];shell[i]=i
        while stack:
            for j in links[stack.pop()]:
                if j not in shell:shell[j]=i;stack.append(j)
    faces=[tuple(p.vertices) for p in m.polygons]
    tree=BVHTree.FromPolygons(v,faces,all_triangles=True)
    return sum(a<b and shell[faces[a][0]]==shell[faces[b][0]] and set(faces[a]).isdisjoint(faces[b]) for a,b in tree.overlap(tree))

bpy.ops.wm.open_mainfile(filepath=str(STAGE/"B02_R2_Pavilion.blend"))
mat=bpy.data.materials["B02_Infill"]
mat.use_fake_user=True
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
scene=bpy.context.scene
records=courtyard_placements()
report={"scene_instances_in_shared_parent":records,"tiers":{}}
models={}
for i in range(3):
    for name,path in (("GalleryCorner",STAGE),("GalleryStraight",B02)):
        tag=f"{name}_LOD{i}"
        bpy.ops.import_scene.fbx(filepath=str(path/(tag+".fbx")))
        o,=bpy.context.selected_objects
        o.data.transform(o.matrix_world)
        o.rotation_euler=(0,0,0);o.location=(0,0,0);o.scale=(1,1,1)
        o.name=tag
        assert len(o.data.materials)==1
        o.data.materials[0]=mat
        bpy.context.view_layer.update()
        models[name,i]=o
        if name=="GalleryCorner":
            result=qa_check.run(objects=[tag],eps=.00018,tol=.0032)
            tris=len(o.data.polygons)
            crosses=self_crossings(o.data)
            report["tiers"][i]={"triangles":tris,"dimensions_blender_xyz_m":list(o.dimensions),
                "shell_self_intersections":crosses,"qa":result}
            print(tag,tris,crosses,result["errors"],result["warnings"],flush=True)
            assert tris<=(8200,1200,500)[i] and result["pass"] and not crosses
        o.hide_render=True

# Measure every point of both original straight gable sections, including the
# sloping shoulders. Test all nine independent LOD pairings against the canopy.
for tier in range(3):
    corner=models["GalleryCorner",tier]
    measures=[]
    for placement in records[1:3]:
        assert placement["parent"]==records[0]["parent"]
        for straight_lod in range(3):
            ob=models["GalleryStraight",straight_lod]
            place_relative(ob,placement,records[0])
            bpy.context.view_layer.update()
            verts=[ob.matrix_world@v.co for v in ob.data.vertices]
            axis=min((1.5,-1.5),key=lambda s:(ob.matrix_world@Vector((s,0,3.624))).xy.length)
            gable=[ob.matrix_world@v.co for v in ob.data.vertices if abs(v.co.x-axis)<2e-6 and v.co.z>2.88]
            margins=[]
            for p in gable:
                z=measure_ray(corner,p.x,p.y)
                assert z is not None and z>p.z
                margins.append(z-p.z)
            ridge=ob.matrix_world@Vector((axis,0,3.624))
            along=(ob.matrix_world.to_3x3()@Vector((1 if axis<0 else -1,0,0))).normalized()
            # Walk out along the ridge and measure where the new cover ends.
            lo,hi=0.,6.
            for _ in range(22):
                mid=(lo+hi)/2;p=ridge+mid*along
                if measure_ray(corner,p.x,p.y) is None:hi=mid
                else:lo=mid
            measures.append({"straight_lod":straight_lod,"center_blender_xy":[ob.location.x,ob.location.y],
                "gable_profile_vertices":len(gable),"minimum_cover_height_over_gable_m":min(margins),
                "ridge_overlap_m":(lo+hi)/2})
    report["tiers"][tier]["actual_connections"]=measures
    for pos in ((-3,0,0),(0,3,0)):
        ob=models["GalleryStraight",tier]
        ob.location=pos;ob.rotation_euler.z=0 if pos[0] else math.pi/2
        bpy.context.view_layer.update()
        end=1.5 if pos[0] else -1.5
        for v in ob.data.vertices:
            if abs(v.co.x-end)<1e-6 and v.co.z>2.88:
                p=ob.matrix_world@v.co
                assert measure_ray(corner,p.x,p.y)>p.z
report["nominal_3m_sockets_covered"]=True
(STAGE/"B02_R2_verification.json").write_text(json.dumps(report,indent=2),"utf8")

if "--checks-only" in sys.argv[sys.argv.index("--")+2:]:
    raise SystemExit(0)

scene.render.engine="CYCLES";scene.cycles.samples=10;scene.cycles.use_denoising=True
scene.render.threads_mode="FIXED";scene.render.threads=10
scene.world=bpy.data.worlds.new("Open sky")
scene.world.use_nodes=True
back=next(n for n in scene.world.node_tree.nodes if n.type=="BACKGROUND")
back.inputs[0].default_value=(.67,.74,.85,1);back.inputs[1].default_value=.52
for pos,power,col,size in [((-5,1,9),1200,(1,.88,.71),4),((5,-3,8),1000,(.83,.9,1),4),((-4,5,3),160,(1,.90,.77),3)]:
    bpy.ops.object.light_add(type="AREA",location=pos);l=bpy.context.object
    l.data.energy=power;l.data.size=size;l.data.color=col
    l.rotation_euler=(Vector((0,0,2.5))-l.location).to_track_quat("-Z","Y").to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.009))
floor=bpy.context.object
stone=bpy.data.materials.new("Studio paving");stone.diffuse_color=(.19,.18,.16,1);floor.data.materials.append(stone)
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type="ORTHO"
scene.render.resolution_x=1120;scene.render.resolution_y=700;scene.render.resolution_percentage=100
def shot(name,pos,focus,width):
    cam.location=pos;cam.rotation_euler=(Vector(focus)-cam.location).to_track_quat("-Z","Y").to_euler()
    cam.data.ortho_scale=width;scene.render.filepath=str(STAGE/(name+".png"));bpy.ops.render.render(write_still=True)
for i in range(3):
    active=[models["GalleryCorner",i]]
    for r in records[1:]:
        ob=models["GalleryStraight",i].copy();scene.collection.objects.link(ob);place_relative(ob,r,records[0]);active.append(ob)
    for ob in active:ob.hide_render=False
    shot(f"assembled_{i}",(-9,11,6.05),(-.75,1.0,2.3),12.0)
    if i==0:
        shot("bearing",(-5.1,4.9,1.65),(-1.25,.50,3.34),4.1)
        shot("roof_close",(-4.1,5.5,6.6),(-.45,.35,4.14),5.9)
        shot("joints_top",(-.25,.35,15),(-.25,.35,0),12.4)
        # Player height perspective, looking along the approach into the turn.
        cam.data.type="PERSP";cam.data.lens=25
        shot("inside",(-3.37,-.18,1.76),(.73,.74,2.4),6)
        cam.data.type="ORTHO"
    for ob in active:ob.hide_render=True

