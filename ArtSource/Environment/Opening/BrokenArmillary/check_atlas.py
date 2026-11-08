"""Verify texel coverage and produce the review sheet after review.py has rendered.
Uses Python 3 + numpy + Pillow outside Blender. Pass the Blender review folder if TEMP differs.
"""
from pathlib import Path
import os, sys, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
SOURCE=Path(sys.argv[1]) if len(sys.argv)>1 else Path(os.environ['TEMP'])/'KaogongArmillaryReview'
MAPS=HERE.parents[3]/'Assets/Art/Environment/Opening/BrokenArmillary/Textures'
data=json.loads((SOURCE/'review_geometry.json').read_text())
uv=np.array(data['uv']);uv[:,:,1]=1-uv[:,:,1]
res=1024;count=np.zeros((res,res),np.int32)
def cross(a,b):
    return a[...,0]*b[...,1]-a[...,1]*b[...,0]
areas=[]
for triangle in uv*res:
    lo=np.floor(triangle.min(0)).astype(int).clip(0,res);hi=np.ceil(triangle.max(0)).astype(int).clip(0,res)
    y,x=np.mgrid[lo[1]:hi[1],lo[0]:hi[0]]
    delta=np.stack([x+.5,y+.5],-1)-triangle[0]
    a,b=triangle[1]-triangle[0],triangle[2]-triangle[0]
    det=cross(a,b);areas.append(abs(det)/2)
    if abs(det)<1e-10:continue
    t=cross(delta,b)/det;u=cross(a,delta)/det
    count[lo[1]:hi[1],lo[0]:hi[0]]+=(t>1e-5)&(u>1e-5)&((t+u)<1-1e-5)
surface=count>0
def sample(channel):
    return np.asarray(Image.open(MAPS/('BrokenArmillary_'+channel+'.png')).convert('RGBA'))[surface]
norm=sample('Normal');al=sample('BaseColor');p=sample('Mask')
report={'surface_texels':int(surface.sum()),'overlap_texels':int((count>1).sum()),
    'uv_coverage_pct':round(float(surface.mean()*100),3),'uv_min':float(uv.min()),'uv_max':float(uv.max()),
    'degenerate_uv_triangles':sum(a<1e-7 for a in areas).item(),'normal_negative_Z_pct':round(float(np.mean(norm[:,2]<127)*100),4),
    'base_black_pct':round(float(np.mean(al[:,:3].max(1)==0)*100),4),'packed_RGBA_median':np.median(p,axis=0).tolist()}
(HERE/'atlas_readback.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(report)
assert report['overlap_texels']==0 and report['degenerate_uv_triangles']==0
assert report['uv_min']>=0 and report['uv_max']<=1
board=Image.new('RGB',(1224,874),'#182523');draw=ImageDraw.Draw(board)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
def panel(im,x,y,label):
    size=388
    board.paste(im.resize((size,size),Image.Resampling.LANCZOS),(x,y))
    draw.text((x,y+size+7),label,font=font,fill='#edf1e8')
for x,key,label in [(12,'source','可编辑铸件高模 · 29,832 三角'),(418,'game','烘焙低模 · 1,776 三角'),(824,'break','低模实拍 · 错开的两截测轴')]:
    panel(Image.open(SOURCE/(key+'.png')).convert('RGB'),x,14,label)
wire=Image.new('RGB',(720,720),'#263733');w=ImageDraw.Draw(wire)
for a,b in data['wire']:w.line([tuple(a),tuple(b)],fill='#bda476',width=1)
layout=Image.open(MAPS/'BrokenArmillary_BaseColor.png').convert('RGB');pen=ImageDraw.Draw(layout)
for c in uv:pen.line([tuple(a*1024) for a in np.vstack((c,c[0]))],fill='#a4ab85',width=1)
panel(wire,12,444,'游戏网格 · 三座支脚落于原石基')
panel(layout,418,444,'单套 UV · 1K 色图与网格')
panel(Image.open(MAPS/'BrokenArmillary_Normal.png').convert('RGB'),824,444,'高模投射 · 切线空间法线')
board.save(HERE/'BrokenArmillary_review.jpg',quality=91,subsampling=0)
