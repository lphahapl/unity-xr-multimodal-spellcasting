"""Author in metres, then encode inverse Cube scaling in Unity mesh positions and tangent basis.
Blender 5.2: blender -b --factory-startup --python-exit-code 1 -P build.py
"""
import bpy,math,json,re,sys,hashlib
from pathlib import Path
import numpy as np
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from paths import *
V=Vector
# Metric order matches Unity local axes. Blender preview uses (x,-z,y).
SPEC={
 'InnerGateStrap':((6.18,.09,.045),'bronze',3),
 'OuterGateStrap':((4.24,.09,.045),'bronze',3),
 'CheckGateStrap':((1.98,.08,.03),'bronze',3),
 'CheckGateLeaf':((2.02,3.08,.38),'timber',3),
 'GaugeFrameRail':((.41344,.033439998,.033439998),'cue',1),
 'GaugeFrameStile':((.033439998,.38,.033439998),'cue',1),
 'GaugePost':((.026751999,.58,.026751999),'bronze',1),
 'WayfindingInlay':((.115,.018,9.85),'limestone',3),
 'HighImpactScar':((.62,.055,.26),'scar',1),
 'SideGateDais':((5.8,.64,5.8),'stone',3)
}
MATERIALS={'bronze':ART/'GateHammeredBronze.mat','timber':PHOTO/'Timber.mat','cue':ROOT/'Assets/Materials/Chapter01/WarmSurveyMark.mat','scar':ROOT/'Assets/Materials/Chapter01/石灰墙.mat','stone':PHOTO/'Stone.mat','limestone':ART/'InlayLimestone.mat'}
PROBE=ART.parent/'ArchitectureLOD/Meshes/Roof_Corridor_Support_LOD2.asset'
TEMPLATE=PROBE.read_text()
PERIOD=dict(bronze=.48,timber=1.26,stone=1.10,limestone=.64,cue=.2,scar=.45)

def rect(w,h,cut):
    a,b=w/2,h/2;c=min(cut,a*.85,b*.85)
    if not c:return [(-a,-b),(a,-b),(a,b),(-a,b)]
    return [(-a+c,-b),(a-c,-b),(a,-b+c),(a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c)]
def ellipse(a,b,n):
    return [(a*math.cos(i*math.tau/n), b*math.sin(i*math.tau/n)) for i in range(n)]
def xyz(u,v,t,axis):
    return (u,v,t) if axis==2 else (t,u,v) if axis==0 else (v,t,u)

class Model:
    def __init__(self,name,role):
        self.name=name;self.role=role;self.p=[];self.f=[];self.uv=[];self.smooth=[]
    def face(self,indices,grain,phase,soft=False):
        p=[V(self.p[i]) for i in indices]
        n=(p[1]-p[0]).cross(p[-1]-p[0]).normalized()
        if n.length<.98: # e.g. a convex polygon with a straight run on its perimeter
            n=sum(((a-p[0]).cross(b-p[0]) for a,b in zip(p[1:-1],p[2:])),V()).normalized()
        tangent=V(grain)-n*V(grain).dot(n)
        if tangent.length<.07:
            tangent=V((0,1,0)) if abs(n.y)<.85 else V((1,0,0))
            tangent-=n*tangent.dot(n)
        tangent.normalize();across=n.cross(tangent).normalized()
        period=PERIOD[self.role]
        if self.role=='timber':u,v=-across,tangent
        else:u,v=tangent,across
        coords=[(q.dot(u)/period+phase,q.dot(v)/period+phase*.37) for q in p]
        tris=tessellate_polygon([p]) if len(indices)>4 else [(0,j,j+1) for j in range(1,len(indices)-1)]
        for tri in tris:
            self.f.append([indices[i] for i in tri])
            self.uv.append([coords[i] for i in tri]);self.smooth.append(soft)
    def loft(self,rings,axis,center=(0,0,0),grain=(1,0,0),soft=False,phase=.0,buried=False):
        loops=[]
        for t,profile in rings:
            pts=[tuple(V(center)+V(xyz(u,v,t,axis))) for u,v in profile]
            start=len(self.p);self.p.extend(pts);loops.append(list(range(start,start+len(pts))))
        if not buried:self.face(loops[0][::-1],grain,phase)
        for a,b in zip(loops,loops[1:]):
            for i in range(len(a)):
                j=(i+1)%len(a)
                self.face([a[i],a[j],b[j],b[i]],grain,phase,soft)
        self.face(loops[-1],grain,phase)
    def slab(self,x,y,bottom,top,w,d,cut,edge,grain=(1,0,0),phase=0,buried=False):
        h=top-bottom;e=min(edge,h*.28)
        rings=[(bottom,rect(w-2*e,d-2*e,cut)),(bottom+e,rect(w,d,cut)),(top-e,rect(w,d,cut)),(top,rect(w-2*e,d-2*e,cut))] if e else [(bottom,rect(w,d,cut)),(top,rect(w,d,cut))]
        if buried and e:
            rings=[(bottom,rect(w,d,cut)),(top-e,rect(w,d,cut)),(top,rect(w-2*e,d-2*e,cut))]
        self.loft(rings,2,(x,y,0),grain,phase=phase,buried=buried)
    def object(self,mat):
        mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.p,[],self.f);mesh.materials.append(mat)
        uv=mesh.uv_layers.new(name='MetricUV')
        for poly,coords,soft in zip(mesh.polygons,self.uv,self.smooth):
            for j,xy in zip(poly.loop_indices,coords):uv.data[j].uv=xy
            poly.use_smooth=soft
        mesh.update();assert not mesh.validate(verbose=False)
        ob=bpy.data.objects.new(self.name,mesh);bpy.context.scene.collection.objects.link(ob)
        return ob

def strap(m,w,h,d,lod):
    top=d/2
    plate=top-.009
    # Rear perimeter is clipped at the corners. The raised face thins toward both long edges.
    def shape(inset):
        return rect(h-2*inset,w-2*inset,.019)
    m.loft([(-top,shape(.004)),(-top+.004,shape(0)),(plate-.003,shape(.006)),(plate,shape(.011))] if lod==0 else [(-top,shape(.005)),(plate-.004,shape(0)),(plate,shape(.009))],1)
    count=max(4,round((w-.15)/(.40 if lod==0 else .62 if lod==1 else 1.05))+1)
    n=(8,6,4)[lod]
    for i in range(count):
        x=-w/2+.068+(w-.136)*i/(count-1);r=.014 if h>.085 else .012
        # Raised boss with a coined shoulder and a shallow domed crown. The underside
        # is bedded in the strap, and the crown shares a single tip instead of a hidden cap.
        base=len(m.p)
        for y,rad in ([(plate-.0015,r),(top-.0024,r*.68)] if lod==0 else [(plate-.0015,r)]):
            for u,v in ellipse(rad,rad,n):m.p.append((x+v,y,u))
        tip=len(m.p);m.p.append((x,top,0))
        for j in range(n):
            k=(j+1)%n
            if lod==0:m.face([base+j,base+k,base+n+k,base+n+j],(1,0,0),i*.223,True)
            offset=base+n if lod==0 else base
            m.face([offset+j,offset+k,tip],(1,0,0),i*.223,True)

def leaf(m,w,h,d,lod):
    # Five joints coincide with the existing separate VerificationDoorBoards overlay.
    cuts=[-.5,-.3515,-.193,0,.193,.3515,.5]
    for i,(a,b) in enumerate(zip(cuts,cuts[1:])):
        x0=a*w+(.0016 if i else 0);x1=b*w-(.0016 if i!=5 else 0)
        # Retain the outer plane at the two outside stiles. Sub-mm offsets break unbroken specular.
        front=d/2-(i%3)*.00045;back=-d/2+.077
        m.slab((x0+x1)/2,(front+back)/2,-h/2,h/2,x1-x0,front-back,.0035 if lod<2 else 0,.0025 if lod==0 else 0,(0,0,1),phase=i*.317)
    for z in (-1.08,1.00):
        m.slab(0,-d/2+.042,z-.087,z+.087,w-.065,.084,.015 if lod<2 else 0,.005 if lod==0 else 0,phase=z*.47)

def frame(m,w,h,d,vertical):
    half=(h if vertical else w)/2
    def section(k):return rect(d*k,d*k,.0045*k)
    grain=(0,0,1) if vertical else (1,0,0)
    if vertical:
        # Reduced tenons fill the horizontal rails without a coplanar front face at each joint.
        rings=[(-half,section(.68)),(-half+.0165,section(.68)),(-half+.020,section(1)),(half-.020,section(1)),(half-.0165,section(.68)),(half,section(.68))]
    else:rings=[(-half,section(.86)),(-half+.0025,section(1)),(half-.0025,section(1)),(half,section(.86))]
    m.loft(rings,2 if vertical else 0,grain=grain)

def pole(m,w,h,d):
    p=[(-h/2,.82),(-h/2+.007,1),(-h/2+.025,1),(-h/2+.029,.75),(h/2-.051,.72),(h/2-.047,1),(h/2-.023,1),(h/2-.018,.82),(h/2,.82)]
    m.loft([(z,ellipse(w*.5*s,d*.5*s,8)) for z,s in p],2,grain=(0,0,1))

def inlay(m,w,h,d,lod):
    m.slab(0,0,-h/2,-.001,w-.006,d-.006,.002,0,(0,1,0))
    n=(16,12,7)[lod];weights=[1+.15*math.sin(i*2.47) for i in range(n)]
    y=-d/2
    for i,weight in enumerate(weights):
        far=y+d*weight/sum(weights)
        a=y+(.0018 if i else 0);b=far-(.0018 if i<n-1 else 0)
        m.slab(0,(a+b)/2,-.0025,h/2,w,b-a,.0032 if lod<2 else 0,.0014 if lod==0 else 0,(0,1,0),phase=i*.317,buried=True)
        y=far

def scar(m,w,h,d):
    # An oblique spall: a crushed blunt heel and a tapered feather edge.
    outline=[(-.50,-.09),(-.42,-.32),(-.21,-.30),(-.09,-.50),(.11,-.27),(.36,-.08),(.50,.18),(.29,.18),(.09,.40),(-.15,.50),(-.24,.28),(-.43,.23)]
    rings=[]
    for level in range(3):
        ids=[]
        for i,(x,y) in enumerate(outline):
            if level==0:q=(x*w*.94,y*d*.91,-h/2)
            elif level==1:q=(x*w,y*d,-.004+.007*math.sin(i*2.3))
            else:q=(x*w*.65-.019,y*d*.43+.007,.014+.006*math.sin(i*1.81))
            ids.append(len(m.p));m.p.append(q)
        rings.append(ids)
    m.face(rings[0][::-1],(1,0,0),0)
    for a,b in zip(rings,rings[1:]):
        for i in range(len(a)):
            j=(i+1)%len(a);m.face([a[i],a[j],b[j],b[i]],(1,0,0),0)
    # A short faceted arris through the blunt end catches light from either side.
    ridge=len(m.p);m.p.append((-.052,.003,h/2))
    for i in range(len(outline)):m.face([rings[-1][i-1],rings[-1][i],ridge],(1,0,0),0)

def dais(m,w,h,d,lod):
    lo=-h/2;hi=h/2
    # Plinth moulding and received bed under a perimeter of individually capped coping stones.
    shape=[(lo,5.58,.08),(lo+.041,5.70,.075),(lo+.102,5.70,.075),(lo+.128,5.61,.085),(hi-.125,5.61,.085),(hi-.104,5.73,.085)]
    if lod==2:shape=[shape[i] for i in (0,1,4,5)]
    m.loft([(z,rect(width,width,cut)) for z,width,cut in shape],2)
    n=(5,3,1)[lod];border=.39;gap=.005 if lod<2 else .003
    for edge in range(4):
        # Alternate header/stretchers. Corners belong to exactly one side.
        start=-w/2+(border if edge>=2 else 0)
        end=w/2-(border if edge>=2 else 0)
        for i in range(n):
            a=start+(end-start)*i/n+(gap if i or edge>=2 else 0)
            b=start+(end-start)*(i+1)/n-(gap if i<n-1 or edge>=2 else 0)
            mid=(a+b)/2;s=(-1 if edge%2 else 1)*(w-border)/2
            x,y=(mid,s) if edge<2 else (s,mid)
            width,depth=((b-a),border) if edge<2 else (border,b-a)
            m.slab(x,y,hi-.109,hi,width,depth,.010 if lod==0 else 0,.007 if lod<2 else 0,phase=i*.278+edge*.165,buried=True)
    span=w-2*border-.012
    rows=(5,3,1)[lod]
    for r in range(rows):
        columns=(4 if r%2==0 else 5) if lod==0 else (2+r%2 if lod==1 else 1)
        depth=span/rows
        for i in range(columns):
            width=span/columns
            m.slab(-span/2+(i+.5)*width,-span/2+(r+.5)*depth,hi-.107,hi-.002-(i%2)*.0009,width-.006,depth-.006,0,.003 if lod==0 else 0,phase=r*.41+i*.27,buried=True)

def make_mat(role):
    mat=bpy.data.materials.new(MATERIALS[role].stem);mat.use_nodes=True
    nodes=mat.node_tree.nodes;link=mat.node_tree.links.new
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    if role in ('cue','scar'):
        srgb=(1,.87,.5) if role=='cue' else (.698,.643,.541)
        bs.inputs['Base Color'].default_value=tuple(c**2.2 for c in srgb)+(1,)
        if role=='cue':bs.inputs['Emission Color'].default_value=(.76,.23,.02,1);bs.inputs['Emission Strength'].default_value=.85
        return mat
    def tex(path,raw=False):
        t=nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(path),check_existing=True);t.image.alpha_mode='CHANNEL_PACKED'
        if raw:t.image.colorspace_settings.name='Non-Color'
        return t
    if role=='bronze':color,normal=ART/'GateBronze_BaseColor.png',ART/'GateBronze_Normal.png'
    else:
        key='Timber' if role=='timber' else 'Stone'
        color=ART/'InlayLimestone_Color.jpg' if role=='limestone' else PHOTO/(key+'_Color.jpg')
        normal=PHOTO/(key+'_Normal.png')
    link(tex(color).outputs['Color'],bs.inputs['Base Color'])
    nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.53 if role!='bronze' else .75
    link(tex(normal,True).outputs['Color'],nm.inputs['Color']);link(nm.outputs[0],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=.74 if role=='timber' else .82
    if role=='bronze':
        t=tex(ART/'GateBronze_MetalSmooth.png',True);sep=nodes.new('ShaderNodeSeparateColor');link(t.outputs[0],sep.inputs[0]);link(sep.outputs[0],bs.inputs['Metallic'])
        inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;link(t.outputs['Alpha'],inv.inputs[1]);link(inv.outputs[0],bs.inputs['Roughness'])
    return mat

def native(ob,dim):
    mesh=ob.data;mesh.calc_loop_triangles();mesh.calc_tangents()
    scale=V((dim[0],dim[2],dim[1]));verts=[];inds=[];lookup={}
    def unity(v):return (v.x,v.z,-v.y)
    for tri in mesh.loop_triangles:
        for i in tri.loops:
            loop=mesh.loops[i];p=mesh.vertices[loop.vertex_index].co
            n=mesh.corner_normals[i].vector;t=loop.tangent
            scaled=lambda v,inv:V([v[j]/scale[j] if inv else v[j]*scale[j] for j in range(3)])
            row=unity(scaled(p,True))+unity(scaled(n,False).normalized())+unity(scaled(t,True).normalized())+(loop.bitangent_sign,)+tuple(mesh.uv_layers.active.data[i].uv)
            if row not in lookup:lookup[row]=len(verts);verts.append(row)
            inds.append(lookup[row])
    arr=np.asarray(verts,dtype='<f4');idx=np.asarray(inds,dtype='<u2')
    assert np.isfinite(arr).all() and len(verts)<65535
    assert np.allclose(arr[:,:3].min(0),-.5,atol=2e-6) and np.allclose(arr[:,:3].max(0),.5,atol=2e-6),(ob.name,arr[:,:3].min(0),arr[:,:3].max(0))
    s=TEMPLATE
    for key,val in {'m_Name':ob.name,'indexCount':len(inds),'vertexCount':len(verts),'m_VertexCount':len(verts),'m_IndexBuffer':idx.tobytes().hex(),'m_DataSize':arr.nbytes,'_typelessdata':arr.tobytes().hex(),'m_Center':'{x: 0, y: 0, z: 0}','m_Extent':'{x: 0.5, y: 0.5, z: 0.5}'}.items():
        s,n=re.subn(r'(?m)^(\s*'+key+r': )[^\n]*',lambda g:g[1]+str(val),s);assert n,key
    path=ART/(ob.name+'.asset');path.write_text(s,encoding='utf8');g=meta(path,PROBE)
    return dict(path=path.relative_to(ROOT).as_posix(),guid=g,fileID='4300000',type=2,triangles=len(inds)//3,vertices=len(verts),sha256=hashlib.sha256(path.read_bytes()).hexdigest())

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    mats={key:make_mat(key) for key in MATERIALS};report={}
    for name,(dim,role,levels) in SPEC.items():
        w,h,d=dim;lods=[]
        for lod in range(levels):
            m=Model(name+('_LOD'+str(lod) if levels>1 else ''),role)
            if 'Strap' in name:strap(m,w,h,d,lod)
            elif name=='CheckGateLeaf':leaf(m,w,h,d,lod)
            elif name.startswith('GaugeFrame'):frame(m,w,h,d,name.endswith('Stile'))
            elif name=='GaugePost':pole(m,w,h,d)
            elif name=='HighImpactScar':scar(m,w,h,d)
            elif name=='WayfindingInlay':inlay(m,w,h,d,lod)
            else:dais(m,w,h,d,lod)
            ob=m.object(mats[role]);lods.append(native(ob,dim))
            ob.hide_render=ob.name!='CheckGateLeaf_LOD0';ob.hide_set(ob.hide_render)
        assert all(a['triangles']>b['triangles'] for a,b in zip(lods,lods[1:])),(name,lods)
        mat=MATERIALS[role];report[name]=dict(metric_local_axes=dim,material=dict(path=mat.relative_to(ROOT).as_posix(),guid=guid(mat),fileID='2100000',type=2),meshes=lods)
    bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'GateFittings.blend'),relative_remap=True)
    (SRC/'mesh-library.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf8')
    print('Published',sum(len(obj['meshes']) for obj in report.values()),'native meshes',flush=True)
if __name__=='__main__':main()
