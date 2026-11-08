"""Independently rebake tangent normals from the packed, editable source, including its authored cage groups."""
from pathlib import Path
import bpy
import json
import numpy as np

here=Path(__file__).resolve().parent
work=here.parents[3]/"Logs/chapter01-wakecot"
tex=here.parents[3]/"Assets/Art/Environment/Opening/WakeCot/Textures"
scene=bpy.context.scene
low=scene.objects["WakeCot"]
high=[o for o in scene.objects if o.name.endswith(" HIGH")]
for ob in [low,*high]:
    ob.hide_set(False);ob.hide_render=False
    mesh=ob.data;n=len(mesh.vertices)
    xyz=np.empty(3*n,dtype=np.float32);mesh.attributes["CotRestPosition"].data.foreach_get("vector",xyz)
    group=np.empty(n,np.int32);mesh.attributes["CotBakePart"].data.foreach_get("value",group)
    xyz.reshape(-1,3)[:,0]+=3.5*group;mesh.vertices.foreach_set("co",xyz);mesh.update()
bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
bpy.context.view_layer.objects.active=low
for o in [low,*high]:o.select_set(True)
new=bpy.data.images.new("Independent normal",width=1024,height=1024,alpha=True);new.colorspace_settings.name="Non-Color"
nt=low.data.materials[0].node_tree;target=nt.nodes.new("ShaderNodeTexImage");target.image=new;nt.nodes.active=target
bpy.ops.object.bake(type="NORMAL",use_selected_to_active=True,cage_extrusion=.014,max_ray_distance=.033,
    margin=5,margin_type="EXTEND",normal_space="TANGENT",normal_r="POS_X",normal_g="POS_Y",normal_b="POS_Z")
new.filepath_raw=str(work/"normal-independent.png");new.file_format="PNG";new.save()

def pixels(img):
    a=np.empty(len(img.pixels),dtype=np.float32);img.pixels.foreach_get(a);return a.reshape(-1,4)
actual=bpy.data.images.load(str(tex/"WakeCot_Normal.png"),check_existing=False)
actual.colorspace_settings.name="Non-Color"
ref=pixels(actual)[:,:3]*2-1;reb=pixels(new)[:,:3]*2-1
# Compute UV coverage in pixel centres, excluding the baked dilation ring.
inside=np.zeros((1024,1024),bool)
uv=low.data.uv_layers.active.data
for p in low.data.polygons:
    a,b,c=np.array([list(uv[li].uv) for li in p.loop_indices])*1024
    x=max(0,int(np.floor(min(a[0],b[0],c[0]))));X=min(1024,1+int(max(a[0],b[0],c[0])))
    y=max(0,int(np.floor(min(a[1],b[1],c[1]))));Y=min(1024,1+int(max(a[1],b[1],c[1])))
    gy,gx=np.mgrid[y:Y,x:X]+.5
    e1=(b[0]-a[0])*(gy-a[1])-(b[1]-a[1])*(gx-a[0])
    e2=(c[0]-b[0])*(gy-b[1])-(c[1]-b[1])*(gx-b[0])
    e3=(a[0]-c[0])*(gy-c[1])-(a[1]-c[1])*(gx-c[0])
    inside[y:Y,x:X] |= ((e1>=0)&(e2>=0)&(e3>=0))|((e1<=0)&(e2<=0)&(e3<=0))
ref=ref[inside.ravel()];reb=reb[inside.ravel()]
def norm(a):return a/np.maximum(np.linalg.norm(a,axis=1,keepdims=True),1e-7)
angle=np.degrees(np.arccos(np.clip(np.sum(norm(ref)*norm(reb),axis=1),-1,1)))
report={"samples_inside_uv":len(ref),"median_degrees":float(np.median(angle)),"p99_degrees":float(np.percentile(angle,99)),
        "negative_z_percent":float(np.mean(ref[:,2]<0)*100),"distinct_high_surfaces":len(high)}
print("TANGENT REBAKE",report)
(work/"normal-rebake.json").write_text(json.dumps(report,indent=2),encoding="utf8")
assert report["p99_degrees"]<1.2 and report["negative_z_percent"]<3
