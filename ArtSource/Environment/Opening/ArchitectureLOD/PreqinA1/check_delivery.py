"""Read back published buffers and scene -> prefab -> LODGroup -> Renderer -> Mesh links."""
from pathlib import Path
import hashlib,json,re,csv,sys
import numpy as np
from PIL import Image

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
ART=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD'
PREF=ROOT/'Assets/Art/Environment/Opening/MountainPolish/EnvironmentPolish.prefab'
HIST=Path('D:/Temp/Chapter01PhotoArt-1795356116829')
def guid(f):return re.search(r'^guid: (\w+)', Path(str(f)+'.meta').read_text(),re.M)[1]
def blocks(s):return [(int(k),i,b) for k,i,b in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',s,re.M|re.S)]
def ref(s,key):return re.search(key+r': \{fileID: (-?\d+)',s)[1]
def field(s,key):return re.search(r'^  '+key+r': (.+)$',s,re.M)[1]
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
def validate():
    expected=json.loads((HERE/'mesh-report.json').read_text())
    qa=json.loads((HERE/'geometry-qa.json').read_text())
    meshes={};filters={};renderers={};transform={};parents={};objects={};lods={};collider_uses=[]
    mat=ART/'Architecture_Atlas.mat';matg=guid(mat);tex={}
    for role in ['BaseColor','Normal','Mask']:
        file=ART/'Textures'/('Architecture_'+role+'.png');meta=Path(str(file)+'.meta').read_text()
        assert guid(file) in mat.read_text() and Image.open(file).size==(2048,2048)
        for setting in ('enableMipMap: 1','wrapU: 0','buildTarget: Android','textureFormat: 50','overridden: 1'):assert setting in meta
        assert ('sRGBTexture: '+str(int(role=='BaseColor'))) in meta
        if role=='Normal':assert 'textureType: 1' in meta
        tex[role]=dict(guid=guid(file),sha256=sha(file))
    for f in sorted((ART/'Meshes').glob('*.asset')):
        s=f.read_text()
        assert field(s,'m_Name')==f.stem and '--- !u!43 &4300000' in s and field(s,'m_IndexFormat')=='0'
        vertex=np.frombuffer(bytes.fromhex(re.search('_typelessdata: (.+)',s)[1]),'<f4').reshape(-1,12)
        tri=np.frombuffer(bytes.fromhex(field(s,'m_IndexBuffer')),'<u2').reshape(-1,3)
        assert np.isfinite(vertex).all() and tri.max()<len(vertex)
        assert vertex.nbytes==int(re.search(r'm_DataSize: (\d+)',s)[1])
        assert len(tri)*3==int(re.search(r'indexCount: (\d+)',s)[1])
        assert len(vertex)==int(re.search(r'm_VertexCount: (\d+)',s)[1])
        for v in (vertex[:,3:6],vertex[:,6:9]):assert np.abs(np.linalg.norm(v,axis=1)-1).max()<.002
        assert np.allclose(np.abs(vertex[:,9]),1) and np.all(vertex[:,11]>=0) and np.all(vertex[:,11]<=1)
        pts=vertex[tri,:3];assert np.linalg.norm(np.cross(pts[:,1]-pts[:,0],pts[:,2]-pts[:,0]),axis=1).min()>1e-9
        assert len(tri)==expected[f.stem]['triangles']==qa[f.stem]['objects'][f.stem]['tris'] and qa[f.stem]['pass']
        meshes[guid(f)]=dict(name=f.stem,asset=f.relative_to(ROOT).as_posix(),mesh_guid=guid(f),mesh_fileID='4300000',sha256=sha(f),triangles=len(tri))
    for k,i,b in blocks(PREF.read_text()):
        if 'm_PrefabInstance:' in b and 'm_ObjectHideFlags:' not in b:continue
        if k==1:
            name=field(b,'m_Name')
            if name.startswith('"'):name=name[1:-1].encode('ascii').decode('unicode_escape')
            objects[i]=(name,field(b,'m_IsActive'))
        if k==4:transform[i]=ref(b,'m_GameObject');parents[transform[i]]=ref(b,'m_Father')
        if k==23:renderers[ref(b,'m_GameObject')]=(i,b)
        if k==33:
            match=re.search(r'm_Mesh: \{fileID: 4300000, guid: (\w+)',b)
            if match and match[1] in meshes:filters[ref(b,'m_GameObject')]=(i,match[1])
        if k==205:
            for h,r in re.findall(r'screenRelativeHeight: ([.\d]+).*?renderer: \{fileID: (\d+)',b,re.S):lods[r]=(i,float(h))
        if k==64 and any(g in b for g in meshes):collider_uses.append(i)
    assert len(filters)==18 and not collider_uses
    rows=[]
    for sc in ('Chapter01_Awakening','Chapter01_Courtyard'):
        scene=(ROOT/'Assets/Scene'/(sc+'.unity')).read_text()
        inst=[(i,b) for k,i,b in blocks(scene) if k==1001 and ('m_SourcePrefab: {fileID: 100100000, guid: '+guid(PREF)) in b]
        assert len(inst)==1
        sid,inst=inst[0]
        ov={(i,k):v for i,k,v in re.findall(r'target: \{fileID: (\d+), guid: '+guid(PREF)+r', type: 3\}\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)',inst)}
        history={r[3]:r[0] for r in csv.reader((HIST/(sc+'.tsv')).open(encoding='utf-8-sig'),delimiter='\t') if len(r)>=6 and r[5]=='True'}
        for go,(fid,g) in filters.items():
            rid,ren=renderers[go];assert matg in ren and ren.count('fileID: 2100000')==1
            assert not any(key[0] in (fid,rid) and (key[1]=='m_Mesh' or key[1].startswith('m_Materials')) for key in ov)
            current=go;chain=[];enabled=ov.get((rid,'m_Enabled'),field(ren,'m_Enabled'))=='1'
            while current:
                name,on=objects[current];chain.append(name)
                enabled &= ov.get((current,'m_IsActive'),on)=='1'
                current=transform.get(parents.get(current))
            info=meshes[g];historic=history.get(info['name'],'');assert bool(historic)==enabled,(sc,info['name'],enabled,historic)
            group,h=lods[rid]
            rows.append(dict(scene=sc,active=int(enabled),prefab_instance=sid,prefab_guid=guid(PREF),path='/'.join(chain[::-1]),gameObject=go,meshFilter=fid,meshRenderer=rid,lodGroup=group,transition=h,**info,material_slot=0,material_guid=matg,material_fileID='2100000',tsv_path=historic))
    with (HERE/'component-map.tsv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]),delimiter='\t');w.writeheader();w.writerows(rows)
    counts={s:sum(r['active'] for r in rows if r['scene']==s) for s in {r['scene'] for r in rows}}
    totals=[sum(v['triangles'] for v in meshes.values() if v['name'].endswith(str(i))) for i in range(3)]
    report=dict(tool='Offline Unity YAML and binary buffer readback; Blender 5.2.2 LTS',active_lod_renderers=counts,mesh_fileID=4300000,draw_slots_per_renderer=1,active_material=matg,texture_maps=tex,architecture_lod_tris=totals,with_three_A07_near=totals[0]+3*3400,mesh_collider_refs_in_source_prefab=collider_uses,accepted_QA_warnings=['UV0 intentionally repeats the shared trim sheet.','Local minimum Z follows the original terrain contacts below the assembly pivot.'],texture_source='ArtSource/Environment/SuspendedPath/Surfaces/Workshop/sources.json (CC0)',unity_runtime='Reserved to parent session')
    (HERE/'readback.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(counts,totals)
if __name__=='__main__':validate()

