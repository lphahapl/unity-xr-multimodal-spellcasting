"""Rebuild the walk-through Tianyi pavilion and its three independent VR meshes."""
from pathlib import Path
import sys,math,json,subprocess
from mathutils import Vector
import bpy,bmesh
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]; ASSET=ROOT/'Assets/Art/Environment/Opening/TianyiPavilionKit'
sys.dont_write_bytecode=True
sys.path.insert(0,str(HERE))
from bake_trims import select

PHOTO=ROOT/'Assets/Art/Environment/Opening/PhotoArchitecture'
SURFACES=[
    ('Paving',PHOTO/'Flags_Color.jpg',PHOTO/'Flags_Normal.png',.53,.18),
    ('Fieldstone',ASSET/'EarthPlinthColor.png',ASSET/'EarthPlinthNormal.png',.93,.13),
    ('Timber',ASSET/'TimberColor.png',ASSET/'TimberNormal.png',.74,.19),
    ('Tile',ASSET/'TileColor.png',ASSET/'TileNormal.png',.86,.31)]

def fresh_material(name,base,normal=None,strength=.74,smoothness=.19):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    p=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Roughness'].default_value=1-smoothness
    def load(path):
        t=nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(path),check_existing=True)
        return t
    links.new(load(base).outputs['Color'],p.inputs['Base Color'])
    if normal:
        t=load(normal);t.image.colorspace_settings.name='Non-Color'
        n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=strength
        links.new(t.outputs['Color'],n.inputs['Color']);links.new(n.outputs['Normal'],p.inputs['Normal'])
    return mat

class Geometry:
    def __init__(self):
        self.v=[];self.f=[];self.m=[];self.uv=[];self.smooth=[];self.sharp=set()
    def polygon(self,ids,mat,uv=None,wood_axis=None,origin=None,smooth=False,earth=False,single_stone=False):
        self.f.append(tuple(ids));self.m.append(mat)
        self.smooth.append(smooth)
        pts=[Vector(self.v[i]) for i in ids]
        if uv is None:
            edge=max((b-a for a,b in zip(pts,pts[1:]+pts[:1])),key=lambda x:x.length_squared).normalized()
            normal=(pts[1]-pts[0]).cross(pts[2]-pts[0]).normalized()
            if mat==2:
                flow=Vector(wood_axis or edge).normalized()
                centre=Vector(origin) if origin is not None else sum(pts,Vector())/len(pts)
                relative=[p-centre for p in pts]
                if abs(normal.dot(flow))>.78:
                    h=Vector((0,0,1)).cross(normal)
                    if h.length < .01: h=Vector((1,0,0))
                    h.normalize();v=normal.cross(h)
                    # One growth-ring field is .375 units across in the atlas.
                    face_centre=sum(pts,Vector())/len(pts)
                    relative=[p-face_centre for p in pts]
                    span=max(.17,2.45*max(max(abs(q.dot(h)),abs(q.dot(v))) for q in relative))
                    phase=math.sin(centre.x*21.3+centre.y*9.73+centre.z*7.27)
                    angle=2.7*phase; cs=math.cos(angle);sn=math.sin(angle)
                    uv=[(.20+.067*phase+(q.dot(h)*cs-q.dot(v)*sn)*.28/span,
                         .19+(q.dot(h)*sn+q.dot(v)*cs)*.28/span) for q in relative]
                else:
                    h=(flow-normal*normal.dot(flow)).normalized();v=normal.cross(h)
                    seed=math.sin(centre.x*16.1+centre.y*9.31+centre.z*17.3)
                    across=[q.dot(v) for q in relative]
                    mid=(max(across)+min(across))/2
                    scale=min(.39,.53/max(.001,max(across)-min(across)))
                    uv=[(q.dot(h)/2.15+.41*seed,.6875+.025*seed+(a-mid)*scale) for q,a in zip(relative,across)]
            elif mat==1:
                if abs(normal.z)>.90:
                    h=Vector((1,0,0))
                    across=[p.dot(normal.cross(h).normalized()) for p in pts]
                    mid=(max(across)+min(across))/2
                    max_span=.075 if single_stone else .38
                    scale=min(.60,max_span/max(.001,max(across)-min(across)))
                    band_centre=.75 if earth else .048 if single_stone else .30
                    uv=[(p.x/2.65,band_centre+(a-mid)*scale) for p,a in zip(pts,across)]
                else:
                    h=Vector((0,0,1)).cross(normal).normalized()
                    if earth:vs=[.515+.47*(p.z-.43)/.635 for p in pts]
                    elif single_stone:vs=[.009+.073*(p.z+.015)/.305 for p in pts]
                    elif max(p.z for p in pts)<=.001:vs=[.463+.73*p.z for p in pts]
                    else:vs=[.105+.385*(p.z+.015)/.46 for p in pts]
                    uv=[(p.dot(h)/2.65,v) for p,v in zip(pts,vs)]
            elif mat==0:
                h=Vector((1,0,0)) if abs(normal.z)>.8 else Vector((0,0,1)).cross(normal).normalized()
                v=normal.cross(h); rep=2.36
            else:
                h=edge;v=normal.cross(h);rep=.85
            if uv is None:uv=[(p.dot(h)/rep,p.dot(v)/rep) for p in pts]
        self.uv.append(uv)
    def prism(self,verts,mat,wood_axis=None,smooth_sides=False):
        a=len(self.v);self.v.extend(verts);n=len(verts)//2
        centre=sum((Vector(p) for p in verts),Vector())/len(verts)
        def face(ids,smooth=False):self.polygon(ids,mat,wood_axis=wood_axis,origin=centre,smooth=smooth)
        face(range(a+n-1,a-1,-1));face(range(a+n,a+2*n))
        for i in range(n):
            j=(i+1)%n
            face([a+i,a+j,a+n+j,a+n+i],smooth_sides)
    def box(self,ctr,sizes,mat):
        x,y,z=ctr;w,d,h=(s/2 for s in sizes)
        flow=tuple(int(i==sizes.index(max(sizes))) for i in range(3))
        self.prism([(x+u*w,y+v*d,z+k*h) for k in [-1,1] for u,v in [(-1,-1),(1,-1),(1,1),(-1,1)]],mat,flow)
    def beam(self,start,end,width,height,mat,round_sides=0):
        a,b=Vector(start),Vector(end);d=(b-a).normalized()
        e=d.cross(Vector((0,0,1)))
        if e.length<.01:e=Vector((1,0,0))
        e.normalize();f=d.cross(e).normalized()
        section=([(math.cos(i*math.tau/round_sides),math.sin(i*math.tau/round_sides)) for i in range(round_sides)]
                 if round_sides else [(-1,-1),(1,-1),(1,1),(-1,1)])
        if round_sides==10:
            # Keep the original ten-sided sawn end. The opposite end is under
            # the roof boards. Only its lower contact and two shoulders show.
            centre=(a+b)/2
            rows=[]
            anchors=(1,5,8)
            for p,outline in ((a,[section[i] for i in anchors]),(b,section)):
                row=[]
                for i,j in outline:
                    row.append(len(self.v));self.v.append(tuple(p+e*i*width/2+f*j*height/2))
                rows.append(row)
            tail,head=rows
            self.polygon(tail[::-1],mat,wood_axis=d,origin=centre)
            self.polygon(head,mat,wood_axis=d,origin=centre)
            for i,start_idx in enumerate(anchors):
                j=(i+1)%len(tail)
                stop=anchors[j] if j else anchors[0]+len(head)
                for k in range(start_idx,stop-1):
                    self.polygon([tail[i],head[k%10],head[(k+1)%10]],mat,wood_axis=d,origin=centre,smooth=True)
                self.polygon([tail[i],head[(stop-1)%10],head[stop%10],tail[j]],mat,wood_axis=d,origin=centre,smooth=True)
            return
        self.prism([tuple(p+e*i*width/2+f*j*height/2) for p in [a,b] for i,j in section],mat,d,bool(round_sides))
    def post(self,x,y,profile,n,mat):
        rows=[]
        for z,r in profile:
            row=[]
            for i in range(n):
                ang=2*math.pi*i/n+math.pi/n;row.append(len(self.v));self.v.append((x+r*math.cos(ang),y+r*math.sin(ang),z))
            rows.append(row)
        self.polygon(rows[0][::-1],mat,single_stone=True);self.polygon(rows[-1],mat,single_stone=True)
        for a,b in zip(rows,rows[1:]):
            for i in range(n):
                j=(i+1)%n;self.polygon([a[i],a[j],b[j],b[i]],mat,single_stone=True)
    def mesh(self,name,collection,mats):
        # Only the overhang spreads beyond the bearing line. Use the same
        # displacement for the tiles, hip caps, soffit, beams and rafter tails.
        vertices=[]
        for x,y,z in self.v:
            radius=max(abs(x),abs(y))
            scale=1+.255*max(0,radius-4.33)/radius if radius>4.33 and z>3.42 else 1.
            vertices.append((x*scale,y*scale,z))
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],self.f);mesh.update()
        for m in mats:mesh.materials.append(m)
        uv=mesh.uv_layers.new(name='TilingUV')
        for e in mesh.edges:
            if e.key in self.sharp:e.use_edge_sharp=True
        for p,idx,coords,smooth in zip(mesh.polygons,self.m,self.uv,self.smooth):
            p.material_index=idx
            p.use_smooth=smooth
            for i,coord in zip(p.loop_indices,coords):uv.data[i].uv=coord
        bm=bmesh.new();bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bm.to_mesh(mesh);bm.free();mesh.update()
        ob=bpy.data.objects.new(name,mesh);collection.objects.link(ob)
        return ob

def axis(side,t,r,z):
    return [(t,-r,z),(r,t,z),(-t,r,z),(-r,-t,z)][side]

def roof_z(r,t):
    slope=(5.468-r)/(5.468-1.40)
    end=max(0.,(abs(t)/r-.46)/.54)
    kick=.425*(1-slope)**1.48*end**2*(1.38-.38*end)
    return 3.935+1.535*(.53*slope+.47*slope*slope)+kick

def tiled_eave(g,side,level):
    """Open section of each fired cover tile, with the pan visible between lips."""
    pitch=.28
    def shell(t,radii,profile):
        rows=[]
        for r in radii:
            row=[]
            for dx,dz in profile:
                row.append(len(g.v))
                nose=r+(.023*(1-(dx/.109)**2) if r>5.4 else 0.)
                g.v.append(axis(side,t+dx,nose,roof_z(nose,t+dx)+dz))
            rows.append(row)
        g.polygon(rows[0][::-1],3)
        g.polygon(rows[-1],3)
        for a,b,r1,r2 in zip(rows,rows[1:],radii,radii[1:]):
            for i in range(len(profile)):
                j=(i+1)%len(profile)
                ids=[a[i],a[j],b[j],b[i]]
                # Use surface distance on the narrow return walls as well, so
                # the vertical lip has real, noncollapsed UVs.
                u1=(t+profile[i][0])/2.24
                u2=(t+profile[j][0])/2.24
                if abs(u2-u1)<.0001:u2=u1+abs(profile[j][1]-profile[i][1])/2.24
                g.polygon(ids,3,[(u1,r1/1.44),(u2,r1/1.44),(u2,r2/1.44),(u1,r2/1.44)],
                          smooth=(i<len(profile)//2-1))
    steps=[7,5,3,2][level]
    arc=[math.pi*i/steps for i in range(steps+1)]
    def barrel(t,stop):
        end=5.461+.009*math.sin(t*17.27+side)
        sections=[(end,1.)]
        for course in range(15,3,-1):
            joint=(course-.43)*.36
            if stop+.045<joint<5.412:
                if level<=1:sections.extend([(joint+.017,.80),(joint,1.)])
                elif course%4==0:sections.append((joint,.92))
        sections.append((stop,.9))
        def row(r,scale,inner=False):
            ids=[]
            for a in arc:
                dx=math.cos(a)*(.058 if inner else .084)*scale
                dz=math.sin(a)*(.065 if inner else .087)*scale+(-.025 if inner else -.010)
                ids.append(len(g.v))
                g.v.append(axis(side,t+dx,r,roof_z(r,t+dx)+dz))
            return ids
        def skin(a,b,r1,r2,smooth=True):
            for j in range(steps):
                ids=[a[j],a[j+1],b[j+1],b[j]]
                g.polygon(ids,3,[((g.v[k][side%2]*(1 if side<2 else -1))/2.24,r/1.44)
                                 for k,r in zip(ids,(r1,r1,r2,r2))],smooth=smooth)
        ridges=[row(r,s) for r,s in sections]
        for k,(a,b) in enumerate(zip(ridges,ridges[1:])):
            is_lap=sections[k][0]-sections[k+1][0]<.03
            if is_lap:
                for ring in [a,b]:
                    g.sharp.update(tuple(sorted(edge)) for edge in zip(ring,ring[1:]))
            skin(a,b,sections[k][0],sections[k+1][0])
        # The mouth has a real cavity. Behind it the hidden underside is a
        # single strip under each course rather than a second curved surface.
        lip=row(end,1.,True)
        back=row(5.398,1.,True)
        first=ridges[0]
        for j in range(steps):
            g.polygon([first[j],lip[j],lip[j+1],first[j+1]],3)
        skin(lip,back,end,5.398)
        g.polygon(back,3)
        a,b=ridges[:2]
        for j in (0,-1):g.polygon([a[j],lip[j],back[j],b[j]],3)
        g.polygon([back[0],back[-1],b[-1],b[0]],3)
        for a,b in zip(ridges[1:],ridges[2:]):g.polygon([a[0],a[-1],b[-1],b[0]],3)
        g.polygon(ridges[-1],3)
    for i in range(-19,20):
        t=i*pitch
        stop=max(1.452,abs(t)+.031)
        # The hip flashing covers the diagonal cut.
        barrel(t,stop)
    pan=[(-.109,.042),(-.063,.001),(0,-.013),(.063,.001),(.109,.042)]
    if level>=2:pan=[pan[0],pan[2],pan[-1]]
    pan=pan+[(t,z-.026) for t,z in reversed(pan)]
    for i in range(-19,19):
        shell((i+.5)*pitch,[5.475,5.29],pan)

def pillar(g,x,y):
    w=.174;c=.020
    outline=[(-w+c,-w),(w-c,-w),(w,-w+c),(w,w-c),(w-c,w),(-w+c,w),(-w,w-c),(-w,-w+c)]
    g.prism([(x+i,y+j,z) for z in (.267,3.451) for i,j in outline],2,(0,0,1))

def hip_ridge(g,side,radii,level):
    # Terminate inside the outer curb face, below the exposed top of its sill.
    radii=[r for r in radii if r>1.60]+[1.60]
    centres=[Vector(axis(side,-r,r,roof_z(r,r)+.061)) for r in radii]
    segments=8 if level<2 else 6
    rows=[]
    for i,c in enumerate(centres):
        d=(centres[min(i+1,len(centres)-1)]-centres[max(i-1,0)]).normalized()
        e=d.cross(Vector((0,0,1))).normalized(); f=d.cross(e)
        row=[]
        for j in range(segments):
            a=j*math.tau/segments
            row.append(len(g.v));g.v.append(tuple(c+e*(.129*math.cos(a))+f*(.097*math.sin(a))))
        rows.append(row)
    g.polygon(rows[0][::-1],3);g.polygon(rows[-1],3)
    for i,(a,b) in enumerate(zip(rows,rows[1:])):
        for j in range(segments):
            k=(j+1)%segments
            g.polygon([a[j],a[k],b[k],b[j]],3,[(j*.026,radii[i]/1.44),((j+1)*.026,radii[i]/1.44),
                                               ((j+1)*.026,radii[i+1]/1.44),(j*.026,radii[i+1]/1.44)])

def bearing(g,side,t,r,z,bottom,top,depth,height,radial=False):
    # Squared, splayed bearing cut from one timber, with a short plumb lip.
    profile=[(-bottom/2,0),(bottom/2,0),(top/2,height*.69),(top/2,height),
             (-top/2,height),(-top/2,height*.69)]
    point=lambda along,cross,h:axis(side,t+(cross if radial else along),r+(along if radial else cross),z+h)
    flow=Vector(point(1,0,0))-Vector(point(0,0,0))
    g.prism([point(a,b,h) for b in (-depth/2,depth/2) for a,h in profile],2,flow)

def timber_ring(g,rx,ry,width,height,base,samples=1,follow_roof=False):
    """One mitered, four-sided timber course, open at its centre."""
    rows=[]
    for lateral,z in ((-.5,0),(.5,0),(.5,height),(-.5,height)):
        row=[]
        for side in range(4):
            across,radial=(rx,ry) if side%2==0 else (ry,rx)
            r=radial+lateral*width
            for i in range(samples):
                t=(across+lateral*width)*(2*i/samples-1)
                bottom=roof_z(radial,t*radial/r)+base if follow_roof else base
                row.append(len(g.v));g.v.append(axis(side,t,r,bottom+z))
        rows.append(row)
    for a,b in zip(rows,rows[1:]+rows[:1]):
        for i in range(len(a)):
            side=i//samples;j=(i+1)%len(a)
            g.polygon([a[i],a[j],b[j],b[i]],2,wood_axis=Vector(axis(side,1,0,0)),
                      origin=axis(side,0,ry if side%2==0 else rx,base if not follow_roof else 4.))


def lantern(g,level):
    # Three unobstructed lights on the long face. The inside of every rail
    # stays outside the protected 2.64 m square through the shaft.
    timber_ring(g,1.535,1.49,.235,.262,5.295)
    timber_ring(g,1.58,1.50,.214,.155,5.89)
    for side in range(4):
        across,radial=(1.54,1.49) if side%2==0 else (1.49,1.54)
        positions=(-1.,-1/3,1/3) if level<3 else (-1.,)
        for fraction in positions:
            x,y,_=axis(side,across*fraction,radial,0)
            g.box((x,y,5.724),(.123,.123,.374),2)

def game_roof(g,level):
    """One closed roof ring. Only its exposed lip needs the fired tile profile."""
    # (offset across one 28 cm tile pair, top above the tile bed)
    profile=[(-.084,.018),(-.050,.059),(0,.077),(.050,.059),(.084,.018),(.115,-.012),(.165,-.012)]
    if level==2:profile=[(0,.067),(.14,-.011)]
    intervals=[0,10,8,4][level]
    def ring(radius,height,relief=0,coarse=False,inner_lip=False):
        samples=[(-radius,0.)]
        if relief:
            section=profile
            if coarse:section=[(0,.077),(.14,-.013)]
            elif inner_lip and level==1:
                section=[(-.069,.010),(-.043,.061),(0,.077),(.043,.061),(.069,.010),(.14,-.013)]
            for i in range(-20,21):
                for dx,dz in section:
                    t=i*.28+dx
                    if abs(t)<radius-.035:samples.append((t,dz))
        else:
            samples.extend((radius*(2*i/intervals-1),0.) for i in range(1,intervals))
        edges=[]
        for side in range(4):
            edge=[]
            for t,dz in samples:
                r=radius
                if radius>5.45 and abs(t)<radius:
                    # Pan noses project beyond the barrels.
                    r+=.026*(.5-.5*math.cos(t*math.tau/.28))
                edge.append(len(g.v))
                g.v.append(axis(side,t,r,roof_z(r,t)+height+relief*dz))
            edges.append(edge)
        return [edge+[edges[(side+1)%4][0]] for side,edge in enumerate(edges)]
    def join(outer,inner,material,edge=False,smooth=False,return_face=False):
        for side,(a,b) in enumerate(zip(outer,inner)):
            def coords(k):
                x,y,z=g.v[k]
                return [(x,-y,z),(y,x,z),(-x,y,z),(-y,-x,z)][side]
            i=j=0
            def face(ids):
                if material==3:
                    points=[coords(k) for k in ids]
                    if edge or return_face:
                        # Use the flat middle of the pan on the clay cut and
                        # underside. Their arches already exist in geometry.
                        centre=sum(t for t,r,z in points)/len(points)
                        u=(round(centre/.28)+.5)/8
                        uv=[(u+(t-centre)*.012,(z if edge else r)/1.44) for t,r,z in points]
                    else:uv=[(t/2.24,r/1.44) for t,r,z in points]
                    g.polygon(ids,material,uv,smooth=smooth)
                else:
                    g.polygon(ids,material,wood_axis=Vector(axis(side,1,0,0)),
                              origin=axis(side,0,4.5,4.0))
            # Stitch fine clay contours to coarse rows without adding matching
            # loops to the entire underside or the field of the roof.
            while i<len(a)-1 or j<len(b)-1:
                ta=coords(a[i+1])[0] if i+1<len(a) else math.inf
                tb=coords(b[j+1])[0] if j+1<len(b) else math.inf
                if abs(ta-tb)<1e-6:
                    face([a[i],a[i+1],b[j+1],b[j]]);i+=1;j+=1
                elif ta<tb:
                    face([a[i],a[i+1],b[j]]);i+=1
                else:
                    face([a[i],b[j+1],b[j]]);j+=1
    radii=([5.06,4.42,3.65,2.84,2.045,1.40] if level==1 else
           [5.06,4.16,2.83,1.40] if level==2 else [5.38,3.66,1.40])
    upper=[ring(r,0.,.20,coarse=True) if level==1 and i==0 else ring(r,0.) for i,r in enumerate(radii)]
    lower=[ring(r,-.146) for r in radii]
    for a,b in zip(upper,upper[1:]):join(a,b,3)
    for a,b in zip(lower,lower[1:]):join(a,b,2)
    join(upper[-1],lower[-1],2)
    if level==3:
        join(upper[0],lower[0],2)
        return radii
    lip=ring(5.468,0.,1.)
    rim=ring(5.468,-.029,1.,inner_lip=True)
    join(lip,upper[0],3,smooth=True)
    join(lip,rim,3,edge=True)
    # Show the ceramic return beneath the projecting mouths. Timber starts
    # behind them on the flat fascia rather than being drawn into every arch.
    tile_back=ring(5.375,-.036,.78 if level==1 else 0,coarse=True)
    fascia=ring(5.375,-.146)
    join(rim,tile_back,3,return_face=True)
    for side,(top,bottom) in enumerate(zip(tile_back,fascia)):
        # Triangulate the scalloped, planar fascia as a concave outline.
        # A fan from a distant corner would fold across the intervening teeth.
        g.polygon(top+bottom[::-1],2,wood_axis=Vector(axis(side,1,0,0)),origin=axis(side,0,5.375,3.8))
    join(fascia,lower[0],2)
    return ([5.455,5.06] if level==1 else [5.43])+radii[1:]

def build(level,collection,mats):
    g=Geometry()
    corners=[(-4.61,-4.61),(4.61,-4.61),(4.61,4.61),(-4.61,4.61)]
    g.v.extend((x,y,z) for z in (-.20,0) for x,y in corners)
    g.polygon((3,2,1,0),0);g.polygon((4,5,6,7),0)
    for i in range(4):
        j=(i+1)%4
        g.polygon((i,j,j+4,i+4),1)
    def bar(side,t,r,z,w,d,h,material=2):
        sx,sy=(w,d) if side%2==0 else (d,w)
        g.box(axis(side,t,r,z),(sx,sy,h),material)
    # Axial entrances face the two ends of the canal-to-cliff walk. Door and window
    # heads sit above the player. The centre is entirely free for the moving instrument.
    for side in range(4):
        door=(side%2==0)
        spans=[(-4.00,-1.69),(1.69,4.00)] if door else [(-4.00,-1.42),(-1.41,1.41),(1.42,4.00)]
        for a,b in spans:
            mid=(a+b)/2;w=b-a
            # Share all edges at the stone shoulder. UV boundaries stay local
            # to this wall even where the stone and earth meet on the same mesh.
            rows=[]
            profile=[(-.0025,.362),(.430,.273),(1.0625,.273)]
            if level<=1:profile.insert(1,(.399,.343))
            for z,depth in profile:
                row=[]
                left=max(a,-3.907);right=min(b,3.907)
                for t,sign in ((left,-1),(right,-1),(right,1),(left,1)):
                    row.append(len(g.v));g.v.append(axis(side,t,4.06+sign*depth/2,z))
                rows.append(row)
            g.polygon(rows[0][::-1],1);g.polygon(rows[-1],1,earth=True)
            for row,(lo,hi) in enumerate(zip(rows,rows[1:])):
                for i in range(4):
                    j=(i+1)%4
                    g.polygon([lo[i],lo[j],hi[j],hi[i]],1,earth=row==len(rows)-2)
            bar(side,mid,4.06,1.104,w+.028,.339,.094)
            # Full-height screens between sill and tie. The heavier centre
            # mullion divides each wing into two glazed-free framed panels.
            count=([11,9,5,3] if door else [7,5,3,1])[level]
            for k in range(count):
                t=a+(k+1)*w/(count+1)
                mullion=k==count//2
                bar(side,t,4.04,2.126,.088 if mullion else .047,.124 if mullion else .071,2.007)
            rails=([1.34,1.58,2.54,2.78,2.99] if door else [1.49,2.59,2.88]) if level<=1 else [1.45,2.78] if level==2 else [2.66]
            for z in rails:bar(side,mid,4.094,z,w,.054,.057)
        # Corner columns and door/window posts with octagonal stone shoes.
        positions=[-4.075,-1.59,1.59] if door else [-4.075,-1.415,1.415]
        for t in positions:
            x,y,_=axis(side,t,4.075,0)
            shoe=[(-.015,.27),(.09,.29),(.20,.245),(.29,.197)] if level==0 else [(-.015,.28),(.29,.197)]
            g.post(x,y,shoe,[18,8,6,4][level],1)
            if level<=1:pillar(g,x,y)
            else:g.box((x,y,1.859),(.348,.348,3.184),2)
            if level<=2:bearing(g,side,t,4.09,3.342,.382,.77,.462,.263)
            if level<=1:bearing(g,side,t,4.20,3.58,.61,1.10,.453,.250,radial=True)
            else:bar(side,t,4.15,3.64,.54,.70,.41)
            if level<2:
                for s in [-1,1]:
                    g.beam(axis(side,t+s*.112,4.095,3.07),axis(side,t+s*.74,4.095,3.735),.186,.204,2)
        if door:
            bar(side,0,4.08,3.092,3.14,.30,.134)
        # Rafter tails define a heavier tiled eave from below.
        count=[31,25,11,0][level]
        for k in range(count):
            t=-4.75+9.50*(k+.5)/count
            g.beam(axis(side,t*.86,4.02,roof_z(4.02,t*.86)-.187),
                   axis(side,t,5.396,roof_z(5.396,t)-.17),.144,.148,2,[12,10,5,4][level])
        # A diagonal outrigger from the corner post carries the rising hip.
        # The toe stays behind and below the tile mouth on the same warp.
        g.beam(axis(side,-4.06,4.06,3.73),
               axis(side,-5.25,5.25,roof_z(5.25,5.25)-.212),.212,.185,2)
    timber_ring(g,4.058,4.058,.23,.252,3.118)
    timber_ring(g,4.088,4.088,.32,.263,3.659)
    timber_ring(g,4.51,4.51,.209,.193,-.435,[8,5,4,2][level],follow_roof=True)
    lantern(g,level)
    # A closed ring of gently upturned roof, with one unobstructed central opening.
    if level:
        radii=game_roof(g,level)
    else:
        radii=[5.38,4.85,4.28,3.60,2.7,1.92,1.40]
        high_roof(g,32,radii)
    for side in range(4):
        if level==0:tiled_eave(g,side,level)
        # Hips, soffit beams and raised skylight curb. Quad rafters encircle the void.
        hip_ridge(g,side,radii,level)
        if level<=2:
            for t in [-1.42,1.42]:
                g.beam(axis(side,t,4.073,3.90),axis(side,t,1.48,5.25),.16,.17,2)
            for span in ([-2.82,0,2.82] if level<=1 else [0]):
                g.beam(axis(side,span,4.02,4.02),axis(side,span*.65,1.67,5.18),.095,.11,2)
    return g.mesh(('PavilionHi' if level==0 else 'Pavilion_LOD'+str(level-1)),collection,mats)

def high_roof(g,intervals,radii):
    rings=[]
    for lower in [False,True]:
        layers=[]
        for r in radii:
            row=[]
            for side in range(4):
                for i in range(intervals):
                    t=r*(-1+2*i/intervals);row.append(len(g.v));g.v.append(axis(side,t,r,roof_z(r,t)-(.146 if lower else 0)))
            layers.append(row)
        rings.append(layers)
    def roof_uv(vid,side):
        p=g.v[vid]; t=[p[0],p[1],-p[0],-p[1]][side];r=[-p[1],p[0],p[1],-p[0]][side]
        return (t/2.24,r/1.44)
    for n,layers in enumerate(rings):
        for a,b in zip(layers,layers[1:]):
            for i in range(len(a)):
                j=(i+1)%len(a);ids=[a[i],a[j],b[j],b[i]]
                if n:
                    g.polygon(ids,2,wood_axis=Vector(axis(i//intervals,1,0,0)))
                else:
                    g.polygon(ids,3,[roof_uv(vid,i//intervals) for vid in ids])
    for k in [0,-1]:
        a,b=rings[0][k],rings[1][k]
        for i in range(len(a)):
            j=(i+1)%len(a);side=i//intervals
            g.polygon([a[i],b[i],b[j],a[j]],2,wood_axis=Vector(axis(side,1,0,0)),origin=axis(side,0,radii[k],roof_z(radii[k],0)))

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=1;s.cycles.use_denoising=False
    s.render.threads_mode='FIXED';s.render.threads=8
    s.unit_settings.system='METRIC'
    stages={}
    for key in ['ArchitecturalHigh','GameLODs']:
        c=bpy.data.collections.new(key);s.collection.children.link(c);stages[key]=c
    if '--cached' not in sys.argv:
        subprocess.run(['python',str(HERE/'prepare_surfaces.py')],check=True)
    mats=[fresh_material(*entry) for entry in SURFACES]
    high=build(0,stages['ArchitecturalHigh'],mats);high.hide_render=True;high.hide_set(True)
    models=[build(k,stages['GameLODs'],mats) for k in [1,2,3]]
    select(models,models[0])
    output=HERE/'Review' if '--stage' in sys.argv else ASSET
    bpy.ops.export_scene.fbx(filepath=str(output/'TianyiPavilion.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    for o in models[1:]:o.hide_set(True);o.hide_render=True
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'TianyiPavilion.blend'),compress=True)
    counts={o.name:len(o.data.polygons) for o in [high]+models}
    (HERE/'mesh-counts.json').write_text(json.dumps(counts,indent=2))
    print('PAVILION_COUNTS',counts)

if __name__=='__main__':main()
