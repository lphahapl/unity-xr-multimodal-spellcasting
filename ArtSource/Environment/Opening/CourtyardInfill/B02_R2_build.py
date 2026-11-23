"""Build the R2 hipped corner in metres from the approved three A1 meshes.
blender -b --factory-startup -P B02_R2_build.py -- STAGING_DIRECTORY
"""
import math
import sys
from pathlib import Path
import bmesh
import bpy
from mathutils import Vector
from io_scene_fbx import encode_bin, parse_fbx

HERE=Path(__file__).resolve().parent
RUNTIME=HERE.parents[3]/"Assets/Art/Environment/Opening/CourtyardInfill/B02"
OUT=Path(sys.argv[sys.argv.index("--")+1]).resolve()
OUT.mkdir(exist_ok=True,parents=True)
RX,RY=3.055,2.25
Z_EAVE,Z_APEX=3.70,4.65
POST_LIFT=.64
POSTS=((-0.83,-1.365),(-.83,1.365),(1.365,-.81),(1.365,.82))


def uv(kind,u,v,i=0):
    boxes={"wood":(1380,328,480,260),"end":(1380+120*(i%4),588,120,120),
           "clay":(30,1480,265,420),"ridge":(345,1590,175,250),"lip":(1380,708,480,100)}
    x,y,w,h=boxes[kind]
    return ((x+6+(w-12)*u)/2048,1-(y+6+(h-12)*v)/2048)


class Builder:
    def __init__(self,name):
        self.name=name
        self.vertices=[]
        self.lookup={}
        self.faces=[]
        self.maps=[]

    def face(self,points,coords):
        ids=[]
        tex=[]
        for p,st in zip(points,coords):
            k=tuple(round(float(v),7) for v in p)
            if k not in self.lookup:
                self.lookup[k]=len(self.vertices)
                self.vertices.append(k)
            idx=self.lookup[k]
            if idx not in ids:
                ids.append(idx)
                tex.append(st)
        if len(ids)>=3:
            self.faces.append(ids)
            self.maps.append(tex)

    def finish(self):
        m=bpy.data.meshes.new(self.name)
        m.from_pydata(self.vertices,[],self.faces)
        m.update()
        layer=m.uv_layers.new(name="UVMap")
        for p,coords in zip(m.polygons,self.maps):
            for j,st in zip(p.loop_indices,coords):
                layer.data[j].uv=st
        bm=bmesh.new()
        bm.from_mesh(m)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
        bmesh.ops.triangulate(bm,faces=bm.faces[:])
        bm.to_mesh(m)
        bm.free()
        o=bpy.data.objects.new(self.name,m)
        group.objects.link(o)
        m.materials.append(mat)
        return o


def wood(name,a,b,w,d,i=0):
    axis=(Vector(b)-Vector(a)).normalized()
    side=Vector((-axis.y,axis.x,0)).normalized()
    up=axis.cross(side)
    rectangle=[(-1,-1),(1,-1),(1,1),(-1,1)]
    rings=[[Vector(c)+side*(x*w/2)+up*(y*d/2) for x,y in rectangle] for c in (a,b)]
    part=Builder(name)
    for j in range(4):
        k=(j+1)%4
        part.face([rings[0][j],rings[0][k],rings[1][k],rings[1][j]],
                  [uv("wood",.10+.16*((i+j)%4)+u,v) for u,v in [(0,.04),(.17,.04),(.17,.96),(0,.96)]])
    for ring in rings:
        part.face(ring,[uv("end",.5+.48*x,.5+.48*y,i) for x,y in rectangle])
    return part.finish()


def roof_z(r,s):
    # Battered roof plane with a gentle lift towards the four corners.
    return Z_EAVE+(Z_APEX-Z_EAVE)*(1-r)**1.19+.044*r**3*abs(s)**3


def world(side,s,dist,z):
    return ((s,-dist,z),(dist,s,z),(-s,dist,z),(-dist,-s,z))[side]


def strip(a,b):
    i=j=0
    while i<len(a)-1 or j<len(b)-1:
        ai=a[i+1][0] if i+1<len(a) else 2
        bj=b[j+1][0] if j+1<len(b) else 2
        face=[a[i]]
        if ai<=bj+1e-8:
            i+=1
            face.append(a[i])
        if bj<=ai+1e-8:
            face.extend((b[j+1],b[j]))
            j+=1
        else:
            face.append(b[j])
        yield face


def canopy(tier):
    part=Builder("01 Fired pan tiles and boarded soffit")
    for side in range(4):
        w,d=(RX,RY) if side%2==0 else (RY,RX)
        count=24 if side%2==0 else 18
        pitch=2*w/count
        # The ceramic lip has the same wave on both skins. Sparse rings behind
        # it transfer the longitudinal relief to the existing A1 normal chart.
        layers=[(.022,0,"crown"),(.54,0,"field"),(.965,0,"seat"),
                (1,0,"mouth"),(1,-.044,"mouth"),(.967,-.094,"return"),
                (.966,-.147,"fascia"),(.56,-.142,"board"),(.022,-.138,"board")]
        if tier==2:
            layers=[layers[i] for i in (0,1,3,4,6,8)]
        elif tier==0:
            layers[1:2]=[(r,z,"course") for r,z in ((.32,.010),(.333,0),(.57,.010),(.583,0),(.77,.010),(.783,0))]
        rows=[]
        for r,offset,kind in layers:
            limit=w*r
            samples={-limit,0.,limit}
            if kind=="mouth" and tier<2:
                phases=(.16,.5,.84) if tier==0 and offset==0 else (0,.5)
                samples.update(s for i in range(0,count,2 if tier==1 else 1) for f in phases if -limit<(s:=-w+(i+f)*pitch)<limit)
            elif kind=="seat" and tier==0:
                samples.update(s for i in range(count) if -limit<(s:=-w+(i+.5)*pitch)<limit)
            elif kind in ("field","return") and tier<2:
                step=pitch*(4 if tier==0 else 8)
                samples.update(k*step for k in range(-count,count) if -limit<k*step<limit)
            row=[]
            for s in sorted(samples):
                frac=s/limit
                wave=(1-math.cos(2*math.pi*(s+w)/pitch))*.5
                lift=.032*wave**2 if kind=="mouth" and tier<2 else 0.
                z=roof_z(r,frac)+offset+lift
                row.append(((frac+1)/2,world(side,s,r*d,z),s,r,z))
            rows.append(row)
        for j,(a,b) in enumerate(zip(rows,rows[1:])):
            ka,kb=layers[j][2],layers[j+1][2]
            for face in strip(a,b):
                coords=[]
                positive=sum(q[2] for q in face)>=0
                for frac,pt,s,r,z in face:
                    if ka=="mouth" and kb=="mouth":
                        coords.append(uv("lip",.05+.90*(s+w)/(2*w),.1+.78*(any(pt==v[1] for v in b))))
                    elif ka in ("return","fascia","board"):
                        # Fascia thickness is vertical. Board grain follows the bay.
                        cross=(z-3.49)/.33 if ka=="return" else r
                        coords.append(uv("wood",.05+.45*cross,.04+.92*(s+w)/(2*w)))
                    elif ka=="mouth":
                        coords.append(uv("lip",.1+.80*(s+w)/(2*w),.15+(.92-r)*8+.6))
                    else:
                        along=s if positive else s+w
                        coords.append(((26+280*r)/2048,1-(1412+44.8*along/pitch)/2048))
                part.face([v[1] for v in face],coords)
    # Top and underside are capped separately. The turned ceramic cap beds
    # over the top polygon; no hole leads into the roof cavity.
    for r,offset in ((.022,0),(.022,-.138)):
        row=[(x*RX*r,y*RY*r,roof_z(r,1)+offset) for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))]
        # Edge middle vertices are also present in the adjacent four strips.
        center=(0.,0.,Z_APEX+offset)
        for a,b in zip(row,row[1:]+row[:1]):
            mid=((a[0]+b[0])/2,(a[1]+b[1])/2,roof_z(r,0)+offset)
            for p,q in ((a,mid),(mid,b)):
                tri=[p,q,center]
                part.face(tri,[uv("clay",.5+x*3,.5+y*3) for x,y,z in tri])
    return part.finish()


def clay_eave_caps(tier):
    if tier != 0:
        return
    # One solid terracotta roll per column. It physically rounds the near
    # slope and drops under the hip bed. Course laps also read in the atlas.
    outline=((-1,-.016),(-1,.008),(-.60,.034),(0,.044),(.60,.034),(1,.008),(1,-.016))
    for side in range(4):
        w,d=(RX,RY) if side%2==0 else (RY,RX)
        count=24 if side%2==0 else 18
        pitch=2*w/count
        for k in range(1,count-1):
            s=-w+(k+.5)*pitch
            start=max(.075,(abs(s)+.093)/w)
            if start>.95:
                continue
            spacing=.30+.018*math.sin((k+side)*2.17)
            profile=[(.999,.012,1.)]
            for i in (1,2):
                r=.999-spacing*i/d
                if r-.016/d>start+.038/d:
                    profile.extend(((r,.002,.90),(r-.016/d,.016,1.02)))
            profile.append((start,-.021,.83))
            part=Builder("03b Fired barrel %s-%02d"%(side,k))
            stations=[]; maps=[]
            for r,lift,span in profile:
                row=[]; tex=[]
                for x,h in outline:
                    transverse=s+x*.082*span
                    z=roof_z(r,transverse/(r*w))+h+lift
                    row.append(world(side,transverse,r*d,z))
                    along=transverse if s>=0 else transverse+w
                    tex.append(((26+280*r)/2048,1-(1412+44.8*along/pitch+83*h)/2048))
                stations.append(row)
                maps.append(tex)
            for a in range(len(stations)-1):
                for j in range(len(outline)):
                    m=(j+1)%len(outline)
                    face=[stations[a][j],stations[a][m],stations[a+1][m],stations[a+1][j]]
                    part.face(face,[maps[a][j],maps[a][m],maps[a+1][m],maps[a+1][j]])
            for row in (stations[0],stations[-1]):
                part.face(row,[uv("lip",.16+.10*x,.42+z*4.) for x,z in outline])
            cap=part.finish()
            axis=Vector(world(side,0,1,0))
            for polygon in cap.data.polygons:
                if polygon.normal.z>.22 and abs(polygon.normal.dot(axis))<.62:
                    polygon.use_smooth=True


def hip(sx,sy,tier):
    part=Builder("02 Roll tile over hip")
    samples=((.023,.32,.334,.66,.674,.998) if tier==0 else
             (.023,.48,.998) if tier==1 else (.023,.998))
    section=[(-1,-.032),(-.85,.018),(0,.054),(.85,.018),(1,-.032)] if tier==0 else [(-1,-.026),(0,.046),(1,-.026)]
    normal=Vector((-sy*RY,sx*RX)).normalized()
    rows=[]
    for r in samples:
        row=[]
        for f,dz in section:
            width=.075*min(1,.55+r*2)
            x=sx*r*RX+normal.x*f*width
            y=sy*r*RY+normal.y*f*width
            radial=min(1,max(abs(x/RX),abs(y/RY)))
            frac=min(abs(x/RX),abs(y/RY))/radial
            z=roof_z(radial,frac)+dz+(.008 if r in (.32,.66) else 0)
            row.append((x,y,z))
        rows.append(row)
    for a,b in zip(rows,rows[1:]):
        for j in range(len(section)):
            k=(j+1)%len(section)
            part.face([a[j],a[k],b[k],b[j]],[uv("ridge",u,v) for u,v in [(.08,.1),(.08,.87),(.92,.87),(.92,.1)]])
    for row in (rows[0],rows[-1]):
        part.face(row,[uv("lip",.5+.37*f,.42+dz*6) for f,dz in section])
    return part.finish()


def finial(tier):
    part=Builder("03 Short fired crown")
    n=(8,7,5)[tier]
    profile=[(.129,4.610),(.123,4.669),(.076,4.691),(.100,4.720),(.048,4.788)]
    if tier:
        profile=[profile[i] for i in (0,1,3,4)]
    if tier==2:
        profile.pop(1)
    circle=[(math.cos(2*math.pi*i/n),math.sin(2*math.pi*i/n)) for i in range(n)]
    rows=[[(a*r,b*r,z) for a,b in circle] for r,z in profile]
    for a,b in zip(rows,rows[1:]):
        for j in range(n):
            k=(j+1)%n
            part.face([a[j],a[k],b[k],b[j]],[uv("lip",u,v) for u,v in [(.05,.15),(.25,.15),(.25,.87),(.05,.87)]])
    for row in (rows[0],rows[-1]):
        part.face(row,[uv("lip",.5+.43*x,.5+.43*y) for x,y in circle])
    return part.finish()


def floor_deck():
    # Lower the two areas buried under existing scene boards by 1.5 mm.
    # Each exposed deck has its finished surface at exactly the prefab height.
    part=Builder("04a Stone platform with two recessed joints")
    xy=[(-1.5,-1.47),(-1.45,-1.47),(1.47,-1.47),(1.47,.10),
        (1.47,1.5),(.03,1.5),(-1.47,1.5),(-1.47,1.47),
        (-1.5,1.47),(-1.45,1.47),(.03,.10)]
    regions=[([0,1,9,7,8],.0935),([1,2,3,10,5,6,7,9],.095),([3,4,5,10],.0935)]
    heights={i:set() for i in range(len(xy))}
    edges={}
    def pos(i,z):
        return (*xy[i],z)
    def lid(indices,z):
        part.face([pos(i,z) for i in indices],
                  [(.02+.28*(xy[i][0]+1.5)/3,.36+.29*(xy[i][1]+1.47)/2.97) for i in indices])
    for ids,z in regions:
        lid(ids,z)
        for i,j in zip(ids,ids[1:]+ids[:1]):
            heights[i].add(z)
            edges.setdefault(tuple(sorted((i,j))),[]).append(z)
    for (i,j),zz in edges.items():
        low,high=(0,zz[0]) if len(zz)==1 else sorted(zz)
        if low==high:continue
        points=[pos(i,low),pos(j,low)]
        points.extend(pos(j,v) for v in sorted(heights[j]) if low<v<high)
        points.extend([pos(j,high),pos(i,high)])
        points.extend(pos(i,v) for v in sorted(heights[i],reverse=True) if low<v<high)
        part.face(points,[(.03+.08*k,.47+.17*z) for x,y,z in points
                          for k in [int((x,y)==xy[j])]])
    lid(list(range(9)),0)
    return part.finish()


def retain_base(source):
    # Reuse the actual A1 floor, four stone feet, shafts and bearing blocks
    # from this tier. Discard old mitred roof and beams by connected component.
    m=source.data.copy()
    bm=bmesh.new()
    bm.from_mesh(m)
    pending=set(bm.verts)
    discard=[]
    while pending:
        first=pending.pop()
        verts={first}
        stack=[first]
        while stack:
            batch={e.other_vert(v) for v in [stack.pop()] for e in v.link_edges}&pending
            pending-=batch
            verts|=batch
            stack.extend(batch)
        low=min(v.co.z for v in verts)
        high=max(v.co.z for v in verts)
        if high<.098:
            discard.extend(verts)
        elif low<.10:
            continue
        elif .18<low<.20 and high>2.7:
            for v in verts:
                if v.co.z>2:v.co.z+=POST_LIFT
        elif 2.61<low<2.63 and high<2.80:
            for v in verts:v.co.z+=POST_LIFT
        else:
            discard.extend(verts)
    bmesh.ops.delete(bm,geom=discard,context="VERTS")
    bmesh.ops.triangulate(bm,faces=bm.faces[:])
    bm.to_mesh(m)
    bm.free()
    ob=bpy.data.objects.new("04 Original plinths, posts and capitals",m)
    group.objects.link(ob)
    return ob


def framing(tier):
    # Both y ties seat directly on pairs of original posts; the two top
    # purlins cross them. Knee braces transfer the outboard cantilevers back
    # to the shafts without adding obstacles at pedestrian height.
    for x in (-.83,1.365):
        wood("05 Main transverse tie",(x,-1.81,3.449),(x,1.81,3.449),.177,.181)
    for y in (-1.60,1.60):
        wood("06 Long cantilever girder",(-2.77,y,3.54),(2.77,y,3.54),.167,.154)
    if tier<2:
        for i,(x,y) in enumerate(POSTS):
            sign=-1 if x<0 else 1
            wood("07 Splayed bearing arm",(x,y,3.064),(x+sign*.64,math.copysign(1.60,y),3.545),.102,.139,i)
    # Bearing rails under the eaves land on the cantilever girders.
    for x in (-2.662,2.662):
        wood("08 Eave rail",(x,-1.96,3.617),(x,1.96,3.617),.135,.145)
    if tier==0:
        for y in (-1.96,1.96):
            wood("08 Eave rail",(-2.78,y,3.646),(2.78,y,3.646),.118,.122)
    if tier<2:
        if tier==0:
            for sx,sy in ((1,1),(-1,1),(-1,-1),(1,-1)):
                wood("10 Diagonal hip rafter",(sx*.045,sy*.033,4.421),
                     (sx*RX*.942,sy*RY*.942,3.568),.115,.151)
        for side in range(4):
            w,d=(RX,RY) if side%2==0 else (RY,RX)
            n=(9 if side%2==0 else 5) if tier==0 else 2
            for i in range(n):
                s=(2*(i+.5)/n-1)*w
                frac=s/w
                a=world(side,s,.83*d,roof_z(.83,frac)-.154)
                b=world(side,s,.989*d,roof_z(.989,frac)-.149)
                wood("09 Exposed rafter tail",a,b,.084,.100,i)


def replace_geometry(template,new,path):
    """Preserve original binary node IDs, geometry names and all connections."""
    root,ver=parse_fbx.parse(str(template))
    raw,_=parse_fbx.parse(str(new))
    def geo(tree):
        return next(k for v in tree.elems if v.id==b"Objects" for k in v.elems if k.id==b"Geometry")
    geo(root).elems[:]=geo(raw).elems
    funcs=dict(zip("BCZYILFDRSilfdbc","bool char int8 int16 int32 int64 float32 float64 bytes string int32_array int64_array float32_array float64_array bool_array byte_array".split()))
    def pack(node):
        e=encode_bin.FBXElem(node.id)
        for typ,p in zip(node.props_type,node.props):getattr(e,"add_"+funcs[chr(typ)])(p)
        e.elems=[pack(k) for k in node.elems]
        return e
    encode_bin.write(str(path),pack(root),ver)


bpy.ops.wm.open_mainfile(filepath=str(HERE/"B02_A1.blend"))
bpy.context.preferences.filepaths.save_version=0
mat=bpy.data.materials["B02_Infill"]
original=list(bpy.data.objects)
for o in original:o.hide_set(True);o.hide_render=True
for tier in range(3):
    tag=f"GalleryCorner_LOD{tier}"
    group=bpy.data.collections.new("R2 pavilion - LOD "+str(tier))
    bpy.context.scene.collection.children.link(group)
    retain_base(bpy.data.objects[tag])
    floor_deck()
    canopy(tier)
    clay_eave_caps(tier)
    for sx,sy in ((1,1),(-1,1),(-1,-1),(1,-1)):hip(sx,sy,tier)
    finial(tier)
    framing(tier)
    bpy.ops.object.select_all(action="DESELECT")
    copies=[]
    for part in list(group.objects):
        obj=part.copy()
        obj.data=part.data.copy()
        bpy.context.scene.collection.objects.link(obj)
        copies.append(obj)
        obj.select_set(True)
    bpy.context.view_layer.objects.active=copies[0]
    bpy.ops.object.join()
    export=bpy.context.object
    export.data.calc_loop_triangles()
    print("R2",tag,len(export.data.loop_triangles),flush=True)
    tmp=OUT/"pavilion_payload.fbx"
    bpy.ops.export_scene.fbx(filepath=str(tmp),use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",
        apply_scale_options="FBX_SCALE_UNITS",bake_space_transform=True,use_tspace=True,add_leaf_bones=False,
        bake_anim=False,path_mode="STRIP",mesh_smooth_type="FACE")
    replace_geometry(RUNTIME/(tag+".fbx"),tmp,OUT/(tag+".fbx"))
    bpy.data.objects.remove(export,do_unlink=True)
    group.hide_viewport=group.hide_render=tier!=0
for o in original:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.outliner.orphans_purge(do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/"B02_R2_Pavilion.blend"),compress=True)



