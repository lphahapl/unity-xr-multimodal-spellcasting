"""Approved C4 crown thunder mark and narrow continuous collar meander."""
def paths():
    out=[]
    motif=[(-.5,-.0015),(-.35,-.0015),(-.35,.0015),(.35,.0015),(.35,-.0007),(-.05,-.0007),(-.05,.0005),(.15,.0005)]
    for k in range(8):out.append(([(-.049+k*.013+x*.013,.108+z) for x,z in motif],.00028))
    for z in [.1058,.1103]:out.append(([(-.054,z),(.054,z)],.00030))
    out.append(([(.016,.323),(.016,.328),(.023,.328),(.023,.324),(.019,.324),(.019,.326)],.00032))
    return out
if __name__=='__main__':
    from pathlib import Path
    from PIL import Image,ImageDraw
    p=Path(__file__).resolve().parent/'SourceMasks';p.mkdir(exist_ok=True);res=4096;height=10240;im=Image.new('L',(res,height));d=ImageDraw.Draw(im)
    for line,r in paths():
        # xz world-space projection has unequal pixel pitches; scale width for X.
        xy=[((x+.07)/.14*res,(1-z/.35)*height) for x,z in line]
        d.line(xy,fill=255,width=max(1,round(2*r/.14*res)),joint='curve')
    im.resize((4096,4096),Image.Resampling.LANCZOS).save(p/'FinePattern.png')
