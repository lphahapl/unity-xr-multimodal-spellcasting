"""Preserve the Hunyuan sleeve atlas while correcting copper highlight and axial wood grain."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

WORK=Path(__file__).resolve().parent
ATLAS=WORK/"SlideSleeveTextures"
pbr_path=ATLAS/"SlideSleeve_ORM.png"
color_path=ATLAS/"SlideSleeve_BaseColor.png"
pbr=np.array(Image.open(pbr_path).convert("RGB"))
base=np.asarray(Image.open(color_path).convert("RGB"),dtype=np.float32)
metal=pbr[:,:,2].astype(np.float32)/255.
active_metal=metal>=.29
result=pbr.copy()
result[:,:,1]=np.where(active_metal,np.maximum(pbr[:,:,1],135),np.maximum(pbr[:,:,1],123)).astype("uint8")
assert np.array_equal(result[:,:,[0,2]],pbr[:,:,[0,2]])
outmask=ATLAS/"SlideSleeve_ReviewORM.png"
Image.fromarray(result).save(outmask,compress_level=5)

gain_data=np.load(WORK/"SlideSleeve-AxialGrainField.npz")
grain=gain_data["field"]
covered=gain_data["covered"]
# Two texel bleed at island edges keeps minification from revealing a texture underline.
for _ in range(2):
    strength=np.zeros_like(grain)
    count=np.zeros(grain.shape,dtype=np.float32)
    for shift,axis in ((-1,0),(1,0),(-1,1),(1,1)):
        neighbor=np.roll(covered,shift,axis=axis)
        strength+=np.roll(grain,shift,axis=axis)*neighbor
        count+=neighbor
    fills=(~covered)&(count>0)
    grain[fills]=strength[fills]/count[fills]
    covered[fills]=True

wood=(1-np.clip((metal-.09)/.20,0,1))*covered
luma=base @ np.array([.2126,.7152,.0722],dtype=np.float32)
luma_base=np.asarray(Image.fromarray(np.uint8(np.clip(luma,0,255))).filter(ImageFilter.GaussianBlur(5)),dtype=np.float32)
existing_detail=np.clip((luma-luma_base)*.48,-8,8)
color=np.rint(np.clip(base*(1+wood[:,:,None]*grain[:,:,None])+wood[:,:,None]*existing_detail[:,:,None],0,255)).astype("uint8")
outbase=ATLAS/"SlideSleeve_ReviewWood.png"
Image.fromarray(color).save(outbase,compress_level=5)
measure={
    "method":"Original metallic and AO channels retained byte-for-byte. Copper roughness floor 0.529 at metal B>=.29. Longitudinal wood variation is UV-projected from the existing mesh and isolated to the existing nonmetal assignment.",
    "roughnessCopperBeforeP5P50P95":[round(float(v)/255,4) for v in np.quantile(pbr[:,:,1][active_metal],[.05,.5,.95])],
    "roughnessCopperAfterP5P50P95":[round(float(v)/255,4) for v in np.quantile(result[:,:,1][active_metal],[.05,.5,.95])],
    "woodUvPixelsAffected":int(np.count_nonzero(wood>.10)),
    "grainGainLimit":.084,
    "hashes":{f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in (color_path,pbr_path,outbase,outmask)}
}
(WORK/"SlideSleeve-Finish.json").write_text(json.dumps(measure,indent=2),encoding="utf-8")

