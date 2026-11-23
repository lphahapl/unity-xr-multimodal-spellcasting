"""Tianyi pavilion furnishings. Coordinates in metres, camera-facing side at Blender -Y."""
from math import sin, cos, pi
from pathlib import Path
import bpy, bmesh, json, numpy as np, os
from mathutils import Vector
HERE=Path(__file__).resolve().parent
STAGE=Path(os.environ.get('WALL_GAUGE_STAGE','D:/Temp/FurnishWallGauges_bake'))
STAGE.mkdir(parents=True,exist_ok=True)
NAMES=('WestWallSquares','EastFiveStraightedges','AssemblySaddle')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=4
scene.render.threads_mode='FIXED'
scene.render.threads=8
shades,group_ids,parts={},{},[]
COMMON=HERE.parent/'FurnishPavilionB'
exec((COMMON/'modelling.py').read_text('utf8'))
linen=material('Raw bleached ramie',(.22,.17,.104),(.56,.48,.344),0,.18,(260,200,50))
ceramic=material('Sooted ochre glaze',(.10,.041,.016),(.38,.19,.063),.09,.63,(13,13,6))
flame=material('Amber wick',(.91,.32,.044),(1.,.87,.31),0,.29,glow=1)
exec((COMMON/'bronze.py').read_text('utf8'))
lowmat.name='WallGauge_Baked'
def square_set(high):
    box('Oak rail',(0,-.014,.261),(1.092,.049,.061),wood,high,.005,True)
    for j,(x,h,w) in enumerate([(-.452,.54,.29),(-.079,.393,.234),(.262,.268,.176)]):
        size=.049 if j==0 else .041 if j==1 else .034
        top=.232
        drop=top-h
        depth=-.077-j*.003
        box('Eye cleat '+str(j),(x,-.049,.226),(.025,.047,.079),bronze,high,.004,True)
        box('Square vertical '+str(j),(x,depth,(top+drop)*.5),(size,.023,h),endwood if j==1 else ruler,high,.003,True)
        # Each limb is a separate mortised piece with a brass corner plate.
        box('Square blade '+str(j),(x+w*.5,depth-.0008,drop+size*.53),(w,.025,size),ruler,high,.003,True)
        box('Elbow copper '+str(j),(x+.006,depth-.016,drop+size*.6),(size*.84,.007,size*.82),light,high,.002,True)
        if high:
            for k in range(2,16):
                xx=x+w*k/17
                mark=box('Division',(xx,depth-.0138,drop+size*.68),(.0016,.001, .008 if k%3 else .015),ink,False)
                mark['bake_host']='Square blade '+str(j)
def five(high):
    # Two recessed wall bearers support every length without adding false scale bars.
    for x in (-.23,.23):
        box('Inner hanging post '+str(x),(x,-.065,0),(.059,.11,.94),wood,high,.003,True)
    for j,w in enumerate((1.98,1.64,1.30,.973,.645)):
        z=-.388+.191*j
        for sign in (-1,1):
            x=sign*.23
            box(f'Rule saddle {j} {sign}',(x,-.092,z-.013),(.041,.130,.041),bronze,high,.003)
            box(f'Retention pin {j} {sign}',(x,-.141,z+.02),(.026,.028,.055),light,high,.002)
        # Graduated lengths are the primary silhouette feature.
        primary=ruler if j in (0,3,4) else endwood if j==1 else wood
        box('Removable straightedge '+str(j),(0,-.111,z+.039),(w,.031,.066),primary,high,.0035,True)
        for sign in (-1,1):
            box(f'End fitting {j} {sign}',(sign*(w*.5-.011),-.129,z+.040),(.019,.007,.049),bronze,high,.002)
        if high:
            for k in range(1,round(w/.029)):
                x=-w*.5+k*.029
                mark=box('Incised division',(x,-.1272,z+.017),(.0021,.001,.015 if k%5==0 else .009),ink,False)
                mark['bake_host']='Removable straightedge '+str(j)
def saddle(high):
    for x in (-.326,.326):
        for y in (-.153,.153):
            beam(f'Trestle post {x} {y}',(x*1.07,y*1.08,.034),(x,y,.56),.063,.065,endwood,high,.005,True)
            box(f'Foot {x} {y}',(x*1.07,y*1.08,.05),(.10,.106,.1),wood,high,.006,True)
    for y in (-.151,.151):
        box('Foot rail '+str(y),(0,y,.165),(.692,.047,.065),wood,high,.004,True)
    for x in (-.326,.326):
        box('Upper crosspiece '+str(x),(x,0,.533),(.080,.365,.065),wood,high,.004,True)
    n=44 if high else 16
    # Curved saddle thickens near the horns. A continuous closed strip spans both bearers.
    for label,base,depth,thick,material in [('Trestle arc',.418,.279,.058,wood),('Oil leather liner',.469,.288,.016,cord)]:
        v=[];f=[]
        for i in range(n+1):
            t=-1+2*i/n
            x=t*(.355 if label=='Oil leather liner' else .358)
            z=base+.151*(abs(t)**2.2)
            for yy,hh in ((-depth/2,0),(depth/2,0),(depth/2,thick),(-depth/2,thick)):
                v.append((x,yy,z+hh))
        for i in range(n):
            for a in range(4):f.append((4*i+a,4*i+(a+1)%4,4*(i+1)+(a+1)%4,4*(i+1)+a))
        f.extend(((0,1,2,3),(n*4,n*4+1,n*4+2,n*4+3)))
        solid(label,v,f,material)
    for j in range(7):
        x=-.302+j*.1; z=.435+.15*(abs(x/.358)**2.2)
        profile('Brass rivet '+str(j),[(0,-.004),(.008,-.004),(.009,.001),(0,.006)],[light,bronze,light],18 if high else 7,'dial',(x,-.14,z))
highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    coll=bpy.data.collections.new('High sources' if high else 'Game meshes');scene.collection.children.link(coll)
    for name,fn in zip(NAMES,(square_set,five,saddle)):
        fn(high)
        obj=join_asset(name+('_HIGH' if high else ''),coll);table[name]=obj
        if not high:
            obj.data.materials.clear();obj.data.materials.append(lowmat)
            for face in obj.data.polygons:face.material_index=0
for ob in highs.values():ob.hide_set(True)
for name,rect in zip(NAMES,[(.006,.006,.428,.476),(.449,.005,.544,.990),(.006,.495,.428,.5)]):
    ob=lows[name];active(ob)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.13,island_margin=.009,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for loop in ob.data.uv_layers.active.data:loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=ob.modifiers.new('Freeze topology','TRIANGULATE');bake_mod(ob,tri)
PREFIX='WallGauge'
BUDGET=12000
exec((COMMON/'bake.py').read_text('utf8'))
