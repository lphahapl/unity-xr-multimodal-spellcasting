"""Bake the B1 ornament material trial into the original low-poly UV atlas."""
from pathlib import Path
from hashlib import sha256
from math import pi
import ast
import bpy
from mathutils import Vector
import json
import math
import numpy as np
import sys

args=sys.argv[sys.argv.index("--")+1:]
base=Path(args[0]).resolve()
out=Path(args[1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(base/'Pedestal_B1.blend'))
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=8
low=bpy.data.objects['PedestalBody']
high=bpy.data.objects['PedestalBody_High']
left=bpy.data.objects['LeftSlidingPin']
right=bpy.data.objects['RightSlidingPin']
high.hide_render=False
high.hide_set(False)


def mesh_signature(obj):
    mesh=obj.data
    digest=sha256()
    def add(collection,field,shape,dtype):
        values=np.empty(shape,dtype=dtype)
        collection.foreach_get(field,values)
        digest.update(values.tobytes())
    add(mesh.vertices,'co',len(mesh.vertices)*3,np.float32)
    add(mesh.loops,'vertex_index',len(mesh.loops),np.int32)
    add(mesh.polygons,'loop_total',len(mesh.polygons),np.int32)
    add(mesh.uv_layers.active.uv,'vector',len(mesh.loops)*2,np.float32)
    return digest.hexdigest()
before={obj.name:mesh_signature(obj) for obj in (low,left,right)}


def active(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[-1]


def math_node(tree,operation,first,second):
    n=tree.nodes.new('ShaderNodeMath')
    n.operation=operation
    for target,source in zip(n.inputs,(first,second)):
        if isinstance(source,(int,float)):
            target.default_value=source
        else:
            tree.links.new(source,target)
    return n.outputs[0]


def assign(tree,target,default):
    if isinstance(default,(int,float,tuple)):
        target.default_value=default
    else:tree.links.new(default,target)


def facade(material,name,image,u_axis,v_axis,width,height,centre_v,style):
    result=material.copy()
    result.name=name
    tree=result.node_tree
    nodes=tree.nodes
    shader=nodes.get('Principled BSDF')
    texcoord=next(n for n in nodes if n.bl_idname=='ShaderNodeTexCoord')
    object_coords=texcoord.outputs['Object']
    separate=nodes.new('ShaderNodeCombineXYZ')
    for i,axis,scale,centre in [(0,u_axis,width,0),(1,v_axis,height,centre_v)]:
        dot=nodes.new('ShaderNodeVectorMath')
        dot.operation='DOT_PRODUCT'
        dot.inputs[1].default_value=axis
        tree.links.new(object_coords,dot.inputs[0])
        offset=math_node(tree,'ADD',math_node(tree,'MULTIPLY',dot.outputs['Value'],1./scale),.5-centre/scale)
        tree.links.new(offset,separate.inputs[i])
    tex=nodes.new('ShaderNodeTexImage')
    tex.name=name+' 投影'
    tex.image=image
    tex.extension='CLIP'
    tree.links.new(separate.outputs[0],tex.inputs['Vector'])
    rgb=nodes.new('ShaderNodeSeparateColor')
    tree.links.new(tex.outputs['Color'],rgb.inputs[0])
    red,green,blue=[rgb.outputs[ch] for ch in ('Red','Green','Blue')]
    p=shader.inputs
    normal=p['Normal'].links[0].from_socket
    initial=p['Base Color'].links[0].from_socket
    m=p['Metallic'].links[0].from_socket if p['Metallic'].is_linked else p['Metallic'].default_value

    def mix_color(a,b,factor):
        node=nodes.new('ShaderNodeMixRGB')
        assign(tree,node.inputs[0],factor);assign(tree,node.inputs[1],a);assign(tree,node.inputs[2],b)
        return node.outputs[0]
    def mix_scalar(a,b,factor):
        return math_node(tree,'ADD',math_node(tree,'MULTIPLY',a,math_node(tree,'SUBTRACT',1,factor)),
                         math_node(tree,'MULTIPLY',b,factor))

    if style=='side':
        initial=mix_color(initial,(.099,.128,.101,1),.38)
        # Green patina in low places, worn warm peaks, blackened engraved scale grooves.
        worn=math_node(tree,'MULTIPLY',red,.82)
        colour=mix_color(initial,(.303,.202,.111,1),worn)
        colour=mix_color(colour,(.33,.23,.113,1),math_node(tree,'MULTIPLY',green,.40))
        colour=mix_color(colour,(.046,.050,.036,1),math_node(tree,'MULTIPLY',blue,.29))
        gloss=mix_scalar(.62,.43,red)
        metal=mix_scalar(math_node(tree,'MULTIPLY',m,.73),.46,worn)
        height_socket=math_node(tree,'SUBTRACT',red,math_node(tree,'MULTIPLY',blue,.26))
        amplitude=.0025
    elif style=='front':
        colour=mix_color(initial,(.164,.121,.066,1),math_node(tree,'MULTIPLY',red,.39))
        gloss=mix_scalar(.65,.56,red)
        metal=mix_scalar(m,.54,red)
        height_socket=red
        amplitude=.00046
    else:
        # Flush tarnished silver. Keep the catchlight broad enough for Quest mipmaps.
        inlay=math_node(tree,'MULTIPLY',green,.80)
        shade=mix_color(initial,(.335,.292,.223,1),inlay)
        warm=mix_color(shade,(.168,.120,.070,1),math_node(tree,'MULTIPLY',green,.16))
        colour=mix_color(warm,(.055,.041,.028,1),math_node(tree,'MULTIPLY',blue,.65))
        gloss=mix_scalar(.72,.43,green)
        metal=mix_scalar(m,.55,green)
        height_socket=green
        amplitude=.00010
    bump=nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value=.78
    bump.inputs['Distance'].default_value=amplitude
    tree.links.new(height_socket,bump.inputs['Height'])
    tree.links.new(normal,bump.inputs['Normal'])
    tree.links.new(bump.outputs['Normal'],p['Normal'])
    tree.links.new(colour,p['Base Color'])
    tree.links.new(gloss,p['Roughness'])
    tree.links.new(metal,p['Metallic'])
    result['ornament']=style
    return result


def load_mask(name):
    texture=bpy.data.images.load(str(out/name))
    texture.colorspace_settings.name='Non-Color'
    return texture


interlace=load_mask('InterlaceRelief.png')
ground=load_mask('AnimalMaskGround.png')
silver=load_mask('SilverInlayMask.png')
source_mats={m.name:i for i,m in enumerate(high.data.materials)}
stone_idx=source_mats['浅灰赭石']
bronze=high.data.materials[source_mats['旧铜 · 灰绿包浆']]
deck_idx=source_mats['旧铜 · 盘内磨面']
dark_idx=source_mats['旧铜 · 阴刻与滑槽']
dark=high.data.materials[dark_idx]
deck=high.data.materials[deck_idx]
side_indices=[]
for idx,axis in enumerate(((1,0,0),(0,1,0),(-1,0,0),(0,-1,0))):
    side_indices.append(len(high.data.materials))
    high.data.materials.append(facade(bronze,'E 交缠龙纹 %d'%idx,interlace,axis,(0,0,1),.57,.34,.299,'side'))
front_idx=len(high.data.materials)
high.data.materials.append(facade(dark,'D 兽面细地纹',ground,(1,0,0),(0,0,1),.62,.09,.54,'front'))
top_idx=len(high.data.materials)
high.data.materials.append(facade(deck,'F 盘沿错银连纹',silver,(1,0,0),(0,1,0),.8,.8,0,'inlay'))
count={m.name:0 for m in high.data.materials if 'ornament' in m}
for poly in high.data.polygons:
    mat=poly.material_index
    x,y,z=poly.center
    n=poly.normal
    if mat==stone_idx:
        # The four sloping stone fields now represent separately cast inlaid bronze panels.
        if abs(y)>=abs(x):slot=side_indices[0 if y<0 else 2]
        else:slot=side_indices[1 if x>0 else 3]
        poly.material_index=slot
    elif mat==deck_idx and z>.607 and n.z>.80:
        poly.material_index=top_idx
    elif mat==dark_idx and y<-.3727 and .503<z<.578 and n.y<-.94:
        poly.material_index=front_idx
    else:
        continue
    count[high.data.materials[poly.material_index].name]+=1
assert all(v>0 for v in count.values()),count
print('Projected high surface polygon counts',count,flush=True)


def set_mask_emission(high,enable):
    for mat in high.data.materials:
        tree=mat.node_tree
        shader=tree.nodes.get('Principled BSDF')
        output=tree.nodes.get('Material Output').inputs['Surface']
        if not enable:
            tree.links.new(shader.outputs[0],output)
            continue
        existing=tree.nodes.get('Ornament mask bake')
        if not existing:
            emission=tree.nodes.new('ShaderNodeEmission')
            emission.name='Ornament mask bake'
            channels=tree.nodes.new('ShaderNodeCombineColor')
            for source,name in (('Metallic','Red'),('Roughness','Green')):
                item=shader.inputs[source]
                origin=item.links[0].from_socket if item.is_linked else item.default_value
                if name=='Green':origin=math_node(tree,'SUBTRACT',1,origin)
                assign(tree,channels.inputs[name],origin)
            channels.inputs['Blue'].default_value=1
            tree.links.new(channels.outputs[0],emission.inputs['Color'])
        else:emission=existing
        tree.links.new(emission.outputs[0],output)

# Reuse the source workflow's image formats, UV padding, tangent-normal bake and pin material.
functions={'output_image','dest','pick_image','baked_material','bake','shot','light'}
parsed=ast.parse((base/'build_pedestal_b1.py').read_text(encoding='utf8'))
selected=[node for node in parsed.body if isinstance(node,ast.FunctionDef) and node.name in functions]
assert {node.name for node in selected}==functions
exec(compile(ast.Module(body=selected,type_ignores=[]),'<B1 baseline functions>','exec'))
report=bake('B1Body_Ornate',low,high,2048)
after={obj.name:mesh_signature(obj) for obj in (low,left,right)}
assert before==after
report.update(unchangedGeometryAndUV=after,highPolygonCounts=count,
              lowTriangles=sum(p.loop_total-2 for p in low.data.polygons),blenderVersion=bpy.app.version_string)
(out/'OrnamentBake.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
high.hide_render=True
high.hide_set(True)
root=low.parent
scene.frame_set(1)
active([root,low,right,left])
bpy.ops.export_scene.gltf(filepath=str(out/'Pedestal_B1_OrnamentTrial.glb'),use_selection=True,
    export_format='GLB',export_extras=True,export_cameras=False,export_lights=False,
    export_tangents=True,export_animation_mode='SCENE',export_anim_scene_split_object=False)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Pedestal_B1_OrnamentTrial.blend'),compress=True)

scene.world=bpy.data.worlds.new('Workshop bronze studio')
scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background')
bg.inputs[0].default_value=(.235,.24,.245,1);bg.inputs[1].default_value=.49
light('Warm broad key',(-1.3,-1.8,2.0),(0,0,.33),190,1.45,(1,.92,.78))
light('Side relief rake',(1.5,-.75,1.10),(0,-.25,.51),105,.84,(.82,.87,1.))
light('Copper rim',(1.28,1.05,1.68),(0,0,.42),135,1.25,(1,.88,.67))
bpy.ops.mesh.primitive_plane_add(size=100,location=(0,0,-.002))
floor=bpy.context.object
ground_mat=bpy.data.materials.new('Matte studio ground')
ground_mat.diffuse_color=(.16,.146,.121,1)
floor.data.materials.append(ground_mat)
scene.view_settings.view_transform='AgX'
scene.render.resolution_x=1120;scene.render.resolution_y=960
scene.render.image_settings.file_format='JPEG';scene.render.image_settings.quality=92
scene.cycles.samples=16
shot('Ornate_Hero',(-1.52,-1.92,1.51),(0,0,.32),1.21)
shot('Ornate_Side',(2.55,-.28,.83),(.29,0,.34),.72)
scene.frame_set(85)
shot('Ornate_Top',(-.45,-.78,2.53),(0,0,.615),.96)
