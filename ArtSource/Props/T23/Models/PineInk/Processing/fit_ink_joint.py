"""Fit the independent cloud-derived ink halves to one measured, watertight seam."""
import json
import math
import sys
from pathlib import Path
from types import SimpleNamespace

sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))

import bmesh
import bpy
import numpy as np
from mathutils import Matrix,Vector

from audit_wrapped_core import WORK,SOURCE,bbox,data_of,digest,load_source,qa_check,review_shots,studio_asset
from inspect_joint import area,boundary_loops,clipped_bmesh,component_groups,object_in_new_frame
from optimize_wrapped_core import bvh,candidate_atlas,decimate,export_glb

PARTS=("WrappedCore","DetachableSegment")
RAW_CLIP={"WrappedCore":.16,"DetachableSegment":.75}
WIDTH=.014
THICKNESS=.008
PART_HEIGHT=.018
ASSEMBLED_GAP=.00014
SEAM_CENTERS={PARTS[0]:ASSEMBLED_GAP*.5,PARTS[1]:-ASSEMBLED_GAP*.5}


def flare_center(obj):
    # Straighten the cloud fragment using its observed central-body outline.
    data=data_of(obj)
    coords,faces=data
    ranges=[]
    low,high=coords[:,2].min(),coords[:,2].max()
    triangles=coords[faces]
    for f in np.linspace(.18,.72,13):
        z=low+(high-low)*f
        intersects=triangles[(triangles[:,:,2].min(axis=1)<z)&(triangles[:,:,2].max(axis=1)>z)]
        edge_x=[]
        for triangle in intersects:
            for a,b in zip(triangle,np.roll(triangle,-1,axis=0)):
                if (a[2]-z)*(b[2]-z)<0:
                    t=(z-a[2])/(b[2]-a[2])
                    edge_x.append(a[0]+(b[0]-a[0])*t)
        ranges.append((z,(min(edge_x)+max(edge_x))*0.5))
    alpha=-math.atan(float(np.polyfit(np.array(ranges)[:,0],np.array(ranges)[:,1],1)[0]))
    obj.data.transform(Matrix.Rotation(alpha,4,"Y"))
    bpy.context.view_layer.update()
    return alpha


def choose_donor_uv(atlas):
    material_node=next(node for node in atlas.node_tree.nodes
                       if node.type=="TEX_IMAGE" and node.image and
                          any(label in node.image.name for label in ("BaseColor","ReviewWood")))
    image=material_node.image
    w,h=image.size
    pixels=np.empty(w*h*4,dtype=np.float32)
    image.pixels.foreach_get(pixels)
    rgb=pixels.reshape(h,w,4)[:,:,:3]
    luminance=.2126*rgb[:,:,0]+.7152*rgb[:,:,1]+.0722*rgb[:,:,2]
    # Select a dark, subtly varied 120px swatch. Existing ultraviolet detail stays intact outside the cut.
    candidates=[]
    radius=60
    for x in range(150,w-150,71):
        for y in range(150,h-150,71):
            samples=luminance[y-radius:y+radius:3,x-radius:x+radius:3]
            population=np.percentile(samples,[2,50,98])
            contrast=population[2]-population[0]
            if population[0]<.005 or contrast<.006 or population[2]>.28:continue
            error=abs(population[1]-.047)+abs(contrast-.025)*.35
            candidates.append((error,(x/w,y/h),(radius/w,radius/h),population.tolist()))
    if not candidates:
        raise RuntimeError("No dark swatch in atlas "+atlas.name)
    best=min(candidates,key=lambda row:row[0])
    return {"center":best[1],"radius":best[2],"linearLuminance2th50th98th":best[3]}


def perimeter_normalized(edit, name):
    groups=component_groups(edit)
    pieces=[]
    for group in groups:
        for loop in boundary_loops(edit,group):
            pieces.append((area(loop),group,loop))
    pieces.sort(key=lambda item:item[0],reverse=True)
    if not pieces:
        raise RuntimeError("No closed junction loop on "+name)
    marked_areas=[float(part[0]) for part in pieces]
    _, exterior, loop=pieces[0]
    keep=set(exterior)
    internal=[v for v in edit.verts if v not in keep]
    if internal:
        bmesh.ops.delete(edit,geom=internal,context="VERTS")
    loops=boundary_loops(edit,exterior)
    if len(loops)!=1:
        raise RuntimeError(f"{name} needs an independent exterior shell, found {len(loops)} loops")
    return loops[0],marked_areas


def draft(asset, atlas, name, index, budget):
    obj=decimate(asset,atlas,name+f"_Joined_LOD{index}",budget)
    pre_cut=data_of(obj)[0]
    low,high=pre_cut[:,2].min(),pre_cut[:,2].max()
    seam=low+(high-low)*RAW_CLIP[name]
    edit=clipped_bmesh(obj,seam,keep_upper=name=="WrappedCore")
    edit.faces.layers.int.new("fracture_cap")
    ring,areas=perimeter_normalized(edit,name)
    outline=np.array([list(v.co)[:2] for v in ring])
    lo,hi=outline.min(axis=0),outline.max(axis=0)
    center=(lo+hi)/2
    ratio=np.array([WIDTH,THICKNESS])/(hi-lo)
    retained_height=high-seam if name=="WrappedCore" else seam-low
    seam_z=SEAM_CENTERS[name]
    for v in edit.verts:
        xy=(np.array(v.co[:2])-center)*ratio
        v.co=(float(xy[0]),float(xy[1]),float((v.co.z-seam)*PART_HEIGHT/retained_height+seam_z))
    edit.normal_update()
    measured_ring=np.array([list(v.co)[:2] for v in ring])
    return obj,edit,ring,measured_ring,areas


def radii_at(outline, angles):
    starts=outline
    edges=np.roll(outline,-1,axis=0)-starts
    numerator=starts[:,0]*edges[:,1]-starts[:,1]*edges[:,0]
    radii=[]
    for theta in np.atleast_1d(angles):
        direction=np.array([math.cos(theta),math.sin(theta)])
        den=direction[0]*edges[:,1]-direction[1]*edges[:,0]
        onward=np.abs(den)>1e-12
        distances=numerator[onward]/den[onward]
        a=starts[onward]
        at=(a[:,0]*direction[1]-a[:,1]*direction[0])/den[onward]
        valid=(at>=-1e-6)&(at<=1+1e-6)&(distances>0)
        if not np.any(valid):
            raise RuntimeError("Junction outline cannot answer a radial query")
        radii.append(float(np.max(distances[valid])))
    return np.array(radii)


def merge_microscopic_cut_slivers(edit,seam_z,max_edge=.000038):
    tolerance=.000011
    bmesh.ops.remove_doubles(edit,verts=list(edit.verts),dist=tolerance)
    bmesh.ops.triangulate(edit,faces=list(edit.faces))
    merged=0
    for _ in range(4):
        bad=[face for face in edit.faces if face.calc_area()<1e-10]
        if not bad:
            break
        for face in bad:
            if not face.is_valid:
                continue
            edge=min(face.edges,key=lambda candidate:candidate.calc_length())
            if edge.calc_length()>max_edge:
                dimensions=[round(e.calc_length()*1e6,3) for e in face.edges]
                raise RuntimeError(f"A large cut triangle needs a geometric repair: {dimensions} um")
            a,b=edge.verts
            merged_at=a.co.lerp(b.co,.5)
            if min(abs(point.co.z-seam_z) for point in (a,b))<1e-7:
                merged_at.z=seam_z
            bmesh.ops.pointmerge(edit,verts=[a,b],merge_co=merged_at)
            merged+=1
    if any(face.calc_area()<1e-10 for face in edit.faces):
        raise RuntimeError("Cut repair left a degenerate triangle")
    return merged


def cap_outline(edit,cap_layer):
    def cap(face):
        return face[cap_layer] == 1

    neighbors={}
    for edge in edit.edges:
        if len(edge.link_faces)==2 and sum(cap(face) for face in edge.link_faces)==1:
            a,b=edge.verts
            neighbors.setdefault(a,[]).append(b)
            neighbors.setdefault(b,[]).append(a)
    if not neighbors or any(len(adj)!=2 for adj in neighbors.values()):
        raise RuntimeError("Cut cleanup damaged the cap perimeter")
    start=next(iter(neighbors))
    ring=[start]
    last,current=start,neighbors[start][0]
    while current not in ring:
        ring.append(current)
        a,b=neighbors[current]
        last,current=current,b if a==last else a
    if len(ring)!=len(neighbors) or current != start:
        raise RuntimeError(f"Expected a continuous closed cap perimeter, got {len(ring)}/{len(neighbors)}")
    return np.array([list(v.co)[:2] for v in ring]),sum(cap(face) for face in edit.faces)


def fit_arc(part, angle_table, profiles, swatch):
    obj,edit,ring,old_polygon,_=part
    seam_z=SEAM_CENTERS[obj.name.split("_",1)[0]]
    for vert in edit.verts:
        dist=abs(vert.co.z-seam_z)
        if dist>=.0037:
            continue
        reach=max(0,min(1,1-dist/.0037))
        blend=reach*reach*(3-2*reach)
        angle=math.atan2(vert.co.y,vert.co.x)%(2*math.pi)
        radius=math.hypot(vert.co.x,vert.co.y)
        outer=radii_at(old_polygon,[angle])[0]
        target=np.interp(angle,angle_table,profiles,period=2*math.pi)
        ratio=1+(target/outer-1)*blend
        vert.co.x*=ratio
        vert.co.y*=ratio
    for v in ring:
        v.co.z=seam_z
    border=[edge for edge in edit.edges if len(edge.link_faces)==1]
    closed=bmesh.ops.triangle_fill(edit,edges=border,use_beauty=True,use_dissolve=False)
    new_faces=[item for item in closed["geom"] if isinstance(item,bmesh.types.BMFace)]
    if not new_faces:
        raise RuntimeError("Fracture cap could not be triangulated")
    cap_layer=edit.faces.layers.int["fracture_cap"]
    uv=edit.loops.layers.uv.active
    for edge in border:
        edge.smooth=False
    for face in new_faces:
        face.smooth=False
        face[cap_layer]=1
        face.material_index=0
        for loop in face.loops:
            u=(loop.vert.co.x/.0075)*swatch["radius"][0]*.90
            v=(loop.vert.co.y/.0045)*swatch["radius"][1]*.90
            loop[uv].uv=(swatch["center"][0]+u,swatch["center"][1]+v)
    fixed_slivers=merge_microscopic_cut_slivers(edit,seam_z)
    for face in edit.faces:
        if face[cap_layer]==1:
            for v in face.verts:v.co.z=seam_z
    bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces))
    edit.normal_update()
    remaining_boundaries=sum(len(edge.link_faces)!=2 for edge in edit.edges)
    if remaining_boundaries:
        raise RuntimeError(f"{obj.name} has {remaining_boundaries} open or nonmanifold edges after fitting")
    # Keep the final exterior, folded seam UVs and capped face in a single mesh/material.
    fitted_ring,final_cap_faces=cap_outline(edit,cap_layer)
    edit.to_mesh(obj.data)
    obj.data.update()
    edit.free()
    return fitted_ring,final_cap_faces,fixed_slivers


def probe_overlap(a,b):
    A=data_of(a)
    B=data_of(b)
    overlap=bvh(A).overlap(bvh(B))
    return {"triangleIntersections":len(overlap),
            "minimumAxialGapMm":float((min(A[0][:,2])-max(B[0][:,2]))*1000)}


def camera_focus(objects):
    all_points=np.concatenate([data_of(obj)[0] for obj in objects])
    low,high=all_points.min(axis=0),all_points.max(axis=0)
    return SimpleNamespace(
        dimensions=Vector(high-low),matrix_world=Matrix.Identity(4),
        bound_box=[(x,y,z) for x in (low[0],high[0]) for y in (low[1],high[1]) for z in (low[2],high[2])]
    )


def main():
    originals={}
    atlases={}
    frames={}
    paths={name:SOURCE.with_name(name+".glb") for name in PARTS}
    for index,name in enumerate(PARTS):
        original=load_source(paths[name],reset=index==0)
        frames[name]=object_in_new_frame(original,"core" if index==0 else "segment")
        if index==1:
            frames["DetachableRollCorrectionRadians"]=flare_center(original)
        originals[name]=original
        atlases[name]=candidate_atlas(original.data.materials[0],name)
    high_shown={"WrappedCore":7600,"DetachableSegment":4100}
    reduced={"WrappedCore":3300,"DetachableSegment":1750}
    drafts={}
    for name in PARTS:
        drafts[name]=[draft(originals[name],atlases[name],name,i,budget)
                      for i,budget in enumerate((high_shown[name],reduced[name]))]
    swatches={name:choose_donor_uv(atlases[name]) for name in PARTS}
    angles=np.linspace(0,2*math.pi,360,endpoint=False)
    outlines=[radii_at(drafts[name][0][3],angles) for name in PARTS]
    common=np.mean(outlines,axis=0)
    report = {
        "status":"Standalone physical mating proof. Final sleeve clearance and final sizes are pending its cloud asset.",
        "solidGeometryOrigin":{name:digest(paths[name]) for name in PARTS},
        "cutFractionMeasuredFromEachOriginalLowEnd":RAW_CLIP,
        "metersPhysicalFit":True,
        "nominalSeamWidthThicknessMm":[WIDTH*1000,THICKNESS*1000],
        "provisionalAxialHeightEachMm":PART_HEIGHT*1000,
        "provisionalFitBasis":"Current Unity InkRubbingLab graybox envelope x18 y56 z12 mm; individual placeholder ink segments x14 y18 z8 mm.",
        "assembledFixedSeamGapMm":ASSEMBLED_GAP*1000,
        "axis":"Blender +Z is grip-to-top, OpenGL GLB +Y asset, local seam at 0. Detachable pulls along -Z in Blender.",
        "rawToAuthoringFrames":frames,
        "capUvDonorSwatchesOnOriginalDerivedAtlases":swatches,
        "components":{},"combinations":{}, "exports":{}
    }
    rings={}
    for name in PARTS:
        report["components"][name]={}
        for index,part in enumerate(drafts[name]):
            obj,_,_,_,areas=part
            ring,num_faces,fixed_slivers=fit_arc(part,angles,common,swatches[name])
            fitted=radii_at(ring,angles)
            rings[name,index]=fitted
            measure=data_of(obj)
            report["components"][name][f"LOD{index}"]={
                "triangles":len(measure[1]), "boundsBlenderMetres":bbox(measure[0]),
                "sourcePlaneComponentBoundaryAreaSourceUnits":areas,"fractureCapTriangles":num_faces,
                "microscopicCutEdgesCollapsed":fixed_slivers,
                "qa":qa_check.run(objects=[obj.name],profile="game",eps=.0000016,tol=.00028)
            }
    for i in range(2):
        for j in range(2):
            measurements=probe_overlap(drafts[PARTS[0]][i][0],drafts[PARTS[1]][j][0])
            measurements["maximumRimRadialDifferenceMm"]=float(np.max(np.abs(rings[PARTS[0],i]-rings[PARTS[1],j]))*1000)
            measurements["rimRadialDifferenceP95Mm"]=float(np.percentile(np.abs(rings[PARTS[0],i]-rings[PARTS[1],j]),95)*1000)
            report["combinations"][f"CoreLOD{i}_DetachableLOD{j}"]=measurements
    scene=bpy.context.scene
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    for raw in originals.values():
        raw.hide_set(True)
        raw.hide_render=True
    marker=bpy.data.objects.new("BreakJoint_LocalOrigin",None)
    scene.collection.objects.link(marker)
    marker.empty_display_type="ARROWS"
    marker.empty_display_size=.003
    marker.hide_render=True
    marker["reference"]="Shared centered contact face, visual gap .14 mm, no moving sleeve assigned."
    marker.location=(0,0,0)
    marker=bpy.data.objects.new("WrappedGrip_Preliminary",None)
    scene.collection.objects.link(marker)
    marker.empty_display_type="CIRCLE"
    marker.empty_display_size=.002
    marker.hide_render=True
    marker.location=(0,0,.0135)

    for name in PARTS:
        for index,part in enumerate(drafts[name]):
            obj=part[0]
            path=WORK/f"{name}-Mated-LOD{index}.glb"
            export_glb(obj,path)
            report["exports"][path.name]=digest(path)
    for name in PARTS:
        lod1=drafts[name][1][0]
        lod1.hide_set(True)
        lod1.hide_render=True
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/"PineInk-JointFit.blend"),compress=True)
    report_file=WORK/"PineInk-JointFit.json"
    report_file.write_text(json.dumps(report,indent=2),encoding="utf-8")
    print([(name,[record["triangles"] for record in entries.values()]) for name,entries in report["components"].items()],flush=True)
    print(report["combinations"],flush=True)

    for name in PARTS:
        body=drafts[name][0][0]
        review_shots.run(objects=[body.name],out=str(WORK/f"{name}-Mated-Wire.png"),mode="wire",
                        views=["front","right","bottom" if name=="WrappedCore" else "top","three_quarter"],res=380)
    high=drafts["WrappedCore"][0][0]
    low=drafts["DetachableSegment"][0][0]
    studio_asset(camera_focus([high,low]),("Mated",{"front":(0,-1,.2),"quarter":(1.4,-2.2,1.6),"side":(1,.0,.3)}))
    low.location=(.018,0,-.005)
    low.rotation_euler.y=math.radians(21.)
    bpy.context.view_layer.update()
    studio_asset(camera_focus([high,low]),("Separated",{"contact":(1.5,-2.0,1.3),"back":(-1.5,-2.3,-1.1)}))


if __name__=="__main__":
    main()

