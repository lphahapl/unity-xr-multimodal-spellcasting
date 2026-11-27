"""R03 actual reduced-depth grip; untouched R02 section below 127 mm."""
import math
import numpy as np
import bpy
from functools import lru_cache
from mathutils import Vector

OLD_Z=np.array([0,.003,.006,.101,.105,.108,.110,.112,.116,.121,.127,.297,.333,.339,.344,.348,.35])
OLD_W=np.array([.101,.104,.106,.106,.106,.110,.115,.117,.117,.119,.120,.120,.120,.119,.116,.110,.104])
OLD_R=np.array([.003,.003,.003,.003,.003,.004,.005,.006,.006,.008,.009,.009,.009,.009,.010,.011,.012])
UP_Z=np.array([.127,.140,.165,.190,.215,.240,.260,.282,.302,.325,.337,.345,.350])
UP_W=np.array([.120,.118,.111,.106,.104,.104,.106,.110,.112,.112,.111,.107,.100])
UP_D=np.array([.120,.105,.086,.078,.075,.074,.076,.079,.080,.080,.078,.073,.064])
UP_R=np.array([.009,.011,.015,.018,.019,.020,.021,.022,.023,.024,.024,.025,.026])
PROFILE_Z=np.sort(np.r_[OLD_Z[:11],UP_Z[1:],.252])

def slopes(xs,ys):
    h=np.diff(xs);delta=np.diff(ys)/h;m=np.zeros(len(xs));m[0]=delta[0];m[-1]=delta[-1]
    for k in range(1,len(xs)-1):
        if delta[k-1]*delta[k]>0:
            w1=2*h[k]+h[k-1];w2=h[k]+2*h[k-1];m[k]=(w1+w2)/(w1/delta[k-1]+w2/delta[k])
    return m

def value(z,xs,ys,ss):
    k=max(0,min(len(xs)-2,int(np.searchsorted(xs,z)-1)));h=xs[k+1]-xs[k];t=max(0,min(1,(z-xs[k])/h))
    return float((2*t**3-3*t*t+1)*ys[k]+(t**3-2*t*t+t)*h*ss[k]+(-2*t**3+3*t*t)*ys[k+1]+(t**3-t*t)*h*ss[k+1])
OW=slopes(OLD_Z,OLD_W);OR=slopes(OLD_Z,OLD_R)
UW=slopes(UP_Z,UP_W);UD=slopes(UP_Z,UP_D);UR=slopes(UP_Z,UP_R)
# Match the frozen shoulder tangent at the split, instead of changing lower slopes.
UW[0]=UD[0]=UR[0]=0

def section(z):
    if z<=.127:
        w=value(z,OLD_Z,OLD_W,OW);return w,w,value(z,OLD_Z,OLD_R,OR)
    return tuple(value(z,UP_Z,ys,ss) for ys,ss in [(UP_W,UW),(UP_D,UD),(UP_R,UR)])

def profile(corner,side,width=.12,depth=.12,r=.009):
    pts=[];a=width/2-r;b=depth/2-r;centers=[(a,b),(-a,b),(-a,-b),(a,-b)]
    for k,(cx,cy) in enumerate(centers):
        for j in range(corner+1):
            angle=(k*90+j*90/corner)*math.pi/180
            pts.append((cx+r*math.cos(angle),cy+r*math.sin(angle)))
        end=np.array(pts[-1]);nc=centers[(k+1)%4];angle=(k+1)*math.pi/2
        start=np.array([nc[0]+r*math.cos(angle),nc[1]+r*math.sin(angle)])
        for j in range(1,side):pts.append(tuple(end+(start-end)*j/side))
    return pts

def thumb(x,y,z):
    # Real smooth depression, 2 mm deep, 36 x 56 mm; only the front face.
    if y>=0:return 0.
    q=(x/.018)**2+((z-.252)/.028)**2
    return .002*(1-q)**2 if q<1 else 0.

@lru_cache(maxsize=8192)
def perimeter_at(corner,side,z):
    ring=ring_at(corner,side,z);d=[0.]
    for j in range(len(ring)):d.append(d[-1]+math.dist(ring[j],ring[(j+1)%len(ring)]))
    return d

@lru_cache(maxsize=8192)
def ring_at(corner,side,z):return profile(corner,side,*section(z))

def surface(corner,side,z,t):
    ring=ring_at(corner,side,z);i=int(math.floor(t))%len(ring);f=t-math.floor(t)
    a=ring[i];b=ring[(i+1)%len(ring)]
    x=a[0]+f*(b[0]-a[0]);y=a[1]+f*(b[1]-a[1])
    return Vector((x,y+thumb(x,y,z),z))

def segdist(x,z,a,b):
    vx,vz=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((x-a[0])*vx+(z-a[1])*vz)/(vx*vx+vz*vz)))
    return math.hypot(x-a[0]-t*vx,z-a[1]-t*vz)

def relief(x,y,z,arc=0.):
    width,depth,radius=section(z);d=1.;reference_d=1.
    if .305<z<.331:
        d=min(abs(z-.3075),abs(z-.329));local=(arc*12%1-.5)*.029
        path=[(-.015,-.0105),(-.011,-.0105),(-.011,.0075),(.011,.0075),(.011,-.0055),(-.005,-.0055),(-.005,.002),(.004,.002),(.004,-.001)]
        for a,b in zip(path,path[1:]):d=min(d,segdist(local,z-.318,a,b))
    is_front=abs(y)/(depth/2)>=abs(x)/(width/2)
    side_x=x if is_front else y
    flat=(abs(y)>depth/2-.0025) if is_front else (abs(x)>width/2-.0005)
    if flat and .017<z<.299:
        reference_d=segdist(side_x,z,(-.012,.018),(-.012,.295));d=min(d,reference_d)
        for i in range(28):
            h=.020+i*.010;length=.021 if i%5==0 else (.014 if i%2==0 else .009)
            d=min(d,segdist(side_x,z,(-.012,h),(-.012+length,h)))
    groove=max(.00048*math.exp(-(d/.00067)**2),.00090*math.exp(-(reference_d/.00110)**2))
    edge=max(0,(abs(side_x)-(width/2-.007))/.007)
    wear=.000075*edge*math.exp(-((z-.012)/.018)**2)*(0.5+0.5*math.sin(side_x*710+z*1370))
    return groove+wear,min(1,groove/.00048)

def geometry(name,corner,side,zs,detailed=False):
    n=len(profile(corner,side));verts=[];grooves=[];rings=[];parameters=[]
    # Front-edge refinement exists only above the frozen shoulder; lower topology is identical.
    front_edge=3*corner+2*side
    for z in zs:
        ts=list(map(float,range(n)))
        if not detailed and z>.127:
            ts+= [front_edge+f for f in [.2,.35,.5,.65,.8]]
            ts=sorted(ts)
        ids=[]
        for t in ts:
            p=surface(corner,side,z,t)
            w,dep,rr=section(z);bx=w/2-rr;by=dep/2-rr
            normal=Vector((p.x-max(-bx,min(bx,p.x)),p.y-max(-by,min(by,p.y)),0)).normalized()
            distances=perimeter_at(corner,side,z);ii=int(t);ff=t-ii;arc=(distances[ii]+ff*(distances[ii+1]-distances[ii]))/distances[-1]
            dd,g=relief(p.x,p.y,z,arc if z>.127 else t/n) if detailed else (0,0)
            p.x-=dd*normal.x;p.y-=dd*normal.y
            ids.append(len(verts));verts.append(tuple(p));grooves.append(g);parameters.append((z,t))
        rings.append((ts,ids))
    base=profile(corner,side);dist=[0.]
    for j in range(n):dist.append(dist[-1]+math.dist(base[j],base[(j+1)%n]))
    def uv(t,z):
        i=min(n-1,int(t));f=t-i
        row=perimeter_at(corner,side,z);d=row[i]+f*(row[i+1]-row[i])
        return (.5+2.0*(d-row[-1]/2),.02+2.0*z)
    faces=[];uvs=[]
    for iz in range(len(zs)-1):
        ta,ia=rings[iz];tb,ib=rings[iz+1];i=j=0
        while i<len(ta) or j<len(tb):
            an=ta[i+1] if i+1<len(ta) else float(n);bn=tb[j+1] if j+1<len(tb) else float(n)
            av=ia[i%len(ia)];bv=ib[j%len(ib)]
            if abs(an-bn)<1e-9:
                faces.append((av,ia[(i+1)%len(ia)],ib[(j+1)%len(ib)],bv));uvs.append([uv(ta[i],zs[iz]),uv(an,zs[iz]),uv(bn,zs[iz+1]),uv(tb[j],zs[iz+1])]);i+=1;j+=1
            elif an<bn:
                faces.append((av,ia[(i+1)%len(ia)],bv));uvs.append([uv(ta[i],zs[iz]),uv(an,zs[iz]),uv(tb[j],zs[iz+1])]);i+=1
            else:
                faces.append((av,ib[(j+1)%len(ib)],bv));uvs.append([uv(ta[i],zs[iz]),uv(bn,zs[iz+1]),uv(tb[j],zs[iz+1])]);j+=1
    side_count=len(faces)
    for iz,top in [(0,False),(len(zs)-1,True)]:
        c=len(verts);verts.append((0,0,zs[iz]));grooves.append(0);parameters.append((zs[iz],0));ids=rings[iz][1]
        cx=.50 if top else .19
        for j in range(len(ids)):
            a,b=ids[j],ids[(j+1)%len(ids)];inds=(c,a,b) if top else (c,b,a)
            faces.append(inds);uvs.append([(cx+verts[k][0]*2.0,.86+verts[k][1]*2.0) for k in inds])
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();uvmap=me.uv_layers.new(name='UVMap')
    for p,coords in zip(me.polygons,uvs):
        p.use_smooth=p.index<side_count
        for li,co in zip(p.loop_indices,coords):uvmap.data[li].uv=co
    attr=me.attributes.new('Groove','FLOAT','POINT');attr.data.foreach_set('value',grooves)
    ob=bpy.data.objects.new(name,me);bpy.context.scene.collection.objects.link(ob)
    if not detailed:
        bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        mod=ob.modifiers.new('Final triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
        normals=[]
        for p in ob.data.polygons:
            cap=all(abs(ob.data.vertices[vi].co.z-zs[0])<1e-7 for vi in p.vertices) or all(abs(ob.data.vertices[vi].co.z-zs[-1])<1e-7 for vi in p.vertices)
            for li in p.loop_indices:
                vi=ob.data.loops[li].vertex_index;z,t=parameters[vi]
                if cap:normal=Vector((0,0,1 if z>.17 else -1))
                else:
                    da=surface(corner,side,z,(t+.0001)%n)-surface(corner,side,z,(t-.0001)%n)
                    dz=surface(corner,side,min(.35,z+.00001),t)-surface(corner,side,max(0,z-.00001),t)
                    normal=da.cross(dz).normalized()
                normals.append(tuple(normal))
        ob.data.normals_split_custom_set(normals)
    return ob
