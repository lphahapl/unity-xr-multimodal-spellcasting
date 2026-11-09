"""Surgical Unity scene merge. `candidate` never publishes assets or the main scene.

candidate --report-dir Logs/exterior-polish-20261108/integration-v4
publish --merge-report <merge-check.json> --expected-scene-hash <sha256>
        --verification-report <parent JSON: passed:true, baseSha256, candidateSha256>

Only the parent may invoke publish after independent Unity validation/approval.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import subprocess

ROOT=Path(__file__).resolve().parents[3]
CLONE=Path('D:/Temp/NarutoVR-Exterior-20261108')
SCENE=ROOT/'Assets/Scene/Chapter01_Awakening.unity'
MERGE=ROOT/'Logs/exterior-polish-20261108/merge'
INSTANCE,TRANSFORM='9001711001','9001711002'
HEADER=re.compile(r'^--- !u!(\d+) &(-?\d+)[^\r\n]*\r?\n',re.M)
sha=lambda b:hashlib.sha256(b).hexdigest()

def require(condition,message):
    if not condition: raise ValueError(message)

def blocks(text):
    found=list(HEADER.finditer(text)); require(found,'No Unity YAML blocks')
    docs={}
    for i,m in enumerate(found):
        require(m[2] not in docs,'Duplicate YAML fileID '+m[2])
        docs[m[2]]=(m[1],text[m.start():found[i+1].start() if i+1<len(found) else len(text)])
    return text[:found[0].start()],docs

def parse_value(value):
    value=value.strip()
    if value.startswith('{'):
        try:return json.loads(value)
        except json.JSONDecodeError:
            return {key.strip():(int(v) if key.strip()=='fileID' else float(v)) for key,v in
                    (part.split(':',1) for part in value.strip('{}').split(','))}
    try:return float(value)
    except ValueError:return value

def equal(a,b):
    a,b=parse_value(a),parse_value(b)
    if isinstance(a,dict) and isinstance(b,dict):
        if set(a)=={'fileID'} and set(b)=={'fileID'}:return a==b
        return a.keys()==b.keys() and all(abs(a[k]-b[k])<=max(1e-5,abs(b[k])*1e-6) for k in a)
    if isinstance(a,(int,float)) and isinstance(b,(int,float)):
        return abs(a-b)<=max(1e-5,abs(b)*1e-6)
    return a==b

def number(v):
    require(isinstance(v,(float,int)),'Only numeric authored values are supported')
    return format(v,'.9g')

def rendered(value):
    parsed=parse_value(value)
    if isinstance(parsed,dict):return '{'+', '.join(k+': '+(str(v) if k=='fileID' else number(v)) for k,v in parsed.items())+'}'
    return number(parsed)

def field_span(body,path):
    """Locate a single scalar/inline-map leaf without reserializing its containing block."""
    start,end=0,len(body)
    for depth,key in enumerate(path.split('.')):
        indent=2+depth*2
        pattern=re.compile(r'^'+(' '*indent)+re.escape(key)+r':(?P<gap> *)(?P<value>[^\r\n]*)',re.M)
        matches=list(pattern.finditer(body,start,end));require(len(matches)==1,'Missing/duplicate field '+path)
        match=matches[0]
        if depth==len(path.split('.'))-1:return match.start('value'),match.end('value')
        require(not match['value'].strip(),'Expected nested YAML mapping '+key)
        start=match.end()
        later=re.search(r'^ {0,'+str(indent)+r'}\S',body[start:end],re.M)
        if later:end=start+later.start()
    raise AssertionError(path)

def patch_field(body,path,before,after):
    a,b=field_span(body,path)
    require(equal(body[a:b],before),'Before mismatch '+path+': '+body[a:b]+' != '+before)
    return body[:a]+rendered(after)+body[b:]

OVERRIDE=re.compile(r'^    - target: (?P<target>\{[^}]+\})\r?\n'
    r'      propertyPath: (?P<property>[^\r\n]+)\r?\n'
    r'      value:(?P<gap> *)(?P<value>[^\r\n]*)\r?\n'
    r'      objectReference: (?P<reference>\{[^}]+\})\r?\n',re.M)

def overrides(body,guid,fid,path):
    matches=[]
    for m in OVERRIDE.finditer(body):
        target=m['target']
        if (re.search(r'fileID: '+re.escape(str(fid))+r'(?:,|\})',target) and
            'guid: '+guid in target and m['property']==path):matches.append(m)
    require(len(matches)<=1,'Duplicate targeted prefab override '+str((guid,fid,path)))
    return matches

def asset_paths(guids):
    """Use ripgrep to avoid opening unrelated large/binary imported assets."""
    if not guids:return {}
    pattern='|'.join('guid: '+re.escape(g) for g in sorted(guids))
    proc=subprocess.run(['rg','-l',pattern,str(ROOT/'Assets'),'-g','*.meta'],capture_output=True,text=True,encoding='utf-8')
    require(proc.returncode in (0,1),'rg failed: '+proc.stderr)
    result={}
    for line in proc.stdout.splitlines():
        meta=Path(line)
        m=re.search(r'^guid: ([a-f0-9]{32})',meta.read_text(encoding='utf-8-sig'),re.M)
        if m and m[1] in guids:
            require(m[1] not in result,'Duplicate asset GUID '+m[1]);result[m[1]]=Path(str(meta)[:-5])
    require(set(result)==set(guids),'Cannot resolve source GUIDs '+str(set(guids)-set(result)))
    return result

def patch_override(body,change,reference,assets,clone,evidence,newline):
    guid,fid,path=change['prefabSourceGUID'],change['prefabSourceFileID'],change['propertyPath']
    found=overrides(body,guid,fid,path)
    if found:
        m=found[0]
        require(equal(m['value'],change['before']),'Prefab override before mismatch '+str((guid,fid,path)))
        require(m['reference']=='{fileID: 0}','Target is an object override, not scalar')
        evidence['beforeValidation']='existing override value'
        return body[:m.start('value')]+rendered(change['after'])+body[m.end('value'):]
    require(not overrides(reference,guid,fid,path),'Concurrent removal of an authored base override')
    # For inherited fields (including FBX), Unity's recorded before comes from the
    # authenticated authoring snapshot. Verify the same inheritance still applies.
    source=assets[guid]; peer=clone/source.relative_to(ROOT)
    require(peer.exists(),'Missing clone source asset '+str(peer))
    require(source.read_bytes()==peer.read_bytes(),'Inherited source asset changed '+str(source))
    main_meta=Path(str(source)+'.meta'); peer_meta=Path(str(peer)+'.meta')
    require(peer_meta.exists() and main_meta.read_bytes()==peer_meta.read_bytes(),
        'Inherited source importer changed '+str(source))
    source_ref=re.search(r'^  m_SourcePrefab: (.+)$',body,re.M)
    reference_ref=re.search(r'^  m_SourcePrefab: (.+)$',reference,re.M)
    require(source_ref and reference_ref and source_ref[1]==reference_ref[1],'Prefab source reference changed')
    evidence['beforeValidation']='recorded Unity before + authenticated snapshot + same inherited asset/importer'
    evidence['inheritedSourceAsset']=str(source.relative_to(ROOT))
    evidence['inheritedSourceSHA256']=sha(source.read_bytes())
    marker=re.search(r'^    m_RemovedComponents:',body,re.M)
    require(marker,'Missing prefab override end')
    if '    m_Modifications: []' in body:
        body=body.replace('    m_Modifications: []','    m_Modifications:',1)
        marker=re.search(r'^    m_RemovedComponents:',body,re.M)
    addition=('    - target: {fileID: '+str(fid)+', guid: '+guid+', type: 3}'+newline+
        '      propertyPath: '+path+newline+'      value: '+rendered(change['after'])+newline+
        '      objectReference: {fileID: 0}'+newline)
    return body[:marker.start()]+addition+body[marker.start():]

def make(args):
    report_dir=args.report_dir.resolve();clone=args.clone.resolve();scene=args.scene.resolve()
    out=(MERGE/report_dir.name);out.mkdir(parents=True,exist_ok=True)
    data_bytes=(report_dir/'scene-field-changes.json').read_bytes()
    data=json.loads(data_bytes.decode('utf-8-sig'))
    base=scene.read_bytes();prefix,docs=blocks(base.decode('utf-8'));original=dict(docs)
    reference_bytes=(report_dir/'source-scene.unity').read_bytes()
    require(sha(reference_bytes)==data['sourceSHA256'],'Authoring snapshot hash differs from report')
    _,reference=blocks(reference_bytes.decode('utf-8'))
    newline='\r\n' if b'\r\n' in base else '\n'
    actual=[c for c in data['changes'] if c['objectType']!='TransientPipelinePreviewSetting']
    assets=asset_paths({c['prefabSourceGUID'] for c in actual if c['prefabInstance']})
    changed=set();fields=[];targets=set()
    allowed={'MeshRenderer':('23',{'m_Enabled','m_CastShadows','m_ReceiveShadows'}),
        'Light':('108',{'m_Color','m_Intensity','m_Shadows.m_Type','m_Shadows.m_Strength','m_Shadows.m_Bias','m_Shadows.m_NormalBias'}),
        'Camera':('20',{'far clip plane'}),
        'RenderSettings':('104',{'m_AmbientSkyColor','m_AmbientEquatorColor','m_AmbientGroundColor','m_AmbientIntensity','m_AmbientMode',
            'm_LinearFogStart','m_LinearFogEnd','m_Fog','m_FogMode','m_FogDensity','m_FogColor','m_Sun'})}
    for c in actual:
        require(c['objectType'] in allowed,'Unsupported scene object type '+c['objectType'])
        expected_type,properties=allowed[c['objectType']]
        require(c['propertyPath'] in properties,'Unsupported property '+c['propertyPath'])
        if c['propertyPath']=='m_Sun':
            for value in [c['before'],c['after']]:
                parsed=parse_value(value)
                require(isinstance(parsed,dict) and set(parsed)=={'fileID'} and isinstance(parsed['fileID'],int),
                    'm_Sun must be an exact integer fileID reference')
        if c['objectType']=='Camera':
            require(all(isinstance(parse_value(c[v]),(int,float)) and parse_value(c[v])>0 for v in ['before','after']),
                'Camera far clip plane must be positive numeric')
        key=str(c['prefabInstanceFileID'] if c['prefabInstance'] else c['fileID'])
        require(key in docs and key in reference,'Missing authored object '+key)
        typ,body=docs[key];entry={k:c[k] for k in ['objectType','propertyPath','before','after']}
        entry['changedBlockID']=key
        signature=(key,c['prefabSourceGUID'],c['prefabSourceFileID'],c['propertyPath'])
        require(signature not in targets,'Repeated authored target '+str(signature));targets.add(signature)
        if c['prefabInstance']:
            require(typ=='1001','Expected PrefabInstance '+key)
            body=patch_override(body,c,reference[key][1],assets,clone,entry,newline)
            require(len(overrides(body,c['prefabSourceGUID'],c['prefabSourceFileID'],c['propertyPath']))==1,'Override not unique after patch')
        else:
            require(typ==expected_type,'Unexpected YAML type '+key)
            body=patch_field(body,c['propertyPath'],c['before'],c['after'])
            entry['beforeValidation']='serialized field before value'
        docs[key]=(typ,body);changed.add(key);fields.append(entry)
    require(INSTANCE not in docs and TRANSFORM not in docs,'New scene fileID collision')
    prefab_path=clone/data['newPrefabPath'];prefab_bytes=prefab_path.read_bytes()
    _,prefab=blocks(prefab_bytes.decode('utf-8'))
    tid=str(data['newPrefabRootFileID']);guid=data['newPrefabGUID']
    require(tid in prefab and prefab[tid][0]=='4','Missing new prefab root transform')
    require('  m_Father: {fileID: 0}' in prefab[tid][1],'New prefab transform is not a root')
    meta=Path(str(prefab_path)+'.meta').read_text(encoding='utf-8-sig')
    require('guid: '+guid in meta,'New prefab GUID mismatch')
    go=re.search(r'^  m_GameObject: \{fileID: (-?\d+)\}',prefab[tid][1],re.M)[1]
    roots=[k for k,(typ,_) in docs.items() if typ=='1660057539'];require(len(roots)==1,'Expected one SceneRoots block')
    rootid=roots[0];typ,body=docs[rootid]
    require('  m_Roots:' in body and body.endswith(newline),'Malformed SceneRoots')
    if '  m_Roots: []' in body:body=body.replace('  m_Roots: []','  m_Roots:',1)
    body+='  - {fileID: '+TRANSFORM+'}'+newline
    docs[rootid]=(typ,body);changed.add(rootid)
    addition=f'''--- !u!1001 &{INSTANCE}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: {go}, guid: {guid}, type: 3}}
      propertyPath: m_Name
      value: 首章外景细化
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}
--- !u!4 &{TRANSFORM} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {tid}, guid: {guid}, type: 3}}
  m_PrefabInstance: {{fileID: {INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
'''.replace('\n',newline)
    result=(prefix+''.join((addition if k==rootid else '')+body for k,(_,body) in docs.items())).encode('utf-8')
    _,after=blocks(result.decode('utf-8'))
    for key,(typ,body) in original.items():
        require(key in after,'Removed original block '+key)
        if key not in changed or typ=='114':require(after[key][1]==body,'Unrelated/original script block changed '+key)
    # Reverse each intended field edit; this proves unlisted fields inside modified
    # Light/RenderSettings/PrefabInstance blocks also retain their original bytes.
    for key in changed-{rootid}:
        permitted=[entry for entry in fields if entry['changedBlockID']==key]
        old=original[key][1];new=after[key][1]
        if original[key][0]=='1001':
            for c in [x for x in actual if x['prefabInstance'] and str(x['prefabInstanceFileID'])==key]:
                om=overrides(old,c['prefabSourceGUID'],c['prefabSourceFileID'],c['propertyPath'])
                nm=overrides(new,c['prefabSourceGUID'],c['prefabSourceFileID'],c['propertyPath'])[0]
                new=new[:nm.start()]+(om[0].group() if om else '')+new[nm.end():]
            if '    m_Modifications: []' in old:
                new=new.replace('    m_Modifications:'+newline,'    m_Modifications: []'+newline,1)
        else:
            for c in permitted:
                a,b=field_span(new,c['propertyPath']);oa,ob=field_span(old,c['propertyPath'])
                new=new[:a]+old[oa:ob]+new[b:]
        require(new==old,'Unlisted field bytes changed inside '+key)
    candidate=clone/'Assets/Scene/QA_ExteriorPolishMerged.unity'
    candidate.write_bytes(result)
    require(scene.read_bytes()==base,'Main scene changed during candidate merge')
    (out/'publication-base.unity').write_bytes(base)
    proof=dict(baseSha256=sha(base),candidateSha256=sha(result),scene=str(scene),candidate=str(candidate),
        reportDir=str(report_dir),authoringReportSha256=sha(data_bytes),sourceSnapshotSha256=sha(reference_bytes),
        allChangedIDs=sorted(changed|{INSTANCE,TRANSFORM},key=int),changedExistingIDs=sorted(changed,key=int),
        addedIDs=[INSTANCE,TRANSFORM],fieldChanges=fields,skippedTransientChanges=[c for c in data['changes'] if c['objectType']=='TransientPipelinePreviewSetting'],
        originalMonoBehaviourCount=sum(t=='114' for t,_ in original.values()),
        candidateSerializedMonoBehaviourCount=sum(t=='114' for t,_ in after.values()),
        allOriginalMonoBehavioursByteIdentical=True,allUnlistedBlocksByteIdentical=True,
        allUnlistedFieldsInChangedBlocksByteIdentical=True,newPrefabGUID=guid,newPrefabRootFileID=tid,
        newPrefabSHA256=sha(prefab_bytes),published=False)
    (out/'merge-check.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:proof[k] for k in ['baseSha256','candidateSha256','allChangedIDs','originalMonoBehaviourCount','published']},indent=2))
    print('REPORT',out/'merge-check.json')

def publish(args):
    report=json.loads(args.merge_report.read_text(encoding='utf-8-sig'))
    verification=json.loads(args.verification_report.read_text(encoding='utf-8-sig'))
    require(verification.get('passed') is True,'Parent verification did not pass')
    for key in ['baseSha256','candidateSha256']:
        require(verification.get(key)==report[key],'Verification is not bound to '+key)
    require(args.expected_scene_hash==report['baseSha256'],'Explicit parent scene hash does not match merge')
    scene,candidate=Path(report['scene']),Path(report['candidate'])
    require(scene.resolve()==SCENE.resolve(),'Unexpected publication scene')
    require(sha(scene.read_bytes())==report['baseSha256'],'Main scene changed; rebuild and revalidate candidate')
    payload=candidate.read_bytes();require(sha(payload)==report['candidateSha256'],'Candidate changed after verification')
    staged=args.merge_report.parent/'publication-staged.unity';staged.write_bytes(payload)
    require(sha(scene.read_bytes())==args.expected_scene_hash,'Main scene changed before publish')
    os.replace(staged,scene)
    require(sha(scene.read_bytes())==report['candidateSha256'],'Published scene hash mismatch')
    result=dict(report,published=True,publishedSha256=sha(scene.read_bytes()),verificationReport=str(args.verification_report))
    (args.merge_report.parent/'publication.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Published scene only. No assets copied; scene .meta preserved.')

def main():
    parser=argparse.ArgumentParser(description=__doc__);sub=parser.add_subparsers(dest='mode',required=True)
    p=sub.add_parser('candidate');p.add_argument('--report-dir',type=Path,required=True)
    p.add_argument('--clone',type=Path,default=CLONE);p.add_argument('--scene',type=Path,default=SCENE)
    p=sub.add_parser('publish');p.add_argument('--merge-report',type=Path,required=True)
    p.add_argument('--expected-scene-hash',required=True);p.add_argument('--verification-report',type=Path,required=True)
    args=parser.parse_args();{'candidate':make,'publish':publish}[args.mode](args)

if __name__=='__main__':main()
