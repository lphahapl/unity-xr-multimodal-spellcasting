"""Decimate the received PineInk rope core with its cloud UVs and audit its joint."""
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
from audit_wrapped_core import WORK, SOURCE, bbox, data_of, digest, load_source, qa_check, review_shots, studio_asset


def bvh(data):
    coords, faces = data
    return BVHTree.FromPolygons([Vector(co) for co in coords],faces.tolist(),all_triangles=True)


def choose_slot(label, stem):
    return WORK/f"{stem}Textures"/f"{stem}_{label}.png"


def candidate_atlas(source, stem="WrappedCore"):
    atlas = source.copy()
    atlas.name=stem+"_Atlas"
    for node in atlas.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        if "metallic" in node.image.name:
            kind = "ORM"
        elif "normal" in node.image.name:
            kind = "NormalGL"
        else:
            kind = "BaseColor"
        img = bpy.data.images.load(str(choose_slot(kind,stem)),check_existing=True)
        img.colorspace_settings.name = "sRGB" if kind=="BaseColor" else "Non-Color"
        node.image=img
    shader = next(node for node in atlas.node_tree.nodes if node.type=="BSDF_PRINCIPLED")
    intensity=shader.inputs["Specular IOR Level"]
    tint=shader.inputs["Specular Tint"]
    if not intensity.is_linked:
        intensity.default_value=.35
    if not tint.is_linked:
        tint.default_value=(1,1,1,1)
    atlas.use_backface_culling=True
    return atlas


def decimate(high, atlas, name, budget):
    low = high.copy()
    low.data = high.data.copy()
    low.name = name
    low.data.name = name
    bpy.context.scene.collection.objects.link(low)
    low.data.materials.clear()
    low.data.materials.append(atlas)
    for entity in list(bpy.context.selected_objects):
        entity.select_set(False)
    low.select_set(True)
    bpy.context.view_layer.objects.active=low
    modifier=low.modifiers.new("Texture-preserving QEM reduction","DECIMATE")
    modifier.decimate_type="COLLAPSE"
    modifier.ratio=budget/50000
    modifier.use_collapse_triangulate=True
    with bpy.context.temp_override(active_object=low,object=low,selected_objects=[low],selected_editable_objects=[low]):
        result=bpy.ops.object.modifier_apply(modifier=modifier.name)
        assert result == {"FINISHED"}
    low.data.calc_loop_triangles()
    return low


def ray_grid(data):
    mesh = bvh(data)
    lo,hi = data[0].min(axis=0),data[0].max(axis=0)
    size=hi-lo
    ray_count=33
    zgrid=[]
    for x in np.linspace(lo[0]+.13*size[0],hi[0]-.13*size[0],ray_count):
        row=[]
        for y in np.linspace(lo[1]+.13*size[1],hi[1]-.13*size[1],ray_count):
            hit,normal,index,distance=mesh.ray_cast(Vector((float(x),float(y),float(lo[2]-.05*size[2]))),
                                                   Vector((0,0,1)),size[2]*1.1)
            row.append(None if hit is None else float(hit.z-lo[2]))
        zgrid.append(row)
    nonempty=np.array([item for row in zgrid for item in row if item is not None])
    percentiles=np.percentile(nonempty,[0,10,25,50,75,90,99,100])
    center=np.array([item for row in zgrid[11:22] for item in row[11:22] if item is not None])
    return {
        "belowRays":ray_count*ray_count,"hits":len(nonempty),
        "zAboveLowestPoint_sourceUnits_percentiles0_10_25_50_75_90_99_100":percentiles.tolist(),
        "medianCenterZAboveLowestPoint_sourceUnits":float(np.median(center)),
        "medianCenterPctOfPartHeight":round(float(np.median(center)/size[2]*100),3),
        "medianAllPctOfPartHeight":round(float(percentiles[3]/size[2]*100),3),
        "nearLowestPlanePct":round(float(np.mean(nonempty<=.04*size[2])*100),3),
        "axisNote":"Probes are from the bare seam end (-Blender Z) across the central 74% of the X,Y footprint."
    }


def section(data, fraction):
    points, faces=data
    lo,hi=points.min(axis=0),points.max(axis=0)
    height=float(lo[2]+fraction*(hi[2]-lo[2]))
    soup=points[faces]
    cross=soup[(soup[:,:,2].min(axis=1)<height)&(soup[:,:,2].max(axis=1)>height)]
    outline=[]
    for tri in cross:
        for x,y in zip(tri,np.roll(tri,-1,axis=0)):
            if (x[2]-height)*(y[2]-height)<0:
                t=(height-x[2])/(y[2]-x[2])
                outline.append((x+t*(y-x))[:2])
    outline=np.array(outline)
    if not len(outline): return None
    p0,p1=outline.min(axis=0),outline.max(axis=0)
    return {
        "heightFraction":fraction,"zSourceUnits":height,
        "spanXySourceUnits":(p1-p0).tolist(),
        "midpointXySourceUnits":((p1+p0)/2).tolist(),
        "intersectedTriangleCount":len(cross)
    }


def compare(high,low):
    source_tree,target_tree=bvh(high),bvh(low)
    sample_low = low[0]
    sample_high = high[0][::2]
    source_to_low = np.array([target_tree.find_nearest(Vector(v))[3] for v in sample_high])
    low_to_source = np.array([source_tree.find_nearest(Vector(v))[3] for v in sample_low])
    height = float(np.ptp(high[0],axis=0)[2])
    def summary(values):
        levels=np.percentile(values,[50,90,95,99,100])
        return dict(sourceUnits=levels.tolist(), percentOfHeight=(levels/height*100).tolist())
    return {
        "sourceVertexToLowSurfacePct50_90_95_99_100":summary(source_to_low),
        "lowVerticesToCloudSurfacePct50_90_95_99_100":summary(low_to_source),
        "sourceSampledVertices":len(sample_high),"candidateSampledVertices":len(sample_low)
    }


def component(obj,high):
    data=data_of(obj)
    return {
        "triangles":len(data[1]),"vertices":len(data[0]),
        "boundsBlenderSourceScale":bbox(data[0]),
        "shapeError":compare(high,data),
        "jointProbe":ray_grid(data),
        "candidateContourSections":[section(data,f) for f in [.07,.12,.16,.21,.26,.31]],
        "qa":qa_check.run(objects=[obj.name],profile="game",eps=.00016,tol=.003)
    }


def add_marker(name, location, size):
    marker=bpy.data.objects.new(name,None)
    bpy.context.scene.collection.objects.link(marker)
    marker.location=location
    marker.empty_display_type="ARROWS"
    marker.empty_display_size=size
    marker.hide_render=True
    return marker


def export_glb(obj,path):
    for selected in list(bpy.context.selected_objects):
        selected.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    result = bpy.ops.export_scene.gltf(
        filepath=str(path),export_format="GLB",use_selection=True,
        export_materials="EXPORT",export_texcoords=True,export_normals=True,
        export_tangents=True,export_animations=False
    )
    if result != {"FINISHED"}: raise RuntimeError(path)


def main():
    high = load_source()
    source_coords = data_of(high)
    atlas = candidate_atlas(high.data.materials[0])
    lods = [decimate(high,atlas,"WrappedCore_LOD0",3600),
            decimate(high,atlas,"WrappedCore_LOD1",1500)]
    high.hide_render=True
    high.hide_set(True)
    high["source_sha256"]=digest(SOURCE)
    high["raw_authoring_note"]="Independent Hunyuan reference. Source-scale geometry, not physical game dimensions."
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    report={
        "sourceFile":SOURCE.name,
        "sourceSHA256":digest(SOURCE),
        "blenderVersion":bpy.app.version_string,
        "state":"derived standalone proof; candidate remains source-scaled until assembly dimensions are selected",
        "axisInterpretation":{
            "gripEnd":"positive Blender Z / positive +Y of Y-up glTF",
            "seamAndDetachableEnd":"negative Blender Z / negative Y of Y-up glTF",
            "recommendedPresentationFront":"Blender +Y (clear rope winding), authored dimensions and roll should be set together with both remaining cloud parts",
            "suggestedFirstFitUpperBoundMm":[30,29,16],
            "firstFitBoundAxisOrder":"Unity X width, Y axial height, Z thickness. Suggested allowance only, not final assembled dimensions.",
            "whyNotNativePose":"Visible rope crown and lower fracture identify the axis. The generated bounding-box major axis points in depth, not along the ink core."
        },
        "sourceJointProbe":ray_grid(source_coords),
        "sourceContourSections":[section(source_coords,f) for f in [.07,.12,.16,.21,.26,.31]],
        "lods":{}
    }
    for pick in lods:
        for obj in lods: obj.hide_set(obj!=pick)
        report["lods"][pick.name]=component(pick,source_coords)
    for obj in lods: obj.hide_set(False)
    lo,hi=source_coords[0].min(axis=0),source_coords[0].max(axis=0)
    ring=section(source_coords,.21)
    x,y=ring["midpointXySourceUnits"]
    marker=add_marker("JointDatum_PROVISIONAL",(x,y,ring["zSourceUnits"]),.055)
    marker["interpretation"]="Reference slice only. Native recessed face has no planar mating surface."
    grip=add_marker("GripAxis_PROVISIONAL",((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]+.77*(hi[2]-lo[2])),.062)
    grip["interpretation"]="On the positive Z rope crown; actual hand grip to be calibrated on assembly."

    glb = WORK/"WrappedCore-Low.glb"
    export_glb(lods[0],glb)
    low_div=WORK/"WrappedCore-LOD1.glb"
    export_glb(lods[1],low_div)
    report["exports"]={path.name:digest(path) for path in (glb,low_div)}
    lods[1].hide_set(True)
    lods[1].hide_render=True
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/"WrappedCore-Processing.blend"),compress=True)
    (WORK/"WrappedCore-Low-QA.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print(f"Core LOD counts: {[(name, item['triangles']) for name,item in report['lods'].items()]}",flush=True)
    print("Native underside",report["sourceJointProbe"],flush=True)
    low=lods[0]
    review_shots.run(objects=[low.name],out=str(WORK/"WrappedCore-Low-Wire.png"),mode="wire",
        views=["front","right","bottom","three_quarter"],res=405)
    studio_asset(low,("Low",{
        "rope":(-1,2.15,1.40),"right":(1,1.4,.45),
        "seam":(.20,.24,-2.35),"split":(-1.35,1.8,-.44)
    }))


if __name__ == "__main__":
    main()

