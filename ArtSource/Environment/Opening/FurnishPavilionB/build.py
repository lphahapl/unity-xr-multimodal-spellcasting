"""Tianyi pavilion furnishings. Coordinates in metres, camera-facing side at Blender -Y."""
from math import sin, cos, pi
from pathlib import Path
import bpy, bmesh, json, numpy as np, os
from mathutils import Vector
HERE=Path(__file__).resolve().parent
STAGE=Path(os.environ.get('PAVILION_STAGE','D:/Temp/FurnishPavilionB_bake'))
STAGE.mkdir(parents=True,exist_ok=True)
NAMES=('ArcCabinet','BenchDressing','HangingDivider')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=4
scene.render.threads_mode='FIXED'
scene.render.threads=8
shades,group_ids,parts={},{},[]
exec((HERE/'modelling.py').read_text('utf8'))
linen=material('Raw bleached ramie',(.22,.17,.104),(.56,.48,.344),0,.18,(260,200,50))
ceramic=material('Sooted ochre glaze',(.10,.041,.016),(.38,.19,.063),.09,.63,(13,13,6))
flame=material('Amber wick',(.91,.32,.044),(1.,.87,.31),0,.29,glow=1)
exec((HERE/'bronze.py').read_text('utf8'))
def cabinet(high):
    for x in (-1.08,1.08):
        for y in (-.19,.19): box(f'Foot {x} {y}',(x,y,.073),(.115,.11,.146),endwood,high,.006,True)
    box('Lower moulding',(0,0,.16),(2.34,.494,.08),wood,high,.01,True)
    box('Carcase',(0,.029,.476),(2.30,.425,.558),wood,high,.006,True)
    box('Chamfered top',(0,0,.79),(2.44,.528,.081),wood,high,.008,True)
    for k in range(5):
        x=(k-2)*.446
        for j in range(3):
            z=.302+j*.168
            pulled=(k==3 and j==1)
            y=-.195-(.126 if pulled else 0)
            box(f'Drawer {k} {j}',(x,y,z),(.428,.043,.148),wood,high,.005,True)
            if pulled:
                box('Visible drawer bottom',(x,-.25,z-.045),(.387,.133,.023),endwood,high,.002)
                box('Drawer liner',(x,-.25,z-.027),(.368,.127,.013),felt,high,.003)
                for s in (-1,1):box('Drawer cheek '+str(s),(x+s*.198,-.248,z),(.018,.142,.132),endwood,high,.002)
            # Bolt holds the hanging ring.
            profile(f'Rosette {k} {j}',[(0,0),(.018,0),(.020,.005),(.014,.011),(0,.012)],[bronze,light,bronze,light],28 if high else 8,'dial',(x,y-.031,z+.012))
            ring(f'Handle {k} {j}',x,y-.034,z-.005,.022,high)
    for axis,r in ((-.061,.415),(.068,.506)):
        arc('Bronze arc '+str(r),.49,axis,.813,r,.043,.019,high)
    box('Arc cradle plinth',(.49,.016,.854),(1.13,.26,.045),endwood,high,.004,True)
    for x in (-.003,.982):
        box('End saddle '+str(x),(x,.02,.921),(.080,.251,.111),wood,high,.005,True)
def desk(high):
    # Closed ribbon with a wavy selvedge; front edge drapes over the long workbench.
    n=56 if high else 18
    rows=[(-.38,-.116),(-.364,-.038),(-.35,.002),(-.31,.009),(-.09,.008),(.15,.009),(.295,.008)]
    v=[];f=[]
    for layer in range(2):
        for y,z in rows:
            for i in range(n+1):
                x=-.66+1.32*i/n
                wave=(.003+.004*(y<-.35))*sin(13*x+3*y)+.002*cos(24*x-2*y)
                v.append((x,y+(.006*sin(16*x) if y<-.35 else 0),z+wave-.004*layer))
    stride=n+1;sheet=len(rows)*stride
    for layer in range(2):
        for r in range(len(rows)-1):
            for j in range(n):
                a=layer*sheet+r*stride+j
                f.append((a,a+1,a+stride+1,a+stride))
    border=list(range(stride))+[r*stride+n for r in range(1,len(rows))]+list(range(sheet-2,sheet-stride-1,-1))+[r*stride for r in range(len(rows)-2,0,-1)]
    for i,a in enumerate(border):
        b=border[(i+1)%len(border)];f.append((a,b,b+sheet,a+sheet))
    solid('Linen runner',v,f,linen)
    x=-.31;y=.107
    box('Weight case',(x,y,.068),(.278,.196,.107),bronze,high,.009,True)
    box('Weight case lid',(x,y,.128),(.290,.203,.026),light,high,.006,True)
    for j in (-1,1):box('Case hinge '+str(j),(x+j*.082,y+.099,.127),(.044,.018,.026),bronze,high,.003)
    box('Clasp',(x,y-.106,.113),(.034,.011,.052),bronze,high,.002,True)
    base=(.295,.085,.012)
    shape=[(0,0),(.065,0),(.076,.011),(.067,.028),(.093,.040),(.114,.068),(.111,.097),(.096,.112),(.081,.126),(.073,.142),(.084,.150),(.077,.157),(.061,.148),(.048,.137),(0,.134)]
    profile('Ceramic oil lamp',shape,[ceramic]*(len(shape)-1),64 if high else 26,'up',base)
    profile('Burning wick',[(0,0),(.011,0),(.012,.018),(.008,.035),(.003,.056),(0,.065)], [ink,flame,flame,flame,flame],26 if high else 9,'up',(.295,.085,.148))
def divider(high):
    for s in (-1,1):
        beam('Compass arm '+str(s),(s*.012,0,-.045),(s*.079,0,-.342),.019,.013,bronze,high,.0025,True)
        beam('Compass point '+str(s),(s*.079,0,-.340),(s*.101,0,-.412),.009,.008,light,high,.002)
    profile('Pivot',[(0,-.016),(.029,-.015),(.031,-.005),(.026,.008),(0,.009)],[light,bronze,light,bronze],50 if high else 14,'dial',(0,0,-.042))
    ring('Suspension eye',0,0,-.012,.019,high)
highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    coll=bpy.data.collections.new('High sources' if high else 'Game meshes');scene.collection.children.link(coll)
    for name,fn in zip(NAMES,(cabinet,desk,divider)):
        fn(high)
        obj=join_asset(name+('_HIGH' if high else ''),coll);table[name]=obj
        if not high:
            obj.data.materials.clear();obj.data.materials.append(lowmat)
            for face in obj.data.polygons:face.material_index=0
for ob in highs.values():ob.hide_set(True)
for name,rect in zip(NAMES,[(.005,.006,.984,.70),(.007,.714,.805,.278),(.824,.715,.169,.277)]):
    ob=lows[name];active(ob)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.13,island_margin=.009,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for loop in ob.data.uv_layers.active.data:loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=ob.modifiers.new('Freeze topology','TRIANGULATE');bake_mod(ob,tri)
PREFIX='Pavilion'
BUDGET=8000
exec((HERE/'bake.py').read_text('utf8'))
