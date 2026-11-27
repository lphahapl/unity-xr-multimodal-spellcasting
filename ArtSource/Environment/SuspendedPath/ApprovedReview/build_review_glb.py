"""Publish a portable, scaled texture review mesh from Chapter02ReviewMesh.Export."""
import argparse
import io
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

def export(snapshot_dir, project, key, dest):
    source = json.loads((snapshot_dir / (key + '-mesh.json')).read_text('utf-8-sig'))
    raw = (snapshot_dir / (key + '-mesh.bin')).read_bytes()
    doc = {'asset': {'version': '2.0', 'generator': 'Chapter02 Unity mesh review'},
           'scene': 0, 'scenes': [{'nodes': []}], 'nodes': [], 'meshes': [], 'materials': [],
           'bufferViews': [], 'accessors': [], 'images': [], 'textures': [],
           'samplers': [{'magFilter': 9729, 'minFilter': 9987, 'wrapS': 10497, 'wrapT': 10497}]}
    data = bytearray()
    def view(binary, target=None):
        data.extend(b'\0' * (-len(data) % 4))
        row = {'buffer': 0, 'byteOffset': len(data), 'byteLength': len(binary)}
        if target: row['target'] = target
        data.extend(binary)
        doc['bufferViews'].append(row)
        return len(doc['bufferViews']) - 1
    def accessor(arr, kind, component, target):
        vals = {'bufferView':view(arr.tobytes(), target), 'componentType':component, 'count':len(arr), 'type':kind}
        if kind == 'VEC3':
            vals.update(min=arr.min(axis=0).tolist(),max=arr.max(axis=0).tolist())
        doc['accessors'].append(vals)
        return len(doc['accessors'])-1

    cache = {}
    def image_input(path):
        if not path or Path(path).suffix.lower() not in {'.png','.jpg','.jpeg','.tga'}:return None
        with Image.open(project / path) as f:
            img=f.convert('RGBA')
        img.thumbnail((1024,1024), Image.Resampling.LANCZOS)
        return img
    def texture(name, im, fmt):
        if name in cache:return cache[name]
        stream=io.BytesIO()
        im.convert('RGB').save(stream,fmt,**({'quality':90,'subsampling':0} if fmt=='JPEG' else {}))
        doc['images'].append({'name':name,'bufferView':view(stream.getvalue()),'mimeType':'image/jpeg' if fmt=='JPEG' else 'image/png'})
        doc['textures'].append({'source':len(doc['images'])-1,'sampler':0})
        cache[name]=len(doc['textures'])-1
        return cache[name]

    for m in source['materials']:
        pbr={'baseColorFactor':[max(0,min(1,float(x))) for x in m['color']], 'metallicFactor':m['metal'],
             'roughnessFactor':1-m['smooth']}
        material={'name':m['name'],'pbrMetallicRoughness':pbr}
        al=image_input(m['albedo']); normal=image_input(m['normal']); ms=image_input(m['metalMap'])
        if al is not None:
            pbr['baseColorTexture']={'index':texture(m['albedo'],al,'JPEG')}
        if normal is not None:
            material['normalTexture']={'index':texture(m['normal'],normal,'PNG'),'scale':m['bump']}
        if ms is not None or (m['smoothFromColor'] and al is not None):
            img=ms if ms is not None else al
            sm=al if m['smoothFromColor'] and al is not None else img
            alpha=np.asarray(sm.resize(img.size).getchannel('A'),dtype=float)/255
            packed=np.full((img.height,img.width,3),255,dtype=np.uint8)
            packed[:,:,1]=np.clip((1-alpha*m['smooth'])*255,0,255).astype(np.uint8)
            packed[:,:,2]=np.asarray(ms.getchannel('R')) if ms is not None else round(m['metal']*255)
            tag='roughness/'+str(m['smooth'])+'/'+str(m['metal'])+'/'+str(m['smoothFromColor'])+'/'+m['albedo']+'/'+m['metalMap']
            idx=texture(tag,Image.fromarray(packed),'PNG')
            pbr.update(metallicFactor=1,roughnessFactor=1,metallicRoughnessTexture={'index':idx})
        doc['materials'].append(material)
    for part in source['parts']:
        n=part['verts'];off=part['offset']
        v=np.frombuffer(raw, dtype='<f4', count=n*8, offset=off).reshape(n,8)
        inds=np.frombuffer(raw,dtype='<u4', count=part['indices'],offset=off+32*n)
        if not np.isfinite(v).all() or inds.max()>=n:raise ValueError(part['name'])
        prim={'attributes':{'POSITION':accessor(v[:,:3],'VEC3',5126,34962),
                            'NORMAL':accessor(v[:,3:6],'VEC3',5126,34962),
                            'TEXCOORD_0':accessor(v[:,6:],'VEC2',5126,34962)},
              'indices':accessor(inds,'SCALAR',5125,34963),'material':part['mat']}
        doc['meshes'].append({'primitives':[prim]})
        doc['scenes'][0]['nodes'].append(len(doc['nodes']))
        doc['nodes'].append({'name':part['name'],'mesh':len(doc['meshes'])-1})
    data.extend(b'\0' * (-len(data)%4));doc['buffers']=[{'byteLength':len(data)}]
    desc=json.dumps(doc,ensure_ascii=False,separators=(',',':')).encode('utf8')
    desc+=b' '*(-len(desc)%4)
    dest.parent.mkdir(parents=True,exist_ok=True)
    with dest.open('wb') as out:
        out.write(struct.pack('<4sIIII',b'glTF',2,28+len(desc)+len(data),len(desc),0x4e4f534a))
        out.write(desc);out.write(struct.pack('<II',len(data),0x004e4942));out.write(data)
    print(key,len(doc['nodes']),'parts',len(doc['images']),'images',dest.stat().st_size,'bytes')
if __name__=='__main__':
    p=argparse.ArgumentParser()
    for arg in ('snapshot_dir','project','destination'):p.add_argument(arg,type=Path)
    a=p.parse_args()
    for k in ('basket','workshop'):export(a.snapshot_dir,a.project,k,a.destination/(k+'.glb'))
