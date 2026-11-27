import bpy,sys,math
from pathlib import Path
from mathutils import Vector
out=Path(sys.argv[sys.argv.index('--')+1]);out.mkdir(parents=True,exist_ok=True)
s=bpy.context.scene
print("FPS",s.render.fps,s.render.fps_base)
s.render.engine='BLENDER_WORKBENCH'
sh=s.display.shading
sh.light='STUDIO';sh.color_type='TEXTURE';sh.show_cavity=True;sh.cavity_type='WORLD';sh.curvature_ridge_factor=.55;sh.curvature_valley_factor=.65;sh.show_shadows=True;sh.show_specular_highlight=True;sh.background_type='WORLD'
world=s.world or bpy.data.worlds.new('Grey sky');s.world=world;world.color=(.08,.095,.10)
s.display.render_aa='16';s.render.film_transparent=False
floorroot=bpy.data.objects.new('Paving',None);s.collection.objects.link(floorroot)
for lane in range(-4,5):
 for i in range(-5,7):
  bpy.ops.mesh.primitive_cube_add(size=1)
  tile=bpy.context.object;tile.parent=floorroot
  tile.location=(lane*.9,(i+.5*(lane%2))*.864,-.031);tile.dimensions=(.893,.857,.054)
  col=.22+.03*((i+lane*9)%3)
  mat=bpy.data.materials.get('Grey slate '+str((i+lane*9)%3))
  if mat is None:
   mat=bpy.data.materials.new('Grey slate '+str((i+lane*9)%3));mat.diffuse_color=(col*.83,col*.90,col,1)
  tile.data.materials.append(mat)
camdat=bpy.data.cameras.new('Walk review');cam=bpy.data.objects.new('Walk review',camdat);s.collection.objects.link(cam);s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.93
s.render.resolution_percentage=100;s.render.resolution_x=576;s.render.resolution_y=610
for view,pos in [('side',(3.1,-.65,1.57)),('rear',(-2.75,3.25,1.72))]:
 cam.location=pos;cam.rotation_euler=(Vector((0.,.075,1.19))-cam.location).to_track_quat('-Z','Y').to_euler()
 for j in range(48):
  s.frame_set(j);floorroot.location.y=.864*j/48
  s.render.filepath=str(out/(view+f'{j:02d}.png'));bpy.ops.render.render(write_still=True)
