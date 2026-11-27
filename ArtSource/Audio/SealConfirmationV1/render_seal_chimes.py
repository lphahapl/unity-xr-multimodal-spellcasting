"""Deterministic single-impact modal SFX, not recordings of acoustic instruments.

Run from any directory: python -X utf8 render_seal_chimes.py [--stage first|all]
Writes ONLY the five commissioned wav files, and local QA/audition artifacts.
The existing LvChime.wav is a read-only reference guarded by SHA-256.
Requires numpy and scipy. No Unity, network, random external sample, or limiter.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import wave

import numpy as np
from scipy import signal
from scipy.io import wavfile


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ASSETS = ROOT / "Assets/Art/VFX/Skills/SealConfirmation"
REFERENCE = ASSETS / "LvChime.wav"
REFERENCE_SHA = "603a8782c8355ec8d0af278f510e32e2bc684079444d9e118add0724277825f4"
FS = 48000
N400 = int(FS * .4)

# Each tuple: frequency Hz, relative displacement amplitude, amplitude tau seconds.
# Independently chosen mode ratios, damping and impact texture: not transpositions.
DESIGNS = {
    "Gui": dict(label="规", duration=.72, seed=4101, attack=.0018,
                description="圆润内敛的中低铜体，单次击发，圆口短余振",
                modes=[(352,1,.115),(594,.74,.092),(982,.39,.067),
                       (1471,.20,.043),(2288,.08,.024),(3196,.025,.013)],
                doublet=.0031, doublet_mix=.18, contact_band=(1600,6200),
                contact_tau=.0029, contact_gain=.13, highpass=110, k_offset=-.15),
    "Ju": dict(label="矩", duration=.57, seed=4102, attack=.0011,
               description="短促干净的硬棱角敲击，中频短体、较明晰触面",
               modes=[(477,.72,.087),(791,-1,.0675),(1367,-.70,.0465),
                      (2049,.31,.030),(2873,.19,.0195),(4312,.07,.012)],
               doublet=.0012, doublet_mix=.09, contact_band=(2300,8700),
               contact_tau=.0017, contact_gain=.32, highpass=170, k_offset=.05),
    "Heng": dict(label="衡", duration=.73, seed=4103, attack=.0021,
                 description="低厚但收住的铜体，抑制超低频，单次紧凑落定",
                 modes=[(216,.78,.098),(387,1,.082),(692,.55,.058),
                        (1061,.22,.036),(1847,.10,.021),(2619,.035,.012)],
                 doublet=.0022, doublet_mix=.17, contact_band=(1200,5100),
                 contact_tau=.0031, contact_gain=.14, highpass=145, k_offset=-.20),
    "Zhun": dict(label="准", duration=.70, seed=4104, attack=.0019,
                 description="平稳轻清的石磬近似音，较少粗糙边带、平滑短尾",
                 modes=[(704,1,.113),(1191,.45,.083),(1943,.21,.054),
                        (3017,.095,.031),(4149,.025,.016)],
                 doublet=.0008, doublet_mix=.06, contact_band=(2100,7400),
                 contact_tau=.0022, contact_gain=.09, highpass=230, k_offset=.10),
    "Sheng": dict(label="绳", duration=.71, seed=4105, attack=.0014,
                  description="细而绷紧的金属线谐鸣，非拨弦，单次触发无乐句",
                  modes=[(927,.80,.105),(1581,1,.082),(2473,.44,.057),
                         (3619,.25,.032),(4973,.09,.017),(6221,.035,.010)],
                  doublet=.0043, doublet_mix=.27, contact_band=(3100,10400),
                  contact_tau=.0016, contact_gain=.19, highpass=370, k_offset=.20),
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rms(x: np.ndarray) -> float:
    return float(np.sqrt(np.mean(np.square(x))))


def db(x: float) -> float:
    return float(20 * np.log10(max(1e-12, x)))


def k_weight(x: np.ndarray) -> np.ndarray:
    # Standard 48 kHz BS.1770 prefilter + RLB coefficients. Used for relative
    # 400ms energy only; these SFX are too short to claim integrated LUFS.
    x = signal.lfilter([1.53512485958697, -2.69169618940638, 1.19839281085285],
                       [1, -1.69065929318241, .73248077421585], x)
    return signal.lfilter([1, -2, 1], [1, -1.99004745483398, .99007225036621], x)


def load(path: Path) -> np.ndarray:
    rate, data = wavfile.read(path)
    assert rate == FS and data.dtype == np.int16 and data.ndim == 1
    return data.astype(np.float64) / 32768


def synthesize(config: dict, reference: np.ndarray) -> np.ndarray:
    rng = np.random.default_rng(config['seed'])
    t = np.arange(round(FS * config['duration'])) / FS
    body = np.zeros_like(t)
    for freq, amplitude, tau in config['modes']:
        # Signed amplitudes represent mode polarity at impact/listener positions.
        # A damped mode and weak nearby split mode approximate an asymmetric
        # solid resonator. All excited at t=0; no delay, second strike or note.
        split = config['doublet'] * rng.uniform(.72, 1.26)
        wave_a = np.sin(2 * np.pi * freq * t)
        wave_b = np.sin(2 * np.pi * freq * (1 + split) * t)
        body += amplitude * np.exp(-t/tau) * (
            wave_a + config['doublet_mix'] * wave_b)
    contact = rng.standard_normal(len(t))
    contact_filter = signal.butter(2, config['contact_band'], fs=FS,
                                   btype='bandpass', output='sos')
    contact = signal.sosfilt(contact_filter, contact)
    contact /= max(rms(contact), 1e-12)
    contact *= np.exp(-t/config['contact_tau']) * config['contact_gain']
    x = body + contact
    x *= np.sin(np.minimum(t/config['attack'], 1) * np.pi/2) ** 2
    x = signal.sosfilt(signal.butter(2, config['highpass'], fs=FS,
                                    btype='highpass', output='sos'), x)
    # Natural modal damping precedes this final safety fade. Avoid a cut tail.
    fade_n = round(FS * .055)
    x[-fade_n:] *= np.cos(np.linspace(0, np.pi/2, fade_n)) ** 2
    x[0] = x[-1] = 0
    target = db(rms(k_weight(reference)[:N400])) + config['k_offset']
    gain = 10 ** ((target - db(rms(k_weight(x)[:N400]))) / 20)
    x *= gain
    assert np.max(np.abs(x)) < 10 ** (-2.5/20), (
        f"{config['label']} peak {db(float(np.max(np.abs(x)))):.2f}dBFS too hot; redesign, no limiter")
    return np.round(x * 32767).astype(np.int16)


def analyze(path: Path, ref: np.ndarray) -> dict:
    x = load(path)
    f, power = signal.welch(x, fs=FS, nperseg=16384)
    peaks, _ = signal.find_peaks(power, distance=4)
    peaks = sorted(peaks, key=lambda i: power[i], reverse=True)[:7]
    csum = np.cumsum(x*x)
    total = float(csum[-1])
    bins = np.array([rms(x[i:i+480]) for i in range(0, len(x), 480)])
    peak_i = int(np.argmax(bins))
    onset_peak = max(bins[:4])
    def decay(level: float):
        found = np.where(bins[peak_i:] <= onset_peak * 10**(level/20))[0]
        return float((found[0]+peak_i)*.01) if len(found) else None
    with wave.open(str(path), 'rb') as wav:
        fmt = dict(sample_rate=wav.getframerate(), channels=wav.getnchannels(),
                   pcm_bits=wav.getsampwidth()*8, samples=wav.getnframes())
    raw400 = db(rms(x[:N400]))
    k400 = db(rms(k_weight(x)[:N400]))
    report = dict(file=str(path.relative_to(ROOT)).replace('\\','/'), sha256=sha(path),
                  **fmt, duration_seconds=len(x)/FS,
                  peak_dbfs=db(float(np.max(np.abs(x)))), rms_dbfs=db(rms(x)),
                  first400_raw_rms_dbfs=raw400, first400_k_rms_dbfs=k400,
                  delta_first400_raw_db=raw400-db(rms(ref[:N400])),
                  delta_first400_k_db=k400-db(rms(k_weight(ref)[:N400])),
                  first_sample=int(round(x[0]*32768)), last_sample=int(round(x[-1]*32768)),
                  last10ms_peak_dbfs=db(float(np.max(np.abs(x[-480:])))),
                  clipped_samples=int(np.sum(np.abs(x)>=32767/32768)),
                  dc_mean=float(np.mean(x)),
                  spectral_centroid_hz=float(np.sum(f*power)/np.sum(power)),
                  sub150hz_energy_percent=float(100*np.sum(power[f<150])/np.sum(power)),
                  dominant_peaks_hz=[round(float(f[i]),2) for i in peaks],
                  energy95_seconds=float(np.searchsorted(csum,total*.95)/FS),
                  decay20_seconds=decay(-20), decay40_seconds=decay(-40),
                  envelope_10ms_rms_dbfs=[round(db(float(v)),2) for v in bins])
    if path != REFERENCE:
        assert report['duration_seconds'] <= .74
        assert abs(report['delta_first400_raw_db']) <= 2.0
        assert abs(report['delta_first400_k_db']) <= 2.0
        assert report['clipped_samples'] == 0
        assert report['first_sample'] == report['last_sample'] == 0
        assert report['last10ms_peak_dbfs'] < -75
    return report


def comparison_metrics(signals: dict) -> list[dict]:
    result = []
    names = list(signals)
    for i, a in enumerate(names):
        for b in names[i+1:]:
            x,y = signals[a][:N400], signals[b][:N400]
            norm = np.linalg.norm(x)*np.linalg.norm(y)
            corr = signal.correlate(x,y,mode='full',method='fft') / norm
            center = len(y)-1
            # Waveform comparisons do not prove perceived discriminability.
            result.append(dict(a=a,b=b,aligned_correlation=float(np.dot(x,y)/norm),
                               max_abs_cross_correlation_lag_50ms=float(
                                   np.max(np.abs(corr[center-2400:center+2401])))))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', choices=['first','all'], default='all')
    args = parser.parse_args()
    assert sha(REFERENCE) == REFERENCE_SHA, 'Reference differs; do not overwrite'
    reference = load(REFERENCE)
    selected = list(DESIGNS)[:3] if args.stage == 'first' else list(DESIGNS)
    for name in selected:
        target = ASSETS / f'{name}Chime.wav'
        wavfile.write(target, FS, synthesize(DESIGNS[name], reference))
    signals = {'Lv':reference, **{name:load(ASSETS/f'{name}Chime.wav') for name in selected}}
    reports = {name:analyze(REFERENCE if name == 'Lv' else ASSETS/f'{name}Chime.wav',reference)
               for name in signals}
    segments = []
    timing = []
    position = 0
    # Isolated, equal gain clips at a 1.5s cadence. Starts with established Lv.
    for name, data in signals.items():
        timing.append(dict(seal=name, label='律' if name=='Lv' else DESIGNS[name]['label'],
                           start_seconds=position/FS, end_seconds=(position+len(data))/FS))
        segment = np.pad(data, (0, round(FS*1.5)-len(data)))
        segments.append(segment)
        position += len(segment)
    audition = HERE / f'comparison_{args.stage}.wav'
    wavfile.write(audition,FS,np.round(np.concatenate(segments)*32768).astype(np.int16))
    qa = dict(version=1, stage=args.stage, method='deterministic damped modal approximation',
              provenance='Original synthetic audio, not acoustic sample recordings',
              reference_sha256_before=REFERENCE_SHA, reference_sha256_after=sha(REFERENCE),
              subjective_listening_status='NOT PERFORMED; waveform QA only',
              unity_playback_status='NOT PERFORMED by audio worker',
              first400_measurement='RMS energy and 48kHz BS.1770 K-weighted RMS; NOT integrated LUFS',
              designs={name:DESIGNS[name] for name in selected}, clips=reports,
              pairwise_waveform_metrics=comparison_metrics(signals),
              audition_file=str(audition.relative_to(ROOT)).replace('\\','/'),
              audition_order=timing)
    (HERE/f'qa_{args.stage}.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    assert sha(REFERENCE) == REFERENCE_SHA
    for name, row in reports.items():
        print(f"{name:5s} {row['duration_seconds']:.2f}s peak={row['peak_dbfs']:.2f} "
              f"rms={row['rms_dbfs']:.2f} raw400diff={row['delta_first400_raw_db']:+.2f} "
              f"K400diff={row['delta_first400_k_db']:+.2f} "
              f"peaksHz={row['dominant_peaks_hz'][:3]}")
    print('PASS: source hash preserved, format/duration/energy/clipping/tail checks')
    print('Listening and Unity playback remain for parent/human acceptance.')


if __name__ == '__main__':
    main()
