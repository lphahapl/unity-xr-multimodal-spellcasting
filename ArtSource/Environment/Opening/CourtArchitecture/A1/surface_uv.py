"""Trim-sheet layout and UV-only finish for the registered A1 roof meshes."""
import math

# Horizontal courses sample a continuous 24 x 24 field of individually fired tiles.
# Each eight-column strip can use any whole-tile window without shifting the mesh's rolls.
TILES=24
END_RECTS=((1280,768,1472,1104),(1472,768,1664,1104),
           (1664,768,1856,1104),(1856,768,2048,1104))

def atlas_uv(rect,u,v):
    x,y,r,b=rect
    return ((x+8+u*(r-x-16))/2048,1-(b-8-v*(b-y-16))/2048)

def local_uv(rect,uv):
    x,y,r,b=rect
    return ((uv.x*2048-x-8)/(r-x-16),(b-8-(1-uv.y)*2048)/(b-y-16))

def slope_side(c,x,y,ridge):
    # The hipped end pitch competes with the broad pitch at their shared diagonal.
    is_end=ridge<x and (x-abs(c.x))/(x-ridge)<1-abs(c.y)/y
    if is_end:
        return 2+int(c.x>0),c.y,x-abs(c.x),y,x-ridge
    return int(c.y>0),c.x,y-abs(c.y),x,y

def rafter_index(c,n,x,y,eave,ridge,tier):
    if tier==2 or abs(c.z-(eave-.112))>.11:
        return None
    front=abs(abs(c.y)-y)<.075 and n.y*c.y>y*.76
    side=ridge<x and abs(abs(c.x)-x)<.075 and n.x*c.x>x*.76
    if not (front or side):
        return None
    span,pos,orientation=(x,c.x,int(c.y>0)) if front else (y,c.y,2+int(c.x>0))
    count=round(2*span/(.59 if tier==0 else .83))
    return min(count-1,max(0,math.floor((pos+span)*count/(2*span)))),orientation

def tile_window(asset_code,side,block,depth):
    # Integer mixing gives different windows on facing pitches and consecutive bays.
    salt=(asset_code+1)*2654435761+(side+5)*2246822519+(block+3)*3266489917
    salt=(salt^(salt>>16))*2246822519
    salt^=salt>>13
    return salt%17,(salt//17)%(1+TILES-math.ceil(depth/.396))

def turn(u,v,angle):
    dx,dy=u-.5,v-.5
    return (.5+dx*math.cos(angle)-dy*math.sin(angle),
            .5+dx*math.sin(angle)+dy*math.cos(angle))

def round_end(coords):
    # A neighbouring rectangular bargeboard can terminate at the same elevation.
    return all(math.hypot(u-.5,v-.5)<.466 for u,v in coords)

def preserve_small_charts(mesh):
    # One clipped H02 bracket triangle has sub-texel UVs. Keep its tangent frame
    # numerically valid after narrowing its generic end-grain panel.
    layer=mesh.uv_layers['Surface']
    for poly in mesh.polygons:
        a,b,c=[layer.data[i].uv.copy() for i in poly.loop_indices]
        if not (1288/2048<a.x<1464/2048 and 1-1096/2048<a.y<1-776/2048):
            continue
        twice_area=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))
        if 0<twice_area<2e-12:
            centre=(a+b+c)/3
            scale=math.sqrt(4e-12/twice_area)
            for loop,uv in zip(poly.loop_indices,(a,b,c)):
                layer.data[loop].uv=centre+(uv-centre)*scale

def finish(mesh,model,tier,models,zones):
    x,y,eave,_,ridge=models[model]
    asset_code=('CourtMainRoof','CourtLowWing','CourtGatehouse').index(model)
    layer=mesh.uv_layers['Surface']
    revision=mesh.get('court_surface_revision')
    if revision==3:return
    if revision==2:
        for poly in mesh.polygons:
            loops=[layer.data[i] for i in poly.loop_indices]
            xp=sum(l.uv.x for l in loops)*2048/3
            yp=(1-sum(l.uv.y for l in loops)/3)*2048
            if xp<1472 or not 768<=yp<1104:continue
            idx=rafter_index(poly.center,poly.normal,x,y,eave,ridge,tier)
            if idx is None:continue
            i,orientation=idx
            variant=1+((19*i+i//4+orientation+asset_code)%3)
            angle=(i*2.39996+orientation)*(.55 if variant==2 else 1)
            coords=[turn(*local_uv(END_RECTS[variant],l.uv),-angle) for l in loops]
            if not round_end(coords):
                for l,(u,v) in zip(loops,coords):
                    l.uv=atlas_uv(END_RECTS[0],u,v)
        preserve_small_charts(mesh)
        mesh['court_surface_revision']=3
        mesh.update()
        return
    for poly in mesh.polygons:
        loops=[layer.data[i] for i in poly.loop_indices]
        original=[item.uv.copy() for item in loops]
        centre=sum((uv.x for uv in original))/3,sum((uv.y for uv in original))/3
        xp,yp=centre[0]*2048,(1-centre[1])*2048
        if xp<1280 and yp<1280:
            side,s,q,extent,depth=slope_side(poly.center,x,y,ridge)
            pitch=2*extent/round(2*extent/.325)
            prev=[local_uv(zones['roof'],v) for v in original]
            # Recover old wrap counts from physical position and the tracked UV window.
            a=round(((s+extent)/pitch)/8-sum(u for u,v in prev)/3)
            b=round(q/(8*.396)-sum(v for u,v in prev)/3)
            col,row=tile_window(asset_code,side,a,depth)
            for loop,(u,v) in zip(loops,prev):
                loop.uv=atlas_uv(zones['roof'],(col+8*u)/TILES,(row+8*b+8*v)/TILES)
        elif 1280<=xp and 768<=yp<1104:
            idx=rafter_index(poly.center,poly.normal,x,y,eave,ridge,tier)
            if not round_end([local_uv(zones['end'],v) for v in original]):
                idx=None
            variant=0
            if idx is not None:
                i,orientation=idx
                variant=1+((19*i+i//4+orientation+asset_code)%3)
            for loop,value in zip(loops,original):
                u,v=local_uv(zones['end'],value)
                if idx is not None:
                    # Rotate around the pith without moving a triangular face out of its disk.
                    angle=(i*2.39996+orientation)*(.55 if variant==2 else 1)
                    u,v=turn(u,v,angle)
                loop.uv=atlas_uv(END_RECTS[variant],u,v)
    preserve_small_charts(mesh)
    mesh['court_surface_revision']=3
    mesh.update()

