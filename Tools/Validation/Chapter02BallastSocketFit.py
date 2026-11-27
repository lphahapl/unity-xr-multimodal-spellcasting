"""Regress the submitted stone mesh against the exported A-basket in dock-local metres."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

import fcl
import numpy as np
from scipy.spatial import ConvexHull, cKDTree
from scipy.spatial.transform import Rotation
from Chapter02BallastSceneClearance import load_obj, mesh


def box_rows(path):
    rows=[]
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        name,*vectors=line.split('\t')
        center,*axes=[np.fromstring(v,sep=' ') for v in vectors]
        half=np.abs(axes).sum(0)
        rows.append((name,center,half,np.array(axes)))
    return rows


def aperture(boxes):
    # The four rim colliders and socket floor live on the same compound collider object.
    rims=[(c,h) for name,c,h,_ in boxes if '配重槽' in name and h[1]>.04]
    assert len(rims)==4, rims
    limits=[]
    for i in (0,2):
        left,right=sorted(((c[i],h[i]) for c,h in rims if h[i]<.20),key=lambda pair:pair[0])
        limits.append((left[0]+left[1],right[0]-right[1]))
    floor=[c[1]+h[1] for name,c,h,_ in boxes if '配重槽' in name and h[1]<.04]
    assert len(floor)==1
    return np.array(limits),float(floor[0])


def margins(points,limits):
    xz=points[:,[0,2]]
    return [xz[:,0].min()-limits[0,0],limits[0,1]-xz[:,0].max(),
            xz[:,1].min()-limits[1,0],limits[1,1]-xz[:,1].max()]


def check(stone_path,socket,out,imported_vertices=None):
    verts,tri,_=load_obj(stone_path)
    bv,bt,groups=load_obj(socket/'socket.obj')
    stone=mesh(verts,tri)
    boxes=box_rows(socket/'colliders.tsv')
    limits,floor=aperture(boxes)
    result={'model':stone_path.name,'sha256':hashlib.sha256(stone_path.read_bytes()).hexdigest(),
            'verts':len(verts),'faces':len(tri),
            'opening_limits_xz_m':limits.tolist(),'basis':(socket/'slot.txt').read_text().strip(),
            'floor_y_m':floor,'base_to_socket_floor_m':verts[:,1].min()-floor,
            'aligned_left_right_back_front_m':margins(verts,limits),'visual':{},'colliders':[]}
    if imported_vertices:
        points=np.loadtxt(imported_vertices)
        error=max(cKDTree(verts).query(points)[0].max(),cKDTree(points).query(verts)[0].max())
        result['unity_fbx_identity_hausdorff_m']=error
        if error>.0001:
            raise ValueError(f'Unity FBX differs from collision geometry by {error} m')
    for name,center,_,axes in boxes:
        sizes=np.linalg.norm(axes,axis=1)
        rotation=(axes/sizes[:,None]).T
        if np.linalg.det(rotation)<0: rotation[:,0]*=-1
        collider=fcl.CollisionObject(fcl.Box(*(2*sizes)),fcl.Transform(rotation,center))
        value=fcl.distance(stone,collider,fcl.DistanceRequest(),fcl.DistanceResult())
        hit=fcl.collide(stone,collider,fcl.CollisionRequest(),fcl.CollisionResult())
        result['colliders'].append(dict(name=name,center=center.tolist(),min_m=max(0.,value),hit=bool(hit)))
    # Entire straight descent remains inside the aperture if the full horizontal projection fits.
    for name in sorted(set(groups)):
        faces=bt[np.array(groups)==name]
        obj=mesh(bv,faces)
        distance=fcl.distance(stone,obj,fcl.DistanceRequest(),fcl.DistanceResult())
        contacts=fcl.CollisionResult()
        hit=fcl.collide(stone,obj,fcl.CollisionRequest(num_max_contacts=1,enable_contact=True),contacts)
        result['visual'][name]={'min_m':max(0.,distance),'touch_or_overlap':bool(hit)}
    angles=np.arange(-45.,45.01,.25)
    samples=[min(margins(Rotation.from_euler('y',a,degrees=True).apply(verts),limits)) for a in angles]
    zero=int(np.argmin(np.abs(angles)))
    low=high=zero
    while low>0 and samples[low-1]>=0:low-=1
    while high+1<len(samples) and samples[high+1]>=0:high+=1
    result['uninterrupted_yaw_range_deg_025_samples']=[angles[low],angles[high]]
    out.mkdir(parents=True,exist_ok=True)
    (out/'fit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    with (out/'yaw.csv').open('w',newline='') as f:
        w=csv.writer(f);w.writerow(['yaw_deg','worst_side_m']);w.writerows(zip(angles,samples))
    from matplotlib import pyplot as plt
    from matplotlib.patches import Rectangle
    from matplotlib.font_manager import FontProperties
    ft=FontProperties(fname='C:/Windows/Fonts/msyh.ttc')
    fig,axs=plt.subplots(1,2,figsize=(10.6,5.4),facecolor='#f5f1e8')
    for ax,yaw in zip(axs,(0,45)):
        ax.set_facecolor('#f5f1e8')
        for name,c,h,_ in boxes:
            if '配重槽' in name and h[1]>.04:
                ax.add_patch(Rectangle((c[0]-h[0],c[2]-h[2]),2*h[0],2*h[2],facecolor='#817965'))
        p=Rotation.from_euler('y',yaw,degrees=True).apply(verts)[:,[0,2]]
        hull=p[ConvexHull(p).vertices]
        ax.fill(hull[:,0],hull[:,1],color='#306b69' if yaw==0 else '#a55d46',alpha=.78)
        gap=min(margins(Rotation.from_euler('y',yaw,degrees=True).apply(verts),limits))
        ax.set_title(f'锁槽朝向 · {yaw}°' if not yaw else f'旋转 {yaw}°',fontproperties=ft,fontsize=16)
        ax.set_xlabel(f'最窄单边净余 {gap*1000:.1f} mm' if gap>=0 else f'超出槽口 {-gap*1000:.1f} mm',fontproperties=ft,fontsize=12)
        ax.set_xticks([]);ax.set_yticks([]);ax.set_aspect('equal');ax.set_xlim(-.84,.84);ax.set_ylim(-.84,.84)
        for spine in ax.spines.values():spine.set_visible(False)
    fig.suptitle('T-25 / A4    正式 A 篮槽口俯视',fontproperties=ft,fontsize=19)
    fig.tight_layout(rect=[0,.01,1,.93]);fig.savefig(out/'socket-fit.png',dpi=160);plt.close(fig)
    print(result['aligned_left_right_back_front_m'],result['base_to_socket_floor_m'],result['visual'])


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for arg in ('stone_path','socket','out'):p.add_argument(arg,type=Path)
    p.add_argument('--imported-vertices',type=Path,help='Point cloud from Unity after instantiating the FBX at identity')
    check(**vars(p.parse_args()))
