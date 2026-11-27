"""Lay out unchanged actual Blender renders at equal scale; no image generation."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parent;old=P.parents[1]/'QingGuiR01/Model'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',28);small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
def three(folder,files,title,target):
    im=Image.new('RGB',(1800,830),(34,39,43));d=ImageDraw.Draw(im);d.text((28,12),title,font=font,fill='white')
    for i,(file,label) in enumerate(zip(files,['正面 / Front','右侧 / Right','背面 / Back'])):
        pic=Image.open(folder/file).convert('RGB').resize((600,720),Image.Resampling.LANCZOS);im.paste(pic,(600*i,58));d.text((600*i+20,779),label,font=small,fill='white')
    im.save(target)
three(P/'Previews',['01_Front.png','02_Right.png','03_Back.png'],'清圭 R02 · 同尺度真实游戏模型 PBR · 总高 350 mm',P/'Previews/12_ThreeView_SameScale.png')
three(P/'SourcePreview',['07_Front.png','08_Right.png','09_Back.png'],'清圭 R02 · Blender 源材质三视图 · 非 Unity 实机',P/'SourcePreview/14_ThreeView_Source.png')
for name,lf,rf,title in [('13_R01_R02_45','04_Front45.png','04_Front45.png','R01 / R02 · 同机位真实低模 PBR 对照'),('14_R01_R02_Aperture','06_ApertureClose.png','06_Aperture.png','R01 / R02 · 圆口与表面实体金丝近景'),('15_R01_R02_Shoulder','07_ShoulderAlignment.png','07_Shoulder.png','R01 / R02 · 下肩过渡与窄云雷纹带')]:
    im=Image.new('RGB',(1600,1050),(34,39,43));d=ImageDraw.Draw(im);d.text((24,12),title,font=font,fill='white')
    for i,file in enumerate([old/'Previews'/lf,P/'Previews'/rf]):
        pic=Image.open(file).convert('RGB').resize((800,960),Image.Resampling.LANCZOS);im.paste(pic,(800*i,55));d.text((800*i+20,1018),'R01' if i==0 else 'R02',font=small,fill='white')
    im.save(P/f'Previews/{name}.png')
print('ACTUAL_RENDER_SHEETS_READY')
