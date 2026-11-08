"""Build the approved modular board with Blender. All geometry is authored in metres."""
import bpy, bmesh, math, json, os
from mathutils import Vector, Matrix
from pathlib import Path

OUT = Path(os.environ.get('MODULAR_BOARD_OUT', str(Path(__file__).resolve().parent.parent / '棋盘模型-v1')))
STEM = 'ModularBoard_v3' if OUT.name.startswith('棋盘模型-v3') else ('ModularBoard_v2' if OUT.name.startswith('棋盘模型-v2') else 'ModularBoard_v1')
SKIP_RENDER = os.environ.get('MODULAR_BOARD_SKIP_RENDER') == '1'
for folder in ('Blender', 'FBX', 'GLB', '预览', 'Unity/Assets/ModularBoard/Models', 'Unity/Assets/ModularBoard/Editor'):
    (OUT / folder).mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = 'METERS'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 40
scene.cycles.use_denoising = True
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.22, .22, .22)
scene.view_settings.view_transform = 'AgX'
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = -.35

def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

LIB = collection('Tile_Library_Origin_Centered')
BOARD = collection('Board_36_Tiles')
STUDIO = collection('Preview_Only')
SHOW = collection('Module_Showcase_Preview_Only')

def move(obj, col):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    col.objects.link(obj)
    return obj

COLORS = {
 'Base': (.105,.12,.145), 'Ivory': (.81,.78,.70),
 'Red': (.78,.12,.065), 'Green': (.08,.52,.22), 'Blue': (.055,.31,.78),
 'Yellow': (.92,.60,.045), 'Purple': (.52,.16,.68),
 'Gold': (.86,.51,.065), 'GoldInset': (.63,.32,.025),
 'Center': (.155,.18,.215), 'Backdrop': (.41,.445,.49), 'Label': (.075,.09,.115)
}
MATS = {}
for name, color in COLORS.items():
    mat = bpy.data.materials.new('MB_' + name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .46 if name in ('Gold','GoldInset') else .64
    bsdf.inputs['Metallic'].default_value = .45 if name in ('Gold','GoldInset') else 0
    MATS[name] = mat

def empty(name, col, parent=None):
    obj = bpy.data.objects.new(name, None)
    col.objects.link(obj)
    obj.parent = parent
    obj.empty_display_size = .15
    obj.empty_display_type = 'ARROWS'
    return obj

def finish(obj, name, mat, col, parent=None, bevel=0):
    obj.name = name
    move(obj, col)
    obj.parent = parent
    obj.data.materials.append(MATS[mat])
    if bevel:
        mod = obj.modifiers.new('Manufactured_Edge_Bevel', 'BEVEL')
        mod.width, mod.segments = bevel, 3
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons: poly.use_smooth = False
    mod = obj.modifiers.new('Weighted_Corner_Normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    bpy.context.view_layer.objects.active = obj
    try: bpy.ops.object.modifier_apply(modifier=mod.name)
    except RuntimeError: pass
    return obj

def box(name, size, pos, mat, col, parent=None, bevel=.009):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, col, parent, bevel)

def cylinder(name, radius, depth, z, mat, parent):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=depth, location=(0,0,z))
    return finish(bpy.context.object, name, mat, LIB, parent, .003)

def extrude_polygon(name, points, z, depth, mat, parent):
    n = len(points)
    verts = [(x,y,z) for x,y in points] + [(x,y,z+depth) for x,y in points]
    faces = [tuple(reversed(range(n))), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new(name,mesh); LIB.objects.link(obj)
    return finish(obj,name,mat,LIB,parent,.004)

def set_frame_preserve_geometry(root, new_basis):
    """Change a tile's local axes while preserving all child geometry in parent space."""
    compensation = new_basis.inverted() @ root.matrix_basis
    for child in root.children:
        child.matrix_basis = compensation @ child.matrix_basis
    root.matrix_basis = new_basis

def bake_part_frames(root):
    """Keep each visible part's axes aligned with its tile without moving geometry."""
    for part in root.children:
        if part.type!='MESH': continue
        basis=part.matrix_basis.copy()
        location=basis.translation.copy()
        linear=basis.copy(); linear.translation=Vector((0,0,0))
        original_normals=[n.vector.copy() for n in part.data.corner_normals] if part.data.has_custom_normals else None
        mesh=part.data.copy()
        part.data=mesh
        mesh.transform(linear)
        mesh.update()
        if original_normals:
            normal_matrix=linear.to_3x3().inverted().transposed()
            mesh.normals_split_custom_set([(normal_matrix@n).normalized() for n in original_normals])
        part.matrix_basis=Matrix.Translation(location)
        part['forward_axis']='+X (same as tile root)'

# A tile's local +Y is the surface normal; Blender scene +Z remains world-up.
upright_frame = Matrix.Rotation(math.pi/2, 4, 'X')
templates = {}
for kind in ('Plain','Red','Green','Blue','Yellow','Purple','Start','Event','Coin'):
    root=empty('Tile_'+kind, LIB)
    root['footprint_m'] = '1 x 1'
    root['pivot'] = 'Bottom center; local +Y up'
    root['forward_axis'] = '+X'
    root['body_height_m'] = .12
    box(kind+'_Base',(1,1,.09),(0,0,.045),'Base',LIB,root,.012)
    box(kind+'_Top',(.982,.982,.03),(0,0,.105),'Ivory',LIB,root,.008)
    if kind in ('Red','Green','Blue','Yellow','Purple'):
        box(kind+'_Strip',(.966,.135,.006),(0,.414,.123),kind,LIB,root,.004)
    if kind=='Start':
        extrude_polygon('Start_Arrow',[(-.29,-.085),(.035,-.085),(.035,-.19),(.29,0),(.035,.19),(.035,.085),(-.29,.085)],.12,.014,'Red',root)
    if kind=='Event':
        bpy.ops.object.text_add(location=(0,0,.12))
        obj=bpy.context.object; obj.data.body='?'; obj.data.align_x='CENTER'; obj.data.align_y='CENTER'
        obj.data.size=.69; obj.data.extrude=.007; obj.data.bevel_depth=.0015; obj.data.bevel_resolution=2
        bpy.ops.object.convert(target='MESH')
        finish(bpy.context.object,'Event_Question','Blue',LIB,root)
    if kind=='Coin':
        cylinder('Coin_Rim',.233,.014,.127,'Gold',root)
        cylinder('Coin_Inset',.194,.004,.136,'GoldInset',root)
        cylinder('Coin_Face',.174,.004,.139,'Gold',root)
    set_frame_preserve_geometry(root, upright_frame)
    templates[kind]=root

def duplicate_tree(src, col, name):
    dst=src.copy(); dst.name=name; col.objects.link(dst)
    for child in src.children:
        dupe=child.copy(); dupe.data=child.data; col.objects.link(dupe); dupe.parent=dst
    return dst

board=empty('Board_10m_36Tiles', BOARD)
board['size_m']='10 x 10'
board['tile_pitch_m']=1.0
board['route_tile_count']=36
coords=[(x,-4.5) for x in [i-4.5 for i in range(10)]]
coords += [(4.5,i-4.5) for i in range(1,10)]
coords += [(i-4.5,4.5) for i in range(8,-1,-1)]
coords += [(-4.5,i-4.5) for i in range(8,0,-1)]
special={0:'Start',5:'Coin',9:'Event',14:'Coin',18:'Start',23:'Coin',27:'Event',32:'Coin'}
manifest=[]
for i,(x,y) in enumerate(coords):
    color=('Blue','Purple','Red','Green')[min(i//9,3)]
    if i in (3,12,21,30): color='Yellow'
    kind=special.get(i,color)
    obj=duplicate_tree(templates[kind],BOARD,f'Tile_{i:02d}_{kind}')
    obj.parent=board
    visual_rot=math.pi if y==-4.5 else (-math.pi/2 if x==4.5 else (0 if y==4.5 else math.pi/2))
    if kind=='Start': visual_rot=0 if i==0 else math.pi
    next_x,next_y=coords[(i+1)%len(coords)]
    forward=Vector((next_x-x,next_y-y,0)).normalized()
    move_rot=math.atan2(forward.y,forward.x)
    translation=Matrix.Translation(Vector((x,y,0)))
    obj.matrix_basis=translation @ Matrix.Rotation(visual_rot,4,'Z') @ upright_frame
    set_frame_preserve_geometry(obj, translation @ Matrix.Rotation(move_rot,4,'Z') @ upright_frame)
    obj['route_index']=i
    obj['next_route_index']=(i+1)%len(coords)
    obj['forward_axis']='+X'
    manifest.append({'index':i,'next_index':(i+1)%len(coords),'type':kind,'position_blender_m':[x,y,0], 'position_unity_m':[x,0,-y],
        'forward_blender':[round(forward.x),round(forward.y),0], 'forward_unity':[round(forward.x),0,round(-forward.y)],
        'forward_axis':'+X','up_axis':'+Y'})
box('Center_Panel_Removable',(8,8,.035),(0,0,.0175),'Center',BOARD,board,.008)
for tile in list(templates.values())+[o for o in board.children if 'route_index' in o]:
    bake_part_frames(tile)

def descendants(root):
    result=[root]
    for child in root.children: result.extend(descendants(child))
    return result

def export(root, stem):
    # FBX's instanced-mesh material lookup can warn about copied object slots.
    # Export temporary single-user mesh data, retaining linked meshes in the source.
    backups=[]
    for obj in descendants(root):
        if obj.type=='MESH' and obj.data.users>1:
            backups.append((obj,obj.data)); obj.data=obj.data.copy()
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in descendants(root): obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    # Unity's FBX importer mirrors local X coordinates when changing handedness.
    # Compensate the tile frames while keeping mesh placement unchanged. The
    # Blender source and glTF retain their native right-handed movement frames.
    fbx_frames={obj:obj.matrix_basis.copy() for obj in descendants(root)}
    fbx_meshes={obj:obj.data for obj in descendants(root) if obj.type=='MESH'}
    fbx_tiles=[obj for obj in descendants(root) if obj.type=='EMPTY' and (obj is root and root.name.startswith('Tile_') or 'route_index' in obj)]
    for tile in fbx_tiles:
        set_frame_preserve_geometry(tile,tile.matrix_basis @ Matrix.Rotation(math.pi,4,'Y'))
        bake_part_frames(tile)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(OUT/'FBX'/f'{stem}.fbx'),use_selection=True,object_types={'EMPTY','MESH'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',global_scale=1,axis_forward='-Z',axis_up='Y',
        bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE',use_custom_props=True,path_mode='AUTO')
    for obj,basis in fbx_frames.items(): obj.matrix_basis=basis
    for obj,mesh in fbx_meshes.items():
        temporary=obj.data; obj.data=mesh
        if temporary!=mesh and temporary.users==0: bpy.data.meshes.remove(temporary)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.gltf(filepath=str(OUT/'GLB'/f'{stem}.glb'),export_format='GLB',use_selection=True,
        export_yup=True,export_extras=True,export_cameras=False,export_lights=False)
    import shutil
    shutil.copy2(OUT/'FBX'/f'{stem}.fbx',OUT/'Unity/Assets/ModularBoard/Models'/f'{stem}.fbx')
    for obj,data in backups:
        temporary=obj.data; obj.data=data; bpy.data.meshes.remove(temporary)

for kind,root in templates.items(): export(root,'Tile_'+kind)
export(board,'Board_10m_36Tiles')
LIB.hide_render=True; LIB.hide_viewport=True

# Verify authored geometry before presentation objects are added.
bpy.context.view_layer.update()
def bounds(root):
    pts=[obj.matrix_world@Vector(corner) for obj in descendants(root) if obj.type=='MESH' for corner in obj.bound_box]
    return [[min(v[a] for v in pts), max(v[a] for v in pts)] for a in range(3)]
checks={'board_bounds_blender_m':bounds(board),'tiles':{},'route_count':len(coords)}
assert len(coords)==36 and len(set(coords))==36
assert all(abs((hi-lo)-10)<1e-5 for lo,hi in checks['board_bounds_blender_m'][:2])
for tile in [c for c in board.children if 'route_index' in c]:
    i=tile['route_index']
    next_xy=coords[(i+1)%len(coords)]
    expected=Vector((next_xy[0]-coords[i][0],next_xy[1]-coords[i][1],0)).normalized()
    actual=(tile.matrix_world.to_3x3() @ Vector((1,0,0))).normalized()
    normal=(tile.matrix_world.to_3x3() @ Vector((0,1,0))).normalized()
    assert actual.dot(expected)>.99999,(tile.name,'bad forward',actual,expected)
    assert normal.dot(Vector((0,0,1)))>.99999,(tile.name,'bad up',normal)
    for part in tile.children:
        if part.type=='MESH':
            right=(part.matrix_world.to_3x3()@Vector((1,0,0))).normalized()
            up=(part.matrix_world.to_3x3()@Vector((0,1,0))).normalized()
            assert right.dot(expected)>.99999,(tile.name,part.name,'part +X wrong')
            assert up.dot(Vector((0,0,1)))>.99999,(tile.name,part.name,'part +Y wrong')
checks['route_forward_axis_validation']='36/36 local +X points to the next tile; local +Y is up'
for kind,root in templates.items():
    bb=bounds(root)
    assert all(abs(hi-lo-1)<1e-5 for lo,hi in bb[:2]),(kind,bb)
    assert abs(bb[2][0])<1e-5
    meshes=[o for o in descendants(root) if o.type=='MESH']
    checks['tiles'][kind]={'bounds_m':bb,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)}
(OUT/'geometry-validation.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
(OUT/'board-layout.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

box('Studio_Floor',(200,200,.04),(0,0,-.045),'Backdrop',STUDIO,None,.003)
def area(name, pos, power, size):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size
    obj=bpy.data.objects.new(name,data); STUDIO.objects.link(obj); obj.location=pos
    obj.rotation_euler=(Vector((0,0,0))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Key',(1,-5,13),1900,8)
area('Fill',(-7,-1,8),1100,7)
area('Rim',(3,8,10),2200,6)
camera_data=bpy.data.cameras.new('Board_Camera'); camera=bpy.data.objects.new('Board_Camera',camera_data)
STUDIO.objects.link(camera); scene.camera=camera
camera_data.type='ORTHO'; camera_data.ortho_scale=16.2
camera.location=(11,-15,17)
camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1800; scene.render.resolution_y=1500
SHOW.hide_render=True
bpy.ops.object.select_all(action='DESELECT'); board.select_set(True); bpy.context.view_layer.objects.active=board
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_distance=17
            a.spaces.active.region_3d.view_location=(0,0,0)
            a.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Blender'/f'{STEM}.blend'))
scene.render.filepath=str(OUT/'预览/01-棋盘整体.png')
if not SKIP_RENDER: bpy.ops.render.render(write_still=True)

BOARD.hide_render=True; SHOW.hide_render=False
for i,(kind,root) in enumerate(templates.items()):
    x=(i%3-1)*1.65; y=(1-i//3)*1.7
    obj=duplicate_tree(root,SHOW,'Preview_'+kind); obj.location=(x,y,0)
    bpy.ops.object.text_add(location=(x,y-.76,.004))
    label=bpy.context.object; label.data.body=kind.upper(); label.data.align_x='CENTER'; label.data.size=.14
    move(label,SHOW); label.data.materials.append(MATS['Label'])
camera.location=(4,-7,10)
camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.ortho_scale=6.8
scene.render.resolution_x=1800; scene.render.resolution_y=1600
scene.render.filepath=str(OUT/'预览/02-单格模块.png')
if not SKIP_RENDER: bpy.ops.render.render(write_still=True)

# Save source showing the assembled board; keep the optional showcase as a disabled collection.
SHOW.hide_render=True; SHOW.hide_viewport=True; BOARD.hide_render=False
camera.location=(11,-15,17); camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.ortho_scale=16.2
scene.render.resolution_x=1800; scene.render.resolution_y=1500
scene.render.filepath=str(OUT/'预览/01-棋盘整体.png')
scene.render.film_transparent=False
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Blender'/f'{STEM}.blend'))
print('MODULAR_BOARD_DONE',str(OUT))
