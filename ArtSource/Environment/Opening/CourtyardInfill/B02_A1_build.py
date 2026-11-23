"""blender -b --factory-startup -P B02_A1_build.py -- STAGE

Run B02_A1_atlas.py STAGE first. Build all six authored LODs from the
original B02_pipeline.blend. Only geometry payloads change in the FBXs.
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from io_scene_fbx import encode_bin, parse_fbx

HERE = Path(__file__).resolve().parent
RUNTIME = HERE.parents[3] / "Assets/Art/Environment/Opening/CourtyardInfill/B02"
OUT = Path(sys.argv[sys.argv.index("--")+1]).resolve()
SPAN = 1.5
EDGE = 1.480
PITCH = .25
REFLECT = lambda p: (-p[1], -p[0], p[2])


def swatch(kind, u, v, variant=0):
    if kind == "clay":
        # Contents of the original roof top UV island. Leave a mip guard.
        return .012 + .305*u, .015 + .297*v
    x, y, w, h = {
        "wood": (1380, 328, 480, 260), "end": (1380+120*(variant%4), 588, 120, 120),
        "lip": (1380, 708, 480, 100)
    }[kind]
    return (x+6+(w-12)*u)/2048, 1-(y+6+(h-12)*v)/2048


class Part:
    def __init__(self, name):
        self.name = name
        self.verts, self.faces, self.uvs, self.indices = [], [], [], {}

    def face(self, points, uvs):
        ids, coords = [], []
        for point, uv in zip(points, uvs):
            key = tuple(round(c, 7) for c in point)
            idx = self.indices.get(key)
            if idx is None:
                idx = len(self.verts)
                self.indices[key] = idx
                self.verts.append(key)
            if idx not in ids:
                ids.append(idx)
                coords.append(uv)
        if len(ids) >= 3:
            self.faces.append(ids)
            self.uvs.append(coords)

    def object(self):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, coords in zip(mesh.polygons, self.uvs):
            for loop, pair in zip(poly.loop_indices, coords):
                uv.data[loop].uv = pair
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        mesh.materials.append(MAT)
        return obj


def h(t):
    return 2.907 + .680*(1-min(1, abs(t)/EDGE))**1.19


def roof_profile(relief):
    # One continuous folded shell. The last 90 mm return underneath each tile
    # before meeting the timber deck. Looking up the barrel exposes its throat.
    upper = [(0, 3.624), (.056, 3.593)]
    for d in (.073, .345, .359, .695, .709, 1.045, 1.059, EDGE):
        step = .012 if d in (.345, .695, 1.045) else 0
        upper.append((d, h(d)+relief*min(1,d/.24)+step))
    upper = [(-t,z) for t,z in reversed(upper[1:])] + upper
    lower = [(EDGE, h(EDGE)+relief-.018), (1.405,h(1.405)+relief-.020),
             (1.385,h(1.385)-.059), (0,h(0)-.064)]
    lower += [(-t,z) for t,z in reversed(lower[:-1])]
    return upper+lower, len(upper)


def roof_strip(first, second):
    """Zip different ring densities; keep every boundary edge in the mesh."""
    a = b = 0
    while a < len(first)-1 or b < len(second)-1:
        na = (first[a+1][0] if a+1 < len(first) else math.inf)
        nb = (second[b+1][0] if b+1 < len(second) else math.inf)
        points = [(first[a][1], 0)]
        if na <= nb + 1e-8:
            a += 1
            points.append((first[a][1], 0))
        if nb <= na + 1e-8:
            points.append((second[b+1][1], 1))
            b += 1
            points.append((second[b-1][1], 1))
        else:
            points.append((second[b][1], 1))
        yield points


def canopy(turn, tier):
    part = Part("Grey pan and cover tiles")
    profile, upper = roof_profile(0)
    cap_profile=profile
    # Terminate the relief behind the mouths, on a narrow band. The band
    # collapses to the original eave point on each plug-in end face.
    collar=1.404
    if tier<2:
        profile=[profile[0],(-collar,h(collar))]+profile[1:upper-1]+[(collar,h(collar))]+profile[upper-1:]
        upper+=2
    waves = (((0,0),(.32,.012),(.5,.060),(.68,.012)),
             ((0,0),(.5,.040)), ((0,0),))[tier]
    rings = []
    for j,(t,z) in enumerate(profile):
        # The eave and its short return have a real varying cross-section.
        # The middle of the slope uses the original straight roof's fluted
        # tangent-space normal swatch and retains the transverse course breaks.
        eave = abs(abs(t)-EDGE) < .0001
        end = -t if turn else SPAN
        points = [(-SPAN,0)]
        if eave and tier < 2:
            samples = [(-SPAN+(i+u)*PITCH,v) for i in range(12) for u,v in waves] + [(SPAN,0)]
            points.extend((s,v) for s,v in samples if -SPAN < s < end-1e-7)
            left,right = next((a,b) for a,b in zip(samples,samples[1:]) if a[0]<=end<=b[0])
            weight = (end-left[0])/(right[0]-left[0])
            last = left[1]+weight*(right[1]-left[1])
        else:
            last=0
            if abs(t)==collar or (tier<2 and abs(t)==1.405):
                # Keep a curved, nearly constant-thickness return behind each
                # tile end. Both rings carry the crest and valley stations.
                lift=(.047 if tier==0 else .030) if abs(t)==collar else .008
                points.extend((s,lift*(i%2)) for i in range(1,24) if (s:=-SPAN+i*PITCH/2)<end-1e-7)
                phase=((end+SPAN)/PITCH)%1
                last=lift*(1-abs(2*phase-1))
                if abs(t)==collar:
                    points.insert(1,(-SPAN+.006,0))
                    if not turn:
                        points.append((SPAN-.006,0))
            elif tier==0 and abs(t)==1.059:
                points.extend((s,0) for i in range(1,12) if (s:=-SPAN+i*PITCH)<end-1e-7)
            elif tier < 2 and (abs(t)>1.04 and abs(t)<1.42):
                points.extend((s,0) for s in (-.75,0,.75) if s < end-1e-7)
        points.append((end,last))
        ring=[]
        for s,v in points:
            y,height=t,z+v
            if abs(t)==collar and (s==-SPAN or (not turn and s==SPAN)):
                y,height=math.copysign(EDGE,t),h(EDGE)
            ring.append(((s+SPAN)/(end+SPAN),(s,y,height)))
        rings.append(ring)
    for j,ring in enumerate(rings):
        k=(j+1)%len(rings)
        is_lip = j in (upper-1,upper,upper+1,len(rings)-2,len(rings)-1)
        for face in roof_strip(ring,rings[k]):
            points=[p for p,edge in face]
            coords=[]
            for (s,t,z),edge in face:
                if is_lip:
                    coords.append(swatch("lip",.08+(s+SPAN)*.28,.13+.72*edge))
                elif j>=upper:
                    coords.append(swatch("wood",.05+.9*(s+SPAN)/3,.05+.90*(t+EDGE)/(2*EDGE)))
                else:
                    # This baked island has a rib every 44.8 texels along V.
                    coords.append(((26+280*abs(t)/EDGE)/2048,1-(1412+(s+SPAN)*179.2)/2048))
            part.face(points,coords)
            if turn:
                part.face([REFLECT(p) for p in points],coords)
    for side in (-1,1):
        points=[(side*SPAN,t,z) for t,z in cap_profile]
        if turn and side==1:
            points=[REFLECT((-SPAN,t,z)) for t,z in cap_profile]
        coords=[swatch("lip", (t+EDGE)/(2*EDGE), (z-2.87)/.8) for t,z in cap_profile]
        part.face(points,coords)
    return part.object()


def member(name, centers, width, height, *, sections=4, taper=None, variant=0):
    """Longitudinal grain, mitred intermediate rings and separate end grain."""
    part=Part(name)
    centers=[Vector(p) for p in centers]
    sizes=taper or [(width,height)]*len(centers)
    axis=(centers[-1]-centers[0]).normalized()
    side=Vector((-axis.y,axis.x,0))
    if side.length<.001:
        side=Vector((1,0,0))
    side.normalize()
    if sections==4:
        profile=[(-1,-1), (1,-1),(1,1),(-1,1)]
    else:
        profile=[(math.cos(2*math.pi*i/sections), math.sin(2*math.pi*i/sections)) for i in range(sections)]
    rings=[]
    dist=[0.]
    for i,(center,(w,d)) in enumerate(zip(centers,sizes)):
        direction=(centers[min(i+1,len(centers)-1)]-centers[max(0,i-1)]).normalized()
        up=direction.cross(side).normalized()
        rings.append([center + side*(x*w/2) + up*(y*d/2) for x,y in profile])
        if i:
            dist.append(dist[-1]+(center-centers[i-1]).length)
    for i,ring in enumerate(rings[:-1]):
        for j in range(sections):
            k=(j+1)%sections
            points=[ring[j],ring[k],rings[i+1][k],rings[i+1][j]]
            base=.04+.106*((j+variant)%7)
            u=[base,base+.17,base+.17,base]
            coords=[swatch("wood",a,.05+.9*b/dist[-1]) for a,b in zip(u,[dist[i]]*2+[dist[i+1]]*2)]
            part.face(points,coords)
    caps=[swatch("end",.5+.48*x,.5+.48*y,variant) for x,y in profile]
    for ring in (rings[0], rings[-1]):
        part.face(ring,caps)
    return part.object()


def post_section(name, x, y, rings, sides, stone, variant):
    part=Part(name)
    circle=[(math.cos(2*math.pi*j/sides),math.sin(2*math.pi*j/sides)) for j in range(sides)]
    rows=[[(x+r*a,y+r*b,z) for a,b in circle] for z,r in rings]
    for i,(lo,hi) in enumerate(zip(rows,rows[1:])):
        for j in range(sides):
            k=(j+1)%sides
            coords=[]
            for u,v in ((j,i),(j+1,i),(j+1,i+1),(j,i+1)):
                if stone:
                    coords.append((.07+.17*u/sides,.42+.045*v/(len(rows)-1)))
                else:
                    coords.append(swatch("wood",.04+.88*u/sides,.04+.92*v/(len(rows)-1)))
            part.face([lo[j],lo[k],hi[k],hi[j]],coords)
    for row in (rows[0],rows[-1]):
        coords=[(.15+.035*a,.48+.035*b) if stone else swatch("end",.5+.45*a,.5+.45*b,variant)
                for a,b in circle]
        part.face(row,coords)
    return part.object()


def framing(turn, tier):
    posts=([(-.83,-1.365),(-.83,1.365),(1.365,-.81),(1.365,.82)] if turn else
           [(x,y) for x in (-.78,.78) for y in (-1.365,1.365)])
    parts=[]
    if tier==0:
        # Individual closed earthenware noses, seated into the half-round
        # crowns. Only the long outer eaves carry complete tiles at the elbow.
        for sign in (-1,1):
            for i in range(12):
                s=-SPAN+(i+.5)*PITCH
                if turn and s+.045 > -sign*EDGE:
                    continue
                for mirrored in range(2 if turn else 1):
                    part=Part("Fired cover tile nose")
                    count=8
                    circle=[(math.cos(2*math.pi*j/count),math.sin(2*math.pi*j/count)) for j in range(count)]
                    def point(x,t,z):
                        p=(s+x,sign*t,2.932+z)
                        return REFLECT(p) if mirrored else p
                    rings=[[point(x*r,t,y*r) for x,y in circle] for r,t in ((.044,1.447),(.041,1.494))]
                    cap_uv=[swatch("lip",.12+.29*(i%3)+.08*x,.5+.39*y) for x,y in circle]
                    # The rear is housed in the tile. Four rear corners carry
                    # the eight-sided visible rim without hidden round bevels.
                    rear,front=rings
                    part.face(rear[::2],cap_uv[::2])
                    part.face(rings[1],cap_uv)
                    for j in range(0,count,2):
                        mid,k=j+1,(j+2)%count
                        part.face([rear[j],rear[k],front[mid]],
                                  [swatch("clay",u,v) for u,v in [(.64,.42),(.70,.42),(.67,.49)]])
                        for a,b,c in ((rear[j],front[mid],front[j]),(rear[k],front[k],front[mid])):
                            part.face([a,b,c],[swatch("clay",u,v) for u,v in [(.67,.42),(.70,.49),(.64,.49)]])
                    parts.append(part.object())
    for i,(x,y) in enumerate(posts):
        sides=(8,6,5)[tier]
        foot=[(.09,.114),(.12,.120),(.20,.089)] if tier==0 else [(.09,.116),(.20,.091)]
        parts.append(post_section("Stone column foot",x,y,foot,sides,True,i))
        parts.append(post_section("Elm column",x,y,[(.194,.081),(2.73,.078)],sides,False,i))
        # Shoulder projects past the shaft and takes the underside of the plate.
        block=[(2.622,.177,.177),(2.705,.248,.236),(2.787,.248,.236)]
        if tier>0:
            block.pop(1)
        parts.append(member("Bearing block",[(x,y,z) for z,w,d in block],0,0,
                            taper=[(w,d) for z,w,d in block],variant=i))
        if tier<2:
            direction=(0,1) if turn and (i>=2 or i==1) else (1,0)
            sign=-1 if i%2 else 1
            if turn and i==1:
                sign=-1
            a=(x,y,2.385)
            b=(x+direction[0]*.35*sign,y+direction[1]*.35*sign,2.792)
            parts.append(member("Housed knee brace",[a,b],.092,.102,variant=i))
    # Continual plates bear the rafter feet. The inner elbow has just 145 mm
    # of run before the mitre, so both legs share one mitred solid.
    for sign in (-1,1):
        t=1.355*sign
        outline=[(t-.071,2.748),(t+.071,2.748),(t+.071,2.877),(t-.071,2.877)]
        part=Part("Longitudinal eave plate")
        rings=[[(-SPAN,y,z) for y,z in outline]]
        if turn:
            rings += [[(-y,y,z) for y,z in outline],[(-y,SPAN,z) for y,z in outline]]
        else:
            rings.append([(SPAN,y,z) for y,z in outline])
        # Split the long runs to keep the longitudinal photo grain at timber
        # scale. Interpolate the mitre profile so adjacent rings still weld.
        spans=rings
        rings=[spans[0]]
        for a,b in zip(spans,spans[1:]):
            steps=max(1,round((Vector(a[0])-Vector(b[0])).length/.88)) if tier==0 else 1
            rings.extend([[Vector(p).lerp(Vector(q),k/steps) for p,q in zip(a,b)] for k in range(1,steps+1)])
        for i in range(len(rings)-1):
            for j in range(4):
                k=(j+1)%4
                part.face([rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]],
                          [swatch("wood",.1+.19*(j%4)+u,v) for u,v in [(0,.03),(.10,.03),(.10,.97),(0,.97)]])
        for ring in (rings[0],rings[-1]):
            part.face(ring,[swatch("end",u,v) for u,v in [(0,0),(1,0),(1,1),(0,1)]])
        parts.append(part.object())
    for x in ((-.83,) if turn else (-.78,.78)):
        parts.append(member("Transverse tie",[(x,-1.433,2.806),(x,1.433,2.806)],.134,.148))
    if turn:
        for y in ((-.81,.82) if tier<2 else (.82,)):
            parts.append(member("Return tie",[(-.87,y,2.816),(1.434,y,2.816)],.128,.146))
        # A wall plate links the inboard post to the return tie at y=.82.
        if tier<2:
            parts.append(member("Inboard bearing",[(-.83,.77,2.812),(-.83,1.438,2.812)],.147,.140))
        parts.append(member("Inner elbow header",[(-1.439,1.366,2.811),(-.806,1.366,2.811)],.124,.118))
    if tier<2:
        for i in (range(6) if tier==0 else (0,2,3,5)):
            s=-1.25+.5*i
            lo,hi=-1.467, min(1.467,-s-.033) if turn else 1.467
            points=[(s,t,h(t)-.089-.034*(1-abs(t)/EDGE)) for t in [lo]+([0] if lo<0<hi else [])+[hi]]
            if tier==1:
                # Retain the visible tails at this tier; the deck hides their
                # upper length. The opposite tail exists only on a straight.
                segments=[[points[0],(s,lo+.17,h(lo+.17)-.089)]]
                if not turn:
                    segments.append([(s,hi-.17,h(hi-.17)-.089),points[-1]])
            else:
                segments=[points]
            for mirrored in range(2 if turn else 1):
                for segment in segments:
                    centers=[REFLECT(p) for p in segment] if mirrored else segment
                    parts.append(member("Round elm rafter",centers,.095,.105,
                                        sections=8 if tier==0 else 5,variant=i+mirrored))
        if turn and tier==0:
            centers=[(-t,t,h(t)-.098) for t in (-1.448,-.72,0,.72,1.448)]
            parts.append(member("Hip and valley bearer",centers,.120,.118,sections=6,variant=2))
    if turn:
        # A narrow weathering roll covers the cut ends on the convex hip.
        part=Part("Bedded hip cover")
        profile=[(-1,-.020),(-1,0),(-.68,.028),(0,.040),(.68,.028),(1,0),(1,-.020)]
        rings=[]
        stops=(.035,.073,.16,.46,.474,.84,.854,1.438) if tier==0 else (.035,.073,.72,1.438)
        if tier>0:
            profile=[(-1,-.020),(0,.040),(1,-.020)]
        roof,count=roof_profile(0)
        offsets,heights=zip(*[(d,z) for d,z in roof[:count] if d>=0])
        for t in stops:
            f=min(1,t/.25)
            ring=[]
            for a,b in profile:
                x,y=t+a*.052*f,-t+a*.052*f
                deck=float(np.interp(max(abs(x),abs(y)),offsets,heights))
                # Seat both shoulders on their own slope, including the
                # steep ridge-to-pan transition at the end of the hip.
                z=deck-.014+b*f+(.009 if t in (.46,.84) else 0)
                ring.append((x,y,z))
            rings.append(ring)
        for i,(a,b) in enumerate(zip(rings,rings[1:])):
            for j in range(len(profile)):
                k=(j+1)%len(profile)
                coords=[swatch("lip",u,v) for u,v in [(.08,.13),(.08,.70),(.91,.70),(.91,.13)]]
                part.face([a[j],a[k],b[k],b[j]],coords)
        for ring in (rings[0],rings[-1]):
            part.face(ring,[swatch("lip",.5+x*.38,.33+y*9) for x,y in profile])
        parts.append(part.object())
    return parts


def base_mesh(name):
    src=bpy.data.objects[name]
    ob=src.copy()
    ob.data=src.data.copy()
    bpy.context.scene.collection.objects.link(ob)
    ob.location=(0,0,0)
    bm=bmesh.new()
    bm.from_mesh(ob.data)
    ids=bm.faces.layers.int["WorkshopBakePart"]
    # Retain the exact floor boundary and its photograph at every distance.
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f[ids]!=1],context="FACES")
    bm.to_mesh(ob.data)
    bm.free()
    return ob


def child(root, tag):
    return next(e for e in root.elems if e.id==tag)


def write_payload(original, fresh, target):
    root,version=parse_fbx.parse(str(original))
    updated,_=parse_fbx.parse(str(fresh))
    geom=child(child(root,b"Objects"),b"Geometry")
    geom.elems[:]=child(child(updated,b"Objects"),b"Geometry").elems
    types=dict(zip("BCZYILFDRSilfdbc",
                   "bool char int8 int16 int32 int64 float32 float64 bytes string int32_array int64_array float32_array float64_array bool_array byte_array".split()))
    def pack(node):
        out=encode_bin.FBXElem(node.id)
        for typ,value in zip(node.props_type,node.props):
            getattr(out,"add_"+types[chr(typ)])(value)
        out.elems=[pack(e) for e in node.elems]
        return out
    encode_bin.write(str(target),pack(root),version)


bpy.ops.wm.open_mainfile(filepath=str(HERE/"B02_pipeline.blend"))
bpy.context.preferences.filepaths.save_version=0
MAT=bpy.data.materials["B02_Infill"]
for im in bpy.data.images:
    role={"albedo2048":"BaseColor","mask2048":"Mask","normal2048":"Normal"}[im.name]
    if im.packed_file:
        im.unpack(method="REMOVE")
    im.filepath=str(OUT/f"B02_{role}.png")
    im.reload()
    im.pack()
    im.filepath=str(RUNTIME/f"B02_{role}.png")
source_objects=list(bpy.data.objects)
for ob in source_objects:
    ob.hide_set(True)
    ob.hide_render=True
for name in ("GalleryStraight","GalleryCorner"):
    turn=name.endswith("Corner")
    for tier in range(3):
        tag=f"{name}_LOD{tier}"
        ob=base_mesh(name)
        ob.hide_set(False)
        ob.hide_render=False
        parts=[ob,canopy(turn,tier)]+framing(turn,tier)
        bpy.ops.object.select_all(action="DESELECT")
        for p in parts:
            p.select_set(True)
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.join()
        ob.name=ob.data.name=tag
        ob.data.calc_loop_triangles()
        print(tag,len(ob.data.loop_triangles),flush=True)
        temp=OUT/"B02_geometry_payload.fbx"
        bpy.ops.export_scene.fbx(filepath=str(temp),use_selection=True,object_types={"MESH"},
            axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_UNITS",
            bake_space_transform=True,use_tspace=True,add_leaf_bones=False,
            bake_anim=False,path_mode="STRIP",mesh_smooth_type="FACE")
        write_payload(RUNTIME/(tag+".fbx"),temp,OUT/(tag+".fbx"))
        ob.location.x=4.5*int(turn)
        ob.hide_set(tier!=0)
        ob.hide_render=tier!=0
for ob in source_objects:
    bpy.data.objects.remove(ob,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/"B02_A1.blend"),compress=True)
