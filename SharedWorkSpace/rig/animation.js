import * as T from 'three';
import { byId as $, get, save } from '../ui/data.js';
import { modelFile, parseModel, readFiles, saveFiles } from '../ui/models.js';

function bindMatrices(model) {
  const matrices = new Map();
  model.traverse((node) => {
    if (!node.isSkinnedMesh) return;
    node.skeleton.bones.forEach((bone, i) => {
      const inverse = node.skeleton.boneInverses[i];
      if (bone && inverse) matrices.set(bone, inverse);
    });
  });
  return matrices;
}
function localBind(matrices, bone, parent) {
  if (!matrices.has(bone) || !matrices.has(parent)) return null;
  const local = matrices.get(parent).clone().multiply(matrices.get(bone).clone().invert());
  const position = new T.Vector3(),
    rotation = new T.Quaternion(),
    scale = new T.Vector3();
  local.decompose(position, rotation, scale);
  return { position, rotation, scale };
}
// Animation-only glTF files may contain ordinary nodes without a skin or stored bind pose.
function matchClip(source, clip, bones, targetMatrices) {
  const byName = new Map(bones.map((b) => [b.name, b]));
  if (byName.size !== bones.length) throw Error('目标模型骨名重复，无法唯一绑定');
  const sourceNodes = new Map();
  source.traverse((n) => {
    if (n.isBone && !byName.has(n.name)) throw Error('源骨架多出 ' + n.name);
    if (!byName.has(n.name)) return;
    if (sourceNodes.has(n.name)) throw Error('动画含重复节点 ' + n.name);
    sourceNodes.set(n.name, n);
  });
  const parentBone = (n, map) => {
    for (let p = n.parent; p; p = p.parent) if (map.get(p.name) === p) return p.name;
    return '';
  };
  const sourceMatrices = bindMatrices(source);
  for (const b of bones) {
    const n = sourceNodes.get(b.name);
    if (!n) throw Error('动画缺少骨骼 ' + b.name);
    const parent = parentBone(b, byName);
    if (parent !== parentBone(n, sourceNodes)) throw Error('父骨骼不一致 · ' + b.name);
    const a = localBind(targetMatrices, b, byName.get(parent)),
      s = localBind(sourceMatrices, n, sourceNodes.get(parent));
    if (
      a &&
      s &&
      (a.position.distanceTo(s.position) > Math.max(0.0001, 0.002 * a.position.length()) ||
        a.rotation.angleTo(s.rotation) > T.MathUtils.degToRad(0.5) ||
        a.scale.distanceTo(s.scale) > 0.002 * Math.max(1, a.scale.length()))
    )
      throw Error('绑定姿态的关节长度、轴向或缩放不一致 · ' + b.name);
  }
  const tracks = clip.tracks.map((track) => {
    const binding = T.PropertyBinding.parseTrackName(track.name);
    const sourceNode =
      binding.objectName === 'bones'
        ? sourceNodes.get(binding.objectIndex)
        : T.PropertyBinding.findNode(source, binding.nodeName);
    if (
      !sourceNode ||
      sourceNodes.get(sourceNode.name) !== sourceNode ||
      !['position', 'quaternion', 'scale'].includes(binding.propertyName) ||
      binding.propertyIndex != null
    )
      throw Error('无法匹配动画轨道 ' + track.name);
    const copy = track.clone();
    copy.name = byName.get(sourceNode.name).uuid + '.' + binding.propertyName;
    return copy;
  });
  if (!tracks.length || !(clip.duration > 0)) throw Error('动画片段没有可播放的骨骼轨道');
  return new T.AnimationClip(clip.name, clip.duration, tracks, clip.blendMode);
}
export function animationPanel() {
  let root,
    bones = [],
    targetMatrices,
    version,
    embedded = [],
    mixer,
    action,
    duration = 0,
    time = 0,
    playing = false,
    last = 0,
    generation = 0,
    request = 0,
    selectedBone;
  const menu = $('clips'),
    slider = $('time'),
    message = $('animStatus');
  function status(text) {
    message.textContent = text;
  }
  function controlState() {
    $('play').disabled = !action;
    $('animStop').disabled = !action;
    slider.disabled = !action;
    $('play').textContent = playing ? '暂停' : '播放';
    slider.value = time;
    $('timecode').textContent = time.toFixed(2) + ' / ' + duration.toFixed(2) + ' 秒';
  }
  function stop() {
    ++request;
    playing = false;
    if (mixer) {
      mixer.stopAllAction();
      mixer.uncacheRoot(root);
    }
    action = mixer = null;
    time = duration = 0;
    controlState();
  }
  function populate() {
    menu.replaceChildren(new Option('手动姿态', ''));
    embedded.forEach((c, i) =>
      menu.add(new Option('内嵌 · ' + (c.name || '片段 ' + (i + 1)), 'local:' + i)),
    );
    for (const set of version?.animations || [])
      set.clips.forEach((c, i) =>
        menu.add(new Option(set.label + ' / ' + c, 'file:' + set.id + ':' + i)),
      );
    if (bones.length) menu.add(new Option('单骨转动测试（合成）', 'test'));
    $('addAnim').disabled = !bones.length;
  }
  function sample(t) {
    time = Math.max(0, Math.min(duration, t));
    action.time = time;
    mixer.update(0);
    controlState();
  }
  async function choose(key) {
    stop();
    if (!key) {
      status('手动姿态');
      return;
    }
    const token = request,
      rev = generation;
    try {
      let clip;
      if (key === 'test') {
        const b = bones[selectedBone?.()] || bones[0],
          q = b.quaternion.clone(),
          r = q.clone().multiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(1, 0, 0), 0.5));
        clip = new T.AnimationClip('test', 2, [
          new T.QuaternionKeyframeTrack(
            b.uuid + '.quaternion',
            [0, 1, 2],
            [...q.toArray(), ...r.toArray(), ...q.toArray()],
          ),
        ]);
      } else if (key.startsWith('local:')) clip = embedded[Number(key.slice(6))];
      else {
        const [, id, i] = key.split(':'),
          set = version.animations.find((a) => a.id === id);
        const asset = await parseModel(await readFiles(set.files), set.filename);
        try {
          if (token !== request || rev !== generation) return;
          clip = matchClip(asset.root, asset.clips[Number(i)], bones, targetMatrices);
        } finally {
          asset.dispose();
        }
      }
      if (token !== request || rev !== generation) return;
      if (!clip || !(clip.duration > 0)) throw Error('片段时间长度无效');
      mixer = new T.AnimationMixer(root);
      action = mixer.clipAction(clip);
      action.play();
      action.paused = true;
      duration = clip.duration;
      slider.max = duration;
      sample(0);
      status('已载入 · ' + clip.tracks.length + ' 条轨道');
    } catch (e) {
      if (token === request) {
        stop();
        menu.value = '';
        status(e.message);
      }
    }
  }
  menu.onchange = () => choose(menu.value);
  $('play').onclick = () => {
    if (!action) return;
    if (time >= duration) sample(0);
    playing = !playing;
    last = performance.now();
    controlState();
  };
  slider.oninput = () => {
    if (action) {
      playing = false;
      sample(Number(slider.value));
    }
  };
  $('animStop').onclick = () => {
    menu.value = '';
    choose('');
  };
  $('addAnim').onclick = () => $('animFiles').click();
  $('animFiles').onchange = async (e) => {
    const files = Array.from(e.target.files);
    e.target.value = '';
    if (!files.length || !bones.length) return;
    const rev = generation,
      v = version;
    $('addAnim').disabled = true;
    let parsed;
    try {
      const f = modelFile(files);
      parsed = await parseModel(files, f.name);
      if (rev !== generation) return;
      if (!parsed.clips.length) throw Error('文件中没有动画片段');
      for (const clip of parsed.clips) matchClip(parsed.root, clip, bones, targetMatrices);
      const set = {
        id: crypto.randomUUID(),
        label: f.name,
        filename: f.name,
        files: await saveFiles(files),
        clips: parsed.clips.map((c, i) => c.name || '片段 ' + (i + 1)),
      };
      const record = await get('rig', v.collectionId);
      const current = record.doc.versions.find((i) => i.id === v.id);
      if (!current) throw Error('模型版本已经移除');
      if (rev !== generation) return;
      (current.animations ??= []).push(set);
      await save('rig', record.doc, record.revision);
      if (rev !== generation) return;
      v.animations = current.animations;
      populate();
      menu.value = 'file:' + set.id + ':0';
      await choose(menu.value);
    } catch (err) {
      if (rev === generation) status('动画导入失败 · ' + err.message);
    } finally {
      parsed?.dispose();
      if (rev === generation) $('addAnim').disabled = !bones.length;
    }
  };
  return {
    bind(model, joints, v, selection) {
      ++generation;
      stop();
      root = model;
      bones = joints;
      targetMatrices = model ? bindMatrices(model) : new Map();
      version = v;
      selectedBone = selection;
      embedded = model?.animations || [];
      populate();
      status('选择片段或导入同骨架动画');
    },
    active: () => !!action,
    tick(now) {
      if (!playing || !action) return;
      const t = time + (now - last) / 1000;
      last = now;
      if (t >= duration && !$('loop').checked) {
        playing = false;
        sample(duration);
      } else sample(t >= duration ? t % duration : t);
    },
  };
}
