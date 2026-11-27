from pathlib import Path
P=Path(__file__).resolve().parent;OLD=P.parent
t=(OLD/'build_source.py').read_text('utf-8')
t=t.replace('ROOT=OUT.parents[4]','ROOT=OUT.parents[5]').replace("LOG=ROOT/'Logs/workshop-master-tenon-qinggui-r02-20261126/model'", "LOG=ROOT/'Logs/workshop-master-tenon-qinggui-r02-20261126/model/revision-b'")
t=t.replace('QingGuiR02_Source.blend','QingGuiR02B_Source.blend').replace('QingGuiR02_BodyCheckpoint.blend','QingGuiR02B_BodyCheckpoint.blend')
# Ship the local edge close-up first; full view and QA follow in the same run.
t=t.replace("render('01_Front45',(.48,-.72,.40))", "render('04_Shoulder',(.18,-.40,.20),(0,0,.115),.19)\nrender('04b_CopperJadeClose',(.18,-.40,.20),(.008,-.043,.130),.075)\nrender('01_Front45',(.48,-.72,.40))")
(P/'build_source.py').write_text(t,'utf-8')
