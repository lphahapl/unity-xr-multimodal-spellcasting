"""Direct UV-texel BVH thickness matching actual CORNER shading normals."""
import bpy,sys,json,time,hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parent/'RevisionA';LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r03-20261126/model/revision-a'
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'));ob=bpy.data.objects['QingGuiR03_JadeCore_LOD0'];me=ob.data;me.calc_loop_triangles();tree=BVHTree.FromObject(ob,bpy.context.evaluated_depsgraph_get())
S=2048;values=np.ones((S,S),np.float32);covered=np.zeros((S,S),bool);raw=[];miss=0;retry=0;started=time.time();eps=.000015
uv=np.array([tuple(x.uv) for x in me.uv_layers.active.data]);vs=np.array([tuple(v.co) for v in me.vertices]);cn=np.array([tuple(n.vector) for n in me.corner_normals])
for ti,t in enumerate(me.loop_triangles):
    ls=list(t.loops);a,b,c=uv[ls]*S;lo=np.maximum(0,np.floor(np.min([a,b,c],axis=0)).astype(int));hi=np.minimum(S-1,np.ceil(np.max([a,b,c],axis=0)).astype(int))
    if np.any(hi<lo):continue
    d1=b-a;d2=c-a;den=d1[0]*d2[1]-d1[1]*d2[0]
    if abs(den)<1e-10:continue
    yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1];dx=xx+.5-a[0];dy=yy+.5-a[1];w1=(dx*d2[1]-dy*d2[0])/den;w2=(d1[0]*dy-d1[1]*dx)/den;w0=1-w1-w2;inside=(w0>=-1e-7)&(w1>=-1e-7)&(w2>=-1e-7)
    xs=xx[inside];ys=yy[inside];weights=np.stack([w0[inside],w1[inside],w2[inside]],axis=1);points=weights@vs[list(t.vertices)];norms=weights@cn[ls];norms/=np.maximum(1e-12,np.linalg.norm(norms,axis=1))[:,None]
    for x,y,p,n in zip(xs,ys,points,norms):
        if covered[y,x]:continue
        pv=Vector(p);nv=Vector(n);hit=tree.ray_cast(pv-nv*eps,-nv,.6)
        if hit[0] is None:
            retry+=1;nv=t.normal;hit=tree.ray_cast(pv-nv*eps,-nv,.6)
        if hit[0] is None:d=.12;miss+=1
        else:d=max(0,float(hit[3])+eps)
        values[y,x]=min(1,d/.12);covered[y,x]=True;raw.append(d)
    if ti%2000==0:print('TEXEL_RAYS',ti,len(raw),flush=True)
# Eight-pixel data padding only outside occupied UV texels, never replacing rays.
padding=covered.copy()
for _ in range(8):
    total=np.zeros_like(values);count=np.zeros_like(values)
    for dy,dx in [(-1,0),(1,0),(0,-1),(0,1)]:
        m=np.roll(padding,(dy,dx),(0,1));v=np.roll(values,(dy,dx),(0,1));total+=v*m;count+=m
    fill=(~padding)&(count>0);values[fill]=total[fill]/count[fill];padding|=fill
im=bpy.data.images.get('QingGuiR03_JadeCore_Thickness');assert im
old=P/'CandidateTextures/QingGuiR03_JadeCore_Thickness.png';backup=P/'CandidateTextures/Diagnostic_VertexThickness.png'
backup.write_bytes(old.read_bytes())
rgba=np.ones((S,S,4),np.float32);rgba[:,:,:3]=values[:,:,None];im.pixels.foreach_set(rgba.ravel());im.update();im.filepath_raw=str(old);im.file_format='PNG';im.save()
arr=np.array(raw);report={'method':'Per occupied UV pixel center, barycentric actual triangle position; interpolated normalized Blender CORNER normal identical to exported shading direction; independent JadeCore BVH inward ray','sampling_resolution':[S,S],'direction':'negative interpolated CORNER normal','origin_inset_m':eps,'measured_texels':len(raw),'triangle_normal_fallbacks':retry,'unresolved_miss':miss,'miss_fallback_m':.12,'clamped_above_120mm':int(np.count_nonzero(arr>.12)),'min_m':float(arr.min()),'max_m':float(arr.max()),'percentiles_m':{str(q):float(np.percentile(arr,q)) for q in [1,5,25,50,75,95,99]},'decode':'linear R * 0.12 m','outside_island_padding_px':8,'seconds':time.time()-started,'source_is_B':False,'limitation':'Single directional chord on candidate mesh; 120mm clamp and PNG quantization; not multiple scattering. Mobile shadow triangle issue was independently diagnosed in Unity, not attributed to this map.'}
(LOG/'texel-jade-thickness.json').write_text(json.dumps(report,indent=2),'utf-8');bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'))
ready=json.loads((P/'candidate-ready.json').read_text('utf-8'));ready['thickness']=report
for f in ready['files']:
    file=Path(f['path']);f['sha256']=hashlib.sha256(file.read_bytes()).hexdigest();f['bytes']=file.stat().st_size
(P/'candidate-ready.json').write_text(json.dumps(ready,ensure_ascii=False,indent=2),'utf-8');print('DIRECT_TEXEL_THICKNESS_READY',report,flush=True)
