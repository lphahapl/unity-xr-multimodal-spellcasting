"""Original bronze ornament masks. The drawing units are metres."""
from pathlib import Path
from math import cos, sin, pi
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import sys

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).parent
Q=2700

class Plate:
    def __init__(self,w,h):
        self.w,self.h=w,h
        self.size=(round(w*Q), round(h*Q))
        self.layer=[Image.new('L',self.size) for _ in range(3)]

    def xy(self,p):
        x,y=p
        return ((x+self.w/2)*Q,(self.h/2-y)*Q)

    def line(self,points,width,color,layer=0):
        pen=ImageDraw.Draw(self.layer[layer])
        points=[self.xy(p) for p in points]
        thick=max(1,round(width*Q))
        pen.line(points,fill=color,width=thick,joint='curve')
        for x,y in points[0],points[-1]:
            r=thick*.5
            pen.ellipse((x-r,y-r,x+r,y+r),fill=color)

    def dot(self,x,y,r,color,layer=0):
        x,y=self.xy((x,y))
        r*=Q
        ImageDraw.Draw(self.layer[layer]).ellipse((x-r,y-r,x+r,y+r),fill=color)

    def save(self,name):
        Image.merge("RGB",tuple(img.filter(ImageFilter.GaussianBlur(.7)) for img in self.layer)).save(root/name)


def bezier(points):
    curve=[points[0]]
    for p,q,r in zip(points[1::3],points[2::3],points[3::3]):
        a=curve[-1]
        for i in range(1,19):
            t=i/18; j=1-t
            curve.append(tuple(j**3*a[k]+3*j*j*t*p[k]+3*j*t*t*q[k]+t**3*r[k] for k in (0,1)))
    return curve


def motif(plate,cx,cy,side=1,flip=1,scale=.77):
    def pos(pts):
        return [(cx+side*x*scale,cy+flip*y*.91) for x,y in pts]
    def stroke(pts,w,level):
        plate.line(pos(bezier(pts)),w,level)
    stroke([(-.072,-.022),(-.083,-.001),(-.064,.019),(-.044,.020),
            (-.010,.022),(-.004,-.017),(.019,-.019),(.036,-.020),(.054,-.008),(.053,.007),
            (.052,.022),(.031,.029),(.023,.019),(.016,.012),(.022,.007),(.032,.008)],.0092,218)
    stroke([(-.072,-.022),(-.081,-.032),(-.098,-.027),(-.096,-.015),
            (-.093,-.006),(-.075,-.014),(-.068,-.017)],.0100,234)
    stroke([(-.090,-.019),(-.083,-.000),(-.068,.003),(-.057,-.005)],.0052,177)
    stroke([(-.068,.015),(-.074,.025),(-.067,.033),(-.055,.035),
            (-.050,.038),(-.039,.034),(-.045,.029)],.0043,183)
    stroke([(-.048,.011),(-.043,-.006),(-.029,-.011),(-.021,-.004)],.0042,143)
    stroke([(-.018,-.014),(-.024,-.027),(-.016,-.036),(-.005,-.032)],.0043,163)
    stroke([(.065,.004),(.077,.023),(.096,.028),(.098,.013),
            (.100,.003),(.088,.001),(.083,.007)],.0041,148)
    # Small repeating notches along the shoulder read as scales at inspection distance.
    for x,y in [(-.054,.019),(-.043,.020),(-.032,.017),(-.023,.012),(-.015,.004),(.003,-.015),(.014,-.019)]:
        plate.line(pos([(x-.003,y+.002),(x+.001,y-.002)]),.0025,102,2)
    x,y=pos([(-.083,-.017)])[0]
    plate.dot(x,y,.0029,235,1)
    x,y=pos([(-.083,-.017)])[0]
    plate.dot(x,y,.00145,223,2)


def key(plate,x,y,s=1,invert=1,level=135):
    coords=[(-.010,-.007),(-.010,.009),(.011,.009),(.011,-.009),(-.004,-.009),(-.004,.003),(.005,.003),(.005,-.003)]
    plate.line([(x+a*s,y+b*s*invert) for a,b in coords],.0029*s,level)


def side_plate():
    p=Plate(.57,.34)
    rim=[(-.241,-.134),(.241,-.134),(.255,-.121),(.226,.119),(.212,.133),
         (-.212,.133),(-.226,.119),(-.255,-.121),(-.241,-.134)]
    p.line(rim,.0055,169)
    inner=[(-.229,-.122),(.229,-.122),(.241,-.112),(.216,.110),(.205,.121),
           (-.205,.121),(-.216,.110),(-.241,-.112),(-.229,-.122)]
    p.line(inner,.0031,113)
    for row,y in enumerate((.045,-.043)):
        # A continuous secondary ribbon crosses the staggered primary dragon bodies.
        wave=[(-.219+j*.438/120,y+.030*sin(j*pi/30+pi*row)) for j in range(121)]
        p.line(wave,.0047,112)
        for i,x in enumerate((-.139,0,.139)):
            motif(p,x,y,-1 if ((row+i)%2) else 1,-1 if ((i)%2) else 1)
        for x in (-.207,.207):
            key(p,x,y-.043 if row==0 else y+.039,.58,1 if row else -1,124)
        for i,x in enumerate((-.175,-.105,-.035,.035,.105,.175)):
            key(p,x,y+.037*(1 if (i+row)%2 else -1),.47,1 if i%2 else -1,104)
    for y in (-.092,.093):
        p.line([(-.209,y),(.209,y)],.003,118)
        for j in range(-7,8):
            x=j*.027
            p.line([(x-.006,y+.009),(x,y+.016),(x+.006,y+.009),(x,y+.004),(x-.006,y+.009)],.0028,143)
            p.dot(x,y+.01,.0023,152,1)
    p.save('InterlaceRelief.png')


def front_ground():
    p=Plate(.62,.09)
    for row,y in enumerate((-.023,.002,.025)):
        for col in range(-12,13):
            key(p,col*.023+(row%2)*.0115,y,.72,-1 if col%2 else 1,162)
    p.save('AnimalMaskGround.png')


def turn(x,y,quarter):
    for _ in range(quarter):
        x,y=-y,x
    return x,y


def top_plate():
    p=Plate(.8,.8)
    def loop(offset,cut):
        return [(-offset+cut,-offset),(offset-cut,-offset),(offset,-offset+cut),(offset,offset-cut),
                (offset-cut,offset),(-offset+cut,offset),(-offset,offset-cut),(-offset,-offset+cut),(-offset+cut,-offset)]
    p.line(loop(.354,.029),.0049,138,1)
    p.line(loop(.343,.027),.0029,119,1)
    p.line(loop(.243,.026),.0042,107,1)
    p.line(loop(.232,.024),.0029,100,1)
    module=[(-.052,0),(-.027,.024),(-.017,.014),(-.029,.002),(-.039,.011),(-.025,.023),
            (-.007,.005),(.007,.005),(.027,.025),(.039,.013),(.029,.003),(.017,.015),(.035,.033),(.052,.017)]
    for q in range(4):
        for i,cx in enumerate((-.211,-.1055,0,.1055,.211)):
            mid=-.29
            for s in (-1,1):
                transformed=[turn(cx+(x if s==1 else -x),mid+s*y,q) for x,y in module]
                p.line(transformed,.0048,191,1)
            diamond=[(cx,mid+.013),(cx+.014,mid),(cx,mid-.013),(cx-.014,mid),(cx,mid+.013)]
            p.line([turn(x,y,q) for x,y in diamond],.0043,165,1)
            x,y=turn(cx,mid,q)
            p.dot(x,y,.0034,152,1)
            for dy in (-.043,.043):
                x,y=turn(cx,mid+dy,q)
                p.dot(x,y,.0028,148,1)
        points=[(.265,-.292),(.295,-.321),(.321,-.294),(.305,-.278),(.288,-.295),(.298,-.308)]
        p.line([turn(x,y,q) for x,y in points],.0048,179,1)
    # A weathered incision remains legible even when the specular lobe misses the viewer.
    border=p.layer[1].filter(ImageFilter.MaxFilter(9))
    p.layer[2]=ImageChops.subtract(border,p.layer[1]).point(lambda value: min(255,round(value*.92)))
    p.save('SilverInlayMask.png')

side_plate()
front_ground()
top_plate()
