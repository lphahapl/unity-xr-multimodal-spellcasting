"""Create the review board from the four Blender state captures."""
from pathlib import Path

from PIL import Image,ImageDraw,ImageFont

work=Path(__file__).resolve().parent
font_file=Path("C:/Windows/Fonts/msyh.ttc")
heading=ImageFont.truetype(font_file,38)
normal=ImageFont.truetype(font_file,21)
small=ImageFont.truetype(font_file,17)
background="#161b1d"
primary="#f2eee3"
muted="#bcae8f"
panel=Image.new("RGB",(1440,600),background)
draw=ImageDraw.Draw(panel)
draw.text((26,17),"醒神松墨锭",font=heading,fill=primary)
draw.text((26,68),"三件混元回件组装 · 31 mm 滑套 · 两件墨身沿预分面独立",font=small,fill=muted)
for index,(name,key,time) in enumerate((
    ("合套","Closed","0.00 秒"),
    ("推开","SlideOpen","2.27 秒"),
    ("折下耗用段","Detached","3.83 秒"),
    ("收起保留段","Reclosed","7.37 秒")
)):
    x=26+index*350
    source=Image.open(work/f"{key}-quarter.png").convert("RGB")
    panel.paste(source.resize((338,338),Image.Resampling.LANCZOS),(x,125))
    draw.text((x,481),name,font=normal,fill=primary)
    draw.text((x,517),time,font=small,fill=muted)
panel.save(work/"PineInk-Review-States.jpg",quality=94,subsampling=0)

