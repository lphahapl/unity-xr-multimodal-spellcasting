import path from 'node:path';
import os from 'node:os';
import { spawn } from 'node:child_process';
import { access, mkdtemp, readFile, rm } from 'node:fs/promises';
import { setTimeout as wait } from 'node:timers/promises';
import { fail } from './records.mjs';

async function chromePath() {
  const names = [
    process.env.WORKSPACE_CHROME,
    ...[process.env.ProgramFiles, process.env['ProgramFiles(x86)'], process.env.LOCALAPPDATA]
      .filter(Boolean)
      .map((p) => path.join(p, 'Google/Chrome/Application/chrome.exe')),
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  ].filter(Boolean);
  for (const name of names) {
    try {
      await access(name);
      return name;
    } catch {}
  }
  fail(503, '找不到 Chrome，请设置本机 WORKSPACE_CHROME 路径');
}

// This function runs in the actual workbench tab. Only JSON values cross into it.
async function inPage(c) {
  const $ = (id) => document.getElementById(id);
  const until = async (fn, message) => {
    for (let i = 0; i < 220; i++) {
      if (fn()) return;
      await new Promise((r) => setTimeout(r, 120));
    }
    throw Error(message);
  };
  let details = {};
  if (c.kind === 'rig') {
    await until(
      () => $('version')?.value === c.versionId && !$('annotations').disabled,
      '模型版本加载失败',
    );
    if (c.bone) {
      const matches = [...$('tree').querySelectorAll('button')].filter(
        (b) => b.title === c.bone || b.textContent === c.bone,
      );
      if (matches.length !== 1) throw Error('骨名缺失或不唯一 · ' + c.bone);
      matches[0].click();
      $('heat').click();
      $('focus').click();
      details = {
        bone: $('selection').textContent,
        affected: $('affected').textContent,
        maxWeight: $('maxWeight').textContent,
      };
    }
    if (c.clip) {
      const menu = $('clips');
      menu.value = c.clip;
      if (menu.value !== c.clip) throw Error('找不到动画片段');
      menu.dispatchEvent(new Event('change'));
      await until(() => !$('time').disabled || menu.value === '', '动画加载超时');
      if ($('time').disabled) throw Error($('animStatus').textContent);
      if (c.seconds > Number($('time').max)) throw Error('时间超过片段长度');
      $('time').value = c.seconds ?? 0;
      $('time').dispatchEvent(new Event('input'));
      details.time = $('timecode').textContent;
    }
    details.audit = $('audit').innerText;
    details.bones = [...$('tree').querySelectorAll('button')].map((b) => b.title);
    details.clips = [...$('clips').options]
      .filter((o) => o.value)
      .map((o) => ({ id: o.value, name: o.text }));
  } else {
    await until(
      () => $('path')?.textContent === 'config/scenes/' + c.target + '.json' && !$('editor').hidden,
      '场景加载失败',
    );
    if ($('sceneMode').value === 'model') {
      await until(() => /已载入|失败|请选择/.test($('sceneStatus').textContent), '场景模型加载超时');
      if (!$('sceneStatus').textContent.includes('已载入')) throw Error($('sceneStatus').textContent);
    }
    details.mode = $('sceneMode').value;
  }
  await new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
  const elem = $(c.kind === 'rig' ? 'stage' : 'viewport3d');
  elem.scrollIntoView({ block: 'center' });
  const r = elem.getBoundingClientRect();
  return {
    details,
    clip: { x: r.x + scrollX, y: r.y + scrollY, width: r.width, height: r.height, scale: 1 },
  };
}

export async function capturePreview(origin, c, doc) {
  if (c.kind === 'rig') {
    if (!doc.versions.some((v) => v.id === c.versionId)) fail(422, '请指定有效的 versionId');
    if (
      (c.bone !== undefined && typeof c.bone !== 'string') ||
      (c.clip !== undefined && typeof c.clip !== 'string') ||
      (c.seconds !== undefined && (!c.clip || !Number.isFinite(c.seconds) || c.seconds < 0))
    )
      fail(422, '骨名、片段或时间无效');
  } else if (c.kind !== 'scenes' || !['perspective', 'top'].includes(c.view ?? 'perspective'))
    fail(422, '预览支持 rig 或 scenes，场景视角为 perspective 或 top');
  const page = new URL('/' + c.kind + '/', origin);
  if (c.kind === 'scenes') {
    page.searchParams.set('id', c.target);
    page.searchParams.set('view', c.view ?? 'perspective');
  } else {
    page.searchParams.set('collection', c.target);
    page.searchParams.set('model', c.versionId);
  }
  const exe = await chromePath(),
    profile = await mkdtemp(path.join(os.tmpdir(), 'ws-preview-'));
  const proc = spawn(
    exe,
    [
      '--headless=new',
      '--no-first-run',
      '--disable-extensions',
      '--disable-background-networking',
      '--no-proxy-server',
      '--enable-unsafe-swiftshader',
      '--use-angle=swiftshader',
      '--window-size=1440,1000',
      '--remote-debugging-port=0',
      '--user-data-dir=' + profile,
      'about:blank',
    ],
    { stdio: 'ignore', windowsHide: true },
  );
  let ws, error;
  proc.once('error', (e) => {
    error = e;
  });
  const closed = new Promise((r) => proc.once('close', r));
  try {
    let port;
    for (let i = 0; i < 100 && !port; i++) {
      if (error || proc.exitCode !== null) throw Error('Chrome 启动失败');
      try {
        port = Number((await readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]);
      } catch {}
      if (!port) await wait(100);
    }
    if (!port) throw Error('Chrome 启动超时');
    const res = await fetch('http://127.0.0.1:' + port + '/json/list', {
      signal: AbortSignal.timeout(5000),
    });
    const tab = (await res.json()).find((t) => t.type === 'page');
    ws = new WebSocket(tab.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
      const t = setTimeout(() => reject(Error('Chrome 连接超时')), 5000);
      ws.onopen = () => {
        clearTimeout(t);
        resolve();
      };
      ws.onerror = () => {
        clearTimeout(t);
        reject(Error('Chrome 连接失败'));
      };
    });
    let seq = 0;
    async function send(method, params = {}) {
      const id = ++seq;
      return new Promise((resolve, reject) => {
        const cleanup = () => {
          clearTimeout(timer);
          ws.removeEventListener('message', handle);
        };
        const timer = setTimeout(() => {
          cleanup();
          reject(Error('预览超时 · ' + method));
        }, 45000);
        const handle = (e) => {
          const r = JSON.parse(e.data);
          if (r.id !== id) return;
          cleanup();
          if (r.error) reject(Error(r.error.message));
          else resolve(r.result);
        };
        ws.addEventListener('message', handle);
        ws.send(JSON.stringify({ id, method, params }));
      });
    }
    await send('Page.navigate', { url: page.href });
    let ready = false;
    for (let i = 0; i < 100 && !ready; i++) {
      const v = await send('Runtime.evaluate', {
        expression:
          'location.pathname === ' +
          JSON.stringify(page.pathname) +
          ' && document.readyState !== "loading"',
      });
      ready = v.result?.value;
      if (!ready) await wait(100);
    }
    if (!ready) throw Error('工作台页面未就绪');
    const { result, exceptionDetails } = await send('Runtime.evaluate', {
      expression: '(' + inPage.toString() + ')(' + JSON.stringify(c) + ')',
      awaitPromise: true,
      returnByValue: true,
    });
    if (exceptionDetails)
      throw Error(exceptionDetails.exception?.description?.split('\n')[0] || '预览加载失败');
    const shot = await send('Page.captureScreenshot', {
      format: 'png',
      clip: result.value.clip,
      captureBeyondViewport: true,
    });
    return { image: Buffer.from(shot.data, 'base64'), details: result.value.details };
  } catch (e) {
    fail(503, e.message);
  } finally {
    if (ws?.readyState === 1) {
      ws.send(JSON.stringify({ id: 99999, method: 'Browser.close' }));
      ws.close();
    }
    if (proc.exitCode === null && !error) {
      await Promise.race([closed, wait(1500)]);
      if (proc.exitCode === null) proc.kill();
    }
    await rm(profile, { recursive: true, force: true, maxRetries: 3, retryDelay: 150 }).catch(() => {});
  }
}
