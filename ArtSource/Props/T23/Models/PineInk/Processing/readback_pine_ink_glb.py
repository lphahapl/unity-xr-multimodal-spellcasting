"""Read the finished GLB buffers and verify the single viewer-synchronized sequence."""
import bisect
import hashlib
import json
import math
import struct
from pathlib import Path

WORK=Path(__file__).resolve().parent
GLB=WORK/"PineInk-Review.glb"
with GLB.open("rb") as file:
    magic,ver,length,size,chunk=struct.unpack("<4sIIII",file.read(20))
    assert magic==b"glTF" and ver==2 and length==GLB.stat().st_size and chunk==0x4E4F534A
    gltf=json.loads(file.read(size))
    size,chunk=struct.unpack("<II",file.read(8))
    assert chunk==0x004E4942
    buffer=file.read(size)
    assert len(buffer)==size and file.read()==b""

assert len(gltf["animations"])==1
clip=gltf["animations"][0]
assert clip["name"]=="推开、折段、收套"
assert sorted(node.get("name") for node in gltf["nodes"])==["DetachableSegment","SlideSleeve","WrappedCore"]
assert len(gltf["meshes"])==3
expected={
    ("SlideSleeve","translation"),
    ("DetachableSegment","translation"),
    ("DetachableSegment","rotation"),
    ("DetachableSegment","scale")
}


def read_accessor(index):
    accessor=gltf["accessors"][index]
    assert accessor["componentType"]==5126
    num={"SCALAR":1,"VEC3":3,"VEC4":4}[accessor["type"]]
    view=gltf["bufferViews"][accessor["bufferView"]]
    offset=view.get("byteOffset",0)+accessor.get("byteOffset",0)
    stride=view.get("byteStride",num*4)
    records=[struct.unpack_from("<"+"f"*num,buffer,offset+i*stride) for i in range(accessor["count"])]
    return records


curves={}
static={}
for channel in clip["channels"]:
    target=channel["target"]
    node=gltf["nodes"][target["node"]]
    key=(node["name"],target["path"])
    sampler=clip["samplers"][channel["sampler"]]
    assert sampler.get("interpolation","LINEAR")=="LINEAR"
    times=[t[0] for t in read_accessor(sampler["input"])]
    values=read_accessor(sampler["output"])
    assert len(times)==len(values)
    assert all(b>a for a,b in zip(times,times[1:]))
    curves[key]=(times,values)
    static[key]={"sampleCount":len(times),"firstSecond":times[0],"lastSecond":times[-1]}
assert set(curves)==expected


def sample(key,time):
    times,values=curves[key]
    idx=bisect.bisect_left(times,time)
    if idx==0:return values[0]
    if idx==len(times):return values[-1]
    weight=(time-times[idx-1])/(times[idx]-times[idx-1])
    return tuple(a*(1.-weight)+b*weight for a,b in zip(values[idx-1],values[idx]))


start=min(times[0] for times,_ in curves.values())
keyframes=clip["extras"]["timeline"]
states={}
for marker in keyframes:
    time=marker["seconds"]+start
    states[marker["name"]]={
        "timeSeconds":time,
        "slideSleeveYMetres":sample(("SlideSleeve","translation"),time)[1],
        "segmentTranslationMetres":sample(("DetachableSegment","translation"),time),
        "segmentRotationXyzw":sample(("DetachableSegment","rotation"),time),
        "segmentScale":sample(("DetachableSegment","scale"),time)[0]
    }
assert abs(states["收合"]["slideSleeveYMetres"])<1e-5
assert abs(states["推开"]["slideSleeveYMetres"]-.031)<5e-5
assert abs(states["掰下"]["slideSleeveYMetres"]-.031)<5e-5 and states["掰下"]["segmentTranslationMetres"][0]>.015
assert sum(value*value for value in states["掰下"]["segmentRotationXyzw"][:3])>.10
assert abs(states["收套"]["slideSleeveYMetres"]-.004)<5e-5 and states["收套"]["segmentScale"]<.003
report={
    "file": GLB.name,
    "sha256":hashlib.sha256(GLB.read_bytes()).hexdigest(),
    "binSHA256":hashlib.sha256(buffer).hexdigest(),
    "animatedClipName":clip["name"],
    "channelEvidence":{"/".join(key):value for key,value in static.items()},
    "states":states,
    "independentMeshNodeCount":len(gltf["nodes"]),
    "passes":True
}
(WORK/"PineInk-Review-GLBReadback.json").write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding="utf-8")
print(json.dumps(report,ensure_ascii=False))

