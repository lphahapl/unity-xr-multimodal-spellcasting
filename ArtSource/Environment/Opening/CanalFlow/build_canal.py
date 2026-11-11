"""Rebuild the shared water mesh and its seamless 128x256 data tile (offline)."""
from pathlib import Path
import argparse, re, uuid
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent
PROJECT=next(root for root in HERE.parents if (root/'ProjectSettings/ProjectVersion.txt').is_file())
ASSETS=PROJECT/'Assets'
def smooth(a,b,x):
    w=np.clip((x-a)/(b-a),0.,1.);return w*w*(3.-2.*w)
def unit(v):
    return v/np.linalg.norm(v)
def make_pattern():
    h,w=256,128
    yy,xx=np.mgrid[:h,:w]
    x=(xx+.5)/w;y=(yy+.5)/h; tau=np.pi*2
    warp=.18*np.sin(tau*x)+.085*np.sin(tau*(2*x-y))+.06*np.cos(tau*(x+2*y))
    height=(.47*np.sin(tau*(3*y+warp))+.25*np.cos(tau*(4*y-2*x+.65*warp))+
            .16*np.sin(tau*(7*y+x+1.2*warp))+.13*np.cos(tau*(2*y+3*x))+
            .07*np.cos(tau*(11*y-2*x+warp)))
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*w/2
    dy=(np.roll(height,-1,0)-np.roll(height,1,0))*h/2
    crest=np.zeros((h,w));air=np.zeros_like(crest)
    rand=np.random.default_rng(83217)
    for k in range(26):
        px,py=rand.uniform(0,1,2);span=rand.uniform(.034,.132); thickness=rand.uniform(.0045,.012)
        dxn=(x-px+.5)%1-.5;dyn=(y-py+.5)%1-.5
        bend=rand.uniform(-.47,.47)*dxn+rand.uniform(-.028,.041)*(dxn/span)**2
        edge=np.exp(-1.8*np.maximum(np.abs(dxn/span),0.)**4)
        dash=.53+.47*smooth(-.48,.51,np.sin(tau*(4*x+2*y)+k*1.78))
        crest=np.maximum(crest,edge*dash*np.exp(-((dyn-bend)/thickness)**2)*rand.uniform(.58,1.))
        for j in range(3):
            bx=px+rand.uniform(-.036,.036);by=py-rand.uniform(.02,.15)
            bx=(x-bx+.5)%1-.5;by=(y-by+.5)%1-.5
            air=np.maximum(air,np.exp(-(bx/rand.uniform(.005,.014))**2-(by/rand.uniform(.012,.052))**2)*rand.uniform(.56,.96))
    # Thin threads split and trail downstream from the crest fragments.
    for k in range(9):
        cx,cy=rand.uniform(0.,1.,2);reach=rand.uniform(.041,.14)
        dy=(y-cy+.5)%1-.5
        drift=.018*np.sin(dy*13.+k)+.12*dy+rand.uniform(-.18,.18)*dy
        dxn=(x-cx-drift+.5)%1-.5
        trail=np.exp(-((dxn/rand.uniform(.0038,.0075))**2+(dy/reach)**2))
        air=np.maximum(air,trail*rand.uniform(.59,.92))
    data=np.stack([.5+.475*np.tanh(dx/6.5),.5+.475*np.tanh(dy/11.),crest,air],axis=2)
    # UV positive V addresses bottom-to-top in the imported texture.
    out=ASSETS/'Materials/Chapter01/CanalRipples.png';Image.fromarray(np.uint8(np.rint(np.clip(data[::-1],0,1)*255))).save(out)
    meta=(PROJECT/'Assets/Materials/Chapter01/WorkshopPaving.png.meta').read_text(encoding='utf8')
    meta=re.sub(r'(?m)^guid: .+$','guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'Kaogong/CanalRipples.png').hex,meta)
    meta=meta.replace('alphaUsage: 0','alphaUsage: 1').replace('sRGBTexture: 1','sRGBTexture: 0').replace('maxTextureSize: 512','maxTextureSize: 256').replace('compressionQuality: 50','compressionQuality: 100')
    Path(str(out)+'.meta').write_text(meta,encoding='utf8')
def main_band(z):
    # A lower thread continues against the walkway, exposing the original stone
    # bed beside the falling sheet that feeds the outer bank.
    dry=float(smooth(52.35,53.7,z)*(1.-.36*smooth(56.4,58.7,z)))
    left=.060+.570*dry+.025*dry*np.sin((z-52.7)*2.2)
    right=1.017
    level=.241-.034*smooth(49.45,50.05,z)-.044*smooth(50.12,50.52,z)-.029*smooth(50.52,61.29,z)
    foam=.67*np.exp(-((z-50.55)/.32)**2)+.45*np.exp(-((z-52.54)/.31)**2)
    row=[]
    for u in cross_section:
        x=left+(right-left)*u
        elevation=level-(level-.096)*dry*(1-smooth(0.,.55,u))
        row.append([x,elevation,z])
    return np.array(row),right-left,0.,foam
def interpolate_knots(points,t):
    # Cubic Hermite interpolation in path length; original endpoints and water gauge
    # positions remain fixed while the slope through each interior station is continuous.
    widths=points[:,3];p=points[:,:3]
    stations=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p,axis=0),axis=1))]
    i=min(np.searchsorted(stations,t,side='right')-1,len(p)-2);i=max(i,0)
    values=np.c_[p,widths];derivative=np.gradient(values,stations,axis=0)
    a,b=stations[i:i+2];f=(t-a)/(b-a);g=f*f;h=g*f
    return ((2*h-3*g+1)*values[i]+(h-2*g+f)*(b-a)*derivative[i]+(-2*h+3*g)*values[i+1]+(h-g)*(b-a)*derivative[i+1])
cross_section=np.array([0.,.18,.5,.82,1.])
rows=[];indices=[]
def strip(samples, flow_direction=1):
    initial=len(rows); arc=0.;previous=None; flow_time_distance=0.;previous_air=0.
    for i,(pts,width,lift,foam) in enumerate(samples):
        centre=pts[2]
        if previous is not None:
            delta=np.linalg.norm(centre-previous);arc+=delta
            flow_time_distance+=flow_direction*delta/(1.+.28*(lift+previous_air)*.5)
        elif initial>0:
            floor_rows=np.array(rows[:initial])[2::5]
            flow_time_distance=np.interp(centre[2],floor_rows[:,2],floor_rows[:,11])
        previous_air=lift
        previous=centre
        for j,(p,u) in enumerate(zip(pts,cross_section)):
            t=(pts[min(j+1,4)]-pts[max(j-1,0)])
            a=samples[min(i+1,len(samples)-1)][0][j]-samples[max(i-1,0)][0][j]
            t=unit(t);normal=unit(np.cross(a,t))
            rows.append([*p,*normal,*t,-float(flow_direction),u,arc,width,lift,foam,flow_time_distance])
        if i>0:
            for j in range(4):
                k=initial+(i-1)*5+j
                indices.extend([k,k+5,k+1,k+1,k+5,k+6])
def make_mesh():
    z_values=np.unique(np.round(np.r_[np.arange(49.32,61.285,.20),[49.32,50.12,50.25,50.40,50.53,61.285]],4))
    strip([main_band(z) for z in z_values])
    points=np.array([[.41,.117,52.02,.41],[.416,.138,52.23,.44],[.425,.171,52.44,.51],[.442,.258,52.68,.55],[.45,.55,53.10,.52],[.39,.86,53.65,.50],
        [-.06,1.43,54.40,.51],[-.68,2.20,55.15,.48],[-1.34,2.94,55.90,.36]])
    length=np.linalg.norm(np.diff(points[:,:3],axis=0),axis=1).sum()
    path=np.array([interpolate_knots(points,t) for t in np.linspace(0,length,32)])
    directions=np.gradient(path[:,:3],axis=0);samples=[]
    for i, (values,tangent) in enumerate(zip(path,directions)):
        distance=i/(len(path)-1)*length
        p=values[:3];width=values[3]
        horizontal=unit(np.cross([0,1,0],tangent)); normal=unit(np.cross(tangent,horizontal))
        # Low ridges fold the open sheet across its width without closing it into a tube.
        twist=.12*np.sin(distance*2.3)*smooth(.15,1.2,distance)
        across=unit(horizontal+twist*normal); face=unit(np.cross(tangent,across));pts=[]
        for u in cross_section:
            r=(u-.5)*width
            offset=.019*np.sin(5.8*u+distance*2.2)*smooth(.21,.85,distance)
            # Let the sheet enter the pool at its centre, with the side edges submerged.
            root=np.exp(-((distance-.40)/.34)**2)
            offset+=root*(.023-.061*abs(2*u-1.)**1.65-.009*np.sin(7*u))
            pts.append(p+across*r+face*offset)
        # The low end is the landing. Keep visible turbulence there and a quieter high source.
        foam=.82*np.exp(-((distance-.46)/.46)**2)+.14*smooth(length-.70,length-.05,distance)
        samples.append((np.array(pts),width,float(smooth(.28,1.18,distance)),foam))
    # Stations are authored from the shared pool upwards. Their signed phase and
    # longitudinal ripple normal point downhill, towards the phase-matched landing.
    strip(samples, flow_direction=-1)
    rows_np=np.array(rows,dtype='<f4');positions=rows_np[:,:3]
    low=positions.min(axis=0)-[.035,.035,.035];high=positions.max(axis=0)+[.035,.035,.035]
    center=(low+high)*.5;ext=(high-low)*.5
    asset=ASSETS/'Meshes/Chapter01/CanalRibbon.asset';s=(HERE/'CanalRibbon.template.yml').read_text(encoding='utf8')
    def assign(name,value):
        nonlocal s
        s=re.sub(r'(?m)^(\s*'+name+r': ).*$',lambda m:m[1]+str(value),s)
    assign('indexCount',len(indices));assign('vertexCount',len(rows));assign('m_VertexCount',len(rows))
    assign('m_DataSize',rows_np.nbytes);assign('m_IndexBuffer',np.asarray(indices,'<u2').tobytes().hex());assign('_typelessdata',rows_np.tobytes().hex())
    for key,vec in [('m_Center',center),('m_Extent',ext)]:assign(key,'{'+', '.join(c+': '+format(v,'.7g') for c,v in zip('xyz',vec))+'}')
    chans=[(0,3),(12,3),(24,4),(0,0),(40,2),(48,4)]+[(0,0)]*8
    channel_text=''.join('    - stream: 0\n      offset: '+str(o)+'\n      format: 0\n      dimension: '+str(d)+'\n' for o,d in chans)
    s=re.sub(r'(?s)(    m_Channels:\n).*?(    m_DataSize:)',lambda m:m[1]+channel_text+m[2],s)
    asset.write_text(s,encoding='utf8');print(f'{len(rows)} vertices; {len(indices)//3} tris; AABB {center} {ext} | {len(z_values)} main stations, {len(path)} fall stations')
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-root',type=Path,default=PROJECT,
                        help='Project-style output folder. An empty temporary folder can be used for independent validation.')
    ASSETS=parser.parse_args().output_root.resolve()/'Assets'
    (ASSETS/'Materials/Chapter01').mkdir(parents=True,exist_ok=True)
    (ASSETS/'Meshes/Chapter01').mkdir(parents=True,exist_ok=True)
    make_pattern();make_mesh()
