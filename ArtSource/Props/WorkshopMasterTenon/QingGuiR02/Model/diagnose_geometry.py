import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parent;bpy.ops.wm.open_mainfile(filepath=str(P/'QingGuiR02_Source.blend'));body=bpy.data.objects['QingGuiR02_Body_HIGH'];wire=bpy.data.objects['QingGuiR02_GoldWire_HIGH']
me=body.data;wall=[]
for v in me.vertices:
    x,y,z=v.co;eq=((x-.041)/.0065)**2+((z-.329)/.012)**2
    if abs(eq-1)<.04:
        wall.append({'co':list(v.co),'bronze':me.attributes['Bronze'].data[v.index].value,'normal':list(v.normal)})
# Find self overlaps of the actual gold mesh; report triangle locations to
# distinguish intended junction contact from unintended duplicate/parallel faces.
mw=wire.data;mw.calc_loop_triangles();tree=BVHTree.FromPolygons([v.co for v in mw.vertices],[t.vertices for t in mw.loop_triangles],all_triangles=True,epsilon=.00001)
pairs=[]
for a,b in tree.overlap(tree):
    if a>=b:continue
    aa=mw.loop_triangles[a];bb=mw.loop_triangles[b]
    if set(aa.vertices)&set(bb.vertices):continue
    if abs(aa.normal.dot(bb.normal))>.999:
        pairs.append({'a':a,'b':b,'center':list(sum((mw.vertices[i].co for i in aa.vertices),Vector())/3)})
LOG=P.parents[4]/'Logs/workshop-master-tenon-qinggui-r02-20261126/model';(LOG/'hole-wire-diagnostic.json').write_text(json.dumps({'wall_sample':wall[::max(1,len(wall)//100)],'wall_count':len(wall),'bronze_min':min(w['bronze'] for w in wall),'bronze_max':max(w['bronze'] for w in wall),'parallel_overlap_pairs':pairs},indent=2),'utf-8')
print('DIAGNOSTIC',len(wall),min(w['bronze'] for w in wall),max(w['bronze'] for w in wall),pairs[:20],flush=True)
exec(compile((P/'inspect_hole.py').read_text('utf-8'),str(P/'inspect_hole.py'),'exec'))
