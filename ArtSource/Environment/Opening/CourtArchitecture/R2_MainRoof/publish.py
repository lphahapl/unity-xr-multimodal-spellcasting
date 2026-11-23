"""Publish from a checked stage, or verify the shipping pair and write the exact inventory."""
from datetime import datetime
from pathlib import Path
from tempfile import NamedTemporaryFile
import hashlib
import json
import re
import sys
import bpy
import numpy as np
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build as b

ROOT=b.roof.ROOT
FBX=b.DEST/(b.MODEL+'.fbx')
PREFAB=FBX.with_suffix('.prefab')
BASE=json.loads((b.HERE/'baseline.json').read_text())
SHARED='bebe04e1d6ee2ec41a89ada758b4a450'
IDS=['-3619996037657576586','-3887925568495935114','-5332546612888290761']

def relative(p):
    return p.relative_to(ROOT).as_posix()

def guid(path):
    return re.search('^guid: (.+)',Path(str(path)+'.meta').read_text(),re.M)[1]

def digest(blob):
    return hashlib.sha256(blob).hexdigest()

def checked_files():
    for name,value in BASE.items():
        path=ROOT/name
        if path not in (FBX,PREFAB):assert b.sha(path)==value, path

def move_sockets(blob,undo=False):
    # Match only shared-instance transform overrides. Preserve original line endings,
    # hierarchy, LODGroup, mesh references and unrelated fields byte for byte.
    result=blob
    for axis,old,new,count in [('x','5.105',f'{b.REAR:.3f}',1),
                                ('x','-5.105',f'{-b.REAR:.3f}',1),
                                ('y','4.267',f'{b.SEAT:.3f}',2)]:
        start,end=(new,old) if undo else (old,new)
        pat=(r'(target: \{fileID: 569573555748551712, guid: '+SHARED+
             r', type: 3\}\r?\n      propertyPath: m_LocalPosition\.'+axis+
             r'\r?\n      value: )'+re.escape(start)+r'(?=\r?\n)')
        result,n=re.subn(pat.encode(),lambda m:m[1]+end.encode(),result)
        assert n==count,(axis,n,start)
    return result

def replace(path,data):
    with NamedTemporaryFile(dir=b.HERE,delete=False) as handle:
        handle.write(data)
        temp=Path(handle.name)
    try:temp.replace(path)
    finally:temp.unlink(missing_ok=True)

def report_data():
    qa=json.loads((b.HERE/'Roof_QA.json').read_text())
    assert qa['passed'] and all('all_cap_LODs' in v['measurements'] for v in qa['tiers'].values())
    return qa

def publish():
    qa=report_data()
    checked_files()
    stage=b.HERE/FBX.name
    assert b.sha(stage)==qa['fbx_sha256']
    original=PREFAB.read_bytes()
    if digest(original)==BASE[relative(PREFAB)]:
        new=move_sockets(original)
    else:
        assert digest(move_sockets(original,undo=True))==BASE[relative(PREFAB)]
        new=original
    allowed=[qa['fbx_sha256'],BASE[relative(FBX)]]
    prior=b.HERE/'delivery.json'
    if prior.exists():
        allowed.append(json.loads(prior.read_text())['runtime_files'][relative(FBX)]['after_sha256'])
    assert b.sha(FBX) in allowed,'Unexpected current runtime FBX'
    if b.sha(FBX)!=qa['fbx_sha256']:replace(FBX,stage.read_bytes())
    if original!=new:replace(PREFAB,new)
    assert digest(move_sockets(PREFAB.read_bytes(),undo=True))==BASE[relative(PREFAB)]
    print('Published',relative(FBX),relative(PREFAB))

def inventory():
    qa=report_data()
    assert qa['scope']=='Published roof FBX' and b.sha(FBX)==qa['fbx_sha256']
    checked_files()
    raw=PREFAB.read_bytes()
    assert digest(move_sockets(raw,undo=True))==BASE[relative(PREFAB)]
    prefab=raw.decode('utf-8-sig')
    ids=b.roof.fbx_ids(FBX)
    orig=json.loads((b.A1/'Review'/(b.MODEL+'_identity.json')).read_text())['fbx']
    assert [list(v) for v in ids]==orig
    bpy.ops.wm.open_mainfile(filepath=str(b.A1/(b.MODEL+'_A1.blend')))
    old_footprints={}
    for ob in bpy.context.scene.objects:
        p=np.array([v.co for v in ob.data.vertices])
        old_footprints[ob.name]=np.ptp(p,axis=0)[:2]
    bpy.ops.wm.open_mainfile(filepath=str(b.HERE/'CourtMainRoof_B2.blend'))
    packed={}
    for image in bpy.data.images:
        name=image.name.split('.png')[0]+'.png'
        assert image.packed_file and digest(image.packed_file.data)==b.sha(b.DEST/name)
        packed[name]=b.sha(b.DEST/name)
    assert len(packed)==9
    mat_guids=[]
    report_tiers=[]
    for i,row in enumerate(qa['tiers'].values()):
        tag=b.MODEL+'_Tier'+str(i)
        mat_id=guid(b.DEST/(tag+'.mat'))
        mat_guids.append(mat_id)
        assert f'm_Mesh: {{fileID: {IDS[i]}, guid: {guid(FBX)}, type: 3}}' in prefab
        assert mat_id in prefab and mat_id in Path(str(FBX)+'.meta').read_text()
        assert row['triangles']==b.BUDGET[i]
        low,high=row['bounds_blender']
        footprint=np.subtract(high,low)[:2]
        assert np.max(abs(footprint-old_footprints[tag]))<.00002
        report_tiers.append(dict(tier=i,triangles=row['triangles'],unity_mesh_fileID=IDS[i],
                            material_guid=mat_id,footprint_m=footprint.round(5).tolist(),
                            original_A1_footprint_m=old_footprints[tag].round(5).tolist()))
    assert prefab.count('m_SourcePrefab: {fileID: 100100000, guid: '+SHARED)==2
    sockets=[]
    for block in re.split(r'^--- !u!1001 ',prefab,flags=re.M)[1:]:
        props=dict(re.findall(r'propertyPath: (m_Local\S+)\s+value: (\S+)',block))
        xyz=[float(props['m_LocalPosition.'+axis]) for axis in 'xyz']
        sign=1 if xyz[0]>0 else -1
        assert xyz==[round(sign*b.REAR,3),round(b.SEAT,3),0]
        assert [float(props.get('m_LocalScale.'+a,1)) for a in 'xyz']==[1.6,1.56,1.]
        quat=[float(props['m_LocalRotation.'+a]) for a in 'xyzw']
        assert quat==[0,sign*.7071068,0,.7071068]
        sockets.append(dict(position_xyz=xyz,outward_axis=[sign,0,0],scale_xyz=[1.6,1.56,1],quaternion_xyzw=quat,
                            pivot='rear opening, base level in prefab-local metres'))
    source_files=sorted(p for p in b.HERE.iterdir() if p.is_file() and p.name!='delivery.json')
    runtime={relative(p):dict(before_sha256=BASE[relative(p)],after_sha256=b.sha(p),guid=guid(p)) for p in (FBX,PREFAB)}
    out=dict(asset='R2 B2 main hall roof',time=datetime.now().astimezone().isoformat(timespec='seconds'),
        runtime_files=runtime,tiers=report_tiers,FBX_object_ids=ids,
        prefab_changes='Only the X and Y overrides of the two shared ceramic instances changed. Reversing these four scalars reproduces the original prefab byte-for-byte.',
        prefab_screen_heights=[float(v) for v in re.findall(r'screenRelativeHeight: (\S+)',prefab)],
        collider={'prefab_collider_components':len(re.findall(r'^\w*Collider:',prefab,re.M)),'model_import_add_colliders':0},
        nested_shared_guid=SHARED,nested_sockets=sockets,editable_blend_packed_maps=packed,
        rebuild='Run build.py, check.py, publish.py -- --publish, check.py -- --published, views.py -- --published, views.py -- --published --topology, then publish.py -- --verify using Blender background mode. The builder reads the original A1 version.',
        nominal_eave_rectangle_m=[2*b.X,2*b.Y],eave_pan_height_m=b.EAVE,gable_face_x_m=b.WALL,
        sill_m=b.SILL,crown_pan_m=b.CROWN,outer_verge_x_m=b.VERGE,
        validation=relative(b.HERE/'Roof_QA.json'),unity_runtime='reserved for parent session',
        warnings_interpretation='Surface UVs share the validated trim sheet. Separate pieces retain coincident boundary vertices and hidden opposing triangles at abutments. Check individual tier counts in Roof_QA.json.',
        unchanged_runtime_and_shared_files={name:val for name,val in BASE.items() if name not in runtime},
        source_and_QA={relative(p):b.sha(p) for p in source_files},inventory_self=relative(b.HERE/'delivery.json'))
    assert 'addColliders: 0' in Path(str(FBX)+'.meta').read_text()
    (b.HERE/'delivery.json').write_text(json.dumps(out,indent=2),encoding='utf8')
    print('Verified runtime IDs, material links, 4 prefab scalars, all protected file hashes and',len(source_files),'source/review files')

if __name__=='__main__':
    if '--publish' in sys.argv:publish()
    elif '--verify' in sys.argv:inventory()
    else:raise SystemExit('Use -- --publish or -- --verify')
