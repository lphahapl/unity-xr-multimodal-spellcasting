import bpy,sys,json
from pathlib import Path
q=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze/.work")
src=Path(r"D:/UnityProjects/NarutoVR/ArtSource/Environment/Opening/ConcentricGauge/Bronze")
for name in ["blender-asset-qa","blender-game-optimization"]:
    sys.path.insert(0,str(Path.home()/".codex/skills"/name/"scripts"))
import qa_check,mesh_stats
names=["Bed","Outer","Inner","Box","Lid"]
report={}
for name in names:
    ob=bpy.data.objects[name]
    # Local pivot is an assembly datum. Test in its rest coordinates, independent of layout.
    ob.location=(0,0,0)
    bpy.context.view_layer.update()
    report[name]=qa_check.run(objects=[name],profile="game",eps=.00015,tol=.0012)
    report[name]["performance"]=mesh_stats.run(objects=[name],budget="vr")
    print(name,report[name]["pass"],report[name]["errors"],report[name]["warnings"],flush=True)
(q/"qa.json").write_text(json.dumps(report,indent=2))


