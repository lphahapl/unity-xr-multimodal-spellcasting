from pathlib import Path
from hashlib import sha256
import json
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
report={}
for name in ("Stone","Bronze"):
    metrics={}
    maps={}
    for k in ("BaseColor","NormalGL","Roughness","Metallic","ORM"):
        file=root/name/f"{name}_{k}.png"
        im=np.asarray(Image.open(file))
        assert im.shape[:2]==(2048,2048)
        a=im.astype(np.float32)
        # 周期边界应像一般相邻像素，不强迫最外两列复制成一条平线。
        avg=lambda q:float(np.mean(np.abs(q)))
        boundaryX=avg(a[:,0]-a[:,-1]); boundaryY=avg(a[0]-a[-1])
        dx=avg(a[:,1:]-a[:,:-1]); dy=avg(a[1:]-a[:-1])
        ratioX=boundaryX/max(dx,.001); ratioY=boundaryY/max(dy,.001)
        assert max(ratioX,ratioY)<2.15, (name,k,ratioX,ratioY)
        metrics[k]=dict(sha256=sha256(file.read_bytes()).hexdigest(), dimensions=list(im.shape[:2]),
            wrapNeighborRatioX=round(ratioX,3),wrapNeighborRatioY=round(ratioY,3))
        maps[k]=im
    assert np.array_equal(maps["Roughness"],maps["ORM"][:,:,1])
    assert np.array_equal(maps["Metallic"],maps["ORM"][:,:,2])
    normal=maps["NormalGL"].astype(np.float32)/127.5-1
    maxError=float(np.max(np.abs(np.linalg.norm(normal,axis=2)-1)))
    assert maxError<.009 and int(maps["NormalGL"][:,:,2].min())>126
    if name=="Stone": assert np.all(maps["Metallic"]==0)
    report[name]={"maps":metrics, "ORMMatchesSingles":True, "maxNormalLengthError":round(maxError,5)}
(root/"Source"/"map-checks.json").write_text(json.dumps(report,indent=2),encoding="utf8")
print("10 tileable 2048px maps passed; ORM G/B match roughness/metallic; normal length verified.")
