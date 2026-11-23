import {
  BinaryParser,
  TextParser,
  isFbxFormatBinary,
  convertArrayBufferToString,
} from 'three/addons/loaders/FBXLoader.js';

function summary() {
  return { vertices: 0, unweighted: 0, sumError: 0, overFour: 0, invalid: 0 };
}
function count(result, sum, influences) {
  ++result.vertices;
  if (!influences) ++result.unweighted;
  else if (Math.abs(sum - 1) > 0.001) ++result.sumError;
  if (influences > 4) ++result.overFour;
}
export function auditFBX(buffer) {
  const data = isFbxFormatBinary(buffer)
    ? new BinaryParser().parse(buffer)
    : new TextParser().parse(convertArrayBufferToString(buffer));
  const deform = data.Objects?.Deformer || {},
    geometry = data.Objects?.Geometry || {},
    models = data.Objects?.Model || {};
  const links = data.Connections?.connections || [],
    result = summary();
  for (const skin of Object.values(deform).filter((n) => n.attrType === 'Skin')) {
    const parts = links
      .filter(([from, to]) => to === skin.id && deform[from]?.attrType === 'Cluster')
      .map(([id]) => deform[id]);
    for (const [, id] of links.filter(([from, to]) => from === skin.id && geometry[to]?.Vertices)) {
      const n = geometry[id].Vertices.a.length / 3,
        sums = new Float64Array(n),
        counts = new Uint16Array(n);
      for (const part of parts) {
        if (!links.some(([from, to]) => to === part.id && models[from])) ++result.invalid;
        const indices = part.Indexes?.a || [],
          weights = part.Weights?.a || [];
        if (weights.length !== indices.length) ++result.invalid;
        for (let j = 0; j < indices.length; j++) {
          const v = indices[j],
            w = weights[j];
          if (!Number.isInteger(v) || v < 0 || v >= n || !Number.isFinite(w) || w < 0) {
            ++result.invalid;
            continue;
          }
          if (w > 0) {
            sums[v] += w;
            ++counts[v];
          }
        }
      }
      for (let v = 0; v < n; v++) count(result, sums[v], counts[v]);
    }
  }
  return result;
}
export async function auditGLTF(parser) {
  const { nodes = [], meshes = [], skins = [] } = parser.json,
    result = summary(),
    seen = new Set();
  for (const node of nodes) {
    if (node.skin === undefined || node.mesh === undefined) continue;
    const key = node.mesh + '/' + node.skin;
    if (seen.has(key)) continue;
    seen.add(key);
    const boneCount = skins[node.skin]?.joints?.length || 0;
    for (const primitive of meshes[node.mesh].primitives) {
      const attrs = primitive.attributes;
      const sets = await Promise.all(
        Object.keys(attrs)
          .filter((k) => /^WEIGHTS_\d+$/.test(k))
          .map(async (k) => {
            const id = attrs[k.replace('WEIGHTS', 'JOINTS')];
            return [
              await parser.getDependency('accessor', attrs[k]),
              id === undefined ? null : await parser.getDependency('accessor', id),
            ];
          }),
      );
      const n = parser.json.accessors[attrs.POSITION].count;
      for (let v = 0; v < n; v++) {
        let sum = 0,
          num = 0;
        for (const [weights, joints] of sets) {
          if (!joints || v >= weights.count || v >= joints.count) {
            ++result.invalid;
            continue;
          }
          for (let k = 0; k < weights.itemSize; k++) {
            const w = weights.getComponent(v, k),
              idx = joints.getComponent(v, k);
            if (
              !Number.isFinite(w) ||
              w < 0 ||
              (w > 0 && (!Number.isInteger(idx) || idx < 0 || idx >= boneCount))
            ) {
              ++result.invalid;
              continue;
            }
            if (w > 0) {
              sum += w;
              num++;
            }
          }
        }
        count(result, sum, num);
      }
    }
  }
  return result;
}
