import { get, save, storeFile } from './data.js';

async function loadOldSnapshot() {
  if (location.origin !== 'http://127.0.0.1:4190')
    throw Error('请在 127.0.0.1:4190 的配置与检索页面迁入旧资料。');
  const old = 'http://127.0.0.1:4186';
  const token = crypto.randomUUID(),
    frame = document.createElement('iframe');
  frame.hidden = true;
  return new Promise((resolve, reject) => {
    function finish() {
      clearTimeout(timer);
      window.removeEventListener('message', receive);
      frame.remove();
    }
    function receive(e) {
      if (e.source !== frame.contentWindow || e.origin !== old || e.data?.token !== token) return;
      finish();
      if (e.data.error) reject(Error(e.data.error));
      else resolve(e.data.snapshot);
    }
    const timer = setTimeout(() => {
      finish();
      reject(Error('未收到旧版工作台响应，请核对本机 4186 服务及 Chrome 配置。'));
    }, 30000);
    window.addEventListener('message', receive);
    frame.onload = () => frame.contentWindow.postMessage({ type: 'read-rig-library', token }, old);
    frame.src = old + '/?legacy-export';
    document.body.append(frame);
  });
}
export async function migrate() {
  const snapshot = await loadOldSnapshot();
  if (!snapshot) return '当前 Chrome 配置没有旧模型库。';
  const { collections, versions, notes } = snapshot;
  let added = 0,
    notesAdded = 0;
  for (const group of collections) {
    let record;
    try {
      record = await get('rig', group.id);
    } catch (e) {
      if (e.status !== 404) throw e;
      record = { doc: { id: group.id, name: group.name, versions: [] } };
    }
    let dirty = false;
    for (const v of versions.filter((v) => v.collectionId === group.id)) {
      let target = record.doc.versions.find((i) => i.id === v.id);
      if (!target) {
        const files = [];
        if (v.sampleId) throw Error('预置集合已从文件移除，请先恢复该集合再迁入其旧批注。');
        const originals = snapshot.files.get(v.id);
        if (!originals?.length) throw Error('旧库缺少附件 · ' + v.filename);
        for (const f of originals) files.push(await storeFile(f));
        target = {
          id: v.id,
          label: v.label,
          created: v.created,
          filename: v.filename,
          files,
          notes: [],
        };
        record.doc.versions.push(target);
        dirty = true;
        added++;
      }
      for (const n of notes.filter((n) => n.versionId === v.id)) {
        if (target.notes.some((t) => t.text === n.text && t.created === n.created)) continue;
        target.notes.push({ id: crypto.randomUUID(), text: n.text, created: n.created });
        notesAdded++;
        dirty = true;
      }
    }
    if (dirty || !record.revision) await save('rig', record.doc, record.revision);
  }
  return '迁入 ' + added + ' 个模型版本、' + notesAdded + ' 条批注。';
}
