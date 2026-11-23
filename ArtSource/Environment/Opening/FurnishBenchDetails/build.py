"""Two bench props mapped into actual previously-baked pavilion linen and elm atlas islands."""
from pathlib import Path
import json,bpy,bmesh,math
from mathutils import Vector
from math import sin,cos,pi
HERE=Path(__file__).resolve().parent
STAGE=Path('D:/Temp/FurnishBenchDetails_bake');STAGE.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
with bpy.data.libraries.load(str(HERE.parent/'FurnishPavilionB/Pavilion_pipeline.blend')) as (a,b):
    b.materials=['Pavilion_Baked']
mat=bpy.data.materials['Pavilion_Baked']
rects=json.loads((HERE/'atlas-rectangles.json').read_text())
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
def active(ob):
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
def mesh(name,verts,faces):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces)
    bm=bmesh.new();bm.from_mesh(m);bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:]);bm.to_mesh(m);bm.free()
    ob=bpy.data.objects.new(name,m);scene.collection.objects.link(ob);m.materials.append(mat);return ob
def beveled(ob,r,segments):
    if r<=0:return
    active(ob);m=ob.modifiers.new('Eased touch edges','BEVEL');m.width=r;m.segments=segments
    bpy.ops.object.modifier_apply(modifier=m.name)
def layer(name,w,d,z,ang,offset,phase,tier):
    nx=(11,8,5)[tier];ny=(6,4,3)[tier]
    v=[];f=[]
    for surf in range(2):
        for j in range(ny+1):
            b=j/ny;vv=2*b-1
            for i in range(nx+1):
                a=i/nx;u=2*a-1
                # Trim rounded corners into the silhouette rather than a square-edged slab.
                x=u*w*.5*(1-.036*abs(vv)**7*abs(u)**3)
                y=vv*d*.5*(1-.043*abs(u)**7*abs(vv)**3)
                x+=.005*sin(3.5*vv+phase)*u*u
                y+=.006*sin(5*u+phase)*vv*vv
                crest=(.0028*sin(9*u+2*vv+phase)+.0021*cos(13*vv-4*u+phase))*(.42+.58*b)
                hem=.0027*cos(3.9*vv+3*u)*abs(u)**4
                zz=z+crest+hem
                if surf==0:zz=max(.0003,zz-.009)
                else:zz+=.009+.0027*(1-u*u)*(1-vv*vv)
                v.append((x*cos(ang)-y*sin(ang)+offset[0],y*cos(ang)+x*sin(ang)+offset[1],zz))
    stride=nx+1;sheet=stride*(ny+1)
    for surf in range(2):
        for j in range(ny):
            for i in range(nx):
                a=j*stride+i+surf*sheet;f.append((a,a+1,a+stride+1,a+stride))
    border=list(range(stride))+[j*stride+nx for j in range(1,ny+1)]+list(range(sheet-2,sheet-stride-1,-1))+[j*stride for j in range(ny-1,0,-1)]
    for i,a in enumerate(border):
        b=border[(i+1)%len(border)];f.append((a,b,b+sheet,a+sheet))
    ob=mesh(name,v,f)
    beveled(ob,(.0031,.0026,0)[tier],1)
    return ob
def cloth(tier):
    return [layer('Lower return',.432,.298,.008,-.024,(.003,-.001),.5,tier),
            layer('Middle folded leaf',.422,.29,.024,.017,(-.006,.003),1.20,tier),
            layer('Upper folded leaf',.410,.292,.040,-.040,(.004,-.011),1.88,tier)]
def cuboid(n,c,d):
    bpy.ops.mesh.primitive_cube_add(size=1,location=c);o=bpy.context.object;o.name=n;o.dimensions=d
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    return o
def cradle_board(y,tier):
    # Two bowl notches cut through a single wooden plate, producing three raised fingers.
    n=(18,11,6)[tier];h=.124;r=.041;w=.314
    path=[(-w/2,.036),(-w/2,h)]
    for xc in (-.069,.069):
        path.append((xc-r,h))
        for j in range(1,n+1):
            angle=pi+pi*j/n;path.append((xc+r*cos(angle),h+r*sin(angle)))
    path.extend(((w/2,h),(w/2,.036)))
    v=[(x,y+dy,z) for dy in (-.018,.018) for x,z in path];k=len(path)
    f=[tuple(range(k)),tuple(range(k,2*k))]
    for j in range(k):f.append((j,(j+1)%k,k+(j+1)%k,k+j))
    o=mesh('Cut comb',v,f);beveled(o,(.0025,.0015,0)[tier],1);return o
def rest(tier):
    out=[]
    for y in (-.069,.069):out.append(cradle_board(y,tier))
    for x in (-.112,.112):
        o=cuboid('Runner',(x,0,.019),(.057,.217,.038));beveled(o,(.003,.002,0)[tier],1);out.append(o)
    return out
def prepare(name,pieces,rect):
    active(pieces[0])
    for o in pieces:o.select_set(True)
    bpy.ops.object.join()
    o=bpy.context.object;o.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Planar UVs per surface. Overlap intentionally reuses the verified source atlas.
    me=o.data
    while me.uv_layers:me.uv_layers.remove(me.uv_layers[0])
    uv=me.uv_layers.new(name='Reused baked atlas')
    lo=[min(v.co[k] for v in me.vertices) for k in range(3)]
    hi=[max(v.co[k] for v in me.vertices) for k in range(3)]
    x0,y0,x1,y1=rect
    for face in me.polygons:
        dim=max(range(3),key=lambda k:abs(face.normal[k]))
        a,b=(1,0) if dim==2 else (2,0) if dim==1 else (2,1)
        for j in face.loop_indices:
            co=me.vertices[me.loops[j].vertex_index].co
            u=(co[a]-lo[a])/(hi[a]-lo[a]);v=(co[b]-lo[b])/(hi[b]-lo[b])
            uv.data[j].uv=(x0+u*(x1-x0),y0+v*(y1-y0))
    me.materials.clear();me.materials.append(mat)
    for f in me.polygons:f.material_index=0
    m=o.modifiers.new('Stable triangle winding','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=m.name)
    return o
report={'method':'Reuse Pavilion_Baked 1024 BaseColor, tangent normal, metallic/occlusion/smoothness. UV rectangles verified within the original linen/elm islands. No extra runtime material or textures.','meshes':{}}
for idx,(name,fn,region) in enumerate((('FoldedLinen',cloth,'linen'),('ThreeFingerRest',rest,'elm'))):
    out=[]
    for tier in range(3):
        ob=prepare(name if tier==0 else name+'_LOD'+str(tier),fn(tier),rects[region])
        active(ob);me=ob.data;me.calc_loop_triangles();out.append(len(me.loop_triangles))
        bpy.ops.export_scene.fbx(filepath=str(STAGE/f'{name}_LOD{tier}.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE')
        if tier:bpy.data.objects.remove(ob,do_unlink=True)
        else:
            dims=list(ob.dimensions);ob.location.x=idx*.58
    assert out[0]>out[1]>out[2]
    report['meshes'][name]={'LOD_tri':out,'dimensions_xyz_blender_m':dims}
(HERE/'bake-report.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'BenchDetails_pipeline.blend'),compress=True)
