import * as T from 'three';
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { fileURL, storeFile } from './data.js';

export function modelFile(files) {
  const models = files.filter((f) => /\.(fbx|glb|gltf)$/i.test(f.name));
  if (models.length !== 1) throw Error('请选择一个 FBX、GLB 或 glTF，外部 BIN 和贴图可一并选择');
  return models[0];
}
export async function saveFiles(files) {
  const paths = [];
  for (const file of files) paths.push(await storeFile(file));
  return paths;
}
export async function readFiles(paths) {
  return Promise.all(
    paths.map(async (path) => {
      const res = await fetch(fileURL(path));
      if (!res.ok) throw Error('附件尚未同步或已移除 · ' + path);
      return new File([await res.blob()], path.split('/').at(-1), {
        type: res.headers.get('content-type') || '',
      });
    }),
  );
}
export async function parseModel(files, filename) {
  const file = files.find((f) => f.name === filename);
  if (!file) throw Error('缺少文件 ' + filename);
  const buffer = await file.arrayBuffer(),
    urls = [],
    manager = new T.LoadingManager();
  const names = new Map(files.map((f) => [f.name.toLowerCase(), f]));
  manager.setURLModifier((url) => {
    if (/^(data:|blob:)/.test(url)) return url;
    const part = decodeURIComponent(url).replaceAll('\\', '/').split('/').at(-1).toLowerCase();
    const f = names.get(part);
    if (!f) return 'data:application/octet-stream;base64,';
    const u = URL.createObjectURL(f);
    urls.push(u);
    return u;
  });
  let root, clips;
  try {
    if (/\.fbx$/i.test(filename)) {
      root = new FBXLoader(manager).parse(buffer, '');
      clips = root.animations;
    } else {
      const result = await new GLTFLoader(manager).parseAsync(buffer, '');
      root = result.scene;
      clips = result.animations;
    }
  } catch (e) {
    urls.forEach((u) => URL.revokeObjectURL(u));
    throw e;
  }
  function dispose() {
    const used = new Set();
    root.traverse((n) => {
      for (const obj of [n.geometry, ...[].concat(n.material || [])]) {
        if (!obj || used.has(obj)) continue;
        used.add(obj);
        if (obj.isMaterial)
          for (const v of Object.values(obj))
            if (v?.isTexture && !used.has(v)) {
              used.add(v);
              v.dispose();
            }
        obj.dispose();
      }
    });
    urls.forEach((u) => URL.revokeObjectURL(u));
  }
  return { root, clips, dispose };
}
