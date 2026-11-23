"""Read Unity's imported mesh buffers without loading a scene. Pass the editor's binary2text.exe."""
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path
import numpy as np
from layout import SOURCE, PROJECT, EXPORT

QA=SOURCE/'QA'
names=('Bench','Stele','Plinth')
expected={'Bench':[1.25,.97,.78],'Stele':[.85,1.65,.36],'Plinth':[.43,.9,.36]}

def inspect(artifact,name,qa):
    with tempfile.TemporaryDirectory(dir=QA) as temp:
        dump=Path(temp)/'artifact.txt'
        subprocess.run([sys.argv[1],str(artifact),str(dump)],check=True,capture_output=True)
        data=dump.read_text('utf8')
    chunks=re.split(r'(?m)^ID: ',data)
    lods=sorted(map(float,re.findall(r'screenRelativeHeight ([.\d]+)',data)),reverse=True)
    assert len(lods)==3
    for section in chunks:
        if '(ClassID: 4) Transform' not in section[:65]: continue
        q=section.split('m_LocalRotation')[1].split('m_LocalPosition')[0]
        assert np.allclose([float(t) for t in re.findall(r'\s[xyzw] ([-\d.e+]+) \(float\)',q)],[0,0,0,1])
        for field,target in [('m_LocalPosition',[0]*3),('m_LocalScale',[1]*3)]:
            assert np.allclose(np.fromstring(re.search(field+r' \(([^)]+)\)',section)[1],sep=' '),target)
    found={}
    for section in chunks:
        if '(ClassID: 43) Mesh' not in section[:65]: continue
        meshname=re.search(r'm_Name "([^"]+)"',section)[1]
        count=int(re.search(r'indexCount (\d+)',section)[1])//3
        assert qa[meshname]['stats']['total_tris']==count
        stream=section.split('m_VertexData  (VertexData)')[1].split('m_CompressedMesh')[0]
        n=int(re.search(r'm_VertexCount (\d+)',stream)[1])
        buf=bytes(map(int,' '.join(re.findall(r'data \(UInt8\) #\d+: ([\d ]+)',stream)).split()))
        channels=re.findall(r'stream (\d+) \(UInt8\)\s+offset (\d+) \(UInt8\)\s+format (\d+) \(UInt8\)\s+dimension (\d+) \(UInt8\)',stream)
        assert all(int(channels[i][2])==0 for i in (0,1,4))
        stride=len(buf)//n
        def channel(i):
            _,offset,_,dim=map(int,channels[i])
            return np.ndarray((n,dim),'<f4',buf,offset,(stride,4))
        xyz,norm,uv=channel(0),channel(1),channel(4)
        low=xyz.min(axis=0); high=xyz.max(axis=0)
        assert np.allclose(high-low,expected[name],atol=.0015) and abs(low[1])<1e-5
        assert np.allclose((low+high)[[0,2]],0,atol=1e-5)
        rec={'triangles':count,'imported_vertices':n,'min_xyz':low.round(6).tolist(),'max_xyz':high.round(6).tolist()}
        if name=='Stele':
            # The region assigned only to the inscription slab.
            chosen=(uv[:,0]>.003)&(uv[:,0]<.417)&(uv[:,1]>.259)&(uv[:,1]<.997)
            assert chosen.any()
            points=xyz[chosen]; nz=norm[chosen,2]
            assert points[:,2].max()<-.11 and np.median(nz)<-.80
            rec.update(inscribed_face_z_range=[float(points[:,2].min()),float(points[:,2].max())],median_normal_z=float(np.median(nz)))
        found[meshname]=rec
    assert len(found)==3
    return {'artifact':artifact.name,'lod_heights':lods,'meshes':found}

if __name__=='__main__':
    checks=json.loads((QA/'MeshQA.json').read_text())
    library=Path(sys.argv[2]) if len(sys.argv)>2 else PROJECT/'Library'
    files=sorted((p for p in (library/'Artifacts').glob('*/*') if 20000<p.stat().st_size<850000 and p.suffix==''),key=lambda p:p.stat().st_mtime,reverse=True)
    results={}
    for p in files:
        data=p.read_bytes()
        for name in names:
            if name not in results and (name+'_LOD0').encode() in data:
                results[name]=inspect(p,name,checks)
        if len(results)==3: break
    assert len(results)==3
    report={'unity_version':(PROJECT/'ProjectSettings/ProjectVersion.txt').read_text().splitlines()[0].split()[-1],'import_context':'isolated project with identical FBX and importer meta' if len(sys.argv)>2 else 'project Library','results':results}
    (QA/'UnityImport.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))

