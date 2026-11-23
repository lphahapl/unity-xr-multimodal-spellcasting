import { createHash, randomUUID } from 'node:crypto';
import {
  readFile,
  readdir,
  rename,
  writeFile,
  link,
  unlink,
  realpath,
  copyFile,
} from 'node:fs/promises';
import { constants } from 'node:fs';
import path from 'node:path';

export const kinds = ['rig', 'plans', 'scenes'];
export const isId = (value) =>
  typeof value === 'string' &&
  /^[a-z0-9][a-z0-9_-]{0,95}$/i.test(value) &&
  !/^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/i.test(value);
export function fail(status, message) {
  throw Object.assign(new Error(message), { status });
}
function check(ok, message) {
  if (!ok) fail(422, message);
}
const object = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);
const str = (v, max = 500000) => typeof v === 'string' && v.length <= max;
const name = (v) => str(v, 240) && v.trim().length > 0;
const number = (v) => typeof v === 'number' && Number.isFinite(v);
export const allowedAsset = (n) =>
  name(n) &&
  !/[<>:"/\\|?*\x00-\x1f]/.test(n) &&
  !/[. ]$/.test(n) &&
  !/^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(n) &&
  /\.(fbx|glb|gltf|bin|png|jpe?g|webp)$/i.test(n);
export function assetPath(v) {
  const parts = typeof v === 'string' ? v.split('/') : [];
  return parts.length === 2 && isId(parts[0]) && allowedAsset(parts[1]);
}
function distinct(rows, label) {
  check(
    Array.isArray(rows) && rows.every((v) => object(v) && isId(v.id)),
    label + '的每一项都需要有效 ID',
  );
  check(new Set(rows.map((v) => v.id)).size === rows.length, label + '的 ID 重复');
}
const triple = (a) => Array.isArray(a) && a.length === 3 && a.every(number);
function modelFiles(v) {
  check(
    object(v) &&
      allowedAsset(v.filename) &&
      /\.(fbx|glb|gltf)$/i.test(v.filename) &&
      Array.isArray(v.files) &&
      v.files.every(assetPath) &&
      v.files.some((f) => f.split('/')[1] === v.filename),
    '无效模型或附件',
  );
  check(
    new Set(v.files.map((f) => f.split('/')[1].toLowerCase())).size === v.files.length,
    '附件文件名重复',
  );
  if (v.material)
    check(
      object(v.material) &&
        ['color', 'normal'].every((k) => v.files.includes(v.material[k])) &&
        number(v.material.metalness) &&
        v.material.metalness >= 0 &&
        v.material.metalness <= 1,
      '材质引用不完整',
    );
}
export function validate(kind, id, doc) {
  check(isId(id) && object(doc) && doc.id === id && name(doc.name), '文件名、id 或名称无效');
  if (kind === 'rig') {
    distinct(doc.versions, '版本');
    if (doc.defaultVersion !== undefined)
      check(doc.versions.some((v) => v.id === doc.defaultVersion), '默认版本需要属于此人物集合');
    check(new Set(doc.versions.map((v) => v.label)).size === doc.versions.length, '集合内版本名称重复');
    for (const v of doc.versions) {
      check(name(v.label), '版本需要名称');
      modelFiles(v);
      if (v.turnaround !== undefined)
        check(v.files.includes(v.turnaround) && /\.(png|jpe?g|webp)$/i.test(v.turnaround),
          '三视图需要引用本版本的图片附件');
      if (v.slotAlbedos !== undefined) {
        check(!v.material && object(v.slotAlbedos), '分槽底色需要独立的材质槽映射');
        const slots = Object.entries(v.slotAlbedos);
        check(
          slots.length > 0 && slots.every(([slot, file]) =>
            name(slot) && allowedAsset(file) && /\.(png|jpe?g|webp)$/i.test(file) &&
            v.files.some((asset) => asset.split('/')[1] === file)),
          '分槽底色需要引用版本中的图片附件',
        );
      }
      distinct(v.animations ?? [], '动画文件');
      for (const a of v.animations ?? []) {
        modelFiles(a);
        check(
          name(a.label) && Array.isArray(a.clips) && a.clips.length > 0 && a.clips.every((c) => name(c)),
          '动画需要片段清单',
        );
      }
      check(number(v.created), '版本 created 使用毫秒时间戳');
      distinct(v.notes, '批注');
      check(
        v.notes.every((n) => str(n.text, 8000) && n.text.trim() && number(n.created)),
        '批注需要正文和毫秒时间戳',
      );
    }
  } else {
    check(str(doc.summary), '摘要需要是文本');
    if (kind === 'plans') check(name(doc.status) && str(doc.body), '策划案需要状态和正文');
    else {
      check(
        number(doc.width) &&
          doc.width > 0 &&
          doc.width <= 10000 &&
          number(doc.depth) &&
          doc.depth > 0 &&
          doc.depth <= 10000,
        '场地尺寸须在 0 到 10000 米之间',
      );
      check(['whitebox', 'model'].includes(doc.mode ?? 'whitebox'), '未知场景模式');
      if (doc.model) {
        modelFiles(doc.model);
        check(object(doc.model.nodes), '模型节点覆盖需要是对象');
        for (const [key, t] of Object.entries(doc.model.nodes)) {
          check(
            /^root(?:\/\d+)*$/.test(key) &&
              object(t) &&
              triple(t.position) &&
              triple(t.rotation) &&
              triple(t.scale) &&
              t.scale.every((n) => Math.abs(n) > 1e-8) &&
              typeof t.visible === 'boolean',
            '模型节点变换无效',
          );
        }
      }
      distinct(doc.objects, '场景元素');
      for (const o of doc.objects)
        check(
          ['box', 'sphere', 'cylinder'].includes(o.shape ?? 'box') &&
            number(o.h ?? 1) &&
            (o.h ?? 1) > 0 &&
            number(o.y ?? 0) &&
            (!o.rotation || triple(o.rotation)) &&
            (o.visible === undefined || typeof o.visible === 'boolean'),
          '白盒三维参数无效',
        );
      for (const o of doc.objects)
        check(
          name(o.name) &&
            name(o.type) &&
            [o.x, o.z, o.w, o.d].every(number) &&
            o.x >= 0 &&
            o.z >= 0 &&
            o.w > 0 &&
            o.d > 0 &&
            o.x + o.w <= doc.width + 1e-8 &&
            o.z + o.d <= doc.depth + 1e-8,
          '场景元素需要名称、类型及场地范围内的坐标和尺寸',
        );
    }
  }
}
export const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');
export async function existingWithin(root, file) {
  const physical = await realpath(file);
  if (!physical.startsWith(root + path.sep)) fail(403, '路径越界');
  return physical;
}
export function recordStore(root) {
  const locks = new Map();
  const folder = (kind) => path.join(root, 'config', kind);
  async function get(kind, id) {
    const file = await existingWithin(root, path.join(folder(kind), id + '.json'));
    const bytes = await readFile(file);
    let doc;
    try {
      doc = JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/, ''));
    } catch {
      fail(422, 'JSON 语法有误');
    }
    validate(kind, id, doc);
    return { doc, revision: digest(bytes) };
  }
  async function list(kind) {
    const entries = [],
      errors = [];
    for (const file of (await readdir(await existingWithin(root, folder(kind)))).sort()) {
      if (!file.toLowerCase().endsWith('.json')) continue;
      const id = file.slice(0, -5);
      try {
        check(isId(id), '文件名须为有效 ID，可能是同步冲突副本');
        entries.push(await get(kind, id));
      } catch (e) {
        errors.push({ file, message: e.message });
      }
    }
    return { entries, errors };
  }
  function change(kind, id, doc, expected, create) {
    const file = path.join(folder(kind), id + '.json');
    const prior = locks.get(file) || Promise.resolve();
    const task = prior
      .catch(() => {})
      .then(async () => {
        await existingWithin(root, folder(kind));
        if (doc !== null) validate(kind, id, doc);
        if (!create && !expected) fail(428, '请提供 If-Match 版本号');
        const temp = path.join(folder(kind), '.' + randomUUID() + '.tmp');
        try {
          if (doc !== null) await writeFile(temp, JSON.stringify(doc, null, 2) + '\n', { flag: 'wx' });
          if (create) {
            // Exclusive creation without exposing a partially written JSON file.
            try {
              try {
                await link(temp, file);
              } catch (e) {
                if (!['ENOSYS', 'ENOTSUP', 'EPERM', 'EXDEV', 'EOPNOTSUPP'].includes(e.code)) throw e;
                await copyFile(temp, file, constants.COPYFILE_EXCL);
              }
            } catch (e) {
              if (e.code === 'EEXIST') fail(409, '该 ID 已存在');
              throw e;
            }
          } else {
            const current = await get(kind, id);
            if (expected !== '"' + current.revision + '"') fail(412, '文件已经变化，请重新读取后再保存');
            if (doc === null) await unlink(file);
            else await rename(temp, file);
          }
        } finally {
          await unlink(temp).catch((e) => {
            if (e.code !== 'ENOENT') throw e;
          });
        }
        return doc === null ? null : get(kind, id);
      });
    locks.set(file, task);
    return task.finally(() => {
      if (locks.get(file) === task) locks.delete(file);
    });
  }
  return { get, list, change };
}
