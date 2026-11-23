"""Assemble the inspected Cycles views into delivery and topology sheets."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

out = Path(__file__).resolve().parent/'Review'
models = [('CourtMainRoof','主殿四坡屋盖'),('CourtGatehouse','独立院门'),('CourtLowWing','低翼硬山屋盖')]
sheet = Image.new('RGB',(1536,726),(45,44,41))
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
for i,(name,title) in enumerate(models):
    x=12+508*i
    draw.text((x,8),title,font=font,fill=(240,232,214))
    for j,kind in enumerate(('overview','eave')):
        with Image.open(out/(name+'_'+kind+'.jpg')) as view:
            sheet.paste(view.resize((496,316),Image.Resampling.LANCZOS),(x,49+324*j))
    topology=Image.new('RGB',(1280,400))
    for j,angle in enumerate(('three_quarter','bottom')):
        with Image.open(out/(name+'_wire_'+angle+'.png')) as view:
            topology.paste(view.convert('RGB'),(640*j,0))
    topology.save(out/(name+'_wire.png'))
draw.text((12,688),'A1  ·  导出 FBX 回读 / Blender 5.2.2 Cycles',font=font,fill=(215,206,190))
sheet.save(out/'A1_CourtArchitecture.jpg',quality=94)

if (out/'CourtMainRoof_tip_after.jpg').exists():
    comparison=Image.new('RGB',(1440,1024),(45,44,41))
    labels=ImageDraw.Draw(comparison)
    for i,(state,caption) in enumerate([('before','修订前'),('after','修订后')]):
        x=12+i*716
        labels.text((x,9),caption,font=font,fill=(240,232,214))
        for kind,y,crop in [('overview',50,(0,85,1080,602)),('eave',400,(0,175,1080,562)),
                             ('tip',668,(0,212,1080,670))]:
            suffix='_'+state if kind=='tip' or state=='before' else ''
            with Image.open(out/('CourtMainRoof_'+kind+suffix+'.jpg')) as view:
                w,h=crop[2]-crop[0],crop[3]-crop[1]
                comparison.paste(view.crop(crop).resize((704,round(h*704/w)),Image.Resampling.LANCZOS),(x,y))
    labels.text((12,982),'主殿整坡、檐口与木椽切面  ·  相同机位和光照  ·  正式 FBX 回读',font=font,fill=(215,206,190))
    comparison.save(out/'CourtMainRoof_surface_compare.jpg',quality=95)

