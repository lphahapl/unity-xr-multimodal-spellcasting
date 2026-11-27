"""True relief: bronze step, cut gold seats and embedded exposed metal floors."""
import math
import bpy,bmesh
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
from qinggui_geometry import section

def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)

BZ=np.array([.105,.112,.120,.145,.170,.200,.230,.260,.290,.308,.317,.35])
BW=np.array([.106,.060,.029,.013,.008,.006,.006,.007,.009,.014,.022,.03])
def inner(z):return section(z)[1]-float(np.interp(z,BZ,BW))

def bezier(cps,count=1000):
    p=np.array(cps,float)
    for t in np.linspace(0,1,count):yield (1-t)**3*p[0]+3*(1-t)**2*t*p[1]+3*(1-t)*t*t*p[2]+t**3*p[3],float(t)

def trees(points_only=False):
    front=[];back=[]
    top=[[(.024,.315),(.024,.287),(-.036,.291),(-.040,.222)],[(.028,.317),(.030,.277),(-.044,.282),(-.043,.179)],[(.031,.318),(.039,.268),(-.043,.267),(-.040,.157)],[(.034,.320),(.043,.274),(-.031,.256),(-.033,.199)],[(.036,.322),(.042,.287),(-.004,.269),(-.027,.237)], [(-.037,.284),(-.031,.295),(-.020,.299),(-.012,.297)]]
    lower=[[(-.026,.123),(-.024,.141),(.014,.139),(.021,.162)],[(-.017,.120),(-.009,.137),(.025,.131),(.029,.151)]]
    for cps in top+lower:
        for p,t in bezier(cps):front.append((p,.00022+.00043*math.sin(math.pi*t)**.4))
    for cps in lower:
        for p,t in bezier(cps):back.append((p,.00022+.00037*math.sin(math.pi*t)**.4))
    border=[]
    for z in np.linspace(.110,.316,1600):
        for offset in [.0012,.0036]:border.append(((inner(z)+offset,z),.00050))
    # Gold line along the sloping crown and upper left return, tapering out into jade.
    for z in np.linspace(.273,.345,650):
        x=section(z)[0]+.0023;border.append(((x,z),.00030))
    for a in np.linspace(0,2*math.pi,850):border.append(((.041+.0078*math.cos(a),.329+.0133*math.sin(a)),.00038))
    # Geometric meander on the low bronze collar, as real recessed metal-filled cuts.
    path=[(-.5,-.0015),(-.35,-.0015),(-.35,.0015),(.35,.0015),(.35,-.0007),(-.05,-.0007),(-.05,.0005),(.15,.0005)]
    for k in range(8):
        x0=-.049+k*.013
        for a,b in zip(path,path[1:]):
            for t in np.linspace(0,1,40):border.append(((x0+.013*(a[0]+(b[0]-a[0])*t),.108+a[1]+(b[1]-a[1])*t),.00022))
    for z in [.1058,.1103]:
        for x in np.linspace(-.054,.054,600):border.append(((x,z),.00028))
    # Small crown thunder motif; no lettering.
    for a,b in zip([(.016,.323),(.016,.328),(.023,.328),(.023,.324),(.019,.324)],[(.016,.328),(.023,.328),(.023,.324),(.019,.324),(.019,.326)]):
        for t in np.linspace(0,1,60):border.append(((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t),.00025))
    if points_only:
        align=[((0,z),.00040) for z in np.linspace(.083,.103,180)]+[((x,.0975),.00040) for x in np.r_[np.linspace(-.013,-.005,80),np.linspace(.005,.013,80)]]
        return front+border+align,back+border+align
    def make(points):
        kd=KDTree(len(points));width=[]
        for i,(p,w) in enumerate(points):kd.insert((p[0],p[1],0),i);width.append(w)
        kd.balance();return kd,width
    return make(front+border),make(back+border)

def sculpt(ob):
    front,back=trees();metal=[];gold=[];groove=[];frontnesses=[];inners=[];outers=[]
    base_normals=[tuple(v.normal) for v in ob.data.vertices]
    normal_attr=ob.data.attributes.new('BaseNormal','FLOAT_VECTOR','POINT');normal_attr.data.foreach_set('vector',np.array(base_normals,np.float32).ravel())
    for v in ob.data.vertices:
        x,y,z=map(float,v.co);l,r,d,rr=section(max(0,min(.35,z)))
        frontness=smooth(d/2-rr-.003,d/2-rr*.3,abs(y))
        bronze=max(smooth(inner(z)-.0008,inner(z)+.0008,x),smooth(.313,.319,z),1-smooth(.110,.116,z)) if z>.105 else 0.
        # Upper-left rim only near crown; do not frame the entire left edge.
        if z>.272:bronze=max(bronze,(1-smooth(l+.002,l+.004,x))*smooth(.272,.285,z))
        kd,ws=front if y<0 else back
        _,idx,dist=kd.find((x,z,0));w=ws[idx]
        g=(1-smooth(w*.70,w*1.03,dist))*frontness if z>.105 else 0.
        cut=(1-smooth(w,w+.00040,dist))*frontness if z>.105 else 0.
        # Inset alignment line stays inside the protected 106 mm square envelope.
        if .082<z<.104 and frontness>.5:
            vertical=abs(x) if .083<=z<=.103 else math.hypot(x,min(abs(z-.083),abs(z-.103)))
            horizontal=math.hypot(max(0,.005-abs(x),abs(x)-.013),z-.0975)
            dm=min(vertical,horizontal)
            g=1-smooth(.00030,.00055,dm);cut=1-smooth(.00055,.00085,dm)
        metal.append(bronze);gold.append(g);groove.append(cut);frontnesses.append(frontness);inners.append(d/2-rr-.003);outers.append(d/2-rr*.3)
        if z>.105:
            # A carved seat 0.28mm deep with metal face 0.10mm above its floor: still inset.
            displacement=.00055*bronze-.00020*cut+.00014*g
            normal=Vector((x-max(l+rr,min(r-rr,x)),y-max(-d/2+rr,min(d/2-rr,y)),0)).normalized()
            v.co+=normal*displacement
        elif frontness>.65 and cut>0:v.co.y-=math.copysign(.00010*cut-.00004*g,y)
    for name,vals in [('Bronze',metal),('Gold',gold),('CutSeat',groove),('Frontness',frontnesses),('FrontInner',inners),('FrontOuter',outers)]:
        a=ob.data.attributes.get(name) or ob.data.attributes.new(name,'FLOAT','POINT');a.data.foreach_set('value',vals)
    ob.data.update()
    bm=bmesh.new();bm.from_mesh(ob.data)
    for _ in range(5):
        tiny=[f for f in bm.faces if f.calc_area()<1e-10]
        if not tiny:break
        edges=list({min(f.edges,key=lambda e:e.calc_length()) for f in tiny})
        bmesh.ops.collapse(bm,edges=edges,uvs=True)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-8)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
    return {'vertices_sculpted':len(ob.data.vertices),'seat_depth_m':.00020,'metal_face_above_seat_floor_m':.00014,'bronze_layer_height_m':.00055}

def material():
    mat=bpy.data.materials.new('QingGui_Jade_Bronze_Gold_Source');mat.use_nodes=True;nt=mat.node_tree;n=nt.nodes;ln=nt.links;n.clear()
    out=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');ln.new(bs.outputs['BSDF'],out.inputs['Surface'])
    def mathn(op,a,b=0):
        no=n.new('ShaderNodeMath');no.operation=op
        for i,v in enumerate([a,b]):
            if isinstance(v,(float,int)):no.inputs[i].default_value=v
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
    bronze=attr('Bronze')
    tc_mask=n.new('ShaderNodeTexCoord');xyz=n.new('ShaderNodeSeparateXYZ');ln.new(tc_mask.outputs['Object'],xyz.inputs[0])
    uv=n.new('ShaderNodeCombineXYZ');ln.new(mathn('DIVIDE',mathn('ADD',xyz.outputs['X'],.07),.14),uv.inputs['X']);ln.new(mathn('DIVIDE',xyz.outputs['Z'],.35),uv.inputs['Y'])
    from pathlib import Path
    masks=Path(__file__).resolve().parent/'SourceMasks'
    facing=mathn('LESS_THAN',xyz.outputs['Y'],0)
    ff=mathn('DIVIDE',mathn('SUBTRACT',mathn('ABSOLUTE',xyz.outputs['Y']),attr('FrontInner')),mathn('SUBTRACT',attr('FrontOuter'),attr('FrontInner')))
    front_mask=mathn('MINIMUM',1,mathn('MAXIMUM',0,ff))
    def masktex(kind):
        ims=[]
        for face in ['Back','Front']:
            no=n.new('ShaderNodeTexImage');no.image=bpy.data.images.load(str(masks/f'{kind}_{face}.png'),check_existing=True);no.image.colorspace_settings.name='Non-Color';no.extension='CLIP';ln.new(uv.outputs[0],no.inputs['Vector']);ims.append(no.outputs['Color'])
        return mathn('MULTIPLY',mix(facing,ims[0],ims[1]),front_mask)
    gold=masktex('Gold');cut_tex=masktex('CutSeat');met=mathn('MAXIMUM',bronze,gold);jade=mathn('SUBTRACT',1,met)
    tc=n.new('ShaderNodeTexCoord');noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=83;noise.inputs['Detail'].default_value=3.5;noise.inputs['Roughness'].default_value=.7;ln.new(tc.outputs['Object'],noise.inputs['Vector'])
    fine=n.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=1650;fine.inputs['Detail'].default_value=2;ln.new(tc.outputs['Object'],fine.inputs['Vector'])
    cloud=mix(noise.outputs['Fac'],(.64,.665,.565,1),(.75,.77,.68,1))
    patina=n.new('ShaderNodeValToRGB');patina.color_ramp.elements[0].position=.26;patina.color_ramp.elements[0].color=(.024,.038,.027,1);patina.color_ramp.elements[1].position=.43;patina.color_ramp.elements[1].color=(.080,.050,.024,1);ln.new(noise.outputs['Fac'],patina.inputs[0])
    base=mix(bronze,cloud,patina.outputs[0]);base=mix(gold,base,(.76,.49,.15,1))
    rough=mix(bronze,(.165,.165,.165,1),(.48,.48,.48,1));rough=mix(gold,rough,(.28,.28,.28,1))
    ln.new(base,bs.inputs['Base Color']);ln.new(rough,bs.inputs['Roughness']);ln.new(met,bs.inputs['Metallic']);bs.inputs['IOR'].default_value=1.53
    ln.new(mathn('MULTIPLY',jade,.28),bs.inputs['Subsurface Weight']);bs.inputs['Subsurface Radius'].default_value=(1,.75,.48);bs.inputs['Subsurface Scale'].default_value=.0065
    ln.new(mathn('MULTIPLY',jade,.02),bs.inputs['Transmission Weight'])
    shape_bump=n.new('ShaderNodeBump');shape_bump.inputs['Strength'].default_value=1;shape_bump.inputs['Distance'].default_value=1
    bn=n.new('ShaderNodeAttribute');bn.attribute_name='BaseNormal';vn=n.new('ShaderNodeVectorMath');vn.operation='NORMALIZE';ln.new(bn.outputs['Vector'],vn.inputs[0]);ln.new(vn.outputs[0],shape_bump.inputs['Normal'])
    height=mathn('ADD',mathn('MULTIPLY',bronze,.00055),mathn('ADD',mathn('MULTIPLY',cut_tex,-.00020),mathn('MULTIPLY',gold,.00014)))
    ln.new(height,shape_bump.inputs['Height'])
    bump=n.new('ShaderNodeBump');ln.new(shape_bump.outputs['Normal'],bump.inputs['Normal']);bump.inputs['Strength'].default_value=.06;bump.inputs['Distance'].default_value=.000006;ln.new(fine.outputs['Fac'],bump.inputs['Height']);ln.new(bump.outputs['Normal'],bs.inputs['Normal'])
    absorb=n.new('ShaderNodeVolumeAbsorption');absorb.inputs['Color'].default_value=(.78,.74,.63,1);absorb.inputs['Density'].default_value=18;ln.new(absorb.outputs['Volume'],out.inputs['Volume'])
    return mat,{'BaseColor':base,'Roughness':rough,'Metallic':met,'JadeMask':jade},(nt,out,bs)
