import bpy, json, os
from pathlib import Path
from mathutils import Vector

root=Path(os.environ.get('MODULAR_BOARD_OUT',str(Path(__file__).resolve().parent.parent/'棋盘模型-v1')))
report=[]
for ext in ('fbx','glb'):
    for path in sorted((root/ext.upper()).glob('*.'+ext)):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if ext=='fbx': bpy.ops.import_scene.fbx(filepath=str(path))
        else: bpy.ops.import_scene.gltf(filepath=str(path))
        bpy.context.view_layer.update()
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
        assert meshes, str(path)
        assert not [o for o in bpy.context.scene.objects if o.type in ('CAMERA','LIGHT')]
        pts=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
        size=[max(v[a] for v in pts)-min(v[a] for v in pts) for a in range(3)]
        # Blender importers both restore Blender's Z-up orientation.
        expected=10 if path.stem.startswith('Board_') else 1
        assert abs(size[0]-expected)<.0001 and abs(size[1]-expected)<.0001,(path.name,size)
        assert abs(min(v.z for v in pts))<.0001,(path.name,'pivot')
        for o in meshes:
            assert o.data.materials and all(m is not None for m in o.data.materials),(path.name,o.name,'missing materials')
            for p in o.data.polygons:
                assert p.material_index<len(o.data.materials),(path.name,o.name,'bad material index')
        mats=sorted({m.name.split('.')[0] for o in meshes for m in o.data.materials})
        if expected==10:
            assert len([o for o in bpy.context.scene.objects if o.type=='EMPTY' and o.name.startswith('Tile_')])==36
            tiles=sorted([o for o in bpy.context.scene.objects if o.type=='EMPTY' and o.name.startswith('Tile_')],key=lambda o:int(o.name[5:7]))
            for i,tile in enumerate(tiles):
                expected_direction=(tiles[(i+1)%len(tiles)].matrix_world.translation-tile.matrix_world.translation).normalized()
                actual=(tile.matrix_world.to_3x3()@Vector((1,0,0))).normalized()
                up=(tile.matrix_world.to_3x3()@Vector((0,1,0))).normalized()
                # FBX frames compensate Unity's local X mirror; glTF keeps native handedness.
                expected_dot=-1 if ext=='fbx' else 1
                assert actual.dot(expected_direction)*expected_dot>.99999,(path.name,tile.name,'+X wrong')
                assert up.dot(Vector((0,0,1)))>.99999,(path.name,tile.name,'+Y wrong')
                if root.name.startswith('棋盘模型-v3'):
                    for part in tile.children:
                        if part.type=='MESH':
                            part_right=(part.matrix_world.to_3x3()@Vector((1,0,0))).normalized()
                            part_up=(part.matrix_world.to_3x3()@Vector((0,1,0))).normalized()
                            assert part_right.dot(expected_direction)*expected_dot>.99999,(path.name,tile.name,part.name,'part +X wrong')
                            assert part_up.dot(Vector((0,0,1)))>.99999,(path.name,tile.name,part.name,'part +Y wrong')
            for mat in ('MB_Base','MB_Ivory','MB_Red','MB_Blue','MB_Green','MB_Purple','MB_Yellow','MB_Gold','MB_Center'):
                assert mat in mats,(path.name,'missing '+mat,mats)
        report.append({'file':str(path.relative_to(root)),'size_m':size,'mesh_count':len(meshes),'materials':mats,'passed':True})
(root/'export-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('VALIDATED',len(report),'EXPORTED MODELS')
