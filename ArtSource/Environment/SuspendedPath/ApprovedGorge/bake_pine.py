"""Bake two azimuths of the project's existing mountain pine for middle-distance vegetation."""
from pathlib import Path
import math,bpy
from mathutils import Vector
import numpy as np
here=Path(__file__).resolve().parent
root=here.parents[3]
out=root/'Assets/Art/Environment/SuspendedPath/ApprovedGorge'
source=root/'ArtSource/Environment/Opening/HunyuanPine/pine-high.glb'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
coords=np.array([list(o.matrix_world@Vector(c)) for o in objects for c in o.bound_box])
lo=coords.min(0);hi=coords.max(0);center=Vector((hi+lo)/2)
world=bpy.data.worlds.new('soft skylight');world.node_tree.nodes.get('Background').inputs[0].default_value=(.55,.63,.70,1);world.node_tree.nodes.get('Background').inputs[1].default_value=.85
scene=bpy.context.scene;scene.world=world
sun=bpy.data.lights.new('Raking sunlight','SUN');sun.energy=3.8;sun.color=(1,.91,.74)
light=bpy.data.objects.new('Raking sunlight',sun);scene.collection.objects.link(light);light.rotation_euler=(.55,-.53,-.3)
camera=bpy.data.cameras.new('Atlas elevation');camera.type='ORTHO';camera.ortho_scale=max(hi-lo)*1.1
cam=bpy.data.objects.new('Sprite camera',camera);scene.collection.objects.link(cam);scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=8;scene.render.film_transparent=True;scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100;scene.render.image_settings.color_mode='RGBA';scene.view_settings.view_transform='Standard'
for i,angle in enumerate([18,108]):
    vec=Vector((math.cos(math.radians(angle)),math.sin(math.radians(angle)),.09));cam.location=center+vec*camera.ortho_scale*3
    cam.rotation_euler=(-vec).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(here/f'PineAzimuth{i}.png')
    bpy.ops.render.render(write_still=True)

