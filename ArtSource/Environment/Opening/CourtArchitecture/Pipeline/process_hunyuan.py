"""Build static tiers from a hash-checked Hunyuan model; surfaces originate in its original geometry."""
import sys,json,hashlib,math
from pathlib import Path
sys.dont_write_bytecode=True
import bpy,bmesh
import numpy as np
from mathutils import Matrix, Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from bake_util import bake,unwrap,select
from low_mesh import clean,make_low,validate_low,strip_clip_slivers
from registration import horizontal_registration

def clip_roof(obj,fraction,wood_image=None):
    data=obj.data;v=np.empty(len(data.vertices)*3,dtype='f');data.vertices.foreach_get("co",v)
    v=v.reshape(-1,3);z=float(v[:,2].min()+np.ptp(v[:,2])*fraction)
    bm=bmesh.new();bm.from_mesh(data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=0.000001)
    cut=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=(0,0,z),plane_no=(0,0,1),dist=1e-7,clear_inner=True)
    edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-z)<1.e-4 for v in e.verts)]
    capmat=bpy.data.materials.new("Repair_soffit");capmat.use_nodes=True
    bs=capmat.node_tree.nodes.get("Principled BSDF");bs.inputs["Base Color"].default_value=(.089,.049,.028,1)
    if wood_image:
        texture=capmat.node_tree.nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(wood_image),check_existing=True)
        capmat.node_tree.links.new(texture.outputs["Color"],bs.inputs["Base Color"])
    data.materials.append(capmat);uv=bm.loops.layers.uv.verify()
    caps=bmesh.ops.holes_fill(bm,edges=edges,sides=0)['faces']
    for face in caps:
        face.material_index=len(data.materials)-1
        for l in face.loops:l[uv].uv=(l.vert.co.x*2,l.vert.co.y*2)
    bm.to_mesh(data);bm.free();data.update()
    return len(caps)

def load_high(cfg):
    source=Path(cfg['source']);h=hashlib.sha256(source.read_bytes()).hexdigest()
    if h.lower()!=cfg["sha256"].lower():raise ValueError("Unapproved GLB "+h)
    bpy.ops.import_scene.gltf(filepath=str(source))
    obs=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    for obj in obs:
        world=obj.matrix_world.copy();obj.parent=None
        obj.data=obj.data.copy();obj.data.transform(world,shape_keys=True);obj.matrix_world=Matrix.Identity(4)
    select(obs,obs[0]);bpy.ops.object.join();high=bpy.context.view_layer.objects.active
    high.name=cfg['asset']+"_Source"
    original_tri=sum(len(p.vertices)-2 for p in high.data.polygons)
    if cfg.get("clip_ground_fraction"):clip_roof(high,cfg["clip_ground_fraction"],cfg.get("wood_source"))
    d=high.data;coords=np.empty(len(d.vertices)*3,dtype='f');d.vertices.foreach_get("co",coords);pts=coords.reshape(-1,3)
    angle=horizontal_registration(pts) if cfg.get("angle_deg") is None else math.radians(cfg['angle_deg'])
    ca,sa=np.cos(angle),np.sin(angle);rotation=np.array([[ca,-sa],[sa,ca]])
    pts[:,:2]=pts[:,:2]@rotation
    lo,hi=pts.min(0),pts.max(0)
    center=(lo+hi)/2;center[2]=lo[2];src_size=hi-lo
    k=np.array(cfg['dimensions_m'])/src_size;pts[:]=(pts-center)*k
    d.vertices.foreach_set("co",pts.ravel());d.update()
    for o in list(bpy.context.scene.objects):
        if o!=high:bpy.data.objects.remove(o,do_unlink=True)
    return high,dict(source_sha256=h, original_triangles=original_tri, aligned_radians=float(angle),original_extent=src_size.tolist(),meters_scale=k.tolist())

def build(cfg):
    bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.unit_settings.system='METRIC'
    s.render.engine='CYCLES';s.cycles.samples=1;s.cycles.use_denoising=False
    s.render.threads_mode='FIXED';s.render.threads=cfg.get("threads",10)
    input_root=Path(cfg['source']);model=cfg['asset']
    dest=Path(cfg['output']);srcdir=Path(cfg['source_dir']);dest.mkdir(parents=True,exist_ok=True);srcdir.mkdir(parents=True,exist_ok=True)
    high,report=load_high(cfg);tiers=[];results=[]
    for i,tris in enumerate(cfg['budgets']):
        tag=model+"_Tier"+str(i);low=make_low(high,tag,int(tris),s.collection)
        for ob in tiers:ob.hide_render=True;ob.hide_set(True)
        unwrap(low,.007 if i==0 else .012)
        bake(high,low,dest,tag,int(cfg["resolutions"][i]),float(cfg["rays_m"][i]))
        if cfg.get("clip_ground_fraction"):strip_clip_slivers(low)
        tiers.append(low);results.append(validate_low(low))
        print('COURT_BAKE_COMPLETE',tag,results[-1],flush=True)
    high.hide_render=True;high.hide_set(True)
    for o in tiers:o.hide_render=False;o.hide_set(False)
    select(tiers,tiers[0])
    bpy.ops.export_scene.fbx(filepath=str(dest/(model+".fbx")),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    for ob in tiers[1:]:ob.hide_set(True);ob.hide_render=True
    bpy.ops.file.pack_all();bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(srcdir/(model+"_pipeline.blend")),compress=True)
    report["tiers"]=results;(srcdir/"bake-result.json").write_text(json.dumps(report,indent=2),encoding='utf-8')
    print("COURT_MODEL_READY",model,flush=True)
if __name__=='__main__':
    cfg=Path(sys.argv[sys.argv.index('--')+1]);job=json.loads(cfg.read_text(encoding="utf-8-sig"))
    project=Path(__file__).resolve().parents[5]
    for key in ('output','source','source_dir','wood_source'):
        if key in job:job[key]=str((project/Path(job[key])).resolve())
    build(job)
