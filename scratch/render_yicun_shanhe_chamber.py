"""A flute, sampled harp and viola version of the approved ambient theme.

Uses unprocessed VSCO-2-CE stereo recordings and the existing recorded dizi bank.
Rendering writes a candidate first; publication follows numerical/independent QA.
"""

import json
from pathlib import Path

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy import signal

from compose_yicun_shanhe import DURATION, SR, bronze_note, circular_room
from render_yicun_shanhe_ambient import build_score, load_dizi, sampled_dizi


ROOT = Path(__file__).resolve().parents[1]
SAMPLE_DIR = ROOT / "ArtSource/Audio/YicunShanhe/VSCO"
LOG = ROOT / "Logs/yicun-shanhe-chamber-v3"
SEED = 202611043


def load_stereo_bank(instrument):
    pitches = {"A2":45, "C3":48, "E3":52, "G3":55, "B3":59,
               "D4":62, "F4":65, "A4":69, "C5":72, "E5":76}
    if instrument == "viola":
        # Viola filenames use an octave convention below the harp bank's.
        # Verified against ViolaEnsSusVib.sfz and each recording's fundamental.
        pitches = {"G2":55, "B2":59, "D3":62, "F3":65, "A3":69}
    bank = {}
    for name, pitch in pitches.items():
        filename = f"KSHarp_{name}_mf.wav"
        if instrument == "viola":
            filename = f"ViolaEns_susvib_{name}_v1_1.wav"
        sample, rate = sf.read(SAMPLE_DIR / filename)
        assert rate == SR and sample.ndim == 2
        sample -= sample.mean(axis=0)
        # Retain the real stereo image and harmonic spectrum. Equalize recording gain only.
        reference = sample[:round(.5*SR)] if instrument == "harp" else sample[SR:3*SR]
        sample /= max(np.sqrt(np.mean(reference**2)), 1e-7)
        bank[pitch] = sample
    return bank


def stereo_note(bank, pitch, duration, instrument):
    root = min(bank, key=lambda value: abs(value-pitch))
    assert abs(pitch-root) <= 3, (instrument, pitch, root)
    ratio = 2**((pitch-root)/12)
    sound = signal.resample_poly(bank[root], 1000, round(ratio*1000), axis=0)
    if instrument == "harp":
        # The harp's natural ringing extends well beyond the notated note value.
        duration = min(len(sound)/SR, max(4.5, duration+3.0))
        release_seconds = 1.2
        attack_seconds = .003
    else:
        duration += .32
        release_seconds = .34
        attack_seconds = min(.48, duration * .3)
    count = min(len(sound), round(duration * SR))
    sound = sound[:count].copy()
    attack = min(count, round(attack_seconds*SR))
    release = min(count, round(release_seconds*SR))
    sound[:attack] *= (.5-.5*np.cos(np.linspace(0,np.pi,attack)))[:, None]
    sound[-release:] *= (.5+.5*np.cos(np.linspace(0,np.pi,release)))[:, None]
    return sound


def render():
    rng = np.random.default_rng(SEED)
    original = build_score()
    score = {"harp":[], "dizi":original["dizi"], "viola":original["sheng"], "bell":original["bell"]}
    for onset, pitch, duration, velocity, technique in original["qin"]:
        if velocity > .6 or technique == "harmonic":
            pitch += 12
        score["harp"].append((onset,pitch,duration,velocity, "plucked"))
    # A single high E response adds a small variation without a run or flourish.
    score["harp"].append((31.8,76,1.0,.29,"plucked"))
    banks = {"harp":load_stereo_bank("harp"), "viola":load_stereo_bank("viola")}
    flute = load_dizi()
    length = DURATION*SR
    stems = {}
    for instrument, events in score.items():
        stem = np.zeros((length,2))
        for onset,pitch,duration,velocity,_ in sorted(events):
            if instrument in banks:
                note = stereo_note(banks[instrument], pitch, duration, instrument)
            elif instrument == "dizi":
                mono = sampled_dizi(flute,pitch,duration,rng)
                note = mono[:,None]*np.array([.66,.75])
            else:
                mono = bronze_note(pitch,duration,1)
                note = mono[:,None]*np.array([.7,.7])
            start=round(onset*SR)
            count=min(len(note),length-start)
            stem[start:start+count] += note[:count]*velocity
            stem[:len(note)-count] += note[count:]*velocity
        stems[instrument]=stem
    shares={"harp":.24,"dizi":.52,"viola":.239,"bell":.001}
    mix=np.zeros((length,2))
    for instrument,stem in stems.items():
        stem *= np.sqrt(shares[instrument]/np.mean(stem**2))
        mix += stem
    mix=circular_room(mix,rng)
    freqs=np.fft.rfftfreq(length,1/SR)
    highpass=freqs**2/(freqs**2+70**2)
    for channel in range(2):
        mix[:,channel]=np.fft.irfft(np.fft.rfft(mix[:,channel])*highpass,n=length)
    meter=pyln.Meter(SR)
    mix *= 10**((-29-meter.integrated_loudness(mix))/20)
    assert np.isfinite(mix).all() and np.max(np.abs(mix))<.5
    LOG.mkdir(parents=True,exist_ok=True)
    output=LOG/"yicun_shanhe_chamber_v3.wav"
    sf.write(output,mix,SR,subtype="PCM_16")
    pcm,_=sf.read(output)
    report={
        "lufs":float(meter.integrated_loudness(pcm)),
        "sample_peak_dbfs":float(20*np.log10(np.max(np.abs(pcm)))),
        "rms_dbfs":float(20*np.log10(np.sqrt(np.mean(pcm**2)))),
        "boundary_jump":np.abs(pcm[0]-pcm[-1]).tolist(),
        "dry_stem_energy_share":shares,
        "events":{name:len(events) for name,events in score.items()},
        "section_rms_dbfs":[float(20*np.log10(np.sqrt(np.mean(pcm[a*SR:b*SR]**2))))
                            for a,b in [(0,16),(16,40),(40,64),(64,80),(80,96)]],
        "seed":SEED,
        "timbres":{"harp":"VSCO-2-CE recorded stereo multisamples",
                   "dizi":"Hypnotriod recorded sustained notes",
                   "viola":"VSCO-2-CE recorded viola section", "bell":"modeled bronze"},
    }
    (LOG/"render.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == "__main__":
    render()
