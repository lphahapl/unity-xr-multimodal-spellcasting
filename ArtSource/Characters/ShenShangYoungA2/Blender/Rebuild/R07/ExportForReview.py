"""Run in Blender with the R07 blend open. Pass a new .glb path after -- ."""
import json
from pathlib import Path
import struct
import sys

import bpy

def export(output):
    if output.suffix.lower() != '.glb' or output.exists():
        raise ValueError('Choose a new .glb output path.')
    character=bpy.data.collections['R07 | Character']
    bpy.ops.object.select_all(action='DESELECT')
    for part in character.objects:
        part.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(output),export_format='GLB',use_selection=True,
                              export_animations=False,export_skins=False,export_tangents=True,export_yup=True)
    raw=output.read_bytes()
    json_length=struct.unpack_from('<I',raw,12)[0]
    description=json.loads(raw[20:20+json_length])
    binary_chunk=raw[20+json_length:]
    for material in description['materials']:
        authored=bpy.data.materials[material['name']]
        if 'A2LinearTint' not in authored:
            continue
        surface=material['pbrMetallicRoughness']
        rgba=surface.get('baseColorFactor',[1,1,1,1])
        surface['baseColorFactor']=[float(tint)*value for tint,value in zip(authored['A2LinearTint'],rgba[:3])]+[rgba[3]]
    payload=json.dumps(description,ensure_ascii=False,separators=(',',':')).encode('utf8')
    payload+=b' '*(-len(payload)%4)
    header=struct.pack('<4sIIII',b'glTF',2,20+len(payload)+len(binary_chunk),len(payload),0x4e4f534a)
    output.write_bytes(header+payload+binary_chunk)

if __name__=='__main__':
    export(Path(sys.argv[sys.argv.index('--')+1]))
