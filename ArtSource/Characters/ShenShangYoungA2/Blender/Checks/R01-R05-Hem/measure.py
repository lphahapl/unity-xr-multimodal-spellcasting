"""Reimport one frozen GLB, test all garment triangle pairs and measure radial covering distance."""
import hashlib, json, math, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

output = Path(__file__).resolve().parent
version = sys.argv[-1]
src = output.parents[1] / 'Rebuild' / version / f'ShenShang_{version}.glb'
dest = output / version
dest.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(src))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
pants = next(o for o in meshes if 'bifurcated' in (o.name + o.data.name))
coats = [o for o in meshes if o.name.startswith(('A2 Short coat', 'Jacket', 'Undertunic', 'Waist sash'))]
lower = [o for o in coats if 'Short coat' in o.name]
assert len(lower) == 3

def geometry(o):
    verts = [o.matrix_world @ v.co for v in o.data.vertices]
    o.data.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in o.data.loop_triangles]
    return BVHTree.FromPolygons(verts, tris, all_triangles=True, epsilon=0),verts,tris

pt,pv,ptids=geometry(pants)
trees={};triangles={}
for o in coats:
    tree,verts,tri=geometry(o)
    trees[o.name]=tree
    pairs=tree.overlap(pt)
    collided=sorted(set(i for i,j in pairs))
    pts=[verts[k] for i in collided for k in tri[i]]
    triangles[o.name]={'testedCoatTriangles':len(tri),'triangleIntersectionPairs':len(pairs),'intersectingCoatTriangles':len(collided),'intersectingPantsTriangles':len(set(j for i,j in pairs)),'zBoundsOfAffectedCoatTriangles_m': [round(min(v.z for v in pts),5),round(max(v.z for v in pts),5)] if pts else None}

bins={k:[] for k in ['back','front','left','right']}
for iz in range(190):
    z=.805+iz*.00125
    for deg in np.arange(0,360,.5):
        angle=math.radians(deg);out=Vector((math.sin(angle),math.cos(angle),0));org=Vector((0,.006,z))+out*.39
        leg=pt.ray_cast(org,-out,.39)[0]
        if leg is None:continue
        layers=[trees[o.name].ray_cast(org,-out,.39)[0] for o in lower]
        hits=[h for h in layers if h is not None]
        if not hits:continue
        outer=max(hits,key=lambda h:h.dot(out))
        gap=(outer-leg).dot(out)*1000
        direction=['back','left','front','right'][int((deg+45)//90)%4]
        bins[direction].append((gap,round(deg,1),round(z,6)))

def stats(rows):
    a=np.array([e[0] for e in rows]);i=int(np.argmin(a))
    return {'samples':len(a),'pantsInFrontMoreThan0_05mm':int((a<-.05).sum()),'within0_05mm':int((np.abs(a)<=.05).sum()),'minOuterMinusPants_mm':round(float(a.min()),4),'minimumLocation':{'azimuth_deg':rows[i][1],'z_m':rows[i][2]},'p05_mm':round(float(np.percentile(a,5)),3)}

out={'inputFile':src.name,'SHA256':hashlib.sha256(src.read_bytes()).hexdigest(),'pose':'exported static pose','units':'metres unless marked mm','trousers':pants.name,'testedPantsTriangles':len(ptids),'pairTests':triangles,'radialSurvey':{'layerObjects':[o.name for o in lower],'layers':'outermost actual skirt, woven binding, or hem bead vs outermost trousers on same torso-facing ray','resolution':'0.5 degree azimuth, 1.25 mm height; rays stop at body axis; only heights shared by both surfaces','all':stats(sum(bins.values(),[])),'byQuadrant':{k:stats(v) for k,v in bins.items()}}}
(dest/'Measurements.json').write_text(json.dumps(out,indent=2,ensure_ascii=False),encoding='utf8')
print(version, out['radialSurvey']['all'], {k:v['triangleIntersectionPairs'] for k,v in triangles.items()})
