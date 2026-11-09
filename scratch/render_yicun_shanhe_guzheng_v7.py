"""Revision 7: full-track source, phrasing and close-voicing cleanup.

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
LOG = ROOT / "Logs/yicun-shanhe-quiet-v7"
# Preserve the same flute performance randomization as v3.
SEED = 202611043
SHARES = {"guzheng": .25, "dizi": .72, "viola": .029, "bell": .001}


def build_v6_score():
    old = build_score()
    score = {"guzheng": [], "dizi": old["dizi"], "viola": [], "bell": old["bell"]}
    for onset, pitch, duration, velocity, technique in old["qin"]:
        melody = velocity > .6
        if melody:
            duration -= .55  # Restore the accepted motif's original note values.
        if melody or technique == "harmonic":
            pitch += 12
        if pitch in (48, 50):
            continue  # This low-D recording carries other strings; leave space instead.
        if pitch == 43:
            pitch = 55  # Keep the G pedal clear; avoid the muddy low-G recording.
        score["guzheng"].append((onset, pitch, duration, velocity, "melody" if melody else "pluck"))
    score["guzheng"].append((31.8, 76, 1.0, .29, "pluck"))
    # Transfer former string themes to the guzheng. At 40s use only a fragment.
    for onset, pitch, duration, velocity, technique in old["sheng"]:
        if technique == "ordinary":
            if 40 <= onset < 48 and onset > 43:
                continue
            score["guzheng"].append((onset, pitch, duration, velocity*.83, "melody"))
        else:
            score["viola"].append((onset, pitch, duration, velocity, "background"))
    return {name: sorted(events) for name, events in score.items()}


def build_guzheng_score():
    score = build_v6_score()
    # Dizi is one wind player: end its dry release before the next breath/attack.
    adjusted = []
    for i, event in enumerate(score["dizi"]):
        onset, pitch, duration, velocity, technique = event
        if i+1 < len(score["dizi"]):
            following = score["dizi"][i+1][0]
            duration = min(duration, following-onset-.04-.14)
        assert duration > .3
        adjusted.append((onset, pitch, duration, velocity, technique))
    score["dizi"] = adjusted
    # C5 responses arrive after the flute resolves D5 -> C5, not before it.
    adjusted = []
    for event in score["guzheng"]:
        onset, pitch, duration, velocity, technique = event
        if abs(onset-13.7) < .001 and pitch == 52:
            continue  # One low fill depends on the F3 take with G3-string spill.
        if any(abs(onset-t) < .001 for t in [22.7, 62.8, 78.8]):
            following = next(e[0] for e in score["dizi"] if e[0] > onset and e[1] == 72)
            onset = following+.08
        adjusted.append((onset, pitch, duration, velocity, technique))
    score["guzheng"] = sorted(adjusted)
    # Let the background open up around low-register D4 and the flute's A4.
    adjusted = []
    for event in score["viola"]:
        onset, pitch, duration, velocity, technique = event
        if pitch == 67 and abs(onset-35.775) < .001:
            continue
        if pitch == 60 and abs(onset-3.9) < .001:
            duration = 1.35
        elif pitch == 60 and abs(onset-83.1) < .001:
            duration = 2.15
        elif 54.19 < onset < 54.28:
            onset += .85
            duration -= .85
        adjusted.append((onset, pitch, duration, velocity, technique))
    score["viola"] = sorted(adjusted)
    return score


def reduce_confirmed_spill(sample, bands):
    """Small source-specific EQ cuts; retain the recorded body, attack and stereo."""
    sound = sample.copy()
    for center, bandwidth, db in bands:
        q = center/bandwidth
        amplitude = 10**(db/40)
        omega = 2*np.pi*center/SR
        alpha = np.sin(omega)/(2*q)
        b = np.array([1+alpha*amplitude, -2*np.cos(omega), 1-alpha*amplitude])
        a = np.array([1+alpha/amplitude, -2*np.cos(omega), 1-alpha/amplitude])
        sound = signal.lfilter(b/a[0], a/a[0], sound, axis=0)
    # Preserve the first 30ms of the original pluck; ease the narrow cuts in.
    time = np.arange(len(sample))/SR
    wet = np.clip((time-.03)/.09, 0, 1)
    wet = .5-.5*np.cos(np.pi*wet)
    return sample*(1-wet[:, None])+sound*wet[:, None]


def load_guzheng():
    metadata = json.loads((SAMPLE_DIR / "selected-samples.json").read_text(encoding="utf-8"))
    cuts = metadata["upstream"]["cuts"]
    cut_by_name = {Path(c["file"]).stem: c for c in cuts}
    # Avoid candidates with pronounced repeated attacks; quieter alternatives exist.
    selected = {"guzheng-57-v1-rr1",
                "guzheng-60-v1-rr2", "guzheng-62-v1-rr1", "guzheng-67-v1-rr2",
                "guzheng-72-v1-rr2", "guzheng-74-v1-rr1"}
    # Audited on the original recording; several later plucks are absent from metadata.
    clean_limits = {"guzheng-57-v1-rr1": 1.00, "guzheng-74-v1-rr1": .84}
    source_path = SAMPLE_DIR / "Pufermufin_396868_original_hq.mp3"
    assert hashlib.sha256(source_path.read_bytes()).hexdigest() == metadata["upstream"]["sha256"], "Source download incomplete or unexpected"
    source, rate = sf.read(source_path)
    assert source.ndim == 2 and source.shape[1] == 2 and rate == SR
    assert abs(len(source)/rate-1474) < 1
    bank = defaultdict(list)
    mapping = []
    original_metadata = json.loads((SAMPLE_DIR / "instrument-source.json").read_text(encoding="utf-8"))
    for original_entry in original_metadata["samples"]:
        entry = {**original_entry, "local_file": Path(original_entry["file"]).name}
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
        # Only independently confirmed foreign-string families are attenuated.
        spill_bands = {57: [(196.7, 6, -8), (393.2, 10, -6), (787.8, 14, -6)],
                       67: [(440.4, 7, -8), (882, 12, -6), (1322, 18, -6)]}.get(entry["midi"], [])
        sample = reduce_confirmed_spill(sample, spill_bands)
        sample /= max(level, 1e-7)
        record = {"sample": sample, "tune": entry.get("tuneCents", 0), "file": entry["local_file"]}
        bank[entry["midi"]].append(record)
        mapping.append({"file": entry["local_file"], "root": entry["midi"],
                        "source_start": cut["start"], "tune_cents": record["tune"],
                        "clean_source_seconds": clean_seconds,
                        "spill_eq": {"bands_hz_bandwidth_db": spill_bands, "fade_in_seconds": [.03, .12]}})
    return dict(bank), mapping


def guzheng_note(bank, pitch, duration, counters, damped=False):
    # Prefer the audited A3 recording for G3; both adjacent roots are two semitones away.
    root = 57 if pitch == 55 else min(bank, key=lambda p: abs(pitch-p))
    assert abs(pitch-root) <= 2
    choices = bank[root]
    choice = choices[counters[root] % len(choices)]
    counters[root] += 1
    ratio = 2**((pitch-root+choice["tune"]/100)/12)
    sound = signal.resample_poly(choice["sample"], 10000, round(ratio*10000), axis=0)
    # Melody damping respects its note value; accompaniment may ring naturally.
    # The recorded body is neither sustain-looped nor synthesized.
    maximum = duration if damped else max(2.2, duration+2.0)
    count = min(len(sound), round(maximum*SR))
    sound = sound[:count].copy()
    attack = min(count, round(.002*SR))
    release = min(round(count*.25), round(.35*SR))
    sound[:attack] *= (.5-.5*np.cos(np.linspace(0, np.pi, attack)))[:, None]
    sound[-release:] *= (.5+.5*np.cos(np.linspace(0, np.pi, release)))[:, None]
    return sound, {"pitch": pitch, "root": root, "sample": choice["file"], "damped": damped,
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

# The recovered v7 source and note helpers are used by the v11 renderer.
