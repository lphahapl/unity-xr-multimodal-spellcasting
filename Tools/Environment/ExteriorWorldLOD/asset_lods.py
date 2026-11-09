"""Blender background: preserve source assets, derive textured LODs and separate the intact circle."""
from pathlib import Path
import bpy, bmesh, json, re, uuid, math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path('D:/UnityProjects/NarutoVR')
OUT=ROOT/'Assets/Art/Environment/Opening/ExteriorLOD'
LOG=ROOT/'Logs/exterior-world-lod-20261109/models'
OUT.mkdir(parents=True,exist_ok=True); LOG.mkdir(parents=True,exist_ok=True)
def export(objects,path):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE',use_custom_props=False)
def meta(path,template):
    target=Path(str(path)+'.meta')
    if target.exists():return
    text=Path(str(template)+'.meta').read_text(encoding='utf-8-sig')
    text=re.sub(r'^guid: [a-f0-9]+','guid: '+uuid.uuid4().hex,text,count=1,flags=re.M)
    target.write_text(text,encoding='utf-8')
report={'blender':bpy.app.version_string,'lods':[]}
for asset in ['StrataRock','MountainPine','PortalGate']:
    src=ROOT/f'Assets/Art/Environment/Opening/{asset}/{asset}.fbx'
    for level,ratio in [(1,.46),(2,.19)]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(src))
        obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
        before=after=0
        for ob in obs:
            bpy.context.view_layer.objects.active=ob
            ob.data.calc_loop_triangles();before+=len(ob.data.loop_triangles)
            mod=ob.modifiers.new('Static LOD collapse','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=mod.name)
            ob.data.calc_loop_triangles();after+=len(ob.data.loop_triangles)
        dst=OUT/f'{asset}_LOD{level}.fbx';export(obs,dst);meta(dst,src)
        report['lods'].append(dict(asset=asset,level=level,sourceTriangles=before,triangles=after,uv='existing baked UV retained',textures='existing atlas reused',algorithm='Blender DECIMATE COLLAPSE'))
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Environment/Opening/BrokenArmillary/BrokenArmillary.blend'),load_ui=False,use_scripts=False)
low=bpy.data.objects.get('BrokenArmillary');assert low and low.type=='MESH'
assert low.data.attributes.get('ProjectionPair')
all_low=[low]
for name,retain in [('StationaryFrame',False),('IntactCircle',True)]:
    ob=low.copy();ob.data=low.data.copy();bpy.context.scene.collection.objects.link(ob);ob.name=name
    bm=bmesh.new();bm.from_mesh(ob.data);layer=bm.verts.layers.int.get('ProjectionPair');assert layer
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if (v[layer]==2)!=retain],context='VERTS')
    bm.to_mesh(ob.data);bm.free()
    all_low.append(ob)
frame,ring=all_low[1:]
center=Vector((0,0,1.46))
U=Vector((.52,0,-.8542)).normalized();V=Vector((.8542*.41,-math.sqrt(1-.41**2),.52*.41)).normalized()
normal=U.cross(V).normalized()
# The original partial horizon bow intersects the intact band even at rest.
# Recess only that fixed bow to provide real radial clearance for rotation.
for v in frame.data.vertices:
    if frame.data.attributes['ProjectionPair'].data[v.index].value==3:
        v.co=center+(v.co-center)*.93
# Two fixed bearing shoes bridge the meridian frame to the rotating circle.
# 3.5 mm radial running clearance; the ring rotates in its existing tilted plane.
bm=bmesh.new();bm.from_mesh(frame.data);uvlayer=bm.loops.layers.uv.active
ids=bm.verts.layers.int.get('ProjectionPair')
atlas=ring.data.uv_layers.active.data
uvpoint=sum((atlas[i].uv for i in list(ring.data.polygons[0].loop_indices)),Vector((0,0)))/len(ring.data.polygons[0].loop_indices)
for side in [-1,1]:
    origin=center+U*(side*1.12)
    corners=[(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]
    verts=[bm.verts.new(origin+U*(x*.039)+V*(y*.0375)+normal*(z*.045)) for x,y,z in corners]
    for v in verts:v[ids]=13
    for face in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(3,7,6,2),(0,1,5,4)]:
        f=bm.faces.new([verts[i] for i in face]);f.material_index=0
        for j,loop in enumerate(f.loops):loop[uvlayer].uv=uvpoint+Vector([(0,0),(.006,0),(.006,.006),(0,.006)][j])
bm.normal_update();bm.to_mesh(frame.data);bm.free()
# Rotation about the circle's own axle preserves its tilted plane and support clearance.
def bvh(ob,matrix=Matrix.Identity(4)):
    ob.data.calc_loop_triangles()
    return BVHTree.FromPolygons([matrix@v.co for v in ob.data.vertices],[tuple(t.vertices) for t in ob.data.loop_triangles],all_triangles=True)
fixed=bvh(frame);collisions=[]
groups={}
for ti,ri in fixed.overlap(bvh(ring)):
    group=frame.data.attributes['ProjectionPair'].data[frame.data.loop_triangles[ti].vertices[0]].value
    groups[str(group)]=groups.get(str(group),0)+1
report['initialIntersectionGroups']=groups
axis_candidates={ 'normal':normal, 'retained_axle':Vector((1.33,0,.58)).normalized(), 'diameterU':U, 'diameterV':V }
axis_results={}
for key,axis in axis_candidates.items():
    bad=0
    for degree in range(0,360,5):
        m=Matrix.Translation(center)@Matrix.Rotation(math.radians(degree),4,axis)@Matrix.Translation(-center)
        bad+=len(fixed.overlap(bvh(ring,m)))
    axis_results[key]=bad
report['axisSweepTrianglePairs']=axis_results
for degree in range(0,360,5):
    m=Matrix.Translation(center)@Matrix.Rotation(math.radians(degree),4,normal)@Matrix.Translation(-center)
    count=len(fixed.overlap(bvh(ring,m)))
    if count:collisions.append(dict(degrees=degree,trianglePairs=count))
report['armillary']={'pivotBlender':list(center),'rotationAxisBlender':list(normal),'rotation':'intact circle rotates in its existing tilted plane between two fixed bearing shoes; broken frame, partial bow and axle remain stationary','fixedBowRadialScale':.93,'bearingShoes':2,'nominalRunningClearanceMeters':.0035,'collisionSweep5Degrees':collisions}
for v in ring.data.vertices:v.co-=center
ring.location=center
for ob in [frame,ring]:
    ob.hide_set(False);ob.hide_render=False;ob.data.calc_loop_triangles()
    report['armillary'][ob.name+'Triangles']=len(ob.data.loop_triangles)
    for attr in ['CastPosition','ProjectionPair']:
        if ob.data.attributes.get(attr):ob.data.attributes.remove(ob.data.attributes[attr])
dst=OUT/'ArmillarySeparated.fbx';export([frame,ring],dst)
meta(dst,ROOT/'Assets/Art/Environment/Opening/BrokenArmillary/BrokenArmillary.fbx')
source=ROOT/'ArtSource/Environment/Opening/ExteriorLOD';source.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source/'ArmillarySeparated.blend'))
(LOG/'model-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
