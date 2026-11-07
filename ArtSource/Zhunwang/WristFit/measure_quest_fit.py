import bpy,numpy as np,json,math
from pathlib import Path
from collections import defaultdict
root=next(p for p in Path(__file__).resolve().parents if (p/'ProjectSettings/ProjectVersion.txt').is_file())
out=root/'Logs/recovery-20261107';out.mkdir(parents=True,exist_ok=True)
sdk=next((root/'Library/PackageCache').glob('com.meta.xr.sdk.interaction@*'))
hand=sdk/'Runtime/Meshes/OpenXRLeftHand.fbx'
prop=root/'Assets/Art/Props/Zhunwang/ButtonSplit/Zhunwang_ButtonSplit.fbx'
def load(p,name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(p))
    ob=next(o for o in bpy.data.objects if o.type=='MESH' and name in o.name)
    ob.data.calc_loop_triangles()
    V=np.array([list(ob.matrix_world@v.co) for v in ob.data.vertices])
    F=np.array([list(t.vertices) for t in ob.data.loop_triangles])
    return V,F

def contours(V,F,y):
    triangles=V[F]
    ys=triangles[:,:,1]
    cut=triangles[(ys.min(1)<y)&(ys.max(1)>y)]
    adj=defaultdict(set);coord={}
    for f in cut:
        points=[]
        for a,b in zip(f,np.roll(f,-1,axis=0)):
            if (a[1]<y)==(b[1]<y):continue
            x=a+(b-a)*((y-a[1])/(b[1]-a[1]))
            point=np.array([x[0],x[2]])
            k=tuple(np.round(point,6));coord[k]=point
            points.append(k)
        if len(points)==2 and points[0]!=points[1]:
            a,b=points;adj[a].add(b);adj[b].add(a)
    remaining=set(adj); result=[]
    while remaining:
        q=next(iter(remaining));stack=[q];component=set()
        while stack:
            a=stack.pop()
            if a in component:continue
            component.add(a);stack.extend(adj[a]-component)
        remaining-=component
        if any(len(adj[k])!=2 for k in component):
            result.append((np.array([coord[k] for k in component]),False))
            continue
        path=[q];prev=None;cur=q
        while True:
            nxt=next(n for n in adj[cur] if n!=prev)
            if nxt==q:break
            path.append(nxt);prev,cur=cur,nxt
            if len(path)>len(component):break
        result.append((np.array([coord[k] for k in path]),len(path)==len(component)))
    return result

def metrics(poly):
    n=np.roll(poly,-1,axis=0)
    area=abs(np.sum(poly[:,0]*n[:,1]-poly[:,1]*n[:,0])*.5)
    lo,hi=poly.min(0),poly.max(0)
    return dict(area=area,extent=(hi-lo).tolist(),center=((hi+lo)*.5).tolist(),bounds=[lo.tolist(),hi.tolist()],contour=poly.tolist())

H,HF=load(hand,'LeftHand')
P,PF=load(prop,'Body')
results={'sourceHand':str(hand),'sourceProp':str(prop),'hand':[],'bands':[],'wearScale':.1020813857}
for y in [-.018,-.012,-.008,-.004,0,.004,.008,.012]:
    cs=contours(H,HF,y+.0000001)
    if not cs:continue
    poly,closed=max(cs,key=lambda c:np.ptp(c[0],axis=0).sum())
    s=metrics(poly)
    s.update(y=y,closed=closed)
    results['hand'].append(s)
    print('WRIST',y,np.round(np.array(s['extent'])*1000,2),s['center'],closed)
for y in np.linspace(-.405,.515,93):
    cs=contours(P,PF,y+.00000001)
    for poly,closed in cs:
        if not closed or len(poly)<12:continue
        s=metrics(poly)
        lo,hi=np.array(s['bounds'])
        if .12<hi[1]<.58 and lo[1]<.26 and s['area']>.035 and s['extent'][0]>.21 and abs(s['center'][0])<.13:
            results['bands'].append(dict(y=y,**s))
            print('APERTURE',round(y,4),np.round(np.array(s['extent'])*102.0814,2),round(s['area'],4),'height',np.round([lo[1],hi[1]],3))
(out/'sections.json').write_text(json.dumps(results,indent=2),encoding='utf-8')