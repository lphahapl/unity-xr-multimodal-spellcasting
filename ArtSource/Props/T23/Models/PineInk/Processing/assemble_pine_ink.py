"""Build standalone animated PineInk review art from the three downloaded cloud components."""
import json
import math
import struct
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bmesh
import bpy
import numpy as np
from mathutils import Matrix,Vector

from audit_wrapped_core import WORK,SOURCE,bbox,data_of,digest,gltf_document,load_source,qa_check,review_shots,studio_asset
from fit_ink_joint import camera_focus,choose_donor_uv,merge_microscopic_cut_slivers
from inspect_joint import component_groups
from optimize_wrapped_core import bvh,candidate_atlas,decimate
from bpy_extras.anim_utils import action_get_channelbag_for_slot

REPO=WORK.parents[5]
PINEINK=WORK/"PineInk-JointFit.blend"
SOURCE_SLEEVE=SOURCE.with_name("SlideSleeve.glb")
CASE=REPO/"Assets/Art/Props/ShenShangToolBelt/Geometry/WoodCase/WoodCase.fbx"
SLEEVE_CLOSED_BASE=-.0185
SLEEVE_HEIGHT=.0282
SLEEVE_TARGET_WIDTH=.018
SLEEVE_TARGET_DEPTH=.012
SLEEVE_TRAVEL=.031
CLIP_GAP=.00037
PART_NAMES=("WrappedCore_Joined_LOD0","DetachableSegment_Joined_LOD0")


def apply_modifier(obj,name):
    with bpy.context.temp_override(active_object=obj,object=obj,selected_objects=[obj],selected_editable_objects=[obj]):
        result=bpy.ops.object.modifier_apply(modifier=name)
        if result != {"FINISHED"}:raise RuntimeError(name)


def sleeve_frame(src):
    matrix=Matrix.Diagonal(Vector((-1.,-1.,1.,1.))) @ src.matrix_world
    src.data.transform(matrix)
    src.matrix_world=Matrix.Identity(4)
    edit=bmesh.new()
    edit.from_mesh(src.data)
    primary=max(component_groups(edit),key=len)
    surviving=set(primary)
    crumbs=[v for v in edit.verts if v not in surviving]
    removed=len(crumbs)
    if crumbs:
        bmesh.ops.delete(edit,geom=crumbs,context="VERTS")
    edit.to_mesh(src.data)
    edit.free()
    data=data_of(src)[0]
    lo,hi=data.min(axis=0),data.max(axis=0)
    mid=(lo+hi)/2
    target=np.array([SLEEVE_TARGET_WIDTH,SLEEVE_TARGET_DEPTH,SLEEVE_HEIGHT])
    ratio=target/(hi-lo)
    offset=np.array([0.,0.,SLEEVE_CLOSED_BASE])
    anchor=np.array([mid[0],mid[1],lo[2]])
    for v in src.data.vertices:
        v.co=Vector((np.array(v.co)-anchor)*ratio+offset)
    src.data.update()
    return removed


def corridor_profile(bounds):
    lo,hi=bounds
    cx,cy=((lo+hi)/2)[:2]
    hx,hy=((hi-lo)/2)[:2]+CLIP_GAP
    corner=.00055
    outline=[]
    for quadrant,(x,y) in enumerate(((hx-corner,hy-corner),(-hx+corner,hy-corner),
                                     (-hx+corner,-hy+corner),(hx-corner,-hy+corner))):
        for part in range(7):
            angle=(quadrant/2+part/12)*math.pi
            outline.append((cx+x+corner*math.cos(angle),cy+y+corner*math.sin(angle)))
    return outline,(float(cx),float(cy)),(float(hx),float(hy))


def support_upper_sidewalls(obj):
    # The original taper narrows through the cutter near the upper lip.
    # Expand just that axial band before subtraction, preserving UVs and the native notch.
    for vert in obj.data.vertices:
        t=(vert.co.z-SLEEVE_CLOSED_BASE)/SLEEVE_HEIGHT
        weight=max(0.,min(1.,(t-.48)/.43))
        weight=weight*weight*(3.-2.*weight)
        vert.co.x*=1.+(.00148/.00825)*weight
    obj.data.update()


def axial_grain_coordinates(obj):
    mesh=obj.data
    mesh.calc_loop_triangles()
    uv=mesh.uv_layers.active.uv
    size=2048
    field=np.zeros((size,size),dtype=np.float32)
    covered=np.zeros((size,size),dtype=bool)
    for tri in mesh.loop_triangles:
        if abs(tri.normal.z)>.53:
            continue
        points=np.array([tuple(mesh.vertices[i].co) for i in tri.vertices])
        texel=np.array([tuple(uv[i].vector) for i in tri.loops])*(size-1)
        texel[:,1]=(size-1)-texel[:,1]
        lo=np.maximum(0,np.floor(texel.min(axis=0)).astype(int))
        hi=np.minimum(size-1,np.ceil(texel.max(axis=0)).astype(int))
        edge_a=texel[1]-texel[0]
        edge_b=texel[2]-texel[0]
        denom=edge_a[0]*edge_b[1]-edge_a[1]*edge_b[0]
        if abs(denom)<.025:
            continue
        ys,xs=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        xx,yy=xs-texel[0,0],ys-texel[0,1]
        a=(xx*edge_b[1]-yy*edge_b[0])/denom
        b=(edge_a[0]*yy-edge_a[1]*xx)/denom
        inside=(a>=-.005)&(b>=-.005)&(a+b<=1.005)
        if not inside.any():
            continue
        hit=points[0]+a[inside,None]*(points[1]-points[0])+b[inside,None]*(points[2]-points[0])
        around=np.arctan2(hit[:,1]/.006,hit[:,0]/.009)/(2*np.pi)
        z=(hit[:,2]-SLEEVE_CLOSED_BASE)/SLEEVE_HEIGHT
        wiggle=.103*np.sin(2*np.pi*(z*.72+around*5))+.049*np.sin(2*np.pi*z*1.39)
        grain=.043*np.sin(2*np.pi*(23*around+wiggle))+.029*np.sin(2*np.pi*(49*around+wiggle*.65))
        grain+=.012*np.sin(2*np.pi*(87*around+wiggle*.39))
        xi,yi=xs[inside],ys[inside]
        field[yi,xi]=grain
        covered[yi,xi]=True
    np.savez_compressed(WORK/"SlideSleeve-AxialGrainField.npz",field=field,covered=covered)


def revise_sleeve_textures(atlas,obj):
    axial_grain_coordinates(obj)
    subprocess.run(["python",str(WORK/"refine_sleeve_finish.py")],check=True)
    links={
        "SlideSleeve_BaseColor.png":"SlideSleeve_ReviewWood.png",
        "SlideSleeve_ORM.png":"SlideSleeve_ReviewORM.png"
    }
    for node in atlas.node_tree.nodes:
        if node.type!="TEX_IMAGE" or node.image is None or node.image.name not in links:
            continue
        file=links[node.image.name]
        node.image=bpy.data.images.load(str(WORK/"SlideSleeveTextures"/file),check_existing=True)
        node.image.colorspace_settings.name="sRGB" if file.endswith("Wood.png") else "Non-Color"
    return json.loads((WORK/"SlideSleeve-Finish.json").read_text(encoding="utf-8"))


def cutter_object(outline):
    lo=SLEEVE_CLOSED_BASE-.008
    hi=SLEEVE_CLOSED_BASE+SLEEVE_HEIGHT+.008
    points=[(x,y,z) for z in [lo,hi] for x,y in outline]
    n=len(outline)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    mesh=bpy.data.meshes.new("Sleeve tool corridor")
    mesh.from_pydata(points,[],faces)
    mesh.update()
    cutter=bpy.data.objects.new("Sleeve tool corridor",mesh)
    bpy.context.scene.collection.objects.link(cutter)
    cutter.data.materials.append(bpy.data.materials.new("Temporary cavity face tag"))
    return cutter


def cut_cavity(low,atlas,outline):
    cutter=cutter_object(outline)
    modifier=low.modifiers.new("Axial sleeve through-bore","BOOLEAN")
    modifier.operation="DIFFERENCE"
    modifier.solver="EXACT"
    modifier.material_mode="TRANSFER"
    modifier.object=cutter
    apply_modifier(low,modifier.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

    edit=bmesh.new()
    edit.from_mesh(low.data)
    cavity_faces=[f for f in edit.faces if low.data.materials[f.material_index] != atlas]
    if len(cavity_faces)<15:
        raise RuntimeError(f"Boolean did not transfer the cavity tag, got {len(cavity_faces)} faces")
    tagged_count=len(cavity_faces)
    swatch=choose_donor_uv(atlas)
    uv=edit.loops.layers.uv.active
    for face in cavity_faces:
        face.smooth=False
        for loop in face.loops:
            x,y,z=loop.vert.co
            u=(x+y*.53)*.68/.009*swatch["radius"][0]
            v=(2*(z-SLEEVE_CLOSED_BASE)/SLEEVE_HEIGHT-1)*swatch["radius"][1]*.87
            loop[uv].uv=(swatch["center"][0]+u,swatch["center"][1]+v)
        face.material_index=0
    skinny_count=merge_microscopic_cut_slivers(edit,10.,max_edge=.00014)
    bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces))
    groups=component_groups(edit)
    if len(groups)!=1:
        raise RuntimeError(f"Tube has {len(groups)} disconnected islands after cutting")
    if any(len(edge.link_faces)!=2 for edge in edit.edges):
        raise RuntimeError("Sleeve tube is not closed/manifold")
    edit.to_mesh(low.data)
    edit.free()
    low.data.materials.clear()
    low.data.materials.append(atlas)
    low.data.update()
    return {"newLiningFacesBeforeTriangulation":tagged_count,"donorUvSwatch":swatch,"microEdgesCollapsed":skinny_count}


def open_ray_test(data,center,extents):
    mesh=bvh(data)
    z0=float(data[0][:,2].min()-.002)
    z1=float(data[0][:,2].max()+.002)
    inside=[]
    for x in np.linspace(center[0]-extents[0]*.92,center[0]+extents[0]*.92,17):
        for y in np.linspace(center[1]-extents[1]*.92,center[1]+extents[1]*.92,11):
            hit,*_=mesh.ray_cast(Vector((x,y,z0)),Vector((0,0,1)),z1-z0)
            inside.append(hit is None)
    return {"continuousBoreProbes":len(inside),"unobstructed":sum(inside),"passed":all(inside)}


def apply_rope_color(wrapped):
    mat=wrapped.data.materials[0]
    base=next(node for node in mat.node_tree.nodes if node.type=="TEX_IMAGE" and "BaseColor" in node.image.name)
    path=WORK/"WrappedCoreTextures/WrappedCore_ReviewGreyGreen.png"
    base.image=bpy.data.images.load(str(path),check_existing=True)
    base.image.colorspace_settings.name="sRGB"
    return digest(path)


def poses(sleeve,detachable):
    scene=bpy.context.scene
    scene.name="PineInk_SlideBreakAndClose"
    scene.render.fps=30
    scene.render.fps_base=1.
    scene.frame_start=1
    scene.frame_end=222
    for frame,value in ((1,0),(30,0),(69,SLEEVE_TRAVEL),(183,SLEEVE_TRAVEL),(216,.004),(222,.004)):
        sleeve.location=(0,0,value)
        sleeve.keyframe_insert("location",frame=frame,group="Slide the wooden case along the axis")
    detachable.rotation_mode="XYZ"
    for frame,loc,angle,scale in (
        (1,(0,0,0),0,1),(84,(0,0,0),0,1),
        (95,(0,.001,-.003),math.radians(25),1),
        (116,(.017,.003,-.017),math.radians(43),1),
        (155,(.017,.003,-.017),math.radians(43),1),
        (170,(.018,.003,-.018),math.radians(43),.001),
        (222,(.018,.003,-.018),math.radians(43),.001)
    ):
        detachable.location=loc
        detachable.rotation_euler=(0,angle,0)
        detachable.scale=(scale,scale,scale)
        for property_name in ("location","rotation_euler","scale"):
            detachable.keyframe_insert(property_name,frame=frame,group="Snap off the use segment")
    for label,frame in (("Closed",1),("Slide open",69),("Break off",116),("Reclosed",222)):
        scene.timeline_markers.new(label,frame=frame)
    for obj in (sleeve,detachable):
        data=obj.animation_data
        data.action.name="Slide Sleeve" if obj==sleeve else "Break and use segment"
        channelbag=action_get_channelbag_for_slot(data.action,data.action_slot)
        for curve in channelbag.fcurves:
            for key in curve.keyframe_points:
                key.interpolation="LINEAR"
    scene.frame_set(1)


def slide_check(sleeve,core,segment):
    scene=bpy.context.scene
    # Only the geometry in the actual animation is measured, with half-frame BVH sampling.
    failures=[]
    clearances=[]
    times=np.arange(1.,222.01,.5)
    for t in times:
        frame=int(t)
        scene.frame_set(frame,subframe=float(t-frame))
        boxes=[data_of(part) for part in (sleeve,core,segment)]
        trees=[bvh(box) for box in boxes]
        for i,j,role in ((0,1,"sleeve/core"),(0,2,"sleeve/segment"),(1,2,"core/segment")):
            pairs=trees[i].overlap(trees[j])
            if pairs:
                failures.append({"frame":float(t),"pair":role,"triangles":len(pairs)})
        if int(t)==t and frame<=69 and frame%4==1:
            ij=bvh(boxes[0])
            gap=min(ij.find_nearest(Vector(p))[3] for box in boxes[1:] for p in box[0])
            clearances.append((int(frame),float(gap*1000)))
    scene.frame_set(1)
    return {"framesSampled":len(times),"frameInterval":.5,"collisionEvents":failures[:80],
            "collisionEventCount":len(failures),
            "minimumSampledInkVertexDistanceToMovingSleeveMm":min(g[1] for g in clearances),
            "clearanceSamplesFrameMm":clearances}


def wood_case_check(parts):
    prior=set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(CASE),use_anim=False)
    bpy.context.scene.render.fps=30
    bpy.context.scene.render.fps_base=1.
    imported=set(bpy.context.scene.objects)-prior
    entry=next(obj for obj in imported if obj.name=="EntryAnchor")
    sampled=[]
    product=[data_of(part) for part in parts]
    lo=min(float(coords[:,2].min()) for coords,_ in product)
    hi=max(float(coords[:,2].max()) for coords,_ in product)
    for name in ("WoodCase_LOD0","WoodCase_LOD1"):
        render=next(obj for obj in imported if obj.name==name)
        tree=bvh(data_of(render))
        blocked=[]
        for depth in np.arange(-.003,.0301,.0005):
            offset=np.array([entry.location.x,entry.location.y,entry.location.z-depth-lo])
            overlaps=sum(len(tree.overlap(bvh((points+offset,faces)))) for points,faces in product)
            if overlaps:
                blocked.append({"depthMm":round(float(depth*1000),2),"intersections":overlaps})
        sampled.append({"caseLOD":name,"sampleStepMm":.5,"testedDepthRangeMm":[-3,30],"intersections":blocked[:20],
                        "passesClosedAssemblyAt28mmSeat":not any(hit["depthMm"]<=28 for hit in blocked)})
    for obj in list(imported):
        bpy.data.objects.remove(obj,do_unlink=True)
    return {"sourceAssetPath":CASE.relative_to(REPO).as_posix(),"sourceAssetSHA256":digest(CASE),
            "verifiedGeometry":sampled,"chosenDepthBelowMouthMm":28.,
            "upperRopeAboveMouthMmAtChosenDepth":(hi-lo)*1000-28,
            "physicalDeclaredSlotWidthThicknessDepthMm":[38,19,80]}


def export_review(parts,path):
    for item in tuple(bpy.context.selected_objects):
        item.select_set(False)
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    result=bpy.ops.export_scene.gltf(filepath=str(path),export_format="GLB",use_selection=True,
                                  export_animations=True,export_animation_mode="SCENE",export_frame_range=True,
                                  export_anim_slide_to_zero=True,
                                  export_texcoords=True,export_normals=True,export_tangents=True)
    if result != {"FINISHED"}:raise RuntimeError("Animated GLB export failed")
    merge_review_tracks(path)


def merge_review_tracks(path):
    # Blender emits the authored object actions as parallel clips. Preserve every binary sampler.
    with path.open("rb") as file:
        magic,version,total,length,kind=struct.unpack("<4sIIII",file.read(20))
        assert magic==b"glTF" and version==2 and kind==0x4E4F534A and total==path.stat().st_size
        gltf=json.loads(file.read(length))
        binary_chunks=file.read()
    merged={"name":"推开、折段、收套", "channels":[], "samplers":[]}
    for clip in gltf["animations"]:
        offset=len(merged["samplers"])
        merged["samplers"].extend(clip["samplers"])
        for channel in clip["channels"]:
            merged["channels"].append(dict(channel,sampler=channel["sampler"]+offset))
    targets={(gltf["nodes"][c["target"]["node"]]["name"], c["target"]["path"]) for c in merged["channels"]}
    expected={("SlideSleeve","translation"),("DetachableSegment","translation"),
              ("DetachableSegment","rotation"),("DetachableSegment","scale")}
    if targets!=expected:
        raise RuntimeError("Unexpected animation targets "+str(targets))
    merged["extras"]={"sceneFramesPerSecond":30,"timeline":[
        {"name":"收合","seconds":0.},{"name":"推开","seconds":68/30},
        {"name":"掰下","seconds":115/30},{"name":"收套","seconds":221/30}
    ]}
    gltf["animations"]=[merged]
    content=json.dumps(gltf,ensure_ascii=False,separators=(",",":")).encode("utf-8")
    content+=b" "*((-len(content))%4)
    staging=path.with_suffix(".glb.staging")
    with staging.open("wb") as file:
        file.write(struct.pack("<4sIIII",magic,version,20+len(content)+len(binary_chunks),len(content),kind))
        file.write(content)
        file.write(binary_chunks)
    staging.replace(path)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(PINEINK))
    scene=bpy.context.scene
    core,dose=[scene.objects[name] for name in PART_NAMES]
    core.name="WrappedCore"
    dose.name="DetachableSegment"
    rope_hash=apply_rope_color(core)
    tex_core=image_for(core,"WrappedCore_ReviewGreyGreen.png")
    print("Rope albedo ready",rope_hash,tex_core,flush=True)
    strength_magnitude=SLEEVE_TRAVEL*1000
    source=load_source(SOURCE_SLEEVE,reset=False)
    stripped_verts=sleeve_frame(source)
    atlas=candidate_atlas(source.data.materials[0],"SlideSleeve")
    sleeve=decimate(source,atlas,"SlideSleeve",2840)
    support_upper_sidewalls(sleeve)
    finish=revise_sleeve_textures(atlas,sleeve)
    source.hide_set(True)
    source.hide_render=True
    source.name="SlideSleeve_Source_Hidden"
    point_cloud=np.concatenate([data_of(obj)[0] for obj in (core,dose)])
    outline,center,extents=corridor_profile((point_cloud.min(axis=0),point_cloud.max(axis=0)))
    cut=cut_cavity(sleeve,atlas,outline)
    sleeve_qa=qa_check.run(objects=[sleeve.name],profile="game",eps=.0000016,tol=.00028)
    sleeve_data=data_of(sleeve)
    ray_check=open_ray_test(sleeve_data,center,extents)
    if not sleeve_qa["pass"] or not ray_check["passed"]:
        raise RuntimeError(json.dumps({"geometry":sleeve_qa,"rays":ray_check}))
    parts=[core,sleeve,dose]
    poses(sleeve,dose)
    motion=slide_check(sleeve,core,dose)
    closed=np.concatenate([data_of(part)[0] for part in parts])
    outer_bounds=(np.ptp(closed,axis=0)*1000).tolist()
    fits=wood_case_check(parts)
    triangles={obj.name:len(data_of(obj)[1]) for obj in parts}
    report={
        "phase":"Review only. Three individually downloaded Hunyuan meshes and local fit corrections.",
        "sourceHashes":{name:digest(SOURCE.with_name(name+".glb")) for name in ("WrappedCore","DetachableSegment","SlideSleeve")},
        "triangles":triangles,"totalTriangles":sum(triangles.values()),
        "closedOutsideBlenderWidthThicknessHeightMm":outer_bounds,
        "actualWoodCaseFit":fits,
        "closedPoseRopeTintSHA256":rope_hash,
        "boreCrossSectionWidthThicknessMm":[value*2000 for value in extents],
        "mouthOpenBothEnds":ray_check,"removedDetachedSourceSpeckVertices":stripped_verts,
        "passesGeometricEnvelope":[outer_bounds[0]<38,outer_bounds[2]<80,outer_bounds[1]<19],
        "localCavityProcessing":cut, "sleeveQA":sleeve_qa,
        "sleeveFinish":finish,
        "actionSweep":motion,
        "slideTravelMm":strength_magnitude,
        "designDecisions":[
            "Thumb notch and both original outer bronze ridges remain mapped to the cloud atlas.",
            "A single rounded longitudinal bore uses the same atlas to expose unblocked true sleeve walls.",
            "The detached piece scales out at a distance after visibly breaking off. No recovery or particle implementation is included."
        ],
        "states": {"fps":30,"closedFrame":1,"slideOpenedFrame":69,"detachedFrame":116,"reclosedFrame":222}
    }
    print(json.dumps({key:report[key] for key in ("triangles","closedOutsideBlenderWidthThicknessHeightMm","actualWoodCaseFit","actionSweep")}),flush=True)
    (WORK/"PineInk-AssemblyQA.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    if motion["collisionEventCount"] or not all(f["passesClosedAssemblyAt28mmSeat"] for f in fits["verifiedGeometry"]):
        raise RuntimeError("Review collision gate needs a motion or fit repair")
    scene.frame_set(1)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/"PineInk-Review.blend"),compress=True)
    output=WORK/"PineInk-Review.glb"
    export_review(parts,output)
    gltf=gltf_document(output)
    report["reviewGLB"]={
        "path":output.name,"sha256":digest(output),
        "visibleNodeNames":[item.get("name","") for item in gltf["nodes"]],
        "animationNames":[item.get("name","") for item in gltf.get("animations",[])],
        "animationTargets": [{
            "node":gltf["nodes"][channel["target"]["node"]]["name"],
            "path": channel["target"]["path"]
        } for clip in gltf.get("animations",[]) for channel in clip["channels"]],
        "animationChannels":sum(len(item["channels"]) for item in gltf.get("animations",[]))
    }
    (WORK/"PineInk-AssemblyQA.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    review_shots.run(objects=[sleeve.name],out=str(WORK/"SlideSleeve-Final-Wire.png"),mode="wire",
                     views=["front","right","top","three_quarter"],res=390)
    for name,f in (("Closed",1),("SlideOpen",69),("Detached",116),("Reclosed",222)):
        scene.frame_set(f)
        active=[part for part in parts if part!=dose or f<170]
        if dose not in active:dose.hide_render=True
        studio_asset(camera_focus(active),(name,{"quarter":(1.1,-2.3,1.15)}))
    scene.frame_set(1)


def image_for(obj,file_name):
    material=obj.data.materials[0]
    return any(node.type=="TEX_IMAGE" and node.image and node.image.name==file_name for node in material.node_tree.nodes)


if __name__=="__main__":
    main()

