"""Author the sloping puzzle gallery canopy in the merged frame's local metres.
Run with Blender 5.2 in background. A1ShellJoin.cs binds the two surfaces.
"""
from pathlib import Path
from math import sin, cos, pi
import json, os
import bpy, bmesh
from mathutils import Vector

out=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
mats={s:bpy.data.materials.new(s) for s in ('Timber','Tile')}
for k,co in [('Timber',(.21,.121,.072,1)),('Tile',(.27,.23,.195,1))]:
    mats[k].diffuse_color=co
lo,hi=-1.65,1.19
front,back=-5.004,5.416
count=37
pitch=(back-front)/count
def shell(x):
    return 1.47+(x+1.57)*.124
def piece(name,profile,rings,material,axis):
    verts=[p for row in rings for p in row];n=len(profile)
    faces=[list(range(n-1,-1,-1)),list(range(len(verts)-n,len(verts)))]
    for r in range(len(rings)-1):
        for j in range(n):
            a=r*n+j;b=r*n+(j+1)%n;faces.append([a,b,b+n,a+n])
    # Map Unity's Y-up authoring coordinates to Blender's right-handed space.
    pts=[(x,-z,y) for x,y,z in verts]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(pts,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(mesh);bm.free()
    ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob);mesh.materials.append(mats[material])
    uv=mesh.uv_layers.new(name='Metre UV')
    for p in mesh.polygons:
        for li in p.loop_indices:
            x,y,z=verts[mesh.loops[li].vertex_index]
            normal=(abs(p.normal.x),abs(p.normal.z),abs(p.normal.y))
            face=max(range(3),key=normal.__getitem__)
            if material=='Tile':
                if face==1:u,v=(z-front)/(8*pitch),(x-lo)/1.44
                elif face==0:u,v=(z-front)/(8*pitch),y/1.44
                else:u,v=(x-lo)/1.44,y/(8*pitch)
            elif (axis=='x' and face==0) or (axis=='z' and face==2):
                u,v=((z if axis=='x' else x)/.70,y/.70)
            elif axis=='x': u,v=(z if face==1 else y)/.70,x/2.45
            else: u,v=(x if face==1 else y)/.70,z/2.45
            uv.data[li].uv=u,v
def span_x(name,profile,start,end,cz,material):
    rings=[[(x,shell(x)+h,cz+z) for z,h in profile] for x in (start,end)]
    piece(name,profile,rings,material,'x')
def span_z(name,profile,start,end,mat):
    rings=[[(x,shell(x)+h,z) for x,h in profile] for z in (start,end)]
    piece(name,profile,rings,mat,'z')
# Roof boarding, with restrained joints on the soffit and fitted gable ends.
for k in range(9):
    a=lo+.04+(hi-lo-.08)*k/9+.0015;b=lo+.04+(hi-lo-.08)*(k+1)/9-.0015
    span_z('Soffit_%02d'%k,[(a,-.022),(b,-.022),(b,-.087),(b-.004,-.091),(a+.004,-.091),(a,-.087)],front+.08,back-.08,'Timber')
for i in range(25):
    z=front+.15+i*(back-front-.3)/24
    profile=[(.059*cos(a*2*pi/7),-.143+.059*sin(a*2*pi/7)) for a in range(7)]
    span_x('Rafter_%02d'%i,profile,lo+.015,hi-.06,z,'Timber')
# Rolled tile plates with a continuous pan and full-depth ends at the drip edge.
for j in range(count):
    mid=front+(j+.5)*pitch
    half=pitch*.5-.002
    profile=[(-half,.022),(0,-.014),(half,.022),(half,-.015),(0,-.039),(-half,-.015)]
    span_x('Pan_%02d'%j,profile,lo-.04-(j%3)*.002,hi,mid,'Tile')
for j in range(count+1):
    mid=front+j*pitch
    profile=[(-.064,.016),(-.055,.049),(-.024,.073),(.024,.073),(.055,.049),(.064,.016),(.040,.016),(0,.048),(-.040,.016)]
    span_x('Cover_%02d'%j,profile,lo-.075,hi+.015,mid,'Tile')
# Upper coping, one longitudinal weather-cap with staggered junctions.
for j in range(18):
    a=front+(back-front)*j/18;b=front+(back-front)*(j+1)/18
    profile=[(hi-.118,.020),(hi-.09,.067),(hi-.037,.099),(hi+.025,.095),(hi+.064,.053),(hi+.068,.019),(hi+.043,.018),(hi-.028,.055),(hi-.097,.017)]
    span_z('Coping_%02d'%j,profile,a+.001,b-.001,'Tile')
bpy.context.scene.unit_settings.system='METRIC'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'GalleryCanopy.blend'),compress=True)
packet=[]
for ob in bpy.context.scene.objects:
    me=ob.data;me.calc_loop_triangles()
    pos=[];norm=[];uv=[]
    for tri in me.loop_triangles:
        # Unity front faces use reversed winding relative to Blender right-hand winding.
        for li in (tri.loops[0],tri.loops[2],tri.loops[1]):
            v=me.vertices[me.loops[li].vertex_index].co;n=tri.normal
            # Conversion (Bx,By,Bz)->(Ux,Uy,Uz)=(Bx,Bz,-By) is right-handed;
            # winding for Unity meshes, confirmed via dot product in join importer.
            pos.extend((v.x,v.z,-v.y));norm.extend((n.x,n.z,-n.y));uv.extend(me.uv_layers.active.data[li].uv)
    packet.append(dict(name=ob.name,mat=ob.data.materials[0].name,points=pos,normals=norm,uv=uv))
(Path(os.environ['CH_PHOTO'])/'canopy.json').write_text(json.dumps(dict(parts=packet),separators=(',',':')))
print('TRIANGLES',sum(len(p['points'])//9 for p in packet))

