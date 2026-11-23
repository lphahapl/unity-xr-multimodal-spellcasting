import { list, get, save, storeFile } from '../ui/data.js';

import { readFiles } from '../ui/models.js';
export async function listLibrary() {
  const { entries, errors } = await list('rig');
  const collections = entries.map(({ doc }) => ({
    id: doc.id, name: doc.name, defaultVersion: doc.defaultVersion,
  }));
  const versions = entries
    .flatMap(({ doc }) => doc.versions.map((v) => ({ ...v, collectionId: doc.id })))
    .sort((a, b) => b.created - a.created);
  return { collections, versions, errors };
}
export async function saveImport(newCollection, version, files) {
  const paths = [];
  for (const file of files) paths.push(await storeFile(file));
  const { collectionId, ...fields } = version;
  const record = newCollection
    ? { doc: { id: collectionId, name: newCollection.name, versions: [] } }
    : await get('rig', collectionId);
  record.doc.versions.push({ ...fields, files: paths, notes: [] });
  await save('rig', record.doc, record.revision);
}
export async function versionFiles(version) {
  return readFiles(version.files);
}
function find(doc, id) {
  const version = doc.versions.find((v) => v.id === id);
  if (!version) throw Error('此版本已移除');
  return version;
}
export async function versionNotes(id, collectionId) {
  return find((await get('rig', collectionId)).doc, id).notes;
}
export async function addNote(id, text, collectionId) {
  const { doc, revision } = await get('rig', collectionId);
  find(doc, id).notes.push({ id: crypto.randomUUID(), text, created: Date.now() });
  await save('rig', doc, revision);
}
