"""104-second ambient arrangement: introduction, development and quiet return.

Reuses v7's audited recorded instruments. Fixed v7 instrument calibration keeps
subtractive arranging meaningful; no section/stem loudness leveling is applied.
The whole clip is a single musical loop, including its eight-second introduction.
"""

import hashlib
import json
from collections import defaultdict
from pathlib import Path

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy import signal
from scipy.interpolate import CubicSpline

import render_yicun_shanhe_guzheng_v7 as v7

ROOT = Path(__file__).resolve().parents[1]
LOG = ROOT / "Logs/yicun-shanhe-shape-v8"
SR = v7.SR
DURATION = 104
SEED = 202611048
SECTIONS = [
    (0, 8, "introduction", "fragment", "guzheng", "C open fifth"),
    (8, 24, "theme_and_answer", "repeat", "guzheng then dizi", "C"),
    (24, 40, "variation", "vary", "dizi", "Gsus2 then Am"),
    (40, 52, "breathing_space", "fragment", "guzheng then low dizi", "open pentatonic"),
    (52, 60, "reentry", "recap", "dizi", "C"),
    (60, 68, "gentle_crest", "vary", "dizi with sparse guzheng responses", "Gsus2 to C"),
    (68, 76, "settling", "contract", "dizi descending", "C"),
    (76, 92, "return", "recap", "guzheng then broadened dizi answer", "C"),
    (92, 104, "link_to_introduction", "contract", "guzheng", "C open fifth"),
]
# A periodic, slowly changing phrase-level contour, in addition to played dynamics.
GAIN_TIMES = [0, 4, 8, 16, 24, 32, 38, 40, 46, 50, 52, 60, 64, 68, 74, 76, 84, 92, 98, 102, 104]
GAIN_DB = [-2.5, -1.8, -.8, -.5, 0, .2, -.5, -1, -.8, -.8, -.5, -.1, .1, -.3, -.5, -.6, -.6, -1.5, -2, -2.5, -2.5]
BALANCE_DB = {"guzheng": 2, "dizi": 0, "viola": -1, "bell": 0}


def build_score():
    score = {name: [] for name in ("guzheng", "dizi", "viola", "bell")}
    phrases = []

    def add(name, onset, pitch, duration, velocity, role="melody"):
        score[name].append((onset, pitch, duration, velocity, role))

    def phrase(name, start, pitches, offsets, lengths, dynamics, label):
        phrases.append({"label": label, "instrument": name, "start": start,
                        "pitches": pitches, "offsets": offsets, "dynamics": dynamics})
        for i, (pitch, offset, length, velocity) in enumerate(zip(pitches, offsets, lengths, dynamics)):
            # Small deterministic timing inflection, not a randomly scattered beat grid.
            timing = .014*np.sin(start*.37+i*1.4)
            add(name, start+offset+timing, pitch, length, velocity)

    # Prelude hints at 1 / 5 / 3 and a quiet pickup, rather than playing the full theme.
    for event in [(.95, 60, 1.65, .38), (3.45, 67, 1.75, .44),
                  (5.95, 64, 1.10, .37), (7.20, 62, .55, .33)]:
        add("guzheng", *event, "introduction")

    standard = [.25, 1.5, 2.25, 4.5, 6, 7]
    lengths = [1.15, .65, 2.05, 1.30, .8, 1.05]
    phrase("guzheng", 8, [60, 62, 67, 64, 62], standard[:5], lengths[:5],
           [.65, .60, .77, .63, .51], "theme: open question")
    phrase("dizi", 16, [72, 74, 79, 76, 74, 72], standard, lengths,
           [.57, .63, .76, .67, .53, .49], "theme: answered in the upper octave")

    phrase("dizi", 24, [67, 69, 74, 72, 69, 67],
           [.25, 1.25, 2.75, 4.25, 6, 7], [.82, 1.12, 1.2, 1.35, .62, .65],
           [.61, .65, .77, .69, .56, .48], "sequence: displaced rhythmic emphasis")
    phrase("dizi", 32, [64, 67, 72, 69, 67, 64],
           [.30, 2, 3, 5, 6.25, 7.25], [1.4, .76, 1.65, .95, .7, .75],
           [.50, .58, .68, .59, .47, .39], "variation: broader ascent and softened ending")

    phrase("guzheng", 40, [60, 62, 67, 64], [.8, 2.75, 4, 6.65],
           [1.45, .85, 1.7, 1.1], [.43, .38, .51, .37], "fragment: spacious remembrance")
    phrase("dizi", 48, [67, 64], [0, 2], [1.55, 1.40], [.38, .32],
           "fragment: lower-register breath")

    phrase("dizi", 52, [72, 74, 79, 76, 74, 72],
           [.25, 1.5, 2.25, 4.35, 5.8, 7], [1.05, .6, 1.75, 1.1, .7, .85],
           [.64, .67, .83, .73, .62, .58], "recap: gradual return")
    phrase("dizi", 60, [67, 69, 74, 72, 69, 67],
           [.2, .95, 2.2, 4.25, 5.85, 7], [.55, .98, 1.8, 1.30, .85, .70],
           [.69, .76, .87, .79, .67, .59], "variation: brief gentle crest")
    phrase("dizi", 68, [76, 74, 72, 67, 64], [.4, 1.75, 2.6, 4.4, 5.65],
           [.85, .65, 1.45, .9, 1.6], [.74, .64, .61, .52, .43],
           "contraction: descending answer")

    phrase("guzheng", 76, [60, 62, 67, 64, 62, 60], standard,
           [1.15, .65, 1.90, 1.2, .7, 1.1], [.68, .60, .76, .62, .49, .45],
           "recap: complete homecoming")
    phrase("dizi", 84, [76, 74, 72], [.5, 3, 5], [1.65, 1.0, 1.70],
           [.51, .43, .36], "variation: broadened three-note answer")

    for event in [(92.6, 60, 1.6, .46), (95.25, 67, 1.3, .40),
                  (97.65, 64, 1.15, .35), (101.6, 60, 1.35, .30)]:
        add("guzheng", *event, "link")

    # Responses and accompaniment are individually placed, not a repeating ostinato.
    for event in [(18.1, 55, .9, .26), (21.0, 64, 1.0, .25), (23.35, 72, .8, .28),
                  (24.05, 55, .9, .31), (27.1, 62, 1.05, .26), (31.85, 67, .75, .28),
                  (34.8, 64, 1.0, .25), (38.8, 60, .85, .23),
                  (53.8, 55, .9, .28), (56.55, 64, .95, .30), (59.35, 72, .65, .31),
                  (60.05, 55, .95, .33), (62.1, 62, 1.1, .33), (64.4, 60, .9, .35),
                  (66.4, 64, .8, .29), (68.1, 64, 1.1, .30), (71.85, 72, .7, .25),
                  (85.8, 60, 1.0, .23), (88.3, 67, .85, .22), (91.35, 64, .9, .20)]:
        add("guzheng", *event, "response")

    # Open dyads, deliberately incomplete when a full chord would crowd the lead.
    for onset, pitches, duration, velocity in [
        (4.8, [55], 2.2, .22),
        (10.8, [60], 1.2, .30), (10.85, [55], 3.4, .30),
        (18.7, [60, 64], 3.2, .36),
        (24.0, [55], 5.3, .36), (24.075, [62], 2.8, .33),
        (33.2, [57, 64], 4.0, .32),
        # No strings at all during 40-52 seconds.
        (52.3, [55, 60], 3.4, .35),
        (60.0, [55], 6.5, .41), (60.075, [62], 3.0, .38),
        (68.2, [55, 60], 2.8, .35), (73.2, [55, 60], 1.8, .28),
        (78.8, [60, 64], 2.0, .30), (82.4, [55], 3.5, .27),
        (88.6, [60], 3.1, .23), (92.4, [55, 60], 4.1, .20),
        # A very quiet held G bridges the seam without carrying a foreground pluck in.
        (101.8, [55], 2.8, .13),
    ]:
        for i, pitch in enumerate(pitches):
            add("viola", onset+i*.075, pitch, duration, velocity, "background")
    add("bell", 59.15, 64, 3.2, .40, "bronze")

    score = {name: sorted(events) for name, events in score.items()}
    # Dizi is a single player. Its source renderer includes a 140ms release.
    wind = []
    for i, event in enumerate(score["dizi"]):
        onset, pitch, duration, velocity, role = event
        if i+1 < len(score["dizi"]):
            duration = min(duration, score["dizi"][i+1][0]-onset-.18)
        assert duration > .3
        wind.append((onset, pitch, duration, velocity, role))
    score["dizi"] = wind
    return score, phrases


def render_raw(score, duration, banks, seed, damp_all=True):
    guzheng, viola, flute = banks
    rng = np.random.default_rng(seed)
    counters = defaultdict(int)
    stems, details = {}, []
    length = duration*SR
    for instrument, events in score.items():
        stem = np.zeros((length, 2))
        for onset, pitch, value, velocity, role in events:
            extra = {}
            if instrument == "guzheng":
                stereo, extra = v7.guzheng_note(guzheng, pitch, value, counters, damp_all or role == "melody")
                note = stereo*np.array([1.04, .96])
            elif instrument == "dizi":
                mono = v7.sampled_dizi(flute, pitch, value, rng)
                note = mono[:, None]*np.array([.66, .75])
            elif instrument == "viola":
                note = v7.viola_pad(viola, pitch, value)
            else:
                mono = v7.bronze_note(pitch, value, 1)
                note = mono[:, None]*np.array([.7, .7])
            start = round(onset*SR)
            count = min(len(note), length-start)
            assert start >= 0 and count > 0
            stem[start:start+count] += note[:count]*velocity
            if count < len(note):
                stem[:len(note)-count] += note[count:]*velocity
            details.append({**extra, "instrument": instrument, "onset": onset, "pitch": pitch,
                            "duration_seconds": len(note)/SR, "velocity": velocity, "role": role})
        stems[instrument] = stem
    return stems, details, rng


def render():
    LOG.mkdir(parents=True, exist_ok=True)
    guzheng, mapping = v7.load_guzheng()
    banks = (guzheng, v7.load_stereo_bank("viola"), v7.load_dizi())
    # Stable historical reference, reconstructed without depending on a Logs WAV.
    reference, _, _ = render_raw(v7.build_guzheng_score(), 96, banks, v7.SEED, damp_all=False)
    calibration = {name: float(np.sqrt(v7.SHARES[name]/np.mean(stem**2)))
                   for name, stem in reference.items()}
    del reference
    score, phrases = build_score()
    stems, details, rng = render_raw(score, DURATION, banks, SEED)
    time = np.arange(DURATION*SR)/SR
    envelope_db = CubicSpline(GAIN_TIMES, GAIN_DB, bc_type="periodic")(time)
    automation = 10**(envelope_db/20)
    for name, stem in stems.items():
        stem *= calibration[name]*10**(BALANCE_DB[name]/20)*automation[:, None]
    mix = v7.circular_room(sum(stems.values()), rng)
    freqs = np.fft.rfftfreq(len(mix), 1/SR)
    highpass = freqs**2/(freqs**2+70**2)
    for channel in range(2):
        mix[:, channel] = np.fft.irfft(np.fft.rfft(mix[:, channel])*highpass, n=len(mix))
    meter = pyln.Meter(SR)
    # A single final gain preserves every section and phrase dynamic relationship.
    gain = 10**((-29-meter.integrated_loudness(mix))/20)
    mix *= gain
    assert np.isfinite(mix).all() and np.max(np.abs(mix)) < .5
    output = LOG / "yicun_shanhe_shape_v8.wav"
    sf.write(output, mix, SR, subtype="PCM_16")
    for name, stem in stems.items():
        sf.write(LOG/f"dry_{name}.wav", stem*gain, SR, subtype="PCM_24")
    pcm, _ = sf.read(output)
    sections = []
    for start, end, name, development, lead, harmony in SECTIONS:
        segment = pcm[start*SR:end*SR]
        sections.append({"start": start, "end": end, "name": name,
                         "development": development, "lead": lead, "harmony": harmony,
                         "rms_dbfs": float(20*np.log10(np.sqrt(np.mean(segment**2)))),
                         "lufs": float(meter.integrated_loudness(segment)),
                         "events": {inst: int(sum(start <= e[0] < end for e in events))
                                    for inst, events in score.items()}})
    report = {"version": 8, "seed": SEED, "duration_seconds": DURATION, "sample_rate": SR,
              "meter": "4/4", "bpm": 60, "bars": 26, "intro_seconds": 8,
              "intro_repeats_with_whole_clip": True,
              "lufs": float(meter.integrated_loudness(pcm)),
              "sample_peak_dbfs": float(20*np.log10(np.max(np.abs(pcm)))),
              "rms_dbfs": float(20*np.log10(np.sqrt(np.mean(pcm**2)))),
              "boundary_jump": np.abs(pcm[0]-pcm[-1]).tolist(),
              "events": {name: len(events) for name, events in score.items()},
              "score": score, "phrases": phrases, "sections": sections,
              "all_rendered_notes": details, "guzheng_mapping": mapping,
              "reference_instrument_gains": calibration,
              "instrument_balance_db": BALANCE_DB,
              "gain_contour": {"seconds": GAIN_TIMES, "db": GAIN_DB, "interpolation": "periodic cubic spline"},
              "post_automation_dry_rms_dbfs": {name: float(20*np.log10(np.sqrt(np.mean((stem*gain)**2))))
                                                 for name, stem in stems.items()},
              "wav_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
              "limitations": "Score, spectra and waveform validation; no subjective listening performed."}
    (LOG/"render.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({key: report[key] for key in ["duration_seconds", "lufs", "sample_peak_dbfs", "events", "sections", "post_automation_dry_rms_dbfs"]}, indent=2))


if __name__ == "__main__":
    render()
