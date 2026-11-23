"""Blender 5.2 headless. Run with -- <CourtMainRoof|CourtLowWing|CourtGatehouse>.
Reads the preserved H01/H02 production blend and replaces its three runtime tiers.
The original Blender datablock names, object names, pivots and export axes are retained.
"""
from pathlib import Path
import math
import json
import sys
import re
import hashlib
from collections import Counter
import bpy
import bmesh
from mathutils import Vector
from io_scene_fbx import parse_fbx, fbx_utils

HERE=Path(__file__).resolve().parent
sys.dont_write_bytecode=True
sys.path.insert(0,str(HERE))
from surface_uv import finish as finish_uv
SOURCE=HERE.parent
ROOT=SOURCE.parents[3]
DEST=ROOT/'Assets/Art/Environment/Opening/CourtArchitecture'
sys.dont_write_bytecode=True
ZONES={'roof':(0,0,1280,1280),'timber':(0,1280,1280,2048),
       'clay':(1280,0,2048,480),'stamp':(1280,480,2048,768),
       'end':(1280,768,2048,1104),'deck':(1280,1104,2048,1456),'stone':(1280,1456,2048,2048)}
# Half footprint X/Y, eave pan height, height at ridge, half ridge length.
MODELS={'CourtMainRoof':(9.55,8.20,.77,4.22,5.30),
        'CourtGatehouse':(3.60,2.40,3.79,5.24,3.60),
        'CourtLowWing':(7.40,4.50,.52,2.31,7.40)}

def atlas(zone,u,v):
    x,y,r,b=ZONES[zone]
    return ((x+8+u*(r-x-16))/2048, 1-(b-8-v*(b-y-16))/2048)

def fbx_ids(path):
    root,_=parse_fbx.parse(str(path))
    return sorted((e.id.decode(),e.props[1].split(b'\0')[0].decode(),e.props[0])
                  for g in root.elems if g.id==b'Objects' for e in g.elems
                  if e.id in (b'Model',b'Geometry',b'Material'))

class Builder:
    def __init__(self,tier):
        self.tier=tier
        self.vertices=[]
        self.faces=[]
        self.uv=[]
        self.smooth=[]
        self.parts=0

    def part(self):
        self.parts+=1
        self.lookup={}

    def face(self,points,zone,coords,smooth=False):
        ids=[]
        uvs=[]
        for p,uv in zip(points,coords):
            k=tuple(round(float(v),6) for v in p)
            if k not in self.lookup:
                self.lookup[k]=len(self.vertices)
                self.vertices.append(k)
            i=self.lookup[k]
            if i not in ids:
                ids.append(i)
                uvs.append(atlas(zone,*uv))
        if len(ids)>=3:
            self.faces.append(ids)
            self.uv.append(uvs)
            self.smooth.append(smooth)

    def timber(self,a,b,width,height,rounded=False,zone='timber',ring=False):
        a,b=Vector(a),Vector(b)
        direction=(b-a).normalized()
        side=direction.cross(Vector((0,0,1)))
        if side.length<.1:
            side=direction.cross(Vector((0,1,0)))
        side.normalize()
        up=side.cross(direction).normalized()
        self.part()
        if rounded:
            n=(7,6,5)[self.tier] if ring else (7,6,5)[self.tier]
            profile=[(math.cos(i*2*math.pi/n),math.sin(i*2*math.pi/n)) for i in range(n)]
        elif self.tier==0:
            profile=[(-.90,-1),(.90,-1),(1,-.87),(1,.87),(.90,1),(-.90,1),(-1,.87),(-1,-.87)]
        else:
            profile=[(-1,-1),(1,-1),(1,1),(-1,1)]
        def cross(p,scale):
            return [p+side*(x*width*.5*scale)+up*(y*height*.5*scale) for x,y in profile]
        stations=[0,1]
        if ring and self.tier==0:
            stations=[0,.135,1]
        elif zone=='timber' and self.tier<2:
            count=max(1,math.ceil((b-a).length/3.1))
            stations=[i/count for i in range(count+1)]
        rings=[cross(a.lerp(b,t),1.12 if ring and self.tier<2 and t<=.09 else 1) for t in stations]
        # End grain is fitted to each exposed cut. Long fibres run down the length.
        capzone='end' if zone=='timber' else ('stamp' if zone=='stamp' else zone)
        capuv=[(.50+.44*x,.5+.44*y) for x,y in profile]
        self.face(rings[0],capzone,capuv)
        if zone=='stamp':
            # The tapered back keys into the thicker covering tile. Only its rim is exposed.
            for i in range(len(profile)):
                j=(i+1)%len(profile)
                self.face([rings[0][i],rings[0][j],b],'clay',[(.05,.1),(.15,.1),(.1,.4)],True)
            return
        self.face(rings[-1],capzone,capuv)
        for step,(lo,hi) in enumerate(zip(rings,rings[1:])):
            for i in range(len(profile)):
                j=(i+1)%len(profile)
                offset=(self.parts*.137+i*.121)% .65
                s0,s1=(stations[step:step+2] if ring else (.02,.98))
                span=max(.014,min(.31,(width+height)*.24*math.dist(profile[i],profile[j])))
                self.face([lo[i],hi[i],hi[j],lo[j]],'clay' if zone=='stamp' else zone,
                          [(s0,offset),(s1,offset),(s1,offset+span),(s0,offset+span)],rounded)

    def append_gate(self,ob):
        # Preserve the four dressed stone bases, pillars, and surviving H02 brackets.
        bm=bmesh.new()
        bm.from_mesh(ob.data)
        cut=3.24
        bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],
                              dist=1e-6,plane_co=(0,0,cut),plane_no=(0,0,1),clear_outer=True)
        edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-cut)<1e-4 for v in e.verts)]
        bmesh.ops.holes_fill(bm,edges=edges,sides=0)
        bmesh.ops.triangulate(bm,faces=bm.faces[:])
        bm.normal_update()
        self.part()
        for f in bm.faces:
            points=[v.co.copy() for v in f.verts]
            c=f.calc_center_median()
            axis=max(range(3),key=lambda i:abs(f.normal[i]))
            stone=c.z<.405
            zone='stone' if stone else 'timber'
            if axis==2:
                uv=[(.5+p.x/8,.5+p.y/5.3) for p in points]
                if not stone: zone='end'
            elif stone or c.z>2.60:
                uv=[(.5+p[1-axis]/7,.055+p.z/4) for p in points]
            else:
                # Separate continuous UV projection for each timber post.
                middle=(2.11 if axis==1 else 1.29)*(1 if c[1-axis]>0 else -1)
                uv=[(.02+p.z/3.6,.5+(p[1-axis]-middle)*.88) for p in points]
            self.face(points,zone,uv)
        bm.free()

    def mesh(self,name):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices,[],self.faces)
        mesh.update()
        uv=mesh.uv_layers.new(name='Surface')
        for poly,coords,smooth in zip(mesh.polygons,self.uv,self.smooth):
            poly.use_smooth=smooth
            for i,coord in zip(poly.loop_indices,coords):
                uv.data[i].uv=coord
        bm=bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
        bmesh.ops.triangulate(bm,faces=bm.faces[:])
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        return mesh

def slope(bld,section,extent,depth,flat,eave,crown,orient,hip):
    # s traverses the eave, q runs up the slope in horizontal metres.
    width=extent*2
    count=round(width/.325)
    pitch=width/count
    tile_depth=.396
    def point(s,q,z):
        return orient(s,q,z)
    def height(q):
        t=q/depth
        return eave+(crown-eave)*(.92*t+.08*t*t)
    def limit(s):
        if flat>=extent: return depth-.025
        return min(depth-.028,max(0,depth*(extent-abs(s))/(extent-flat)-.044))
    corrugated=bld.tier==0 and (section!='end' or not hip)
    def profile(s):
        phase=((s+extent)/pitch)%1-.5
        return .069*math.sqrt(max(0,1-(phase/.236)**2)) if corrugated else .023
    steps=([.27,.50,.73],[.26,.50,.74],[0])[bld.tier]
    if corrugated:
        cols={-extent+pitch*(i+t) for i in range(count) for t in steps}
    else:
        cols={-extent+pitch*i for i in range(0,count,8)}
    rows=[i*tile_depth for i in range(0,math.ceil(depth/tile_depth)+1,8)]
    if rows[-1]<depth: rows.append(depth)
    if flat<extent:
        # A hip terminates at one point. Include it even on the sparse far-tier grid.
        cols.update((-flat,flat))
        for row in rows+[depth-.028]:
            x=extent-(row+.044)*(extent-flat)/depth
            if 0<x<extent: cols.update([-x,x])
    cols.update([-extent,extent])
    cols.update(-extent+pitch*i for i in range(0,count,8))
    cols=sorted(set(round(c,6) for c in cols))
    # Cut patch edges exactly at the two diagonal hips. Zero-width tips are omitted.
    bld.part()
    boundary=Counter()
    planar={}
    def key(s,q): return (round(s,5),round(q,5))
    def upper(s,q): return point(s,q,height(q)+profile(s))
    def lower(s,q):
        return point(s,q,height(q)-.059)
    for l,r in zip(cols,cols[1:]):
        ml,mr=limit(l),limit(r)
        for q0,q1 in zip(rows,rows[1:]):
            if q0>=max(ml,mr)-1e-6: continue
            line=[key(l,min(q0,ml)),key(r,min(q0,mr)),key(r,min(q1,mr)),key(l,min(q1,ml))]
            line=list(dict.fromkeys(line))
            if len(line)<3: continue
            a=math.floor(((l+r)/2+extent)/(8*pitch))
            b=math.floor((q0+1e-5)/(8*tile_depth))
            bld.face([upper(s,q) for s,q in line],'roof',
                     [((s+extent)/(8*pitch)-a,q/(8*tile_depth)-b) for s,q in line],True)
            for p,n in zip(line,line[1:]+line[:1]):
                boundary[tuple(sorted((p,n)))]+=1
    perimeter=[edge for edge,n in boundary.items() if n==1]
    vertices={v for e in perimeter for v in e}
    centre=(0, sum(v[1] for v in vertices)/len(vertices))
    def soffit_uv(s,q): return (.5+s/(width*1.02),.02+.96*q/depth)
    for a,b in perimeter:
        ax,ay=a
        bx,by=b
        bld.face([upper(*a),upper(*b),lower(*b),lower(*a)],'clay',
                 [(0,.72),(.22,.72),(.22,.9),(0,.9)])
        bld.face([lower(*a),lower(*b),lower(*centre)],'deck',
                 [soffit_uv(*a),soffit_uv(*b),soffit_uv(*centre)])
    # The fascia and repeated exposed rafter tails sit directly behind the pan lip.
    beam_z=height(.39)-.247
    bld.timber(point(-extent+.10,.39,beam_z),point(extent-.10,.39,beam_z),.22,.245)
    if bld.tier==0:
        for i in range(count):
            s=-extent+(i+.5)*pitch
            bld.timber(point(s,-.024,eave+.004),point(s,.12,eave+.042),.149,.14,True,'stamp')
    if bld.tier<2:
        n=round(width/(.59 if bld.tier==0 else .83))
        for i in range(n):
            s=-extent+(i+.5)*width/n
            run=min(limit(s)-.08,1.35 if extent>4 else .88)
            if run<.28: continue
            bld.timber(point(s,.003,eave-.112),point(s,run,height(run)-.112),.177,.172,True)
    return height

def build(bld,model,original):
    x,y,eave,crown,ridge=MODELS[model]
    hip=model=='CourtMainRoof'
    gate=model=='CourtGatehouse'
    if gate: bld.append_gate(original)
    def z(t): return eave+(crown-eave)*(.92*t+.08*t*t)
    for side in (-1,1):
        slope(bld,'front',x,y,ridge,eave,crown,
              lambda s,q,h:(s,side*(y-q),h),hip)
        if hip:
            slope(bld,'end',y,x-ridge,0,eave,crown,
                  lambda s,q,h:(side*(x-q),s,h),hip)
    # Ridge caps have a bell mouth at the downhill end. A buried joint separates courses.
    def caps(a,b,w,h):
        a,b=Vector(a),Vector(b)
        n=math.ceil((b-a).length/.79) if bld.tier<2 else 1
        for i in range(n):
            start=a.lerp(b,i/n)
            stop=a.lerp(b,(i+1)/n)+.013*(b-a).normalized()
            bld.timber(start,stop,w,h,True,'clay',True)
    caps((-ridge-.02,0,crown+.064),(ridge+.02,0,crown+.064),.39,.30)
    if hip:
        for sx in (-1,1):
            for sy in (-1,1):
                # Follow the two pitch segments to keep the hip roll bedded down.
                for lo,hi in [(0,.5),(.5,1)]:
                    a=(sx*(x-(x-ridge)*lo),sy*y*(1-lo),z(lo)+.045)
                    b=(sx*(x-(x-ridge)*hi),sy*y*(1-hi),z(hi)+.045)
                    caps(a,b,.30,.24)
    else:
        for sx in (-1,1):
            for sy in (-1,1):
                # Paired bargeboards and a narrow fired-clay verge define each hard gable.
                bld.timber((sx*(x-.052),sy*(y-.09),eave-.092),
                           (sx*(x-.052),sy*.012,crown-.129),.155,.215)
                setback=.092 if gate else .066
                a=(sx*(x-setback),sy*(y-.03),eave+(.020 if gate else .046))
                b=(sx*(x-setback),0,crown+(.018 if gate else .051))
                caps(a,b,.20,.155)
    # Full-width bearing ties and purlins under both pitches.
    base=3.27 if gate else (.14 if hip else .105)
    at=1.29 if gate else y*.83
    px=2.115 if gate else (x-.49 if not hip else ridge-.18)
    for sy in (-1,1):
        bld.timber((-px-.23,sy*at,base+.10),(px+.23,sy*at,base+.10),.32,.40 if gate else base*2+.20)
    for sx in (-1,1):
        xx=sx*px
        bld.timber((xx,-at-.207,base+.124),(xx,at+.207,base+.124),.292,.332)
        for sy in (-1,1):
            yy=sy*at
            seat=z((y-at)/y)-.19
            bld.timber((xx,yy,base+.17),(xx,yy,seat+.04),.245,.269)
            bld.timber((xx,yy,seat-.03),(xx,sy*.015,crown-.16),.23,.26)
        bld.timber((xx,0,base+.23),(xx,0,crown-.20),.26,.27)
        if not hip:
            # Exposed triangular gable truss nearer the verge.
            xx=sx*(x-.27)
            tie=eave-.30
            bld.timber((xx,-y+.20,tie),(xx,y-.20,tie),.19,.23)
            for sy in (-1,1):
                bld.timber((xx,sy*(y-.23),tie+.025),(xx,0,crown-.205),.18,.23)
            bld.timber((xx,0,tie+.035),(xx,0,crown-.20),.16,.16)
    bld.timber((-ridge+.03,0,crown-.20),(ridge-.03,0,crown-.20),.25,.28)

def material(tag,dest):
    mat=bpy.data.materials.new(tag+'_Baked')
    mat.use_nodes=True
    nt=mat.node_tree
    bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
    for kind in ('BaseColor','Normal','Mask'):
        tx=nt.nodes.new('ShaderNodeTexImage')
        tx.image=bpy.data.images.load(str(dest/(tag+'_'+kind+'.png')))
        if kind=='BaseColor':
            nt.links.new(tx.outputs['Color'],bs.inputs['Base Color'])
        else:
            tx.image.colorspace_settings.name='Non-Color'
            if kind=='Normal':
                normal=nt.nodes.new('ShaderNodeNormalMap')
                normal.inputs['Strength'].default_value=.78
                nt.links.new(tx.outputs['Color'],normal.inputs['Color'])
                nt.links.new(normal.outputs[0],bs.inputs['Normal'])
            else:
                tx.image.alpha_mode='CHANNEL_PACKED'
                inv=nt.nodes.new('ShaderNodeMath')
                inv.operation='MULTIPLY_ADD'
                inv.inputs[1].default_value=-.82
                inv.inputs[2].default_value=1
                nt.links.new(tx.outputs['Alpha'],inv.inputs[0])
                nt.links.new(inv.outputs[0],bs.inputs['Roughness'])
    return mat

def main(model):
    dest=DEST/model
    review=HERE/'Review'
    review.mkdir(exist_ok=True)
    fbx=dest/(model+'.fbx')
    baseline=review/(model+'_identity.json')
    if not baseline.exists():
        baseline.write_text(json.dumps(dict(fbx=fbx_ids(fbx),prefab_sha256=hashlib.sha256((dest/(model+'.prefab')).read_bytes()).hexdigest(),
            mesh_meta_sha256=hashlib.sha256(Path(str(fbx)+'.meta').read_bytes()).hexdigest()),indent=2))
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/model/(model+'_pipeline.blend')))
    # Imported source stays available separately in the archived production blend.
    tiers=[bpy.data.objects[f'{model}_Tier{i}'] for i in range(3)]
    for o in list(bpy.context.scene.objects):
        if o not in tiers: bpy.data.objects.remove(o,do_unlink=True)
    for i in reversed(range(3)):
        o=tiers[i]
        old=o.data
        meshname=old.name
        old.name='Previous_'+meshname
        for m in old.materials: m.name='Previous_'+m.name
        b=Builder(i)
        build(b,model,tiers[1] if model=='CourtGatehouse' and i==2 else o)
        mesh=b.mesh(meshname)
        o.data=mesh
        mesh.materials.append(material(o.name,dest))
        o.hide_set(False)
        o.hide_render=False
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active=o
        # Collapse in the registered asset's original per-tier budget before UV2 packing.
        budgets={'CourtMainRoof':(11600,4950,1750),'CourtLowWing':(8100,3400,1250),
                 'CourtGatehouse':(12500,5400,1840)}
        budget=budgets[model][i]
        if len(mesh.polygons)>budget:
            dec=o.modifiers.new('Static silhouette budget','DECIMATE')
            dec.ratio=budget/len(mesh.polygons)
            dec.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=dec.name)
            mesh=o.data
        mesh.uv_layers.new(name='Lightmap')
        mesh.uv_layers.active_index=1
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.004,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh.uv_layers.active_index=0
        mesh.uv_layers[0].active_render=True
        finish_uv(mesh,model,i,MODELS,ZONES)
        print(o.name,mesh.name,len(mesh.polygons),'tris',flush=True)
        path=dest/(o.name+'.mat')
        text=path.read_text()
        for key,value in {'_Smoothness':'.82','_Metallic':'0','_BumpScale':'.78'}.items():
            text=re.sub(r'(- '+key+r': )[^\n]+',lambda m:m[1]+value,text)
        path.write_text(text)
    publish(model,tiers,fbx,baseline)

def publish(model,tiers,fbx,baseline):
    dest=fbx.parent
    bpy.ops.object.select_all(action='DESELECT')
    for o in tiers:
        o.hide_set(False)
        o.hide_render=False
        o.select_set(True)
    bpy.context.view_layer.objects.active=tiers[0]
    expected=json.loads(baseline.read_text())
    original_ids={(kind,name):number for kind,name,number in expected['fbx']}
    for o in tiers:
        for kind,item in [('Model',o),('Geometry',o.data),('Material',o.data.materials[0])]:
            key=fbx_utils.get_blenderID_key(item)
            number=fbx_utils.UUID(original_ids[(kind,item.name)])
            fbx_utils._keys_to_uuids[key]=number
            fbx_utils._uuids_to_keys[number]=key
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    for o in tiers[1:]:
        o.hide_render=True
        o.hide_set(True)
    bpy.data.orphans_purge(do_recursive=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/(model+'_A1.blend')),compress=True)
    expected=json.loads(baseline.read_text())
    ids=fbx_ids(fbx)
    print('FBX identity',ids,flush=True)
    assert [list(row) for row in ids]==expected['fbx']
    assert expected['mesh_meta_sha256']==hashlib.sha256(Path(str(fbx)+'.meta').read_bytes()).hexdigest()
    assert expected['prefab_sha256']==hashlib.sha256((dest/(model+'.prefab')).read_bytes()).hexdigest()

def refresh_surfaces(model):
    dest=DEST/model
    bpy.ops.wm.open_mainfile(filepath=str(HERE/(model+'_A1.blend')))
    tiers=[bpy.data.objects[f'{model}_Tier{i}'] for i in range(3)]
    for i,o in enumerate(tiers):
        finish_uv(o.data,model,i,MODELS,ZONES)
        for node in o.data.materials[0].node_tree.nodes:
            if node.type!='TEX_IMAGE':continue
            filename=Path(bpy.path.abspath(node.image.filepath)).name
            image=bpy.data.images.load(str(dest/filename),check_existing=False)
            image.colorspace_settings.name=node.image.colorspace_settings.name
            image.alpha_mode=node.image.alpha_mode
            node.image=image
    publish(model,tiers,dest/(model+'.fbx'),HERE/'Review'/(model+'_identity.json'))

if __name__=='__main__':
    (refresh_surfaces if '--surface-only' in sys.argv else main)(sys.argv[-1])

