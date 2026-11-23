import { byId as $, el } from './data.js';
const ns = 'http://www.w3.org/2000/svg';
function svg(name, attrs) {
  const node = document.createElementNS(ns, name);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
  return node;
}
const palette = { 入口: '#64ac99', 触发区: '#d0aa69', 角色: '#c48d7e', 出口: '#92aacd' };
export function sceneEditor(changed, onRender) {
  let doc, selected, drag;
  const map = $('map');
  const fields = { objectName: 'name', objectType: 'type', x: 'x', z: 'z', w: 'w', d: 'd' };
  function active() {
    return doc?.objects.find((o) => o.id === selected);
  }
  function showFields() {
    const o = active();
    $('objectFields').disabled = $('removeObject').disabled = !o;
    for (const [id, key] of Object.entries(fields)) $(id).value = o?.[key] ?? '';
  }
  function render() {
    onRender(doc, selected);
    const W = doc.width,
      D = doc.depth;
    if (!(W > 0 && D > 0)) {
      map.replaceChildren();
      return;
    }
    const stroke = Math.max(W, D) / 300,
      font = Math.max(W, D) / 45;
    map.setAttribute('viewBox', [-0.2, -0.2, W + 0.4, D + 0.4].join(' '));
    const spacing = Math.max(1, Math.ceil(Math.max(W, D) / 36));
    const pattern = svg('pattern', {
      id: 'metres',
      width: spacing,
      height: spacing,
      patternUnits: 'userSpaceOnUse',
    });
    pattern.append(
      svg('path', {
        d: 'M ' + spacing + ' 0 H0 V' + spacing,
        stroke: '#3e5053',
        'stroke-width': stroke / 2,
        fill: 'none',
      }),
    );
    const defs = svg('defs', {});
    defs.append(pattern);
    map.replaceChildren(
      defs,
      svg('rect', {
        x: 0,
        y: 0,
        width: W,
        height: D,
        fill: 'url(#metres)',
        stroke: '#586c6c',
        'stroke-width': stroke,
      }),
    );
    const rows = $('objects');
    rows.replaceChildren();
    for (const o of doc.objects) {
      const g = svg('g', { 'data-object': o.id }),
        color = palette[o.type] || '#92bbb8';
      g.append(
        svg('rect', {
          x: o.x,
          z: o.z,
          y: o.z,
          width: Math.max(0, o.w),
          height: Math.max(0, o.d),
          fill: color,
          'fill-opacity': 0.28,
          stroke: o.id === selected ? '#ffda95' : color,
          'stroke-width': o.id === selected ? stroke * 2 : stroke,
        }),
      );
      const text = svg('text', {
        x: o.x + o.w / 2,
        y: o.z + o.d / 2,
        'text-anchor': 'middle',
        'dominant-baseline': 'central',
        'font-size': font,
        fill: '#f5f0df',
      });
      text.textContent = o.name;
      g.append(text);
      map.append(g);
      const tr = el('tr'),
        td = el('td'),
        b = el('button', o.name || '未命名');
      b.type = 'button';
      b.setAttribute('aria-pressed', o.id === selected);
      b.onclick = () => {
        selected = o.id;
        showFields();
        render();
      };
      td.append(b);
      tr.append(td, el('td', o.type), el('td', o.x + ' / ' + o.z), el('td', o.w + ' × ' + o.d));
      rows.append(tr);
    }
  }
  for (const [id, key] of Object.entries(fields))
    $(id).oninput = () => {
      const o = active();
      if (!o) return;
      o[key] = ['name', 'type'].includes(key) ? $(id).value : $(id).valueAsNumber;
      changed();
      render();
    };
  for (const id of ['width', 'depth'])
    $(id).oninput = () => {
      doc[id] = $(id).valueAsNumber;
      changed();
      render();
    };
  $('addObject').onclick = () => {
    const id = crypto.randomUUID();
    doc.objects.push({
      id,
      name: '新元素',
      type: '道具',
      x: 0,
      z: 0,
      w: Math.min(1, doc.width),
      d: Math.min(1, doc.depth),
    });
    selected = id;
    changed();
    render();
    showFields();
    $('objectName').select();
  };
  $('removeObject').onclick = () => {
    doc.objects = doc.objects.filter((o) => o.id !== selected);
    selected = doc.objects[0]?.id;
    changed();
    render();
    showFields();
  };
  function point(e) {
    return new DOMPoint(e.clientX, e.clientY).matrixTransform(map.getScreenCTM().inverse());
  }
  map.onpointerdown = (e) => {
    const item = e.target.closest('[data-object]');
    if (!item || e.button !== 0) return;
    selected = item.dataset.object;
    const o = active(),
      p = point(e);
    drag = { pid: e.pointerId, dx: p.x - o.x, dz: p.y - o.z };
    map.setPointerCapture(e.pointerId);
    render();
    showFields();
  };
  map.onpointermove = (e) => {
    if (drag?.pid !== e.pointerId) return;
    const p = point(e),
      o = active();
    o.x = Math.max(0, Math.min(doc.width - o.w, Math.round((p.x - drag.dx) * 10) / 10));
    o.z = Math.max(0, Math.min(doc.depth - o.d, Math.round((p.y - drag.dz) * 10) / 10));
    changed();
    render();
    showFields();
  };
  map.onpointerup =
    map.onpointercancel =
    map.onlostpointercapture =
      () => {
        drag = null;
      };
  return (value, preferred) => {
    selected =
      preferred ||
      (value === doc && value.objects.some((o) => o.id === selected) ? selected : value.objects[0]?.id);
    doc = value;
    drag = null;
    $('width').value = doc.width;
    $('depth').value = doc.depth;
    render();
    showFields();
  };
}
