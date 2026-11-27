import bpy,json
from pathlib import Path
from mathutils import Vector
P=Path(__file__).resolve().parent;PRE=P/'StructurePreview';LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r03-20261126/model'
bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR03_StructureCandidate.blend'));s=bpy.context.scene;s.cycles.samples=24
ob=bpy.data.objects['QingGuiR03_BronzeFrame_LOD0'];bpy.data.objects['QingGuiR03_GoldWire_LOD0'].hide_render=True
original=ob.data.copy();cam=s.camera;cam.location=(.09,.40,.35);cam.rotation_euler=(Vector((.035,0,.319))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.09
material=ob.data.materials[0];bs=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
assert not bs.inputs['Normal'].is_linked
for name,mode in [('Custom','custom'),('Smooth','smooth'),('Flat','flat')]:
    ob.data=original.copy()
    if mode=='smooth':ob.data.normals_split_custom_set([(0,0,0)]*len(ob.data.loops))
    if mode=='flat':
        for p in ob.data.polygons:p.use_smooth=False
    for angle in [0,1]:
        key=bpy.data.objects['Rim'];key.location=(.2 if angle==0 else -.25,.35,.45);key.rotation_euler=(Vector((0,0,.28))-key.location).to_track_quat('-Z','Y').to_euler()
        s.render.filepath=str(PRE/f'Diag_{name}_{angle}.png');bpy.ops.render.render(write_still=True)
print('NO_NORMAL_TEXTURE_DIAGNOSTIC_COMPLETE')
