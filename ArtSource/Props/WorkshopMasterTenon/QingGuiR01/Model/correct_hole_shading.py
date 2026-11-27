"""Use corner-domain radial tube normals; retain point baseline on outer body."""
import bpy,sys,json,math,shutil
import numpy as np
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent;LOG=OUT.parents[4]/'Logs/workshop-master-tenon-qinggui-r01-20261125/model'
def correct(ob):
    me=ob.data;base=me.attributes['BaseNormal'];norms=np.empty((len(me.loops),3),np.float32);walls=0
    for p in me.polygons:
        vv=[me.vertices[v].co for v in p.vertices]
        iswall=abs(p.normal.y)<.2 and all(abs(((v.x-.041)/.0065)**2+((v.z-.329)/.012)**2-1)<.05 for v in vv)
        if iswall:walls+=1
        for li in p.loop_indices:
            vi=me.loops[li].vertex_index;v=me.vertices[vi].co
            no=Vector((-(v.x-.041)/.0065**2,0,-(v.z-.329)/.012**2)).normalized() if iswall else Vector(base.data[vi].vector)
            norms[li]=no
    old=me.attributes.get('HoleSafeNormal')
    if old:me.attributes.remove(old)
    a=me.attributes.new('HoleSafeNormal','FLOAT_VECTOR','CORNER');a.data.foreach_set('vector',norms.ravel())
    for n in ob.data.materials[0].node_tree.nodes:
        if n.type=='ATTRIBUTE' and n.attribute_name in ['BaseNormal','HoleSafeNormal']:n.attribute_name='HoleSafeNormal'
    return walls

evidence={}
for file in ['QingGuiR01_Source.blend','QingGuiR01.blend']:
    bpy.ops.wm.open_mainfile(filepath=str(OUT/file));high=bpy.data.objects['QingGui_HIGH'];evidence[file]={'corrected_wall_faces':correct(high)}
    if file=='QingGuiR01.blend':
        low=bpy.data.objects['QingGui_LOD0'];s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='GPU';s.cycles.samples=16
        pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='CUDA';pref.get_devices()
        for d in pref.devices:d.use=d.type=='CUDA'
        bpy.ops.object.select_all(action='DESELECT')
        for o in [high,low]:o.hide_set(False);o.hide_render=False;o.select_set(True)
        bpy.context.view_layer.objects.active=low
        mat=low.data.materials[0];node=next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'NormalGL' in n.image.name);mat.node_tree.nodes.active=node
        s.render.bake.use_selected_to_active=True;s.render.bake.cage_extrusion=.003;s.render.bake.max_ray_distance=.012;s.render.bake.margin=16;s.render.bake.normal_space='TANGENT'
        bpy.ops.object.bake(type='NORMAL');node.image.save()
        for folder in ['QingGui_LOD0.fbm','QingGui_LODs.fbm']:
            if (OUT/folder).is_dir():shutil.copy2(OUT/'Textures/QingGui_NormalGL.png',OUT/folder/'QingGui_NormalGL.png')
        high.hide_set(True);high.hide_render=True;high.select_set(False)
        bpy.ops.export_scene.gltf(filepath=str(OUT/'QingGuiR01.glb'),export_format='GLB',use_selection=True,export_tangents=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/file))
if True:
    s=bpy.context.scene;cam=bpy.data.objects['Ortho'];s.camera=cam;s.cycles.samples=32;s.render.resolution_x=1000;s.render.resolution_y=1200;s.render.resolution_percentage=100
    cam.location=(.09,-.40,.35);cam.rotation_euler=(Vector((.035,0,.327))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.075
    s.render.filepath=str(OUT/'Previews/06_ApertureClose.png');bpy.ops.render.render(write_still=True)
(LOG/'hole-normal-correction.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8');print('HOLE_NORMAL_PATCH',evidence,flush=True)
