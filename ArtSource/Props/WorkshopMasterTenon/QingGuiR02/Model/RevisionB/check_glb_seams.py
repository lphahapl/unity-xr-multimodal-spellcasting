"""Double-precision projected area evidence; never edits any mesh or export."""
import json,numpy as np
from pathlib import Path
P=Path(__file__).resolve().parent;LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'
cold=json.loads((LOG/'cold-import.json').read_text('utf-8'))
def cross(a,b):return float(a[0]*b[1]-a[1]*b[0])
def area(poly):
    if len(poly)<3:return 0.
    origin=poly[0]
    return abs(sum(cross(a-origin,b-origin) for a,b in zip(poly,poly[1:]+poly[:1])))*.5
records=[]
for e in cold['QingGuiR02B.glb']['objects'][0]['export_seam_evidence']:
    a,b=np.array(e['coordinates'],dtype=np.float64);n=np.cross(a[1]-a[0],a[2]-a[0]);n/=np.linalg.norm(n)
    drop=int(np.argmax(abs(n)));keep=[i for i in range(3) if i!=drop];aa=[p[keep] for p in a];bb=[p[keep] for p in b]
    if cross(bb[1]-bb[0],bb[2]-bb[0])<0:bb.reverse()
    poly=aa[:]
    for u,v in zip(bb,bb[1:]+bb[:1]):
        result=[]
        if not poly:break
        for s,t in zip(poly,poly[1:]+poly[:1]):
            ds=cross(v-u,s-u);dt=cross(v-u,t-u);ins=ds>=0;inte=dt>=0
            if ins!=inte:result.append(s+(t-s)*ds/(ds-dt))
            if inte:result.append(t)
        poly=result
    shared=[p for p in a if any(np.array_equal(p,q) for q in b)]
    records.append({**e,'exact_shared_position_count':len(shared),'projected_overlap_area_m2':area(poly),'projected_area_a_m2':area(aa),'projected_area_b_m2':area(bb),'b_plane_distances_m':[float(np.dot(p-a[0],n)) for p in b],'projected_axis_dropped':drop})
report={'runtime_eps_m':1e-5,'diagnostic_weld_m':1e-9,'export_unchanged':True,'method':'Float64 axis projection and polygon clipping with zero half-plane slack; exact binary-coordinate shared edge test. Original QA and adjacency-normalized QA are in cold-import.json.','pairs':records}
(LOG/'glb-seam-projected-overlap.json').write_text(json.dumps(report,indent=2),'utf-8');print(json.dumps(report,indent=2))
