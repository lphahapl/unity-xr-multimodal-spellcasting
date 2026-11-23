"""Refine only the common B02 pan-tile swatch, using the A1 atlas as input.
Run with Python, Pillow and NumPy. The input and output paths may differ in size
between base color and normal; all coordinates below use the 2048 normal atlas.
"""
from pathlib import Path
import argparse

from PIL import Image
import numpy as np

ap = argparse.ArgumentParser()
ap.add_argument("source", type=Path)
ap.add_argument("destination", type=Path)
args = ap.parse_args()
args.destination.mkdir(parents=True, exist_ok=True)
left, top, right, bottom = 10, 1396, 323, 1966
y, x = np.mgrid[top:bottom, left:right].astype(float)
pitch = 44.8
t = (y + .5 - 1412) / pitch
# A molded cover runs down the slope. The pans to either side can overlap
# independently, so do not line up their visible lips across every course.
column = np.floor(t + .5)
stagger = .16 * (column % 2) + .11 * np.sin(column * 2.37)
s = ((x - 24) / 44 + stagger) % 1
lap = np.exp(-((s - .13) / .052)**2)
fade = np.minimum(np.clip((x-left)/16,0,1), np.clip((right-1-x)/15,0,1))
fade *= np.minimum(np.clip((y-top)/12,0,1),np.clip((bottom-1-y)/12,0,1))
variation = .024 * np.sin(column*1.7 + x*.039) + .02*np.sin(y*.051 - x*.024)
with Image.open(args.source / "B02_BaseColor.png") as source:
    rgb = source.convert("RGB")
    k = rgb.width/2048
    box = tuple(round(k*v) for v in (left,top,right,bottom))
    patch = rgb.crop(box)
    factor = 1 + fade * (variation - .12 * lap + .031*np.exp(-((s-.202)/.064)**2))
    mask = Image.fromarray(factor.astype(np.float32)).resize(patch.size,Image.Resampling.BOX)
    patch = np.asarray(patch)*np.asarray(mask)[:,:,None]
    rgb.paste(Image.fromarray(np.uint8(np.clip(patch,0,255))),box)
    rgb.save(args.destination/"B02_BaseColor.png")

with Image.open(args.source / "B02_Normal.png") as source:
    normal = source.convert("RGBA")
    assert normal.size == (2048,2048)
    a = np.asarray(normal.crop((left,top,right,bottom))).astype(float)/127.5-1
    # Reduce the high-amplitude baked crown slope, preserving the existing
    # normal direction at the border and adding a shallow clay lap.
    u = a[:,:,0] * (1 - .17*fade) - (.071 * np.gradient(lap,axis=1) * 8) * fade
    v = a[:,:,1] * (1 - .20*fade)
    z = np.sqrt(np.maximum(1.e-5, 1 - u*u - v*v))
    baked = np.uint8(np.clip(np.rint((np.dstack((u,v,z,a[:,:,3]))+1)*127.5),0,255))
    baked[fade==0] = np.asarray(normal.crop((left,top,right,bottom)))[fade==0]
    normal.paste(Image.fromarray(baked),(left,top))
    normal.save(args.destination/"B02_Normal.png")


with Image.open(args.source/"B02_Mask.png") as source:
    mask = source.convert("RGBA")
    box = (left,top,right,bottom)
    raw = np.array(mask.crop(box))
    # The baked A1 chart had an AO stripe with zero irradiance in the
    # green channel. Override it inside the common roof swatch only.
    old_ao = raw[:,:,1].astype(float)
    ao = np.maximum(224,old_ao) - 9*lap
    raw[:,:,1] = np.uint8(np.rint(old_ao + fade*(ao-old_ao)))
    mask.paste(Image.fromarray(raw),box)
    mask.save(args.destination/"B02_Mask.png")
