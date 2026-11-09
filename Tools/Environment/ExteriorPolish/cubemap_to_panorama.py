"""Deterministic Unity cubemap -> true 2:1 equirectangular panorama (numpy/Pillow).

Usage: python Tools/Environment/ExteriorPolish/cubemap_to_panorama.py [--wait-seconds 300]
World +Z is the panorama centre, +X to its right, +Y at the top.
Orientation is selected by 12 matching world-space cube edges, not file assumptions.
"""
from pathlib import Path
import argparse
import hashlib
import json
import time
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
NAMES = ['PositiveX', 'NegativeX', 'PositiveY', 'NegativeY', 'PositiveZ', 'NegativeZ']
# Face forward, image right, image down in Unity's left-handed world coordinates.
BASIS = np.array([
    [[1,0,0],[0,0,-1],[0,-1,0]], [[-1,0,0],[0,0,1],[0,-1,0]],
    [[0,1,0],[1,0,0],[0,0,1]], [[0,-1,0],[1,0,0],[0,0,-1]],
    [[0,0,1],[1,0,0],[0,-1,0]], [[0,0,-1],[-1,0,0],[0,-1,0]],
], dtype=np.float64)
TRANSFORMS = ['identity', 'rotate90', 'rotate180', 'rotate270',
              'flipX', 'flipX_rotate90', 'flipY', 'flipY_rotate90']

def transformed(im, k):
    return np.rot90(np.fliplr(im) if k >= 4 else im, k % 4)

def project(directions, face):
    f, r, d = BASIS[face]
    scale = directions @ f
    return (directions @ r)/scale, (directions @ d)/scale

def bilinear(im, u, v):
    h,w = im.shape[:2]
    x = np.clip((u+1)*w/2-.5, 0, w-1)
    y = np.clip((v+1)*h/2-.5, 0, h-1)
    x0,y0 = x.astype(np.int32),y.astype(np.int32)
    x1,y1 = np.minimum(x0+1,w-1),np.minimum(y0+1,h-1)
    fx,fy = (x-x0)[...,None],(y-y0)[...,None]
    return (im[y0,x0]*(1-fx)*(1-fy)+im[y0,x1]*fx*(1-fy)
            +im[y1,x0]*(1-fx)*fy+im[y1,x1]*fx*fy)

def edge_costs(images):
    """Evaluate eight square symmetries per face at 12 world-space shared edges."""
    variants = [[transformed(im,k) for k in range(8)] for im in images]
    result = []
    samples = np.linspace(-.97,.97,384)
    inset = 1.0 - 1.0/images[0].shape[0]
    for a in range(6):
        f,r,d = BASIS[a]
        for axis in (r,d):
            other = d if np.array_equal(axis,r) else r
            for sign in (-1,1):
                neighbor_forward = axis*sign
                b = next(i for i in range(6) if np.array_equal(BASIS[i,0],neighbor_forward))
                if a >= b: continue
                directions = f + neighbor_forward + samples[:,None]*other
                ua,va = project(directions,a)
                ub,vb = project(directions,b)
                # Sampling one-half pixel inside each edge avoids unrendered fringe.
                sa = [bilinear(im,ua*inset,va*inset) for im in variants[a]]
                sb = [bilinear(im,ub*inset,vb*inset) for im in variants[b]]
                costs = np.zeros((8,8))
                for ka in range(8):
                    for kb in range(8):
                        errors = np.mean(np.abs(sa[ka]-sb[kb]),axis=1)/255
                        # Suppress only the worst 5%: thin silhouettes can alias at an edge.
                        costs[ka,kb] = np.mean(np.sort(errors)[:int(.95*len(errors))])
                result.append((a,b,costs))
    return result

def choose_orientation(costs):
    def score(state): return float(np.mean([c[state[a],state[b]] for a,b,c in costs]))
    global_scores = [score([k]*6) for k in range(8)]
    best_global = int(np.argmin(global_scores))
    starts = [[k]*6 for k in range(8)]
    rng = np.random.default_rng(4291)
    starts += [list(rng.integers(0,8,6)) for _ in range(48)]
    candidates=[]
    for start in starts:
        state = start[:]
        for _ in range(24):
            before=state[:]
            for face in range(6):
                choices=[]
                for k in range(8):
                    trial=state[:]; trial[face]=k
                    choices.append(score(trial))
                state[face]=int(np.argmin(choices))
            if state == before: break
        candidates.append((score(state),state))
    candidates.sort(key=lambda v:(v[0],sum(k!=best_global for k in v[1]),v[1]))
    best_score,state=candidates[0]
    # Prefer the common readback convention when custom transforms bring negligible benefit.
    if global_scores[best_global] <= best_score+.0004:
        state=[best_global]*6; best_score=global_scores[best_global]
    return state, best_score, dict(zip(TRANSFORMS,global_scores))

def panorama(images, width=4096, height=2048):
    output=np.empty((height,width,3),dtype=np.uint8)
    longitude=(np.arange(width)+.5)/width*2*np.pi-np.pi
    for start in range(0,height,64):
        rows=np.arange(start,min(start+64,height))
        latitude=np.pi/2-(rows+.5)/height*np.pi
        lon,lat=np.meshgrid(longitude,latitude)
        directions=np.stack([np.sin(lon)*np.cos(lat), np.sin(lat),np.cos(lon)*np.cos(lat)],axis=-1)
        major=np.argmax(np.abs(directions),axis=-1)
        chosen=major*2+(np.take_along_axis(directions,major[...,None],axis=-1)[...,0]<0)
        tile=np.empty(directions.shape,dtype=np.uint8)
        for face in range(6):
            mask=chosen==face
            u,v=project(directions[mask],face)
            tile[mask]=np.clip(np.rint(bilinear(images[face],u,v)),0,255).astype(np.uint8)
        output[start:start+len(rows)]=tile
    return Image.fromarray(output)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',type=Path,default=ROOT/'Logs/exterior-polish-20261108/integration')
    parser.add_argument('--output',type=Path,default=ROOT/'Logs/exterior-polish-20261108/panorama')
    parser.add_argument('--wait-seconds',type=int,default=0)
    args=parser.parse_args()
    files=[args.input/('cube-'+name+'.png') for name in NAMES]
    orientation_file=args.input/'cubemap-orientation.txt'
    deadline=time.monotonic()+args.wait_seconds
    while not all(p.exists() for p in files+[orientation_file]) and time.monotonic()<deadline: time.sleep(2)
    missing=[str(p) for p in files+[orientation_file] if not p.exists()]
    if missing: raise SystemExit('Missing cubemap faces: '+', '.join(missing))
    args.output.mkdir(parents=True,exist_ok=True)
    images=[np.asarray(Image.open(p).convert('RGB')) for p in files]
    if any(im.shape != (1024,1024,3) for im in images): raise SystemExit('Expected six 1024 square RGB faces')
    provenance=orientation_file.read_text(encoding='utf-8-sig')
    face_info={name:dict(mean_rgb=im.mean(axis=(0,1)).tolist(),
        near_black_fraction=float(np.mean(np.max(im,axis=2)<4)),
        sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for name,im,p in zip(NAMES,images,files)}
    if any(x['near_black_fraction']>.98 for x in face_info.values()):
        (args.output/'black-face-error.json').write_text(json.dumps(face_info,indent=2),encoding='utf-8')
        raise SystemExit('A source face is over 98% black; see black-face-error.json. Refusing misleading panorama.')
    costs=edge_costs(images)
    state,score,global_scores=choose_orientation(costs)
    oriented=[transformed(im,k) for im,k in zip(images,state)]
    pano=panorama(oriented)
    pano.save(args.output/'panorama.png')
    preview=pano.crop((0,512,4096,1536)).resize((3072,768),Image.Resampling.LANCZOS)
    canvas=Image.new('RGB',(3072,800),(22,27,30));canvas.paste(preview,(0,0))
    draw=ImageDraw.Draw(canvas)
    labels=[(8,'-Z / BACK'),(768,'-X / LEFT'),(1536,'+Z / FORWARD'),(2304,'+X / RIGHT'),(2980,'-Z / BACK')]
    for x,label in labels: draw.text((x,779),label,fill=(225,232,235))
    canvas.save(args.output/'panorama-wide-preview.png')
    contact=Image.new('RGB',(1536,1060),(22,27,30));draw=ImageDraw.Draw(contact)
    for i,(name,im) in enumerate(zip(NAMES,oriented)):
        x=(i%3)*512;y=(i//3)*530
        contact.paste(Image.fromarray(im).resize((512,512),Image.Resampling.LANCZOS),(x,y+18))
        draw.text((x+8,y+3),name+' '+TRANSFORMS[state[i]],fill='white')
    contact.save(args.output/'oriented-faces-contact.png')
    edges=[dict(a=NAMES[a],b=NAMES[b],mean_absolute_rgb_difference=float(c[state[a],state[b]]),
        equivalent_8bit_levels=float(c[state[a],state[b]]*255)) for a,b,c in costs]
    pixels=np.asarray(pano).astype(float)
    wrap=float(np.mean(np.abs(pixels[:,0]-pixels[:,-1]))/255)
    adjacent=float(np.mean(np.abs(pixels[:,1:]-pixels[:,:-1]))/255)
    report=dict(capture_provenance=provenance.strip(),
        actual_render_method='native Camera.RenderToCubemap' if provenance.startswith('Unity Camera.RenderToCubemap') else 'six square 90-degree Camera.Render fallback',
        projection='true 360x180 equirectangular, 4096x2048, centre +Z; right +X; top +Y',
        source_face_info=face_info,face_transforms=dict(zip(NAMES,[TRANSFORMS[k] for k in state])),
        global_transform_scores=global_scores,mean_edge_error=score,edges=edges,
        panorama_wrap_difference=wrap,ordinary_horizontal_adjacent_difference=adjacent,
        edge_audit_pass=bool(score<.025 and max(e['mean_absolute_rgb_difference'] for e in edges)<.07),
        note='No scene edits; pixels are bilinear resampling of six same-position camera faces. Wide preview covers full 360 longitude and latitude +/-45 degrees; full PNG includes both poles.')
    (args.output/'panorama-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:report[k] for k in ['actual_render_method','face_transforms','mean_edge_error','panorama_wrap_difference','edge_audit_pass']},indent=2))
    if not report['edge_audit_pass']: raise SystemExit('Panorama generated but edge audit failed; inspect source/orientation.')

if __name__=='__main__': main()
