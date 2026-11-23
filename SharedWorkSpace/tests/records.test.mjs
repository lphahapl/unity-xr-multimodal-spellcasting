import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { recordStore, validate } from '../records.mjs';

test('separate hand and cuff slots reference images from the same version', () => {
  const version = {
    id: 'left', label: 'Left', filename: 'hand.fbx', created: 0, notes: [],
    files: ['hands/hand.fbx', 'hands/body.png', 'hands/cuff.png'],
  };
  const check = (slots) => validate('rig', 'hands', {
    id: 'hands', name: 'Hands', versions: [{ ...version, slotAlbedos: slots }],
  });
  check(undefined);
  check({ HandBody: 'body.png', ForearmEnd: 'cuff.png' });
  for (const slots of [{}, { HandBody: 'missing.png' }, { ForearmEnd: 'hand.fbx' }])
    assert.throws(() => check(slots), { status: 422 });
});

test('hand edits, conflicting writes, invalid sync copies and conditional deletion', async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'shared-workbench-test-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(path.join(root, 'config/plans'), { recursive: true });
  const store = recordStore(root),
    doc = { id: 'brief', name: 'Draft', status: 'open', summary: '', body: 'v1' };
  const initial = await store.change('plans', doc.id, doc, null, true);
  await assert.rejects(store.change('plans', doc.id, doc, null, true), { status: 409 });
  const edit = async (body) =>
    store.change('plans', doc.id, { ...doc, body }, '"' + initial.revision + '"', false);
  const concurrent = await Promise.allSettled([edit('one'), edit('two')]);
  assert.equal(concurrent.filter((r) => r.status === 'fulfilled').length, 1);
  assert.equal(concurrent.find((r) => r.status === 'rejected').reason.status, 412);
  const old = await store.get('plans', doc.id);
  await writeFile(
    path.join(root, 'config/plans/brief.json'),
    '\uFEFF' + JSON.stringify({ ...doc, body: 'manual' }),
  );
  await assert.rejects(store.change('plans', doc.id, null, '"' + old.revision + '"', false), {
    status: 412,
  });
  await writeFile(path.join(root, 'config/plans/broken.json'), '{');
  const result = await store.list('plans');
  assert.equal(result.entries[0].doc.body, 'manual');
  assert.equal(result.errors[0].file, 'broken.json');
  await store.change('plans', doc.id, null, '"' + result.entries[0].revision + '"', false);
  assert.equal((await store.list('plans')).entries.length, 0);
});
