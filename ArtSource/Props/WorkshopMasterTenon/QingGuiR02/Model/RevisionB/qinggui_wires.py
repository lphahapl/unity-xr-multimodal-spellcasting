"""Solid tapered surface wires. BVH contact to the final body, never texture impostors."""
import bpy,math,json
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from qinggui_detail import inner
from qinggui_geometry import section

TOP=[[(.024,.315),(.024,.287),(-.036,.291),(-.040,.222)],[(.028,.317),(.030,.277),(-.044,.282),(-.043,.179)],[(.031,.318),(.039,.268),(-.043,.267),(-.040,.157)],[(.034,.320),(.043,.274),(-.031,.256),(-.033,.199)],[(.036,.322),(.042,.287),(-.004,.269),(-.027,.237)],[(-.037,.284),(-.031,.295),(-.020,.299),(-.012,.297)]]
LOWER=[[(-.026,.123),(-.024,.141),(.014,.139),(.021,.162)],[(-.017,.120),(-.009,.137),(.025,.131),(.029,.151)]]
# Local spacing correction: retain the original fan direction, remove two
# crossings introduced when former painted strokes became millimetre wires.
TOP[3]=[(.034,.320),(.043,.274),(-.027,.256),(-.029,.199)]
TOP[4]=[(.036,.322),(.044,.287),(.008,.269),(-.015,.237)]
TOP[5]=[(-.029,.291),(-.024,.297),(-.018,.299),(-.012,.297)]
def bez(cps,n=600):
    p=np.asarray(cps,float)
    return [(1-t)**3*p[0]+3*(1-t)**2*t*p[1]+3*(1-t)*t*t*p[2]+t**3*p[3] for t in np.linspace(0,1,n)]
def paths():
    out=[]
    fan=[]
    for i,c in enumerate(TOP):
        line=bez(c)
        if 0<i<5:
            prev=np.array(fan[-1]);adjust=[]
            for x,z in line:
                if prev[-1,1]<=z<=prev[0,1]:
                    required=float(np.interp(z,prev[::-1,1],prev[::-1,0]))+.0026
                    x=.5*(x+required+math.sqrt((x-required)**2+.0005**2))
                adjust.append((x,z))
            line=adjust
        fan.append(line);out.append((f'FrontFlow{i+1}',-1,line,.00055,False))
    for face in [-1,1]:
        for i,c in enumerate(LOWER):out.append((f'{face}_LowerFlow{i+1}',face,bez(c),.00050,False))
        for i,offset in enumerate([.0012,.0036]):
            line=[]
            for z in np.linspace(.112,.310,850):
                x=inner(z)+offset
                if z>.297:x=max(x,.0355+i*.0024)
                line.append((x,z))
            out.append((f'{face}_Spine{i+1}',face,line,.00044,False))
        out.append((f'{face}_CrownReturn',face,[(section(z)[0]+.0023,z) for z in np.linspace(.276,.345,350)],.00043,False))
        out.append((f'{face}_ApertureRim',face,[(.041+.0078*math.cos(a),.329+.0133*math.sin(a)) for a in np.linspace(0,2*math.pi,500,endpoint=False)],.00046,True))
    return out

def gold_material():
    mat=bpy.data.materials.get('QingGuiR02_GoldWire') or bpy.data.materials.new('QingGuiR02_GoldWire');mat.use_nodes=True
    b=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Base Color'].default_value=(.76,.47,.14,1);b.inputs['Metallic'].default_value=1;b.inputs['Roughness'].default_value=.24
    mat.diffuse_color=(.76,.47,.14,1);return mat

def build_wires(body,name='QingGuiR02_GoldWire_LOD0',level=0):
    bpy.context.view_layer.update();tree=BVHTree.FromObject(body,bpy.context.evaluated_depsgraph_get())
    verts=[];faces=[];uvfaces=[];evidence=[]
    step=[.0050,.009,.016,.00075][level];sides=[6,5,4,12][level]
    def add_tube(points,normals,rad,loop,label):
        if len(points)<3:return
        chart=len(evidence);charts=18
        # At least 0.1 mm of each circular section sits inside the supporting body.
        radii=[rad if loop else .00019+(rad-.00019)*max(0,math.sin(math.pi*i/(len(points)-1)))**.45 for i in range(len(points))]
        centers=[p+n*(r-.00010) for p,n,r in zip(points,normals,radii)]
        n=len(centers);start=len(verts)
        for i,(c,no,r) in enumerate(zip(centers,normals,radii)):
            tangent=(centers[(i+1)%n]-centers[(i-1)%n]).normalized() if loop else (centers[min(n-1,i+1)]-centers[max(0,i-1)]).normalized()
            a=(no-tangent*no.dot(tangent)).normalized();b=tangent.cross(a).normalized()
            for j in range(sides):verts.append(tuple(c+r*(math.cos(2*math.pi*j/sides)*a+math.sin(2*math.pi*j/sides)*b)))
        for i in range(n if loop else n-1):
            for j in range(sides):
                faces.append((start+i*sides+j,start+i*sides+(j+1)%sides,start+((i+1)%n)*sides+(j+1)%sides,start+((i+1)%n)*sides+j))
                uvfaces.append([((chart+.05+.90*u)/charts,.02+.70*v) for u,v in [(j/sides,i/n),((j+1)/sides,i/n),((j+1)/sides,(i+1)/n),(j/sides,(i+1)/n)]])
        if not loop:
            # Small solid rounded end caps, instead of infinitely tapering into decals.
            for i,sgn in [(0,-1),(n-1,1)]:
                tangent=(centers[min(n-1,i+1)]-centers[max(0,i-1)]).normalized()*sgn
                a=normals[i];b=tangent.cross(a).normalized();a=b.cross(tangent).normalized();c=centers[i];r=radii[i];previous=[start+i*sides+j for j in range(sides)]
                def cap_uv(f):
                    return [((chart+.5+.45*(Vector(verts[vi])-c).dot(a)/r)/charts,(.82 if i==0 else .94)+.04*(Vector(verts[vi])-c).dot(b)/r) for vi in f]
                # Preserve ring index orientation by using existing radial vectors.
                for angle in [math.pi/4]:
                    ring=[]
                    for j in range(sides):
                        radial=Vector(verts[start+i*sides+j])-c;ring.append(len(verts));verts.append(tuple(c+tangent*r*math.sin(angle)+radial*math.cos(angle)))
                    for j in range(sides):
                        f=(previous[j],previous[(j+1)%sides],ring[(j+1)%sides],ring[j]);faces.append(f);uvfaces.append(cap_uv(f))
                    previous=ring
                tip=len(verts);verts.append(tuple(c+tangent*r))
                for j in range(sides):
                    f=(previous[j],previous[(j+1)%sides],tip);faces.append(f);uvfaces.append(cap_uv(f))
        gaps=[]
        for i in range(n if loop else n-1):
            j=(i+1)%n
            for f in [.0,.25,.5,.75]:
                c=centers[i].lerp(centers[j],f);rr=radii[i]*(1-f)+radii[j]*f
                hit=tree.find_nearest(c)
                if hit[0] is not None:gaps.append((c-hit[0]).dot(hit[1])-rr)
        evidence.append({'path':label,'samples':n,'diameter_max_m':rad*2,'end_diameter_m':.00038 if not loop else None,'nominal_embedding_m':.00010,'closed_loop':loop,'wire_vertices':len(verts)-start,'contact_probe_count':len(gaps),'max_radial_clearance_m':max(gaps) if gaps else None,'clearance_definition':'signed center-to-body normal distance minus local radius; positive=gap; sampled at quarter segments'})
    for label,side,raw,rad,loop in paths():
        projected=[];normals=[];valid=[]
        for x,z in raw:
            # No ornamental strand is allowed to bridge the aperture or stab
            # through its ring. Leave a physical clearance and rounded end.
            if 'ApertureRim' not in label and ((x-.041)/.0094)**2+((z-.329)/.0149)**2<1:continue
            hit=tree.ray_cast(Vector((float(x),side*.16,float(z))),Vector((0,-side,0)),.32)
            if hit[0] is None:
                if projected and not loop:break
                continue
            p,no=hit[0],hit[1]
            if no.y*side<.12:
                if projected and not loop:break
                continue
            projected.append(p);normals.append(no.normalized())
        if not projected:continue
        chosen=[0];last=0;dist=0.
        for i in range(1,len(projected)):
            dist+=(projected[i]-projected[i-1]).length
            if dist>=step:
                chosen.append(i);dist=0;last=i
        if not loop and chosen[-1]!=len(projected)-1:chosen.append(len(projected)-1)
        if loop and len(chosen)<20:chosen=list(np.linspace(0,len(projected)-1,20,dtype=int))
        # Extra stations only where a chord would lift away from a concave
        # support surface. Nominal 5 mm spacing is not used as a blind shortcut.
        for _ in range(5):
            new=[];added=False
            for a,b in zip(chosen,chosen[1:]):
                new.append(a);m=(a+b)//2
                if m==a:continue
                chord=projected[a].lerp(projected[b],(m-a)/(b-a))
                if (chord-projected[m]).dot(normals[m])>.000035:new.append(m);added=True
            new.append(chosen[-1]);chosen=new
            if not added:break
        add_tube([projected[i] for i in chosen],[normals[i] for i in chosen],rad,loop,label)
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();uv=me.uv_layers.new(name='WireUV')
    for p,uvs in zip(me.polygons,uvfaces):
        p.use_smooth=True
        for li,co in zip(p.loop_indices,uvs):uv.data[li].uv=co
    import bmesh
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free();me.update()
    # One cylindrical chart and two end disks per strand; no triangle-by-triangle
    # seams that would unnecessarily duplicate GPU vertices for a uniform metal.
    ob=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(ob);ob.data.materials.append(gold_material());ob['intentional_connected_to_body']='Independent solid metal strands, each partially embedded; disconnected mesh islands intentional.'
    return ob,evidence
