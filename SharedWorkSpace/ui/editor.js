import { byId as $, el, list, save, remove } from './data.js';
import { sceneEditor } from './scene.js';
const kind = location.pathname.split('/')[1],
  scene = kind === 'scenes';
let entries = [],
  doc,
  revision,
  dirty = false,
  busy = false;
function changed() {
  dirty = true;
  $('status').textContent = '有未保存的修改';
}
let view3d;
const showScene = scene ? sceneEditor(changed, (value, id) => view3d?.sync(value, id)) : null;
if (scene) {
  const { createSceneView } = await import('./scene-view.js');
  view3d = createSceneView(changed, (value, id) => showScene(value, id));
}
function canLeave() {
  return !busy && (!dirty || confirm('放弃本页尚未保存的修改？'));
}
function sidebar() {
  const term = $('filter').value.toLowerCase();
  $('list').replaceChildren();
  for (const { doc: item } of entries) {
    if (![item.name, item.summary, item.status, item.body].join(' ').toLowerCase().includes(term))
      continue;
    const b = el('button', item.name);
    b.setAttribute('aria-pressed', String(item.id === doc?.id));
    b.onclick = () => {
      if (canLeave()) open(item.id);
    };
    $('list').append(b);
  }
}
function display(value, rev) {
  doc = structuredClone(value);
  revision = rev;
  dirty = false;
  $('editor').hidden = false;
  $('name').value = doc.name;
  $('summary').value = doc.summary;
  if (scene) showScene(doc);
  else {
    $('planStatus').value = doc.status;
    $('body').value = doc.body;
    const preview = $('reviewPreview');
    preview.replaceChildren();
    preview.hidden = !/^\/plans\/[a-z0-9-]+\.html$/.test(doc.reviewPage || '');
    if (!preview.hidden) {
      const frame = document.createElement('iframe');
      frame.title = doc.name + ' · 图稿与模型对照';
      frame.style.cssText = 'display:block;width:100%;height:480px;border:0';
      frame.onload = () => {
        const content = frame.contentDocument?.body;
        if (!content) return;
        const observer = new ResizeObserver(() => {
          frame.style.height = Math.ceil(content.getBoundingClientRect().height) + 2 + 'px';
        });
        observer.observe(content);
        frame.contentWindow.addEventListener('pagehide', () => observer.disconnect(), {once:true});
      };
      frame.src = doc.reviewPage;
      preview.append(frame);
    }
  }
  $('raw').hidden = $('delete').disabled = !rev;
  $('raw').href = '/config/' + kind + '/' + doc.id + '.json';
  $('path').textContent = 'config/' + kind + '/' + doc.id + '.json';
  $('status').textContent = '';
  const url = new URL(location.href);
  url.searchParams.set('id', doc.id);
  history.replaceState(null, '', url);
  sidebar();
}
function open(id) {
  const found = entries.find((e) => e.doc.id === id) || entries[0];
  if (found) display(found.doc, found.revision);
  else {
    doc = null;
    view3d?.clear();
    dirty = false;
    $('editor').hidden = true;
    sidebar();
    $('status').textContent = '尚无记录，可以新建。';
  }
}
async function refresh(id) {
  const library = await list(kind);
  entries = library.entries;
  $('errors').textContent = library.errors.map((e) => e.file + ' · ' + e.message).join('\n');
  open(id);
}
$('new').onclick = () => {
  if (!canLeave()) return;
  const id = crypto.randomUUID();
  display({
    id,
    name: scene ? '新场景' : '新策划案',
    summary: '',
    ...(scene ? { width: 20, depth: 14, objects: [] } : { status: '草稿', body: '' }),
  });
  changed();
  $('name').select();
};
$('filter').oninput = sidebar;
$('reload').onclick = () => {
  if (canLeave()) refresh(doc?.id).catch(error);
};
$('editor').addEventListener('input', changed);
function error(e) {
  $('status').textContent = e.message;
}
function lock(v) {
  busy = v;
  $('editor').inert = v;
  $('new').disabled = $('reload').disabled = v;
}
$('editor').onsubmit = async (e) => {
  e.preventDefault();
  if (busy) return;
  lock(true);
  try {
    if (view3d?.pending) throw Error('场景模型仍在导入，请完成后保存。');
    doc.name = $('name').value.trim();
    doc.summary = $('summary').value;
    if (!scene) {
      doc.status = $('planStatus').value.trim();
      doc.body = $('body').value;
    }
    const record = await save(kind, doc, revision);
    entries = entries.filter((e) => e.doc.id !== doc.id);
    entries.push(record);
    display(record.doc, record.revision);
    $('status').textContent = '已保存到文件';
  } catch (e) {
    error(e);
  } finally {
    lock(false);
  }
};
$('delete').onclick = async () => {
  if (busy || !confirm('删除「' + doc.name + '」的配置文件？')) return;
  lock(true);
  try {
    await remove(kind, doc.id, revision);
    await refresh();
  } catch (e) {
    error(e);
  } finally {
    lock(false);
  }
};
window.addEventListener('beforeunload', (e) => {
  if (dirty) {
    e.preventDefault();
    e.returnValue = '';
  }
});
await refresh(new URLSearchParams(location.search).get('id')).catch(error);
