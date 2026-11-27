"""Explicit quad annuli on the front/back C4 cap. No Boolean around the hole."""
import bpy,bmesh,math
from mathutils import Vector
from qinggui_geometry import ring,point
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def rebuild(ob):
    me=ob.data;uv=me.uv_layers.active;remove=[]
    for p in me.polygons:
        if min(me.vertices[i].co.z for i in p.vertices)<.3139999:continue
        ts=[(uv.data[li].uv.x-.02)/.96*28 for li in p.loop_indices]
        if all(uv.data[li].uv.y<.74 for li in p.loop_indices) and (all(5.9999<t<7.0001 for t in ts) or all(19.9999<t<21.0001 for t in ts)):remove.append(p.index)
    bm=bmesh.new();bm.from_mesh(me);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in remove],context='FACES');bm.verts.ensure_lookup_table();bm.verts.index_update()
    boundary=[e for e in bm.edges if e.is_boundary];adj={}
    for e in boundary:
        a,b=e.verts;adj.setdefault(a.index,[]).append(b.index);adj.setdefault(b.index,[]).append(a.index)
    assert all(len(v)==2 for v in adj.values()),'Cap boundary must be simple loops'
    loops=[];todo=set(adj)
    while todo:
        first=min(todo);loop=[first];previous=None;cur=first
        while True:
            opts=adj[cur];nxt=opts[0] if opts[0]!=previous else opts[1]
            if nxt==first:break
            loop.append(nxt);previous,cur=cur,nxt
            assert len(loop)<=len(adj)
        todo.difference_update(loop);loops.append(loop)
    assert len(loops)==2,[(len(l),sum(bm.verts[i].co.y for i in l)/len(l)) for l in loops]
    verts=[tuple(v.co) for v in bm.verts];faces=[tuple(v.index for v in f.verts) for f in bm.faces];bm.free()
    N=96;cx=.041;cz=.329;rx=.0065;rz=.012;fil=.00105
    def yface(x,z,side):
        pp,_=ring(z)
        for j in [6,20]:
            a,b=pp[j],pp[(j+1)%28]
            if a[1]*side>0:
                f=max(0,min(1,(x-a[0])/(b[0]-a[0])));return point(z,j+f).y
    def add(p):i=len(verts);verts.append(tuple(p));return i
    bores={}
    for loop in loops:
        side=1 if sum(verts[i][1] for i in loop)>0 else -1
        poly=[Vector((verts[i][0],verts[i][2])) for i in loop];center=Vector((cx,cz))
        def outer(theta):
            d=Vector((math.cos(theta)*rx,math.sin(theta)*rz));hits=[]
            for a,b in zip(poly,poly[1:]+poly[:1]):
                e=b-a;den=cross(d,e)
                if abs(den)<1e-16:continue
                lam=cross(a-center,e)/den;t=cross(a-center,d)/den
                if lam>0 and -.00001<=t<=1.00001:hits.append(lam)
            assert hits,(theta,side)
            return center+d*min(hits)
        rows=[]
        # Inner bore -> true circular fillet -> flat cap -> outer C4 boundary.
        for alpha in [math.pi/2,math.pi/3,math.pi/6,0.]:
            row=[]
            for k in range(N):
                th=2*math.pi*k/N;extra=fil*(1-math.sin(alpha));x=cx+(rx+extra)*math.cos(th);z=cz+(rz+extra)*math.sin(th)
                y=yface(x,z,side)-side*fil*(1-math.cos(alpha));row.append(add((x,y,z)))
            rows.append(row)
        bores[side]=rows[0]
        for f in [.22,.48,.73]:
            row=[]
            for k in range(N):
                th=2*math.pi*k/N;a=Vector((cx+(rx+fil)*math.cos(th),cz+(rz+fil)*math.sin(th)));p=a.lerp(outer(th),f)
                row.append(add((p.x,yface(p.x,p.y,side),p.y)))
            rows.append(row)
        for a,b in zip(rows,rows[1:]):
            for k in range(N):faces.append((a[k],a[(k+1)%N],b[(k+1)%N],b[k]))
        # Only the far outer strip adapts counts; the entire visible hole lip is quads.
        ordered=sorted(loop,key=lambda i:math.atan2((verts[i][2]-cz)/rz,(verts[i][0]-cx)/rx)%(2*math.pi))
        angles=[math.atan2((verts[i][2]-cz)/rz,(verts[i][0]-cx)/rx)%(2*math.pi) for i in ordered];a=rows[-1];i=j=0
        while i<N or j<len(ordered):
            an=2*math.pi*(i+1)/N if i+1<N else 2*math.pi;bn=angles[j+1] if j+1<len(angles) else 2*math.pi
            va=a[i%N];vb=ordered[j%len(ordered)]
            if abs(an-bn)<1e-9:faces.append((va,a[(i+1)%N],ordered[(j+1)%len(ordered)],vb));i+=1;j+=1
            elif an<bn:faces.append((va,a[(i+1)%N],vb));i+=1
            else:faces.append((va,ordered[(j+1)%len(ordered)],vb));j+=1
    front=bores[-1];back=bores[1]
    middle=[add((verts[front[k]][0],0,verts[front[k]][2])) for k in range(N)]
    for a,b in [(front,middle),(middle,back)]:
        for k in range(N):faces.append((a[k],a[(k+1)%N],b[(k+1)%N],b[k]))
    mesh=bpy.data.meshes.new('C4_ExplicitCapAnnuli');mesh.from_pydata(verts,[],faces);mesh.update();bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_edges],context='VERTS');bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bmesh.ops.triangulate(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free();mesh.update();ob.data=mesh
    return {'removed_cap_faces':len(remove),'outer_boundary_counts':[len(l) for l in loops],'hole_segments':N,'annular_rows_per_side':7,'bore_rows':3,'method':'Explicit continuous quad rings, preserved C4 cap outer boundary; no hole Boolean'}
