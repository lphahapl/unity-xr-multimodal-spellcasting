import json,hashlib,shutil
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np
q=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work")
assets=Path(r"D:/UnityProjects/NarutoVR/Assets/Art/Environment/Opening/ConcentricGauge/Bronze")
out=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze")/"QA"
def read(name):return json.loads((q/name).read_text())
qa=read("qa.json"); fit=read("fit.json"); mesh=read("current.json")
assert mesh==read("previous.json")
before={"Bed.fbx.meta":"7f9a57c156dda3b4c18846a2044384be3a0126f306aa1a8836bf65d59d6470aa","Box.fbx.meta":"e678ffe53809c39461b9ac04c5e5caf3e4e10d90db10532d77edf2ad3a547f3c","CG_A1_BaseColor.png.meta":"f3a3e8f59950035f599e9abc31c484f37c698dc2163a948ebfbd90434890b247","CG_A1_Bronze.mat.meta":"7a9c7354ec9682f373e3b457b4e32b8422c54d1a0ddeb54772b119b1d82a470b","CG_A1_MetalSmooth.png.meta":"c4463b46d33dc88199443adbe2ff4cb83b48f0697f2b56c001f0be0a9a734705","CG_A1_Normal.png.meta":"ed8917f7d493c9502c6fafd1a43704476d2e6c77f001ecdb228bcc091d7bb345","Inner.fbx.meta":"93ac02d1c379b95a30c6e1748f5ce1dbb6520e0ae593a7d116003e91153c7804","Lid.fbx.meta":"7fbeda7ffb3a1021a16ec4d4aff9fb15c21477a8bd0de39d2fc01adf0c990869","Outer.fbx.meta":"7269838dc88237949a44018fa5dd1a65274c257c238983c9d9dbd26dc86e6905","CG_A1_Bronze.mat":"301da3ecc4095f8f28ef4f3dfbdb9ffaa334f62582c0d552ca7d4831b044a791","Bronze.meta":"e1645356d30e575c15618344dda504a9ea1846c04e20a81d8cb01d59c281deee"}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for n,h in before.items():
    p=assets/n if n!="Bronze.meta" else assets.with_suffix(".meta")
    if n=="CG_A1_Bronze.mat" and sha(p)!=h:
        reference=Path(r"D:/Temp/CG_Bronze_20261120_QA/Assets/Bronze/CG_A1_Bronze.mat")
        assert sha(reference)==h
        assert p.read_text().replace("  - _EMISSION\n","")==reference.read_text()
        assert "_EmissionColor: {r: 0, g: 0, b: 0, a: 1}" in p.read_text()
    else:
        assert sha(p)==h,n
geometry=np.load(q/"triangles.npz")
painted=np.load(q/"painted.npz")
assert all(np.array_equal(geometry[k],painted[k]) for k in geometry.files)
assert all(v["pass"] and v["performance"]["budget"]["pass"] for v in qa.values())
counts={n:g["performance"]["total_tris"] for n,g in qa.items()}
assert counts=={n:g["triangles"] for n,g in fit.items()}
assert 4800<=sum(counts.values())<=6000
assert not list(out.parent.glob("*.blend1"))
assert int((q/"sweep.txt").read_text().split()[0])==1080
image=Image.new("RGB",(1280,1326),"#303030")
draw=ImageDraw.Draw(image)
for i,(title,stem) in enumerate([("Gauge: separate moving parts","Dial_random"),("Gauge: wire","Dial_wire"),("Casket: body and removable cover","Casket_random"),("Casket: wire","Casket_wire")]):
    x=i%2*640;y=i//2*663
    draw.text((x+10,y+5),title)
    image.paste(Image.open(q/(stem+".png")).convert("RGB"),(x,y+23))
image.save(out/"Topology.png")
for n in ["Assembled.png","Open.png"]:shutil.copyfile(q/n,out/n)
report={"blender":"5.2.2 LTS","unit":"metre","triangles":counts,"bronze_total":sum(counts.values()),"stone_total_user_provided":2954,"combined_total":sum(counts.values())+2954,
"assembly_world_y":{"Bed":.99,"Outer":.99,"Inner":1.008,"Box":.90,"Lid":1.036},"blender_game_profile":qa,
"accepted_warnings":{"Bed":"The foot is 20 mm below its fixed pivot to seat on the stone at Y 0.970 m.","Lid":"The flange extends 35 mm below its closing pivot at Y 1.036 m."},
"rotational_sweep":(q/"sweep.txt").read_text(),"fbx_geometry_and_blender_surface_measurements":fit,"fbx_names_checked_against_before":mesh,
"unity_replay":"Reserved for integration. All positions above come from FBX control points or Blender surface tests.",
"material":"CG_A1_Bronze (URP/Lit)","all_meta_byte_identical":True,"material_check":"Original shader, GUID and all texture/parameter bindings. Concurrent serialization added _EMISSION to valid keywords; emission color remains black.",
"atlas":{"dimensions":[2048,2048],"CG_A1_BaseColor":"sRGB _BaseMap","CG_A1_MetalSmooth":"linear _MetallicGlossMap R metallic A smoothness","CG_A1_Normal":"tangent-space _BumpMap","bake_input_matches_final_geometry_and_uv":True},
"files_sha256":{p.name:sha(p) for p in sorted(assets.iterdir()) if p.is_file()},
"source_and_review_sha256":{p.name:sha(p) for p in sorted([*out.parent.glob("*.py"),*out.parent.glob("*.blend"),*out.glob("*.png")])}}
(out/"Bronze_QA.json").write_text(json.dumps(report,indent=2))
print(counts,report["bronze_total"],report["combined_total"])


