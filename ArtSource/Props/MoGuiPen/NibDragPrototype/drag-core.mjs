// PROTOTYPE ONLY. Board-local motion drives visible bristles, never the logical Nib.
const finitePoint = point => Array.isArray(point) && point.length === 3 && point.every(Number.isFinite);

export class NibDragState {
  constructor({ gainSeconds = .018, maxBend = .0045, bendTau = .045,
    returnTau = .060, deadSpeed = .005, maxGap = .12 } = {}) {
    for (const [name, value] of Object.entries({ gainSeconds, maxBend, bendTau, returnTau, deadSpeed, maxGap })) {
      if (!Number.isFinite(value) || value < 0 || (['bendTau', 'returnTau', 'maxGap'].includes(name) && value === 0))
        throw new RangeError(`Invalid ${name}`);
    }
    Object.assign(this, { gainSeconds, maxBend, bendTau, returnTau, deadSpeed, maxGap });
    this.bend = [0, 0, 0];
    this.previousTime = null;
    this.previousPoint = null;
    this.previousContact = false;
    this.previousBoard = null;
  }

  update({ time, point, contact, tracked = true, boardId = 'board' }) {
    const valid = Number.isFinite(time) && tracked && finitePoint(point);
    const active = valid && Boolean(contact);
    const snapshot = (resetReason = null, speed = 0, usable = valid) => ({
      bend: [...this.bend], speed, valid: Boolean(usable), contact: Boolean(active), resetReason
    });
    const zero = () => this.bend.fill(0);
    const baseline = () => {
      this.previousTime = time;
      this.previousBoard = boardId;
      this.previousContact = active;
      this.previousPoint = active ? [...point] : null;
    };
    const follow = (target, dt, tau) => {
      const amount = -Math.expm1(-dt / tau);
      for (let axis = 0; axis < 2; axis++)
        this.bend[axis] += (target[axis] - this.bend[axis]) * amount;
      this.bend[2] = 0;
    };
    if (!Number.isFinite(time)) {
      zero();
      this.previousTime = this.previousPoint = null;
      this.previousContact = false;
      return snapshot('invalid_time', 0, false);
    }
    if (this.previousTime === null) {
      zero(); baseline();
      return snapshot('first_sample');
    }
    const dt = time - this.previousTime;
    if (dt === 0) return snapshot('duplicate_time', 0, false); // No integration or baseline mutation.
    if (dt < 0) {
      zero(); baseline();
      return snapshot('time_reversed');
    }
    if (dt > this.maxGap) {
      zero(); baseline();
      return snapshot('sample_gap');
    }
    if (boardId !== this.previousBoard) {
      zero(); baseline();
      return snapshot('board_changed');
    }
    if (!valid || !active) {
      const wasContact = this.previousContact;
      follow([0, 0], dt, this.returnTau); baseline();
      return snapshot(!tracked ? 'tracking_lost' : !valid ? 'invalid_point' : wasContact ? 'lifted' : 'no_contact');
    }
    if (!this.previousContact || !this.previousPoint) {
      follow([0, 0], dt, this.returnTau); baseline();
      return snapshot('contact_started');
    }
    // Z is the board normal; only the local XY displacement can drag the brush.
    const vx = (point[0] - this.previousPoint[0]) / dt;
    const vy = (point[1] - this.previousPoint[1]) / dt;
    const speed = Math.hypot(vx, vy);
    if (!Number.isFinite(speed)) {
      zero(); baseline();
      return snapshot('invalid_velocity', 0, false);
    }
    const moving = speed > this.deadSpeed;
    const distance = moving ? Math.min(this.maxBend, speed * this.gainSeconds) : 0;
    const target = moving ? [-vx / speed * distance, -vy / speed * distance] : [0, 0];
    follow(target, dt, moving ? this.bendTau : this.returnTau);
    baseline();
    return snapshot(null, speed);
  }
}

// Fixed root AND fixed contact tip: the bristle belly bows behind travel.
// Convert board-local bend to pen-local before calling; its Z need not be zero.
export function deformPosition(position, bendInPenLocal, { rootZ = .0677, tipZ = .108 } = {}) {
  if (!finitePoint(position) || !finitePoint(bendInPenLocal) || !Number.isFinite(rootZ) ||
    !Number.isFinite(tipZ) || tipZ <= rootZ) throw new RangeError('Invalid deformation input');
  const [x, y, z] = position;
  if (z <= rootZ || z >= tipZ) return [x, y, z];
  const weight = Math.sin(Math.PI * (z - rootZ) / (tipZ - rootZ)) ** 2;
  return position.map((value, axis) => value + bendInPenLocal[axis] * weight);
}
