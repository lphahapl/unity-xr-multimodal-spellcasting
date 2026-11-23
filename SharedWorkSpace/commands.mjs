import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import http from 'node:http';
import { setTimeout as delay } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';
import { mkdir, readFile, readdir, realpath, rename, rm, stat, writeFile } from 'node:fs/promises';
import { digest, existingWithin, fail, isId, kinds, validate } from './records.mjs';
import { capturePreview } from './preview.mjs';

const encode = (v) => JSON.stringify(v, null, 2) + '\n';
const versionHash = /^[a-f0-9]{64}$/;
const check = (v, msg) => {
  if (!v) fail(422, msg);
};
const maxBytes = 8 * 1024 * 1024;

async function optional(read) {
  try {
    return await read();
  } catch (e) {
    if (e.code === 'ENOENT') return null;
    throw e;
  }
}
async function publish(file, bytes) {
  const tmp = path.join(path.dirname(file), '.' + randomUUID() + '.tmp');
  try {
    await writeFile(tmp, bytes, { flag: 'wx' });
    await rename(tmp, file);
  } finally {
    await rm(tmp, { force: true });
  }
}
function localFolder(root) {
  return path.join(
    process.env.LOCALAPPDATA || os.homedir(),
    'NarutoVR-Workspaces',
    digest(root).slice(0, 24),
  );
}

function desired(command, current) {
  const { op, kind, target, doc } = command;
  if (op === 'delete') return null;
  if (op === 'create' || op === 'replace') {
    validate(kind, target, doc);
    return doc;
  }
  const next = structuredClone(current.doc);
  if (op === 'scene.set') {
    check(kind === 'scenes' && (next.mode ?? 'whitebox') === 'whitebox', 'scene.set 需要白盒场景');
    const item = next.objects.find((o) => o.id === command.objectId);
    const keys = Object.keys(command.fields || {});
    check(
      item &&
        keys.length &&
        keys.every((k) =>
          ['x', 'y', 'z', 'w', 'h', 'd', 'rotation', 'visible', 'name', 'type', 'shape'].includes(k),
        ),
      '场景元素或字段无效',
    );
    Object.assign(item, command.fields);
  } else {
    check(op === 'rig.note' && kind === 'rig', '未知工作台命令');
    const v = next.versions.find((v) => v.id === command.versionId);
    check(v, '没有该模型版本');
    v.notes.push({ id: command.id, created: Date.now(), text: command.text });
  }
  validate(kind, target, next);
  return next;
}

export async function commandReceiver(root, store, stateDir, origin) {
  const inbox = path.join(root, 'inbox'),
    results = path.join(root, 'results');
  for (const folder of [inbox, results]) {
    await mkdir(folder, { recursive: true });
    await existingWithin(root, folder);
  }
  await mkdir(stateDir, { recursive: true });
  const observed = new Map();
  async function run(file, id) {
    const meta = await stat(file);
    if (meta.size > maxBytes) fail(413, '命令大于 8 MiB');
    const bytes = await readFile(file);
    if (bytes.length > maxBytes) fail(413, '命令大于 8 MiB');
    const hash = digest(bytes),
      journal = path.join(stateDir, id + '.json');
    const receipt = path.join(results, id + '.json');
    let state = await optional(async () => JSON.parse(await readFile(journal, 'utf8')));
    if (!state) {
      const synced = await optional(async () =>
        JSON.parse(await readFile(await existingWithin(results, receipt), 'utf8')),
      );
      if (
        synced?.id === id &&
        versionHash.test(synced.requestHash) &&
        ['ok', 'error', 'conflict'].includes(synced.status)
      )
        state = { hash: synced.requestHash, receipt: synced };
    }
    if (state && state.hash !== hash && state.receipt?.code === 400) state = null;
    if (state && state.hash !== hash) {
      await publish(
        path.join(results, id + '.conflict-' + hash.slice(0, 12) + '.json'),
        encode({ id, status: 'conflict', error: '命令 ID 已使用，请新建 ID', requestHash: hash }),
      );
      return;
    }
    if (state?.receipt) {
      const saved = encode(state.receipt);
      if ((await optional(() => readFile(receipt, 'utf8'))) !== saved) await publish(receipt, saved);
      return;
    }
    let reply = { id, requestHash: hash };
    try {
      let c;
      try {
        c = JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, ''));
      } catch {
        fail(400, '命令 JSON 尚不完整或语法有误');
      }
      check(
        c && c.id === id && kinds.includes(c.kind) && isId(c.target),
        '文件名、命令 id、kind 或 target 无效',
      );
      const { op, kind, target } = c;
      check(
        ['read', 'preview', 'create', 'replace', 'delete', 'scene.set', 'rig.note'].includes(op),
        '不支持的操作',
      );
      Object.assign(reply, { op, kind, target });
      const current = await optional(() => store.get(kind, target));
      if (op === 'read' || op === 'preview') {
        if (!current) fail(404, '目标不存在');
        if (c.baseRevision !== undefined && c.baseRevision !== current.revision)
          fail(412, '记录版本已变化');
        if (op === 'read') Object.assign(reply, current);
        else {
          const preview = await capturePreview(origin, c, current.doc);
          if ((await store.get(kind, target)).revision !== current.revision)
            fail(412, '渲染期间记录发生变化');
          await publish(path.join(results, id + '.png'), preview.image);
          Object.assign(reply, {
            revision: current.revision,
            image: 'results/' + id + '.png',
            details: preview.details,
          });
        }
      } else {
        if (!state) {
          if (op === 'create') {
            if (current) fail(409, '目标已存在');
          } else {
            check(versionHash.test(c.baseRevision ?? ''), '修改或删除需要 read 返回的 baseRevision');
            if (!current || current.revision !== c.baseRevision) fail(412, '记录版本已变化，请重新读取');
          }
          const next = desired(c, current);
          state = { hash, next, after: next === null ? null : digest(encode(next)) };
          await publish(journal, encode(state));
        }
        // A crash can occur after the record commit but before its receipt is published.
        if ((current?.revision ?? null) !== state.after) {
          await store.change(kind, target, state.next, '"' + c.baseRevision + '"', op === 'create');
        }
        reply.revision = state.after;
        if (op === 'scene.set') reply.object = state.next.objects.find((o) => o.id === c.objectId);
        if (op === 'rig.note')
          reply.note = state.next.versions
            .find((v) => v.id === c.versionId)
            .notes.find((n) => n.id === c.id);
      }
      reply.status = 'ok';
    } catch (e) {
      if (!e.status) throw e;
      Object.assign(reply, {
        status: [409, 412].includes(e.status) ? 'conflict' : 'error',
        code: e.status,
        error: e.message,
      });
    }
    reply.finishedAt = new Date().toISOString();
    await publish(journal, encode({ hash, receipt: reply }));
    await publish(receipt, encode(reply));
  }
  let scanning = false;
  return async function scan() {
    if (scanning) return;
    scanning = true;
    try {
      if ((await realpath(inbox)) !== inbox || (await realpath(results)) !== results)
        fail(403, '收件与回执目录需要是本地工作台内的普通目录');
      const files = await readdir(inbox, { withFileTypes: true });
      const seen = new Set();
      for (const f of files) {
        const id = f.name.slice(0, -5);
        if (!f.isFile() || !f.name.endsWith('.json') || !isId(id)) continue;
        seen.add(id);
        try {
          const file = await existingWithin(inbox, path.join(inbox, f.name));
          const s = await stat(file),
            key = [s.size, s.mtimeMs, s.ctimeMs].join('/');
          const prev = observed.get(id);
          if (prev?.key !== key) {
            observed.set(id, { key });
            continue;
          }
          if (prev.done) continue;
          if (s.size > maxBytes) {
            await publish(
              path.join(results, id + '.oversize.json'),
              encode({ id, status: 'error', code: 413, error: '命令大于 8 MiB' }),
            );
          } else await run(file, id);
          prev.done = true;
        } catch (e) {
          console.error('Command ' + id + ': ' + e.message);
        }
      }
      for (const id of observed.keys()) if (!seen.has(id)) observed.delete(id);
    } finally {
      scanning = false;
    }
  };
}

async function receiverLock(root) {
  // A Windows named pipe is released by the OS even if the process terminates.
  const lock = net.createServer((socket) => socket.end());
  const acquired = await new Promise((resolve, reject) => {
    lock.once('error', (e) => (e.code === 'EADDRINUSE' ? resolve(false) : reject(e)));
    lock.listen('\\\\.\\pipe\\narutovr-commands-' + digest(root).slice(0, 24), () => resolve(true));
  });
  return acquired ? lock : null;
}

export async function startReceiver(root, store, port) {
  const stateDir = localFolder(root);
  const setting = await optional(async () =>
    JSON.parse(await readFile(path.join(stateDir, 'enabled.json'), 'utf8')),
  );
  if (setting?.port !== port) return false;
  if (process.platform === 'win32') while (!(await receiverLock(root))) await delay(500);
  const scan = await commandReceiver(
    root,
    store,
    path.join(stateDir, 'journal'),
    'http://127.0.0.1:' + port,
  );
  const run = () => scan().catch((e) => console.error('Command inbox: ' + e.message));
  await run();
  setInterval(run, 2000).unref();
  return true;
}

// Migration path for a running pre-inbox Windows service. Its existing store
// still serializes every change alongside edits from the browser.
async function attach(root, folder, port) {
  if (process.platform !== 'win32') throw Error('热接入适用于 Windows 执行机');
  const origin = 'http://127.0.0.1:' + port,
    lock = await receiverLock(root);
  if (!lock) return;
  const request = (route, options = {}) =>
    new Promise((resolve, reject) => {
      const req = http.request(
        origin + route,
        { method: options.method, headers: options.headers, signal: AbortSignal.timeout(10000) },
        (res) => {
          const chunks = [];
          res.on('data', (chunk) => chunks.push(chunk));
          res.on('error', reject);
          res.on('end', () => {
            try {
              resolve({
                status: res.statusCode,
                body: JSON.parse(Buffer.concat(chunks).toString('utf8')),
              });
            } catch (e) {
              reject(e);
            }
          });
        },
      );
      req.on('error', reject);
      req.end(options.body);
    });
  async function legacy() {
    const identity = await request('/api/identity');
    if (identity.body.root !== root) throw Error('工作台目录不一致');
    return (await request('/api/receiver')).status === 404;
  }
  async function call(k, id, options) {
    if (!(await legacy())) throw Error('收件任务正交回主服务');
    const res = await request('/api/' + k + '/' + id, options);
    const data = res.body;
    if (res.status === 404) throw Object.assign(Error('目标不存在'), { code: 'ENOENT' });
    if (res.status >= 400) fail(res.status, data.error);
    return data;
  }
  const store = {
    get: (k, id) => call(k, id),
    change: (k, id, doc, expected, create) =>
      call(k, id, {
        method: doc === null ? 'DELETE' : 'PUT',
        headers: {
          'Content-Type': 'application/json',
          ...(create ? { 'If-None-Match': '*' } : { 'If-Match': expected }),
        },
        body: doc === null ? undefined : JSON.stringify(doc),
      }),
  };
  try {
    const setting = await optional(async () =>
      JSON.parse(await readFile(path.join(folder, 'enabled.json'), 'utf8')),
    );
    if (setting?.port !== port || !(await legacy())) return;
    const scan = await commandReceiver(root, store, path.join(folder, 'journal'), origin);
    while (await legacy()) {
      await scan();
      await delay(2000);
    }
  } finally {
    lock.close();
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = await realpath(path.dirname(fileURLToPath(import.meta.url)));
  const folder = localFolder(root),
    flag = path.join(folder, 'enabled.json');
  if (process.argv[2] === '--enable') {
    await mkdir(folder, { recursive: true });
    await publish(flag, encode({ port: Number(process.env.RIG_WORKBENCH_PORT || 4190) }));
  } else if (process.argv[2] === '--disable') await rm(flag, { force: true });
  else if (process.argv[2] === '--attach')
    await attach(root, folder, Number(process.env.RIG_WORKBENCH_PORT || 4190));
  else throw Error('请使用 --enable 或 --disable，然后重启工作台服务');
}
