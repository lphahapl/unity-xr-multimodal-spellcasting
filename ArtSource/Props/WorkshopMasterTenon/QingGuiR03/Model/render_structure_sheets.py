"""Two requested real clay views, no asset or candidate mutation."""
import bpy
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent/'RevisionA';PRE=P/'StructurePreview'
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'));s=bpy.context.scene;s.cycles.samples=24
obs=[bpy.data.objects['QingGuiR03_'+n+'_LOD0'] for n in ['JadeCore','BronzeFrame','GoldWire']]
m=bpy.data.materials.new('UnifiedUntexturedClay');m.use_nodes=True;b=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Base Color'].default_value=(.42,.42,.42,1);b.inputs['Roughness'].default_value=.42
for o in obs:o.hide_render=False;o.data.materials.clear();o.data.materials.append(m)
cam=s.camera
def shot(name,loc,target,scale):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
shot('11_UntexturedClayAssembly',(.48,-.72,.40),(0,0,.175),.41)
for o,x in zip(obs,[-.16,0,.16]):o.location.x=x
s.render.resolution_x=1700;s.render.resolution_y=1100
shot('12_ThreeActualPartsExploded',(.13,-1,.32),(0,0,.175),.68)
print('THREE_PART_CLAY_SHEETS_READY')
