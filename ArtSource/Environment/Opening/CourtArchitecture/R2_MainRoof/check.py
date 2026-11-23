"""Read the exported FBX and probe both gables, the roof skin, eaves and actual cap feet."""
from pathlib import Path
import sys
import json
import math
import re
from datetime import datetime
import bpy
import bmesh
import numpy as np
from mathutils.bvhtree import BVHTree

sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build as b
import surface_check as samples
sys.path.insert(0,str(Path.home()/'.codex/skills/blender-asset-qa/scripts'))
import qa_check

def imported(path):
    prev=set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return sorted(set(bpy.context.scene.objects)-prev,key=lambda o:o.name)

def geometry(ob):
    p=np.asarray([ob.matrix_world@v.co for v in ob.data.vertices])
    faces=np.asarray([tuple(f.vertices) for f in ob.data.polygons])
    return p,faces

def tree(ob):
    p,tris=geometry(ob)
    return BVHTree.FromPolygons(p.tolist(),tris.tolist(),all_triangles=True)

def surface_stats(ob,tier):
    mat=(b.DEST/(ob.name+'.mat')).read_text()
    assert '_METALLICSPECGLOSSMAP' in mat and '_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A' not in mat
    mult=float(re.search(r'- _Smoothness: (.+)',mat)[1])
    mask=samples.read_map(mat,b.DEST,ob.name,'_MetallicGlossMap','Mask')
    p,tr=geometry(ob)
    pts=p[tr]
    normals=np.cross(pts[:,1]-pts[:,0],pts[:,2]-pts[:,0])
    area=np.linalg.norm(normals,axis=1)
    normals/=area[:,None]
    data=np.empty(len(ob.data.loops)*2,'f')
    ob.data.uv_layers['Surface'].data.foreach_get('uv',data)
    uv=data.reshape(-1,3,2)
    pix=uv.mean(axis=1)*[2048,-2048]+[0,2048]
    ids=np.flatnonzero((pix[:,0]<1280)&(pix[:,1]<1280))
    # A planar tile lane has one repeat every longitudinal pitch and one
    # course every 396 mm. Test edge derivatives so any integer atlas window is legal.
    edges=np.diff(pts[ids],axis=1)
    tile_edges=np.diff(uv[ids],axis=1)*(2048*24/1264)
    residual=[]
    for across,extent in [(0,b.X),(1,b.Y)]:
        pitch=2*extent/round(2*extent/.325)
        for sign in (-1,1):
            expected=np.stack([edges[:,:,across]/pitch,sign*edges[:,:,1-across]/.396],axis=2)
            residual.append(abs(expected-tile_edges).max(axis=(1,2)))
    error=np.min(residual,axis=0)
    assert error.max()<.002,(ob.name,'tile UV drift in repeats',error.max())
    rng=np.random.default_rng(1300)
    pick=rng.choice(ids,150000,p=area[ids]/area[ids].sum())
    f=rng.uniform(size=(len(pick),2))
    f[f.sum(1)>1]=1-f[f.sum(1)>1]
    u=uv[pick,0]+f[:,:1]*(uv[pick,1]-uv[pick,0])+f[:,1:]*(uv[pick,2]-uv[pick,0])
    alpha=samples.sample(mask,u)[:,3]
    cut=(pix[:,0]>1472)&(pix[:,1]>768)&(pix[:,1]<1104)
    c=pts.mean(1)
    front=(abs(abs(c[:,1])-b.Y)<.08)&(normals[:,1]*c[:,1]>b.Y*.75)
    end=(abs(abs(c[:,0])-b.X)<.08)&(normals[:,0]*c[:,0]>b.X*.75)
    allowed=(front|end)&(abs(c[:,2]-(b.EAVE-.112))<.115)
    assert not np.any(cut&~allowed) and bool(np.any(cut))==(tier<2)
    return dict(tiled_surface_samples=len(alpha),tile_triangle_uv_edge_error_max_repeats=round(float(error.max()),6),
                smoothness_multiplier=mult,
                mask_A_p05_p50_p95=samples.pct(alpha),effective_smoothness_p05_p50_p95=samples.pct(mult*alpha),
                detailed_end_triangles=int(cut.sum()),side_faces_on_detailed_end_texture=int((cut&~allowed).sum()))

def probe(roof,caps,tier):
    bv=tree(roof)
    counters={}
    def skin(label,points):
        misses=[]
        N=0
        for x,y in points:
            N+=1
            h=b.envelope(x,y)
            hit,n,idx,d=bv.ray_cast((x,y,b.CROWN+1),(0,0,-1))
            if hit is None or hit.z<h-.041:misses.append([round(x,3),round(y,3)])
        counters[label]={'rays':N,'misses':len(misses),'examples':misses[:5]}
    skin('whole_roof',((x,y) for x in np.linspace(-b.X+.09,b.X-.09,139) for y in np.linspace(-b.Y+.09,b.Y-.09,111)))
    skin('knee',((x,sy*(b.GABLE+dy)) for x in np.linspace(-b.VERGE+.02,b.VERGE-.02,151)
                  for sy in (-1,1) for dy in (-.035,-.006,0,.006,.035)))
    skin('four_hips',((sx*(b.X-t*(b.X-b.WALL)+offset),sy*(b.Y-t*(b.Y-b.GABLE)))
         for sx in (-1,1) for sy in (-1,1) for t in np.linspace(.02,.99,101) for offset in (-.085,-.01,0,.01,.085)))
    # Inspect the identity of the first material seen from above: a closed
    # deck can still look wrong if a truss climbs above its ceramic covering.
    sheet=roof.data.uv_layers['Surface']
    ceramic=[]
    for face in roof.data.polygons:
        u,v=np.mean([sheet.data[k].uv[:] for k in face.loop_indices],axis=0)*2048
        ceramic.append(2048-v<(1280 if u<1280 else 768))
    wood=[]
    rays=0
    for x in np.linspace(-b.X+.115,b.X-.115,247):
        for y in np.linspace(-b.Y+.115,b.Y-.115,151):
            hit,n,idx,d=bv.ray_cast((x,y,b.CROWN+1),(0,0,-1))
            rays+=1
            if idx is not None and not ceramic[idx]:wood.append([round(x,3),round(y,3)])
    counters['wood_through_roof_skin']={'rays':rays,'misses':len(wood),'examples':wood[:10]}
    misses=[]
    total=0
    for sx in (-1,1):
        for s in np.linspace(-b.GABLE+.02,b.GABLE-.02,117):
            high=b.broad(s)-.028
            for z in np.linspace(b.SILL+.01,high,19):
                if z<b.SILL:continue
                total+=1
                hit=bv.ray_cast((sx*(b.X+2),s,z),(-sx,0,0))[0]
                if hit is None or hit.x*sx<b.WALL-.19:misses.append((round(s,3),round(z,3)))
    counters['gable_closure']={'rays':total,'misses':len(misses),'examples':misses[:5]}
    thickness=[]
    for end in (False,True):
        for phase in (.03,.15,.49,.81):
            extent=b.Y if end else b.X
            pitch=2*extent/round(2*extent/.325)
            for k in range(3,round(2*extent/pitch)-3,3):
                s=-extent+(k+phase)*pitch
                xy=(b.X-.092,s) if end else (s,b.Y-.092)
                nom=b.envelope(*xy)
                upper=bv.ray_cast((*xy,nom+.27),(0,0,-1))[0]
                lower=bv.ray_cast((*xy,nom-.098),(0,0,1))[0]
                if lower and upper and .025<upper.z-lower.z<.24:thickness.append(upper.z-lower.z)
    counters['eave_skin_thickness_m']={'sample_count':len(thickness),'p05_p50_p95':np.quantile(thickness,[.05,.5,.95]).round(4).tolist()}
    matrix={}
    for index,cap in enumerate(caps):
        matrix[str(index)]=test_caps(bv,cap)
    counters.update(matrix[str(tier)])
    counters['all_cap_LODs']=matrix
    return counters

def test_caps(bv,cap):
    counters={}
    cp,tr=geometry(cap)
    for sx in (-1,1):
        # Actual shared FBX local +Z points towards Blender -Y. Rotate into the ridge axis.
        cp2=np.column_stack((sx*(b.REAR-cp[:,1]),sx*cp[:,0]*1.60,b.SEAT+cp[:,2]*1.56))
        cap_bv=BVHTree.FromPolygons(cp2.tolist(),tr.tolist(),all_triangles=True)
        overlaps=bv.overlap(cap_bv)
        feet=[]
        for sy in (-1,1):
            supported=0
            # Interrogate the nested mesh underside first, then the mount immediately below it.
            gaps=[]
            for t in np.linspace(.035,.315,12):
                for offset in (.240,.248,.254):
                    x=sx*(b.REAR+t);y=sy*offset
                    sole=cap_bv.ray_cast((x,y,b.SEAT-.18),(0,0,1))[0]
                    if not sole:continue
                    seat=bv.ray_cast((x,y,b.SEAT+.035),(0,0,-1))[0]
                    if seat:
                        gap=sole.z-seat.z
                        gaps.append(gap)
                        if -.044<=gap<=.008:supported+=1
            feet.append({'side':sy,'hits':supported,'tested':len(gaps),'gap_range_m':np.round([min(gaps),max(gaps)],4).tolist() if gaps else []})
        counters['cap_'+str(sx)]={'contact_triangle_pairs':len(overlaps),'feet':feet}
    return counters

def run(published=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fbx=(b.DEST if published else b.HERE)/(b.MODEL+'.fbx')
    obs=imported(fbx)
    caps=imported(b.CAP/'PreqinRidgeCap.fbx')
    data={}
    for i,(ob,cap) in enumerate(zip(obs,caps)):
        assert ob.name==b.MODEL+'_Tier'+str(i)
        assert len(ob.material_slots)==1 and ob.material_slots[0].name==ob.name+'_Baked'
        ob.data.materials[0].name='FBX_'+ob.name
        ob.data.materials[0]=b.roof.material(ob.name,b.DEST)
        assert list(ob.data.uv_layers.keys())==['Surface','Lightmap']
        result=qa_check.run(objects=[ob.name],profile='game',eps=.0002,tol=.003)
        measures=probe(ob,caps,i)
        p,tr=geometry(ob)
        bm=bmesh.new();bm.from_mesh(ob.data);bm.loops.layers.uv.remove(bm.loops.layers.uv['Surface'])
        uv2=qa_check._uv_stats(ob,bm);bm.free()
        print(ob.name,result['errors'],result['warnings'],flush=True)
        print({k:v for k,v in measures.items() if v.get('misses') or k in ('cap_1','eave_skin_thickness_m')},flush=True)
        data[ob.name]=dict(triangles=len(tr),bounds_blender=[p.min(0).round(5).tolist(),p.max(0).round(5).tolist()],
            game_qa=result,lightmap_uv=uv2,surface=surface_stats(ob,i),measurements=measures)
    passed=all(row['game_qa']['pass'] and not any(v.get('misses',0) for v in row['measurements'].values())
               and all(f['hits']==f['tested']==36 for cap_pair in row['measurements']['all_cap_LODs'].values()
                       for item in cap_pair.values() for f in item['feet'])
               and row['lightmap_uv']['uv_overlap_pct']==0 and row['lightmap_uv']['uv_zero_area_faces']==0
               for row in data.values())
    report=dict(blender=bpy.app.version_string,time=datetime.now().astimezone().isoformat(timespec='seconds'),
                fbx_sha256=b.sha(fbx),scope='Published roof FBX' if published else 'Staging FBX',passed=passed,tiers=data)
    (b.HERE/'Roof_QA.json').write_text(json.dumps(report,indent=2))
    print('PASS',passed)
    return passed

if __name__=='__main__':
    if not run('--published' in sys.argv):sys.exit(1)

