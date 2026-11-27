from pathlib import Path
P=Path(__file__).resolve().parent
t=(P/'measure_interface.py').read_text('utf-8')
t=t.replace("bpy.ops.wm.open_mainfile(filepath=str(OUT/'QingGuiR02B.blend'))", "bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(OUT/'QingGuiR02B_GeometryCandidate.fbx'))")
t=t[:t.index("report=json.loads")]
t=t.replace('measured-interface.json','candidate-interface.json')
exec(compile(t,str(P/'measure_interface.py'),'exec'))
print('CANDIDATE_INTERFACE_PASS',flush=True)
