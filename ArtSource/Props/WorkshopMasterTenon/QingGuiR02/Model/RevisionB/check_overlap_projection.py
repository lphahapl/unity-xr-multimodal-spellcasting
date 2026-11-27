import ast,json,numpy as np
from pathlib import Path
P=Path(__file__).resolve().parent;LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'
line=next(l for l in (LOG/'source-overlap-diagnostic.log').read_text('utf-8').splitlines() if l.startswith('EXACT_ZF_DIAGNOSTIC '));pairs=ast.literal_eval(line.split(' ',1)[1])
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def area(poly):return abs(sum(cross(a,b) for a,b in zip(poly,poly[1:]+poly[:1])))*.5 if len(poly)>2 else 0.
records=[]
for ia,ib,a,b in pairs:
    a=np.array(a);b=np.array(b);n=np.cross(a[1]-a[0],a[2]-a[0]);n/=np.linalg.norm(n);drop=int(np.argmax(abs(n)));keep=[i for i in range(3) if i!=drop];aa=[p[keep] for p in a];bb=[p[keep] for p in b]
    if cross(bb[1]-bb[0],bb[2]-bb[0])<0:bb.reverse()
    poly=aa[:]
    for u,v in zip(bb,bb[1:]+bb[:1]):
        result=[]
        if not poly:break
        for s,e in zip(poly,poly[1:]+poly[:1]):
            ds=cross(v-u,s-u);de=cross(v-u,e-u);ins=ds>=-1e-20;ine=de>=-1e-20
            if ins!=ine:result.append(s+(e-s)*ds/(ds-de))
            if ine:result.append(e)
        poly=result
    records.append({'triangles':[ia,ib],'projection_axis_dropped':drop,'projected_overlap_area_m2':area(poly),'a_projected_area_m2':area(aa),'b_projected_area_m2':area(bb),'b_plane_distances_m':[float(np.dot(p-a[0],n)) for p in b]})
(LOG/'source-overlap-exact-proof.json').write_text(json.dumps(records,indent=2),'utf-8');print(json.dumps(records,indent=2))
