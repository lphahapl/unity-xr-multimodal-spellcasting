import bpy,bmesh,json,sys
from pathlib import Path
P=Path(__file__).resolve().parent;sys.dont_write_bytecode=True;sys.path.insert(0,str(P));from qinggui_wires import build_wires
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'));o=bpy.data.objects['QingGuiR02_Body_HIGH'];bm=bmesh.new();bm.from_mesh(o.data)
fins=[f for f in bm.faces if f.calc_area()<1e-7 and sum(e.is_boundary for e in f.edges)>=2 and any(len(e.link_faces)>2 for e in f.edges)];count=len(fins)
if fins:bmesh.ops.delete(bm,geom=fins,context='FACES')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
bpy.data.objects.remove(bpy.data.objects['QingGuiR02_GoldWire_HIGH'],do_unlink=True);wire,paths=build_wires(o,'QingGuiR02_GoldWire_HIGH',3)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'QingGuiR02B_Source.blend'))
LOG=P.parents[5]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b';(LOG/'fin-repair.json').write_text(json.dumps({'removed_tiny_boolean_fins':count,'criterion':'area<1e-8, two boundary edges and one >2-face edge; no broad surface smoothing'},indent=2),'utf-8')
(LOG/'wire-source-paths.json').write_text(json.dumps(paths,indent=2),'utf-8')
sys.path.insert(0,r'C:/Users/鲁迅/.codex/skills/blender-asset-qa/scripts');import qa_check
q=qa_check.run(objects=[o.name,wire.name],profile='render',eps=.00001,tol=.0002,max_tris=2000000);(LOG/'source-high-qa.json').write_text(json.dumps(q,indent=2),'utf-8');assert q['pass'];print('CLEAN_SOURCE',count,q['warnings'],flush=True)
