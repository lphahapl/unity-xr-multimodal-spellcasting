"""Blender 5.2 --background --factory-startup --python-exit-code 1 -P build.py
Writes actual Unity native Mesh buffers and the Blender source with the same UVs.
"""
import gzip, json, math, random, re, sys
from collections import defaultdict
from pathlib import Path
import bpy
from mathutils import Vector
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
ART = ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD'
BANDS = dict(roof=(0,720), wood=(720,1120), stone=(1120,1480), plaster=(1480,1616), nose=(1616,1712),
             clay=(1712,1832), end=(1832,1952), bronze=(1952,1992), rule=(1992,2020), ink=(2020,2048))
SPANS = dict(wood=1.60,stone=1.5,plaster=1.35,clay=.80,end=.50,bronze=.50,rule=.50,ink=.50)
V = Vector
def band(key,u,v):
    lo,hi=BANDS[key]
    if key=='clay':u/=4  # Twenty-four cover tiles per atlas width; preserve the physical lap.
    return u,(lo+4+(hi-lo-8)*v)/2048

class Kit:
    def __init__(self,name,scale,turned):
        self.name,self.scale,self.turned=name,scale,turned
        self.smooth=set(); self.verts=[]; self.faces=[]; self.uv=[]; self.lookup={}
    def face(self,points,key='wood',uv=None,grain=None,up=None,offset=.0):
        p=[V(q) for q in points]
        n=(p[1]-p[0]).cross(p[-1]-p[0]).normalized()
        if n.z<-.9999 and max(abs(q.z) for q in p)<.0001:
            return  # Hidden undersides of timbers and daub seated directly on the floor.
        if uv is None:
            axis=V(grain) if grain is not None else max((p[(i+1)%len(p)]-a for i,a in enumerate(p)),key=lambda d:d.length)
            axis=axis-n*axis.dot(n)
            if axis.length<.00001:
                axis=n.orthogonal()
            axis.normalize()
            perp=n.cross(axis).normalized()
            if up is not None and perp.dot(V(up))<0:perp.negate()
            us=[q.dot(axis)*self.scale for q in p]
            vs=[q.dot(perp)*self.scale for q in p]
            span=max(SPANS[key],max(vs)-min(vs)+.001)
            start=(span-max(vs)+min(vs))*(.2+offset% .6)
            uv=[band(key,u/2.56+offset,(v-min(vs)+start)/span) for u,v in zip(us,vs)]
        idx=[]
        for point in p:
            x,y,z=point
            if self.turned:x,y=-y,x
            pos=tuple(round(float(a),6) for a in (x,y,z))
            if pos not in self.lookup:
                self.lookup[pos]=len(self.verts); self.verts.append(pos)
            idx.append(self.lookup[pos])
        if len(idx)>4:
            for j in range(1,len(idx)-1):
                self.faces.append([idx[0],idx[j],idx[j+1]])
                self.uv.append([uv[0],uv[j],uv[j+1]])
        else:
            self.faces.append(idx);self.uv.append(uv)
    def member(self,a,b,width,height,key='wood',rounds=4,capkey=None,skip_back=False,tail=False,bedded=False):
        a,b=V(a),V(b)
        axis=(b-a).normalized()
        cross=axis.cross(V((0,0,1)))
        if cross.length<.01:cross=V((1,0,0))
        cross.normalize();up=axis.cross(cross).normalized()
        if rounds==4:
            profile=[(-1,-1),(1,-1),(1,1),(-1,1)]
        else:
            profile=[(math.cos(i*math.tau/rounds),math.sin(i*math.tau/rounds)) for i in range(rounds)]
        rings=[[c+cross*s*width*.5+up*t*height*.5 for s,t in profile] for c in (a,b)]
        # End-grain is centered on each sawn face.
        for j in ([] if bedded else [1] if skip_back else [0,1]):
            indices=list(range(rounds)) if j else list(reversed(range(rounds)))
            cap=capkey or ('end' if key=='wood' else key)
            if cap=='plaster':
                self.face([rings[j][i] for i in indices],cap,grain=(0,1,0),up=(0,0,1),offset=a.x*.17)
                continue
            self.face([rings[j][i] for i in indices],cap,[band(cap,.51+profile[i][0]*.073,.50+profile[i][1]*.44) for i in indices])
        for i in range(rounds):
            # Half of a projecting rafter is bedded in the underside of the deck.
            if tail and up.z*(profile[i][1]+profile[(i+1)%rounds][1])>.15:continue
            k=(i+1)%rounds
            self.face([rings[0][i],rings[0][k],rings[1][k],rings[1][i]],key,grain=axis,offset=a.x*.27+a.y*.13)
    def box(self,x,y,z,w,d,h,key,bedded=False):
        if key in ('plaster','stone') or w>=max(d,h):
            self.member((x-w/2,y,z),(x+w/2,y,z),d,h,key,bedded=bedded)
        elif d>=h:
            self.member((x,y-d/2,z),(x,y+d/2,z),w,h,key,bedded=bedded)
        else:self.member((x,y,z-h/2),(x,y,z+h/2),w,d,key,bedded=bedded)
    def blender(self,material):
        m=bpy.data.meshes.new(self.name);m.from_pydata(self.verts,[],self.faces);m.materials.append(material)
        for i in self.smooth:m.polygons[i].use_smooth=True
        layer=m.uv_layers.new(name='ArchitectureMetric')
        for p,uv in zip(m.polygons,self.uv):
            for loop,coords in zip(p.loop_indices,uv):layer.data[loop].uv=coords
        m.update();assert not m.validate(verbose=False)
        o=bpy.data.objects.new(self.name,m);bpy.context.scene.collection.objects.link(o)
        return o

def west_side_daub(m,x,eave):
    # Two skins of a 188 mm thick panel. The rear bay continues up to the wall plate;
    # the front and centre windows retain daylight under a narrow transom panel.
    back=2.03; mullion=.96; front=-2.02
    head=eave-.19; transom=eave-.475; sill=1.10
    def face(p):
        m.face(p,'plaster',grain=(0,1,0),up=(0,0,1),offset=.27*x)
    for sign in (-1,1):
        xx=x+.094*sign
        for y0,y1,z0,z1 in [(front,mullion,0,sill),(mullion,back,0,head),(front,mullion,transom,head)]:
            pts=[(xx,y0,z0),(xx,y1,z0),(xx,y1,z1),(xx,y0,z1)]
            face(pts if sign>0 else pts[::-1])
    # Exposed window reveal and soffit. All other edges meet the horizontal rails,
    # corner posts, footing or rear wall.
    face([(x-.094,mullion,sill),(x+.094,mullion,sill),(x+.094,mullion,transom),(x-.094,mullion,transom)])
    face([(x-.094,front,transom),(x-.094,mullion,transom),(x+.094,mullion,transom),(x+.094,front,transom)])

def hall_body(m,w,eave,lod):
    # Front is -Y. The original local 2.10 m opening, landing and level are retained.
    m.box(0,-.30,-.096,w+.74,5.44,.192,'stone')
    m.box(0,2.02,1.26,w-.28,.20,2.52,'plaster')
    for side in (-1,1):
        x=side*(w/2-.11)
        if m.name.startswith('CalibrationHall_0_'):
            west_side_daub(m,x,eave)
        else:
            m.box(x,0,.55,.20,4.02,1.10,'plaster')
        m.box(x,0,.17,.23,4.035,.34,'stone')
        m.box(x,0,1.115,.24,4.04,.12,'wood')
        for y in (-2.1,2.1):
            m.box(x,y,.13,.43,.43,.26,'stone')
            m.box(x,y,(.24+eave)/2,.255,.255,eave-.26,'wood',bedded=True)
        m.box(x,0,eave-.13,.265,4.50,.26,'wood')
        m.box(side*(w*.25+.57),-2.075,.56,w*.5-1.18,.21,1.12,'plaster')
        m.box(side*(w*.25+.57),-2.075,.165,w*.5-1.17,.23,.33,'stone')
        m.box(side*(w*.25+.57),-2.075,1.135,w*.5-1.17,.24,.11,'wood')
        m.box(side*1.20,-2.1,(eave-.04)/2,.27,.30,eave-.04,'wood',bedded=True)
        if lod==0:
            # Two timber mullions and a transom frame the open measuring bays.
            for f in (.31,.68):
                xbay=side*(1.32+f*(w/2-1.58))
                m.box(xbay,-2.084,(1.18+eave-.24)/2,.065,.078,eave-1.4,'wood',bedded=True)
            m.box(side*(1.32+(w/2-1.58)/2),-2.114,eave-.56,w/2-1.55,.065,.072,'wood',bedded=True)
        if lod<2:
            m.box(side*1.2,-2.26,1.65,.07,.025,1.6,'rule')
            for y in (-.97,.96):
                m.box(x,y,(1.15+eave)/2,.11,.10,eave-1.14,'wood',bedded=True)
            m.box(x,0,eave-.49,.12,4.04,.08,'wood',bedded=True)
            if lod==0:
                m.member((x,-2.09,eave-.61),(x-side*.60,-2.09,eave-.17),.14,.15)
                # Short false tenon visible where the lintel meets the post.
                m.box(x,-2.248,eave-.21,.115,.075,.095,'end')
    if lod<2:
        # Original wall gauge, ruler stock and billet pile. The independent A07 table
        # is still instantiated by the prefab at local (0,0,.5).
        x=w/2-.70
        m.member((x,-1.22,.003),(x,-1.22,1.43),.11,.14,skip_back=True)
        m.member((x,-1.27,1.445),(x,-.35,1.445),.13,.12,'bronze')
        m.member((x,-.41,.85),(x,-.41,1.42),.06,.06,'rule',skip_back=True)
    if lod==0:
        # Only the top and exposed faces of eight touching billets need triangles.
        for row in range(2):
            for j in range(4):
                x=-w/2+.55+row*.06
                y=.75-j*.167
                z=-.002+row*.151
                a,b=x-.4,x+.4
                lo,hi=y-.083,y+.083
                top=z+.15
                for xx,flip in ([(b,False)] if row==1 else [(a,True),(b,False)]):
                    pts=[(xx,lo,z),(xx,hi,z),(xx,hi,top),(xx,lo,top)]
                    if flip:pts.reverse()
                    m.face(pts,'end')
                if row==1:m.face([(a,lo,top),(b,lo,top),(b,hi,top),(a,hi,top)],'wood',grain=(1,0,0))
                for face_y,visible in [(lo,j==3),(hi,j==0)]:
                    if visible:
                        pts=[(a,face_y,z),(b,face_y,z),(b,face_y,top),(a,face_y,top)]
                        if face_y==hi:pts.reverse()
                        m.face(pts,'wood',grain=(1,0,0))
        for j in range(5):
            x=w/2-.65+j*.09
            m.member((x,1.17,-.002),(x,1.17,1.72),.041,.045,'rule',rounds=3,skip_back=True)
    for y in (-2.10,2.1):
        m.box(0,y,eave-.13,w+.19,.285,.27,'wood')

def roof(m,half,depth,base,rise,lod,style):
    # Straight gable, flared gable ends, or clipped hip, on the original eave footprint.
    scale=m.scale
    lanes=max(6,round(2*half*scale/.34))
    step=2*half/lanes
    hip=(0,.12,.65)[style]
    length=math.hypot(depth,rise)*scale
    def roof_z(t):
        return base+rise*t
    def point(x,t,side):
        return (x*(1-hip*t/half),side*depth*(1-t),roof_z(t))
    # Course texture repeats at 0.315 m along the pitch; split at each eight-course boundary.
    for side in (-1,1):
        def sheet(points,coords):
            if side<0:points,coords=points[::-1],coords[::-1]
            m.face(points,'roof',coords)
        tile_column=(style*13 + (0 if side<0 else 21) + 7*int(m.turned))%48
        variation=random.Random(9473+style*302+side*51+int(m.turned)*121)
        limits=sorted(set([0.,.45,1.]+[i*2.52/length for i in range(1,math.ceil(length/2.52))]))
        for t0,t1 in zip(limits,limits[1:]):
            cycle=math.floor((t0+t1)*.5*length/5.04)
            def tex(x,t):
                return band('roof',((x+half)/step+tile_column)/48,t*length/5.04-cycle)
            p=[point(half,t0,side),point(-half,t0,side),point(-half,t1,side),point(half,t1,side)]
            sheet(p,[tex(half,t0),tex(-half,t0),tex(-half,t1),tex(half,t1)])
        if lod==0:
            for lane in range(lanes):
                # Tapered barrel runs extend from ridge to eave, over the pan surface.
                # Each run has continuous convex shading and a flat stamped ceramic nose.
                x=-half+(lane+.5)*step
                rad=step*.205
                segments=2 if m.turned else 3
                outer=[];inner=[]
                for j in range(segments+1):
                    theta=math.pi*j/segments
                    dx=-rad*math.cos(theta)
                    height=rad*.80*math.sin(theta)
                    outer.append((x+dx,side*(depth+.057/scale),base-.014/scale+height))
                    inner.append(((x+dx)*(1-hip/half),side*.055,roof_z(1-.055/depth)-.016/scale+height))
                # Shift by whole tiles, then introduce a small joint offset. Neighbouring
                # barrel runs draw different kiln batches without changing the tile pitch.
                phase=variation.randrange(24)/6+variation.uniform(-.009,.009)
                patina=(variation.randrange(8)+.23)/8
                for j in range(segments):
                    p=[outer[j+1],outer[j],inner[j],inner[j+1]]
                    v0=patina+.057*j/segments;v1=patina+.057*(j+1)/segments
                    uv=[band('clay',u+phase,v) for u,v in [(0,v1),(0,v0),(length/1.89,v0),(length/1.89,v1)]]
                    if side<0:p.reverse();uv.reverse()
                    m.smooth.add(len(m.faces))
                    m.face(p,'clay',uv)
                rim=outer if side>0 else list(reversed(outer))
                m.face(rim,'nose',[band('nose',(lane%16+.5+.46*(q[0]-x)/rad)/16,.10+.80*(q[2]-base+.014/scale)/(rad*.80)) for q in rim])
        # Front edge of the curved pan tiles and their bedding. Surface connects the
        # cover ends; laid separately from the longitudinal grain of the eave plate.
        p=[(-half,side*depth,base),(half,side*depth,base),(half,side*(depth+.006),base-.048/scale),(-half,side*(depth+.006),base-.048/scale)]
        if side<0:p.reverse()
        m.face(p,'clay',grain=(1,0,0))
        # Downward-facing boarded deck; backface culling stays enabled.
        rim=depth-.145
        profile=[(-half+.064,side*rim,base-.095),(half-.064,side*rim,base-.095),(half-hip-.064,0,base+rise-.11),(-half+hip+.064,0,base+rise-.11)]
        if side<0:profile.reverse()
        m.face(profile,'wood',grain=(1,0,0))
        m.member((-half+.02,side*(depth-.16),base-.105),(half-.02,side*(depth-.16),base-.105),.17,.18)
        n=max(2,round(2*half*scale/(.96 if not m.turned else 1.52))) if lod==0 else max(2,int(half/1.2)) if lod==1 else 0
        for i in range(n):
            x=(i+.5)*(2*half/n)-half
            # Timber tails bear on the eave plate and daylight exposes their end grain.
            m.member((x,side*(depth-.56),base-.11+.50*rise/depth),(x,side*(depth-.015),base-.12),.115/scale,.15/scale,'wood',rounds=6 if lod==0 and not m.turned else 4,skip_back=True,tail=True)
    for s in (-1,1):
        x=s*half;top=s*(half-hip)
        g=[(x,-depth,base-.07),(x,depth,base-.07),(top,0,base+rise)]
        if s<0:g.reverse()
        m.face(g,'plaster',grain=(0,1,0),up=(0,0,1))
        for side in (-1,1):
            m.member((x,side*depth,base-.06),(top,0,base+rise-.025),.125,.14)
        m.member((x,-depth+.033,base-.11),(x,depth-.033,base-.11),.165,.18)
        if lod<2:
            m.member((x,0,base-.13),(top,0,base+rise-.04),.14,.14,bedded=True)
    # Continuous ceramic saddle with course collars encoded in the clay trim.
    m.member((-half+hip-.10,0,base+rise+.035),(half-hip+.10,0,base+rise+.035),.295,.245,'clay',rounds=8 if lod==0 else 6 if lod==1 else 4)
    if lod<2:
        for s in (-1,1):
            x=s*(half-hip+.09)
            m.member((x-s*.14,0,base+rise+.055),(x+s*.06,0,base+rise+.073),.32,.29,'clay',rounds=6 if lod==0 else 5,skip_back=True)
    return lanes

def save_native(m,ob,split):
    data=ob.data;data.calc_loop_triangles();data.calc_tangents(uvmap=data.uv_layers.active.name)
    out=[];lookup={};indices=[];src_count=0;src_tris=0
    for tri in data.loop_triangles:
        for li in tri.loops:
            loop=data.loops[li];p=data.vertices[loop.vertex_index].co
            n=data.corner_normals[li].vector;t=loop.tangent;u,v=data.uv_layers.active.data[li].uv
            # Blender (x,y,z) -> Unity (x,z,-y) is a proper rotation.
            row=(p.x,p.z,-p.y,n.x,n.z,-n.y,t.x,t.z,-t.y,loop.bitangent_sign,u,v)
            if row not in lookup:lookup[row]=len(out);out.append(row)
            indices.append(lookup[row])
        if tri.polygon_index<split:src_count=len(out);src_tris=len(indices)
    buf=np.asarray(out,dtype='<f4');idx=np.asarray(indices,dtype='<u2')
    assert len(buf)<65535 and np.isfinite(buf).all() and idx.max()<len(buf)
    asset=ART/'Meshes'/(m.name+'.asset')
    s=asset.read_text()
    assert '--- !u!43 &4300000' in s and 'm_Name: '+m.name in s
    def replace(key,value):
        nonlocal s
        s,n=re.subn(r'(?m)^(\s*'+re.escape(key)+r': )[^\n]*',lambda k:k[1]+str(value),s)
        assert n, key
    for k,val in [('indexCount',len(idx)),('vertexCount',len(buf)),('m_VertexCount',len(buf)),('m_IndexBuffer',idx.tobytes().hex()),('m_DataSize',buf.nbytes),('_typelessdata',buf.tobytes().hex())]:replace(k,val)
    pos=buf[:,:3];lo=pos.min(axis=0);hi=pos.max(axis=0)
    for k,vec in [('m_Center',(lo+hi)/2),('m_Extent',(hi-lo)/2)]:replace(k,'{x: %.8g, y: %.8g, z: %.8g}'%tuple(vec))
    asset.write_text(s,encoding='utf8')
    srcname=m.name.replace('_Grounded','').replace('_Support','')
    parts=buf[:src_count]
    content=dict(name=srcname,vertices=parts[:,:3].ravel().tolist(),uv=parts[:,10:12].ravel().tolist(),triangles=idx[:src_tris].tolist())
    (ART/(srcname+'.json')).write_text(json.dumps(content,separators=(',',':')),encoding='utf8')
    return dict(triangles=len(idx)//3,vertices=len(buf),size=(hi-lo).tolist(),fileID=4300000)

def support(m,src):
    pos=[(x,-z,y) if not m.turned else (-z,-x,y) for x,y,z in src['pos']]
    if m.turned:
        lod=int(m.name[-1])
        for sx in (-1,1):
            for sy in (-1,1):
                pts=np.array([p for p in pos if sx*p[0]>0 and sy*p[1]>0])
                x,y=(pts.min(axis=0)[:2]+pts.max(axis=0)[:2])/2
                z=pts[:,2].min()
                m.box(x,y,z+.09,.65,.65,.18,'stone')
                m.member((x,y,z+.16),(x,y,z+.34),.55,.55,'stone',rounds=6 if lod==0 else 4,skip_back=True)
                m.box(x,y,(z+.31-.105)/2,.395,.395,-.105-z-.31,'wood',bedded=True)
                m.box(x,y,-.102,.695,.695,.22,'wood')
                if lod<2:m.box(x,y,-.277,.418,.418,.079,'bronze')
        return
    if int(m.name[-1])<2:
        # Close the narrow joints between the four individually terrain-fit footing
        # blocks. The backing is recessed from the original chamfered stone noses.
        foot=[q for q in pos if q[2]<0]
        for axis in (0,1):
            other=1-axis
            for sign in (-1,1):
                edge=(max if sign>0 else min)(q[axis] for q in foot)
                center=-.30 if axis==0 else 0.
                selected=[q[2] for q in foot if abs(q[axis]-edge)<.06 and abs(q[other]-center)<.06]
                low=min(selected)
                def joint(s,z):
                    a=edge-sign*.025
                    return (a,center+s,z) if axis==0 else (center+s,a,z)
                patch=[joint(-.062,low),joint(.062,low),joint(.062,-.181),joint(-.062,-.181)]
                if (axis==0 and sign<0) or (axis==1 and sign>0):patch.reverse()
                m.face(patch,'stone')
    # Keep the exact terrain-contact positions and every entrance division.
    for tri in src['tri']:
        points=[pos[i] for i in tri]
        region=src['reg'][tri[0]]
        key=['stone','wood','roof','bronze','rule','ink'][region]
        # Use a consistent world-space frame on both triangles of each support face.
        normal=(V(points[1])-V(points[0])).cross(V(points[2])-V(points[0])).normalized()
        if normal.z<-.995 or (not m.turned and max(p[2] for p in points)<0 and normal.z>.98):
            continue  # Buried bottoms and tops inside the landing slab.
        if not m.turned and max(p[2] for p in points)<0:
            if all(abs(q[0])<.052 for q in points) or all(abs(q[1]+.3)<.052 for q in points):continue
        if abs(normal.z)>.75:u,v=V((1,0,0)),V((0,1,0))
        else:u,v=V((0,0,1)).cross(normal).normalized(),V((0,0,1))
        if key=='wood':u,v=v,u
        coords=[band(key,V(p).dot(u)*m.scale/2.56,.5+V(p).dot(v)*m.scale/(16 if key=='stone' and m.turned else 8)) for p in points]
        m.face(points,key,coords)

def material():
    mat=bpy.data.materials.new('Architecture_Atlas');mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    for role in ('BaseColor','Normal','Mask'):
        t=nodes.new('ShaderNodeTexImage')
        t.image=bpy.data.images.load(str(ART/'Textures'/('Architecture_'+role+'.png')))
        if role!='BaseColor':t.image.colorspace_settings.name='Non-Color'
        t.image.alpha_mode='CHANNEL_PACKED'
        if role=='BaseColor':links.new(t.outputs['Color'],bs.inputs['Base Color'])
        if role=='Normal':
            n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.6
            links.new(t.outputs['Color'],n.inputs['Color']);links.new(n.outputs[0],bs.inputs['Normal'])
        if role=='Mask':
            n=nodes.new('ShaderNodeSeparateColor');links.new(t.outputs[0],n.inputs[0]);links.new(n.outputs[0],bs.inputs['Metallic'])
            inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
            links.new(t.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bs.inputs['Roughness'])
    return mat

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0
    mat=material()
    with gzip.open(HERE/'fitted_supports.json.gz','rt') as f:supports=json.load(f)
    result={}
    for name,src in sorted(supports.items()):
        lod=int(name[-1]);turned=name.startswith('Roof')
        if turned:
            half,depth,base,rise,style=dict(Wake=(7.175,7.08,0,1.45,0),Corridor=(4.5,3.78,0,1.20,1),Trial=(8.8,7.93,0,1.45,2))[name.split('_')[1]]
            scale=1
        else:
            style=int(name.split('_')[1]);w=[6.,6.8,7.3][style];base=[2.85,2.7,2.95][style];rise=[1,.90,1.02][style]
            half=w/2+.5;depth=2.58;scale=[1.25,1.35,1.60][style]
        m=Kit(name,scale,turned)
        if not turned:hall_body(m,w,base,lod)
        lanes=roof(m,half,depth,base,rise,lod,style)
        split=len(m.faces);support(m,src)
        ob=m.blender(mat)
        result[name]=save_native(m,ob,split)
        result[name].update(world_lane_pitch=2*half*scale/lanes,levels='prefab LODGroup')
        ob.hide_render=ob.name!='CalibrationHall_0_Grounded_LOD0';ob.hide_set(ob.hide_render)
    for pre in set(n[:-1] for n in result):
        a,b,c=[result[pre+str(i)]['triangles'] for i in range(3)]
        assert a>b>c,(pre,a,b,c)
    assert sum(r['triangles'] for n,r in result.items() if n.endswith('0'))+10200<18000
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'PreqinArchitecture.blend'),relative_remap=True)
    (HERE/'mesh-report.json').write_text(json.dumps(result,indent=2),encoding='utf8')
    print('NEAR',sum(r['triangles'] for n,r in result.items() if n.endswith('0')),flush=True)
if __name__=='__main__':main()
