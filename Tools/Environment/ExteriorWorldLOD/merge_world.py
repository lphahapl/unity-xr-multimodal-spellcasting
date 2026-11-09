"""Patch only explicitly recorded existing renderer fields, retaining every original scene block."""
from pathlib import Path
import json,hashlib,importlib.util
R=Path('D:/UnityProjects/NarutoVR');C=Path('D:/Temp/NarutoVR-Exterior-20261108')
L=R/'Logs/exterior-world-lod-20261109/integration'
spec=importlib.util.spec_from_file_location('surgical',R/'Tools/Environment/ExteriorPolish/merge_scene.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
sha=lambda b:hashlib.sha256(b).hexdigest()
scene=R/'Assets/Scene/Chapter01_Awakening.unity';base=scene.read_bytes()
data=json.loads((L/'changes.json').read_text(encoding='utf-8-sig'))
assert sha(base)==data['sourceSHA256']==sha((L/'source-scene.unity').read_bytes())
prefix,docs=module.blocks(base.decode('utf-8'));original=dict(docs)
changes=data['changes'];assets=module.asset_paths({c['prefabSourceGUID'] for c in changes if c['prefabInstance']})
evidence=[];changed=set()
for c in changes:
    assert c['objectType'] in ['MeshRenderer','BoxCollider'] and c['propertyPath']=='m_Enabled' and c['after']=='0'
    if c['objectType']=='BoxCollider':
        assert c['path'] in ['第一章 · 初醒校验室/关外石坪/左收边碰撞','第一章 · 初醒校验室/关外石坪/右收边碰撞']
    key=str(c['prefabInstanceFileID'] if c['prefabInstance'] else c['fileID']);typ,body=docs[key]
    entry={'id':key,'path':c['path'],'property':c['propertyPath']}
    if c['prefabInstance']:
        assert typ=='1001';body=module.patch_override(body,c,original[key][1],assets,C,entry,'\n' if b'\r\n' not in base else '\r\n')
    else:
        assert typ==('23' if c['objectType']=='MeshRenderer' else '65');body=module.patch_field(body,c['propertyPath'],c['before'],c['after'])
    docs[key]=(typ,body);changed.add(key);evidence.append(entry)
# The restored source has an existing audio MonoBehaviour with no reciprocal
# GameObject component entry. Unity repairs it on load and reports a build error.
# Preserve its entire serialized payload; add only the missing owner reference.
owner='436909642'; component='991080001'; typ,body=docs[owner]
assert typ=='1' and docs[component][0]=='114'
assert 'm_GameObject: {fileID: '+owner+'}' in docs[component][1]
assert 'NarutoVR.SealCombat.SkillAudioPlayer' in docs[component][1]
assert ('component: {fileID: '+component+'}') not in body
nl='\r\n' if '\r\n' in body else '\n'
anchor='  - component: {fileID: 901000001}'+nl
assert body.count(anchor)==1
docs[owner]=(typ,body.replace(anchor,anchor+'  - component: {fileID: '+component+'}'+nl,1))
changed.add(owner)
evidence.append({'id':owner,'path':'技能训练台','property':'m_Component','addedExistingComponent':component,
                 'reason':'Restore reciprocal reference to existing SkillAudioPlayer; payload unchanged'})
result=(prefix+''.join(body for _,body in docs.values())).encode('utf-8')
for key,(typ,body) in original.items():
    if key not in changed or typ=='114':assert body==docs[key][1],key
assert scene.read_bytes()==base
candidate=C/'Assets/Scene/QA_WorldExteriorMerged.unity';candidate.write_bytes(result)
proof={'baseSha256':sha(base),'candidateSha256':sha(result),'sourceScene':str(scene),'candidate':str(candidate),'fields':evidence,'originalBlocks':len(original),'originalMonoBehavioursByteIdentical':True,'originalMonoBehaviours':sum(t=='114' for t,b in original.values()),'newRootAdded':False,'modifiedPrefab':'Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab','prefabSHA256':sha((C/'Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab').read_bytes())}
(L/'merge-proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(proof,ensure_ascii=False))
