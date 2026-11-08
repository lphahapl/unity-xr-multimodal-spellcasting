import bpy
import hashlib
import json
from pathlib import Path
import numpy as np

root = Path(__file__).parent
downloads = Path.home() / 'Downloads'
sources = sorted([*downloads.glob('Standing*.fbx'), downloads / 'Walking.fbx', downloads / 'Dying.fbx', downloads / 'Sitting Dazed.fbx'])
report = []
for file in sources:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(file), use_image_search=False)
    scene = bpy.context.scene
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    action = rig.animation_data.action
    first, last = [int(x) for x in action.frame_range]
    motion = []
    samples = []
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        positions = [list(rig.matrix_world @ bone.head) for bone in rig.pose.bones]
        rotations = [list(bone.rotation_quaternion) for bone in rig.pose.bones]
        motion.append([positions, rotations])
        if frame in [first, (first + last) // 2, last]:
            samples.append({'frame': frame, 'positions': positions})
    motion_bytes = json.dumps(motion).encode()
    entry = dict(source=file.name, bytes=file.stat().st_size, sha256=hashlib.sha256(file.read_bytes()).hexdigest(),
        fps=scene.render.fps, first_frame=first, last_frame=last,
        duration=(last-first)/scene.render.fps, bones=[b.name for b in rig.data.bones],
        parents=[b.parent.name if b.parent else None for b in rig.data.bones],
        meshes=[{'name': o.name, 'vertices': len(o.data.vertices)} for o in scene.objects if o.type == 'MESH'],
        motion_sha256=hashlib.sha256(motion_bytes).hexdigest(), samples=samples)
    report.append(entry)
    (root / 'sources.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('AUDIT', file.name, first, last, entry['duration'], entry['motion_sha256'], flush=True)
