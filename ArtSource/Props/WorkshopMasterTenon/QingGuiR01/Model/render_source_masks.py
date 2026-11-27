"""Continuous 4K source masks from millimetre curves; not a replacement for cut geometry."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFilter
OUT=Path(__file__).resolve().parent;DEST=OUT/'SourceMasks';DEST.mkdir(exist_ok=True)
points=json.loads((OUT/'source-mask-paths.json').read_text('utf-8'))
N=8192
for face,pts in zip(['Front','Back'],points):
    for kind,extra in [('Gold',0),('CutSeat',.00040)]:
        im=Image.new('L',(N,N));draw=ImageDraw.Draw(im)
        for (x,z),w in pts:
            rad=w+extra;cx=(x+.07)/.14*N;cy=(1-z/.35)*N;rx=rad/.14*N;ry=rad/.35*N
            draw.ellipse((cx-rx,cy-ry,cx+rx,cy+ry),fill=255)
        im=im.resize((4096,4096),Image.Resampling.LANCZOS)
        im=im.filter(ImageFilter.GaussianBlur(.35 if kind=='Gold' else 2.0))
        im.save(DEST/f'{kind}_{face}.png')
        print('MASK',kind,face)
