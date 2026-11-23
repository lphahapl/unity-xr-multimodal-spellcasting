import sys,bpy
from pathlib import Path
sys.path.insert(0,str(Path.home()/".codex/skills/blender-asset-qa/scripts"))
import review_shots
out=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work")
for mode in ["wire","random"]:
    for kind,names in [("Dial",["Bed","Outer","Inner"]),("Casket",["Box","Lid"])]:
        review_shots.run(objects=names,out=str(out/(kind+"_"+mode+".png")),views=["front","right","top","three_quarter"],mode=mode,res=320)


