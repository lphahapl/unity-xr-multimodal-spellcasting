import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { setTimeout as wait } from 'node:timers/promises';
import { isId, kinds } from './records.mjs';

const url = new URL(process.argv[2]);
if (url.origin !== 'https://gemini.google.com' || !/^\/(?:u\/\d+\/)?app\/[a-z0-9]+$/i.test(url.pathname))
  throw Error('请指定一个已打开的 Gemini 对话链接');
const chat = url.origin + url.pathname;
const matches = (value) => {
  try {
    const u = new URL(value);
    return u.origin + u.pathname === chat;
  } catch {
    return false;
  }
};
const tabs = await (
  await fetch('http://127.0.0.1:9222/json/list', { signal: AbortSignal.timeout(4000) })
).json();
const tab = tabs.find((t) => t.type === 'page' && matches(t.url));
if (!tab) throw Error('已启用远程调试的本机 Chrome 中没有这个对话');
const inbox = fileURLToPath(new URL('./inbox/', import.meta.url));
await mkdir(inbox, { recursive: true });
const socket = new WebSocket(tab.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.onopen = resolve;
  socket.onerror = () => reject(Error('Chrome 连接失败'));
});
let seq = 0;
function readTurn(chatUrl) {
  if (location.origin + location.pathname !== chatUrl) return { closed: true };
  if (document.querySelector('button[aria-label="停止回答"],button[aria-label="Stop response"]'))
    return {};
  const q = [...document.querySelectorAll('user-query')].at(-1);
  const marker = q?.querySelector('.query-text-line')?.textContent.trim();
  if (!marker?.startsWith('工作台投递 ')) return {};
  const response = q.closest('.conversation-container')?.querySelector('model-response message-content');
  const blocks = response?.querySelectorAll('pre code');
  if (blocks?.length !== 1) return {};
  return { marker, body: blocks[0].textContent };
}
async function snapshot() {
  const id = ++seq;
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      socket.removeEventListener('message', receive);
      reject(Error('读取超时'));
    }, 5000);
    function receive(e) {
      const m = JSON.parse(e.data);
      if (m.id !== id) return;
      clearTimeout(timer);
      socket.removeEventListener('message', receive);
      if (m.error || m.result.exceptionDetails) reject(Error('标签页已关闭或无法读取'));
      else resolve(m.result.result.value);
    }
    socket.addEventListener('message', receive);
    socket.send(
      JSON.stringify({
        id,
        method: 'Runtime.evaluate',
        params: {
          expression: '(' + readTurn.toString() + ')(' + JSON.stringify(chat) + ')',
          returnByValue: true,
        },
      }),
    );
  });
}
const once = process.argv.includes('--once');
const deadline = once ? Date.now() + 120000 : Infinity;
let last = '';
try {
  while (socket.readyState === 1 && Date.now() < deadline) {
    const turn = await snapshot();
    if (turn.closed) break;
    if (
      turn.marker &&
      turn.body &&
      turn.marker !== last &&
      Buffer.byteLength(turn.body) <= 8 * 1024 * 1024
    ) {
      let c;
      try {
        c = JSON.parse(turn.body);
      } catch {}
      const [tag, id, op, kind, target, ...extra] = turn.marker.split(/\s+/);
      if (
        c &&
        !extra.length &&
        tag === '工作台投递' &&
        isId(id) &&
        isId(target) &&
        kinds.includes(kind) &&
        ['read', 'preview', 'create', 'replace', 'delete', 'scene.set', 'rig.note'].includes(op) &&
        c.id === id &&
        c.op === op &&
        c.kind === kind &&
        c.target === target
      ) {
        const file = path.join(inbox, id + '.json'),
          bytes = JSON.stringify(c, null, 2) + '\n';
        try {
          await writeFile(file, bytes, { flag: 'wx' });
        } catch (e) {
          if (e.code !== 'EEXIST') throw e;
          if ((await readFile(file, 'utf8')) !== bytes)
            throw Error('命令编号已被不同内容占用，请使用新 ID');
        }
        console.log('已投递 ' + id);
        last = turn.marker;
        if (once) break;
      }
    }
    await wait(1800);
  }
  if (once && !last) throw Error('未收到与投递首行匹配的完整命令');
} finally {
  socket.close();
}
