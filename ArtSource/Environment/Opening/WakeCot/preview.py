"""Render matched high/low views and record projected edges/UV charts from the retained source."""
from pathlib import Path
import bpy
import json
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

here=Path(__file__).resolve().parent
out=here.parents[3]/"Logs/chapter01-wakecot"
scene=bpy.context.scene
scene.render.engine="CYCLES";scene.cycles.samples=14
scene.render.threads_mode="FIXED";scene.render.threads=8
scene.render.resolution_x=840;scene.render.resolution_y=570;scene.render.resolution_percentage=100
world=bpy.data.worlds.new("Cot inspection daylight");scene.world=world;world.use_nodes=True
bg=next(n for n in world.node_tree.nodes if n.type=="BACKGROUND")
bg.inputs[0].default_value=(.21,.25,.25,1);bg.inputs[1].default_value=.50
for where,energy,size in [((2.4,-3.5,4),470,2.8),((-.4,2.7,3.2),290,2.7),((-2.4,-1.0,2),110,2.0)]:
    bpy.ops.object.light_add(type="AREA",location=where);ob=bpy.context.object;ob.data.energy=energy;ob.data.size=size
    ob.rotation_euler=(Vector((0,0,.3))-ob.location).to_track_quat("-Z","Y").to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.002))
ground=bpy.context.object;ground.name="Inspection ground"
groundmat=bpy.data.materials.new("Muted stone backdrop");groundmat.diffuse_color=(.063,.064,.053,1);ground.data.materials.append(groundmat)
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type="ORTHO"
low=scene.objects["WakeCot"]
high=[o for o in scene.objects if o.name.endswith(" HIGH")]
views=[("assembly",(.02,0,.34),(-2.38,-3.6,1.78),3.12),
       ("weave",(.66,0,.625),(-.58,-1.5,.91),1.24),
       ("reverse",(0,0,.34),(2.6,3.1,1.12),3.12)]
for title,at,offset,zoom in views:
    target=Vector(at);cam.location=target+Vector(offset)
    cam.rotation_euler=(-Vector(offset)).to_track_quat("-Z","Y").to_euler();cam.data.ortho_scale=zoom
    for tier in ["high","baked"]:
        for ob in high:ob.hide_render=tier!="high"
        low.hide_render=tier!="baked"
        scene.render.filepath=str(out/(title+"_"+tier+".png"));bpy.ops.render.render(write_still=True)
    if title=="assembly":
        edges=[]
        for e in low.data.edges:
            q=[world_to_camera_view(scene,cam,low.matrix_world@low.data.vertices[i].co) for i in e.vertices]
            edges.append([(round(p.x*840,2),round((1-p.y)*570,2)) for p in q])
        (out/"edges.json").write_text(json.dumps(edges),encoding="utf8")
uv=low.data.uv_layers.active.data
(out/"uv.json").write_text(json.dumps([[list(uv[li].uv) for li in p.loop_indices] for p in low.data.polygons]),encoding="utf8")
