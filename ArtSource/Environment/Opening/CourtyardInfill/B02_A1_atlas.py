"""Prepare the B02 wood and clay trims and the low-poly roof normal swatch.

Run with system Python and an output directory. Pixel coordinates use a top-left
origin. Roof relief is confined to the original straight roof's normal island.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

SOURCE = Path(__file__).resolve().parent
ART = SOURCE.parents[3] / "Assets/Art/Environment/Opening"
RUNTIME = ART / "CourtyardInfill/B02"
OUT = Path(sys.argv[1])
OUT.mkdir(parents=True, exist_ok=True)
X, Y, W = 1380, 328, 480
images = {role: Image.open(RUNTIME / f"B02_{role}.png").copy()
          for role in ("BaseColor", "Normal", "Mask")}


def put(dx, dy, rgb, normal, smooth):
    h, w = rgb.shape[:2]
    mask = Image.new("RGBA", (w, h), (4, 250, 0, smooth))
    for role, patch in (("BaseColor", rgb), ("Normal", normal), ("Mask", mask)):
        if isinstance(patch, np.ndarray):
            patch = Image.fromarray(patch.clip(0, 255).astype("uint8"))
        dest=images[role]
        scale=dest.width/2048
        size=(round(w*scale),round(h*scale))
        dest.paste(patch.resize(size,Image.Resampling.LANCZOS),
                   (round((X+dx)*scale),round((Y+dy)*scale)))


def photo(role):
    path = ART / "PhotoArchitecture" / ("Timber_Color.jpg" if role == "color" else "Timber_Normal.png")
    # Keep the normal axes and photograph in their original orientation.
    return np.array(Image.open(path).convert("RGB").resize((480, 260), Image.Resampling.LANCZOS))


wood = photo("color").astype(float)*[.84,.87,.89]
put(0, 0, wood, photo("normal"), 62)
rng = np.random.default_rng(6202)
for i in range(4):
    y, x = np.mgrid[-1:1:120j, -1:1:120j]
    x = x + .16 * np.sin(i + .5)
    y = y + .14 * np.cos(i)
    angle = np.arctan2(y, x)
    radius = np.hypot(x, y) * (1 + .033*np.sin(3*angle+i) + .021*np.cos(6*angle))
    rings = np.cos(52*radius + 1.2*np.sin(13*radius+i))
    latewood = np.exp(-5*(1+rings))
    cuts = np.zeros_like(radius)
    for start in (.24, 2.63, 4.88):
        delta = angle - start - .9*i - .08*np.sin(radius*10+i)
        cuts += np.exp(-((np.arctan2(np.sin(delta), np.cos(delta)) * radius)/.015)**2) * np.clip((radius-.40)*3, 0, 1)
    grain = rng.normal(0, 3, radius.shape) - 13*latewood - 42*cuts
    rgb = wood[22:142, i*88:i*88+120].astype(float)*.54 + [57, 47, 38] + grain[:,:,None]
    gy, gx = np.gradient(.42*latewood + cuts)
    normal = np.stack([127+gx*76, 127-gy*76, 254-np.minimum(14, (gx*gx+gy*gy)*16)], axis=-1)
    put(120*i, 260, rgb, normal, 50)

# A warmer fired edge on the lip catches grazing light. The body samples the
# original blue-grey roof island. Carry its mineral variation into the edge.
base=images["BaseColor"]
tile_box=tuple(round(a*base.width/2048) for a in (42,1630,522,1730))
clay=np.array(base.crop(tile_box).convert("RGB").resize((480,100)),dtype=float)
put(0, 380, clay * [1.06, 1.01, .95] + [7, 5, 4],
    np.array(images["Normal"].crop((42, 1630, 522, 1730)).convert("RGB")), 72)

# Transfer the slope of the removed 60 mm crowns into the existing straight
# roof normal swatch. Retain baked fine normals from its adjacent undisturbed
# gutter. Sampling outside the destination makes regeneration idempotent.
normal=images["Normal"]
left,top,right,bottom=10,1396,323,1966
width,height=right-left,bottom-top
fine=np.asarray(normal.crop((3,top,10,bottom)).convert("RGB"),dtype=float)/127.5-1
fine=np.tile(fine,(1,(width+6)//7,1))[:,:width]
y,x=np.mgrid[top:bottom,left:right]
phase=2*np.pi*(y+.5-1412)/44.8
crown=(1-np.cos(phase))*.5
slope=.060*2.8*np.pi/.25 * crown**1.8 * np.sin(phase)
fade=np.clip((x-27)/39,0,1)*np.clip((316-x)/25,0,1)
vectors=np.dstack((fine[:,:,0]*.45, fine[:,:,1]*.45+slope*fade, np.ones_like(slope)))
vectors/=np.linalg.norm(vectors,axis=2,keepdims=True)
normal.paste(Image.fromarray(np.rint((vectors+1)*127.5).astype("uint8")),(left,top))
for role, img in images.items():
    img.save(OUT / f"B02_{role}.png")
