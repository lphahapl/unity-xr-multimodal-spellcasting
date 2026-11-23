import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createReadStream, createWriteStream, constants } from 'node:fs';
import { copyFile, mkdir, readFile, realpath, stat, unlink } from 'node:fs/promises';
import { createHash, randomUUID } from 'node:crypto';
import { Transform } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { kinds, isId, fail, allowedAsset, assetPath, existingWithin, recordStore } from './records.mjs';
import { startReceiver } from './commands.mjs';

const root = await realpath(path.dirname(fileURLToPath(import.meta.url)));
const store = recordStore(root);
const port = Number(process.env.RIG_WORKBENCH_PORT || 4190);
let receiverStatus = { enabled: false };
const publicDirs = new Set(['rig', 'plans', 'scenes', 'reader', 'ui', 'vendor']);
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.md': 'text/plain; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
};
function json(res, status, value, extra = {}) {
  res
    .writeHead(status, { 'Content-Type': types['.json'], ...extra })
    .end(JSON.stringify(value, null, 2));
}
async function body(req) {
  const chunks = [];
  let n = 0;
  for await (const c of req) {
    n += c.length;
    if (n > 8 * 1024 * 1024) fail(413, '配置文件过大');
    chunks.push(c);
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch {
    fail(400, '请求正文需要有效 JSON');
  }
}
async function upload(req, res, name) {
  if (!allowedAsset(name)) fail(422, '附件文件名或类型无效');
  const dir = await existingWithin(root, path.join(root, 'assets'));
  const temp = path.join(dir, '.upload-' + randomUUID() + '.tmp');
  const hash = createHash('sha256');
  let size = 0;
  const meter = new Transform({
    transform(chunk, _, cb) {
      size += chunk.length;
      if (size > 512 * 1024 * 1024)
        return cb(Object.assign(Error('单个附件上限为 512 MiB'), { status: 413 }));
      hash.update(chunk);
      cb(null, chunk);
    },
  });
  try {
    await pipeline(req, meter, createWriteStream(temp, { flags: 'wx' }));
    const key = hash.digest('hex'),
      bucket = path.join(dir, key);
    await mkdir(bucket, { recursive: true });
    await existingWithin(dir, bucket);
    const target = path.join(bucket, name);
    try {
      await copyFile(temp, target, constants.COPYFILE_EXCL);
    } catch (e) {
      if (e.code !== 'EEXIST') throw e;
    }
    json(res, 201, { path: key + '/' + name, size });
  } finally {
    await unlink(temp).catch((e) => {
      if (e.code !== 'ENOENT') throw e;
    });
  }
}
async function serve(req, res, file) {
  const target = await existingWithin(root, file);
  const info = await stat(target);
  if (!info.isFile()) fail(404, '文件不存在');
  res.writeHead(200, {
    'Content-Type': types[path.extname(target).toLowerCase()] || 'application/octet-stream',
    'Content-Length': info.size,
  });
  if (req.method === 'HEAD') res.end();
  else await pipeline(createReadStream(target), res);
}
const server = http.createServer(async (req, res) => {
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  try {
    const hosts = ['127.0.0.1:' + port, 'localhost:' + port];
    if (!hosts.includes(req.headers.host)) fail(403, '请使用本机工作台地址');
    const url = new URL(req.url, 'http://' + req.headers.host);
    const parts = decodeURIComponent(url.pathname).split('/').filter(Boolean);
    if (
      parts.some(
        (p) => p === '.' || p === '..' || p.includes('\\') || p.includes(':') || p.startsWith('.'),
      )
    )
      fail(400, '无效路径');
    const [area, kind, id] = parts,
      method = req.method,
      read = method === 'GET' || method === 'HEAD';
    if (
      !read &&
      (req.headers['sec-fetch-site'] === 'cross-site' ||
        (req.headers.origin && req.headers.origin !== url.origin))
    )
      fail(403, '写入须由同源工作台发起');
    if (area === 'api') {
      if (method === 'GET' && kind === 'receiver' && parts.length === 2)
        return json(res, 200, receiverStatus);
      if (method === 'GET' && kind === 'identity' && parts.length === 2)
        return json(res, 200, { app: 'SharedWorkSpace', root });
      if (method === 'GET' && kind === 'context' && parts.length === 2) {
        const workspace = JSON.parse(
          (await readFile(path.join(root, 'workspace.json'), 'utf8')).replace(/^\uFEFF/, ''),
        );
        const groups = await Promise.all(kinds.map(async (k) => [k, await store.list(k)]));
        return json(res, 200, { workspace, ...Object.fromEntries(groups) });
      }
      if (
        kind === 'assets' &&
        parts.length === 2 &&
        method === 'POST' &&
        req.headers['content-type'] === 'application/octet-stream'
      )
        return await upload(req, res, url.searchParams.get('name'));
      if (!kinds.includes(kind) || parts.length > 3 || (id && !isId(id))) fail(404, '未知记录路径');
      if (method === 'GET' && !id) return json(res, 200, await store.list(kind));
      if (!id) fail(405, '请指定记录 ID');
      if (method === 'GET') {
        const record = await store.get(kind, id);
        return json(res, 200, record, { ETag: '"' + record.revision + '"' });
      }
      if (method === 'PUT' || method === 'DELETE') {
        const create = method === 'PUT' && req.headers['if-none-match'] === '*';
        if (method === 'PUT' && req.headers['content-type']?.split(';')[0] !== 'application/json')
          fail(415, '需要 application/json');
        const doc = method === 'DELETE' ? null : await body(req);
        if (method === 'PUT' && doc === null) fail(422, '请提供记录');
        const result = await store.change(kind, id, doc, req.headers['if-match'], create);
        return json(res, create ? 201 : 200, result);
      }
      fail(405, '不支持该方法');
    }
    if (!read) fail(405, '不支持该方法');
    if (!area && url.searchParams.has('model')) {
      res.writeHead(302, { Location: '/rig/' + url.search }).end();
      return;
    }
    if (area === 'assets' && !assetPath(parts.slice(1).join('/'))) fail(404, '未知附件');
    if (
      area === 'config' &&
      !(parts.length === 3 && kinds.includes(kind) && id.endsWith('.json') && isId(id.slice(0, -5)))
    )
      fail(404, '未知配置');
    if (
      area &&
      !publicDirs.has(area) &&
      !['assets', 'config'].includes(area) &&
      !(parts.length === 1 && ['index.html', 'workspace.json', 'README.md'].includes(area))
    )
      fail(404, '未知页面');
    if (parts.length === 1 && publicDirs.has(area) && !url.pathname.endsWith('/')) {
      res.writeHead(302, { Location: url.pathname + '/' + url.search }).end();
      return;
    }
    const file = path.join(
      root,
      ...parts,
      ...(!area || url.pathname.endsWith('/') ? ['index.html'] : []),
    );
    await serve(req, res, file);
  } catch (e) {
    if (res.headersSent || res.destroyed) return;
    const status = e.status || (e.code === 'ENOENT' ? 404 : 500);
    json(res, status, {
      error: status === 500 ? '工作台读写失败 · ' + (e.code || '内部错误') : e.message,
    });
  }
});
server.on('error', (e) => {
  console.error(e.message);
  process.exitCode = 1;
});
server.listen(port, '127.0.0.1', async () => {
  console.log('SharedWorkSpace http://127.0.0.1:' + port + '/');
  try {
    receiverStatus = { enabled: await startReceiver(root, store, port) };
  } catch (e) {
    receiverStatus = { enabled: false, error: e.message };
    console.error('Command inbox:', e.message);
  }
});
if (port === 4190) {
  const legacy = http.createServer(async (req, res) => {
    if (
      req.method !== 'GET' ||
      !['127.0.0.1:4186', 'localhost:4186'].includes(req.headers.host) ||
      new URL(req.url, 'http://localhost').pathname !== '/'
    ) {
      res.writeHead(404).end();
      return;
    }
    try {
      await serve(req, res, path.join(root, 'ui/legacy-entry.html'));
    } catch {
      if (!res.headersSent) res.writeHead(404);
      res.end();
    }
  });
  legacy.on('error', (e) => {
    if (e.code !== 'EADDRINUSE') console.error('Legacy reader:', e.message);
  });
  legacy.listen(4186, '127.0.0.1');
}
