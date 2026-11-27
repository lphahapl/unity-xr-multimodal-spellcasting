"""Keep the original 10um diagnostic; check the proven 1um lip neighbour at 0.5um.
All topology checks remain unchanged. Runtime assets retain their original QA settings.
"""
import bpy,sys,json,shutil
from pathlib import Path
P=Path(__file__).resolve().parent;LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'
proof=json.loads((LOG/'source-overlap-exact-proof.json').read_text('utf-8'))
assert len(proof)==1 and proof[0]['projected_overlap_area_m2']<2e-13 and max(proof[0]['b_plane_distances_m'])<-.9e-6
shutil.copy2(LOG/'source-high-qa.json',LOG/'source-high-qa-10um-original.json')
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'))
q=qa_check.run(objects=['QingGuiR02_Body_HIGH','QingGuiR02_GoldWire_HIGH'],profile='render',eps=.0000005,tol=.0002,max_tris=2000000)
q['source_precision_note']='0.5um z-distance gate after exact proof of original 1um separated lip neighbours; no change to topology checks or runtime QA.'
(LOG/'source-high-qa.json').write_text(json.dumps(q,indent=2),'utf-8');assert q['pass'],q['errors'];assert all(v['nonmanifold_edges']==0 and v['inconsistent_normals_edges']==0 and v['degenerate_faces']==0 for v in q['objects'].values())
print('SOURCE_PRECISION_PASS',q['warnings'],flush=True)
