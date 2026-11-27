"""Audit the exact displayed mesh exported from the Chapter 02 carrier's stone root.

Run from the project root with python-fcl, numpy and scipy installed. OBJ coordinates
are Unity stone-local XYZ in metres. No importer axis conversion is applied.
"""
import argparse
import csv
from pathlib import Path

import fcl
import numpy as np
from scipy.spatial.transform import Rotation
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy.optimize import differential_evolution, minimize

from Chapter02BallastClearance import load_capture, triangle_sets, follow_palms, write_transforms


def load_obj(path):
    verts, tris, groups = [], [], []
    group = 'stone'
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        cols = line.split()
        if not cols:
            continue
        if cols[0]=='v':
            verts.append([float(x) for x in cols[1:4]])
        if cols[0] in ('usemtl','g','o'):
            group=' '.join(cols[1:])
        if cols[0]=='f':
            face=[int(x.split('/')[0]) for x in cols[1:]]
            face=[i-1 if i>0 else len(verts)+i for i in face]
            for i in range(1,len(face)-1):
                tris.append([face[0],face[i],face[i+1]])
                groups.append(group)
    return np.array(verts), np.array(tris), groups


def mesh(verts,tris):
    geom=fcl.BVHModel()
    geom.beginModel(len(tris),len(verts))
    geom.addSubModel(verts,tris)
    geom.endModel()
    return fcl.CollisionObject(geom)


def distance(a,b):
    result=fcl.DistanceResult()
    d=fcl.distance(a,b,fcl.DistanceRequest(enable_nearest_points=True),result)
    return max(0.,d),result


def shells(vertices,faces):
    """Identify connected triangle shells after joining coincident rendering seams."""
    _,weld=np.unique(vertices.round(5),axis=0,return_inverse=True)
    f=weld[faces]
    graph=coo_matrix((np.ones(3*len(f)),(f.ravel(),np.roll(f,1,axis=1).ravel())),
                     shape=(weld.max()+1,weld.max()+1))
    _,label=connected_components(graph,directed=False)
    labels=label[f[:,0]]
    result=[]
    for group in np.unique(labels):
        fs=f[labels==group]
        edges=np.sort(np.stack([fs,np.roll(fs,1,axis=1)],axis=2).reshape(-1,2),axis=1)
        _,counts=np.unique(edges,axis=0,return_counts=True)
        result.append((faces[labels==group],bool((counts==2).all())))
    return result


def contained(point,triangles):
    if np.any(point<=triangles.min(axis=(0,1))) or np.any(point>=triangles.max(axis=(0,1))):
        return False
    # Ray parity for closed shells. Deduplicate hits on shared edges/triangulation seams.
    direction=np.array([.78931,.46271,.28019])
    a,b,c=triangles.transpose(1,0,2)
    e1,e2=b-a,c-a
    p=np.cross(direction,e2)
    det=(e1*p).sum(1)
    inv=np.divide(1.,det,out=np.zeros_like(det),where=np.abs(det)>1e-12)
    s=point-a
    u=(s*p).sum(1)*inv
    q=np.cross(s,e1)
    v=(q*direction).sum(1)*inv
    t=(q*e2).sum(1)*inv
    hits=t[(np.abs(det)>1e-12)&(u>=0)&(v>=0)&(u+v<=1)&(t>1.e-9)]
    return len(np.unique(hits.round(8)))%2==1


def search_fit(data,sv,stone_shells,moderate=False):
    # The scene's five closed components are cuboids. Confirm their exact form before
    # using the cheaper support-plane solver. The final test still uses the exported triangles.
    solids=[]
    for faces,closed in stone_shells:
        pts=np.unique(sv[faces].reshape(-1,3).round(6),axis=0)
        assert closed and len(pts)==8 and len(faces)==12 and all(len(np.unique(p))==2 for p in pts.T)
        solids.append(((pts.max(0)+pts.min(0))/2,np.ptp(pts,axis=0)/2))
    v,tri,masks,mid=data
    _,ids=np.unique(v[0].round(5),axis=0,return_index=True)
    ids=ids[v[0,ids,1]>-.40]
    frames=[0,22,44,65]
    meshpoints=v[frames][:,ids]
    palms=[v[frames][:,masks[k]] for k in ('left','right')]
    def cost(p):
        rot=Rotation.from_euler('xyz',p[:3],degrees=True).as_matrix()
        local=(meshpoints-p[3:])@rot
        deepest=min((np.abs(local-c)-h).max(2).min() for c,h in solids)
        val=10*max(0.,.005-deepest)
        for hand in palms:
            pts=(hand-p[3:])@rot
            per_box=[]
            for c,h in solids:
                q=pts-c
                horiz=np.maximum(np.abs(q[:,:,[0,2]])-h[[0,2]],0)
                per_box.append(np.sqrt((horiz*horiz).sum(2)+(q[:,:,1]+h[1])**2).min(1))
            miss=np.min(per_box,axis=0)
            val+=miss.max()+.25*miss.mean()
        return val+.20*abs(p[3])+.33*max(0.,p[5]-.14)
    bounds=[(-55,22),(-65,65),(-50,50),(-.23,.23),(.30,.95),(.055,.70)]
    if moderate:
        bounds[:3]=[(-28,22),(-35,35),(-20,20)]
    best=differential_evolution(cost,bounds,seed=38,popsize=8,maxiter=55,polish=False)
    fit=minimize(cost,best.x,method='Nelder-Mead',bounds=bounds,options={'maxiter':420})
    return follow_palms(data,fit.x,solids=solids)


def run(capture,obj,poses,out,yaw=0,lift=False,fit=False,moderate=False):
    data=load_capture(capture)
    v,t,masks,mid=data
    sv,st,groups=load_obj(obj)
    stone=mesh(sv,st)
    sets=triangle_sets(t,masks)
    actor_shells=shells(v[0],t)
    stone_shells=shells(sv,st)
    out.mkdir(parents=True,exist_ok=True)
    if fit or moderate:
        write_transforms(out,search_fit(data,sv,stone_shells,moderate),mid)
        poses=out/'candidate.csv'
    x=np.loadtxt(poses,delimiter=',',skiprows=1)
    assert len(x)==len(v) and np.allclose(x[:,8:],1)
    rows=[]
    for i,verts in enumerate(v):
        q=Rotation.from_quat(x[i,4:8])*Rotation.from_euler('y',yaw,degrees=True)
        base=x[i,1:4].copy()-mid[i]
        actors={k:mesh(verts,faces) for k,faces in sets.items()}
        def at(y):
            stone.setTransform(fcl.Transform(q.as_matrix(),base+[0,y,0]))
        correction=0.
        if lift:
            # Descend from a known free elevation to the first shell along this short
            # vertical adjustment. Bisection uses true triangle surface distances.
            high=.30
            at(high)
            if distance(actors['all'],stone)[0] < .003:
                raise ValueError(f'frame {i}: insufficient starting clearance')
            for low in np.arange(high-.01,-.32,-.01):
                at(low)
                if distance(actors['all'],stone)[0] < .003:
                    break
                high=low
            else:
                raise ValueError(f'frame {i}: no limiting surface within search range')
            for _ in range(12):
                y=(low+high)/2
                at(y)
                if distance(actors['all'],stone)[0] < .003:
                    low=y
                else:
                    high=y
            correction=high
        at(correction)
        x[i,2]+=correction
        x[i,4:8]=q.as_quat()
        row=dict(frame=i,lift_m=correction)
        for name,a in actors.items():
            d,res=distance(a,stone)
            collision=fcl.CollisionResult()
            hit=fcl.collide(a,stone,fcl.CollisionRequest(num_max_contacts=1,enable_contact=True),collision)
            row[name+'_gap_m']=d
            row[name+'_hit']=int(bool(hit))
            if name=='extension':
                tri_id=collision.contacts[0].b2 if hit else res.b2
                point=np.array(collision.contacts[0].pos if hit else res.nearest_points[1])
                local=q.inv().apply(point-base-[0,correction,0])
                row.update(stone_triangle=int(tri_id),stone_group=groups[tri_id])
                for k,n in zip('xyz',local):
                    row['stone_witness_'+k]=n
        sp=q.apply(sv)+base+[0,correction,0]
        if not row['all_hit']:
            # After a disjoint surface test, one representative per connected shell
            # suffices to rule out a fully enclosed disconnected mesh component.
            ref=[q.inv().apply(verts[f[0,0]]-base-[0,correction,0]) for f,_ in actor_shells]
            row['actor_shell_inside_stone']=int(any(contained(p,sv[f]) for f,closed in stone_shells
                                                    if closed for p in ref))
            row['stone_shell_inside_actor']=int(any(contained(sp[h[0,0]],verts[f])
                  for f,closed in actor_shells if closed for h,_ in stone_shells))
        else:
            row.update(actor_shell_inside_stone='',stone_shell_inside_actor='')
        ext=verts[np.unique(sets['extension'])]
        row['extension_aabb_overlap']=int(np.all(sp.max(0)>=ext.min(0)) and np.all(sp.min(0)<=ext.max(0)))
        rows.append(row)
    out.mkdir(parents=True,exist_ok=True)
    with (out/'frames.csv').open('w',encoding='utf-8',newline='') as f:
        w=csv.DictWriter(f,rows[0]);w.writeheader();w.writerows(rows)
    np.savetxt(out/'candidate.csv',x,delimiter=',',header='frame,x,y,z,qx,qy,qz,qw,sx,sy,sz',
               comments='',fmt=['%d']+['%.12g']*10)
    print(out.name,{k:(round(min(r[k] for r in rows),5),round(max(r[k] for r in rows),5))
          for k in rows[0] if 'gap' in k or k=='lift_m'},
          {k:sum(r[k] for r in rows) for k in rows[0] if k.endswith('hit') or k.endswith('overlap')})


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('capture','obj','poses','out'):
        p.add_argument(name,type=Path)
    p.add_argument('--yaw',type=float,default=0)
    p.add_argument('--lift',action='store_true')
    p.add_argument('--fit',action='store_true',help='Search a new two-palm pose on the exported scene geometry')
    p.add_argument('--moderate',action='store_true',help='Also constrain the initial roll and pitch during search')
    run(**vars(p.parse_args()))
