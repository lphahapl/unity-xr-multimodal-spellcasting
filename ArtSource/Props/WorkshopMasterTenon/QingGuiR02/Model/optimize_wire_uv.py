"""UV-only repair. Assert exact positions/topology before copying a single UV value."""
import bpy,sys,json,struct,hashlib
from pathlib import Path
P=Path(__file__).resolve().parent;sys.dont_write_bytecode=True;sys.path.insert(0,str(P));from qinggui_wires import build_wires
LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model';result={}
def sig(ob):
    ob.data.calc_loop_triangles();return hashlib.sha256(b''.join(struct.pack('<3f',*v.co) for v in ob.data.vertices)).hexdigest(),hashlib.sha256(b''.join(struct.pack('<3I',*t.vertices) for t in ob.data.loop_triangles)).hexdigest()
for file in ['QingGuiR02_Source.blend','QingGuiR02.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(P/file));levels=[('HIGH',3)] if '_Source' in file else [('HIGH',3),('LOD0',0),('LOD1',1),('LOD2',2)]
    for suffix,level in levels:
        body=bpy.data.objects[f'QingGuiR02_Body_{suffix}'];old=bpy.data.objects[f'QingGuiR02_GoldWire_{suffix}'];new,paths=build_wires(body,'UV_ONLY_TEMP',level)
        assert sig(old)==sig(new),(suffix,sig(old),sig(new))
        for a,b in zip(old.data.uv_layers.active.data,new.data.uv_layers.active.data):a.uv=b.uv
        result[f'{file}/{suffix}']={'exact_geometry_unchanged':True,'coordinate_topology_sha256':sig(old)};bpy.data.objects.remove(new,do_unlink=True)
    if file=='QingGuiR02.blend':
        lows=[bpy.data.objects[f'QingGuiR02_{kind}_LOD{l}'] for l in range(3) for kind in ['Body','GoldWire']]
        bpy.ops.object.select_all(action='DESELECT')
        for o in lows[:2]:o.hide_set(False);o.select_set(True)
        bpy.context.view_layer.objects.active=lows[0]
        bpy.ops.export_scene.fbx(filepath=str(P/'QingGuiR02_LOD0.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,path_mode='COPY')
        bpy.ops.export_scene.gltf(filepath=str(P/'QingGuiR02.glb'),export_format='GLB',use_selection=True,export_tangents=True)
        for o in lows:o.hide_set(False);o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(P/'QingGuiR02_LODs.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,path_mode='COPY')
        for o in lows[2:]:o.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(P/file))
(LOG/'wire-uv-only-optimization.json').write_text(json.dumps(result,indent=2),'utf-8');print('EXACT_GEOMETRY_UV_ONLY_PASS',flush=True)
