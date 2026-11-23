import { animationPanel } from './animation.js';
import { fileURL } from '../ui/data.js';
import { startLibrary } from './collection-view.js';
import * as T from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { auditFBX, auditGLTF } from './weights.js';

const $ = (id) => document.getElementById(id);
const stage = $('stage'),
  canvas = $('view'),
  overlay = $('overlay'),
  ctx = overlay.getContext('2d');
const renderer = new T.WebGLRenderer({ canvas, antialias: true, alpha: false });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.outputColorSpace = T.SRGBColorSpace;
renderer.setClearColor(0x2a3236);
const scene = new T.Scene(),
  camera = new T.PerspectiveCamera(36, 1, 0.002, 100);
scene.add(new T.HemisphereLight(0xfff2df, 0x80949a, 1.7));
for (const [p, color, power] of [
  [[3, 5, 4], 0xffe2b6, 2.4],
  [[-3, 2, 1], 0xc7e1eb, 1.2],
  [[1, 3, -4], 0xffe0bd, 1.8],
]) {
  const light = new T.DirectionalLight(color, power);
  light.position.set(...p);
  scene.add(light);
}
const grid = new T.GridHelper(10, 50, 0x626d6d, 0x3e494c);
scene.add(grid);
const controls = new OrbitControls(camera, canvas);
controls.enableDamping = true;
controls.minDistance = 0.025;
controls.maxDistance = 14;
const colorStops = [0x18232c, 0x295187, 0x2fb8ac, 0xf4c660, 0xef654c].map((c) => new T.Color(c));
let asset,
  holder,
  handBounds,
  bones = [],
  parents = [],
  skins = [],
  projected = [],
  picked = -1,
  hover = -1;
let data,
  rest = [],
  undo = [],
  redo = [],
  width = 1,
  height = 1,
  heat = false,
  showBones = true,
  showNames = false,
  rotate = false,
  drag = null;
let currentResources = [],
  originalColors = new Map(),
  originalMaterials = new Map(),
  heatMaterials = new Map();
const v1 = new T.Vector3(),
  v2 = new T.Vector3(),
  v3 = new T.Vector3(),
  mouse = new T.Vector2(),
  ray = new T.Raycaster();
const q1 = new T.Quaternion(),
  q2 = new T.Quaternion(),
  q3 = new T.Quaternion();
const nameOf = (b) => b?.name.replace(/^mixamorig[:_]?/i, '') || '—';
const updateStatus = (t) => {
  $('viewstatus').textContent = t;
};
const format = (n) => n.toLocaleString('zh-CN');
function toggle(id, value) {
  $(id).classList.toggle('on', value);
  $(id).setAttribute('aria-pressed', String(value));
}
function frameAll() {
  if (asset && handBounds) {
    const halfAngle = T.MathUtils.degToRad(camera.fov / 2);
    const narrowAngle = Math.min(halfAngle, Math.atan(Math.tan(halfAngle) * camera.aspect));
    const distance = (handBounds.radius * 1.05) / Math.sin(narrowAngle);
    controls.target.copy(handBounds.center);
    camera.position.set(0.38, 1, -0.43).normalize().multiplyScalar(distance).add(controls.target);
    controls.update();
    return;
  }
  controls.target.set(0, 0.92, 0);
  camera.position.set(0.72, 1.14, Math.max(3.65, 2.2 / camera.aspect));
  controls.update();
}
function focusBone() {
  if (!bones[picked]) return;
  const target = bones[picked].getWorldPosition(new T.Vector3());
  const direction = camera.position.clone().sub(controls.target).normalize();
  controls.target.copy(target);
  camera.position.copy(target).addScaledVector(direction, 0.84);
  controls.update();
}
function resize() {
  width = Math.max(1, stage.clientWidth);
  height = Math.max(1, stage.clientHeight);
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  const d = Math.min(devicePixelRatio, 2);
  overlay.width = Math.round(d * width);
  overlay.height = Math.round(d * height);
  ctx.setTransform(d, 0, 0, d, 0, 0);
}
new ResizeObserver(resize).observe(stage);
resize();
frameAll();
function capture() {
  return bones.map((b) => [b.position.clone(), b.quaternion.clone()]);
}
function apply(pose) {
  pose.forEach(([p, q], i) => {
    bones[i].position.copy(p);
    bones[i].quaternion.copy(q);
  });
  asset?.updateWorldMatrix(true, true);
}
function historyButtons() {
  $('undo').disabled = !undo.length;
  $('redo').disabled = !redo.length;
  $('reset').disabled = !bones.length;
}
function save() {
  undo.push(capture());
  if (undo.length > 64) undo.shift();
  redo = [];
  historyButtons();
}
const animation = animationPanel();
function undoPose() {
  if (animation.active()) return;
  if (!undo.length) return;
  redo.push(capture());
  apply(undo.pop());
  historyButtons();
  updateStatus('已撤销姿态');
}
function redoPose() {
  if (animation.active()) return;
  if (!redo.length) return;
  undo.push(capture());
  apply(redo.pop());
  historyButtons();
  updateStatus('已重做姿态');
}
$('undo').onclick = undoPose;
$('redo').onclick = redoPose;
$('reset').onclick = () => {
  if (animation.active()) $('animStop').click();
  if (bones.length) {
    save();
    apply(rest);
    updateStatus('已还原导入姿态');
  }
};
$('fit').onclick = frameAll;
$('focus').onclick = focusBone;
function showTree() {
  const filter = $('search').value.trim().toLowerCase(),
    frag = document.createDocumentFragment();
  bones.forEach((bone, i) => {
    if (filter && !bone.name.toLowerCase().includes(filter)) return;
    const button = document.createElement('button');
    button.className = 'bone' + (i === picked ? ' chosen' : '');
    button.textContent = nameOf(bone);
    button.title = bone.name;
    button.setAttribute('role', 'option');
    button.setAttribute('aria-selected', String(i === picked));
    button.dataset.index = i;
    let depth = 0;
    for (let p = parents[i]; p >= 0; p = parents[p]) depth++;
    button.style.paddingLeft = (filter ? 4 : 4 + Math.min(depth, 7) * 7) + 'px';
    button.onclick = () => select(i);
    button.ondblclick = focusBone;
    frag.append(button);
  });
  $('tree').replaceChildren(frag);
}
$('search').oninput = showTree;
function review() {
  const area = $('audit');
  area.replaceChildren();
  if (!data) return;
  for (const [label, key] of [
    ['原始顶点', 'vertices'],
    ['未加权', 'unweighted'],
    ['权重和异常', 'sumError'],
    ['超过四骨', 'overFour'],
    ['无效权重或引用', 'invalid'],
  ]) {
    const row = document.createElement('div');
    row.className = 'auditline' + (key !== 'vertices' && data[key] ? ' warn' : '');
    const a = document.createElement('span'),
      b = document.createElement('strong');
    a.textContent = label;
    b.textContent = format(data[key]);
    row.append(a, b);
    area.append(row);
  }
  const note = document.createElement('p');
  note.className = 'hint';
  note.textContent = '统计原文件蒙皮。热图显示浏览器使用的前四项影响，权重和误差阈值为 0.001。';
  area.append(note);
}
function select(i) {
  picked = i;
  $('selection').textContent = i < 0 ? '模型没有骨骼' : nameOf(bones[i]);
  $('parent').textContent = i < 0 ? '—' : nameOf(bones[parents[i]]);
  let count = 0,
    maximum = 0;
  for (const mesh of skins) {
    const index = mesh.skeleton.bones.indexOf(bones[i]),
      geo = mesh.geometry;
    const ids = geo.getAttribute('skinIndex'),
      weights = geo.getAttribute('skinWeight');
    if (!ids || !weights) continue;
    const colors = heat ? new Float32Array(ids.count * 3) : null,
      c = new T.Color();
    for (let n = 0; n < ids.count; n++) {
      let w = 0;
      if (index >= 0)
        for (let j = 0; j < Math.min(4, ids.itemSize); j++)
          if (ids.getComponent(n, j) === index) w += weights.getComponent(n, j);
      if (w > 0) {
        count++;
        maximum = Math.max(w, maximum);
      }
      if (colors) {
        const t = Math.min(1, Math.max(0, w)) * 4,
          k = Math.min(3, Math.floor(t));
        c.copy(colorStops[k])
          .lerp(colorStops[k + 1], t - k)
          .toArray(colors, n * 3);
      }
    }
    if (colors) {
      geo.setAttribute('color', new T.BufferAttribute(colors, 3));
      mesh.material = heatMaterials.get(mesh);
    } else {
      const old = originalColors.get(mesh);
      if (old) geo.setAttribute('color', old);
      else geo.deleteAttribute('color');
      mesh.material = originalMaterials.get(mesh);
    }
  }
  $('affected').textContent = format(count);
  $('affected').title = '实际渲染顶点数，UV 和法线接缝可能拆点';
  $('maxWeight').textContent = maximum.toFixed(3);
  showTree();
}
function setHeat(v) {
  heat = v;
  toggle('heat', v);
  toggle('texture', !v);
  select(picked);
}
$('texture').onclick = () => setHeat(false);
$('heat').onclick = () => setHeat(true);
$('dots').onclick = () => {
  showBones = !showBones;
  toggle('dots', showBones);
};
$('labels').onclick = () => {
  showNames = !showNames;
  showBones = true;
  toggle('dots', true);
  toggle('labels', showNames);
};
$('pull').onclick = () => {
  rotate = false;
  toggle('pull', true);
  toggle('rotation', false);
};
$('rotation').onclick = () => {
  rotate = true;
  toggle('pull', false);
  toggle('rotation', true);
};

function release(object) {
  if (!object) return;
  const textures = new Set();
  object.traverse((n) => {
    if (n.isMesh) {
      n.geometry?.dispose();
      for (const m of [].concat(n.material || [])) {
        for (const v of Object.values(m)) if (v?.isTexture) textures.add(v);
        m.dispose();
      }
    }
  });
  textures.forEach((t) => t.dispose());
}
function boundsOf(object) {
  object.updateWorldMatrix(true, true);
  const box = new T.Box3().setFromObject(object, true);
  if (box.isEmpty() || !Number.isFinite(box.min.lengthSq() + box.max.lengthSq()))
    throw Error('文件中没有可预览的网格');
  return box;
}
function mount(object, audit, name, resources, box) {
  if (holder) {
    for (const [mesh, mat] of originalMaterials) mesh.material = mat;
    scene.remove(holder);
    release(asset);
  }
  heatMaterials.forEach((m) => m.dispose());
  currentResources.forEach((u) => URL.revokeObjectURL(u));
  currentResources = resources;
  originalMaterials = new Map();
  originalColors = new Map();
  heatMaterials = new Map();
  asset = object;
  data = audit;
  holder = new T.Group();
  holder.add(object);
  scene.add(holder);
  object.updateWorldMatrix(true, true);
  const size = box.getSize(new T.Vector3()),
    center = box.getCenter(new T.Vector3()),
    s = 1.85 / Math.max(0.0001, size.y, size.x * 0.6, size.z * 0.6);
  holder.scale.setScalar(s);
  holder.position.set(-center.x * s, -box.min.y * s, -center.z * s);
  holder.updateWorldMatrix(true, true);
  const jointSet = new Set();
  skins = [];
  object.traverse((n) => {
    if (n.isBone) jointSet.add(n);
    if (n.isSkinnedMesh) skins.push(n);
  });
  for (const mesh of skins) for (const bone of mesh.skeleton.bones) jointSet.add(bone);
  bones = Array.from(jointSet).filter(Boolean);
  handBounds =
    bones.length === 26 && bones.some((b) => b.name === 'XRHand_Wrist')
      ? box.clone().applyMatrix4(holder.matrixWorld).getBoundingSphere(new T.Sphere())
      : null;
  const indices = new Map(bones.map((b, i) => [b, i]));
  parents = bones.map((b) => {
    for (let p = b.parent; p; p = p.parent) if (indices.has(p)) return indices.get(p);
    return -1;
  });
  for (const mesh of skins) {
    // Separate geometry color buffers even if two skin instances share the original geometry.
    if (skins.some((other) => other !== mesh && other.geometry === mesh.geometry))
      mesh.geometry = mesh.geometry.clone();
    originalMaterials.set(mesh, mesh.material);
    originalColors.set(mesh, mesh.geometry.getAttribute('color'));
    heatMaterials.set(mesh, new T.MeshBasicMaterial({ vertexColors: true, side: T.DoubleSide }));
    mesh.frustumCulled = false;
  }
  rest = capture();
  undo = [];
  redo = [];
  drag = null;
  hover = -1;
  projected = bones.map(() => new T.Vector3());
  $('filename').textContent = name;
  $('count').textContent = bones.length;
  historyButtons();
  review();
  let start = bones.findIndex((b) => /right.*forearm|forearm.*[._]r$/i.test(b.name));
  if (start < 0) start = bones.length ? 0 : -1;
  select(start);
  frameAll();
  updateStatus(bones.length + ' 根骨骼 · ' + skins.length + ' 个蒙皮网格');
}
async function loadModel(buffer, filename, files, version, beforeMount) {
  const manager = new T.LoadingManager(),
    urls = [],
    byName = new Map(files.map((f) => [f.name.toLowerCase(), f]));
  manager.setURLModifier((url) => {
    if (/^(data:|blob:)/.test(url)) return url;
    const parts = decodeURIComponent(url).replaceAll('\\', '/').split('/');
    const file = byName.get(parts.at(-1).toLowerCase());
    if (file) {
      const tmp = URL.createObjectURL(file);
      urls.push(tmp);
      return tmp;
    }
    // User imports resolve only their local companion files. No third-party requests.
    return 'data:application/octet-stream;base64,';
  });
  let object, audit;
  try {
    if (/\.fbx$/i.test(filename)) {
      audit = auditFBX(buffer);
      object = new FBXLoader(manager).parse(buffer, '');
    } else {
      const loader = new GLTFLoader(manager);
      // GLTFLoader normalizes the accessor in place while constructing a SkinnedMesh.
      loader.register((parser) => ({
        name: 'SourceSkinAudit',
        beforeRoot: async () => {
          audit = await auditGLTF(parser);
        },
      }));
      const result = await loader.parseAsync(buffer, '');
      object = result.scene;
      object.animations = result.animations;
    }
    const sample = version.material;
    if (version.slotAlbedos) {
      const slots = new Set();
      object.traverse((node) => {
        if (node.isMesh) for (const material of [].concat(node.material)) slots.add(material);
      });
      for (const [name, albedoFile] of Object.entries(version.slotAlbedos)) {
        const targets = [...slots].filter((material) => material.name === name);
        const attachment = files.find((file) => file.name === albedoFile);
        if (!targets.length || !attachment) throw Error('材质槽或底色附件缺失 · ' + name);
        const url = URL.createObjectURL(attachment);
        urls.push(url);
        const map = await new T.TextureLoader().loadAsync(url);
        map.colorSpace = T.SRGBColorSpace;
        map.flipY = /\.fbx$/i.test(filename);
        for (const material of targets) {
          material.map = map;
          material.color.set(0xffffff);
          material.needsUpdate = true;
        }
      }
    } else if (sample) {
      const loader = new T.TextureLoader(),
        [map, normalMap] = await Promise.all([
          loader.loadAsync(fileURL(sample.color)),
          loader.loadAsync(fileURL(sample.normal)),
        ]);
      map.colorSpace = T.SRGBColorSpace;
      const material = new T.MeshStandardMaterial({
        map,
        normalMap,
        metalness: sample.metalness,
        roughness: 0.65,
      });
      object.traverse((n) => {
        if (n.isMesh) {
          for (const old of [].concat(n.material)) old?.dispose();
          n.material = material;
        }
      });
    }
    const box = boundsOf(object);
    if (beforeMount) await beforeMount();
    mount(object, audit, filename, urls, box);
  } catch (err) {
    if (object !== asset) release(object);
    urls.forEach((u) => URL.revokeObjectURL(u));
    throw err;
  }
}
async function displayVersion(version, files, beforeMount) {
  if (!version) {
    animation.bind(null, [], null);
    if (asset) {
      for (const [mesh, mat] of originalMaterials) mesh.material = mat;
      scene.remove(holder);
      release(asset);
      asset = holder = null;
    }
    for (const m of heatMaterials.values()) m.dispose();
    heatMaterials.clear();
    originalMaterials.clear();
    originalColors.clear();
    currentResources.forEach((url) => URL.revokeObjectURL(url));
    currentResources = [];
    bones = [];
    parents = [];
    skins = [];
    projected = [];
    data = null;
    rest = [];
    undo = [];
    redo = [];
    drag = null;
    $('filename').textContent = '尚未选择模型';
    $('count').textContent = '0';
    select(-1);
    review();
    historyButtons();
    return;
  }
  updateStatus('正在载入 ' + version.filename + '…');
  const source = files.find((f) => f.name === version.filename);
  if (!source) throw Error('缺少模型文件 ' + version.filename);
  await loadModel(await source.arrayBuffer(), version.filename, files, version, beforeMount);
  animation.bind(asset, bones, version, () => picked);
}
function point(e) {
  const r = canvas.getBoundingClientRect();
  return [e.clientX - r.left, e.clientY - r.top];
}
function hit(x, y) {
  if (!showBones) return -1;
  let best = 11,
    id = -1;
  projected.forEach((p, i) => {
    if (p.z < -1 || p.z > 1) return;
    const d = Math.hypot(p.x - x, p.y - y);
    if (d < best) {
      best = d;
      id = i;
    }
  });
  return id;
}
function targetAt(x, y, plane) {
  mouse.set((2 * x) / width - 1, 1 - (2 * y) / height);
  ray.setFromCamera(mouse, camera);
  return ray.ray.intersectPlane(plane, new T.Vector3());
}
function turnWorld(b, q) {
  const parent = b.parent.getWorldQuaternion(q1);
  b.quaternion.premultiply(q2.copy(parent).invert().multiply(q).multiply(parent)).normalize();
  b.updateWorldMatrix(false, true);
}
const isFinger = (b) => /thumb|index|middle|pinky|little|ring\d/i.test(b?.name || '');
function pull(i, goal) {
  const b = bones[i],
    pi = parents[i];
  if (pi < 0) {
    b.position.copy(b.parent.worldToLocal(goal.clone()));
    b.updateWorldMatrix(false, true);
    return;
  }
  if (isFinger(b) && !isFinger(bones[pi])) {
    const child = b.children.find((n) => n.isBone);
    if (!child) return;
    b.getWorldPosition(v1);
    child.getWorldPosition(v2).sub(v1);
    v3.copy(v2).add(goal).sub(v1);
    if (v3.lengthSq() && v2.lengthSq())
      turnWorld(b, q3.setFromUnitVectors(v2.normalize(), v3.normalize()));
    return;
  }
  for (let iter = 0; iter < 14; iter++) {
    if (b.getWorldPosition(v1).distanceToSquared(goal) < 0.000001) break;
    let j = pi;
    for (let n = 0; n < 2 && j >= 0; n++, j = parents[j]) {
      if (parents[j] < 0 || (isFinger(b) && !isFinger(bones[j]))) break;
      const joint = bones[j];
      joint.getWorldPosition(v1);
      b.getWorldPosition(v2).sub(v1);
      v3.copy(goal).sub(v1);
      if (v2.lengthSq() > 1e-10 && v3.lengthSq() > 1e-10)
        turnWorld(joint, q3.setFromUnitVectors(v2.normalize(), v3.normalize()));
    }
  }
}
canvas.addEventListener(
  'pointerdown',
  (e) => {
    if (e.button !== 0) return;
    if (animation.active()) {
      const id = hit(...point(e));
      if (id >= 0) select(id);
      return;
    }
    const [x, y] = point(e),
      id = hit(x, y);
    if (id < 0) return;
    e.stopImmediatePropagation();
    e.preventDefault();
    select(id);
    const pos = bones[id].getWorldPosition(new T.Vector3());
    const plane = new T.Plane().setFromNormalAndCoplanarPoint(
      camera.getWorldDirection(new T.Vector3()),
      pos,
    );
    const p = targetAt(x, y, plane);
    if (!p) return;
    drag = { id, plane, offset: pos.sub(p), x, y, saved: false, pointer: e.pointerId };
    controls.enabled = false;
    canvas.setPointerCapture(e.pointerId);
  },
  true,
);
canvas.addEventListener(
  'pointermove',
  (e) => {
    const [x, y] = point(e);
    hover = hit(x, y);
    canvas.style.cursor = drag ? 'grabbing' : hover >= 0 ? 'grab' : 'default';
    if (!drag || drag.pointer !== e.pointerId) return;
    e.stopImmediatePropagation();
    const dx = x - drag.x,
      dy = y - drag.y;
    if (!dx && !dy) return;
    if (!drag.saved) {
      save();
      drag.saved = true;
    }
    if (rotate) {
      const qYaw = new T.Quaternion().setFromAxisAngle(
        v1.set(0, 1, 0).applyQuaternion(camera.quaternion),
        dx * 0.009,
      );
      const qPitch = new T.Quaternion().setFromAxisAngle(
        v1.set(1, 0, 0).applyQuaternion(camera.quaternion),
        dy * 0.009,
      );
      turnWorld(bones[drag.id], qYaw.multiply(qPitch));
    } else {
      const p = targetAt(x, y, drag.plane);
      if (p) pull(drag.id, p.add(drag.offset));
    }
    drag.x = x;
    drag.y = y;
    updateStatus('预览姿态已修改');
  },
  true,
);
function finish(e) {
  if (drag && drag.pointer === e.pointerId) {
    drag = null;
    controls.enabled = true;
    if (canvas.hasPointerCapture(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
  }
}
for (const event of ['pointerup', 'pointercancel', 'lostpointercapture'])
  canvas.addEventListener(event, finish, true);
canvas.addEventListener('dblclick', (e) => {
  const id = hit(...point(e));
  if (id >= 0) {
    select(id);
    focusBone();
  }
});
window.addEventListener('keydown', (e) => {
  if (e.target.closest('dialog, input, select, textarea')) return;
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
    e.preventDefault();
    e.shiftKey ? redoPose() : undoPose();
  }
  if (e.key.toLowerCase() === 'f') focusBone();
});
function draw() {
  requestAnimationFrame(draw);
  animation.tick(performance.now());
  controls.update();
  renderer.render(scene, camera);
  ctx.clearRect(0, 0, width, height);
  if (!showBones || !asset) return;
  for (let i = 0; i < bones.length; i++) {
    const p = bones[i].getWorldPosition(projected[i]).project(camera);
    p.x = ((p.x + 1) * width) / 2;
    p.y = ((1 - p.y) * height) / 2;
  }
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  for (let i = 0; i < bones.length; i++) {
    const p = projected[i],
      other = projected[parents[i]];
    if (!other || Math.abs(p.z) > 1 || Math.abs(other.z) > 1) continue;
    ctx.moveTo(other.x, other.y);
    ctx.lineTo(p.x, p.y);
  }
  ctx.strokeStyle = 'rgba(112,233,213,.74)';
  ctx.stroke();
  for (let i = 0; i < bones.length; i++) {
    const p = projected[i];
    if (Math.abs(p.z) > 1) continue;
    const strong = i === picked || i === hover;
    ctx.beginPath();
    ctx.arc(p.x, p.y, strong ? 5.5 : 2.8, 0, Math.PI * 2);
    ctx.fillStyle = strong ? '#f6bf70' : '#a2e5d9';
    ctx.fill();
    ctx.strokeStyle = '#223a3b';
    ctx.lineWidth = 1;
    ctx.stroke();
  }
  ctx.font = '12px Consolas, monospace';
  ctx.lineWidth = 3;
  for (let i = 0; i < bones.length; i++) {
    if (!showNames && i !== picked && i !== hover) continue;
    const p = projected[i];
    if (Math.abs(p.z) > 1) continue;
    const label = nameOf(bones[i]);
    ctx.strokeStyle = '#1a2529';
    ctx.strokeText(label, p.x + 8, p.y - 6);
    ctx.fillStyle = i === picked ? '#ffda98' : '#e4f4ed';
    ctx.fillText(label, p.x + 8, p.y - 6);
  }
}
draw();
startLibrary(displayVersion, updateStatus).catch((error) =>
  updateStatus('无法打开本地模型库 · ' + error.message),
);
