import bpy,math
from mathutils import Vector
from pathlib import Path
q=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work")
scene=bpy.context.scene
scene.render.engine="CYCLES"
scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1280;scene.render.resolution_y=740
scene.render.resolution_percentage=100
scene.view_settings.view_transform="AgX"
scene.world=bpy.data.worlds.new("Studio")
scene.world.color=(.31,.33,.36)
for name in ["Box","Lid"]: bpy.data.objects[name].location.x=-.36
def point(o,v):
    o.rotation_euler=(Vector(v)-o.location).to_track_quat("-Z","Y").to_euler()
def light(name,p,power,size,color):
    d=bpy.data.lights.new(name,"AREA"); d.energy=power; d.shape="DISK";d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=p;point(o,(-.13,0,.99))
light("Silk",(.25,.15,1.85),11,.65,(1,.89,.72))
light("Back strip",(-.50,-.45,1.46),15,.48,(.84,.91,1))
light("Front card",(-.25,.65,1.25),8,.36,(1,.86,.67))
mat=bpy.data.materials.new("Review charcoal")
p=next(n for n in mat.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
p.inputs["Base Color"].default_value=(.036,.039,.040,1)
p.inputs["Roughness"].default_value=.82
def block(loc,dim,bevel):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object;o.dimensions=dim;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat);m=o.modifiers.new("Soft edge","BEVEL");m.width=bevel;m.segments=3
    o.modifiers.new("Normals","WEIGHTED_NORMAL")
block((0,0,.934),(.405,.405,.072),.006)
block((-.36,0,.865),(.235,.215,.07),.004)
block((0,0,.819),(200,200,.02),.001)
camera=bpy.data.cameras.new("Lens");cam=bpy.data.objects.new("Review",camera)
scene.collection.objects.link(cam);scene.camera=cam;camera.type="ORTHO";camera.clip_start=.005
def take(path,where,target,scale):
    cam.location=where;point(cam,target);camera.ortho_scale=scale
    scene.render.filepath=str(q/path)
    bpy.ops.render.render(write_still=True)
take("Assembled.png",(.09,.82,1.57),(-.136,0,.989),.73)
bpy.data.objects["Lid"].hide_render=True
take("Open.png",(-.28,.43,1.48),(-.36,0,.964),.46)



