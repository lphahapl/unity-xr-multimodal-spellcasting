import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

WORK = Path(__file__).parent
OUT = Path(sys.argv[sys.argv.index("--") + 1])
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def mesh_object(name, verts, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def extruded_outline(name, xy, zmin, zmax):
    signed_area = sum(x*y2-x2*y for (x,y),(x2,y2) in zip(xy, xy[1:]+xy[:1]))
    if signed_area < 0:
        xy = xy[::-1]
    n = len(xy)
    verts = [(x, y, z) for z in (zmin, zmax) for x,y in xy]
    faces = [tuple(range(n-1, -1, -1)), tuple(range(n, 2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh_object(name, verts, faces)


def arc(cx, cy, radius, start, end, count=12):
    angles = [math.radians(start + (end-start)*i/count) for i in range(count+1)]
    return [(cx+radius*math.cos(a),cy+radius*math.sin(a)) for a in angles]


def rounded_rect(x0, x1, y0, y1, r):
    return (arc(x1-r,y0+r,r,-90,0) + arc(x1-r,y1-r,r,0,90)
            + arc(x0+r,y1-r,r,90,180) + arc(x0+r,y0+r,r,180,270))


def apply(obj, mod):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def combine(obj, operand, operation):
    mod = obj.modifiers.new(operation, 'BOOLEAN')
    mod.operation = operation
    mod.solver = 'EXACT'
    mod.object = operand
    apply(obj, mod)
    bpy.data.objects.remove(operand, do_unlink=True)


outline = [(0,-4.8),(15,-11),(69,-11),(70,-10)]
outline += arc(80.1,-5.1,4.9,-90,0)
outline += arc(80.1,5.1,4.9,0,90)
outline += [(70,10),(69,11)]
outline += arc(62.8,11,1.6,0,-180)
outline += [(15,11),(0,4.8)]
part = extruded_outline('方圆规印', outline, -.9, .9)
hole = extruded_outline('尾环开口', rounded_rect(72.1,82.7,-7.1,7.1,2.4), -3, 3)
combine(part, hole, 'DIFFERENCE')


def guide(name, side, a, b, taper):
    section = [(10.5,-.51),(11.8,-.76),(11.8,.76),(10.5,.51)]
    stations = [(a, .38), (a+taper, 1), (b-.43, 1), (b, .53)]
    verts = [(x, side*(10.5+(y-10.5)*width), z) for x,width in stations for y,z in section]
    faces = [(3,2,1,0),(12,13,14,15)]
    for j in range(0,12,4):
        for i in range(4):
            k=(i+1)%4
            faces.append((j+i,j+k,j+k+4,j+i+4))
    if side < 0:
        faces = [f[::-1] for f in faces]
    return mesh_object(name, verts, faces)


for guide_params in [('上缘滑棱',1,15,61.05,2.4),('榫口后滑棱',1,64.55,69,0.45),('下缘滑棱',-1,15,69,2.4)]:
    combine(part, guide(*guide_params), 'UNION')

if '--check-geometry' in sys.argv:
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(part.data)
    nonmanifold = sum(not edge.is_manifold for edge in bm.edges)
    span = [max(v.co[i] for v in bm.verts) - min(v.co[i] for v in bm.verts)
            for i in range(3)]
    print(f'Envelope mm: {span}, nonmanifold edges: {nonmanifold}')
    assert nonmanifold == 0 and all(abs(actual - expected) < .01
                                   for actual, expected in zip(span, [85, 23.6, 1.8]))
    assert not part.ray_cast((77.4,0,4), (0,0,-1))[0], 'Loop aperture is occluded'
    assert part.ray_cast((72,8.8,4), (0,0,-1))[0], 'Loop bridge missing'
    bm.free()
    sys.exit(0)

bevel = part.modifiers.new('Rounded working edges', 'BEVEL')
bevel.width = .16
bevel.segments = 3
bevel.affect = 'EDGES'
bevel.angle_limit = math.radians(19)
bevel.harden_normals = True
part.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
for poly in part.data.polygons:
    poly.use_smooth = True

uv = part.data.uv_layers.new(name='Face stamp')
for poly in part.data.polygons:
    for li in poly.loop_indices:
        x,y,z = part.data.vertices[part.data.loops[li].vertex_index].co
        uv.data[li].uv = (x/85, (y+12)/24) if poly.normal.z > .8 and z>.6 else (0,0)

bronze = bpy.data.materials.new('Aged matte bronze')
bronze.use_nodes = True
part.data.materials.clear()
part.data.materials.append(bronze)
nt = bronze.node_tree
n,l = nt.nodes,nt.links
bs = n.get('Principled BSDF')
bs.inputs['Metallic'].default_value = .78
bs.inputs['Roughness'].default_value = .34
coord = n.new('ShaderNodeTexCoord')
grain = n.new('ShaderNodeTexNoise')
grain.inputs['Scale'].default_value = .7
grain.inputs['Detail'].default_value = 4
l.new(coord.outputs['Object'],grain.inputs['Vector'])
ramp = n.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].position = .19
ramp.color_ramp.elements[0].color = (.15,.091,.037,1)
ramp.color_ramp.elements[1].position = .86
ramp.color_ramp.elements[1].color = (.34,.213,.097,1)
ramp.color_ramp.elements.new(.48).color = (.244,.148,.065,1)
l.new(grain.outputs['Fac'],ramp.inputs[0])
tex = n.new('ShaderNodeTexImage')
tex.image = bpy.data.images.load(str(WORK/'engraving_mask.png'))
tex.image.colorspace_settings.name = 'Non-Color'
tex.extension = 'EXTEND'
l.new(coord.outputs['UV'],tex.inputs['Vector'])
mix = n.new('ShaderNodeMixRGB')
mix.inputs[2].default_value = (.057,.030,.011,1)
l.new(tex.outputs['Color'],mix.inputs[0])
l.new(ramp.outputs['Color'],mix.inputs[1])
l.new(mix.outputs[0],bs.inputs['Base Color'])
pits = n.new('ShaderNodeTexNoise')
pits.inputs['Scale'].default_value = 13
pits.inputs['Detail'].default_value = 2
l.new(coord.outputs['Object'],pits.inputs['Vector'])
micro = n.new('ShaderNodeBump')
micro.inputs['Strength'].default_value = .22
micro.inputs['Distance'].default_value = .027
l.new(pits.outputs['Fac'],micro.inputs['Height'])
stamp = n.new('ShaderNodeBump')
stamp.invert = True
stamp.inputs['Strength'].default_value = .72
stamp.inputs['Distance'].default_value = .115
l.new(tex.outputs['Color'],stamp.inputs['Height'])
l.new(micro.outputs[0],stamp.inputs['Normal'])
l.new(stamp.outputs[0],bs.inputs['Normal'])

scene.world = bpy.data.worlds.new('Studio fill')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.44,.47,.52,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .7
scene.render.film_transparent = True
scene.render.engine = 'CYCLES'
scene.render.threads_mode = 'FIXED'
scene.render.threads = 5
scene.cycles.samples = 10
scene.cycles.adaptive_threshold = .055
scene.cycles.use_denoising = True
scene.render.resolution_x = scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'
scene.render.dither_intensity = 0
scene.view_settings.view_transform = 'AgX'

scene.use_nodes = True
if bpy.app.version < (5, 0, 0):
    compositor = scene.node_tree
    compositor.nodes.clear()
    layer = compositor.nodes.new('CompositorNodeRLayers')
    over = compositor.nodes.new('CompositorNodeAlphaOver')
    over.inputs[1].default_value = (.57,.57,.57,1)
    compositor.links.new(layer.outputs['Image'],over.inputs[2])
    compositor.links.new(over.outputs['Image'],compositor.nodes.new('CompositorNodeComposite').inputs['Image'])
else:
    compositor = scene.compositing_node_group = bpy.data.node_groups.new('Neutral background', 'CompositorNodeTree')
    compositor.interface.new_socket(name='Image', in_out='OUTPUT', socket_type='NodeSocketColor')
    nodes = compositor.nodes
    layer = nodes.new('CompositorNodeRLayers')
    over = nodes.new('CompositorNodeAlphaOver')
    over.inputs['Background'].default_value = (.57,.57,.57,1)
    compositor.links.new(layer.outputs['Image'], over.inputs['Foreground'])
    output = nodes.new('NodeGroupOutput')
    compositor.links.new(over.outputs['Image'], output.inputs['Image'])
    
center = Vector((42.5,0,0))
camera_data = bpy.data.cameras.new('Orthographic')
cam = bpy.data.objects.new('Orthographic',camera_data)
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = 'ORTHO'
cam.data.clip_end = 1000


def light(name, size, power):
    data = bpy.data.lights.new(name, 'AREA')
    data.shape = 'DISK'
    data.size = size
    data.energy = power
    obj = bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    return obj


key = light('Diffuse key', 115, 20000)
fill = light('Fill card', 130, 9000)
rim = light('Edge card', 65, 10500)


def place_view(axis, up, scale):
    forward, vertical = Vector(axis).normalized(), Vector(up)
    cam.location = center + forward * 145
    right = vertical.cross(forward).normalized()
    vertical = forward.cross(right).normalized()
    from mathutils import Matrix
    cam.rotation_euler = Matrix((right, vertical, forward)).transposed().to_euler()
    cam.data.ortho_scale = scale
    for lamp, (h,v,d) in zip((key,fill,rim),((-33,45,90),(40,-25,95),(4,74,35))):
        lamp.location = center + right*h + vertical*v + forward*d
        lamp.rotation_euler = (center-lamp.location).to_track_quat('-Z','Y').to_euler()


views = [
    ('01_正面.png',(0,0,1),(0,1,0)),
    ('02_左前45度.png',(-1,0,1),(0,1,0)),
    ('03_右前45度.png',(1,0,1),(0,1,0)),
    ('04_左面.png',(-1,0,0),(0,1,0)),
    ('05_右面.png',(1,0,0),(0,1,0)),
    ('06_背面.png',(0,0,-1),(0,1,0)),
    ('07_顶面.png',(0,1,0),(0,0,-1)),
    ('08_底面.png',(0,-1,0),(0,0,1)),
]
selected = next((arg.split('=', 1)[1].split(',') for arg in sys.argv
                 if arg.startswith('--views=')), [])
for name,axis,up in views:
    if selected and name[:2] not in selected:
        continue
    place_view(axis,up,99)
    if '--preview' in sys.argv:
        print('Material nodes', [(node.name, node.type) for node in n], flush=True)
        print('Material links', [(link.from_node.name, link.to_node.name, link.is_valid) for link in l], flush=True)
        print('Material IDs', [m.name if m else None for m in part.data.materials], set(p.material_index for p in part.data.polygons), flush=True)
        print('Modifier material IDs', set(p.material_index for p in part.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.polygons), flush=True)
        scene.render.resolution_percentage = 48
        scene.cycles.samples = 4
        scene.render.filepath = str(WORK/'material_preview.png')
        bpy.ops.render.render(write_still=True)
        break
    scene.render.filepath = str(OUT/name)
    bpy.ops.render.render(write_still=True)
