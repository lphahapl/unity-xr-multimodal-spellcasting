import { listLibrary, saveImport, versionFiles, versionNotes, addNote } from './library.js';
import { fileURL } from '../ui/data.js';

const $ = (id) => document.getElementById(id);
const newId = () => crypto.randomUUID();
const empty = '';
const channel = new BroadcastChannel('RigWorkbenchLibrary');

export async function startLibrary(display, setStatus) {
  let library = await listLibrary();
  let current,
    pending,
    busy = false,
    editingVersion,
    noteSaving = false;
  const drafts = new Map();
  const forCollection = (id) => library.versions.filter((v) => v.collectionId === id);
  function firstVersion(id) {
    const preferred = library.collections.find((c) => c.id === id)?.defaultVersion;
    const versions = forCollection(id);
    return versions.find((v) => v.id === preferred) || versions[0];
  }
  const options = (rows, label) => rows.map((row) => new Option(row[label], row.id));
  const importDialog = $('importDialog'),
    notesDialog = $('notesDialog');

  function controls() {
    $('collection').disabled = $('version').disabled = $('open').disabled = busy;
    $('annotations').disabled = busy || !current;
    $('turnaround').hidden = !current?.turnaround;
    $('turnaround').disabled = busy;
    for (const field of $('importForm').elements) field.disabled = busy;
  }
  $('turnaround').onclick = () => {
    const url = fileURL(current.turnaround);
    $('turnaroundOriginal').href = url;
    $('turnaroundImage').src = url;
    $('turnaroundImage').alt = current.label + ' · 全身三视图';
    $('turnaroundDialog').showModal();
  };
  function menus() {
    $('collection').replaceChildren(...options(library.collections, 'name'));
    if (current) $('collection').value = current.collectionId;
    $('version').replaceChildren(...options(forCollection($('collection').value), 'label'));
    if (current) $('version').value = current.id;
  }
  function remember(version) {
    current = version;
    const url = new URL(location.href);
    url.searchParams.set('model', version.id);
    url.searchParams.set('collection', version.collectionId);
    history.replaceState(null, '', url);
    menus();
  }
  async function choose(version) {
    if (busy) return;
    if (!version) {
      current = null;
      await display(null);
      controls();
      $('version').replaceChildren();
      const url = new URL(location.href);
      url.searchParams.delete('model');
      if ($('collection').value) url.searchParams.set('collection', $('collection').value);
      else url.searchParams.delete('collection');
      history.replaceState(null, '', url);
      setStatus('集合中尚无模型版本，可以导入第一版。');
      return;
    }
    busy = true;
    controls();
    try {
      const files = await versionFiles(version);
      if (!files) throw Error('缺少此版本的本地文件');
      await display(version, files);
      remember(version);
    } catch (error) {
      setStatus('载入失败 · ' + error.message);
      menus();
    } finally {
      busy = false;
      controls();
    }
  }
  $('collection').onchange = () => choose(firstVersion($('collection').value));
  $('version').onchange = () =>
    choose(forCollection($('collection').value).find((v) => v.id === $('version').value));
  function importFields() {
    const dest = $('destination').value;
    $('newNameRow').hidden = dest !== 'new';
    $('newName').required = dest === 'new';
    const versions = forCollection(dest);
    let n = versions.length + 1;
    while (versions.some((v) => v.label === 'V' + n)) n++;
    $('versionName').value = 'V' + n;
  }
  function askImport(list) {
    if (busy || notesDialog.open || importDialog.open) return;
    const files = Array.from(list),
      models = files.filter((f) => /\.(fbx|glb|gltf)$/i.test(f.name));
    if (models.length !== 1) {
      setStatus('每次请选择一个模型，可以同时选择贴图和 BIN 文件');
      return;
    }
    pending = { files, model: models[0] };
    $('importFile').textContent = pending.model.name;
    $('destination').replaceChildren(
      new Option('选择新建或已有集合', empty),
      new Option('＋ 新建人物集合', 'new'),
      ...options(library.collections, 'name'),
    );
    $('newName').value = pending.model.name.replace(/\.[^.]+$/, '');
    $('versionName').value = '';
    $('newNameRow').hidden = true;
    $('newName').required = false;
    $('importError').textContent = '';
    importDialog.showModal();
    $('destination').focus();
  }
  $('destination').onchange = importFields;
  $('open').onclick = () => $('files').click();
  $('files').onchange = (e) => {
    askImport(e.target.files);
    e.target.value = '';
  };
  window.addEventListener('dragover', (e) => e.preventDefault());
  window.addEventListener('drop', (e) => {
    e.preventDefault();
    askImport(e.dataTransfer.files);
  });
  $('cancelImport').onclick = () => importDialog.close();
  importDialog.oncancel = (e) => {
    if (busy) e.preventDefault();
  };
  importDialog.onclose = () => {
    if (!importDialog.open) pending = null;
  };
  $('importForm').onsubmit = async (e) => {
    e.preventDefault();
    if (!pending || busy) return;
    const dest = $('destination').value,
      name = $('newName').value.trim(),
      label = $('versionName').value.trim();
    const error = $('importError');
    if (!label || (dest === 'new' && !name)) {
      error.textContent = '请填写人物名和版本名。';
      return;
    }
    if (
      (dest === 'new' && library.collections.some((c) => c.name === name)) ||
      forCollection(dest).some((v) => v.label === label)
    ) {
      error.textContent = '名称已使用，请选已有集合或换一个版本名。';
      return;
    }
    const created = Date.now();
    const collection = dest === 'new' ? { id: newId(), name, created } : null;
    const version = {
      id: newId(),
      collectionId: collection?.id ?? dest,
      label,
      created,
      filename: pending.model.name,
    };
    const { files } = pending;
    busy = true;
    controls();
    error.textContent = '正在检查模型并保存…';
    try {
      await display(version, files, () => saveImport(collection, version, files));
      library = await listLibrary();
      remember(version);
      importDialog.close();
      channel.postMessage({ library: true });
    } catch (err) {
      error.textContent =
        err.name === 'QuotaExceededError'
          ? '浏览器存储空间不足。'
          : err.name === 'ConstraintError'
            ? '该集合名或版本名已使用。'
            : '导入失败 · ' + err.message;
      setStatus(error.textContent);
    } finally {
      busy = false;
      controls();
    }
  };
  async function showHistory(id) {
    const notes = await versionNotes(id, current.collectionId);
    if (editingVersion !== id || !notesDialog.open) return;
    const history = $('noteHistory');
    history.replaceChildren();
    if (!notes.length) {
      history.textContent = '这个版本还没有批注。';
      return;
    }
    for (const note of notes) {
      const article = document.createElement('article'),
        date = document.createElement('time'),
        body = document.createElement('div');
      date.dateTime = new Date(note.created).toISOString();
      date.textContent = new Date(note.created).toLocaleString('zh-CN', { hour12: false });
      body.textContent = note.text;
      article.append(date, body);
      history.append(article);
    }
    history.scrollTop = history.scrollHeight;
  }
  $('annotations').onclick = async () => {
    if (!current || busy) return;
    editingVersion = current.id;
    const group = library.collections.find((c) => c.id === current.collectionId);
    $('noteVersion').textContent = group.name + ' / ' + current.label;
    $('noteText').value = drafts.get(current.collectionId + '/' + current.id) || '';
    $('notesError').textContent = '';
    $('noteHistory').replaceChildren();
    notesDialog.showModal();
    try {
      await showHistory(current.id);
    } catch (e) {
      $('notesError').textContent = e.message;
    }
  };
  function closeNotes() {
    if (noteSaving) return;
    if (editingVersion) drafts.set(current.collectionId + '/' + editingVersion, $('noteText').value);
    editingVersion = null;
    notesDialog.close();
  }
  $('closeNotes').onclick = closeNotes;
  notesDialog.oncancel = (e) => {
    e.preventDefault();
    closeNotes();
  };
  $('notesForm').onsubmit = async (e) => {
    e.preventDefault();
    const text = $('noteText').value.trim(),
      id = editingVersion;
    if (!text || !id || noteSaving) return;
    noteSaving = true;
    $('closeNotes').disabled = $('saveNote').disabled = $('noteText').disabled = true;
    try {
      await addNote(id, text, current.collectionId);
      drafts.delete(current.collectionId + '/' + id);
      $('noteText').value = '';
      await showHistory(id);
      $('notesError').textContent = '';
      channel.postMessage({ notes: id });
    } catch (err) {
      $('notesError').textContent = '保存失败 · ' + err.message;
    } finally {
      noteSaving = false;
      $('closeNotes').disabled = $('saveNote').disabled = $('noteText').disabled = false;
    }
  };
  channel.onmessage = async (event) => {
    if (event.data.library) {
      library = await listLibrary();
      if (!busy) menus();
    }
    if (event.data.notes && event.data.notes === editingVersion) await showHistory(editingVersion);
  };
  menus();
  $('reloadLibrary').onclick = async () => {
    if (busy) return;
    try {
      const selected = $('collection').value;
      library = await listLibrary();
      menus();
      const group = library.collections.some((c) => c.id === selected)
        ? selected
        : library.collections[0]?.id;
      $('collection').value = group || '';
      await choose(forCollection(group).find((v) => v.id === current?.id) || firstVersion(group));
      reportErrors();
    } catch (e) {
      setStatus(e.message);
    }
  };
  function reportErrors() {
    $('libraryErrors').textContent = library.errors.map((e) => e.file + ' · ' + e.message).join('；');
  }
  reportErrors();
  const requested = new URL(location.href).searchParams.get('model') || 'sample';
  const group = new URL(location.href).searchParams.get('collection');
  const initial = library.versions.find(
    (v) => v.id === requested && (!group || v.collectionId === group),
  );
  if (group && library.collections.some((c) => c.id === group)) {
    $('collection').value = group;
    await choose(initial || firstVersion(group));
  } else await choose(initial || library.versions.find((v) => v.id === 'sample') || library.versions[0]);
}
