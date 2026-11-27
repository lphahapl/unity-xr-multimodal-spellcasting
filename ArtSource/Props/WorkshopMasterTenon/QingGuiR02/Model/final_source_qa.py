import bpy,sys,json
from pathlib import Path
P=Path(__file__).resolve().parent;LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model'
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02_Source.blend'))
q=qa_check.run(objects=['QingGuiR02_Body_HIGH','QingGuiR02_GoldWire_HIGH'],profile='render',eps=.00001,tol=.0002,max_tris=2000000)
(LOG/'source-high-qa.json').write_text(json.dumps(q,indent=2),'utf-8');assert q['pass'],q['errors'];print('FINAL_SOURCE_QA',q['pass'],q['warnings'],flush=True)
