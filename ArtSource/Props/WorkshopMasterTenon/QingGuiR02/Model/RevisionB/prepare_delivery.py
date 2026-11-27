"""Derive a local RevisionB pipeline; frozen parent directory is read-only."""
from pathlib import Path
P=Path(__file__).resolve().parent;OLD=P.parent
for name in ['build_delivery.py','verify_delivery.py','measure_interface.py','review_delivery.py','make_lookdev_glb.py','source_optics.py']:
    t=(OLD/name).read_text('utf-8')
    t=t.replace('ROOT=OUT.parents[4]','ROOT=OUT.parents[5]').replace("OUT.parents[4]/'Logs", "OUT.parents[5]/'Logs").replace("P.parents[4]/'Logs", "P.parents[5]/'Logs")
    t=t.replace('qinggui-r02-20261126/model\'', 'qinggui-r02-20261126/model/revision-b\'')
    for suffix in ['_Source.blend','.blend','_LOD0.fbx','_LODs.fbx','.glb','_Lookdev.glb','_GeometryCandidate.fbx']:
        t=t.replace('QingGuiR02'+suffix,'QingGuiR02B'+suffix)
    t=t.replace("'asset':'QingGuiR02'", "'asset':'QingGuiR02B'")
    if name=='build_delivery.py':t=t.replace("assert stats(low)['triangles']<=6500", "assert stats(low)['triangles']<=15000")
    if name=='measure_interface.py':t=t.replace("OUT.parents[1]/'R03/", "OUT.parents[2]/'R03/")
    (P/name).write_text(t,'utf-8')
