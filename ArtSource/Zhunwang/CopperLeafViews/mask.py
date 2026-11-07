from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

base = Path(__file__).parent
img = Image.new("L", (3400, 960))
d = ImageDraw.Draw(img)
font = ImageFont.truetype(r"C:\Windows\Fonts\simkai.ttf", 143)
for i, glyph in enumerate("方圆规印"):
    d.text((995 + i*158, 482), glyph, font=font, fill=238, anchor="mm")
cx, cy, r, half = 1880, 480, 149, 74
d.ellipse((cx-r,cy-r,cx+r,cy+r), outline=238, width=12)
d.rectangle((cx-half,cy-half,cx+half,cy+half), outline=238, width=11)
img.filter(ImageFilter.GaussianBlur(1.9)).save(base / "engraving_mask.png")
