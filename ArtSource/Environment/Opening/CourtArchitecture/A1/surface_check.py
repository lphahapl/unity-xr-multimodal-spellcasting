"""Probe actual surface triangles, UVs, linked Unity textures and original URP Lit settings."""
from pathlib import Path
import hashlib
import json
import math
import re
import sys
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import roof
from surface_uv import END_RECTS

def sample(image,uv):
    size=np.array(image.shape[1::-1])
    p=np.clip(uv*size-.5,0,size-1.000001)
    i=p.astype('int32')
    x,y=i.T
    a,b=(p-i).T[:,:,None]
    return (1-b)*((1-a)*image[y,x]+a*image[y,x+1])+b*((1-a)*image[y+1,x]+a*image[y+1,x+1])

def read_map(mat,dest,tag,prop,suffix):
    path=dest/(tag+'_'+suffix+'.png')
    meta=Path(str(path)+'.meta').read_text()
    gid=re.search('^guid: (.+)',meta,re.M)[1]
    assert re.search(r'- '+prop+r':\s+m_Texture: \{[^}]*guid: '+gid,mat)
    data=bpy.data.images.load(str(path))
    data.colorspace_settings.name='Non-Color'
    data.alpha_mode='CHANNEL_PACKED'
    w,h=data.size
    a=np.empty((h,w,4),'f')
    data.pixels.foreach_get(a.ravel())
    bpy.data.images.remove(data)
    return a

def pct(values):
    return np.round(np.percentile(values,[5,50,95]),4).tolist()

def signature(positions,tris,uv2):
    # Winding, all three metre coordinates and associated lightmap corners, order independent.
    records=[]
    for t,v in zip(tris,uv2):
        row=np.round(np.column_stack((positions[t],v))*1.e6).astype('<i4')
        b=[row[i].tobytes() for i in range(3)]
        records.append(min(b[i]+b[(i+1)%3]+b[(i+2)%3] for i in range(3)))
    return hashlib.sha256(b''.join(sorted(records))).hexdigest()

def report(model,stage):
    dest=roof.DEST/model
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(dest/(model+'.fbx')))
    result={}
    for tier,o in enumerate(sorted(bpy.context.scene.objects,key=lambda ob:ob.name)):
        matpath=dest/(o.name+'.mat')
        mat=matpath.read_text()
        assert '_METALLICSPECGLOSSMAP' in mat and '_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A' not in mat
        mult=float(re.search(r'- _Smoothness: (.+)',mat)[1])
        base=read_map(mat,dest,o.name,'_BaseMap','BaseColor')
        mask=read_map(mat,dest,o.name,'_MetallicGlossMap','Mask')
        scalar=np.array([.299,.587,.114])
        m=o.data
        positions=np.asarray([o.matrix_world@v.co for v in m.vertices])
        tris=np.array([f.vertices[:] for f in m.polygons])
        points=positions[tris]
        area=np.linalg.norm(np.cross(points[:,1]-points[:,0],points[:,2]-points[:,0]),axis=1)
        def channel(name):
            raw=np.empty(len(m.loops)*2,'f')
            m.uv_layers[name].data.foreach_get('uv',raw)
            return raw.reshape(-1,3,2)
        uv=channel('Surface')
        pix=uv.mean(axis=1)*[2048,-2048]+[0,2048]
        use=np.flatnonzero((pix[:,0]<1280)&(pix[:,1]<1280))
        rng=np.random.default_rng(1300)
        def surface_samples(ids,count):
            choice=rng.choice(ids,size=count,p=area[ids]/area[ids].sum())
            f=rng.uniform(size=(count,2))
            f[f.sum(axis=1)>1]=1-f[f.sum(axis=1)>1]
            return uv[choice,0]+f[:,:1]*(uv[choice,1]-uv[choice,0])+f[:,1:]*(uv[choice,2]-uv[choice,0])
        query=surface_samples(use,150000)
        alpha=sample(mask,query)[:,3]
        row=dict(triangles=len(tris),mesh_lightmap_digest=signature(positions,tris,channel('Lightmap')),
            fbx_geometry_model_material_ids=roof.fbx_ids(dest/(model+'.fbx')),
            smoothness_multiplier=mult,roof_samples=len(query),roof_mask_A_p05_p50_p95=pct(alpha),
            roof_effective_smoothness_p05_p50_p95=pct(mult*alpha),
            roof_luma_8bit_p05_p50_p95=pct((sample(base,query)[:,:3]@scalar)*255))
        x,y,eave,crown,ridge=roof.MODELS[model]
        if stage=='after':
            centres=points.mean(axis=1)
            normals=np.cross(points[:,1]-points[:,0],points[:,2]-points[:,0])/np.maximum(1e-8,area[:,None])
            outward_front=(abs(abs(centres[:,1])-y)<.08)&(normals[:,1]*centres[:,1]>y*.75)
            outward_side=(ridge<x)&(abs(abs(centres[:,0])-x)<.08)&(normals[:,0]*centres[:,0]>x*.75)
            allowed=(outward_front|outward_side)&(abs(centres[:,2]-(eave-.112))<.115)
            total=0
            for rect in END_RECTS[1:]:
                x0,y0,x1,y1=rect
                selected=(pix[:,0]>x0)&(pix[:,0]<x1)&(pix[:,1]>y0)&(pix[:,1]<y1)
                assert np.all(allowed[selected]),(o.name,'side sampling a rafter cut')
                corners=uv[selected].reshape(-1,2)*[2048,-2048]+[0,2048]
                assert ((corners>=[x0+7,y0+7])&(corners<=[x1-7,y1-7])).all()
                total+=int(selected.sum())
            row['triangles_on_detailed_rafter_end_panels']=total
            row['non_end_triangles_on_detailed_rafter_end_panels']=0
            assert (total>0)==(tier<2)
        if tier==0:
            centres=points.mean(axis=1)
            normals=np.cross(points[:,1]-points[:,0],points[:,2]-points[:,0])/np.maximum(1e-8,area[:,None])
            c0=abs(centres[:,1]+y)<.09
            for label,z,box in [('wood',eave-.112,roof.ZONES['end']),('ceramic',eave+.004,roof.ZONES['stamp'])]:
                hits=np.flatnonzero(c0 & (abs(centres[:,2]-z)<.10) & (normals[:,1]<-.75)
                      & (pix[:,0]>=box[0]) & (pix[:,0]<box[2]) & (pix[:,1]>=box[1]) & (pix[:,1]<box[3]))
                assert len(hits)>0,(model,label)
                raw_uv=uv[hits].reshape(-1,2)*[2048,-2048]+[0,2048]
                facevals=sample(base,surface_samples(hits,24000))[:,:3]@scalar*255
                row[label+'_tip']=dict(front_cap_triangles=len(hits),atlas_min_px=raw_uv.min(0).round(2).tolist(),
                    atlas_max_px=raw_uv.max(0).round(2).tolist(),luma_8bit_p05_p50_p95=pct(facevals))
            # Compare the same position on actual cover/pan tiles eight columns apart.
            tree=BVHTree.FromPolygons(positions.tolist(),tris.tolist(),all_triangles=True)
            pitch=2*x/round(x*2/.325)
            diffs=[]
            roof_ids=set(use.tolist())
            for course in range(min(16,int(y/.396))):
                for j in range(round(2*x/.325)-8):
                    values=[]
                    for column in (j,j+8):
                        xx=-x+(column+.14)*pitch
                        yy=-y+(course+.55)*.396
                        hit,n,idx,d=tree.ray_cast((xx,yy,crown+1),(0,0,-1))
                        if idx not in roof_ids:break
                        a,b,c=points[idx]
                        weights=np.linalg.lstsq(np.column_stack([b-a,c-a]),np.array(hit)-a,rcond=None)[0]
                        lookup=uv[idx,0]+weights@(uv[idx,1:]-uv[idx,0])
                        values.append(float(sample(base,lookup[None,:])[0,:3]@scalar*255))
                    if len(values)==2:diffs.append(values[0]-values[1])
            row['eight_column_luma_delta_rms_8bit']=round(float(np.mean(np.square(diffs))**.5),3)
            row['valid_tile_pairs']=len(diffs)
        result[o.name]=row
    path=HERE/'Review'/f'{model}_surface_{stage}.json'
    if stage=='after':
        old=json.loads(path.with_name(f'{model}_surface_before.json').read_text())
        for k,row in result.items():
            assert row['mesh_lightmap_digest']==old[k]['mesh_lightmap_digest']
            assert row['fbx_geometry_model_material_ids']==[tuple(v) for v in old[k]['fbx_geometry_model_material_ids']]
    path.write_text(json.dumps(result,indent=2),encoding='utf8')
    print(model,stage,result[model+'_Tier0']['roof_effective_smoothness_p05_p50_p95'])

if __name__=='__main__':report(*sys.argv[-2:])

