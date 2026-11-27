import bpy,csv,sys
from mathutils import Matrix,Quaternion
from pathlib import Path
rig=next(x for x in bpy.context.scene.objects if x.type=='ARMATURE');p=Path(sys.argv[sys.argv.index('--')+1])
s=Matrix(((-1.,0.,0.),(0.,0.,1.),(0.,-1.,0.)))
prev={}
with p.open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['frame','bone','x','y','z','qx','qy','qz','qw'])
 for frame in range(-1,49):
  if frame>=0:bpy.context.scene.frame_set(frame)
  for pb in rig.pose.bones:
   base=(rig.matrix_world@pb.bone.matrix_local)
   end=base if frame<0 else rig.matrix_world@pb.matrix
   delta=end.to_quaternion()@base.to_quaternion().inverted()
   quat=(s@delta.to_matrix()@s.inverted()).to_quaternion();quat.normalize()
   if pb.name in prev: quat.make_compatible(prev[pb.name])
   prev[pb.name]=quat.copy()
   v=s@end.translation
   w.writerow([frame,pb.name,*[round(x,9) for x in (*v,quat.x,quat.y,quat.z,quat.w)]])
print(p)
