"""Blender 5.2 background generator. Run bake_stone.py first, then blender -b -P build_stone.py."""
import math
import sys
from pathlib import Path
import bpy
import bmesh
from mathutils import Matrix, Vector, geometry
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from layout import CHARTS, SIZE, SOURCE, EXPORT

def atlas(key,u,v):
    x,y,w,h,*_=CHARTS[key]
    return ((x+.5+(w-1)*u)/SIZE, 1-(y+.5+(h-1)*(1-v))/SIZE)

class Masonry:
    def __init__(self,name,lod):
        self.name,self.lod=name,lod
        self.verts,self.faces,self.uv=[],[],[]
        self.indices={}
        self.seed=0
    def face(self,points,key='field',coords=None):
        points=[Vector(v) for v in points]
        if coords is None:
            n=geometry.normal(points)
            u=Vector((0,0,1)).cross(n)
            if u.length<.05: u=Vector((1,0,0))-n*n.x
            u.normalize()
            v=n.cross(u).normalized()
            xs=[p.dot(u) for p in points]; ys=[p.dot(v) for p in points]
            sx,sy=CHARTS[key][-2:]
            dx=max(max(xs)-min(xs),sx); dy=max(max(ys)-min(ys),sy)
            offx=((self.seed*.61803+.13)%1)*(1-(max(xs)-min(xs))/dx)
            offy=((self.seed*.38197+.43)%1)*(1-(max(ys)-min(ys))/dy)
            coords=[(offx+(x-min(xs))/dx,offy+(y-min(ys))/dy) for x,y in zip(xs,ys)]
        idx=[]
        for v in points:
            k=tuple(round(a,7) for a in v)
            if k not in self.indices:
                self.indices[k]=len(self.verts); self.verts.append(k)
            idx.append(self.indices[k])
        self.faces.append(idx)
        self.uv.append([atlas(key,u,v) for u,v in coords])
    def strip(self,a,b,key='field'):
        for i in range(len(a)):
            j=(i+1)%len(a)
            self.face([a[i],a[j],b[j],b[i]],key)
    def panel(self,pts,key):
        a,b,c,d=map(Vector,pts); n=geometry.normal([a,b,c,d])
        def rect(inset,depth):
            x=inset/(b-a).length; z=inset/(d-a).length
            return [a.lerp(b,u).lerp(d.lerp(c,u),v)-n*depth for u,v in ((x,z),(1-x,z),(1-x,1-z),(x,1-z))]
        last=pts
        for inset,depth in ((.018,0),(.024,.006),(.030,.008)) if self.lod==0 else ((.021,0),(.029,.008)):
            r=rect(inset,depth)
            self.strip(last,r,'arris' if depth==.006 else 'field'); last=r
        self.face(last,key,[(0,0),(1,0),(1,1),(0,1)])
    def member(self,x,y,rows,corner=.018,panels=None,top=None):
        """Courses specified bottom to top as (height, width, depth, side treatment)."""
        self.seed+=1; prev=None
        for z,w,d,style in rows:
            a=w/2; b=d/2; c=corner
            ring=[(x+u,y+v,z) for u,v in [(-a+c,-b),(a-c*1.17,-b),(a,-b+c*.83),(a,b-c),(a-c,b),(-a+c*.85,b),(-a,b-c*1.14),(-a,-b+c)]]
            if prev is None: self.face(ring[::-1])
            else:
                for i in range(8):
                    j=(i+1)%8
                    pts=[prev[i],prev[j],ring[j],ring[i]]
                    if style=='panel' and i%2==0:
                        self.panel(pts,'post')
                    elif style in ('socle','fascia') and i in (0,4):
                        self.face(pts,style,[(0,0),(1,0),(1,1),(0,1)])
                    else: self.face(pts,style if style in ('arris','field') else 'field')
            prev=ring
        self.face(prev,top or 'field',[(p[0]/1.25+.5,p[1]/.78+.5) for p in prev] if top else None)
    def mesh(self,mat):
        name=f'{self.name}_LOD{self.lod}'
        me=bpy.data.meshes.new(name)
        me.from_pydata(self.verts,[],self.faces); me.update()
        layer=me.uv_layers.new(name='UV0_Stone')
        for poly,coords in zip(me.polygons,self.uv):
            for i,uv in zip(poly.loop_indices,coords): layer.data[i].uv=uv
        bm=bmesh.new(); bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm,faces=bm.faces[:])
        bmesh.ops.triangulate(bm,faces=bm.faces[:])
        bm.to_mesh(me); bm.free(); me.update()
        ob=bpy.data.objects.new(name,me); bpy.context.scene.collection.objects.link(ob)
        me.materials.append(mat)
        return ob

def profile(m,x,y,rows,indices=None,**kw):
    sel=range(len(rows)) if indices is None else indices[m.lod]
    m.member(x,y,[rows[i] for i in sel],**kw)

def bench(m):
    # Broad side piers, built up as continuous profiles, support a recessed back panel.
    for sign in (-1,1):
        profile(m,sign*.465,0,[
            (0,.295,.699,''),(.014,.307,.714,'arris'),(.116,.307,.714,'field'),(.133,.293,.70,'arris'),
            (.142,.272,.684,'field'),(.161,.272,.684,'field'),(.176,.241,.656,'arris'),
            (.189,.228,.643,'arris'),(.652,.221,.635,'field'),(.668,.247,.653,'arris'),
            (.681,.282,.678,'field'),(.741,.282,.678,'field'),(.754,.297,.69,'arris'),
            (.816,.297,.69,'field')], [range(14),[0,2,3,5,7,8,10,11,13],[0,2,7,8,10,13]],corner=.021)
    profile(m,0,.096,[(.007,.742,.371,''),(.017,.748,.38,'arris'),(.132,.747,.377,'field'),
                       (.147,.739,.358,'arris'),(.16,.734,.348,'field'),(.804,.734,.348,'field')],
            [range(6),[0,2,3,5],[0,2,5]])
    profile(m,0,0,[(.794,1.163,.696,''),(.804,1.181,.713,'arris'),(.818,1.181,.713,'field'),
                    (.825,1.201,.735,'arris'),(.838,1.241,.769,'arris'),(.849,1.25,.78,'arris'),
                    (.944,1.25,.78,'fascia'),(.958,1.242,.772,'arris'),(.97,1.218,.75,'arris')],
            [range(9),[0,2,4,5,6,8],[0,5,6,8]],corner=.032,top='tabletop')
    # Paired cloud-head aprons. Curved underside and bevel are visible in silhouette.
    half=[(0,.642),(.038,.644),(.057,.649),(.073,.662),(.087,.665),
          (.153,.665),(.185,.669),(.205,.68),(.239,.68),(.252,.677),(.27,.664),
          (.28,.642),(.291,.631),(.309,.629),(.32,.625),(.333,.602),
          (.346,.593),(.367,.593),(.376,.588),(.385,.571),(.4,.565),(.505,.565)]
    if m.lod==1: half=half[::2]+[half[-1]]
    if m.lod==2: half=[half[i] for i in (0,4,8,11,14,17,20,21)]
    contour=[(-x,z) for x,z in reversed(half[1:])]+half+[(.505,.809),(-.505,.809)]
    for sign in (-1,1):
        front=sign*.308
        def section(scale,y):
            return [(x*scale,y,.727+(z-.727)*scale) for x,z in contour]
        a=section(.988,front); b=section(1,front-sign*.006); c=section(1,front-sign*.064)
        m.face(a,'apron',[(x/1.01+.5,(z-.565)/.244) for x,y,z in a])
        m.strip(a,b,'arris'); m.strip(b,c); m.face(c[::-1])

def plinth(m):
    rows=[(0,.397,.326,''),(.014,.411,.34,'arris'),(.103,.411,.34,'field'),
          (.116,.398,.328,'arris'),(.126,.381,.311,'field'),(.144,.381,.311,'field'),
          (.156,.355,.291,'arris'),(.17,.342,.278,'arris'),(.734,.342,.278,'panel'),
          (.748,.355,.293,'arris'),(.756,.376,.312,'field'),(.774,.376,.312,'field'),
          (.787,.399,.331,'arris'),(.799,.43,.36,'arris'),(.88,.43,.36,'field'),
          (.895,.422,.352,'arris'),(.90,.406,.339,'arris')]
    profile(m,0,0,rows,[range(17),[0,2,3,5,7,8,10,11,13,14,16],[0,2,7,8,13,14,16]],corner=.014)

def tablet_face(m,contour,y):
    def face(points):
        m.face(points,'stele',[(x/.734+.5,(z-.32)/1.33) for x,_,z in points])
    if m.lod==2: face(contour); return
    # An annular V-cut, two diametrical channels and a chamfered blind square socket.
    cx=.95
    rays=72 if m.lod==0 else 32
    angles=sorted(set(round(2*math.pi*i/rays,8) for i in range(rays)) |
                  set(round((a+b)%(2*math.pi),8) for a in (0,math.pi) for b in (-.052,-.018,.018,.052)))
    def loop(radius,depth=0,square=False,bar=False):
        out=[]
        for a in angles:
            c,s=math.cos(a),math.sin(a)
            r=radius/max(abs(c),abs(s)) if square else radius
            z=r*s
            if bar and abs(s)<.055:
                width=.0095 if abs(s)>.03 else .0034 if abs(s)>.001 else 0
                z=math.copysign(width,s)
            elif bar and square and abs(s)<abs(c):
                t=(abs(s/c)-math.tan(.052))/(1-math.tan(.052))
                z=math.copysign(.0095+t*(radius-.0095),s)
            dy=depth+(.0065 if bar and abs(s)<.025 else 0)
            out.append((r*c,y+dy,cx+z))
        return out
    loops=[loop(.283),loop(.2765,.0075),loop(.2695,.0075),loop(.263,bar=True),
           loop(.0735,square=True,bar=True),loop(.055,.011,square=True)]
    # A square floor only needs its four corner vertices and a boundary fan.
    last=loops[-1]
    for i in range(len(last)): face([last[i],last[(i+1)%len(last)],(0,y+.011,cx)])
    for a,b in zip(loops,loops[1:]):
        for i in range(len(a)):
            j=(i+1)%len(a); face([a[i],a[j],b[j],b[i]])
    outlines=[contour,loops[0]]
    coords=[Vector((p[0],p[2])) for line in outlines for p in line]
    count=len(contour)
    outer=list(range(count)); inner=list(range(count,len(coords)))
    v,e,f,_,__,orig=geometry.delaunay_2d_cdt(coords,[],[outer,inner],0,1e-8)
    for ids,owners in zip(f,orig):
        if 0 in owners and 1 not in owners: face([(v[i].x,y,v[i].y) for i in ids])

def stele(m):
    profile(m,0,0,[(0,.824,.338,''),(.013,.85,.36,'arris'),(.079,.85,.36,'field'),
        (.095,.838,.349,'arris'),(.106,.812,.331,'field'),(.272,.804,.328,'socle'),
        (.279,.817,.34,'arris'),(.292,.817,.34,'field'),(.30,.844,.353,'arris'),
        (.341,.844,.353,'field'),(.354,.828,.337,'arris'),(.364,.797,.314,'arris')],
        [range(12),[0,1,2,4,5,6,8,9,11],[0,2,4,5,8,9,11]],corner=.018)
    half=[(.361,.351),(.367,.367),(.367,1.514),(.361,1.529),(.344,1.538),
          (.324,1.546),(.308,1.561),(.297,1.584),(.287,1.612),(.273,1.633),(.25,1.646),(.223,1.65)]
    if m.lod==1: half=[half[i] for i in (0,1,2,3,5,6,8,10,11)]
    if m.lod==2: half=[half[i] for i in (0,2,5,8,11)]
    contour=half+[(-x,z) for x,z in half[::-1]]
    def rim(s,y):
        return [(x*s,y,1.0+(z-1.0)*s) for x,z in contour]
    rings=[rim(.973,-.126),rim(.986,-.124),rim(1,-.114),rim(1,.111),rim(.98,.124)]
    tablet_face(m,rings[0],-.126)
    for i,(a,b) in enumerate(zip(rings,rings[1:])):
        m.strip(a,b,'arris' if i!=2 else 'field')
    m.face(rings[-1][::-1])

def material():
    mat=bpy.data.materials.new('Stone'); mat.use_nodes=True
    tree=mat.node_tree; bs=next(n for n in tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.82
    for suffix in ('BaseColor','Normal'):
        node=tree.nodes.new('ShaderNodeTexImage')
        node.image=bpy.data.images.load(str(EXPORT/f'Stone_{suffix}.png'))
        if suffix=='BaseColor':
            node.location=(-560,300); tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
        else:
            node.location=(-560,0); node.image.colorspace_settings.name='Non-Color'
            norm=tree.nodes.new('ShaderNodeNormalMap'); norm.location=(-260,0)
            tree.links.new(node.outputs['Color'],norm.inputs['Color'])
            tree.links.new(norm.outputs[0],bs.inputs['Normal'])
    return mat

def run():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    mat=material()
    for name,func,offset in [('Bench',bench,0),('Stele',stele,-1.23),('Plinth',plinth,.94)]:
        collection=bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
        group=[]
        for level in range(3):
            builder=Masonry(name,level); func(builder)
            ob=builder.mesh(mat)
            collection.objects.link(ob); bpy.context.scene.collection.objects.unlink(ob)
            group.append(ob)
        bpy.ops.object.select_all(action='DESELECT')
        for ob in group: ob.select_set(True)
        bpy.context.view_layer.objects.active=group[0]
        # Unity's FBX importer maps Blender -Y to +Z with these export axes.
        turn=Matrix.Rotation(math.pi,4,'Z')
        for ob in group: ob.data.transform(turn)
        bpy.ops.export_scene.fbx(filepath=str(EXPORT/f'{name}.fbx'),use_selection=True,
                object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,
                apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_tspace=True,
                path_mode='STRIP',add_leaf_bones=False,bake_anim=False)
        for i,ob in enumerate(group):
            ob.data.transform(turn.inverted())
            ob.location.x=offset
            ob.hide_viewport=i>0; ob.hide_render=i>0
    for tex in bpy.data.images:
        if tex.type=='IMAGE' and tex.filepath: tex.pack()
    for sc in bpy.data.screens:
        for area in sc.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active; space.shading.color_type='TEXTURE'
                space.region_3d.view_location=(-.13,0,.78); space.region_3d.view_distance=4.2
                space.region_3d.view_rotation=Vector((-.26,.94,-.27)).to_track_quat('-Z','Y')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'A1_Stone.blend'),compress=True)

if __name__=='__main__': run()

