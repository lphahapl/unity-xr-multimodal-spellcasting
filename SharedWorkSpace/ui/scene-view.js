import * as T from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { TransformControls } from 'three/addons/controls/TransformControls.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { byId as $, fileURL } from './data.js';
import { modelFile, parseModel, readFiles, saveFiles } from './models.js';

const axes = ['x', 'y', 'z'];
const shapes = {
  box: new T.BoxGeometry(),
  sphere: new T.SphereGeometry(0.5, 24, 16),
  cylinder: new T.CylinderGeometry(0.5, 0.5, 1, 24),
};
const colors = { 入口: 0x76bfaa, 出口: 0x8fb2ce, 触发区: 0xcaa969, 角色: 0xcf927e };
const degrees = (v) => v * T.MathUtils.RAD2DEG,
  radians = (v) => v * T.MathUtils.DEG2RAD;
const round = (x) => Number(x.toFixed(6));

export function createSceneView(changed, updatePlan) {
  const canvas = $('viewport3d'),
    renderer = new T.WebGLRenderer({ canvas, antialias: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  const scene = new T.Scene();
  scene.background = new T.Color(0x222e33);
  const camera = new T.PerspectiveCamera(42, 1, 0.01, 1000),
    orbit = new OrbitControls(camera, canvas);
  const gizmo = new TransformControls(camera, canvas);
  gizmo.setSize(0.8);
  scene.add(gizmo.getHelper());
  const light = new T.DirectionalLight(0xffe6bf, 2.2);
  light.position.set(4, 8, 6);
  scene.add(light, new T.HemisphereLight(0xf5f6ef, 0x839493, 1.8));
  const grid = new T.GridHelper(30, 30, 0x586a6b, 0x38484d);
  scene.add(grid);
  const blocks = new T.Group();
  blocks.name = 'Whitebox';
  scene.add(blocks);
  let doc,
    mode,
    parsed,
    epoch = 0,
    selected = '',
    nodes = new Map(),
    pointer,
    dragged = false,
    signature = '',
    pending = false;
  const msg = (t) => {
    $('sceneStatus').textContent = t;
  };
  // A background Chrome tab may be inspected before its next animation frame.
  function draw() {
    if (doc) renderer.render(scene, camera);
  }
  function selectedObject() {
    return nodes.get(selected);
  }
  function snapshot(n) {
    return {
      position: n.position.toArray().map(round),
      rotation: axes.map((k) => round(degrees(n.rotation[k]))),
      scale: n.scale.toArray().map(round),
      visible: n.visible,
    };
  }
  function fields() {
    const n = selectedObject();
    $('transformFields').disabled = !n;
    $('deleteBlock').disabled = !n;
    if (!n) return;
    const values = snapshot(n);
    for (const key of ['position', 'rotation', 'scale'])
      values[key].forEach((v, i) => ($(key + i).value = v));
    $('nodeVisible').checked = n.visible;
  }
  function select(id) {
    selected = nodes.has(id) ? id : nodes.keys().next().value || '';
    $('node3d').value = selected;
    const n = selectedObject();
    if (n) gizmo.attach(n);
    else gizmo.detach();
    fields();
  }
  function menu() {
    $('node3d').replaceChildren(
      ...Array.from(
        nodes,
        ([id, n]) =>
          new Option(
            (mode === 'model' ? '　'.repeat(Math.min(5, id.split('/').length - 1)) : '') +
              (n.name || n.type),
            id,
          ),
      ),
    );
    select(selected);
  }
  function fit() {
    const tree = mode === 'model' ? parsed?.root : blocks;
    const bounds = tree ? new T.Box3().setFromObject(tree) : new T.Box3();
    if (bounds.isEmpty())
      bounds.set(new T.Vector3(0, 0, 0), new T.Vector3(doc?.width || 8, 1, doc?.depth || 8));
    const center = bounds.getCenter(new T.Vector3()),
      size = bounds.getSize(new T.Vector3());
    const span = Math.max(size.x, size.y, size.z, 1e-3);
    camera.near = Math.max(span / 3000, 0.00001);
    camera.far = span * 180;
    camera.updateProjectionMatrix();
    const top = new URLSearchParams(location.search).get('view') === 'top';
    camera.position
      .copy(center)
      .addScaledVector(
        (top ? new T.Vector3(0, 1, 0.0001) : new T.Vector3(0.94, 0.76, 1.18)).normalize(),
        span * 1.95,
      );
    orbit.target.copy(center);
    orbit.minDistance = span / 2000;
    orbit.maxDistance = span * 65;
    orbit.update();
    grid.position.set(center.x, bounds.min.y - 0.002 * span, center.z);
    grid.scale.setScalar(span / 14);
    draw();
  }
  function disposeSource() {
    gizmo.detach();
    if (parsed) {
      scene.remove(parsed.root);
      parsed.dispose();
      parsed = null;
    }
    nodes.clear();
    signature = '';
  }
  function clearBlocks() {
    for (const n of [...blocks.children]) {
      n.material.dispose();
      blocks.remove(n);
    }
  }
  function syncBlocks() {
    const ids = new Set(doc.objects.map((o) => o.id));
    for (const [id, n] of nodes)
      if (!ids.has(id)) {
        if (gizmo.object === n) gizmo.detach();
        blocks.remove(n);
        n.material.dispose();
        nodes.delete(id);
      }
    for (const o of doc.objects) {
      let n = nodes.get(o.id);
      if (!n) {
        n = new T.Mesh(shapes.box, new T.MeshStandardMaterial({ roughness: 0.84 }));
        blocks.add(n);
        nodes.set(o.id, n);
      }
      const h = o.h ?? 1;
      n.geometry = shapes[o.shape || 'box'];
      n.material.color.setHex(colors[o.type] ?? 0xb4c0be);
      n.name = o.name;
      n.position.set(o.x + o.w / 2, (o.y ?? 0) + h / 2, o.z + o.d / 2);
      n.scale.set(o.w, h, o.d);
      n.rotation.set(...(o.rotation || [0, 0, 0]).map(radians));
      n.visible = o.visible !== false;
    }
    const sig = JSON.stringify(doc.objects.map((o) => [o.id, o.name]));
    if (sig !== signature) {
      signature = sig;
      menu();
    } else fields();
    draw();
  }
  function indexSource(asset, desc) {
    disposeSource();
    parsed = asset;
    scene.add(asset.root);
    function visit(n, key) {
      nodes.set(key, n);
      const t = desc.nodes?.[key];
      if (t) {
        n.position.fromArray(t.position);
        n.rotation.set(...t.rotation.map(radians));
        n.scale.fromArray(t.scale);
        n.visible = t.visible;
      }
      n.children.forEach((child, i) => visit(child, key + '/' + i));
    }
    visit(asset.root, 'root');
    menu();
    fit();
  }
  async function prepare(files, desc) {
    const asset = await parseModel(files, desc.filename);
    try {
      const box = new T.Box3().setFromObject(asset.root);
      if (box.isEmpty() || !Number.isFinite(box.min.lengthSq() + box.max.lengthSq()))
        throw Error('文件中没有可预览的场景网格');
      if (desc.material) {
        const loader = new T.TextureLoader();
        const [map, normalMap] = await Promise.all([
          loader.loadAsync(fileURL(desc.material.color)),
          loader.loadAsync(fileURL(desc.material.normal)),
        ]);
        map.colorSpace = T.SRGBColorSpace;
        const m = new T.MeshStandardMaterial({
          map,
          normalMap,
          roughness: 0.8,
          metalness: desc.material.metalness,
        });
        asset.root.traverse((n) => {
          if (n.isMesh) {
            for (const old of [].concat(n.material || [])) old.dispose();
            n.material = m;
          }
        });
      }
      return asset;
    } catch (e) {
      asset.dispose();
      throw e;
    }
  }
  async function load(desc, serial) {
    let asset;
    try {
      asset = await prepare(await readFiles(desc.files), desc);
      if (serial !== epoch) {
        asset.dispose();
        return;
      }
      indexSource(asset, desc);
      msg('已载入 ' + nodes.size + ' 个场景节点');
    } catch (e) {
      if (serial === epoch) msg('场景加载失败 · ' + e.message);
    }
  }
  function sync(value, planSelection) {
    const next = value.mode || 'whitebox',
      switched = doc !== value || next !== mode;
    doc = value;
    mode = next;
    if (switched) {
      ++epoch;
      pending = false;
      gizmo.detach();
      clearBlocks();
      disposeSource();
      selected = '';
      menu();
      $('sceneMode').value = mode;
      blocks.visible = mode === 'whitebox';
      $('blockTools').hidden = $('floorplan').hidden = mode !== 'whitebox';
      $('modelTools').hidden = mode !== 'model';
      $('sourceName').textContent = doc.model?.filename || '';
      msg('');
      if (mode === 'model') {
        if (doc.model) load(doc.model, epoch);
        else {
          msg('请选择要编辑的 3D 场景文件');
          fit();
        }
      }
    }
    if (mode === 'whitebox') {
      syncBlocks();
      if (planSelection && planSelection !== selected && nodes.has(planSelection)) select(planSelection);
      if (switched) fit();
    }
  }
  function recordTransform() {
    const n = selectedObject();
    if (!n || !doc) return;
    const t = snapshot(n);
    if (mode === 'model') {
      (doc.model.nodes ??= {})[selected] = t;
      fields();
      changed();
      draw();
      return;
    }
    const o = doc.objects.find((i) => i.id === selected);
    if (!o) return;
    o.w = Math.min(doc.width, Math.max(0.01, Math.abs(t.scale[0])));
    o.h = Math.max(0.01, Math.abs(t.scale[1]));
    o.d = Math.min(doc.depth, Math.max(0.01, Math.abs(t.scale[2])));
    o.x = round(Math.max(0, Math.min(doc.width - o.w, n.position.x - o.w / 2)));
    o.z = round(Math.max(0, Math.min(doc.depth - o.d, n.position.z - o.d / 2)));
    o.y = round(n.position.y - o.h / 2);
    o.rotation = t.rotation;
    o.visible = t.visible;
    changed();
    updatePlan(doc, selected);
  }
  for (const key of ['position', 'rotation', 'scale'])
    for (let i = 0; i < 3; i++)
      $(key + i).onchange = () => {
        const n = selectedObject(),
          v = $(key + i).valueAsNumber;
        if (!n || !Number.isFinite(v) || (key === 'scale' && Math.abs(v) < 0.000001)) {
          fields();
          return;
        }
        n[key][axes[i]] = key === 'rotation' ? radians(v) : v;
        recordTransform();
      };
  $('nodeVisible').onchange = () => {
    const n = selectedObject();
    if (n) {
      n.visible = $('nodeVisible').checked;
      recordTransform();
    }
  };
  gizmo.addEventListener('dragging-changed', (e) => {
    orbit.enabled = !e.value;
    if (e.value) dragged = true;
  });
  gizmo.addEventListener('objectChange', recordTransform);
  $('transformMode').onchange = () => gizmo.setMode($('transformMode').value);
  function pick(id) {
    select(id);
    if (mode === 'whitebox') updatePlan(doc, selected);
  }
  $('node3d').onchange = () => pick($('node3d').value);
  $('sceneMode').onchange = () => {
    doc.mode = $('sceneMode').value;
    changed();
    sync(doc);
  };
  $('frame3d').onclick = fit;
  $('addBlock').onclick = () => {
    const shape = $('blockShape').value,
      id = crypto.randomUUID();
    doc.objects.push({
      id,
      name: $('blockShape').selectedOptions[0].text,
      type: '白盒',
      shape,
      x: doc.width / 3,
      z: doc.depth / 3,
      w: Math.min(1, doc.width / 2),
      d: Math.min(1, doc.depth / 2),
      h: 1,
    });
    selected = id;
    changed();
    updatePlan(doc, id);
  };
  $('deleteBlock').onclick = () => {
    doc.objects = doc.objects.filter((o) => o.id !== selected);
    selected = '';
    changed();
    updatePlan(doc);
  };
  $('importScene').onclick = () => $('sceneFiles').click();
  $('sceneFiles').onchange = async (e) => {
    const files = Array.from(e.target.files);
    e.target.value = '';
    if (!files.length) return;
    const serial = ++epoch;
    pending = true;
    let candidate;
    try {
      const f = modelFile(files),
        desc = { filename: f.name, files: [], nodes: {} };
      candidate = await prepare(files, desc);
      if (serial !== epoch) return;
      desc.files = await saveFiles(files);
      if (serial !== epoch) return;
      doc.model = desc;
      indexSource(candidate, desc);
      candidate = null;
      $('sourceName').textContent = f.name;
      changed();
      msg('已导入模型，请保存场景配置');
    } catch (err) {
      if (serial === epoch) msg(err.message);
    } finally {
      candidate?.dispose();
      if (serial === epoch) pending = false;
    }
  };
  $('export3d').onclick = async () => {
    const target = mode === 'model' ? parsed?.root : blocks;
    if (!target || pending) {
      msg('请先载入场景');
      return;
    }
    try {
      const bytes = await new GLTFExporter().parseAsync(target, { binary: true, onlyVisible: true });
      const url = URL.createObjectURL(new Blob([bytes], { type: 'model/gltf-binary' })),
        a = document.createElement('a');
      a.href = url;
      a.download = doc.id + '.glb';
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 10000);
      msg('已导出当前可见场景');
    } catch (e) {
      msg('导出失败 · ' + e.message);
    }
  };
  canvas.addEventListener(
    'pointerdown',
    (e) => {
      pointer = [e.clientX, e.clientY];
      dragged = false;
    },
    true,
  );
  canvas.addEventListener('pointerup', (e) => {
    if (
      !pointer ||
      dragged ||
      Math.hypot(e.clientX - pointer[0], e.clientY - pointer[1]) > 4 ||
      gizmo.axis
    )
      return;
    const r = canvas.getBoundingClientRect(),
      ray = new T.Raycaster();
    ray.setFromCamera(
      new T.Vector2((2 * (e.clientX - r.x)) / r.width - 1, 1 - (2 * (e.clientY - r.y)) / r.height),
      camera,
    );
    const content = mode === 'model' ? parsed?.root : blocks;
    if (!content) return;
    const hit = ray.intersectObject(content, true).find((h) => {
      for (let p = h.object; p; p = p.parent) if (!p.visible) return false;
      return true;
    });
    if (hit)
      for (const [id, n] of nodes)
        if (n === hit.object) {
          pick(id);
          break;
        }
  });
  new ResizeObserver(() => {
    const { width, height } = canvas.getBoundingClientRect();
    if (!width || !height) return;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    draw();
  }).observe(canvas);
  orbit.addEventListener('change', draw);
  gizmo.addEventListener('change', draw);
  renderer.setAnimationLoop(() => {
    if (doc && !document.hidden) {
      orbit.update();
      draw();
    }
  });
  return {
    sync,
    get pending() {
      return pending;
    },
    clear() {
      ++epoch;
      pending = false;
      disposeSource();
      clearBlocks();
      doc = null;
    },
  };
}
