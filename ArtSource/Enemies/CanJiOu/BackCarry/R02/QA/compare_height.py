"""Compare matched R01/R02 phases in Unity Y-up, actor-local metre captures."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np


def read(capture):
    m=json.loads((capture/'manifest.json').read_text(encoding='utf-8-sig'))
    b=np.fromfile(capture/'bones.f32','<f4').reshape(m['frames'],m['bones'],7).astype(float)
    if m['frames']!=49 or abs(m['clipLength']-1.6)>.001:
        raise ValueError('Expected the matching, 49-sample 1.6 s production cycle')
    p={name.split(':')[-1]:b[:,i,:3] for i,name in enumerate(m['boneNames'])}
    knees={}
    for side in ('Left','Right'):
        a=p[side+'UpLeg']-p[side+'Leg']
        c=p[side+'Foot']-p[side+'Leg']
        cosine=np.sum(a*c,axis=1)/(np.linalg.norm(a,axis=1)*np.linalg.norm(c,axis=1))
        knees[side]=180-np.degrees(np.arccos(np.clip(cosine,-1,1)))
    values={'hips_mm':1000*p['Hips'][:,1],'head_mm':1000*p['Head'][:,1],
            'shoulders_mm':500*(p['LeftShoulder'][:,1]+p['RightShoulder'][:,1]),
            'left_knee_flex_deg':knees['Left'],'right_knee_flex_deg':knees['Right']}
    neck=p['Neck']-p['Hips']
    values['hip_to_neck_forward_lean_deg']=np.degrees(np.arctan2(neck[:,2],neck[:,1]))
    return {name:arr[:-1] for name,arr in values.items()}


def compare(r01,r02,out):
    baseline=read(r01)
    revised=read(r02)
    stats={}
    for name,old in baseline.items():
        new=revised[name]
        delta=new-old
        stats[name]={'r01_mean':float(old.mean()),'r02_mean':float(new.mean()),
                     'matched_min_change':float(delta.min()),'matched_max_change':float(delta.max()),
                     'mean_change':float(delta.mean())}
    hashes={t:hashlib.sha256((p/'bones.f32').read_bytes()).hexdigest() for t,p in [('r01',r01),('r02',r02)]}
    payload={'bone_stream_sha256':hashes,'phases':'48 unique samples, loop endpoint omitted','metrics':stats}
    out.write_text(json.dumps(payload,indent=2)+'\n',encoding='utf8')
    return payload


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('r01','r02','out'):parser.add_argument(name,type=Path)
    print(json.dumps(compare(**vars(parser.parse_args())),indent=2))
