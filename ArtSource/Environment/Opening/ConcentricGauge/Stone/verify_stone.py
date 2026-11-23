"""Run against A1_Stone.blend. Optional command line flags --shots and --beauty."""
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
OUT=ROOT/'QA'
OUT.mkdir(exist_ok=True)
skills=Path.home()/'.codex/skills'
sys.path[:0]=[str(skills/f'{s}/scripts') for s in ('blender-asset-qa','blender-game-optimization')]
import qa_check, mesh_stats, review_shots
scene=bpy.context.scene
obs=list(scene.objects)
results={}
for o in obs:
    x=o.location.x
    o.hide_viewport=False; o.hide_render=False; o.location.x=0
    bpy.context.view_layer.update()
    qa=qa_check.run(objects=[o.name],profile='game')
    stats=mesh_stats.run(objects=[o.name],budget='vr')
    results[o.name]={'qa':qa,'stats':stats,'dimensions_m':list(o.dimensions)}
    print(o.name,qa['pass'],qa['errors'],qa['warnings'],flush=True)
    o.location.x=x
(OUT/'MeshQA.json').write_text(json.dumps(results,indent=2),encoding='utf8')
if '--shots' in sys.argv:
    for i in range(3):
        names=[o.name for o in obs if o.name.endswith(f'LOD{i}')]
        for mode in ('random','wire'):
            result=review_shots.run(objects=names,out=str(OUT/f'LOD{i}_{mode}.png'),views=['front','three_quarter','back','top'],mode=mode,res=392)
            for path in result['views']: Path(path).unlink()

if '--beauty' in sys.argv:
    for o in obs: o.hide_render=not o.name.endswith('LOD0')
    scene.world=bpy.data.worlds.new('Courtyard sky'); scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.68,.75,.88,1)
    scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.45
    def light(name,pos,power,size,color):
        ld=bpy.data.lights.new(name,'AREA'); ld.energy=power; ld.shape='DISK'; ld.size=size; ld.color=color
        ob=bpy.data.objects.new(name,ld); scene.collection.objects.link(ob); ob.location=pos
        ob.rotation_euler=(Vector((0,0,.65))-ob.location).to_track_quat('-Z','Y').to_euler()
    light('Courtyard skylight',(-2,-3.1,4.1),480,2.5,(1,.90,.77))
    light('Rim',(2,1.2,3.4),320,2.3,(.78,.87,1))
    bpy.ops.mesh.primitive_plane_add(size=200)
    floor=bpy.context.object; m=bpy.data.materials.new('QA ground'); m.diffuse_color=(.115,.104,.091,1); floor.data.materials.append(m)
    cam=bpy.data.cameras.new('QA lens'); co=bpy.data.objects.new('QA view',cam); scene.collection.objects.link(co); scene.camera=co
    cam.type='ORTHO'; cam.ortho_scale=3.37
    co.location=(-1.67,-5.65,2.5); co.rotation_euler=(Vector((-.22,0,.80))-co.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='CYCLES'; scene.cycles.samples=18; scene.cycles.use_denoising=True
    scene.render.resolution_x=1320; scene.render.resolution_y=840; scene.render.resolution_percentage=100
    scene.render.filepath=str(OUT/'A1_Stone.png')
    bpy.ops.render.render(write_still=True)
    cam.type='PERSP'; cam.lens=18; cam.sensor_width=36
    co.location=(-1.23,-2.126,1.56)
    co.rotation_euler=(Vector((-1.23,-.126,.95))-co.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/'Stele_2m.png')
    bpy.ops.render.render(write_still=True)
if any(not rec['qa']['pass'] for rec in results.values()): raise ValueError('mesh QA failed')

