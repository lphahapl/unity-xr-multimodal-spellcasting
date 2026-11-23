import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import os from 'node:os';
import { mkdtemp, mkdir, writeFile, readFile, readdir, rm, realpath } from 'node:fs/promises';
import { recordStore } from '../records.mjs';
import { commandReceiver } from '../commands.mjs';

test('Drive inbox, local edits, receipts and interrupted writes', async (t) => {
  const root = await realpath(await mkdtemp(path.join(os.tmpdir(), 'ws-inbox-test-')));
  t.after(() => rm(root, { recursive: true, force: true }));
  for (const k of ['scenes', 'rig', 'plans'])
    await mkdir(path.join(root, 'config', k), { recursive: true });
  const store = recordStore(root),
    journal = path.join(root, 'test-journal');
  let worker = await commandReceiver(root, store, journal);
  const input = (id) => path.join(root, 'inbox', id + '.json');
  const result = async (id) =>
    JSON.parse(await readFile(path.join(root, 'results', id + '.json'), 'utf8'));
  async function send(c) {
    await writeFile(input(c.id), JSON.stringify(c));
    await worker();
    await worker();
    return result(c.id);
  }
  const scene = {
    id: 'room',
    name: 'Room',
    width: 10,
    depth: 10,
    summary: '',
    objects: [{ id: 'gate', name: 'Gate', type: 'box', x: 1, z: 1, w: 1, d: 1 }],
  };
  const dest = { kind: 'scenes', target: 'room' };
  const put = await send({ ...dest, id: 'first', op: 'create', doc: scene });
  const read = await send({ ...dest, id: 'read', op: 'read' });
  assert.equal(put.revision, read.revision);
  const move = {
    ...dest,
    id: 'move',
    op: 'scene.set',
    objectId: 'gate',
    baseRevision: read.revision,
    fields: { x: 2 },
  };
  const edited = await send(move);
  assert.equal(edited.object.x, 2);
  assert.equal((await send({ ...move, id: 'stale', fields: { x: 4 } })).status, 'conflict');
  assert.equal(
    (await send({ ...move, id: 'invalid', baseRevision: edited.revision, fields: { x: 14 } })).code,
    422,
  );
  assert.equal((await send({ ...move, fields: { x: 3 } })).object.x, 2);
  assert((await readdir(path.join(root, 'results'))).some((f) => f.startsWith('move.conflict-')));
  const draft = { id: 'draft', name: 'Draft', body: 'First', status: 'draft', summary: '' },
    plan = { kind: 'plans', target: 'draft' };
  const made = await send({ ...plan, id: 'p-create', op: 'create', doc: draft });
  const e = await send({
    ...plan,
    id: 'p-edit',
    op: 'replace',
    baseRevision: made.revision,
    doc: { ...draft, body: 'Second' },
  });
  assert.equal((await store.get('plans', 'draft')).doc.body, 'Second');
  assert.equal(
    (await send({ ...plan, id: 'p-delete', op: 'delete', baseRevision: e.revision })).status,
    'ok',
  );
  await assert.rejects(store.get('plans', 'draft'), { code: 'ENOENT' });

  const asset = {
    id: 'hero',
    name: 'Hero',
    versions: [
      { id: 'v1', label: 'model', filename: 'm.glb', created: 0, files: ['hero/m.glb'], notes: [] },
    ],
  };
  const rig = { kind: 'rig', target: 'hero' };
  const first = await send({ ...rig, id: 'rig', op: 'create', doc: asset });
  const note = {
    ...rig,
    id: 'note',
    op: 'rig.note',
    versionId: 'v1',
    text: 'wrist',
    baseRevision: first.revision,
  };
  let writes = 0;
  worker = await commandReceiver(
    root,
    {
      ...store,
      async change(...args) {
        await store.change(...args);
        writes++;
        throw Error('simulated interruption');
      },
    },
    journal,
  );
  await writeFile(input(note.id), JSON.stringify(note));
  await worker();
  await worker();
  assert.equal(writes, 1);
  worker = await commandReceiver(root, store, journal);
  await worker();
  await worker();
  assert.equal((await result('note')).note.text, 'wrist');
  assert.equal((await store.get('rig', 'hero')).doc.versions[0].notes.length, 1);
  await writeFile(input('partial'), '{');
  await worker();
  await worker();
  assert.equal((await result('partial')).code, 400);
  assert.equal((await send({ ...dest, id: 'partial', op: 'read' })).doc.objects[0].x, 2);
  assert.equal((await send({ ...dest, id: 'invalid-op', op: 'script' })).code, 422);
});
