"""R02 body has no ornamental gold seats: all upper gold is separate solid wire."""
import bpy,bmesh,math
import numpy as np
from functools import lru_cache
from mathutils import Vector
from mathutils.kdtree import KDTree
from qinggui_geometry import section,slopes,value,point
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
BZ=np.array([.105,.112,.120,.145,.170,.200,.230,.260,.290,.308,.317,.35])
BW=np.array([.106,.060,.029,.013,.008,.006,.006,.007,.009,.014,.022,.03])
BS=slopes(BZ,BW)
@lru_cache(maxsize=8192)
def inner(z):return section(z)[1]-float(value(max(.105,min(.35,z)),BZ,BW,BS))
def border_distance(x,z):
    l,r,d,rr=section(max(0,min(.35,z)))
    ds=x-inner(z);dc=z-.316;dl=.113-z
    result=max(ds,dc,dl)
    if z>.272:
        width=.003*smooth(.272,.285,z);result=max(result,l+width-x)
    return result
def sculpt(ob):
    from fine_patterns import paths
    pp=[]
    for line,w in paths():
        for aa,bb in zip(line,line[1:]):
            for t in np.linspace(0,1,max(2,int(math.dist(aa,bb)/.00012))):pp.append(((aa[0]+(bb[0]-aa[0])*t,aa[1]+(bb[1]-aa[1])*t),w))
    kd=KDTree(len(pp))
    for i,(p,w) in enumerate(pp):kd.insert((p[0],p[1],0),i)
    kd.balance()
    base=[tuple(v.normal) for v in ob.data.vertices];bn=ob.data.attributes.new('BaseNormal','FLOAT_VECTOR','POINT');bn.data.foreach_set('vector',np.array(base,np.float32).ravel())
    bronze=[];engr=[];fine=[];finner=[];fouter=[];heights=[];joints=[]
    uv=ob.data.uv_layers.active;params={}
    for lp in ob.data.loops:
        co=uv.data[lp.index].uv
        if co.y<.74 and lp.vertex_index not in params:params[lp.vertex_index]=(co.x-.02)/.96*28
    for v in ob.data.vertices:
        x,y,z=map(float,v.co);l,r,d,rr=section(max(0,min(.35,z)))
        signed=border_distance(x,z) if z>.105 else -1.
        br=smooth(-.00015,.00015,signed) if z>.105 else 0.
        joint=math.exp(-((signed+.00036)/.00014)**2) if z>.105 else 0.
        height=.00050*br-.00010*joint
        frontness=smooth(d/2-rr-.003,d/2-rr*.3,abs(y))
        _,ki,dist=kd.find((x,z,0));fc=(1-smooth(pp[ki][1]*.75,pp[ki][1]+.00014,dist))*frontness if z>.105 else 0.
        cut=0.
        if .082<z<.104 and abs(y)>d/2-.002:
            vertical=abs(x) if .083<=z<=.103 else math.hypot(x,min(abs(z-.083),abs(z-.103)))
            horizontal=math.hypot(max(0,.005-abs(x),abs(x)-.013),z-.0975)
            cut=1-smooth(.00040,.00085,min(vertical,horizontal))
            v.co.y-=math.copysign(.00010*cut,y)
        if z>.105:
            normal=Vector((x-max(l+rr,min(r-rr,x)),y-max(-d/2+rr,min(d/2-rr,y)),0)).normalized()
            # Feathered, rounded bronze skin follows body and is never a hard extruded shelf.
            hole_eq=((x-.041)/.0065)**2+((z-.329)/.012)**2
            lipfade=smooth(1.15,1.65,hole_eq) if z>.313 else 1.
            v.co+=normal*(height*smooth(.105,.107,z)*lipfade-.00016*fc)
            if br>.001 and hole_eq>2.0 and z<.3495 and v.index in params:
                t=params[v.index];da=point(z,(t+.0001)%28)-point(z,(t-.0001)%28);dz=point(min(.35,z+.00001),t)-point(max(0,z-.00001),t)
                an=da.cross(dz).normalized();base[v.index]=tuple(Vector(base[v.index]).lerp(an,br).normalized())
        bronze.append(br);engr.append(cut);fine.append(fc);finner.append(d/2-rr-.003);fouter.append(d/2-rr*.3);heights.append(height);joints.append(joint)
    bn.data.foreach_set('vector',np.array(base,np.float32).ravel())
    for name,vals in [('Bronze',bronze),('CalibrationCut',engr),('FinePattern',fine),('FaceInner',finner),('FaceOuter',fouter),('MetalStepHeight',heights),('MetalJoint',joints)]:
        a=ob.data.attributes.new(name,'FLOAT','POINT');a.data.foreach_set('value',vals)
    bm=bmesh.new();bm.from_mesh(ob.data)
    # The dense Boolean can leave an isolated triangular fin attached along
    # one otherwise-manifold lip edge. Remove only that diagnostic topology.
    fins=[f for f in bm.faces if f.calc_area()<1e-8 and sum(e.is_boundary for e in f.edges)>=2 and any(len(e.link_faces)>2 for e in f.edges)]
    if fins:bmesh.ops.delete(bm,geom=fins,context='FACES')
    for _ in range(5):
        tiny=[f for f in bm.faces if f.calc_area()<1e-10]
        if not tiny:break
        bmesh.ops.collapse(bm,edges=list({min(f.edges,key=lambda e:e.calc_length()) for f in tiny}),uvs=True)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-8);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
    bm=bmesh.new();bm.from_mesh(ob.data)
    fins=[f for f in bm.faces if f.calc_area()<1e-7 and sum(e.is_boundary for e in f.edges)>=2 and any(len(e.link_faces)>2 for e in f.edges)]
    if fins:bmesh.ops.delete(bm,geom=fins,context='FACES')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
    normals=[]
    for p in ob.data.polygons:
        wall=abs(p.normal.y)<.2 and all(abs(((ob.data.vertices[i].co.x-.041)/.0065)**2+((ob.data.vertices[i].co.z-.329)/.012)**2-1)<.05 for i in p.vertices)
        for li in p.loop_indices:
            vi=ob.data.loops[li].vertex_index;v=ob.data.vertices[vi].co
            near_joint=v.z>.105 and abs(border_distance(float(v.x),float(v.z)))<.0010
            n=Vector((-(v.x-.041)/.0065**2,0,-(v.z-.329)/.012**2)).normalized() if wall else (ob.data.vertices[vi].normal.copy() if near_joint else Vector(ob.data.attributes['BaseNormal'].data[vi].vector))
            normals.append(tuple(n))
    a=ob.data.attributes.new('HoleSafeNormal','FLOAT_VECTOR','CORNER');a.data.foreach_set('vector',np.array(normals,np.float32).ravel())
    return {'bronze_max_relief_m':.00050,'edge_rounding_width_m':.00030,'joint_depth_m':.00010,'protected_calibration_cut_depth_m':.00010,'ornamental_gold_seats_removed':True,'copper_normal_basis':'continuous analytic profile derivatives except protected aperture/crown'}

def material():
    mat=bpy.data.materials.new('QingGuiR02_Body_Source');mat.use_nodes=True;nt=mat.node_tree;n=nt.nodes;ln=nt.links;n.clear()
    out=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');ln.new(bs.outputs[0],out.inputs['Surface'])
    def mathn(op,a,b=0):
        no=n.new('ShaderNodeMath');no.operation=op
        for i,v in enumerate([a,b]):
            if isinstance(v,(int,float)):no.inputs[i].default_value=v
            else:ln.new(v,no.inputs[i])
        return no.outputs[0]
    def mix(f,a,b):
        no=n.new('ShaderNodeMixRGB')
        for i,v in enumerate([f,a,b]):
            if isinstance(v,tuple):no.inputs[i].default_value=v
            elif isinstance(v,(int,float)):no.inputs[i].default_value=v
            else:ln.new(v,no.inputs[i])
        return no.outputs[0]
    def attr(name):
        a=n.new('ShaderNodeAttribute');a.attribute_name=name;return a.outputs['Fac']
    bronze=attr('Bronze');cut=attr('CalibrationCut');jade=mathn('SUBTRACT',1,bronze)
    # Only the approved small collar/crown details remain engraved. All long
    # gold flow/border lines are absent from this body material and geometry.
    from pathlib import Path
    tcpat=n.new('ShaderNodeTexCoord');xyz=n.new('ShaderNodeSeparateXYZ');ln.new(tcpat.outputs['Object'],xyz.inputs[0]);uv=n.new('ShaderNodeCombineXYZ')
    ln.new(mathn('DIVIDE',mathn('ADD',xyz.outputs['X'],.07),.14),uv.inputs['X']);ln.new(mathn('DIVIDE',xyz.outputs['Z'],.35),uv.inputs['Y'])
    ft=n.new('ShaderNodeTexImage');ft.image=bpy.data.images.load(str(Path(__file__).resolve().parent/'SourceMasks/FinePattern.png'),check_existing=True);ft.image.colorspace_settings.name='Non-Color';ft.extension='CLIP';ln.new(uv.outputs[0],ft.inputs['Vector'])
    ff=mathn('DIVIDE',mathn('SUBTRACT',mathn('ABSOLUTE',xyz.outputs['Y']),attr('FaceInner')),mathn('SUBTRACT',attr('FaceOuter'),attr('FaceInner')))
    fine=mathn('MULTIPLY',ft.outputs['Color'],mathn('MINIMUM',1,mathn('MAXIMUM',0,ff)))
    tc=n.new('ShaderNodeTexCoord');cloud=n.new('ShaderNodeTexNoise');cloud.inputs['Scale'].default_value=73;cloud.inputs['Detail'].default_value=4;cloud.inputs['Roughness'].default_value=.65;ln.new(tc.outputs['Object'],cloud.inputs['Vector'])
    jadecolor=mix(cloud.outputs['Fac'],(.68,.70,.625,1),(.735,.75,.685,1))
    copper_noise=n.new('ShaderNodeTexNoise');copper_noise.inputs['Scale'].default_value=210;copper_noise.inputs['Detail'].default_value=2;ln.new(tc.outputs['Object'],copper_noise.inputs['Vector'])
    patina=n.new('ShaderNodeValToRGB');patina.color_ramp.elements[0].position=.2;patina.color_ramp.elements[0].color=(.075,.045,.023,1);patina.color_ramp.elements[1].position=.8;patina.color_ramp.elements[1].color=(.090,.055,.030,1);ln.new(copper_noise.outputs['Fac'],patina.inputs[0])
    base=mix(bronze,jadecolor,patina.outputs[0]);base=mix(mathn('MULTIPLY',cut,.7),base,(.11,.105,.075,1))
    base=mix(fine,base,(.47,.28,.075,1))
    base=mix(mathn('MULTIPLY',attr('MetalJoint'),.65),base,(.035,.030,.021,1))
    rough=mix(bronze,(.13,.13,.13,1),(.43,.43,.43,1))
    ln.new(base,bs.inputs['Base Color']);ln.new(rough,bs.inputs['Roughness']);ln.new(bronze,bs.inputs['Metallic']);bs.inputs['IOR'].default_value=1.53
    ln.new(mathn('MULTIPLY',jade,.32),bs.inputs['Subsurface Weight']);bs.inputs['Subsurface Radius'].default_value=(1,.78,.55);bs.inputs['Subsurface Scale'].default_value=.0055
    ln.new(mathn('MULTIPLY',jade,.055),bs.inputs['Transmission Weight'])
    ln.new(mathn('MULTIPLY',jade,.18),bs.inputs['Coat Weight']);bs.inputs['Coat Roughness'].default_value=.09
    # Smooth polish: no high frequency procedural bump on jade.
    bn=n.new('ShaderNodeAttribute');bn.attribute_name='HoleSafeNormal';vn=n.new('ShaderNodeVectorMath');vn.operation='NORMALIZE';ln.new(bn.outputs['Vector'],vn.inputs[0])
    bump=n.new('ShaderNodeBump');bump.inputs['Distance'].default_value=1;bump.inputs['Strength'].default_value=1;ln.new(vn.outputs[0],bump.inputs['Normal'])
    # The step uses its real support-ring geometry normals; differentiating a
    # point-interpolated step height created the grey sawtooth fringe in v0.
    ln.new(mathn('ADD',mathn('MULTIPLY',cut,-.00010),mathn('MULTIPLY',fine,-.00016)),bump.inputs['Height']);ln.new(bump.outputs[0],bs.inputs['Normal'])
    absorb=n.new('ShaderNodeVolumeAbsorption');absorb.inputs['Color'].default_value=(.82,.79,.71,1);absorb.inputs['Density'].default_value=23;ln.new(absorb.outputs[0],out.inputs['Volume'])
    return mat,{'BaseColor':base,'Roughness':rough,'Metallic':bronze,'JadeMask':jade},(nt,out,bs)
