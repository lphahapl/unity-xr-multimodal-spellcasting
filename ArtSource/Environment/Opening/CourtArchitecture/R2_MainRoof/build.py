"""B2 main hall. Build from the three finished A1 game meshes, in Blender background mode."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import math
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector
from io_scene_fbx import fbx_utils

sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
A1=HERE.parent/'A1'
sys.path.insert(0,str(A1))
import roof
from surface_uv import tile_window, END_RECTS

MODEL='CourtMainRoof'
DEST=roof.DEST/MODEL
CAP=DEST.parent.parent/'PreqinRidgeCap'
X,Y=9.55,8.20
EAVE,SILL,CROWN=.77,2.052,5.38
GABLE=5.0                  # Half-width of gable at the skirt
WALL,VERGE=7.10,7.48       # Face and outer verge stations
REAR=VERGE-.345            # Pivot at the rear opening of the approved Hunyuan cap
SEAT=CROWN-.105
BUDGET=(11600,4208,920)

def broad(y):
    q=Y-abs(y)
    if q<=Y-GABLE:
        t=q/(Y-GABLE)
        return EAVE+(SILL-EAVE)*(.94*t+.06*t*t)
    t=(q-(Y-GABLE))/GABLE
    return SILL+(CROWN-SILL)*(.98*t+.02*t*t)

def end_height(x):
    t=(X-abs(x))/(X-WALL)
    return EAVE+(SILL-EAVE)*(.94*t+.06*t*t)

def envelope(x,y):
    if abs(x)<=WALL or (abs(x)<=VERGE and abs(y)<=GABLE):
        return broad(y)
    return min(broad(y),end_height(x))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

class R2Builder(roof.Builder):
    def face(self,points,zone,coords,smooth=False):
        if zone=='end':
            # Generic end grain in the finished atlas; the three neighbouring panels
            # belong solely to A1 exposed circular rafter tips.
            zone='generic_end'
        super().face(points,zone,coords,smooth)

roof.ZONES['generic_end']=END_RECTS[0]

def keep_a1(ob):
    """Retain original exposed ends, fascia, purlins and trusses with their final Surface UV."""
    bm=bmesh.new()
    bm.from_mesh(ob.data)
    uv=bm.loops.layers.uv.get('Surface')
    unseen=set(bm.verts)
    remove=[]
    retained=discarded=0
    while unseen:
        seed=unseen.pop()
        stack=[seed]
        verts=[seed]
        while stack:
            v=stack.pop()
            for edge in v.link_edges:
                nxt=edge.other_vert(v)
                if nxt in unseen:
                    unseen.remove(nxt);stack.append(nxt);verts.append(nxt)
        faces={f for v in verts for f in v.link_faces}
        regions=set()
        for f in faces:
            u=sum(loop[uv].uv.x for loop in f.loops)/len(f.loops)*2048
            v=2048-sum(loop[uv].uv.y for loop in f.loops)/len(f.loops)*2048
            if u<1280 and v<1280:regions.add('tile field')
            elif u>=1280 and v<480:regions.add('clay')
            else:regions.add('trim')
        if 'tile field' in regions or regions=={'clay'}:
            remove.extend(verts);discarded+=len(faces)
            continue
        retained+=len(faces)
        extent=np.array([v.co for v in verts])
        # The A1 principal rafters span this new break in pitch. Their straight
        # shafts stay below the tile skin, with the low foot on the transverse tie.
        recessed_rafter=(4.9<abs(extent[:,0].mean())<5.35 and
                         np.ptp(extent[:,0])<.4 and np.ptp(extent[:,1])>5 and np.ptp(extent[:,2])>2)
        skirt_purlin=(9.0<abs(extent[:,0].mean())<9.3 and np.ptp(extent[:,1])>15)
        for v in verts:
            x,y,z=v.co
            t=min(1-abs(y)/Y,(X-abs(x))/(X-5.30))
            old=EAVE+(4.22-EAVE)*(.92*t+.08*t*t)
            weight=max(0,min(1,(z-.39)/max(.20,old-.61)))
            v.co.z+=weight*(envelope(x,y)-old)
            if recessed_rafter:v.co.z-=.125+.47*abs(y)/6.86
            if skirt_purlin:v.co.z-=.164*(abs(y)/Y)**2
    bmesh.ops.delete(bm,geom=remove,context='VERTS')
    bm.to_mesh(ob.data);bm.free();ob.data.update()
    return retained,discarded

def patch(b,kind,sign):
    """Closed ceramic/deck strip; top UVs keep the kiln field in architectural metres."""
    is_end=kind=='end'
    upper=kind=='upper'
    extent=Y if is_end else X
    pitch=2*extent/round(2*extent/.325)
    band=16 if b.tier==2 else 8
    depth=X-WALL if is_end else Y
    side=(2 if is_end else 0)+int(sign>0)
    qlo,qhi=(0,X-WALL+.11) if is_end else ((Y-GABLE-.012,Y-.024) if upper else (0,Y-GABLE+.010))
    span=VERGE if upper else extent
    def xy(s,q):
        return (sign*(X-q),s) if is_end else (s,sign*(Y-q))
    def h(q):
        return end_height(X-q) if is_end else broad(Y-q)
    def maxq(s):
        if upper:return qhi
        if is_end:return max(0,min(qhi,(Y-abs(s))*(X-WALL)/(Y-GABLE)))
        return max(0,min(qhi,(X-abs(s))*(Y-GABLE)/(X-WALL)))
    def profile(s):
        r=(((s+extent)/pitch)%1-.5)/.235
        return .061*math.sqrt(max(0,1-r*r)) if b.tier==0 else .023
    def top(s,q):
        return (*xy(s,q),h(q)+profile(s)+(.014 if upper else 0))
    def bot(s,q):
        # Linear deck underside lies below the curve at every point.
        return (*xy(s,q),h(qlo)+(h(qhi)-h(qlo))*(q-qlo)/(qhi-qlo)-(.081 if upper else .077))
    cols={-span,span}
    if b.tier==0:
        for i in range(round(2*extent/pitch)):
            for phase in (.275,.50,.725):
                s=-extent+(i+phase)*pitch
                if abs(s)<span:cols.add(s)
    for i in range(0,round(2*extent/pitch),band):
        s=-extent+i*pitch
        if abs(s)<span:cols.add(s)
    qs=sorted({qlo,qhi,*[k*8*.396 for k in range(1,4) if qlo<k*8*.396<qhi]})
    if b.tier>0:qs=[qlo,qhi]
    if not upper:
        for q in qs:
            s=(Y-q*(Y-GABLE)/(X-WALL)) if is_end else (X-q*(X-WALL)/(Y-GABLE))
            if 0<s<span:cols.update((-s,s))
    cols=sorted(set(round(s,5) for s in cols))
    boundary=Counter()
    b.part()
    for left,right in zip(cols,cols[1:]):
        block=math.floor(((left+right)*.5+extent)/(band*pitch))
        col,row=tile_window(0,side,block,depth)
        col%=25-band
        for q0,q1 in zip(qs,qs[1:]):
            ml,mr=maxq(left),maxq(right)
            if max(ml,mr)<=q0+1e-5:continue
            pts=[(left,min(q0,ml)),(right,min(q0,mr)),(right,min(q1,mr)),(left,min(q1,ml))]
            pts=list(dict.fromkeys((round(s,5),round(q,5)) for s,q in pts))
            if len(pts)<3:continue
            b.face([top(s,q) for s,q in pts],'roof',
                   [((col+(s+extent)/pitch-band*block)/24,(row+q/.396)/24) for s,q in pts],True)
            for a,c in zip(pts,pts[1:]+pts[:1]):boundary[tuple(sorted((a,c)))]+=1
    perimeter=[e for e,n in boundary.items() if n==1]
    centre=(0,(qlo+qhi)*.51)
    def deck_uv(s,q):
        return (.5+s/(2*extent*1.02),.025+.95*q/(X-WALL+.12 if is_end else Y))
    for a,c in perimeter:
        b.face([top(*a),top(*c),bot(*c),bot(*a)],'clay',[(.08,.75),(.26,.75),(.26,.94),(.08,.94)])
        b.face([bot(*a),bot(*c),bot(*centre)],'deck',[deck_uv(*a),deck_uv(*c),deck_uv(*centre)])

def gable(b,sign):
    # The closed board lining folds under the skirt and upper deck. The grille stands
    # proud of it, allowing a real shadow between the laths at both ends of the hall.
    width=GABLE+.085
    foot=SILL-.095
    crest=CROWN-.052
    b.part()
    outline=[(-width,foot),(width,foot),(0,crest)]
    front=[(sign*(WALL-.038),s,z) for s,z in outline]
    back=[(sign*(WALL-.158),s,z) for s,z in outline]
    panel_uv=[(.02,.04),(.98,.04),(.5,.97)]
    b.face(front,'deck',panel_uv);b.face(back,'deck',panel_uv)
    for i in range(3):
        j=(i+1)%3
        b.face([front[i],front[j],back[j],back[i]],'timber',[(.02,.17),(.98,.17),(.98,.24),(.02,.24)])
    # Scarfed sill, two external pitched boards, central post, intermediate transom.
    def beam(x,s0,z0,s1,z1,w,h):
        b.timber((sign*x,s0,z0),(sign*x,s1,z1),w,h)
    beam(WALL+.046,-GABLE+.39,SILL+.066,GABLE-.39,SILL+.066,.258,.24)
    for sy in (-1,1):
        beam(VERGE-.119,sy*(GABLE-.080),SILL-.103,sy*.014,CROWN-.242,.184,.244)
        beam(WALL+.048,sy*(GABLE-.055),SILL-.124,sy*.035,CROWN-.262,.245,.23)
    beam(WALL+.074,0,SILL+.128,0,CROWN-.14,.208,.22)
    mid=SILL+.95
    half=GABLE*(1-(mid-SILL)/(CROWN-SILL))-.075
    beam(WALL+.115,-half,mid,half,mid,.223,.194)
    # Narrow battens run with the grain and disappear behind the horizontal transom.
    spacing=(.255,.345,.76)[b.tier]
    count=math.floor((GABLE-.30)/spacing)
    for i in range(-count,count+1):
        if not i:continue
        s=i*spacing
        top=broad(s)-.145
        if top<=SILL+.16:continue
        beam(WALL-.002,s,SILL+.122,s,top,.085 if b.tier<2 else .115,.091 if b.tier<2 else .145)
    # Connect the verge to the recessed gable wall with visible purlin ends.
    if b.tier<2:
        for s in (-3.2,-1.6,0,1.6,3.2):
            z=broad(s)-.18
            b.timber((sign*(WALL-.28),s,z),(sign*(VERGE+.031),s,z),.19,.20)

def caps(b,mounts):
    def run(a,c,width,height):
        a,c=Vector(a),Vector(c)
        n=max(1,math.ceil((c-a).length/(.82 if b.tier==0 else 1.52))) if b.tier<2 else 1
        for i in range(n):
            b.timber(a.lerp(c,i/n),a.lerp(c,(i+1)/n)+.009*(c-a).normalized(),width,height,True,'clay',True)
    run((-REAR-.038,0,CROWN+.032),(REAR+.038,0,CROWN+.032),.374,.247)
    for sx in (-1,1):
        # Bedding shoulders carry both feet of the socket; the raised pan below
        # beds into the middle of the hollow, so the terminal can never show daylight.
        mounts.timber((sx*(REAR-.071),0,SEAT-.070),(sx*(REAR+.363),0,SEAT-.070),.521,.162,False,'clay')
        mounts.timber((sx*(REAR-.011),0,SEAT+.037),(sx*(REAR+.347),0,SEAT+.037),.212,.106,False,'clay')
        for sy in (-1,1):
            run((sx*X,sy*Y,EAVE+.039),(sx*(WALL-.041),sy*(GABLE-.052),SILL+.059),.30,.213)
            run((sx*(VERGE-.076),sy*(GABLE+.042),SILL+.025),
                (sx*(VERGE-.076),sy*.031,CROWN+.015),.219,.158)

def repair_tile_projection(mesh):
    # QEM can move a tile vertex without preserving the original trim-sheet derivative.
    # Reconstruct full-size pan/roll coordinates in metres on each surviving triangle.
    bm=bmesh.new()
    bm.from_mesh(mesh)
    layer=bm.loops.layers.uv['Surface']
    unseen=set(bm.faces)
    while unseen:
        seed=unseen.pop()
        faces=[seed]
        for f in faces:
            for edge in f.edges:
                for nbr in edge.link_faces:
                    if nbr in unseen:unseen.remove(nbr);faces.append(nbr)
        tops=[]
        for f in faces:
            u,v=[sum(l[layer].uv[k] for l in f.loops)*2048/3 for k in (0,1)]
            if u<1280 and 2048-v<1280:tops.append(f)
        if not tops:continue
        p=np.array([v.co for f in faces for v in f.verts])
        end=np.ptp(p[:,1])>np.ptp(p[:,0])
        across=1 if end else 0
        extent=Y if end else X
        span=X if end else Y
        side=(2 if end else 0)+int(p[:,1-across].mean()>0)
        pitch=2*extent/round(2*extent/.325)
        for f in tops:
            ss=[(l.vert.co[across]+extent)/pitch for l in f.loops]
            block=math.floor(sum(ss)/(3*8))
            col,row=tile_window(0,side,block,X-WALL if end else Y)
            low=math.ceil(-min(ss)-.0001)
            high=math.floor(24-max(ss)+.0001)
            assert low<=high
            shift=min(high,max(low,col-8*block))
            for l,s in zip(f.loops,ss):
                q=span-abs(l.vert.co[1-across])
                l[layer].uv=roof.atlas('roof',(s+shift)/24,(row+q/.396)/24)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

def new_mesh(tier,limit):
    b=R2Builder(tier)
    pans=R2Builder(tier) if tier else b
    for sign in (-1,1):
        for kind in ('lower','upper','end'):patch(pans,kind,sign)
        gable(b,sign)
    caps(b,pans)
    protected=None
    if tier:
        protected=bpy.data.objects.new('Protected UV windows',pans.mesh('Tiled pitches'))
        bpy.context.collection.objects.link(protected)
        limit-=len(protected.data.polygons)
    me=b.mesh('B2 new surfaces')
    ob=bpy.data.objects.new('B2 tooling',me)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active=ob
    if len(me.polygons)>limit:
        dec=ob.modifiers.new('R2 triangle budget','DECIMATE')
        dec.ratio=limit/len(me.polygons);dec.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=dec.name)
    if protected:
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True);protected.select_set(True)
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.join()
    else:
        repair_tile_projection(ob.data)
    return ob

def build():
    bpy.ops.wm.open_mainfile(filepath=str(A1/(MODEL+'_A1.blend')))
    bpy.context.preferences.filepaths.save_version=0
    tiers=[bpy.data.objects[f'{MODEL}_Tier{i}'] for i in range(3)]
    stats=[]
    for i,ob in enumerate(tiers):
        ob.hide_set(False);ob.hide_render=False
        a1_extents=np.max(np.abs([v.co for v in ob.data.vertices]),axis=0)
        retained,dropped=keep_a1(ob)
        added=new_mesh(i,BUDGET[i]-retained)
        # Match the registered A1 roof's outer trim envelope without stretching
        # the pan pitch, timber grid or gable. Only the four hip noses extend past it.
        new_extents=np.max(np.abs([v.co for v in added.data.vertices]),axis=0)
        for v in added.data.vertices:
            for axis,edge in [(0,X),(1,Y)]:
                coord=v.co[axis]
                if abs(coord)>edge:
                    factor=(a1_extents[axis]-edge)/(new_extents[axis]-edge)
                    v.co[axis]=math.copysign(edge+(abs(coord)-edge)*factor,coord)
        mat=ob.data.materials[0]
        added.data.materials.append(mat)
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True);added.select_set(True)
        bpy.context.view_layer.objects.active=ob
        bpy.ops.object.join()
        mesh=ob.data
        mesh.uv_layers.active_index=1
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.004,scale_to_bounds=True)
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh.uv_layers.active_index=0;mesh.uv_layers[0].active_render=True
        stats.append(dict(tier=i,a1_trim_triangles=retained,replaced_a1=dropped,
                          new_mesh_triangles=len(mesh.polygons)-retained,total=len(mesh.polygons)))
    bpy.ops.object.select_all(action='DESELECT')
    ids={(kind,name):n for kind,name,n in json.loads((A1/'Review'/f'{MODEL}_identity.json').read_text())['fbx']}
    for ob in tiers:
        ob.select_set(True)
        for kind,item in [('Model',ob),('Geometry',ob.data),('Material',ob.data.materials[0])]:
            key=fbx_utils.get_blenderID_key(item)
            number=fbx_utils.UUID(ids[(kind,item.name)])
            fbx_utils._keys_to_uuids[key]=number;fbx_utils._uuids_to_keys[number]=key
    bpy.context.view_layer.objects.active=tiers[0]
    fbx=HERE/(MODEL+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',
                             apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_anim=False,
                             add_leaf_bones=False,path_mode='STRIP')
    assert {(k,n):v for k,n,v in roof.fbx_ids(fbx)}==ids
    for o in tiers[1:]:o.hide_set(True);o.hide_render=True
    bpy.data.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'CourtMainRoof_B2.blend'),compress=True)
    (HERE/'build_result.json').write_text(json.dumps(stats,indent=2))
    print('B2 mesh budgets',stats,flush=True)

if __name__=='__main__':build()

