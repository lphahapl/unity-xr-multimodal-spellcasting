"""Prove every Cube record against its source component pair and original transform chain.
String identifiers avoid losing 64-bit prefab local IDs in JavaScript/JSON consumers.
"""
import csv,json,re,sys,hashlib
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
sys.dont_write_bytecode=True
from paths import *
DUMP=Path('D:/Temp/NarutoChapter01-A1-ShellReview')
PREF=ART.parent/'MountainPolish/EnvironmentPolish.prefab'
BY_ID={'532637787':'InnerGateStrap','1390359402':'InnerGateStrap','794158063':'InnerGateStrap','920146296':'OuterGateStrap','1012601844':'OuterGateStrap','1364962065':'OuterGateStrap','319964002':'GaugeFrameRail','902963025':'GaugeFrameRail','1222546537':'GaugeFrameStile','1510990424':'GaugeFrameStile','454558206':'GaugePost','1301679345':'WayfindingInlay','303298073':'CheckGateLeaf','770077089':'CheckGateStrap','1622662084':'CheckGateStrap','551458604':'HighImpactScar','9107100705686059392':'SideGateDais'}
def parse(path):return {i:(int(k),b) for k,i,b in re.findall(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',path.read_text(encoding='utf-8-sig'),re.M|re.S)}
def field(b,key):return re.search(r'^  '+key+r': (.+)$',b,re.M)[1]
def ref(b,k):return re.search(r'\{fileID: (-?\d+)',field(b,k))[1]
def floats(s):return [float(f) for f in re.findall(r'[-+]?(?:\d*\.\d+|\d+)(?:e[-+]?\d+)?',s,re.I)]
def transform(b):
    loc,quat,sc=[floats(field(b,k)) for k in ('m_LocalPosition','m_LocalRotation','m_LocalScale')]
    matrix=np.eye(4);matrix[:3,3]=loc;matrix[:3,:3]=Rotation.from_quat(quat).as_matrix() @ np.diag(sc)
    return matrix
def sha(f):return hashlib.sha256(f.read_bytes()).hexdigest()
def record(b):return {k:floats(field(b,k)) for k in ('m_LocalPosition','m_LocalRotation','m_LocalScale')}
def exported(t):
    name,ref=t.rsplit('@',1);g,i=ref.split(':');return dict(name=name,guid=g,fileID=i)
def main():
    lib=json.loads((SRC/'mesh-library.json').read_text(encoding='utf8'))
    qa=json.loads((SRC/'geometry-qa.json').read_text(encoding='utf8'))
    assert all(x['pass'] for x in qa.values())
    bronze=ART/'GateHammeredBronze.mat'
    surface=bronze.read_text()
    for role in ('BaseColor','Normal','MetalSmooth'):
        file=ART/('GateBronze_'+role+'.png')
        importer=Path(str(file)+'.meta').read_text()
        for expected in ['enableMipMap: 1','wrapU: 0','wrapV: 0','sRGBTexture: '+str(int(role=='BaseColor'))]:
            assert expected in importer
        assert guid(file) in surface
        if role=='Normal':assert 'textureType: 1' in importer
    assert '_NORMALMAP' in surface and '_METALLICSPECGLOSSMAP' in surface and '_BumpScale: 0.75' in surface
    assert guid(PHOTO/'Stone_Normal.png') in (ART/'InlayLimestone.mat').read_text()
    for item in lib.values():
        dim=np.array(item['metric_local_axes'])
        mat=ROOT/item['material']['path'];assert guid(mat)==item['material']['guid'];assert re.search('--- !u!21 &2100000',mat.read_text(encoding='utf8'))
        for mesh in item['meshes']:
            file=ROOT/mesh['path'];s=file.read_text()
            assert guid(file)==mesh['guid'] and sha(file)==mesh['sha256']
            assert '--- !u!43 &4300000' in s and field(s,'m_Name')==file.stem
            buf=np.frombuffer(bytes.fromhex(re.search('_typelessdata: (.+)',s)[1]),'<f4').reshape(-1,12)
            idx=np.frombuffer(bytes.fromhex(field(s,'m_IndexBuffer')),'<u2').reshape(-1,3)
            assert len(idx)==mesh['triangles']==qa[file.stem]['objects'][file.stem]['tris']
            assert int(re.search('indexCount: (\d+)',s)[1])==idx.size and int(re.search('m_DataSize: (\d+)',s)[1])==buf.nbytes
            assert np.isfinite(buf).all() and idx.max()<len(buf) and np.allclose(buf[:,:3].min(0),-.5,atol=2e-6) and np.allclose(buf[:,:3].max(0),.5,atol=2e-6)
            n=buf[:,3:6];t=buf[:,6:9]
            assert np.allclose(np.linalg.norm(n,axis=1),1,atol=2e-5) and np.allclose(np.linalg.norm(t,axis=1),1,atol=2e-5) and np.max(abs((n*t).sum(1)))<.002
            face_pos=buf[idx,:3]*dim;cross=np.cross(face_pos[:,1]-face_pos[:,0],face_pos[:,2]-face_pos[:,0])
            assert np.min(np.linalg.norm(cross,axis=1))>1e-9
            geometric=cross/np.linalg.norm(cross,axis=1)[:,None]
            world_n=n[idx]/dim
            assert np.min((world_n*geometric[:,None,:]).sum(2))>0,(file,'normals')
    detail=[];flat=[];counts={}
    for scene in ('Chapter01_Awakening','Chapter01_Courtyard'):
        sc=ROOT/'Assets/Scene'/(scene+'.unity');ds=parse(sc);dp=parse(PREF)
        exported_rows=list(csv.reader((DUMP/('original-'+scene+'.tsv')).open(encoding='utf-8-sig'),delimiter='\t'))
        cubes=[r for r in exported_rows if r[1].startswith('Cube@')]
        assert len(cubes)==(17 if scene.endswith('Awakening') else 10)
        counts[scene]=len(cubes)
        for path,oldmesh,oldmat,ctr,ext,isp,rid,fid in cubes:
            prefab=isp=='True';data=dp if prefab else ds;source=PREF if prefab else sc
            key=BY_ID[fid];item=lib[key]
            oldm=exported(oldmesh);oldmt=exported(oldmat)
            assert data[rid][0]==23 and data[fid][0]==33
            rb,fb=data[rid][1],data[fid][1]
            assert oldm['guid'] in field(fb,'m_Mesh') and ref(fb,'m_Mesh')==oldm['fileID']
            mats=re.search(r'm_Materials:\n((?:  - [^\n]+\n)+)',rb)[1]
            assert mats.count('fileID:')==1 and f"fileID: {oldmt['fileID']}, guid: {oldmt['guid']}" in mats
            go=ref(rb,'m_GameObject');assert go==ref(fb,'m_GameObject')
            tr,b=next((i,b) for i,(t,b) in data.items() if t==4 and '  m_LocalScale:' in b and ref(b,'m_GameObject')==go)
            chain=[];tid=tr;world=np.eye(4)
            while tid!='0':
                block=data[tid][1];chain.append(dict(id=tid,**record(block)));world=transform(block)@world;tid=ref(block,'m_Father')
            if prefab:
                instances=[(i,b) for i,(k,b) in ds.items() if k==1001 and ('m_SourcePrefab: {fileID: 100100000, guid: '+guid(PREF)) in b]
                assert len(instances)==1
                pi,ib=instances[0]
                # No fitting-specific mesh or material overrides are present.
                for i in (rid,fid):assert 'target: {fileID: '+i+', guid: '+guid(PREF) not in ib
            else:pi=''
            dim=np.linalg.norm(world[:3,:3],axis=0)
            assert np.allclose(dim,item['metric_local_axes'],atol=1e-5)
            assert np.allclose(np.abs(world[:3,:3]).sum(1),floats(ext),atol=.0006) and np.allclose(world[:3,3],floats(ctr),atol=.0006),(scene,path,world)
            groups=[i for i,(k,b) in data.items() if k==205 and f'renderer: {{fileID: {rid}}}' in b]
            assert not groups
            mesh=item['meshes'][0];mat=item['material']
            d=dict(scene=scene,path=path,source=source.relative_to(ROOT).as_posix(),is_prefab=prefab,prefab_instance_id=pi,game_object_id=go,transform_id=tr,renderer_id=rid,filter_id=fid,old_mesh={**oldm,'type':int(re.search(r'type: (\d+)',field(fb,'m_Mesh'))[1])},old_material={**oldmt,'type':2},material_slot=0,new_mesh=mesh,new_material=mat,old_lod_group_ids=groups,mesh_levels=item['meshes'],world_axis_lengths=dim.tolist(),original_world_center=floats(ctr),original_world_aabb_size=floats(ext),transform_chain=chain)
            detail.append(d)
            row=dict(scene=scene,path=path,source=d['source'],isPrefab=isp,prefab_instance_id=pi,renderer_id=rid,filter_id=fid,transform_id=tr,old_mesh=oldmesh,old_mesh_guid=oldm['guid'],old_mesh_fileID=oldm['fileID'],old_material=oldmat,old_material_guid=oldmt['guid'],old_material_fileID=oldmt['fileID'],material_slot=0,new_mesh=mesh['path'],new_mesh_guid=mesh['guid'],new_mesh_fileID=mesh['fileID'],new_mesh_type=2,new_material=mat['path'],new_material_guid=mat['guid'],new_material_fileID=mat['fileID'],new_material_type=2,old_lod_group='',local_metric_xyz=' '.join(f'{x:.8g}' for x in dim),original_center=ctr,original_size=ext)
            for level in [1,2]:
                alt=item['meshes'][level] if level<len(item['meshes']) else {}
                for k in ('path','guid','fileID','triangles'):row[f'lod{level}_{k}']=alt.get(k,'')
            flat.append(row)
    (SRC/'component-map.json').write_text(json.dumps(detail,indent=2,ensure_ascii=False),encoding='utf8')
    with (SRC/'component-map.tsv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=flat[0].keys(),delimiter='\t');w.writeheader();w.writerows(flat)
    files=[Path(str(ART)+'.meta')]+sorted(f for f in ART.rglob('*') if f.is_file())
    (SRC/'runtime-files.tsv').write_text('project_relative_path\tsha256\n'+''.join(f'{f.relative_to(ROOT).as_posix()}\t{sha(f)}\n' for f in files),encoding='utf-8-sig')
    summary=dict(integration=['Apply the new mesh to the listed MeshFilter and the new material to MeshRenderer material slot zero.', 'Keep the original object, parent, transform, collider, gameplay components and animation targets.', 'Use source + component ID as the patch key. The nested stone dais is a prefab-source record.', 'mesh_levels are exported native geometry. The baseline has no LODGroups on these renderers. Additional levels require explicit wiring in Unity if used.'],qa_warnings=['UV0 deliberately repeats metric tiling textures.', 'Pivot is the original Cube centre. The lower surface lies below the origin.'],scene_counts=counts,component_rows=len(flat),prefab_rows=sum(d['is_prefab'] for d in detail),unique_near_meshes=len(lib),published_meshes=len(qa),native_bounds_local=[-.5,.5],original_lod_groups=0,baseline='original TSV + source hierarchy static readback',render_review='Blender Cycles in metres; material wrap and tangent checks',runtime='parent session',shared_materials=[v['material']['path'] for v in lib.values() if not v['material']['path'].startswith(ART.relative_to(ROOT).as_posix())],scene_triangles={s:sum(d['new_mesh']['triangles'] for d in detail if d['scene']==s) for s in counts})
    (SRC/'validation.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False),encoding='utf8')
    print(counts,summary['scene_triangles'],len(qa))
if __name__=='__main__':main()
