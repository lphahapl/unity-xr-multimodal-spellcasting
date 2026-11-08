"""Render matched high and baked-low comparisons from the saved pipeline file."""
from pathlib import Path
import bpy
from mathutils import Vector
import json
from bpy_extras.object_utils import world_to_camera_view
here=Path(__file__).resolve().parent;out=here.parents[3]/"Logs/chapter01-interior/model-work"
scene=bpy.context.scene
scene.render.engine="CYCLES";scene.cycles.samples=16;scene.render.threads_mode="FIXED";scene.render.threads=8
scene.render.resolution_x=480;scene.render.resolution_y=416;scene.render.resolution_percentage=100
scene.render.image_settings.file_format="PNG";scene.render.film_transparent=False
scene.world=bpy.data.worlds.new("Inspection world");scene.world.use_nodes=True
back=next(n for n in scene.world.node_tree.nodes if n.type=="BACKGROUND")
back.inputs[0].default_value=(.16,.19,.19,1);back.inputs[1].default_value=.6
for location,energy,size in [((-3,-4,5),510,3.),((3,-2,3),280,2.),((1,2,3),180,2.)]:
    bpy.ops.object.light_add(type="AREA",location=location);l=bpy.context.object;l.data.energy=energy;l.data.shape="DISK";l.data.size=size
    l.rotation_euler=(Vector((0,0,.9))-l.location).to_track_quat("-Z","Y").to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type="ORTHO"
groups={"gauge":(["TargetTrestle","TargetGaugeFace"],(0,0,.94),(1.22,-6,1.9),2.46),
        "face":(["TargetGaugeFace"],(0,-.13,1.60),(.35,-3,.39),.77),
        "lamp":(["WorkshopWallLamp"],(.76,-.17,1.08),(2.45,-3.5,1.20),.56)}
for key,(names,centre,direction,scale) in groups.items():
    centre=Vector(centre);cam.location=centre+Vector(direction);cam.rotation_euler=(centre-cam.location).to_track_quat("-Z","Y").to_euler();cam.data.ortho_scale=scale
    for high in (True,False):
        active={n+("_HIGH" if high else "") for n in names}
        for o in scene.objects:
            if o.type=="MESH":o.hide_render=o.name not in active
        scene.render.filepath=str(out/(key+("_high.png" if high else "_baked.png")))
        bpy.ops.render.render(write_still=True)
    wires=[]
    for name in names:
        obj=scene.objects[name]
        for edge in obj.data.edges:
            pt=[world_to_camera_view(scene,cam,obj.matrix_world@obj.data.vertices[idx].co) for idx in edge.vertices]
            wires.append([[round(a.x*480,2),round((1-a.y)*416,2)] for a in pt])
    (out/(key+"_wire.json")).write_text(json.dumps(wires),encoding="utf8")
uvdata={}
for name in ("TargetGaugeFace","TargetTrestle","WorkshopWallLamp"):
    mesh=scene.objects[name].data;uv=mesh.uv_layers.active.data
    uvdata[name]=[[list(uv[li].uv) for li in face.loop_indices] for face in mesh.polygons]
(out/"uv-layout.json").write_text(json.dumps(uvdata),encoding="utf8")
