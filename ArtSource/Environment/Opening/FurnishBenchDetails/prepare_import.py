from pathlib import Path
import re,shutil,uuid,sys
iso=Path(sys.argv[1])
root=Path('Assets/Art/Environment/Opening')
stage=Path('D:/Temp/FurnishBenchDetails_bake')
out=iso/root/'FurnishBenchDetails';out.mkdir(parents=True,exist_ok=True)
def guid(name):return uuid.uuid5(uuid.NAMESPACE_DNS,'NarutoVR.FurnishBenchDetails.'+name).hex
def meta(src,name):
    return re.sub(r'^guid: [0-9a-f]+','guid: '+guid(name),src.read_text(),count=1,flags=re.M)
Path(str(out)+'.meta').write_text(meta(root/'FurnishPavilionB.meta','FurnishBenchDetails'))
template=root/'FurnishPavilionB/ArcCabinet_LOD0.fbx.meta'
for src in stage.glob('*_LOD?.fbx'):
    shutil.copyfile(src,out/src.name)
    (out/(src.name+'.meta')).write_text(meta(template,src.name))
