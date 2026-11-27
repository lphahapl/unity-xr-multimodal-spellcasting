"""Author a closed, in-place burden walk on the production skeleton. Run in Blender 5.2."""
import bpy, csv, json, math, struct, sys
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
ACTOR = ROOT / 'Assets/Art/Enemies/CanJiOu'
FBX = ACTOR / 'Animations/CanJiOu_BackCarryWalk.fbx'
PI, TAU = math.pi, 2 * math.pi
V = Vector
I = Quaternion()
def rot(axis, deg):
    return Quaternion(V(axis), math.radians(deg))
def imp(path):
    before=set(bpy.data.objects)
    assert bpy.ops.import_scene.fbx(filepath=str(path)) == {'FINISHED'}
    return list(set(bpy.data.objects)-before)
def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
def write_action(name):
    return bpy.data.actions.new(name)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
s=bpy.context.scene
s.render.fps=30
s.frame_start=0
s.frame_end=48
actor=imp(ACTOR/'CanJiOu_LOD0.fbx')
s.render.fps=30
s.render.fps_base=1.0
arm=next(o for o in actor if o.type=='ARMATURE')
body=next(o for o in actor if o.type=='MESH')
for o in actor:
    o.animation_data_clear()
action=write_action('CanJiOu_BackCarryWalk')
arm.animation_data_create().action=action
pbs={p.name.split(':')[-1]:p for p in arm.pose.bones}
parent={k:pb.parent.name.split(':')[-1] if pb.parent else None for k,pb in pbs.items()}
restpos={k:arm.matrix_world@p.bone.head_local for k,p in pbs.items()}
restrot={k:(arm.matrix_world@p.bone.matrix_local).to_quaternion() for k,p in pbs.items()}
armScale=arm.matrix_world.to_scale().x
prior={}
pack_poses=[]
foot_track={}
def solve(frame):
    t=frame/48
    w=TAU*t
    # Shift onto the supported side midway through stance, settle during double support.
    sway=.039*math.sin(w)
    bob=.011*math.cos(2*w-PI)
    hipshift=V((sway,-.078-.007*math.sin(2*w),-.089+bob))
    pitch=32.0+1.4*math.cos(2*w+.24)
    loadroll=1.6*math.sin(w+.18)
    packrot=rot((0,1,0),loadroll) @ rot((1,0,0),pitch)
    packpos=V((sway*.61,.32+.007*math.cos(2*w+.2),1.82+bob*.85)) + packrot@V((0.,-(.049+.010*math.cos(2*w)),0.))
    pack_poses.append((packpos,packrot))
    pos,ori={},{}
    def head(k):
        if k in pos: return pos[k]
        p=parent[k]
        assert p in ori,k
        return pos[p]+(ori[p]@restrot[p].inverted())@(restpos[k]-restpos[p])
    def setrot(k,q):
        p=parent[k]
        basis=restrot[k]
        if p: basis=(ori[p] @ restrot[p].inverted())@basis
        local=basis.inverted()@q
        local.normalize()
        if k in prior: local.make_compatible(prior[k])
        prior[k]=local.copy()
        pb=pbs[k];pb.rotation_mode='QUATERNION';pb.rotation_quaternion=local
        pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        pos[k]=head(k) if p else restpos[k]+hipshift
        ori[k]=q
    def delta(k,d):
        setrot(k,d@restrot[k])
    def aim(k,child,target):
        start=head(k)
        swing=(restpos[child]-restpos[k]).rotation_difference(target-start)
        delta(k,swing)
    def chain(a,b,c,target,pole):
        start=head(a)
        vec=target-start
        n=vec.normalized()
        l1=(restpos[b]-restpos[a]).length;l2=(restpos[c]-restpos[b]).length
        dist=min(vec.length,l1+l2-.00001)
        elbowAlong=(l1*l1-l2*l2+dist*dist)/(2*dist)
        sidepole=pole-n*pole.dot(n);sidepole.normalize()
        mid=start+n*elbowAlong+sidepole*math.sqrt(max(0.,l1*l1-elbowAlong*elbowAlong))
        aim(a,b,mid);aim(b,c,start+n*dist)
    pb=pbs['Hips'];pb.location=(restrot['Hips'].inverted()@hipshift)/armScale
    pb.keyframe_insert('location',frame=frame,group=pb.name)
    delta('Hips',rot((0,0,1),3.5*math.cos(w))@rot((0,1,0),-2.5*math.sin(w))@rot((1,0,0),14.8))
    for k,ang in [('Spine',23),('Spine1',29),('Spine2',pitch),('Neck',30),('Head',12)]:
        delta(k,rot((0,0,1),-.9*math.cos(w)) @ rot((0,1,0),loadroll) @ rot((1,0,0),ang))
    for tag,sg,phase in [('Left',1,t%1),('Right',-1,(t+.5)%1)]:
        d=.60
        travel=.864
        if phase<=d:
            fore=travel*(phase-d/2)
            lift=toe=0.
        else:
            u=(phase-d)/(1-d)
            smooth=u*u*u*(10+u*(-15+6*u))
            fore=travel*(d/2+(1-d)*u-smooth)
            lift=.065*math.sin(PI*u)**1.65
            toe=-9*math.sin(TAU*u)*math.sin(PI*u)
        ankle=V((sg*(.146+.014*math.sin(PI*max(0.,(phase-d)/(1-d)))**2),.009+fore,.126+lift))
        chain(tag+'UpLeg',tag+'Leg',tag+'Foot',ankle,V((sg*.12,-1,0)))
        footq=rot((1,0,0),toe)
        delta(tag+'Foot',footq);delta(tag+'ToeBase',footq)
        foot_track.setdefault(tag,[]).append(list(head(tag+'Foot')))
        delta(tag+'Shoulder',rot((0,0,1),sg*10) @ rot((1,0,0),pitch))
        # Hands cradle the lower front edge. Elbows pass to the sides of the chest.
        target=packpos+packrot@V((sg*.285,-.534 if sg>0 else -.515,-.567 if sg>0 else -.54))
        chain(tag+'Arm',tag+'ForeArm',tag+'Hand',target,V((sg*.45,-.30,-.92)))
        palm=V((sg*.51, .45 if sg>0 else -.72,.04)).normalized()
        dh=(arm.matrix_world.to_3x3()@(pbs[tag+'Hand'].bone.tail_local-pbs[tag+'Hand'].bone.head_local)).rotation_difference(palm)
        delta(tag+'Hand',dh)
for frame in range(49):solve(frame)

def linear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for pt in curve.keyframe_points: pt.interpolation='LINEAR'
linear(action)
s.frame_set(0)
select([arm])
assert bpy.ops.export_scene.fbx(filepath=str(FBX),use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1.,bake_anim_simplify_factor=0.,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS')=={'FINISHED'}
# Unity +Z forward; the rig faces -Y in Blender. Preserve identity for the upright A4.
# A Unity-imported FBX's +X is mirrored across Blender X; +Y is up, +Z is forward.
def unity_pos(p):return (-p.x,p.z,-p.y)
def unity_rot(q):
    conv=Matrix(((-1.,0.,0.),(0.,0.,1.),(0.,-1.,0.)))
    # reflected coordinate transform, apply to both bases
    g=(conv@q.to_matrix()@conv.inverted()).to_quaternion()
    return (g.x,g.y,g.z,g.w)
with (HERE/'StoneRoot.csv').open('w',newline='') as f:
    rows=csv.writer(f);rows.writerow(['phase','x','y','z','qx','qy','qz','qw'])
    for i,(p,q) in enumerate(pack_poses):
        rows.writerow([round(x,8) for x in (i/48,*unity_pos(p),*unity_rot(q))])
stones=imp(ACTOR/'BallastStoneA4/T25-A4-Unity.fbx')
s.render.fps=30
s.render.fps_base=1.0
base=bpy.data.objects.new('A4_BackLoad',None);s.collection.objects.link(base)
for o in stones:
    if o.parent not in stones:
        original=o.matrix_world.copy();o.parent=base;o.matrix_world=original
base.rotation_mode='QUATERNION'
for i,(p,q) in enumerate(pack_poses):
    base.location=p;base.rotation_quaternion=q;base.keyframe_insert('location',frame=i);base.keyframe_insert('rotation_quaternion',frame=i)
base.animation_data.action.name='A4_BackLoad'
linear(base.animation_data.action)

# Attach the production base colors to the Blender preview; the exported animation FBX stays skeleton-only.
for o in [body]+[o for o in stones if o.type=='MESH']:
 for mat in o.data.materials:
    if not mat or not mat.node_tree:continue
    shader=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    name=mat.name.split('.')[0]
    fname=('CanJiOu_BaseColor.png' if o==body else name+'_BaseColor.png')
    files=list(ACTOR.rglob(fname))
    if not files or not shader:continue
    img=bpy.data.images.load(str(files[0]),check_existing=True);img.pack()
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    shader.inputs['Metallic'].default_value=.4 if name in ['Bronze','CanJiOu_Surface'] else .04
    shader.inputs['Roughness'].default_value=.48 if name=='Bronze' else .74
s.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'CanJiOu_BackCarryWalk.blend'))
select([arm,body,base]+stones)
assert bpy.ops.export_scene.gltf(filepath=str(HERE/'CanJiOu_BackCarryWalk.glb'),export_format='GLB',use_selection=True,export_animation_mode='SCENE',export_force_sampling=True,export_extras=False)=={'FINISHED'}
# Blender can emit one scene-mode Action per moving object. Both belong to one playback clip.
glb=HERE/'CanJiOu_BackCarryWalk.glb'
raw=glb.read_bytes()
jsize,_=struct.unpack_from('<I4s',raw,12)
doc=json.loads(raw[20:20+jsize]); binary=raw[20+jsize:]
merged={'name':'CanJiOu_BackCarryWalk','channels':[],'samplers':[]}
for track in doc['animations']:
    n=len(merged['samplers'])
    merged['samplers'].extend(track['samplers'])
    for ch in track['channels']:
        ch['sampler']+=n;merged['channels'].append(ch)
doc['animations']=[merged]
data=json.dumps(doc,ensure_ascii=False,separators=(',',':')).encode('utf8')
data+=b' '*(-len(data)%4)
glb.write_bytes(struct.pack('<4sII',b'glTF',2,20+len(data)+len(binary))+struct.pack('<I4s',len(data),b'JSON')+data+binary)
print('DONE',FBX,flush=True)
