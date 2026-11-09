EXTERIOR-WORLD-LOD-20261109-MATERIAL
Model: GPT-6; exact service variant not exposed. Worker entry: Codex desktop.

M01_QingHuiStrata: blue-grey weathered sedimentary/slate rock, 2 m per UV tile.
M02_MossScree: desaturated grey-olive moss and slate scree, 1.5 m per UV tile.

Both base images were newly generated with the built-in OpenAI image_gen tool,
using text-only prompts in generation-prompts.json. Tool calls completed on
2026-11-09 Asia/Shanghai. Exact image model/version is not exposed by the tool.
Original generated PNGs are retained beside this file.

Local reuse audit: D:/BaiduNetdiskDownload contained Battle Sound Library and
Epic Toon FX/Master MagicFX packages. The Rock.png and RockMiddle.png candidates
under Assets/Imported/MasterMagicFX are a particle mesh atlas and a UV wireframe,
not a usable tileable PBR ground set. Neither was copied or sampled into outputs.
No third-party texture, Rodin model, Hunyuan model or external photo was used.

Rights/source record: AI-generated output for this project; not CC0 or a scanned
material. Applicable account terms govern output. OpenAI's Terms of Use, effective
2026-01-01, address user ownership of output as between user and OpenAI, to the
extent allowed by law, and state that outputs may not be unique.
Verified official source on 2026-11-09:
https://openai.com/policies/terms-of-use/
This record does not claim exclusive copyright or a third-party asset licence.

Rebuild with Python 3 + numpy + Pillow:
  python ArtSource/Environment/Opening/SurfaceMaterialsLOD/build_materials.py
All build outputs stay inside the three delegated directories.

Processing: Lanczos downsample 1254x1254 to 1024x1024; discrete periodic/smooth
decomposition removes low-frequency boundary discontinuities while retaining
the generated stone structures. No procedural noise is substituted for rock.
Wrapped filtering and central differences derive a shallow tangent-space normal
from local luminance; cavity and microstructure estimates derive AO/smoothness.
These maps are estimates from color, NOT high-poly bakes, photogrammetry, measured
roughness or guaranteed reconstruction of real stone depth. Derived 16-bit heights
are source evidence only and are not imported into the Unity runtime folder.

Runtime: exactly two URP/Lit materials and six 1024 PNGs, plus their .meta files.
BaseColor RGB sRGB; Normal RGB tangent +Y with Unity NormalMap importer;
Mask RGBA linear = R metallic 0, G AO, B 0, A smoothness. Mask is wired to both
_MetallicGlossMap and _OcclusionMap; _Smoothness=1 reads its alpha. No emission,
transparency, displacement, parallax or new shader. Normal strength .75/.65,
AO strength .65. UV Repeat, trilinear, mipmaps enabled, aniso 2, non-readable.
Android explicitly ASTC 6x6 / 1024 / quality 100; desktop uses existing auto format.
Estimated ASTC storage with complete mip chains: 3,757,728 bytes (~3.584 MiB)
for all six textures, excluding driver/asset overhead. This is not a GPU timing test.

The worker did not start Unity or control a browser and did not modify any existing
scene/material/global setting/handoff. Parent owns scene application, Unity import,
near/distant lighting and ASTC inspection, Quest/PICO performance acceptance and
final handoff records. Parent/Unity must own SurfaceMaterialsLOD.meta at the parent
directory level, which is outside this worker's exact write scope.

Evidence: Logs/exterior-world-lod-20261109/material-delegation/
material-manifest.json has exact GUIDs, parameters, source and runtime hashes.
*_Tiling2x2.png are square tiling checks. *_Review.png are offline material sheets,
including a labelled synthetic light swatch, not Unity screenshots.
