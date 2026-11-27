"""Executed inside build script before baking: source-material geometry comparison."""
from geometry_r03 import OLD_Z
old_path=OUT.parent/'R02/MasterTenon_R02.blend'
with bpy.data.libraries.load(str(old_path),link=False) as (a,b):
    b.objects=['MasterTenon_HIGH','MasterTenon_LOD0']
old_high,old_low=b.objects
for ob in [old_high,old_low]:
    scene.collection.objects.link(ob);ob.name='R02_Reference_'+ob.name;ob.hide_set(False);ob.hide_render=True
for ob in [low,lod1,lod2]:ob.hide_render=True;ob.hide_set(True)
old_high.hide_render=True;high.hide_render=False
bpy.context.view_layer.update()

def geometry_signature(ob,limit=.127):
    me=ob.data;me.calc_loop_triangles()
    coords={v.index:tuple(float(q) for q in v.co) for v in me.vertices if v.co.z<=limit+1e-8}
    faces=sorted(tuple(sorted(coords[i] for i in t.vertices)) for t in me.loop_triangles if all(i in coords for i in t.vertices))
    return {'vertices':len(coords),'triangles':len(faces),'vertex_sha':hashlib.sha256(repr(sorted(coords.values())).encode()).hexdigest(),'triangle_sha':hashlib.sha256(repr(faces).encode()).hexdigest()}
def volume(ob):
    bm=bmesh.new();bm.from_mesh(ob.data);v=bm.calc_volume(signed=True);bm.free();return v
old_sig=geometry_signature(old_low);new_sig=geometry_signature(low)
assert old_sig==new_sig,(old_sig,new_sig)
report={'status':'PREVIEW_ONLY_NOT_FROZEN','lower_0_to_127_mm_exact_coordinate_and_triangle_identity':True,'R02_lower':old_sig,'R03_lower':new_sig,'R02_volume_m3':volume(old_low),'R03_volume_m3':volume(low),'volume_reduction_percent':100*(1-volume(low)/volume(old_low)),'meshes':[mesh_stats(o) for o in [high,low,lod1,lod2]],'sections':[dict(z_mm=float(z*1000),width_mm=section(z)[0]*1000,depth_mm=section(z)[1]*1000,corner_radius_mm=section(z)[2]*1000) for z in PROFILE_Z],'thumb':{'surface':'front -Y','center_metres':[0,-section(.252)[1]/2,.252],'depth_metres':.002,'oval_size_metres':[.036,.056],'real_low_mesh':True},'R02_grip_depth_mm':120,'R03_waist_depth_mm':74,'depth_reduction_percent':100*(1-.074/.12),'lighting_note':'Both versions rendered in same neutral cameras and light rig, source procedural jade materials, before final rebake.'}
savejson(LOG/'preview-geometry-comparison.json',report)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world.use_nodes=True;bg=next(q for q in scene.world.node_tree.nodes if q.type=='BACKGROUND');bg.inputs['Color'].default_value=(.11,.14,.16,1);bg.inputs['Strength'].default_value=.45
def lamp(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,.18))-o.location).to_track_quat('-Z','Y').to_euler();return o
lamp('Soft_Key',(.4,-.45,.65),12,.45,(1,.97,.94));lamp('Cool_Fill',(-.35,-.15,.3),4,.35,(.77,.88,1));lamp('Edge_Rim',(.2,.35,.45),12,.30,(.85,1,.94))
cd=bpy.data.cameras.new('Review_Camera');cam=bpy.data.objects.new('Review_Camera',cd);scene.collection.objects.link(cam);scene.camera=cam;cd.type='ORTHO';cd.clip_start=.001
views=[('01_Front45',(.44,-.64,.43),(0,0,.175),.46),('02_RightThickness',(.75,0,.175),(0,0,.175),.46),('03_GripClose',(.22,-.48,.34),(0,0,.25),.24)]
for version,ob in [('R03',high),('R02',old_high)]:
    high.hide_render=ob!=high;old_high.hide_render=ob!=old_high
    for name,loc,target,scale in views:
        cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cd.ortho_scale=scale
        scene.render.filepath=str(PRE/f'{version}_{name}.png');bpy.ops.render.render(write_still=True)
        print('PREVIEW_READY',scene.render.filepath,flush=True)
high.hide_render=False;old_high.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MasterTenon_R03_Preview.blend'))
print('PREVIEW_ONLY_COMPLETE',flush=True)
