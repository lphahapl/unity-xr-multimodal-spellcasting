"""Quiet recorded-guzheng/dizi arrangement; viola supplies background only.

Uses CC0 Pufermufin recordings; XSXB-Band-Samples supplies candidate cut times.
Recuts the original stereo HQ MP3 directly, avoiding processed FLAC tails.
Writes a candidate and dry stems under Logs before publication to Unity.
"""

import json
import hashlib
from collections import defaultdict
from pathlib import Path

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy import signal

from compose_yicun_shanhe import DURATION, SR, bronze_note, circular_room
from render_yicun_shanhe_ambient import build_score, load_dizi, sampled_dizi
from render_yicun_shanhe_chamber import load_stereo_bank

ROOT = Path(__file__).resolve().parents[1]
SAMPLE_DIR = ROOT / "ArtSource/Audio/YicunShanhe/Guzheng"
LOG = ROOT / "Logs/yicun-shanhe-guzheng-v4"
# Preserve the same flute performance randomization as v3.
SEED = 202611043
SHARES = {"guzheng": .30, "dizi": .64, "viola": .059, "bell": .001}


def build_guzheng_score():
    old = build_score()
    score = {"guzheng": [], "dizi": old["dizi"], "viola": [], "bell": old["bell"]}
    for onset, pitch, duration, velocity, technique in old["qin"]:
        if velocity > .6 or technique == "harmonic":
            pitch += 12
        if pitch == 43:
            pitch = 55  # Keep the G pedal clear; avoid the muddy low-G recording.
        score["guzheng"].append((onset, pitch, duration, velocity, "pluck"))
    score["guzheng"].append((31.8, 76, 1.0, .29, "pluck"))
    # Transfer former string themes to the guzheng. At 40s use only a fragment.
    for onset, pitch, duration, velocity, technique in old["sheng"]:
        if technique == "ordinary":
            if 40 <= onset < 48 and onset > 43:
                continue
            score["guzheng"].append((onset, pitch, duration, velocity*.83, "pluck"))
        else:
            score["viola"].append((onset, pitch, duration, velocity, "background"))
    return {name: sorted(events) for name, events in score.items()}


def load_guzheng():
    metadata = json.loads((SAMPLE_DIR / "selected-samples.json").read_text(encoding="utf-8"))
    cuts = metadata["upstream"]["cuts"]
    cut_by_name = {Path(c["file"]).stem: c for c in cuts}
    # Avoid candidates with pronounced repeated attacks; quieter alternatives exist.
    selected = {"guzheng-50-v1-rr1", "guzheng-53-v1-rr2", "guzheng-55-v1-rr2",
                "guzheng-60-v1-rr1", "guzheng-60-v1-rr2", "guzheng-62-v1-rr1",
                "guzheng-62-v1-rr2", "guzheng-67-v1-rr2",
                "guzheng-72-v1-rr2", "guzheng-74-v1-rr1"}
    # Audited on the original recording; several later plucks are absent from metadata.
    clean_limits = {"guzheng-50-v1-rr1": .70, "guzheng-55-v1-rr2": .50,
                    "guzheng-62-v1-rr2": 2.20, "guzheng-74-v1-rr1": .84}
    source_path = SAMPLE_DIR / "Pufermufin_396868_original_hq.mp3"
    assert hashlib.sha256(source_path.read_bytes()).hexdigest() == metadata["upstream"]["sha256"], "Source download incomplete or unexpected"
    source, rate = sf.read(source_path)
    assert source.ndim == 2 and source.shape[1] == 2 and rate == SR
    assert abs(len(source)/rate-1474) < 1
    bank = defaultdict(list)
    mapping = []
    for entry in metadata["samples"]:
        name = Path(entry["local_file"]).stem
        if name not in selected:
            continue
        cut = cut_by_name[name]
        # End before another documented pluck inside this extracted recording.
        later = [c["start"]-cut["start"] for c in cuts
                 if cut["start"]+.1 < c["start"] < cut["start"]+cut["length"]]
        clean_seconds = min([cut["length"], 2.4, clean_limits.get(name, 2.4)]+[t-.22 for t in later])
        start = round(cut["start"]*rate)
        sample = source[start:start+round(clean_seconds*rate)].copy()
        assert len(sample) == round(clean_seconds*rate) and np.isfinite(sample).all()
        sample -= np.mean(sample, axis=0)
        # Balance recording gain while retaining note dynamics in the score.
        level = np.sqrt(np.mean(sample[:round(.5*SR)]**2))
        sample /= max(level, 1e-7)
        record = {"sample": sample, "tune": entry.get("tuneCents", 0), "file": entry["local_file"]}
        bank[entry["midi"]].append(record)
        mapping.append({"file": entry["local_file"], "root": entry["midi"],
                        "source_start": cut["start"], "tune_cents": record["tune"],
                        "clean_source_seconds": clean_seconds})
    return dict(bank), mapping


def guzheng_note(bank, pitch, duration, counters):
    root = min(bank, key=lambda p: abs(pitch-p))
    assert abs(pitch-root) <= 2
    choices = bank[root]
    choice = choices[counters[root] % len(choices)]
    counters[root] += 1
    ratio = 2**((pitch-root+choice["tune"]/100)/12)
    sound = signal.resample_poly(choice["sample"], 10000, round(ratio*10000), axis=0)
    # Let the recorded string decay; do not sustain-loop or synthesize its body.
    count = min(len(sound), round(max(2.2, duration+2.0)*SR))
    sound = sound[:count].copy()
    attack = min(count, round(.002*SR))
    release = min(round(count*.25), round(.35*SR))
    sound[:attack] *= (.5-.5*np.cos(np.linspace(0, np.pi, attack)))[:, None]
    sound[-release:] *= (.5+.5*np.cos(np.linspace(0, np.pi, release)))[:, None]
    return sound, {"pitch": pitch, "root": root, "sample": choice["file"],
                   "semitones": pitch-root, "duration_seconds": count/SR}


def viola_pad(bank, pitch, duration):
    root = min(bank, key=lambda p: abs(p-pitch))
    assert abs(root-pitch) <= 2
    ratio = 2**((pitch-root)/12)
    sound = signal.resample_poly(bank[root], 1000, round(ratio*1000), axis=0)
    count = round((duration+.5)*SR)
    assert count <= len(sound)
    sound = sound[:count].copy()
    attack = round(min(.9, duration*.3)*SR)
    release = round(.65*SR)
    sound[:attack] *= (.5-.5*np.cos(np.linspace(0, np.pi, attack)))[:, None]
    sound[-release:] *= (.5+.5*np.cos(np.linspace(0, np.pi, release)))[:, None]
    return sound


def render():
    LOG.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(SEED)
    score = build_guzheng_score()
    guzheng, mapping = load_guzheng()
    viola = load_stereo_bank("viola")
    flute = load_dizi()
    counters = defaultdict(int)
    rendered_notes = []
    stems = {}
    length = DURATION*SR
    for instrument, events in score.items():
        stem = np.zeros((length, 2))
        for onset, pitch, duration, velocity, _ in events:
            if instrument == "guzheng":
                stereo, details = guzheng_note(guzheng, pitch, duration, counters)
                rendered_notes.append({"onset": onset, **details})
                note = stereo*np.array([1.04, .96])
            elif instrument == "dizi":
                mono = sampled_dizi(flute, pitch, duration, rng)
                note = mono[:, None]*np.array([.66, .75])
            elif instrument == "viola":
                note = viola_pad(viola, pitch, duration)
            else:
                mono = bronze_note(pitch, duration, 1)
                note = mono[:, None]*np.array([.7, .7])
            start = round(onset*SR)
            count = min(len(note), length-start)
            stem[start:start+count] += note[:count]*velocity
            stem[:len(note)-count] += note[count:]*velocity
        stem *= np.sqrt(SHARES[instrument]/np.mean(stem**2))
        stems[instrument] = stem
    mix = circular_room(sum(stems.values()), rng)
    freqs = np.fft.rfftfreq(length, 1/SR)
    highpass = freqs**2/(freqs**2+70**2)
    for channel in range(2):
        mix[:, channel] = np.fft.irfft(np.fft.rfft(mix[:, channel])*highpass, n=length)
    meter = pyln.Meter(SR)
    gain = 10**((-29-meter.integrated_loudness(mix))/20)
    mix *= gain
    assert np.isfinite(mix).all() and np.max(np.abs(mix)) < .5
    output = LOG / "yicun_shanhe_guzheng_v4.wav"
    sf.write(output, mix, SR, subtype="PCM_16")
    for instrument, stem in stems.items():
        sf.write(LOG / f"dry_{instrument}.wav", stem*gain, SR, subtype="PCM_24")
    pcm, _ = sf.read(output)
    report = {
        "version": 4, "seed": SEED, "duration_seconds": DURATION, "sample_rate": SR,
        "lufs": float(meter.integrated_loudness(pcm)),
        "sample_peak_dbfs": float(20*np.log10(np.max(np.abs(pcm)))),
        "rms_dbfs": float(20*np.log10(np.sqrt(np.mean(pcm**2)))),
        "boundary_jump": np.abs(pcm[0]-pcm[-1]).tolist(),
        "dry_stem_energy_share": SHARES,
        "dry_viola_vs_dizi_db": float(10*np.log10(SHARES["viola"]/SHARES["dizi"])),
        "events": {name: len(events) for name, events in score.items()},
        "section_rms_dbfs": [float(20*np.log10(np.sqrt(np.mean(pcm[a*SR:b*SR]**2))))
                             for a, b in [(0,16),(16,40),(40,64),(64,80),(80,96)]],
        "timbres": {"guzheng": "Pufermufin original recorded stereo HQ MP3, directly recut",
                    "dizi": "Hypnotriod recorded sustained notes",
                    "viola": "VSCO-2-CE real stereo viola section, background only",
                    "bell": "modeled bronze"},
        "guzheng_mapping": mapping, "guzheng_rendered_notes": rendered_notes,
        "score": score,
    }
    (LOG / "render.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items()
                      if k not in ("score", "guzheng_mapping", "guzheng_rendered_notes")}, indent=2))


if __name__ == "__main__":
    render()
