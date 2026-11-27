"""Author the A2 +X retaining shoulder against the actual Unity altar mesh.
Run: blender -b --factory-startup -P build_landing_shoulder.py -- <project-root>
"""
from pathlib import Path
import re
import sys
import math
import random
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(sys.argv[sys.argv.index('--') + 1])
src = root / 'ArtSource/Environment/SuspendedPath/ApprovedBasket'
assets = root / 'Assets/Art/Environment/SuspendedPath'
dst = assets / 'ApprovedBasket'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.scene.unit_settings.system = 'METRIC'

# Unity serializes this project's readable mesh using a single stream with a 48-byte stride.
yaml = (assets / 'SuspendedPathGeometry.asset').read_text(encoding='utf8')
mesh = next(section for section in yaml.split('--- !u!') if r'\u5929\u5811\u53F0 ' in section)
data = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)', mesh)[1])
positions = np.frombuffer(data, '<f4').reshape(-1, 12)[:, :3]
assert len(positions) == int(re.search(r'm_VertexCount: (\d+)', mesh)[1])
indices = np.frombuffer(bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]+)', mesh)[1]), '<u2')
boundaries = [int(i) // 2 for i in re.findall(r'firstByte: (\d+)', mesh)]
assert boundaries == [0, 36, 918, 1290]
triangles = indices[boundaries[2]:].reshape(-1, 3)
cliff = BVHTree.FromPolygons(positions.tolist(), triangles.tolist(), all_triangles=True)

def surface(y, z):
    hit = cliff.ray_cast(Vector((31., y, z)), Vector((-1, 0, 0)), 12)
    return hit[0].x if hit[0] else None

def mapped(x, y, z):
    return (-x, z, y)

mat = bpy.data.materials.new('Ashlar')
mat.use_nodes = True
nodes = mat.node_tree.nodes
bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
base = nodes.new('ShaderNodeTexImage')
base.image = bpy.data.images.load(str(assets/'Surfaces/Workshop/Ashlar_BaseSmooth.png'))
normal = nodes.new('ShaderNodeTexImage')
normal.image = bpy.data.images.load(str(assets/'Surfaces/Workshop/Ashlar_NormalGL.png'))
normal.image.colorspace_settings.name = 'Non-Color'
convert = nodes.new('ShaderNodeNormalMap')
mat.node_tree.links.new(base.outputs['Color'], bsdf.inputs['Base Color'])
mat.node_tree.links.new(normal.outputs['Color'], convert.inputs['Color'])
mat.node_tree.links.new(convert.outputs['Normal'], bsdf.inputs['Normal'])

# Row zero sits below the walkable cap. Real modelled rubble projects only from the existing face.
courses = [-.48, -.91, -1.38, -1.9, -2.48, -3.08, -3.74, -4.47]
snapshots = []
for tier in range(3):
    verts, faces, uv_offsets = [], [], []
    count = 0
    def face(corners, seed):
        faces.append(corners)
        uv_offsets.append(seed)

    for row, (top, bottom) in enumerate(zip(courses, courses[1:])):
        rng = random.Random(3701 + row)
        z0 = .37
        while z0 < 21.69:
            z1 = min(z0 + rng.uniform(.72, 1.51), 21.69)
            length = z1 - z0
            za, zb = z0 + .015, z1 - .015
            z0 = z1
            chamfer = rng.uniform(.04, .125)
            ch2 = rng.uniform(.035, .1)
            stagger = rng.uniform(-.014, .014)
            depth = rng.uniform(.105, .185)
            uv_seed = (rng.random(), rng.random())
            top_y = top - .012 + stagger
            bottom_y = bottom + .011 + stagger
            if length < .26 or row > 2 + int(2.65 + 1.4*math.sin((za+zb)*.49)) or row > (6, 5, 3)[tier]:
                continue
            # Cut-back corners, deeper weathering on lower courses. Front and back follow the actual rock.
            p = [(za+chamfer,bottom_y),(zb-ch2,bottom_y),(zb,bottom_y+ch2),
                 (zb,top_y-chamfer*.7),(zb-chamfer,top_y),(za+ch2,top_y),
                 (za,top_y-ch2),(za,bottom_y+chamfer*.65)]
            if tier == 2:
                p = [(za,bottom_y),(zb,bottom_y),(zb,top_y),(za,top_y)]
            samples = [surface(y,z) for z,y in p]
            if any(x is None for x in samples):
                continue
            n = len(p)
            base_index = len(verts)
            midz, midy = .5*(za+zb), .5*(top_y+bottom_y)
            for i, (z,y) in enumerate(p):
                verts.append(mapped(samples[i] - .09, y, z))
            for i, (z,y) in enumerate(p):
                verts.append(mapped(min(27.982, samples[i] + depth*.62), y, z))
            for i in range(n):
                j = (i+1) % n
                face([base_index+i, base_index+j, base_index+n+j, base_index+n+i],uv_seed)
            face([base_index+i for i in range(n)],uv_seed)
            if tier == 0:
                for i, (z,y) in enumerate(p):
                    yy,zz = y*.90+midy*.10,z*.95+midz*.05
                    verts.append(mapped(min(27.982, surface(yy,zz)+depth), yy, zz))
                for i in range(n):
                    j = (i+1) % n
                    face([base_index+n+i,base_index+n+j,base_index+2*n+j,base_index+2*n+i],uv_seed)
                start=base_index+2*n
            else:
                start=base_index+n
            cy = midy+.037
            cz = midz-.067
            center = len(verts)
            verts.append(mapped(min(27.982, surface(cy,cz)+depth*1.14),cy,cz))
            for i in range(n):
                face([start+i,start+(i+1)%n,center],uv_seed)
            count += 1
    name = 'LandingShoulder_LOD'+str(tier)
    geom = bpy.data.meshes.new(name)
    geom.from_pydata(verts,[],faces)
    geom.update()
    # Give each individual stone coherent planar texture coordinates, including its chipped edges.
    uvs = geom.uv_layers.new(name='Stone_m')
    for poly, (u,v) in zip(geom.polygons, uv_offsets):
        for idx in poly.loop_indices:
            x,z,y = geom.vertices[geom.loops[idx].vertex_index].co
            if abs(poly.normal.z) > .68:
                uv = (z/1.75+u, -x/1.75+v)
            elif abs(poly.normal.y) > .70:
                uv = (-x/1.75+u, y/1.75+v)
            else:
                uv = (z/1.75+u,y/1.75+v)
            uvs.data[idx].uv = uv
    bm=bmesh.new()
    bm.from_mesh(geom)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
    bmesh.ops.triangulate(bm,faces=bm.faces[:])
    bm.to_mesh(geom)
    bm.free()
    geom.update()
    geom.materials.append(mat)
    ob=bpy.data.objects.new(name,geom)
    bpy.context.scene.collection.objects.link(ob)
    snapshots.append(ob)
    geom.calc_loop_triangles()
    print('SHOULDER',name,count,'blocks',len(geom.loop_triangles),'triangles')

for label, point in [('Origin',(0,0,0)),('X',(-1,0,0)),('Y',(0,0,1)),('Z',(0,1,0))]:
    empty=bpy.data.objects.new('Frame'+label,None)
    bpy.context.scene.collection.objects.link(empty)
    empty.location=point
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(dst/'LandingShoulder.fbx'), use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',use_tspace=True,mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE')
for ob in snapshots[1:]:
    ob.hide_set(True)
    ob.hide_render=True
for im in (base.image,normal.image):
    im.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(src/'LandingShoulder.blend'),compress=True)
