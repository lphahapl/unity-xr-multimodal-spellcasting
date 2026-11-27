"""Measure a production Unity BakeMesh cycle in actor-local metres.

Inputs follow Chapter02CarryPoseSampler's manifest/mesh/weights layout, with one
stone pose per sample (frame,x,y,z,qx,qy,qz,qw,...). The actor looks toward +Z.
Set PYTHONPATH for the same python-fcl build used by Chapter02BallastSceneClearance.
"""
import argparse
import csv
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path

import numpy as np
from scipy.spatial.transform import Rotation


ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'Tools/Validation'))
from Chapter02BallastSceneClearance import distance, fcl, load_obj, mesh


def extent_mm(values):
    return [round(float(np.min(values)) * 1000, 2), round(float(np.max(values)) * 1000, 2)]


def measure_feet(vertices, feet, times, speed):
    result = {}
    for side, indices in feet.items():
        world = vertices[:, indices].copy()
        world[:, :, 2] += (times * speed)[:, None]
        bottom = np.min(world[:, :, 1], axis=1)
        # The authored left stance starts at phase zero, the right at phase 0.5.
        # The stone-carrier spends 60% of a cycle on each foot. During swing a
        # near-ground foot can move quickly just before contact; count only stance.
        phase=np.mod(times / times[-1] + (0 if side=='Left' else .5),1)
        planted=phase <= .600001
        sole=np.min(world[planted,:,1])
        low=(world[:,:,1] <= sole+.025) & planted[:,None]
        start = None
        movement = []
        patches = []
        for i in range(len(world) - 1):
            # Use the same samples on both frames, so toe-off cannot masquerade as slip.
            contact = low[i] & low[i+1]
            active = contact.sum() >= 3
            if active:
                if start is None:
                    start = i
                delta = world[i+1, contact][:, [0, 2]] - world[i, contact][:, [0, 2]]
                movement.append(np.median(delta, axis=0))
            if start is not None and (not active or i == len(world)-2):
                end = i+1 if active else i
                travel = np.cumsum(np.vstack([np.zeros(2), movement]), axis=0)
                fixed_patch = low[start:end+1].all(axis=0)
                # Track persistent sole vertices, also reporting the worst local
                # skin motion that the median stride estimate can hide.
                contact_vertices = world[start:end+1, fixed_patch][:,:, [0,2]].astype(float)
                spans = np.linalg.norm(np.ptp(contact_vertices,axis=0),axis=1)
                patches.append(dict(frames=[int(start),int(end)],
                    seconds=round(float(times[end]-times[start]),3),
                    persistent_vertices=int(len(spans)),
                    worst_sole_vertex_horizontal_span_mm=(round(1000*float(spans.max()),3)
                                                         if len(spans) else None),
                    accumulated_horizontal_slip_mm=round(1000*float(
                        np.linalg.norm(travel.max(axis=0)-travel.min(axis=0))),2)))
                start = None
                movement = []
        result[side] = dict(lowest_sole_height_mm=extent_mm(bottom),
                            sole_height_during_authored_stance_mm=extent_mm(bottom[planted]),
                            stance_intervals=patches)
    return result


def audit(capture, poses, output, speed):
    m = json.loads((capture/'manifest.json').read_text(encoding='utf-8-sig'))
    vertices = np.fromfile(capture/'vertices.f32', '<f4').reshape(m['frames'], m['vertices'],3)
    bones = np.fromfile(capture/'bones.f32', '<f4').reshape(m['frames'],m['bones'],7)
    tris = np.fromfile(capture/'triangles.i32', '<i4').reshape(-1,3)
    names = [n.split(':')[-1] for n in m['boneNames']]
    weights = np.zeros((m['vertices'], len(names)))
    with (capture/'weights.csv').open(encoding='utf-8-sig') as file:
        for v in csv.DictReader(file):
            weights[int(v['vertex']),int(v['bone'])] += float(v['weight'])
    positions = np.loadtxt(poses,delimiter=',',skiprows=1)
    if positions.shape[0] != len(vertices):
        raise ValueError('Pose and mesh samples must correspond frame by frame')
    if positions.shape[1] >= 11 and not np.allclose(positions[:,8:11],1.):
        raise ValueError('Stone transform includes non-unit scale')
    feet = {s: np.flatnonzero(weights[:,[i for i,n in enumerate(names)
                                  if n.startswith(s) and ('Foot' in n or 'Toe' in n)]].sum(axis=1)>.7)
            for s in ('Left','Right')}
    torso_bones = [i for i,n in enumerate(names) if n.startswith('Spine') or n.endswith('Shoulder')]
    dominant = np.argmax(weights[tris].mean(axis=1),axis=1)
    hand = weights[:,names.index('RightHand')]>.95
    hand &= np.linalg.norm(vertices[0]-bones[0,names.index('RightHand'),:3],axis=1)>.19
    head = weights[:,[i for i,n in enumerate(names) if 'Head' in n or n=='Neck']].sum(axis=1)>.60
    subsets = {'body':tris,'back_and_shoulders':tris[np.isin(dominant,torso_bones)],
               'head_and_neck':tris[np.all(head[tris],axis=1)],
               'right_hand_long_tool':tris[np.any(hand[tris],axis=1)]}
    stone_file = ROOT/'SharedWorkSpace/results/T25-A4/Model/T25-A4-stone-local.obj'
    sv, st, _ = load_obj(stone_file)
    stone = mesh(sv,st)
    contact = {name:[] for name in subsets}
    hit = {name:[] for name in subsets}
    collisions_by_bone=Counter()
    for i,frame in enumerate(vertices):
        stone.setTransform(fcl.Transform(Rotation.from_quat(positions[i,4:8]).as_matrix(),positions[i,1:4]))
        for name, faces in subsets.items():
            if not len(faces):
                raise ValueError(f'No triangles in region {name}')
            actor = mesh(frame, faces)
            contact[name].append(distance(actor,stone)[0])
            pairs=fcl.CollisionResult()
            detail=(name=='body')
            request=fcl.CollisionRequest(num_max_contacts=2000 if detail else 1,enable_contact=detail)
            if fcl.collide(actor,stone,request,pairs):
                hit[name].append(i)
                if detail:
                    collisions_by_bone.update(names[dominant[j]] for j in {c.b1 for c in pairs.contacts})
    times = np.linspace(0,m['clipLength'],len(vertices))
    result = dict(clip=m['clip'],frames=m['frames'],seconds=m['clipLength'],speed_m_s=speed,
        geometry_samples=int(vertices.shape[0]*vertices.shape[1]),
        a4_sha256=hashlib.sha256(stone_file.read_bytes()).hexdigest(),
        behind_upper_spine_mm=extent_mm(bones[:,names.index('Spine2'),2]-positions[:,3]),
        seam_worst_mesh_vertex_mm=round(1000*float(np.linalg.norm(vertices[-1]-vertices[0],axis=1).max()),2),
        stone_seam_mm=round(1000*float(np.linalg.norm(positions[-1,1:4]-positions[0,1:4])),2),
        stone_surface_distance_mm={k:extent_mm(v) for k,v in contact.items()},
        intersecting_frames=hit, collided_actor_triangle_frames_by_bone=collisions_by_bone,
        feet=measure_feet(vertices,feet,times,speed))
    output.write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('capture','poses','output'):
        parser.add_argument(name,type=Path)
    parser.add_argument('--speed',type=float,required=True)
    args=parser.parse_args()
    print(json.dumps(audit(**vars(args)),indent=2))
