"""Rebake the final tangent normal from the saved source pair IDs, without invoking the model generator."""
from pathlib import Path
import bpy
import numpy as np
import json
folder=Path(__file__).resolve().parent
low=bpy.data.objects['BrokenArmillary']
source=[o for o in bpy.context.scene.objects if o.type=='MESH' and o!=low]
for obj in [low,*source]:
    me=obj.data
    count=len(me.vertices);rest=np.empty(3*count,np.float32);ids=np.empty(count,np.int32)
    me.attributes['CastPosition'].data.foreach_get('vector',rest)
    me.attributes['ProjectionPair'].data.foreach_get('value',ids)
    rest.reshape(-1,3)[:,0]+=ids*3.5
    me.vertices.foreach_set('co',rest);me.update()
    obj.hide_render=False;obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=low
node=low.data.materials[0].node_tree.nodes.new('ShaderNodeTexImage')
size=1024
node.image=bpy.data.images.new('Independent witness',width=size,height=size,alpha=True)
node.image.colorspace_settings.name='Non-Color'
low.data.materials[0].node_tree.nodes.active=node
bpy.ops.object.bake(type='NORMAL',use_selected_to_active=True,use_clear=True,
    normal_space='TANGENT',normal_r='POS_X',normal_g='POS_Y',normal_b='POS_Z',
    cage_extrusion=.018,max_ray_distance=.045,margin=4,margin_type='EXTEND')
def encoded(image):
    a=np.empty(size*size*4,np.float32);image.pixels.foreach_get(a)
    return np.rint(np.clip(a.reshape(-1,4)[:,:3],0,1)*255).astype(np.int16)
out=folder.parents[3]/'Assets/Art/Environment/Opening/BrokenArmillary/Textures/BrokenArmillary_Normal.png'
saved=bpy.data.images.load(str(out),check_existing=False);saved.colorspace_settings.name='Non-Color'
old=encoded(saved);rebaked=encoded(node.image);diff=np.abs(old-rebaked)
report={'blender':bpy.app.version_string,'source_high_parts':len(source),'triangulated_target':len(low.data.polygons),
    'pixel_count':len(diff),'normal_channel_byte_MAE':float(diff.mean()),
    'normal_max_abs_channel_error':int(diff.max()),'exact_rgb_texels_pct':float(100*(diff.max(axis=1)==0).mean()),
    'method':'Fresh Cycles selected-to-active tangent bake from the .blend, using per-vertex ProjectionPair group and stored rest coordinates.'}
assert report['normal_channel_byte_MAE']<.03,report
(folder/'normal_rebake.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('INDEPENDENT',report)
