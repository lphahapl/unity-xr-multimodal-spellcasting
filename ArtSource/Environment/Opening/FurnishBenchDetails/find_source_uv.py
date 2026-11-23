"""Locate reusable continuous islands in the pavilion baked linen and cabinet-top UVs."""
import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parent
result={}
for category,obname in [('linen','BenchDressing'),('elm','ArcCabinet')]:
    mesh=bpy.data.objects[obname].data; mesh.calc_loop_triangles()
    chosen=[]
    for tri in mesh.loop_triangles:
        co=[mesh.vertices[n].co for n in tri.vertices]
        z=sum(p.z for p in co)/3
        # Work in asset-local coordinates; the QA .blend displays meshes side by side.
        if tri.normal.z<.88:continue
        if category=='linen' and not (.002<z<.018 and all(p.y>-.31 and abs(p.x)<.70 for p in co)):continue
        if category=='elm' and not (.826<z<.834):continue
        chosen.append([list(mesh.uv_layers.active.data[l].uv) for l in tri.loops])
    assert chosen,(category,obname)
    result[category]=chosen
(root/'source-uv-triangles.json').write_text(json.dumps(result,separators=(',',':')))
