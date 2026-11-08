"""Check the movement axis of every tile root and selectable mesh part."""
from pathlib import Path
import re, sys, json, yaml
import numpy as np
from scipy.spatial.transform import Rotation

path=Path(sys.argv[1] if len(sys.argv)>1 else r'D:\unity_training\Runnner&&Lucy\Assets\ModularBoard\Prefabs\Board_10m_36Tiles.prefab')
text=path.read_text(encoding='utf-8-sig')
docs={int(m.group(1)):yaml.safe_load(m.group(2)) for m in re.finditer(r'^--- !u!\d+ &(-?\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
names={k:v['GameObject']['m_Name'] for k,v in docs.items() if 'GameObject' in v}
transforms={k:v['Transform'] for k,v in docs.items() if 'Transform' in v}
mesh_objects={v['MeshFilter']['m_GameObject']['fileID'] for v in docs.values() if 'MeshFilter' in v}
collider_objects={v['BoxCollider']['m_GameObject']['fileID'] for v in docs.values() if 'BoxCollider' in v}
cache={}
def vec(d,keys='xyz'): return np.array([d[k] for k in keys],dtype=float)
def world(k):
    if not k:return np.eye(4)
    if k in cache:return cache[k]
    t=transforms[k]; matrix=np.eye(4)
    matrix[:3,:3]=Rotation.from_quat(vec(t['m_LocalRotation'],'xyzw')).as_matrix()@np.diag(vec(t['m_LocalScale']))
    matrix[:3,3]=vec(t['m_LocalPosition'])
    cache[k]=world(t['m_Father']['fileID'])@matrix
    return cache[k]
tiles=sorted([(names[t['m_GameObject']['fileID']],k) for k,t in transforms.items() if re.match(r'^Tile_\d{2}_',names[t['m_GameObject']['fileID']])])
assert len(tiles)==36
failures=[]; corners=[]; parts=0
for i,(name,key) in enumerate(tiles):
    delta=world(tiles[(i+1)%36][1])[:3,3]-world(key)[:3,3]
    direction=delta/np.linalg.norm(delta)
    def check(k,label):
        w=world(k); right=w[:3,0]/np.linalg.norm(w[:3,0]); up=w[:3,1]/np.linalg.norm(w[:3,1])
        dot=float(np.dot(right,direction)); up_dot=float(np.dot(up,[0,1,0]))
        if dot<.99999 or up_dot<.99999: failures.append({'object':label,'right_dot':round(dot,6),'up_dot':round(up_dot,6)})
        return round(dot,6)
    root_dot=check(key,name)
    if transforms[key]['m_GameObject']['fileID'] not in collider_objects:
        failures.append({'object':name,'error':'Collider is not on the model tile root'})
    corner_parts=[]
    for child,t in transforms.items():
        if t['m_Father']['fileID']==key and t['m_GameObject']['fileID'] in mesh_objects:
            part_name=names[t['m_GameObject']['fileID']]; parts+=1
            dot=check(child,name+'/'+part_name)
            if i in (0,9,18,27):corner_parts.append({'name':part_name,'right_dot':dot})
    if i in (0,9,18,27):corners.append({'tile':name,'root_right_dot':root_dot,'parts':corner_parts})
report={'prefab':str(path),'tiles':36,'mesh_parts':parts,'corners':corners,'failure_count':len(failures),'failures':failures,'pass':not failures}
if 'Route_Colliders' in names.values():
    failures.append({'object':'Route_Colliders','error':'Unnecessary separate collision hierarchy remains'})
report['failure_count']=len(failures); report['pass']=not failures
output=Path(__file__).resolve().parent/('tile-part-axes-pass.json' if not failures else 'tile-part-axes-fail.json')
output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='failures'},ensure_ascii=False,indent=2))
sys.exit(1 if failures else 0)
