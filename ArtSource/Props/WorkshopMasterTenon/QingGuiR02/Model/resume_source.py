"""Resume from the exact body checkpoint after a wire-only correction."""
from pathlib import Path
P=Path(__file__).resolve().parent
text=(P/'build_source.py').read_text('utf-8')
start="ob=build_body('QingGuiR02_Body_HIGH',dense=True);detail=sculpt(ob);mat,_,_=material();ob.data.materials.clear();ob.data.materials.append(mat);print('BODY_HIGH_READY',flush=True)"
replace="bpy.ops.wm.open_mainfile(filepath=str(OUT/'QingGuiR02_BodyCheckpoint.blend'));s=bpy.context.scene;ob=bpy.data.objects['QingGuiR02_Body_HIGH'];print('BODY_CHECKPOINT_REUSED',flush=True)"
assert start in text
exec(compile(text.replace(start,replace),str(P/'build_source.py'),'exec'))
