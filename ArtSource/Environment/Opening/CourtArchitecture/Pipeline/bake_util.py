"""Banked selected-to-active bakes from imported, meter-fit Hunyuan meshes."""
import bpy, math, sys
from pathlib import Path
import numpy as np

def select(objects,active):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=active

def emitter_route(mat, mask=False):
    """Retain the GLB pixel colour, texture factor, or colour attribute used by its shader."""
    if not mat.use_nodes:mat.use_nodes=True
    tree=mat.node_tree
    output=next((n for n in tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output),None)
    if not output:return lambda:None
    incoming=next(iter(output.inputs['Surface'].links),None)
    original=incoming.from_socket if incoming else None
    bs=next((n for n in tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    emission=tree.nodes.new('ShaderNodeEmission');emission.inputs['Strength'].default_value=1
    combine=None
    if mask and bs:
        combine=tree.nodes.new('ShaderNodeCombineColor')
        for channel,src in [('Red','Metallic'),('Green','Roughness')]:
            sock=bs.inputs[src]
            if sock.is_linked:tree.links.new(sock.links[0].from_socket,combine.inputs[channel])
            else:combine.inputs[channel].default_value=sock.default_value
        combine.inputs["Blue"].default_value=1
        tree.links.new(combine.outputs[0],emission.inputs["Color"])
    elif bs:
        bc=bs.inputs['Base Color']
        if bc.is_linked:tree.links.new(bc.links[0].from_socket,emission.inputs['Color'])
        else:emission.inputs['Color'].default_value=bc.default_value
    else:emission.inputs['Color'].default_value=mat.diffuse_color
    tree.links.new(emission.outputs[0],output.inputs['Surface'])
    def restore():
        tree.nodes.remove(emission)
        if combine:tree.nodes.remove(combine)
        if original:tree.links.new(original,output.inputs['Surface'])
    return restore

def unwrap(low,margin=.011):
    select([low],low)
    for layer in list(low.data.uv_layers):low.data.uv_layers.remove(layer)
    low.data.uv_layers.new(name='BakedUV')
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(68),island_margin=margin,area_weight=.45,correct_aspect=True,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')

def bake(high,low,output,asset,resolution,distance):
    tree=low.data.materials[0].node_tree;nodes=tree.nodes
    target=nodes.new('ShaderNodeTexImage');nodes.active=target
    select([high,low],low);high.hide_render=False;low.hide_render=False
    bake=bpy.context.scene.render.bake;bake.use_selected_to_active=True;bake.max_ray_distance=distance*2.75;bake.cage_extrusion=distance
    bake.margin=max(6,resolution//128)
    results={}
    for suffix,kind in [('BaseColor','EMIT'),('Normal','NORMAL'),('Mask','EMIT')]:
        image=bpy.data.images.new(asset+'_'+suffix,width=resolution,height=resolution,alpha=suffix=='Mask',is_data=suffix!='BaseColor')
        target.image=image;nodes.active=target
        restorers=[emitter_route(mat,suffix=='Mask') for mat in high.data.materials if mat] if suffix!='Normal' else []
        try:bpy.ops.object.bake(type=kind,normal_space='TANGENT')
        finally:
            for restore in restorers:restore()
        if suffix=='Mask':
            a=np.empty(resolution*resolution*4,dtype='f');image.pixels.foreach_get(a);a=a.reshape((-1,4))
            a[:,3]=1-np.clip(a[:,1],0,1);a[:,1]=1;a[:,2]=0
            image.pixels.foreach_set(a.ravel());image.alpha_mode='CHANNEL_PACKED';image.update()
        image.filepath_raw=str(output/(asset+'_'+suffix+'.png'));image.file_format='PNG';image.save()
        px=np.empty(resolution*resolution*4,dtype='f');image.pixels.foreach_get(px);px=px.reshape(-1,4)[:,:3]
        if not np.isfinite(px).all():raise ValueError(asset+' nonfinite bake')
        results[suffix]=image
    nodes.remove(target)
    receiver=tree.nodes.get("Principled BSDF")
    for kind in results:
        tex=nodes.new("ShaderNodeTexImage");tex.image=results[kind]
        if kind=="BaseColor":tree.links.new(tex.outputs['Color'],receiver.inputs['Base Color'])
        elif kind=="Normal":
            normal=nodes.new('ShaderNodeNormalMap');tree.links.new(tex.outputs['Color'],normal.inputs['Color'])
            tree.links.new(normal.outputs["Normal"],receiver.inputs['Normal'])
    receiver.inputs['Roughness'].default_value=.76
