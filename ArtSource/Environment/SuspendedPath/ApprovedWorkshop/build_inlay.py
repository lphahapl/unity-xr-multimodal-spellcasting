"""Rebuild the brass-edged survey inlay on the bridge to the chapter-two workshop."""
import bpy, math, json, pathlib,sys
from mathutils import Vector
import numpy as np
root=pathlib.Path(__file__).resolve().parents[4]
if not (root/'Assets').is_dir(): root=pathlib.Path('D:/UnityProjects/NarutoVR')
out=root/'Assets/Art/Environment/SuspendedPath/ApprovedWorkshop'
source=pathlib.Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
mat=bpy.data.materials.new('Workshop survey bronze');mat.use_nodes=True
w=.152; l=.748
def relief(x,y):
    edge=min((1-abs(x))*w*.5,(1-abs(y))*l*.5)
    z=.0124+.0032*min(1,max(0,edge/.0032))
    g=math.exp(-(abs(abs(x)-.56)/.045)**2)*min(1,max(0,(.87-abs(y))*50))
    g+=math.exp(-(abs(abs(y)-.87)/.010)**2)*min(1,max(0,(.56-abs(x))*25))
    z-=.00085*min(1,g)
    for yy in (-.935,.935):
        rad=(x*w/2)**2+((y-yy)*l/2)**2
        if rad<.0065**2:z+=.0021*(1-rad/.0065**2)**.65
    return z+(.00019*math.sin(x*54+y*27)*math.sin(y*120-x*17)+.00010*math.cos(x*88-y*94))*min(1,edge/.006)
def make(name,nx,ny,hi):
    vertices=[];faces=[];uvs=[]
    def ix(x,y):return x+(nx+1)*y
    uvtop=lambda i:(.04+(i%(nx+1))/nx*.92,.015+(i//(nx+1))/ny*.97)
    for y in range(ny+1):
        for x in range(nx+1):
            a=2*x/nx-1;b=2*y/ny-1; vx=a*(w/2-max(0,abs(b)*l/2-(l/2-.007))*.68)
            vz=relief(a,b) if hi else .0151
            vertices.append((vx,b*l/2,vz))
    for y in range(ny):
        for x in range(nx):
            ids=[ix(x,y),ix(x+1,y),ix(x+1,y+1),ix(x,y+1)];faces.append(ids);uvs.append(list(map(uvtop,ids)))
    edge=[ix(x,0) for x in range(nx)]+[ix(nx,y) for y in range(ny)]+[ix(x,ny) for x in range(nx,0,-1)]+[ix(0,y) for y in range(ny,0,-1)]
    lower=[]
    for i in edge:
        vx,vy,_=vertices[i];lower.append(len(vertices));vertices.append((vx,vy,.0003))
    for n,i in enumerate(edge):
        nn=(n+1)%len(edge);faces.append([edge[nn],i,lower[n],lower[nn]])
        u0=n/len(edge);u1=(n+1)/len(edge);uvs.append([(.019,.015+u1*.97),(.019,.015+u0*.97),(.004,.015+u0*.97),(.004,.015+u1*.97)])
    center=len(vertices);vertices.append((0,0,.0003))
    for n,i in enumerate(lower):
        nn=(n+1)%len(lower);faces.append([center,lower[nn],i]);uvs.append([(.498,.0075),(.498+vertices[lower[nn]][0]*5,.0075+vertices[lower[nn]][1]*.014),(.498+vertices[i][0]*5,.0075+vertices[i][1]*.014)])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);mesh.materials.append(mat)
    lay=mesh.uv_layers.new(name="PackedSurvey")
    for p,coords in zip(mesh.polygons,uvs):
        for loop,uv in zip(p.loop_indices,coords):lay.data[loop].uv=uv
        if hi and p.index<nx*ny:p.use_smooth=True
    return ob
high=make('SurveyInlay_Sculpt',38,174,True);lo=make('SurveyInlay_LOD0',2,12,False);far=make('SurveyInlay_LOD1',1,4,False)
far.hide_render=True;far.hide_set(True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=3;scene.cycles.device='CPU'
normal=bpy.data.images.new('SurveyInlay_Normal',256,1024,alpha=False);normal.colorspace_settings.name='Non-Color'
nodes=mat.node_tree.nodes;target=nodes.new('ShaderNodeTexImage');target.image=normal;nodes.active=target
bpy.ops.object.select_all(action='DESELECT');lo.select_set(True);high.select_set(True);bpy.context.view_layer.objects.active=lo
scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.006;scene.render.bake.max_ray_distance=.010
bpy.ops.object.bake(type='NORMAL',margin=2);normal.filepath_raw=str(out/'SurveyInlay_Normal.png');normal.file_format='PNG';normal.save()
high.hide_set(True);high.hide_render=True;far.hide_set(False);far.hide_render=False
bs=nodes.get('Principled BSDF')
n=nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(target.outputs['Color'],n.inputs['Color']);mat.node_tree.links.new(n.outputs['Normal'],bs.inputs['Normal'])
for file,input in [('SurveyInlay_BaseSmooth.png','Base Color'),('SurveyInlay_Emission.png','Emission Color')]:
    img=bpy.data.images.load(str(out/file));t=nodes.new('ShaderNodeTexImage');t.image=img;mat.node_tree.links.new(t.outputs['Color'],bs.inputs[input])
bs.inputs['Emission Strength'].default_value=.35;bs.inputs['Metallic'].default_value=.50
bpy.ops.object.select_all(action='DESELECT')
for ob in (lo,far):ob.select_set(True)
bpy.context.view_layer.objects.active=lo
bpy.ops.export_scene.fbx(filepath=str(out/'SurveyInlay.fbx'),use_selection=True,apply_unit_scale=True,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='FACE',path_mode='STRIP')
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(source/'SurveyInlay.blend'))
def triangles(obj):obj.data.calc_loop_triangles();return len(obj.data.loop_triangles)
print('[Survey] high/lowA/lowB',*[triangles(o) for o in (high,lo,far)])
