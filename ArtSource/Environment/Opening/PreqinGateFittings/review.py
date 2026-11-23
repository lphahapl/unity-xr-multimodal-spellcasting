"""Inspect metric source with the game profile; render seam/edge and material witnesses."""
import bpy,json,sys,math
from pathlib import Path
from mathutils import Vector
SRC=Path(__file__).resolve().parent
dir=SRC/'Review';dir.mkdir(exist_ok=True)
obs=[o for o in bpy.context.scene.objects if o.type=='MESH']
if '--qa' in sys.argv:
    sys.path.insert(0,str(Path.home()/'.codex/skills/blender-asset-qa/scripts'));import qa_check
    report={}
    for o in obs:
        o.hide_set(False)
        report[o.name]=qa_check.run(objects=[o.name],profile='game')
        print(o.name,report[o.name]['errors'],flush=True)
        o.hide_set(True)
    (SRC/'geometry-qa.json').write_text(json.dumps(report,indent=2),encoding='utf8')
else:
    s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.cycles.use_denoising=True
    s.render.threads_mode='FIXED';s.render.threads=8;s.world=bpy.data.worlds.new('Neutral daylight');s.world.use_nodes=True
    bg=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs[0].default_value=(.48,.55,.62,1);bg.inputs[1].default_value=.65
    bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.rotation_euler=Vector((.56,-.65,-1)).to_track_quat('-Z','Y').to_euler();sun.data.energy=2.8;sun.data.angle=.13
    bpy.ops.object.light_add(type='AREA',location=(-1,3,2));a=bpy.context.object;a.data.shape='DISK';a.data.size=4;a.data.energy=210;a.rotation_euler=(-a.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';s.render.image_settings.file_format='JPEG';s.render.image_settings.quality=93
    for o in obs:o.hide_render=True
    def shot(name,parts,target,view,size,res=(740,500)):
        for key,loc in parts:
            o=bpy.data.objects[key];o.location=loc;o.hide_render=False
        cam.location=Vector(target)+Vector(view);cam.rotation_euler=(-Vector(view)).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=size;cam.data.clip_start=.01
        s.render.resolution_x,s.render.resolution_y=res;s.render.resolution_percentage=100
        s.render.filepath=str(dir/(name+'.jpg'));bpy.ops.render.render(write_still=True)
        for key,_ in parts:
            o=bpy.data.objects[key];o.hide_render=True;o.location=(0,0,0)
    zero=(0,0,0)
    def clone(original,new):
        obj=bpy.data.objects[original].copy();obj.name=new;s.collection.objects.link(obj)
    clone('CheckGateStrap_LOD0','UpperBand')
    gate=[('CheckGateLeaf_LOD0',zero),('CheckGateStrap_LOD0',(0,.210,-1.08)),('UpperBand',(0,.210,1))]
    shot('GateFront',gate,(0,0,0),(2,7,1),3.95,(620,700))
    # Check actual retained hardware against the new leaf and its two moving bands.
    import re,numpy as np
    root=SRC.parents[3]
    overlay=[]
    for mesh_name,rgb,metal,rough in [('VerificationDoorIron',(.6745,.5098,.2824),.52,.59),('VerificationDoorBoards',(.302,.224,.184),0,.75)]:
        text=(root/'Assets/Meshes/Chapter01'/(mesh_name+'.asset')).read_text()
        count=int(re.search(r'm_VertexCount: (\d+)',text)[1])
        v=np.frombuffer(bytes.fromhex(re.search(r'_typelessdata: (.+)',text)[1]),'<f4').reshape(count,-1)[:,:3]*[2.02,3.08,.38]
        tri=np.frombuffer(bytes.fromhex(re.search(r'm_IndexBuffer: (.+)',text)[1]),'<u2').reshape(-1,3)
        data=bpy.data.meshes.new(mesh_name);data.from_pydata([(x,-z,y) for x,y,z in v],[],tri.tolist())
        mat=bpy.data.materials.new(mesh_name);mat.use_nodes=True;bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bs.inputs['Base Color'].default_value=tuple(c**2.2 for c in rgb)+(1,)
        bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough;data.materials.append(mat)
        obj=bpy.data.objects.new(mesh_name,data);s.collection.objects.link(obj);obj.hide_render=True;overlay.append((mesh_name,zero))
    shot('InstalledGate',gate+overlay,(0,0,0),(.86,6,.70),3.75,(610,700))
    shot('GateBack',gate,(0,0,0),(-2,-7,1),3.95,(620,700))
    shot('RivetDetail',gate,(-.45,.21,1),(.18,1,.45),.54)
    shot('Dais',[('SideGateDais_LOD0',zero)],zero,(6,8,6),8.9)
    shot('Coping',[('SideGateDais_LOD0',zero)],(1.90,2.78,.19),(1,2,1.1),1.9)
    shot('Inlay',[('WayfindingInlay_LOD0',zero)],(0,.1,0),(.74,.6,1.4),1.62)
    clone('GaugeFrameRail','LowerRail');clone('GaugeFrameStile','LeftStile')
    shot('Sight',[('GaugeFrameRail',(0,0,.19)),('LowerRail',(0,0,-.19)),('LeftStile',(-.19,0,0)),('GaugeFrameStile',(.19,0,0)),('GaugePost',(0,0,-.48))],(0,0,-.26),(.35,2,.16),1.10,(530,600))
    shot('Scar',[('HighImpactScar',zero)],zero,(.4,-.6,1.4),.83)
    # Actual LOD comparison in wire shading.
    s.render.engine='CYCLES';mat=bpy.data.objects['SideGateDais_LOD0'].data.materials[0];nodes=mat.node_tree.nodes
    wire=nodes.new('ShaderNodeWireframe');wire.use_pixel_size=True;wire.inputs[0].default_value=1.05
    mix=nodes.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(.72,.70,.62,1);mix.inputs[2].default_value=(.035,.033,.029,1)
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');mat.node_tree.links.new(wire.outputs[0],mix.inputs[0]);mat.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    for i in range(3):shot('Dais_LOD'+str(i),[('SideGateDais_LOD'+str(i),zero)],zero,(6,8,6),8.9,(460,280))
