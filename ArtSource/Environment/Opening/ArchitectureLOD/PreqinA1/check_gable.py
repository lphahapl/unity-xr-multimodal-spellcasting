from pathlib import Path
import bpy,sys,json,numpy as np
from mathutils.bvhtree import BVHTree
HERE=Path(__file__).resolve().parent
baseline=Path(sys.argv[sys.argv.index('--')+1])
old=json.loads(baseline.read_text(encoding='utf-8-sig'))
assert old['mesh']=='CalibrationHall_0_Grounded_LOD0'
points=np.array(old['points']).reshape(-1,3)
tris=np.array(old['slots'][0]['triangles']).reshape(-1,3)
assert len(tris)==1463
old_tree=BVHTree.FromPolygons([(x,-z,y) for x,y,z in points],tris.tolist(),all_triangles=True)
def ratios(tree):
    def hit(origin,dir,distance):return tree.ray_cast(origin,dir,distance)[0] is not None
    sides=[]
    for s in (-1,1):
        covered=[hit((s*3.18,y,z),(-s,0,0),.55) for y in np.linspace(-1.95,1.95,160) for z in np.linspace(1.19,2.56,92)]
        sides.append(round(1-np.mean(covered),4))
    entrance=[hit((x,-2.34,z),(0,1,0),.42) for x in np.linspace(-1.04,1.04,84) for z in np.linspace(.03,2.51,60)]
    return dict(side_window_open_fraction=sides,doorway_samples=len(entrance),doorway_intersections=sum(entrance))
data={'before_Unity_export':ratios(old_tree),'side_window_local_y':[-1.95,1.95],'side_window_local_z':[1.19,2.56],'uniform_parallel_rays_per_side':160*92,'entry_local_x':[-1.04,1.04],'entry_local_z':[.03,2.51]}
for i in range(3):
    o=bpy.data.objects[f'CalibrationHall_0_Grounded_LOD{i}']
    m=o.data;m.calc_loop_triangles()
    tree=BVHTree.FromPolygons([v.co for v in m.vertices],[t.vertices[:] for t in m.loop_triangles],all_triangles=True)
    r=ratios(tree);assert r['doorway_intersections']==0 and max(r['side_window_open_fraction'])<.7
    data['after_LOD'+str(i)]=r
(HERE/'west-gable-readback.json').write_text(json.dumps(data,indent=2))
print(data)
