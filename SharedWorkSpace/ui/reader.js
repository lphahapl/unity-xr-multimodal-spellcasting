import { byId as $, el, link, request, get, save, remove } from './data.js';
import { migrate } from './legacy.js';
const labels = { rig: '骨骼与动画', plans: '策划案', scenes: '场景原型' };
let snapshot,
  editing,
  writing = false;
function report(e) {
  $('status').textContent = e.message;
}
async function refresh() {
  snapshot = await request('/api/context');
  $('status').textContent = Object.keys(labels)
    .flatMap((k) => snapshot[k].errors.map((e) => k + '/' + e.file + ' · ' + e.message))
    .join('\n');
  render();
}
function lines(kind, doc) {
  if (kind === 'plans') return [doc.status, doc.summary, doc.body];
  if (kind === 'scenes')
    return [
      doc.summary,
      doc.mode === 'model' ? '3D 场景模型 · ' + (doc.model?.filename || '待导入') : '3D 白盒',
      ...Object.entries(doc.model?.nodes || {}).map(
        ([k, v]) =>
          k +
          ' · 位置 ' +
          v.position.join(', ') +
          ' · 旋转 ' +
          v.rotation.join(', ') +
          ' · 缩放 ' +
          v.scale.join(', ') +
          ' · ' +
          (v.visible ? '可见' : '隐藏'),
      ),
      '场地 ' + doc.width + ' × ' + doc.depth + ' 米',
      ...doc.objects.map(
        (o) =>
          o.name +
          ' · ' +
          o.type +
          ' · X ' +
          o.x +
          ' / Z ' +
          o.z +
          ' · ' +
          o.w +
          ' × ' +
          (o.h ?? 1) +
          ' × ' +
          o.d +
          ' 米 · ' +
          (o.shape || 'box') +
          ' · Y ' +
          (o.y ?? 0) +
          ' · 旋转 ' +
          (o.rotation || [0, 0, 0]).join(', '),
      ),
    ];
  return doc.versions.flatMap((v) => [
    v.label + ' · ' + v.filename,
    ...(v.animations || []).map((a) => '动画 · ' + a.label + ' · ' + a.clips.join(' / ')),
    ...v.notes.map((n) => '批注 · ' + new Date(n.created).toLocaleString('zh-CN') + ' · ' + n.text),
  ]);
}
function render() {
  const term = $('query').value.trim().toLowerCase(),
    area = $('results');
  area.replaceChildren();
  for (const kind of Object.keys(labels)) {
    if ($('kind').value && $('kind').value !== kind) continue;
    for (const record of snapshot[kind].entries) {
      const doc = record.doc,
        body = lines(kind, doc),
        file = 'config/' + kind + '/' + doc.id + '.json';
      if (term && ![file, doc.name, ...body].join(' ').toLowerCase().includes(term)) continue;
      const card = el('article', '', 'record');
      card.dataset.id = doc.id;
      card.dataset.kind = kind;
      card.append(el('small', labels[kind], 'muted'), el('h3', doc.name), el('pre', body.join('\n\n')));
      const actions = el('div', '', 'tools');
      const href =
        kind === 'rig'
          ? '/rig/?collection=' + doc.id + '&model=' + (doc.versions[0]?.id || '')
          : '/' + kind + '/?id=' + doc.id;
      actions.append(link('打开工作台', href), link('原始 JSON', '/' + file));
      const edit = el('button', '编辑 JSON'),
        del = el('button', '删除', 'danger');
      edit.onclick = () => open(kind, record);
      del.onclick = async () => {
        if (!confirm('删除「' + doc.name + '」的配置文件？')) return;
        try {
          await remove(kind, doc.id, record.revision);
          await refresh();
        } catch (e) {
          report(e);
        }
      };
      actions.append(edit, del);
      card.append(actions, el('p', file, 'path'));
      area.append(card);
    }
  }
  if (!area.childElementCount) area.append(el('p', '没有匹配记录。', 'muted'));
}
function template(kind) {
  const base = { id: crypto.randomUUID(), name: '新记录' };
  if (kind === 'rig') return { ...base, versions: [] };
  if (kind === 'plans') return { ...base, status: '草稿', summary: '', body: '' };
  return { ...base, width: 20, depth: 14, summary: '', objects: [] };
}
function open(kind, record) {
  editing = record;
  $('editKind').value = kind;
  $('editKind').disabled = !!record;
  $('title').textContent = record ? '编辑 ' + record.doc.name : '新建配置文件';
  $('jsonText').value = JSON.stringify(record?.doc || template(kind), null, 2);
  $('error').textContent = '';
  $('dialog').showModal();
}
$('editKind').onchange = () => {
  $('jsonText').value = JSON.stringify(template($('editKind').value), null, 2);
};
$('new').onclick = () => open($('kind').value || 'plans');
$('cancel').onclick = () => $('dialog').close();
$('dialog').oncancel = (e) => {
  if (writing) e.preventDefault();
};
$('form').onsubmit = async (e) => {
  e.preventDefault();
  if (writing) return;
  writing = true;
  $('write').disabled = $('cancel').disabled = true;
  try {
    const doc = JSON.parse($('jsonText').value);
    if (editing && doc.id !== editing.doc.id) throw Error('编辑现有记录时请保留 ID');
    await save($('editKind').value, doc, editing?.revision);
    $('dialog').close();
    await refresh();
  } catch (e) {
    $('error').textContent = e.message;
  } finally {
    writing = false;
    $('write').disabled = $('cancel').disabled = false;
  }
};
$('kind').value = new URL(location.href).searchParams.get('kind') || '';
$('kind').onchange = $('query').oninput = render;
$('reload').onclick = () => refresh().catch(report);
$('migrate').onclick = async () => {
  $('migrate').disabled = true;
  try {
    const message = await migrate();
    await refresh();
    $('status').textContent = message;
  } catch (e) {
    report(e);
  } finally {
    $('migrate').disabled = false;
  }
};
await refresh().catch(report);
