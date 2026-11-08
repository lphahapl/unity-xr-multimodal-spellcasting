"""Apply the project's validated URP/Android import template; keep existing asset GUIDs on rebuild."""
from pathlib import Path
import re
import uuid

root=Path(__file__).resolve().parents[4]/"Assets/Art/Environment/Opening"
base=root/"Interior"
dest=root/"WakeCot"
dest.mkdir(exist_ok=True)
(dest/"Textures").mkdir(exist_ok=True)
guid_pattern=r"(?m)^guid: ([a-f0-9]{32})"
def guid(path):
    return re.search(guid_pattern,path.read_text(encoding="utf-8"))[1]
def reuse(source,target):
    target_guid=guid(target) if target.exists() else uuid.uuid5(uuid.NAMESPACE_URL,"NarutoVR/Opening/"+target.relative_to(root).as_posix()).hex
    text=source.read_text(encoding="utf-8").replace(guid(source),target_guid,1)
    target.write_text(text,encoding="utf-8")
    return target_guid

reuse(base.with_suffix(".meta"),dest.with_suffix(".meta"))
reuse(base/"Textures.meta",dest/"Textures.meta")
mat=(base/"GaugeWorkshop_Baked.mat").read_text(encoding="utf-8").replace("GaugeWorkshop_Baked","WakeCot_Baked")
mat=mat.replace("  - _EMISSION\n","").replace("m_LightmapFlags: 2","m_LightmapFlags: 4")
mat=mat.replace("{r: 0.3, g: 0.3, b: 0.3, a: 1}","{r: 0, g: 0, b: 0, a: 1}")
mat=mat.replace("{fileID: 2800000, guid: "+guid(base/"Textures/GaugeWorkshop_Emission.png.meta")+", type: 3}","{fileID: 0}")
for key in ["BaseColor","Normal","Mask"]:
    src=base/f"Textures/GaugeWorkshop_{key}.png.meta"
    inguid=reuse(src,dest/f"Textures/WakeCot_{key}.png.meta")
    mat=mat.replace(guid(src),inguid)
(dest/"WakeCot_Baked.mat").write_text(mat,encoding="utf8")
new_mat_guid=reuse(base/"GaugeWorkshop_Baked.mat.meta",dest/"WakeCot_Baked.mat.meta")
fbx=dest/"WakeCot.fbx.meta"
reuse(base/"TargetTrestle.fbx.meta",fbx)
text=fbx.read_text(encoding="utf8").replace("GaugeWorkshop_Baked","WakeCot_Baked")
fbx.write_text(text.replace(guid(base/"GaugeWorkshop_Baked.mat.meta"),new_mat_guid),encoding="utf8")
