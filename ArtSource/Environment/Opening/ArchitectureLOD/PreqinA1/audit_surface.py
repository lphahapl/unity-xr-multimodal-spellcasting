"""Area-weighted, bilinear mip-zero shader-input audit of actual published hall triangles."""
from pathlib import Path
import re,json,hashlib
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
ART=ROOT/'Assets/Art/Environment/Opening/ArchitectureLOD'
BANDS=dict(pan=(0,720),timber=(720,1120),masonry=(1120,1480),plaster=(1480,1616),terminal=(1616,1712),cover_and_ridge=(1712,1832))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def percentile(x):return [round(float(v),4) for v in np.percentile(x,[5,50,95])]
def read(path):
    native=path.read_text()
    verts=np.frombuffer(bytes.fromhex(re.search('_typelessdata: (.+)',native)[1]),'<f4').reshape(-1,12)
    indices=np.frombuffer(bytes.fromhex(re.search('m_IndexBuffer: (.+)',native)[1]),'<u2').reshape(-1,3)
    return verts,indices

def main():
    text=(ART/'Architecture_Atlas.mat').read_text()
    path=ART/'Textures/Architecture_Mask.png';meta=Path(str(path)+'.meta').read_text()
    guid=re.search('guid: (\w+)',meta)[1]
    assert re.search(r'_MetallicGlossMap:\s+m_Texture: \{fileID: \d+, guid: '+guid,text)
    assert '_METALLICSPECGLOSSMAP' in text and '_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A' not in text
    assert '_SmoothnessTextureChannel: 0' in text and 'sRGBTexture: 0' in meta and 'alphaUsage: 1' in meta
    smooth=float(re.search(r' - _Smoothness: ([.\d]+)',text)[1])
    alpha=np.asarray(Image.open(path))[::-1,:,3].astype('f')/255
    h,w=alpha.shape
    result={'percentiles':[5,50,95],'domain':'Linear uncompressed source Mask.A, bilinear filtering at mip 0. Samples weighted by metric triangle area for each of the three LOD0 halls; device mip selection and ASTC are not measured.','binding':{'texture_guid':guid,'mask_sha256':sha(path),'smoothness_multiplier':smooth,'roughness':'1 - A * _Smoothness','GGX_alpha':'max(perceptualRoughness^2, HALF_MIN_SQRT)','shader':'Universal Render Pipeline/Lit metallic workflow'},'sample_count_per_hall_per_band':48000}
    def bilinear(u,v):
        x=(u%1)*w-.5;y=np.clip(v*h-.5,0,h-1)
        ix=np.floor(x).astype(int);iy=np.floor(y).astype(int);fx=x-ix;fy=y-iy
        return (1-fy)*((1-fx)*alpha[iy,ix%w]+fx*alpha[iy,(ix+1)%w])+fy*((1-fx)*alpha[(iy+1).clip(max=h-1),ix%w]+fx*alpha[(iy+1).clip(max=h-1),(ix+1)%w])
    result['per_hall']={};pooled={k:[] for k in BANDS}
    for path in sorted((ART/'Meshes').glob('CalibrationHall_*LOD0.asset')):
        vertex,idx=read(path);points=vertex[idx,:3];uv=vertex[idx,10:12]
        areas=np.linalg.norm(np.cross(points[:,1]-points[:,0],points[:,2]-points[:,0]),axis=1)/2
        rng=np.random.default_rng(290)
        per={}
        for key,(lo,hi) in BANDS.items():
            ys=uv[:,:,1]*h;selected=np.flatnonzero((ys.min(1)>=lo)&(ys.max(1)<=hi))
            assert selected.size>0
            weights=areas[selected].astype(float);weights/=weights.sum()
            sample=uv[rng.choice(selected,48000,p=weights)]
            a=np.sqrt(rng.random(48000));b=rng.random(48000)
            xy=sample[:,0]*(1-a[:,None])+sample[:,1]*(a*(1-b))[:,None]+sample[:,2]*(a*b)[:,None]
            rough=1-bilinear(xy[:,0],xy[:,1])*smooth
            per[key]=dict(perceptual_p05_median_p95=percentile(rough),GGX_median=round(float(np.median(rough**2)),4))
            pooled[key].append(rough)
        result['per_hall'][path.stem]=per
    result['three_halls_equal_weight']={k:percentile(np.concatenate(arr)) for k,arr in pooled.items()}
    result['full_atlas_band_perceptual']={k:percentile(1-alpha[lo+4:hi-4]*smooth) for k,(lo,hi) in BANDS.items()}
    (HERE/'mask-roughness.json').write_text(json.dumps(result,indent=2),encoding='utf8')
    baseline=json.loads((HERE/'revision-baseline.json').read_text())['old_asset_sha256']
    lines=[];delta=[]
    for file,old in sorted(baseline.items()):
        f=ROOT/file;digest=sha(f);row=f'{file}\t{digest}\n';lines.append(row)
        if old!=digest:delta.append(row)
    for name,rows in [('runtime-files',lines),('revision-files',delta)]:
        (HERE/(name+'.tsv')).write_text('project_relative_path\tsha256\n'+''.join(rows),encoding='utf-8-sig')
    print(result['three_halls_equal_weight'],len(delta))
if __name__=='__main__':main()
