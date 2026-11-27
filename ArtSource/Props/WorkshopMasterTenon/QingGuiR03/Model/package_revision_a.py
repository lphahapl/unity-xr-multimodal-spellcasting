import json,hashlib,datetime
from pathlib import Path
import numpy as np
SOURCE=Path(__file__).resolve().parent;P=SOURCE/'RevisionA';LOG=SOURCE.parents[4]/'Logs/workshop-master-tenon-qinggui-r03-20261126/model/revision-a'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),'utf-8')
ready=read(P/'candidate-ready.json');cold=read(LOG/'cold-exports.json');assert len(ready['wire_paths'])==18 and all(p['max_radial_clearance_m']<0 for p in ready['wire_paths'])
assert all(v['qa']['pass'] for v in cold.values())
for f in ready['files']:assert sha(Path(f['path']))==f['sha256'],f
old=read(SOURCE/'candidate-ready.json');assert all(sha(Path(f['path']))==f['sha256'] for f in old['files'])
B=SOURCE.parents[1]/'QingGuiR02/Model/RevisionB/frozen-manifest.json';prior=read(B);assert all(sha(Path(f['path']))==f['sha256'] for f in prior['core_files'])
def cross(a,b):return float(a[0]*b[1]-a[1]*b[0])
def area(p):return abs(sum(cross(a-p[0],b-p[0]) for a,b in zip(p,p[1:]+p[:1])))*.5 if len(p)>2 else 0.
proof=[]
for e in cold['QingGuiR03_StructureCandidate.glb'].get('seam_pair_evidence',[]):
    a,b=np.array(e['coordinates'],dtype=np.float64);n=np.cross(a[1]-a[0],a[2]-a[0]);drop=int(np.argmax(abs(n)));keep=[i for i in range(3) if i!=drop];aa=[x[keep] for x in a];bb=[x[keep] for x in b]
    if cross(bb[1]-bb[0],bb[2]-bb[0])<0:bb.reverse()
    poly=aa[:]
    for u,v in zip(bb,bb[1:]+bb[:1]):
        out=[]
        if not poly:break
        for s,t in zip(poly,poly[1:]+poly[:1]):
            ds=cross(v-u,s-u);dt=cross(v-u,t-u)
            if (ds>=0)!=(dt>=0):out.append(s+(t-s)*ds/(ds-dt))
            if dt>=0:out.append(t)
        poly=out
    proof.append({**e,'projected_overlap_area_m2':area(poly),'dropped_axis':drop})
write(LOG/'glb-seam-area-proof.json',proof)
stamp=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat()
manifest={'status':'FROZEN','asset':'QingGuiR03_StructureA','scope':'STRUCTURE_CANDIDATE_NOT_FINAL_OPTIMIZED_ASSET','frozen_at':stamp,'core_count':len(ready['files']),'core_files':ready['files'],'first_candidate_unchanged':True,'R02B_25_core_unchanged':True,'wire_paths':18,'triangles':40024,'limitations':['Crown finite segments and copper-tail corner notch remain first-sample limits.','Final engraving bakes, LODs, final jade optics and Quest performance unverified.','Unity first-shot hard triangles were diagnosed as Mobile shadow bias, not attributed to Thickness or model normals.']}
write(P/'frozen-manifest.json',manifest)
specs=[('three_parts_clay','StructurePreview/12_ThreeActualPartsExploded.png','说明性拆件：玉芯 / 铜框 / 18条独立金丝','Actual separated meshes from this RevisionA; same untextured grey material; not gameplay disassembly'),('clay_assembly','StructurePreview/11_UntexturedClayAssembly.png','真实三件装配：统一无贴图灰模','Actual same candidate meshes; no Normal texture or jade shader'),('assembly_pbr','StructurePreview/01_Assembled45.png','A 结构材质装配','Actual Blender Cycles; not Unity final jade appearance'),('crown_tail','StructurePreview/02_CrownTailClose.png','铜帽实体终止与孔口','Actual RevisionA meshes'),('back_scale','StructurePreview/03_BackHeld.png','背面正常比例','Actual RevisionA meshes'),('receiving_pocket','StructurePreview/05_JadeReceivingPocket.png','移去铜件后的完整玉芯承口','Actual closed JadeCore; structure-only material'),('bronze_interior','StructurePreview/06_BronzeInterior.png','铜框独立内壁与端面','Actual closed BronzeFrame; crown segment limits retained'),('structure_glb','QingGuiR03_StructureCandidate.glb','R03 A 三件结构旋转预览','Standard PBR GLB, no Unity jade shader'),('thickness','CandidateTextures/QingGuiR03_JadeCore_Thickness.png','新玉芯逐像素真实厚度','1930932 UV texel rays matching CORNER normals; R×.12m, limits in ray report')]
media=[]
for ident,rel,label,origin in specs:
    p=P/rel;media.append({'id':ident,'path':str(p),'relative_path':rel,'label':label,'origin':origin,'sha256':sha(p),'bytes':p.stat().st_size})
write(P/'media-contract.json',{'status':'FROZEN','asset':manifest['asset'],'frozen_at':stamp,'model_manifest':str(P/'frozen-manifest.json'),'media':media,'presentation_rules':['11/12 are the requested same-version clay assembly and full three-part exploded view.','Do not use old two-part04 as full three-part clay.','Blender views are not Unity screenshots; root provides actual Unity evidence.','This is a structural first sample, not complete optimized game art.','Keep first R03 and R02B history available.']})
print('STRUCTURE_A_FROZEN',stamp,'media',len(media),'GLB seam areas',[x['projected_overlap_area_m2'] for x in proof])
