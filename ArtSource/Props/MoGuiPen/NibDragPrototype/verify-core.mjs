// Run: node ArtSource/Props/MoGuiPen/NibDragPrototype/verify-core.mjs
// Deterministic checks only. This script writes no files and opens no Unity scene.
import assert from 'node:assert/strict';
import { NibDragState, deformPosition } from './drag-core.mjs';

const checks = [];
const norm = point => Math.hypot(...point);
const near = (a, b, tolerance = 1e-10) => assert.ok(Math.abs(a - b) <= tolerance, `${a} != ${b} (tol ${tolerance})`);
const run = (name, fn) => { fn(); checks.push({ name, passed: true }); };
const sample = (state, time, point, extra = {}) => state.update({ time, point, contact: true, ...extra });
function travel({ hz = 60, seconds = 1, velocity = [.2, 0, 0], state = new NibDragState(), start = 0, origin = [0, 0, 0] } = {}) {
  let result;
  for (let frame = 0; frame <= Math.round(hz * seconds); frame++) {
    const t = frame / hz;
    result = sample(state, start + t, origin.map((value, i) => value + velocity[i] * t));
  }
  return { state, result };
}

for (const [name, velocity, axis, sign] of [
  ['positive X drags negative X', [.2, 0, 0], 0, -1],
  ['negative X drags positive X', [-.2, 0, 0], 0, 1],
  ['positive Y drags negative Y', [0, .2, 0], 1, -1],
  ['negative Y drags positive Y', [0, -.2, 0], 1, 1]
]) run(name, () => {
  const { result } = travel({ velocity });
  assert.ok(result.bend[axis] * sign > .0035);
  near(result.bend[1 - axis], 0); near(result.bend[2], 0);
});

for (const mode of ['stopped', 'lifted', 'tracking_lost']) run(`${mode} returns by the configured exponential`, () => {
  const { state } = travel();
  const before = norm(state.bend);
  for (let i = 1; i <= 30; i++) sample(state, 1 + i / 60, [.2, 0, 0], {
    contact: mode !== 'lifted', tracked: mode !== 'tracking_lost'
  });
  near(norm(state.bend), before * Math.exp(-.5 / state.returnTau));
  assert.ok(norm(state.bend) < 1e-6);
});

run('first sample has no velocity spike at an arbitrary position', () => {
  const state = new NibDragState();
  const r = sample(state, 10, [100, -100, .1]);
  assert.deepEqual(r.bend, [0, 0, 0]); near(r.speed, 0); assert.equal(r.resetReason, 'first_sample');
});
run('recontact and recovery do not differentiate across missing samples', () => {
  for (const mode of ['lifted', 'lost']) {
    const { state } = travel();
    sample(state, 1.01, [.2, 0, 0], { contact: mode !== 'lifted', tracked: mode !== 'lost' });
    const before = norm(state.bend);
    const r = sample(state, 1.02, [9, -20, 0]);
    assert.equal(r.resetReason, 'contact_started'); near(r.speed, 0); assert.ok(norm(r.bend) < before);
    const settled = sample(state, 1.03, [9, -20, 0]);
    near(settled.speed, 0); assert.ok(norm(settled.bend) < norm(r.bend));
  }
});
run('switching board clears bend and establishes a new local baseline', () => {
  const { state } = travel();
  const r = sample(state, 1.01, [-10, 99, 0], { boardId: 'other' });
  assert.equal(r.resetReason, 'board_changed'); assert.deepEqual(r.bend, [0, 0, 0]); near(r.speed, 0);
});
run('pure board-normal motion never bends', () => {
  const { result } = travel({ velocity: [0, 0, 4] });
  assert.deepEqual(result.bend, [0, 0, 0]); near(result.speed, 0);
});
run('duplicate timestamps neither integrate nor poison the prior point', () => {
  const { state } = travel();
  const before = [...state.bend];
  const duplicate = sample(state, 1, [-900, 0, 0]);
  assert.deepEqual(state.bend, before); assert.equal(duplicate.valid, false);
  const next = sample(state, 1.01, [.202, 0, 0]);
  near(next.speed, .2); assert.ok(next.bend[0] < 0);
});
run('backwards time, long gap, and invalid time reset safely', () => {
  const { state } = travel();
  let r = sample(state, .5, [3, 5, 0]);
  assert.equal(r.resetReason, 'time_reversed'); assert.deepEqual(r.bend, [0, 0, 0]);
  sample(state, .51, [3.1, 5, 0]);
  r = sample(state, 2, [-80, 0, 0]);
  assert.equal(r.resetReason, 'sample_gap'); assert.deepEqual(r.bend, [0, 0, 0]);
  r = sample(state, Number.NaN, [0, 0, 0]);
  assert.equal(r.valid, false); assert.deepEqual(r.bend, [0, 0, 0]);
  r = sample(state, 3, [500, 0, 0]);
  near(r.speed, 0); assert.deepEqual(r.bend, [0, 0, 0]);
});
run('invalid point decays and requires a new baseline', () => {
  const { state } = travel(); const before = norm(state.bend);
  const r = sample(state, 1.01, [Number.NaN, 0, 0]);
  assert.equal(r.valid, false); assert.ok(norm(r.bend) < before);
  const next = sample(state, 1.02, [10, 0, 0]);
  near(next.speed, 0); assert.equal(next.resetReason, 'contact_started');
});
run('sub-deadzone motion does not accumulate drift', () => {
  const state = new NibDragState();
  for (let i = 0; i < 120; i++) sample(state, i / 60, [i % 2 ? .00002 : 0, 0, 0]);
  assert.deepEqual(state.bend, [0, 0, 0]);
});
run('large and changing velocities remain bounded', () => {
  const state = new NibDragState();
  for (let i = 0; i < 600; i++) {
    const r = sample(state, i / 60, [Math.sin(i * .08) * 10, Math.cos(i * .08) * 10, 0]);
    assert.ok(norm(r.bend) <= state.maxBend + 1e-12);
  }
});
run('reversal converges to the new opposite direction', () => {
  const { state } = travel();
  const { result } = travel({ state, start: 1, origin: [.2, 0, 0], velocity: [-.2, 0, 0] });
  assert.ok(result.bend[0] > .0035);
});
run('shaft, ferrule, root and visible contact tip are unchanged', () => {
  for (const p of [[.01, .01, -.1], [.002, .001, .06], [.002, .001, .0677], [0, 0, .108], [0, 0, .12]])
    assert.deepEqual(deformPosition(p, [.003, .002, .001]), p);
});
run('only bristle belly bows, including transformed pen-local Z', () => {
  const p = [.002, .001, (.0677 + .108) / 2], bend = [.002, -.003, .001];
  const out = deformPosition(p, bend);
  out.forEach((v, i) => near(v, p[i] + bend[i]));
  assert.deepEqual(p, [.002, .001, (.0677 + .108) / 2]); // Inputs are not mutated.
});
const hzResults = {};
run('30/60/90/120 Hz produce the same constant-speed response and return', () => {
  for (const hz of [30, 60, 90, 120]) {
    const { state } = travel({ hz, seconds: 1 });
    const moving = [...state.bend];
    for (let i = 1; i <= hz / 2; i++) sample(state, 1 + i / hz, [.2, 0, 0]);
    hzResults[hz] = { moving, stopped: [...state.bend] };
  }
  const baseline = hzResults[60];
  for (const result of Object.values(hzResults)) {
    near(result.moving[0], baseline.moving[0], 1e-9);
    near(result.stopped[0], baseline.stopped[0], 1e-9);
  }
});
run('invalid configuration and deformation arguments are rejected', () => {
  assert.throws(() => new NibDragState({ returnTau: 0 }), RangeError);
  assert.throws(() => new NibDragState({ maxBend: -1 }), RangeError);
  assert.throws(() => deformPosition([0, 0, 0], [NaN, 0, 0]), RangeError);
});

console.log(JSON.stringify({ scope: 'Pure JS prototype; synthetic samples, not Unity or headset verification',
  passed: true, count: checks.length, checks, hzResults }, null, 2));
