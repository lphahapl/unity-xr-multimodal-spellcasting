"""C4 shape parameters in metres. +Z up, front -Y, high crown on +X."""
import sys,math
from functools import lru_cache
from pathlib import Path
import numpy as np
import bpy,bmesh
from mathutils import Vector

OLD=Path(__file__).resolve().parents[2]/'R03'
sys.path.insert(0,str(OLD))
sys.dont_write_bytecode=True
from geometry_r03 import section as old_section,profile,slopes,value

Z=np.array([.105,.115,.130,.145,.170,.200,.230,.260,.280,.295,.308,.320,.330,.339,.345,.349,.350])
LEFT=np.array([-.053,-.055,-.049,-.043,-.039,-.041,-.044,-.046,-.041,-.031,-.016,.003,.020,.025,.029,.034,.038])
RIGHT=np.array([.053,.055,.054,.049,.043,.040,.041,.045,.050,.053,.055,.056,.056,.055,.055,.052,.048])
DEPTH=np.array([.106,.103,.080,.052,.048,.046,.046,.047,.047,.046,.044,.042,.039,.034,.032,.026,.020])
RADIUS=np.array([.003,.0035,.004,.004,.004,.004,.004,.004,.004,.004,.004,.004,.004,.004,.004,.004,.003])
PARAMS=[(v,slopes(Z,v)) for v in [LEFT,RIGHT,DEPTH,RADIUS]]
# The shoulder blend finishes with zero slope; continue that same derivative
# into the upper grip instead of reverting to R01's nonzero 145 mm tangent.
for _,tangents in PARAMS[:3]:tangents[3]=0.
BASE_Z=[0,.003,.006,.101,.105]

@lru_cache(maxsize=8192)
def section(z):
    if z<=.105:
        w,d,r=old_section(z);return -w/2,w/2,d,r
    vals=[value(z,Z,v,s) for v,s in PARAMS]
    if .105<z<.145:
        t=(z-.105)/.04;st=t*t*t*(t*(t*6-15)+10)
        vals[2]=.106-(.106-.052)*st
        # Round the real shoulder cross section, not only its shading normals.
        vals[3]=.003+.0028*math.sin(math.pi*t)**2+.001*st
        # Remove the narrow lateral ledge while retaining the approved upper contour.
        vals[0]=-.053+.010*st-.0012*math.sin(math.pi*t)**2
        vals[1]=.053-.004*st+.0012*math.sin(math.pi*t)**2
    return tuple(vals)

@lru_cache(maxsize=8192)
def ring(z,corner=6):
    l,r,d,fillet=section(z);center=(l+r)/2
    # Broad jade faces have a subtle real crown, separate from the 4 mm edge fillet.
    tt=max(0,min(1,(z-.105)/.04));bulge=.002*(tt*tt*(3-2*tt))
    pp=profile(corner,1,r-l,d-2*bulge,min(fillet,(r-l)/2-.0002,(d-2*bulge)/2-.0002))
    return [(x+center,y) for x,y in pp],bulge

def point(z,t,corner=6):
    pp,bulge=ring(z,corner);n=len(pp);j=int(t)%n;f=t-int(t);a=pp[j];b=pp[(j+1)%n]
    x=a[0]+(b[0]-a[0])*f;y=a[1]+(b[1]-a[1])*f
    # Flat-edge parameter spans only. The square insertion tip is mathematically unchanged.
    if j in [corner,3*corner+2]:y+=math.copysign(bulge*math.sin(math.pi*f)**2,y)
    if y<0 and z>.15:
        q=((x+.010)/.022)**2+((z-.228)/.034)**2
        if q<1:y+=.0012*(1-q)**2
    return Vector((x,y,z))

def build_body(name='QingGui_Blockout',corner=6,subdiv=7,dense=False):
    n=4*(corner+1)
    zs=sorted(set(BASE_Z+list(Z[1:])+(list(np.linspace(.105,.35,900))+list(np.linspace(0,.105,400)) if dense else [.108,.111,.118,.122,.126,.134,.138,.142,.150,.155,.185,.215,.245,.270,.287,.302,.314,.325,.335,.342,.347])))
    zs=sorted(set(round(float(z),7) for z in zs))
    verts=[];rings=[];params=[]
    for z in zs:
        ts=list(map(float,range(n)))
        if z>.105 or dense:
            for k in range(4):
                l,r,dd,rr=section(z)
                flat=(r-l-2*rr) if k in [0,2] else (dd-2*rr)
                target=max(1,int(flat/(.00032 if k in [0,2] else .001)))
                steps=min(255 if k in [0,2] else 31,2**max(1,int(math.log2(target)))-1) if dense else subdiv
                ts += [k*(corner+1)+corner+j/(steps+1) for j in range(1,steps+1)]
            ts.sort()
        ids=[]
        for t in ts:ids.append(len(verts));verts.append(tuple(point(z,t,corner)));params.append((z,t))
        rings.append((ts,ids))
    faces=[];uvs=[]
    def uv(t,z):return (.02+.96*t/n,.02+2*z)
    for row in range(len(zs)-1):
        ta,ia=rings[row];tb,ib=rings[row+1];i=j=0
        while i<len(ta) or j<len(tb):
            an=ta[i+1] if i+1<len(ta) else float(n);bn=tb[j+1] if j+1<len(tb) else float(n)
            a=ia[i%len(ia)];b=ib[j%len(ib)]
            if abs(an-bn)<1e-8:
                faces.append((a,ia[(i+1)%len(ia)],ib[(j+1)%len(ib)],b));uvs.append([uv(ta[i],zs[row]),uv(an,zs[row]),uv(bn,zs[row+1]),uv(tb[j],zs[row+1])]);i+=1;j+=1
            elif an<bn:
                faces.append((a,ia[(i+1)%len(ia)],b));uvs.append([uv(ta[i],zs[row]),uv(an,zs[row]),uv(tb[j],zs[row+1])]);i+=1
            else:
                faces.append((a,ib[(j+1)%len(ib)],b));uvs.append([uv(ta[i],zs[row]),uv(bn,zs[row+1]),uv(tb[j],zs[row+1])]);j+=1
    for iz,top in [(0,False),(len(zs)-1,True)]:
        pp=[Vector(verts[v]) for v in rings[iz][1]];center=sum(pp,Vector())/len(pp)
        vi=len(verts);verts.append(tuple(center));params.append((zs[iz],0));ids=rings[iz][1]
        for j,a in enumerate(ids):
            b=ids[(j+1)%len(ids)];f=(vi,a,b) if top else (vi,b,a);faces.append(f)
            uvs.append([((.5 if top else .2)+(verts[v][0]-center.x)*1.6,.86+(verts[v][1]-center.y)*1.6) for v in f])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    uvmap=mesh.uv_layers.new(name='UVMap')
    for p,uv in zip(mesh.polygons,uvs):
        p.use_smooth=len(p.vertices)==4
        for li,co in zip(p.loop_indices,uv):uvmap.data[li].uv=co
    ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    # True open aperture; axis through front/back, wall and bevel are visible from side.
    # A swept round-over cutter makes a continuous real lip. Beveling individual
    # Boolean intersection edges on the dense skin caused alternating clamped
    # bevel widths and the sawtooth lip reported in R01/early R02.
    cv=[];cf=[];count=96 if dense else 48;fil=.00105
    levels=[(-1,0.,-.006)]+[(-1,float(a),fil*(1-math.cos(a))) for a in np.linspace(0,math.pi/2,5)]+[(0,math.pi/2,0.)]+[(1,float(a),-fil*(1-math.cos(a))) for a in np.linspace(math.pi/2,0,5)]+[(1,0.,.006)]
    def front_y(x,z):
        pp,_=ring(z)
        for j in [corner,3*corner+2]:
            aa,bb=pp[j],pp[(j+1)%n]
            if aa[1]<0:
                f=max(0,min(1,(x-aa[0])/(bb[0]-aa[0])));return point(z,j+f).y
        raise RuntimeError('front perimeter edge missing')
    for side,theta,off in levels:
        extra=fil*(1-math.sin(theta))
        for k in range(count):
            a=2*math.pi*k/count;x=.041+(.0065+extra)*math.cos(a);z=.329+(.012+extra)*math.sin(a)
            y=0. if side==0 else (-front_y(x,z) if side>0 else front_y(x,z))+off
            cv.append((x,y,z))
    for row in range(len(levels)-1):
        for k in range(count):cf.append((row*count+k,row*count+(k+1)%count,(row+1)*count+(k+1)%count,(row+1)*count+k))
    cf.append(tuple(range(count-1,-1,-1)));cf.append(tuple((len(levels)-1)*count+k for k in range(count)))
    cm=bpy.data.meshes.new('RoundedApertureCutter');cm.from_pydata(cv,[],cf);cm.update();bm=bmesh.new();bm.from_mesh(cm);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(cm);bm.free()
    cutter=bpy.data.objects.new('Aperture_Cutter',cm);bpy.context.scene.collection.objects.link(cutter)
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Real through aperture','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
    # Geometric round-over on the crown and true hole lip; lower shaft is untouched.
    bm=bmesh.new();bm.from_mesh(ob.data);bm.normal_update();ed=[]
    for e in bm.edges:
        if not e.is_manifold:continue
        vv=list(e.verts)
        crown=all(v.co.z>.3499 for v in vv)
        lip=all(abs(((v.co.x-.041)/.0065)**2+((v.co.z-.329)/.012)**2-1)<.04 for v in vv) and vv[0].co.y*vv[1].co.y>0 and abs(vv[0].co.y-vv[1].co.y)<.003
        if crown and e.calc_face_angle(0)>.45:ed.append(e)
    if ed:bmesh.ops.bevel(bm,geom=ed,offset=.00105,segments=4,profile=.5,affect='EDGES',clamp_overlap=True)
    bm.to_mesh(ob.data);bm.free();ob.data.update()
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Final triangle preview','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=3.5e-5);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-9);bm.to_mesh(ob.data);bm.free();ob.data.update()
    for p in ob.data.polygons:
        p.use_smooth=not(all(abs(ob.data.vertices[v].co.z)<1e-7 for v in p.vertices) or all(abs(ob.data.vertices[v].co.z-.35)<1e-6 for v in p.vertices))
    if not dense:
        normals=[]
        for p in ob.data.polygons:
            vv=[ob.data.vertices[i].co for i in p.vertices]
            cap=all(abs(v.z)<1e-7 for v in vv) or all(abs(v.z-.35)<1e-6 for v in vv)
            wall=all(abs(((v.x-.041)/.0065)**2+((v.z-.329)/.012)**2-1)<.03 for v in vv)
            hole_lip=all(.80<((v.x-.041)/.0065)**2+((v.z-.329)/.012)**2<1.70 for v in vv)
            for li in p.loop_indices:
                v=ob.data.vertices[ob.data.loops[li].vertex_index].co
                if cap:no=Vector((0,0,1 if v.z>.17 else -1))
                elif wall:no=Vector((-(v.x-.041)/.0065**2,0,-(v.z-.329)/.012**2)).normalized()
                elif hole_lip:no=ob.data.vertices[ob.data.loops[li].vertex_index].normal.normalized()
                else:
                    t=(ob.data.uv_layers.active.data[li].uv.x-.02)/.96*n;z=max(0,min(.35,float(v.z)))
                    da=point(z,(t+.0001)%n,corner)-point(z,(t-.0001)%n,corner)
                    dz=point(min(.35,z+.00001),t,corner)-point(max(0,z-.00001),t,corner)
                    no=da.cross(dz).normalized()
                normals.append(tuple(no))
        ob.data.normals_split_custom_set(normals)
    return ob
