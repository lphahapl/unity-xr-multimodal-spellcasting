"""Deterministic original canal ambience; numpy only, no model or source recording.

Run at repository root: python Tools/Environment/WaterPolish/generate_water.py
The synthesis uses circular FFT filtering and wrapped bubble grains: every layer
has the same period. There is no fade-to-silence at the loop join.
"""
from pathlib import Path
import hashlib
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Art/Environment/Opening/WaterPolish/Canal_GentleFlow_Loop.wav'
REPORT = ROOT / 'Logs/exterior-polish-20261108/water/audio-measurements.json'
SR, SECONDS, SEED = 32000, 18, 831074
rng = np.random.default_rng(SEED)
n = SR * SECONDS
t = np.arange(n) / SR
freq = np.fft.rfftfreq(n, 1/SR)

def noise(low, high, slope=0):
    shape = (np.maximum(freq, 1)/1000)**slope
    shape *= (1-np.exp(-(freq/low)**4)) * np.exp(-(freq/high)**4)
    z = np.fft.irfft(np.fft.rfft(rng.normal(size=n)) * shape, n)
    return z / np.std(z)

# Low wash, flowing mid-band and soft splashing; no sub rumble or piercing hiss.
mod = 1 + .12*np.sin(2*np.pi*3*t/SECONDS + .4) + .07*np.sin(2*np.pi*7*t/SECONDS)
x = .18*noise(140, 950, -.35)*mod
x += .11*noise(650, 4300, -.2)*(1+.16*np.sin(2*np.pi*5*t/SECONDS+1.2))
x += .025*noise(2000, 6500)*mod
# Overlapping damped bubble resonances, irregular pitch, duration and density.
for _ in range(290):
    duration = rng.uniform(.025, .15)
    count = int(duration*SR)
    u = np.arange(count)/SR
    pitch = rng.uniform(380, 2100)
    env = np.sin(np.pi*np.arange(count)/count)**2*np.exp(-u/duration*2)
    phase = 2*np.pi*(pitch*u - .22*pitch*u*u/duration)
    grain = rng.uniform(.025, .105)*env*np.sin(phase)
    start = rng.integers(n)
    np.add.at(x, (start+np.arange(count)) % n, grain)
# Remove DC, set a conservative peak. PCM upload is gapless and avoids Vorbis join ambiguity.
x -= np.mean(x)
x *= 10**(-7/20)/np.max(np.abs(x))
pcm = np.rint(x*32767).astype('<i2')
OUT.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(OUT), 'wb') as wav:
    wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(SR); wav.writeframes(pcm.tobytes())
y = pcm.astype(float)/32768
steps = np.diff(y)
join = float(y[0]-y[-1])
window = SR//20
rms = lambda a: float(np.sqrt(np.mean(a*a)))
report = dict(seed=SEED, synthesis='original circular filtered noise + wrapped bubble grains',
    sample_rate=SR, channels=1, seconds=SECONDS, pcm_bits=16, frames=n,
    wav_bytes=OUT.stat().st_size, decoded_pcm_bytes=n*2,
    peak_dbfs=float(20*np.log10(np.max(np.abs(y)))), rms_dbfs=float(20*np.log10(rms(y))),
    dc=float(np.mean(y)), clipped_samples=int(np.sum(np.abs(pcm.astype(int))>=32767)),
    loop_boundary_step=join, ordinary_step_rms=rms(steps),
    boundary_step_over_ordinary_rms=abs(join)/rms(steps),
    head_50ms_rms=rms(y[:window]), tail_50ms_rms=rms(y[-window:]),
    note='Boundary step is measured against normal adjacent sample changes; no artificial silent join.',
    sha256=hashlib.sha256(OUT.read_bytes()).hexdigest())
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, indent=2))
