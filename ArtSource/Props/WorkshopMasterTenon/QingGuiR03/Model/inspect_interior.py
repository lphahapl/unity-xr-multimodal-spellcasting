"""Read-only inspection of frozen-for-first-shot structure candidate."""
import bpy
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;PRE=P/'StructurePreview'
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'));s=bpy.context.scene;s.cycles.samples=32
ob=bpy.data.objects['QingGuiR03_BronzeFrame_LOD0']
for o in s.objects:
    if o.type=='MESH':o.hide_render=o!=ob
cam=s.camera;cam.data.ortho_scale=.075;cam.data.clip_start=.001
def shot(name,loc,target):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
shot('07_ActualInnerCapMetal',(.015,-.030,.290),(.039,.017,.329))
rim=bpy.data.objects['Rim'];rim.location=(-.2,.3,.45);rim.rotation_euler=(Vector((.035,0,.329))-rim.location).to_track_quat('-Z','Y').to_euler()
shot('08_ActualInnerCapTurnLight',(.015,-.030,.290),(.039,.017,.329))
m=bpy.data.materials.new('InspectionClay');m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.40,.40,.40,1);bs.inputs['Roughness'].default_value=.5
ob.data.materials.clear();ob.data.materials.append(m)
shot('09_ActualInnerCapClay',(.015,-.030,.290),(.039,.017,.329))
s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='SINGLE';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True
cu=bpy.data.curves.new('InspectionEdges','CURVE');cu.dimensions='3D';cu.bevel_depth=.000025;cu.bevel_resolution=0
for e in ob.data.edges:
    a,b=[ob.data.vertices[i].co for i in e.vertices]
    if max(a.z,b.z)<.312:continue
    sp=cu.splines.new('POLY');sp.points.add(1);sp.points[0].co=(*a,1);sp.points[1].co=(*b,1)
edge=bpy.data.objects.new('InspectionEdges',cu);s.collection.objects.link(edge);edge.color=(.01,.01,.01,1);s.display.shading.color_type='OBJECT';ob.color=(.6,.6,.6,1)
shot('10_ActualInnerCapWire',(.015,-.030,.290),(.039,.017,.329))
print('INNER_CAP_INSPECTION_COMPLETE')
