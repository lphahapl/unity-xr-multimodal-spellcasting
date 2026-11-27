"""Build the A2 lifting cage and adjoining landing railing from measured local dimensions.
Usage: blender -b --factory-startup -P build_basket.py -- <project> <scratch>
"""
from pathlib import Path
from math import pi, sin, cos, ceil
from mathutils import Vector
import bpy, math, sys, random, json
import numpy as np

project, scratch = (Path(x).resolve() for x in sys.argv[sys.argv.index('--')+1:])
src=project/'ArtSource/Environment/SuspendedPath/ApprovedBasket'
dst=project/'Assets/Art/Environment/SuspendedPath/ApprovedBasket'
workshop=project/'Assets/Art/Environment/SuspendedPath/Surfaces/Workshop'
bronze=project/'Assets/Art/Environment/Opening/PreqinGateFittings'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
bpy.context.preferences.filepaths.save_version=0

def new_collection(name):
    c=bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

def textured_material(name,base,normal):
    m=bpy.data.materials.new(name); m.use_nodes=True
    n=m.node_tree.nodes; p=next(x for x in n if x.type=='BSDF_PRINCIPLED')
    def texture(path,colour):
        tex=n.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(path),check_existing=True)
        tex.image.colorspace_settings.name=colour
        return tex
    t=texture(base,'sRGB'); nrm=texture(normal,'Non-Color'); nm=n.new('ShaderNodeNormalMap')
    m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
    m.node_tree.links.new(nrm.outputs['Color'],nm.inputs['Color'])
    m.node_tree.links.new(nm.outputs['Normal'],p.inputs['Normal'])
    if name in ('Timber','EndGrain'):
        inv=n.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
        m.node_tree.links.new(t.outputs['Alpha'],inv.inputs[1]);m.node_tree.links.new(inv.outputs[0],p.inputs['Roughness'])
    else:
        p.inputs['Metallic'].default_value=.67;p.inputs['Roughness'].default_value=.43
    return m

def obj(name,v,f,c):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],f);mesh.update()
    ob=bpy.data.objects.new(name,mesh);c.objects.link(ob)
    return ob

def select(objects,active):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=active

def bake_band():
    """Tangent-space bake of 2 forged pins, rolled edges and hammer dimples to a single face."""
    c=new_collection('01_BAKE_copper_ferrule_240x120mm');dim=(.24,.12);N,M=224,112
    y,x=np.mgrid[0:M+1,0:N+1];u=x/N;v=y/M
    h=np.zeros_like(u)
    # Low raised lips at the top/bottom of the peened band.
    for k in (.065,.935):
        h+=.0012*np.exp(-((v-k)/.022)**2)
    rng=random.Random(126)
    for _ in range(170):
        a,b=rng.random(),rng.random()
        rr=((u-a)/rng.uniform(.012,.033))**2+((v-b)/rng.uniform(.03,.07))**2
        h-=rng.uniform(.00014,.00047)*np.maximum(0,1-rr)**2
    for a in (.18,.82):
        rr=((u-a)*.24)**2+((v-.49)*.12)**2
        rad=.014
        h+=.0023*np.exp(-((np.sqrt(rr)-rad)/.0018)**2)
        h+=.0067*np.sqrt(np.maximum(0,1-rr/rad**2))
    vv=list(zip(((u-.5)*dim[0]).ravel(),((v-.5)*dim[1]).ravel(),h.ravel()))
    faces=[(j*(N+1)+i,j*(N+1)+i+1,(j+1)*(N+1)+i+1,(j+1)*(N+1)+i) for j in range(M) for i in range(N)]
    high=obj('ForgedBand_HIGH',vv,faces,c)
    for f in high.data.polygons:f.use_smooth=True
    low=obj('ForgedBand_projection_LOW',[(-.12,-.06,0),(.12,-.06,0),(.12,.06,0),(-.12,.06,0)],[(0,1,2,3)],c)
    uv=low.data.uv_layers.new()
    for lp,co in zip(uv.data,[(0,0),(1,0),(1,1),(0,1)]):lp.uv=co
    mat=bpy.data.materials.new('Projection target');mat.use_nodes=True;low.data.materials.append(mat)
    target=mat.node_tree.nodes.new('ShaderNodeTexImage');mat.node_tree.nodes.active=target
    im=bpy.data.images.new('Band_Normal',512,256,alpha=False)
    im.colorspace_settings.name='Non-Color';target.image=im
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=2
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.013;scene.render.bake.max_ray_distance=.025
    scene.render.bake.margin=2;select([high,low],low);bpy.ops.object.bake(type='NORMAL')
    im.filepath_raw=str(dst/'ForgedBand_Normal.png');im.file_format='PNG';im.save()
    c.hide_render=True;c.hide_viewport=True
    return high

hi=bake_band()
mats=[textured_material(k,workshop/(k+'_BaseSmooth.png'),workshop/(k+'_NormalGL.png')) for k in ('Timber','EndGrain')]
mats += [textured_material('ForgedBand',bronze/'GateBronze_BaseColor.png',dst/'ForgedBand_Normal.png'),textured_material('HammeredBronze',bronze/'GateBronze_BaseColor.png',bronze/'GateBronze_Normal.png')]

class Joinery:
    def __init__(self,tier):
        self.v=[];self.faces=[];self.uv=[];self.mi=[];self.tier=tier;self.part=0
    def member(self,axis,center,length,width,height,material=0,bevel=.009):
        # Build in Unity-local numeric XYZ; place into right-handed Blender as -X,+Z,+Y.
        # Cross sections are ordered in a right-handed basis *in Blender* below.
        self.part+=1
        def b(v):return Vector((-v[0],v[2],v[1]))
        along,ax1,ax2=[b(np.eye(3)[k]) for k in (axis,(axis+1)%3,(axis+2)%3)]
        c=b(center);lo=len(self.v)
        A,B=width*.5,height*.5
        edge=min(bevel,A*.26,B*.26)
        sections=[(-length/2,edge*.7),(-length/2+edge,0),(length/2-edge,0),(length/2,edge*.7)] if self.tier<=0 else [(-length/2,0),(length/2,0)]
        rings=[]
        for t,cut in sections:
            a,bh=A-cut,B-cut
            if self.tier==2:profile=[(-a,-bh),(a,-bh),(a,bh),(-a,bh)]
            else:
                steps=4 if self.tier<0 else 1
                profile=[]
                for x,y,at in ((a-edge,bh-edge,0),(-a+edge,bh-edge,pi/2),(-a+edge,-bh+edge,pi),(a-edge,-bh+edge,3*pi/2)):
                    for j in range(steps+1):
                        angle=at+pi*j/(2*steps)
                        profile.append((x+edge*cos(angle),y+edge*sin(angle)))
            rings.append(profile)
            self.v.extend(tuple(c+along*t+ax1*x+ax2*y) for x,y in profile)
        K=len(rings[0])
        def face(ids,uvs,k):self.faces.append(ids);self.uv.append(uvs);self.mi.append(k)
        for idx,reverse in ((0,True),(len(rings)-1,False)):
            seq=list(range(K));seq=seq[::-1] if reverse else seq
            p=rings[idx]
            face([lo+idx*K+i for i in seq],[(.5+p[i][0]/width*.69,.5+p[i][1]/height*.69) for i in seq],1 if material==0 else 3)
        for ring in range(len(rings)-1):
            t0,t1=sections[ring][0],sections[ring+1][0]
            for i in range(K):
                j=(i+1)%K
                ids=[lo+ring*K+i,lo+ring*K+j,lo+(ring+1)*K+j,lo+(ring+1)*K+i]
                p,q=rings[ring][i],rings[ring][j]
                u0=(t0+length/2)/length;u1=(t1+length/2)/length
                # UV along each timber grain. Every member gets its own crop of a 2 m repeat.
                if material==0:
                    side=np.linalg.norm(np.array(p)-q)/2
                    offset=(self.part*.381966)%1;v0=(self.part*.171+i*.309)%1
                    uv=[(t0/2+offset,v0),(t0/2+offset,v0+side),(t1/2+offset,v0+side),(t1/2+offset,v0)]
                else:
                    a,bp=rings[ring][i],rings[ring][j]
                    side=np.linalg.norm(np.array(a)-bp)
                    # Ferrules have one pair of pins per broad face. Orient trim across that face.
                    uv=[(.018,.015+.97*u0),(.982,.015+.97*u0),(.982,.015+.97*u1),(.018,.015+.97*u1)]
                    if material==3:uv=[(t0/.48,0),(t0/.48,side/.48),(t1/.48,side/.48),(t1/.48,0)]
                face(ids,uv,material if material!=2 or side>.065 else 3)
    def mesh(self,name,coll):
        o=obj(name,self.v,self.faces,coll)
        for m in mats:o.data.materials.append(m)
        uv=o.data.uv_layers.new(name='JoinerySurface')
        for f,coords,mi in zip(o.data.polygons,self.uv,self.mi):
            f.material_index=mi
            for i,val in zip(f.loop_indices,coords):uv.data[i].uv=val
        mod=o.modifiers.new('Export topology','TRIANGULATE')
        select([o],o);bpy.ops.object.modifier_apply(modifier=mod.name)
        return o

def build(tier,rail):
    j=Joinery(tier)
    def wood(axis,c,d,bevel=.009):j.member(axis,c,d[axis],d[(axis+1)%3],d[(axis+2)%3],0,bevel)
    def metal(axis,c,d,baked=True):j.member(axis,c,d[axis],d[(axis+1)%3],d[(axis+2)%3],2 if baked else 3,.004)
    if not rail:
        # Floor opening X=.375..1.625, Z=-.625..+.625. Reuse original removable socket.
        for x0,x1,z0,z1 in [(0,3.2,-1.5,-.625),(0,3.2,.625,1.5),(0,.375,-.625,.625),(1.625,3.2,-.625,.625)]:
            count=max(1,round((z1-z0)/.24));width=(z1-z0)/count
            for i in range(count):
                z=z0+(i+.5)*width
                wood(0,((x0+x1)/2,-.08,z),(x1-x0-.002,.16,width-.005),.004)
        for x in (.11,3.09):
            for z in (-1.4,1.4):
                wood(1,(x,1.07,z),(.175,2.32,.175),.008)
                for h in (.12,.69,1.06,2.19):
                    metal(1,(x,h,z),(.193,.104,.193))
        for z in (-1.4,1.4):
            segments=[(.11,3.09)] if z<0 else [(.11,.78),(2.42,3.09)]
            for a,b in segments:
                for h in (.22,.63,1.04):wood(0,((a+b)/2,h,z),(b-a,.112,.124))
            wood(0,(1.6,2.245,z),(3.17,.14,.146))
        for x in (.11,3.09):
            for a,b in ([(-1.40,-.76),(.76,1.4)] if x<1 else [(-1.4,1.4)]):
                for h in (.22,.63,1.04):wood(2,(x,h,(a+b)*.5),(.124,.112,b-a))
        wood(2,(1.60,2.27,0),(.154,.13,2.95));metal(0,(1.6,2.27,0),(.14,.145,.17))
        for z in (-1.39,1.39):wood(0,(1.6,-.203,z),(3.17,.115,.14))
        for x in (.11,3.09):wood(2,(x,-.202,0),(.14,.115,2.78))
    else:
        for x1,z1,x2,z2 in [(0,.22,27.8,.22),(0,21.78,27.8,21.78),(.16,.3,.16,8.7),(.16,13.3,.16,21.7),(27.84,.3,27.84,9.25),(27.84,12.75,27.84,21.7)]:
            ax=0 if x1!=x2 else 2;length=abs(x2-x1)+abs(z2-z1);n=ceil(length/2.5)
            for k in range(n+1):
                x=x1+(x2-x1)*k/n;z=z1+(z2-z1)*k/n
                if (ax==2 and (k==0 and z<1 or k==n and z>21)):continue
                wood(1,(x,.59,z),(.226,1.18,.226))
                metal(1,(x,1.117,z),(.238,.095,.238))
            for k in range(n):
                x=x1+(x2-x1)*(k+.5)/n;z=z1+(z2-z1)*(k+.5)/n
                for y in (.70,1.047):
                    dims=[.138,.136,.138];dims[ax]=length/n-.003
                    wood(ax,(x,y,z),dims)
    return j

stats={}
sources=new_collection('02_MASTER_precise_joinery')
runtime=new_collection('03_EXPORT')
for name,rail in [('CounterweightBasket',False),('UpperLandingRail',True)]:
    lods=[]
    for tier in (-1,0,1,2):
        # At mid range replace three short bevel rings with a single edge bevel.
        j=build(tier,rail)
        ob=j.mesh(name+('_HIGH' if tier<0 else '_LOD'+str(tier)),sources if tier<0 else runtime)
        ob.data.calc_loop_triangles();stats[ob.name]=len(ob.data.loop_triangles)
        if tier<0:continue
        lods.append(ob)
    markers=[]
    for label,b in [('Origin',(0,0,0)),('X',(-1,0,0)),('Y',(0,0,1)),('Z',(0,1,0))]:
        o=bpy.data.objects.new('Frame'+label,None);runtime.objects.link(o);o.location=b;markers.append(o)
    select(lods+markers,lods[0]);bpy.ops.export_scene.fbx(filepath=str(dst/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',use_tspace=True,mesh_smooth_type='FACE',bake_anim=False,add_leaf_bones=False,path_mode='RELATIVE')
    for m in markers:bpy.data.objects.remove(m,do_unlink=True)
    for m in lods[1:]:m.hide_set(True);m.hide_render=True
    # Spatially separate *only* source-display objects after export, using local collection offsets.
    if rail:
        for m in lods:m.location.y+=6
sources.hide_viewport=True;sources.hide_render=True
for im in bpy.data.images:
    if im.source=='FILE' and im.filepath:
        try:im.pack()
        except RuntimeError: pass
bpy.ops.wm.save_as_mainfile(filepath=str(src/'ApprovedBasket.blend'),compress=True)
(src/'mesh_counts.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
print('Basket mesh stats',stats)
