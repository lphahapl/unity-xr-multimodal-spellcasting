"""Modeled source tiles transfer their relief and pigment to reusable metre-scale game trims."""
import bpy
import math
import numpy as np
from pathlib import Path

def select(objs, active):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active

def transfer(kind, folder, collection):
    size = 192
    vertices, faces, pigments = [], [], []
    for j in range(size + 1):
        for i in range(size + 1):
            u, v = i / size, j / size
            # A rim of duplicate periods supplies rays just outside the target footprint.
            x, y = u * 1.04 - .02, v * 1.04 - .02
            a, b = math.tau * x, math.tau * y
            if kind == 'Timber':
                grain = math.sin(29*b + 1.5*math.sin(a) + .5*math.sin(3*a+2*b))
                pores = math.sin(51*b + 1.2*math.sin(4*a)+grain)
                drift = math.sin(2*b+math.sin(a))*math.sin(b-a)
                height = .005+.00035*grain+.00022*pores+.0005*drift
                k=.60+.055*grain+.036*pores+.060*drift+.031*math.sin(5*a+7*b)*math.cos(9*b-2*a)
                color=(.155*k,.077*k,.039*k)
            else:
                # Six alternating convex tile caps and shallow pans. Staggered seams.
                phase = (6*x)%1
                cap = max(0, math.cos((phase-.5)*math.pi*2))**1.1
                pan = .5-.5*math.cos(phase*math.tau)
                seam = (3*y + (.5 if phase<.25 or phase>.75 else 0))%1
                step = max(0.,1.-abs(seam-.12)/.10)
                height = .008+.025*cap + .004*pan + .006*step
                grit=.04*math.sin(21*a+17*b)*math.cos(14*b-5*a)
                k=.82+.12*cap-.10*step+grit+.035*math.cos(2*a+b)
                color=(.056*k,.067*k,.079*k)
            vertices.append((x,y,height))
            pigments.append((*color,1.))
    for j in range(size):
        for i in range(size):
            k=j*(size+1)+i
            faces.append((k,k+1,k+size+2,k+size+1))
    hi=bpy.data.meshes.new(kind+'_SculptGrid');hi.from_pydata(vertices,[],faces);hi.update()
    high=bpy.data.objects.new(kind+'_HighRelief',hi);collection.objects.link(high)
    attr=hi.color_attributes.new(name='Pigment',type='FLOAT_COLOR',domain='POINT')
    attr.data.foreach_set('color',np.asarray(pigments,dtype=np.float32).ravel())
    mat=bpy.data.materials.new(kind+'_Pigment');mat.use_nodes=True
    nodes=mat.node_tree.nodes; bs=nodes.get('Principled BSDF')
    vertex=nodes.new('ShaderNodeVertexColor');vertex.layer_name='Pigment'
    mat.node_tree.links.new(vertex.outputs['Color'],bs.inputs['Base Color'])
    hi.materials.append(mat)
    for face in hi.polygons:face.use_smooth=True
    lo=bpy.data.meshes.new(kind+'_Receiver')
    lo.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2,3)]);lo.update()
    uv=lo.uv_layers.new(name='UVMap')
    for k,coord in enumerate(((0,0),(1,0),(1,1),(0,1))):uv.data[k].uv=coord
    low=bpy.data.objects.new(kind+'_BakePlane',lo);collection.objects.link(low)
    recv=bpy.data.materials.new(kind+'_ReceiverMat');recv.use_nodes=True;lo.materials.append(recv)
    tex=recv.node_tree.nodes.new('ShaderNodeTexImage');recv.node_tree.nodes.active=tex
    select([high,low],low)
    settings=bpy.context.scene.render.bake
    settings.use_selected_to_active=True;settings.cage_extrusion=.06
    settings.max_ray_distance=.11;settings.margin=4
    settings.use_pass_direct=False;settings.use_pass_indirect=False;settings.use_pass_color=True
    for suffix,bake_type in [('Color','DIFFUSE'),('Normal','NORMAL')]:
        im=bpy.data.images.new(kind+suffix,512,512,alpha=False,is_data=(suffix=='Normal'))
        tex.image=im
        bpy.ops.object.bake(type=bake_type)
        im.filepath_raw=str(folder/(kind+suffix+'.png'));im.file_format='PNG';im.save()
        pixels=np.empty(512*512*4,dtype='f');im.pixels.foreach_get(pixels)
        px=pixels.reshape((-1,4))[:,:3]
        if not np.isfinite(px).all() or float(np.max(np.ptp(px,axis=0)))<.025:
            raise ValueError('Flat or invalid bake '+im.name)
    high.hide_render=True;low.hide_render=True;high.hide_set(True);low.hide_set(True)
