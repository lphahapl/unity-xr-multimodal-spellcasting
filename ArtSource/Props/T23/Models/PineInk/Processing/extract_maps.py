"""Derive a 2K atlas set directly from the three embedded Hunyuan PNGs."""
import hashlib
from io import BytesIO
import json
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

WORK = Path(__file__).resolve().parent
NAME = sys.argv[1] if len(sys.argv)>1 else "WrappedCore"
if NAME not in {"WrappedCore","DetachableSegment","SlideSleeve"}:
    raise ValueError(NAME)
SOURCE = WORK.parent / "Source" / f"{NAME}.glb"
textures = WORK / f"{NAME}Textures"
textures.mkdir(exist_ok=True)

with SOURCE.open("rb") as file:
    magic, version, _, size, kind = struct.unpack("<4sIIII", file.read(20))
    assert magic == b"glTF" and version == 2 and kind == 0x4E4F534A
    gltf = json.loads(file.read(size))
    size, kind = struct.unpack("<II", file.read(8))
    assert kind == 0x004E4942
    payload = file.read(size)

material = gltf["materials"][0]
pbr = material["pbrMetallicRoughness"]
maps = (
    ("BaseColor", pbr["baseColorTexture"]["index"]),
    ("NormalGL", material["normalTexture"]["index"]),
    ("ORM", pbr["metallicRoughnessTexture"]["index"])
)
report = {}
for label, index in maps:
    image_ref = gltf["images"][gltf["textures"][index]["source"]]
    assert image_ref["mimeType"] == "image/png"
    view = gltf["bufferViews"][image_ref["bufferView"]]
    start = view.get("byteOffset", 0)
    data = payload[start:start+view["byteLength"]]
    with Image.open(BytesIO(data)) as img:
        source_dimensions = [img.width, img.height]
        if label == "NormalGL":
            resized = img.convert("RGB").resize((2048,2048),Image.Resampling.BOX)
            vectors = np.asarray(resized).astype("float32") / 127.5 - 1
            lengths = np.maximum(np.linalg.norm(vectors,axis=2,keepdims=True),.001)
            unit = vectors/lengths
            derived = Image.fromarray(np.rint((unit+1.)*127.5).clip(0,255).astype("uint8"))
        else:
            derived = img.convert("RGB").resize((2048,2048),Image.Resampling.LANCZOS)
    if NAME=="DetachableSegment" and label=="BaseColor":
        # The source camera leaked a clean beige background into the sides.
        # Compress only the high-key range, keeping black engraved tracery and surface grain.
        rgb = np.asarray(derived).astype("float32")
        luma = np.sum(rgb*np.array([.2126,.7152,.0722],dtype="float32"),axis=2)
        corrected = 68+(luma-68)*.19
        scale = np.minimum(1.,corrected/np.maximum(luma,1.))
        tinted = rgb*scale[:,:,None]*np.array([.96,1.00,.98],dtype="float32")
        derived = Image.fromarray(np.rint(tinted).clip(0,255).astype("uint8"))
    output = textures / f"{NAME}_{label}.png"
    derived.save(output,compress_level=5)
    extrema = list(derived.getextrema())
    distribution = [list(v) for v in np.quantile(np.asarray(derived).reshape(-1,3), [.05,.5,.95],axis=0)]
    report[label] = {
        "file": output.relative_to(WORK).as_posix(),
        "sourceName": image_ref.get("name",""),
        "sourcePngSHA256": hashlib.sha256(data).hexdigest(),
        "derivedPngSHA256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "sourceDimensions": source_dimensions,
        "derivedDimensions": [2048,2048],
        "process": ("Compressed bright source contamination using an ink lightness curve"
                    if NAME=="DetachableSegment" and label=="BaseColor" else "Resampled embedded PNG"),
        "outputChannel5th50th95thPercentiles": distribution
    }
(WORK/f"{NAME}-Maps.json").write_text(json.dumps(report,indent=2),encoding="utf-8")

