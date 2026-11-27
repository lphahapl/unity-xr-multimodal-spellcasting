"""Shift warm high-luminance hemp toward the approved subdued grey-green."""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

work = Path(__file__).resolve().parent
atlas = work / "WrappedCoreTextures"
source = atlas / "WrappedCore_BaseColor.png"
output = atlas / "WrappedCore_ReviewGreyGreen.png"
rgb = np.asarray(Image.open(source).convert("RGB"), dtype="float32")
luminance = rgb @ np.array([.2126, .7152, .0722], dtype="float32")
strength = np.clip((luminance - 55) / 54, 0, 1)
strength = strength * strength * (3 - 2 * strength)
gain = 1 - strength[:, :, None] * np.array([.19, .10, .06], dtype="float32")
result = np.rint(rgb * gain).clip(0,255).astype("uint8")
Image.fromarray(result).save(output,compress_level=5)
report = {
    "source": source.name,
    "output": output.name,
    "sourceSHA256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "derivedSHA256": hashlib.sha256(output.read_bytes()).hexdigest(),
    "size": [2048,2048],
    "method": "Per-pixel continuous lightness mask, peak RGB changes -19/-10/-6 percent. Dark ink below sampled 55 preserved exactly. Same source UV, normal, roughness and geometry."
}
(work / "WrappedCore-ReviewTint.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
