import bpy,sys,types,json
from pathlib import Path
P=Path(__file__).resolve().parent;QA=Path(r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts/qa_check.py');sys.path.insert(0,str(QA.parent))
text=QA.read_text('utf-8').replace('zf = _describe(zf_pairs)', "print('EXACT_ZF_DIAGNOSTIC',[(i,j,[list(verts[k]) for k in tris[i]],[list(verts[k]) for k in tris[j]]) for i,j in zf_pairs],flush=True)\n    zf = _describe(zf_pairs)")
module=types.ModuleType('readonly_qa_diagnostic');module.__file__=str(QA);exec(compile(text,str(QA),'exec'),module.__dict__)
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'));q=module.run(objects=['QingGuiR02_Body_HIGH'],profile='render',eps=.00001,tol=.0002,max_tris=2000000)
print('DIAGNOSTIC_COMPLETE',q['errors'],flush=True)
