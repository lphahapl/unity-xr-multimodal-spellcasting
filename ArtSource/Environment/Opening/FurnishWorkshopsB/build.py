"""Tianyi pavilion furnishings. Coordinates in metres, camera-facing side at Blender -Y."""
from math import sin, cos, pi
from pathlib import Path
import bpy, bmesh, json, numpy as np, os
from mathutils import Vector
HERE=Path(__file__).resolve().parent
STAGE=Path(os.environ.get('WORKSHOP_STAGE','D:/Temp/FurnishWorkshopsB_bake'))
STAGE.mkdir(parents=True,exist_ok=True)
NAMES=('WestViseAndInk','WestAngleSamples','EastGaugeCase','AssemblyPartsCase','HangingMandrels','PartsCabinet')
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
lowmat.name='Workshops_Baked'
def drum(name,pos,r,h,mat,high):
    profile(name,[(0,0),(r*.95,0),(r,h*.1),(r,h*.90),(r*.95,h),(0,h)],[mat]*5,44 if high else 14,'up',pos)
def jig(high):
    box('Vise base',(-.43,0,.046),(.54,.257,.092),wood,high,.006,True)
    for x in (-.66,-.30):
        box('Vice jaw '+str(x),(x,0,.131),(.094,.278,.193),ruler,high,.009,True)
        box('Iron jaw liner '+str(x),(x+.035,0,.222),(.029,.274,.028),bronze,high,.003)
    beam('Bench screw',(-.72,0,.124),(-.105,0,.124),.032,.035,bronze,high,.009,True)
    beam('T handle',(-.111,-.087,.08),(-.111,.087,.167),.018,.018,light,high,.003,True)
    for j in range(2):
        z=.023+j*.116
        drum('Reel flange '+str(j),(.46,-.016,z),.114,.025,wood,high)
    drum('Ink string spool',(.46,-.016,.039),.086,.112,ink,high)
    box('Yoke',(.466,.071,.084),(.31,.041,.153),wood,high,.012,True)
    beam('Ink handle',(.55,.070,.11),(.665,.059,.127),.044,.043,ruler,high,.006,True)
    beam('Line from reel',(.458,-.104,.030),(.176,-.114,.003),.006,.006,cord,high,.001)
def sample(high):
    w=1.15
    for y in (.01,):
        box('Pigeonhole backing',(0,.146,.208),(w,.025,.391),wood,high,.005,True)
    for i,z in enumerate((.03175,.404)):box('Shelf '+str(i),(0,-.012,z),(w,.342,.0635 if i==0 else .055),wood,high,.004,True)
    for i in range(4):box('Partition '+str(i),(-.552+i*.368,-.005,.211),(.037,.323,.345),endwood,high,.004,True)
    box('Reference block',(-.371,-.033,.156),(.245,.163,.185),ruler,high,.003,True)
    for x in (0,.365):
        box('Sample short arm '+str(x),(x,-.05,.091),(.247,.116,.06),ruler,high,.003,True)
        box('Sample rise '+str(x),(x-.087,-.047,.175),(.062,.108,.146),ruler,high,.003,True)
    beam('Mitre brace',(-.084,-.063,.244),(.112,-.063,.105),.034,.036,ruler,high,.003,True)
def case_shell(name,x,w,d,high):
    # Open horizontal base with a lined lid hinged at the far edge.
    box(name+' base',(x,0,.025),(w+.008,d+.005,.05),wood,high,.006,True)
    box(name+' liner',(x,0,.054),(w-.042,d-.04,.016),felt,high,.002)
    for y in (-d/2+.013,d/2-.013):box(name+' side '+str(y),(x,y,.075),(w,.025,.092),wood,high,.003,True)
    for side in (-1,1):
        xx=x+side*(w/2-.014)
        box(name+' end '+str(side),(xx,0,.072),(.029,d-.047,.088),endwood,high,.004,True)
        box(name+' clasp '+str(side),(x+side*w*.33,-d/2-.004,.076),(.040,.012,.047),bronze,high,.003)
    back=d/2+.013
    lidheight=d*.86
    box(name+' outside lid',(x,back,.105+lidheight*.5),(w,.041,lidheight),wood,high,.005,True)
    box(name+' textile lid',(x,back-.026,.105+lidheight*.5),(w-.064,.015,lidheight-.046),felt,high,.003)
    for side in (-1,1):box(name+' lid trim '+str(side),(x+side*(w/2-.02),back-.031,.105+lidheight/2),(.035,.027,lidheight),endwood,high,.003)
    for z in (.107,.1+lidheight):box(name+' long trim '+str(z),(x,back-.031,z),(w-.065,.025,.031),wood,high,.003)
def gauge(high):
    case_shell('Etalon chest',0,1.48,.278,high)
    box('Master rule',(0,-.003,.091),(1.28,.095,.05),ruler,high,.004,True)
    for s in (-1,1):box('Endshoe '+str(s),(s*.605,-.002,.095),(.071,.101,.055),light,high,.003)
    if high:
        for j in range(50):
            x=-.56+.0227*j
            o=box('Scribed division',(x,-.032+.005*(j%5!=0),.117),(.0022,.028 if j%5==0 else .014,.001),ink,False)
            o['bake_host']='Master rule'
    for k in (-1,1):box('Handle mount '+str(k),(k*.086,-.146,.078),(.018,.03,.025),bronze,high,.002)
    beam('Front grip',(-.093,-.158,.077),(.093,-.158,.077),.016,.014,bronze,high,.003,True)
def sleeve(name,x,y,z,r,h,high):
    q=[(r*.65,0),(r,0),(r*1.035,.008),(r,h-.008),(r*1.035,h),(r*.64,h),(r*.65,0)]
    ob=profile(name,q,[bronze,light,bronze,light,bronze,bronze],48 if high else 15,'up',(x,y,z))
    bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.remove_doubles(bm,verts=bm.verts[:],dist=.000001);bm.to_mesh(ob.data);bm.free()
def equipment(high):
    case_shell('Fitting chest',0,.94,.47,high)
    box('Tray divider',(-.019,0,.106),(.022,.42,.089),wood,high,.004,True)
    box('Tray crossbar',(.214,.012,.106),(.428,.023,.088),wood,high,.004,True)
    sleeve('Machine hub',-.242,-.034,.063,.133,.06,high)
    for i in range(6):
        a=2*pi*i/6
        drum('Hub rivet '+str(i),(-.242+.11*cos(a),-.034+.11*sin(a),.12),.010,.006,light,high)
    for i in range(3):sleeve('Sleeve '+str(i),.077+i*.113,.102,.065,.039+i*.007,.065+i*.008,high)
    for j in range(3):beam('Taper pin '+str(j),(.086+j*.085,-.145,.067),(.131+j*.085,-.057,.067),.014,.015,light,high,.002,True)
def spools(high):
    box('Mandrel rail',(0,0,0),(.93,.055,.078),endwood,high,.009,True)
    for j in range(3):
        x=(j-1)*.295
        box('Hook block '+str(j),(x,-.046,-.018),(.052,.078,.070),wood,high,.005,True)
        ring('Suspending link '+str(j),x,-.066,-.075,.034,high)
        for i in range(2):drum('Spool flange '+str(j)+' '+str(i),(x,-.068,-.318+i*.175),.083,.022,wood,high)
        drum('Sleeved mandrel '+str(j),(x,-.068,-.304),.051+j*.006,.167,bronze,high)
        beam('Top fixing '+str(j),(x,-.066,-.159),(x,-.066,-.100),.018,.02,bronze,high,.003)
def cabinet(high):
    for x in (-.562,.562):
        for y in (-.162,.162):box(f'Leg {x} {y}',(x,y,.072),(.095,.087,.144),endwood,high,.005,True)
    box('Backing',(0,.018,.433),(1.25,.381,.648),wood,high,.008,True)
    box('Cornice',(0,0,.777),(1.33,.453,.065),wood,high,.008,True)
    for j in range(3):
        for i in range(2):
            x=(i-.5)*.602;z=.232+j*.205
            y=-.187-(.085 if i==1 and j==1 else 0)
            box(f'Drawer {i} {j}',(x,y,z),(.576,.036,.181),wood,high,.005,True)
            if i==1 and j==1:
                box('Open recess base',(x,-.22,z-.053),(.55,.1,.03),felt,high,.003)
                for k in (-1,1):box('Open rim '+str(k),(x+k*.276,-.219,z),(.021,.105,.155),wood,high,.003)
            for k in (-1,1):box(f'Pull cleat {i} {j} {k}',(x+k*.054,y-.027,z),(.019,.025,.036),bronze,high,.002)
            beam(f'Bronze grip {i} {j}',(x-.064,y-.046,z),(x+.064,y-.046,z),.019,.018,light,high,.003,True)
highs,lows={},{}
for high,table in ((True,highs),(False,lows)):
    coll=bpy.data.collections.new('High sources' if high else 'Game meshes');scene.collection.children.link(coll)
    for name,fn in zip(NAMES,(jig,sample,gauge,equipment,spools,cabinet)):
        fn(high)
        obj=join_asset(name+('_HIGH' if high else ''),coll);table[name]=obj
        if not high:
            obj.data.materials.clear();obj.data.materials.append(lowmat)
            for face in obj.data.polygons:face.material_index=0
for ob in highs.values():ob.hide_set(True)
for name,rect in zip(NAMES,[(.005,.005,.31,.49),(.327,.005,.29,.49),(.63,.005,.363,.49),(.005,.51,.327,.486),(.345,.51,.225,.486),(.583,.51,.407,.486)]):
    ob=lows[name];active(ob)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.13,island_margin=.009,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for loop in ob.data.uv_layers.active.data:loop.uv=(rect[0]+rect[2]*loop.uv.x,rect[1]+rect[3]*loop.uv.y)
    tri=ob.modifiers.new('Freeze topology','TRIANGULATE');bake_mod(ob,tri)
PREFIX='Workshops'
BUDGET=12000
exec((COMMON/'bake.py').read_text('utf8'))
