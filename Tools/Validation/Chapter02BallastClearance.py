"""T25 offline mesh clearance in Unity actor-local metres. Requires numpy, scipy, python-fcl.

Input is Chapter02CarryPose's BakeMesh export. Pose parameters are rotation about
actor X/Y/Z (scipy extrinsic xyz degrees) and XYZ centre offset from the wrist midpoint.
"""
import argparse
import csv
import json
from pathlib import Path

import numpy as np
from scipy.spatial.transform import Rotation


STONE = [(0., (.5075, .5075, .5075)), (-.28, (.525, .0475, .525)),
         (.28, (.525, .0475, .525))]


def prepare(vertices, triangles, left, right, left_weight, right_weight):
    # Palm spans the proximal 16 cm, keeping the rigid extension out of the support metric.
    ld = np.linalg.norm(vertices[0] - left[0], axis=1)
    rd = np.linalg.norm(vertices[0] - right[0], axis=1)
    masks = dict(left=(left_weight >= .85) & (ld <= .16),
                 right=(right_weight >= .85) & (rd <= .16),
                 extension=(right_weight >= .95) & (rd >= .19))
    mid = (left + right) / 2
    v = vertices - mid[:, None]
    return v, triangles, masks, mid


def box_sdf(points):
    best = None
    for height, half in STONE:
        d = np.abs(points - [0, height, 0]) - half
        distance = np.linalg.norm(np.maximum(d, 0), axis=-1) + np.minimum(d.max(axis=-1), 0)
        best = distance if best is None else np.minimum(best, distance)
    return best


def optimize(vertices, triangles, masks, forward_limit=.86, long_support=False):
    from scipy.optimize import differential_evolution, minimize
    # Deduplicate coincident seam vertices. Use a small sample for discovery, then audit all triangles.
    _, unique = np.unique(vertices[0].round(6), axis=0, return_index=True)
    ids = unique[vertices[0, unique, 1] > -.45]
    frames = [0, 15, 30, 45, 61, 76]
    whole = vertices[frames][:, ids]
    palms = [vertices[frames][:, masks[k]] for k in ('left', 'right')]
    if long_support:
        # Positive clearance at the distal end, as a third possible support beyond the stone centre.
        distal = masks['extension'] & (vertices[0,:,2] > .40)
        palms.append(vertices[frames][:,distal])
    def objective(q):
        rot = Rotation.from_euler('xyz', q[:3], degrees=True).as_matrix()
        local = (whole - q[3:]) @ rot
        # Fast conservative rectangular envelope (final audit uses the three visible solids).
        signed = (np.abs(local) - [.517, .5075, .517]).max(axis=2)
        worst = signed.min()
        penalty = 9 * max(0., .006 - worst)
        for palm in palms:
            p = (palm - q[3:]) @ rot
            # A hand supporting the underside should approach the bottom, within its footprint.
            gaps = np.maximum(np.abs(p[..., [0, 2]]) - .505, 0)
            h = np.abs(p[..., 1] + .5075)
            distances = np.sqrt((gaps**2).sum(axis=-1) + h*h).min(axis=1)
            penalty += distances.max() + distances.mean() * .3
        return penalty + .12 * abs(q[3]) + .12 * abs(q[5] - .25)
    bounds = [(-65, 35), (-48, 48), (-60, 60), (-.29, .29), (.18, .85), (.025, forward_limit)]
    best = differential_evolution(objective, bounds, seed=17, popsize=8, maxiter=47,
                                  polish=False, tol=.002)
    refined = minimize(objective, best.x, method='Nelder-Mead',
                       bounds=bounds, options=dict(maxiter=420, xatol=.00015))
    return refined.x


def triangle_sets(tri, masks):
    palm = {k: tri[np.all(mask[tri], axis=1)] for k, mask in masks.items() if k != 'extension'}
    # Include transition triangles crossing the proximal cut plane, conservatively.
    palm['extension'] = tri[np.any(masks['extension'][tri], axis=1)]
    return dict(all=tri, **palm)


def measure(vertices, triangles, masks, pose):
    import fcl
    sets = triangle_sets(triangles, masks)
    poses = np.broadcast_to(pose,(len(vertices),6))
    rows = []
    for i, verts in enumerate(vertices):
        pose = poses[i]
        rot = Rotation.from_euler('xyz', pose[:3], degrees=True).as_matrix()
        boxes = [fcl.CollisionObject(fcl.Box(*(2*np.array(half))),
                                     fcl.Transform(rot, pose[3:] + rot[:, 1] * y)) for y, half in STONE]
        row = dict(frame=i)
        for key, tris in sets.items():
            if len(tris) == 0:
                raise ValueError(f'empty region: {key}')
            model = fcl.BVHModel()
            model.beginModel(len(tris), len(verts))
            model.addSubModel(verts, tris)
            model.endModel()
            actor = fcl.CollisionObject(model)
            values = [fcl.distance(actor, box, fcl.DistanceRequest(), fcl.DistanceResult()) for box in boxes]
            row[key + '_gap_m'] = max(0., min(values))
            # Boolean triangle/solid intersection is independent of contact normal/penetration estimates.
            row[key + '_hit'] = int(any(fcl.collide(actor, box, fcl.CollisionRequest(),
                                                    fcl.CollisionResult()) for box in boxes))
        ext = np.unique(sets['extension'])
        local = (verts - pose[3:]) @ rot
        sdf = box_sdf(local[ext])
        j = np.argmin(sdf)
        row['extension_inside_vertex_m'] = max(0., -sdf[j])
        row['witness_vertex'] = int(ext[j])
        for k, coord in zip('xyz', local[ext[j]]):
            row['witness_stone_' + k] = coord
        # Both bounds are transformed to actor-local axis-aligned bounds.
        pts = verts[ext]
        corner = np.max([np.abs(rot) @ half + abs(y)*np.abs(rot[:,1]) for y,half in STONE], axis=0)
        row['extension_aabb_overlap'] = int(np.all(pts.min(0) <= pose[3:]+corner) and
                                            np.all(pts.max(0) >= pose[3:]-corner))
        rows.append(row)
    return rows


def last_vertical_contact(verts, triangles, rot, offset, solids=None):
    """Highest contact during a vertical sweep, exact SAT for each triangle vs three oriented boxes."""
    p = ((verts - [offset[0], 0, offset[2]]) @ rot)[triangles]
    edge = np.roll(p,-1,axis=1)-p
    cardinal = np.broadcast_to(np.eye(3),(len(p),3,3))
    normals = np.cross(edge[:,0],edge[:,1])[:,None]
    axes = np.concatenate([cardinal, normals, *(np.cross(edge,a) for a in np.eye(3))],axis=1)
    projection = np.einsum('tac,tkc->tak',axes,p)
    low, high = projection.min(2), projection.max(2)
    vertical_rate = np.einsum('tac,c->ta',axes,rot[1])
    parallel = np.abs(vertical_rate) < 1.e-12
    divisor = np.where(parallel,1.,vertical_rate)
    last = np.full(len(p),-np.inf)
    if solids is None:
        solids=[([0,height,0],half) for height,half in STONE]
    for center, half in solids:
        radius = np.einsum('tac,c->ta',np.abs(axes),half)
        projected_center=np.einsum('tac,c->ta',axes,center)
        l = low-radius-projected_center
        h = high+radius-projected_center
        a,b = l/divisor,h/divisor
        tmin = np.where(parallel,-np.inf,np.minimum(a,b)).max(1)
        tmax = np.where(parallel,np.inf,np.maximum(a,b)).min(1)
        possible = (tmin <= tmax) & ~np.any(parallel & ((l>1e-10)|(h < -1e-10)),axis=1)
        last = np.maximum(last,np.where(possible,tmax,-np.inf))
    return last


def follow_palms(data, params, long_support=False, solids=None):
    """Test whether modest continuous roll and height changes can keep the underside near both palms."""
    v, tri, masks, _ = data
    palm_sets = [tri[np.all(masks[key][tri],axis=1)] for key in ('left','right')]
    if long_support:
        distal = masks['extension'] & (v[0,:,2] > .40)
        palm_sets.append(tri[np.any(distal[tri],axis=1)])
    poses = np.tile(params,(len(v),1))
    for i,verts in enumerate(v):
        def contacts(pitch,roll):
            rot = Rotation.from_euler('xyz',[pitch,params[1],roll],degrees=True).as_matrix()
            return np.array([last_vertical_contact(verts,faces,rot,params[3:],solids).max() for faces in palm_sets])
        # Select the roll that lets the two hands reach the underside at similar heights.
        angles = params[2] + np.linspace(-8,8,33)
        if long_support:
            from scipy.optimize import least_squares
            def residual(par):
                h = contacts(*par)
                return np.clip(np.nan_to_num(h[1:]-h[0],nan=2.),-2.,2.)
            best=least_squares(residual,params[[0,2]],bounds=(params[[0,2]]-16,params[[0,2]]+16),
                               xtol=1.e-5,max_nfev=22)
            poses[i,[0,2]] = best.x
        else:
            errors = [abs(np.diff(contacts(params[0],a)).item()) for a in angles]
            poses[i,2] = angles[np.argmin(errors)]
    # Keep the angle continuous across the loop seam; frames zero and 86 are duplicate endpoints.
    for axis in (0,2):
        cycle = poses[:-1,axis]
        smooth = sum(w*np.roll(cycle,k) for w,k in zip([1,4,6,4,1],range(-2,3)))/16
        poses[:,axis] = np.r_[smooth,smooth[0]]
    for pose,verts in zip(poses,v):
        rot = Rotation.from_euler('xyz',pose[:3],degrees=True).as_matrix()
        pose[4] = last_vertical_contact(verts,tri,rot,pose[3:],solids).max() + .004
    return poses


def write_transforms(out,poses,mid):
    with (out/'candidate.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.writer(f)
        writer.writerow('frame x y z qx qy qz qw sx sy sz'.split())
        for i,q in enumerate(poses):
            writer.writerow([i,*(mid[i]+q[3:]),*Rotation.from_euler('xyz',q[:3],degrees=True).as_quat(),1,1,1])


def run(data, out, pose=None,follow=False, forward_limit=.86, long_support=False):
    v, tri, masks, mid = data
    out.mkdir(parents=True, exist_ok=True)
    p = np.array(pose) if pose is not None else optimize(v, tri, masks,forward_limit,long_support)
    print('CANDIDATE', p.tolist(), flush=True)
    (out/'pose.json').write_text(json.dumps({'euler_extrinsic_xyz_deg': p[:3].tolist(),
        'actor_axis_offset_from_wrist_midpoint_m': p[3:].tolist(),
        'quaternion_xyzw': Rotation.from_euler('xyz',p[:3],degrees=True).as_quat().tolist()},
        indent=2), encoding='utf-8')
    poses = follow_palms(data,p,long_support) if follow else np.broadcast_to(p,(len(v),6))
    write_transforms(out,poses,mid)
    rows = measure(v,tri,masks,poses)
    with (out/'frames.csv').open('w',newline='',encoding='utf-8') as f:
        writer = csv.DictWriter(f, rows[0]); writer.writeheader(); writer.writerows(rows)
    print({key:(min(r[key] for r in rows),max(r[key] for r in rows),sum(r[key] for r in rows)
                if key.endswith('_hit') or key.endswith('overlap') else None)
           for key in rows[0] if 'gap' in key or 'hit' in key or 'overlap' in key or 'inside' in key})


def load_capture(path):
    m = json.loads((path/'manifest.json').read_text(encoding='utf-8-sig'))
    verts = np.fromfile(path/'vertices.f32',dtype='<f4').reshape(m['frames'],m['vertices'],3).astype(float)
    tris = np.fromfile(path/'triangles.i32',dtype='<i4').reshape(-1,3)
    bones = np.fromfile(path/'bones.f32',dtype='<f4').reshape(m['frames'],m['bones'],7)
    wrists = [m['leftWrist'],m['rightWrist']]
    weights = np.zeros((2,m['vertices']))
    with (path/'weights.csv').open(encoding='utf-8-sig') as f:
        for row in csv.DictReader(f):
            b = int(row['bone'])
            if b in wrists:
                weights[wrists.index(b),int(row['vertex'])] += float(row['weight'])
    return prepare(verts,tris,bones[:,wrists[0],:3],bones[:,wrists[1],:3],*weights)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture',type=Path)
    parser.add_argument('out',type=Path)
    parser.add_argument('--pose', type=float,nargs=6,metavar=('RX','RY','RZ','X','Y','Z'))
    parser.add_argument('--follow-palms',action='store_true')
    parser.add_argument('--forward-limit',type=float,default=.86,help='Maximum centre forward offset during search, m')
    parser.add_argument('--long-support',action='store_true',help='Try sharing load with the distal right-hand mesh')
    args = parser.parse_args()
    run(load_capture(args.capture),args.out,args.pose,args.follow_palms,args.forward_limit,args.long_support)

