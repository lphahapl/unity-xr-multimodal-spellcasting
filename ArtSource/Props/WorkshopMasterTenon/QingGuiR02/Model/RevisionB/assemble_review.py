"""Equal-scale layout of actual unchanged game-mesh renders; no image synthesis."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parent;PRE=P/'Previews';old=P.parent/'Previews'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',28);small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
im=Image.new('RGB',(1800,830),(34,39,43));d=ImageDraw.Draw(im);d.text((28,12),'清圭 R02 Revision B · 真实游戏 LOD0 · 同尺度三视 · 总高 350 mm',font=font,fill='white')
for i,(file,label) in enumerate(zip(['01_Front.png','02_Right.png','03_Back.png'],['正面 / Front','右侧 / Right','背面 / Back'])):
    pic=Image.open(PRE/file).convert('RGB').resize((600,720),Image.Resampling.LANCZOS);im.paste(pic,(600*i,58));d.text((600*i+20,779),label,font=small,fill='white')
im.save(PRE/'12_ThreeView_SameScale.png')
for name,file,title in [('13_R02_B_45','04_Front45.png','原 R02 / Revision B · 同机位同灯光 · 实际游戏 LOD0'),('14_R02_B_Aperture','06_Aperture.png','原 R02 / Revision B · 孔冠铜玉边 · 实际游戏 LOD0'),('15_R02_B_Shoulder','07_Shoulder.png','原 R02 / Revision B · 薄铜包边与肩 · 实际游戏 LOD0')]:
    im=Image.new('RGB',(1600,1050),(34,39,43));d=ImageDraw.Draw(im);d.text((24,12),title,font=font,fill='white')
    for i,path in enumerate([old/file,PRE/file]):
        pic=Image.open(path).convert('RGB').resize((800,960),Image.Resampling.LANCZOS);im.paste(pic,(800*i,55));d.text((800*i+20,1018),'原 R02' if i==0 else 'Revision B',font=small,fill='white')
    im.save(PRE/f'{name}.png')
print('ACTUAL_RENDER_SHEETS_READY')
