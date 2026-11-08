"""Ensure the new movement frames did not move the visible board geometry."""
import bpy, json, os
from pathlib import Path
from mathutils.kdtree import KDTree
base=Path(__file__).resolve().parent.parent
output=Path(os.environ.get('MODULAR_BOARD_OUT',str(base/'棋盘模型-v2-X轴前进')))
def read(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    result={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH': continue
        key=obj.data.materials[0].name.split('.')[0]
        result.setdefault(key,[]).extend(obj.matrix_world@v.co for v in obj.data.vertices)
    return result
before=read(base/'棋盘模型-v1/FBX/Board_10m_36Tiles.fbx')
after=read(output/'FBX/Board_10m_36Tiles.fbx')
assert set(before)==set(after)
errors={}
for material in before:
    assert len(before[material])==len(after[material]),material
    max_error=0
    for source,target in ((before[material],after[material]),(after[material],before[material])):
        tree=KDTree(len(target))
        for i,v in enumerate(target): tree.insert(v,i)
        tree.balance()
        max_error=max(max_error,max(tree.find(v)[2] for v in source))
    assert max_error<.0001,(material,max_error)
    errors[material]=max_error
(output/'appearance-validation.json').write_text(json.dumps({'pass':True,'max_vertex_shift_by_material_m':errors},indent=2),encoding='utf-8')
print('APPEARANCE_UNCHANGED max displacement:',max(errors.values()),'m')
