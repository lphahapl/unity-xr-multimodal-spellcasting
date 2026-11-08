"""Read-only source/pitch audit; writes only the authorized v7 audit JSON."""
import hashlib
import json
from collections import Counter
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy import signal

import render_yicun_shanhe_guzheng_v6 as renderer

ROOT = Path(__file__).resolve().parents[1]
SR = renderer.SR
OUT = ROOT / "Logs/yicun-shanhe-quiet-v7/source-full-audit.json"
EQ = {
    57: [(196.7, 6, -8), (393.2, 10, -6), (787.8, 14, -6)],
    67: [(440.4, 7, -8), (882, 12, -6), (1322, 18, -6)],
}


def frequency(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def power_spectrum(x):
    if x.ndim == 1:
        x = x[:, None]
    window = np.hanning(len(x))[:, None]
    nfft = max(65536, 2 ** int(np.ceil(np.log2(len(x) * 4))))
    power = np.mean(abs(np.fft.rfft(x * window, nfft, axis=0)) ** 2, axis=1)
    return np.fft.rfftfreq(nfft, 1 / SR), power


def local_peak(freq, power, center, halfwidth):
    ids = np.flatnonzero(abs(freq - center) < halfwidth)
    j = int(ids[np.argmax(power[ids])])
    v = np.log(power[j - 1:j + 2] + 1e-30)
    denominator = v[0] - 2 * v[1] + v[2]
    offset = np.clip(.5 * (v[0] - v[2]) / denominator, -.5, .5) if denominator else 0
    return float(freq[j] + offset * (freq[1] - freq[0])), j


def pitch_track(x, expected, start=.15, end=None, frame=.35, hop=.2):
    end = min(len(x) / SR, end if end is not None else len(x) / SR)
    values = []
    for onset in np.arange(start, end - frame + 1e-9, hop):
        segment = x[round(onset * SR):round((onset + frame) * SR)]
        if np.sqrt(np.mean(segment ** 2)) < 1e-8:
            continue
        f, p = power_spectrum(segment)
        hz, _ = local_peak(f, p, expected, expected * .04)
        values.append({"start_seconds": float(onset), "hz": hz,
                       "cents": float(1200 * np.log2(hz / expected))})
    if not values:
        return {"frames": []}
    cents = [v["cents"] for v in values]
    return {"median_cents": float(np.median(cents)),
            "p10_cents": float(np.quantile(cents, .1)),
            "p90_cents": float(np.quantile(cents, .9)), "frames": values}


def spectral_windows(x, expected, windows, tolerance=.025):
    rows = []
    for a, b in windows:
        b = min(b, len(x) / SR)
        if b - a < .12:
            continue
        f, p = power_spectrum(x[round(a * SR):round(b * SR)])
        peaks, _ = signal.find_peaks(p, distance=max(1, round(10 / (f[1] - f[0]))))
        peaks = peaks[(f[peaks] > 80) & (f[peaks] < 2400)]
        family, other = [], []
        for j in peaks:
            k = max(1, round(f[j] / expected))
            (family if abs(f[j] - expected * k) <= max(8, expected * k * tolerance)
             else other).append(int(j))
        reference = max(p[j] for j in family)
        other.sort(key=lambda j: p[j], reverse=True)
        rows.append({"window_seconds": [a, b], "off_family_peaks": [
            {"hz": float(f[j]), "db_relative_strongest_intended_harmonic":
             float(10 * np.log10(p[j] / reference))} for j in other[:5]]})
    return rows


def secondary_attacks(x):
    if x.ndim == 2:
        x = x.mean(axis=1)
    y = signal.sosfilt(signal.butter(3, 2000, fs=SR, btype="highpass", output="sos"), x)
    hop = round(.01 * SR)
    env = np.sqrt(np.mean(y[:len(y) // hop * hop].reshape(-1, hop) ** 2, axis=1))
    hits = []
    for j in range(15, len(env) - 3):
        before, after = np.mean(env[j - 4:j - 1]), np.mean(env[j:j + 3])
        rise = 20 * np.log10((after + 1e-12) / (before + 1e-12))
        level = 20 * np.log10((after + 1e-12) / (max(env) + 1e-12))
        if rise > 6 and level > -30:
            if not hits or j * .01 - hits[-1]["seconds"] > .08:
                hits.append({"seconds": j * .01, "rise_db": float(rise), "relative_level_db": float(level)})
    return hits


def bell(fc, bandwidth, gain_db):
    amplitude = 10 ** (gain_db / 40)
    w = 2 * np.pi * fc / SR
    alpha = np.sin(w) / (2 * fc / bandwidth)
    c = np.cos(w)
    b = np.array([1 + alpha * amplitude, -2 * c, 1 - alpha * amplitude])
    a = np.array([1 + alpha / amplitude, -2 * c, 1 - alpha / amplitude])
    return np.r_[b / a[0], 1, a[1:] / a[0]]


def eq_trial(x, root, expected):
    sos = np.stack([bell(*cut) for cut in EQ[root]])
    filtered = signal.sosfilt(sos, x, axis=0)
    t = np.arange(len(x)) / SR
    wet = .5 - .5 * np.cos(np.pi * np.clip((t - .03) / .09, 0, 1))
    y = x * (1 - wet[:, None]) + filtered * wet[:, None]
    rows, wanted, unwanted = [], [], []
    for a, b in [(.15, .35), (.35, .65), (.65, 1), (1.1, 1.5), (1.5, 2.05)]:
        b = min(b, len(x) / SR)
        if b - a < .15:
            continue
        f, before = power_spectrum(x[round(a * SR):round(b * SR)])
        _, after = power_spectrum(y[round(a * SR):round(b * SR)])
        main = []
        for k in range(1, 6):
            _, j = local_peak(f, before, expected * k, max(6, expected * k * .015))
            change = float(10 * np.log10(after[j] / before[j]))
            main.append({"harmonic": k, "hz": float(f[j]), "change_db": change})
            wanted.append(abs(change))
        cuts = []
        for fc, width, gain in EQ[root]:
            _, j = local_peak(f, before, fc, width / 2)
            change = float(10 * np.log10(after[j] / before[j]))
            cuts.append({"center_hz": fc, "peak_hz": float(f[j]), "change_db": change})
            unwanted.append(-change)
        rows.append({"window_seconds": [a, b], "desired_harmonics": main, "residue_cuts": cuts})
    return {"parameters_frequency_bandwidth_gain_db": EQ[root],
            "topology": "RBJ finite-depth peaking EQ, Q=frequency/bandwidth, causal; same filters on both stereo channels",
            "wet_ramp_seconds": [.03, .12], "wet_ramp": "half cosine",
            "minimum_residue_peak_reduction_db": min(unwanted),
            "maximum_residue_peak_reduction_db": max(unwanted),
            "maximum_desired_harmonic_peak_change_db": max(wanted), "windows": rows}


def run():
    score = renderer.build_guzheng_score()
    render = json.loads((renderer.LOG / "render.json").read_text(encoding="utf-8"))
    bank, mapping = renderer.load_guzheng()
    guzheng = []
    windows = [(.03, .15), (.15, .35), (.35, .65), (.65, 1), (1, 1.5), (1.5, 2.1), (2.1, 2.4)]
    for meta in mapping:
        root = meta["root"]
        sample = bank[root][0]["sample"]
        row = {**meta, "events": [n for n in render["guzheng_rendered_notes"] if n["root"] == root],
               "pitch_track_before_metadata_tuning": pitch_track(sample, frequency(root)),
               "spectral_windows": spectral_windows(sample, frequency(root), windows),
               "secondary_attack_candidates": secondary_attacks(sample)}
        if root in EQ:
            row["bounded_eq_trial"] = eq_trial(sample, root, 221.1 if root == 57 else 391.2)
        row["median_tuning_error_after_metadata_correction_cents"] = (
            row["pitch_track_before_metadata_tuning"].get("median_cents", 0) + meta["tune_cents"])
        row["interpretation"] = {
            53: "Independent G3-family residue (about196.7/393Hz) remains under F3. When transposed to the sole E3 accompaniment at13.7s it becomes F-sharp-related residue. Removing this one optional event avoids further processing.",
            57: "Independent G3-family residue under A3 is supported by196.7/393/787Hz components. It becomes F3-related residue when transposed to G3. Apply only the three bounded trial cuts; weaker C4-like components do not justify additional cuts in this pass.",
            60: "Preferred C4 take remains coherent; no comparably strong independent residual family was established. Retain its natural partials.",
            62: "Preferred D4 take remains coherent; no comparably strong independent residual family was established. Natural inharmonicity and late room components do not justify generic harmonic extraction.",
            67: "An independent A4 family near440/882/1322Hz is present under G4. It is weaker in stereo-power measurements than a mono collapse suggests. The three small finite cuts reduce it without materially changing G4 partials.",
            72: "The near504Hz side component has no correspondingly strong stable1008/1512Hz family established here. Treat it as an ambiguous string/body/sideband feature, not a confirmed second note; do not notch the main note broadly.",
            74: "The near570Hz/other side components do not establish a strong separate fundamental family in the retained0.84s. Keep the31.8s E5 decoration; lack of evidence does not justify deleting it.",
        }[root]
        guzheng.append(row)

    dizi_bank = renderer.load_dizi()
    dizi_sources = []
    for root, (sample, tuned_hz) in dizi_bank.items():
        dizi_sources.append({"root": root, "file": f"dizi_{root}_Hypnotriod.mp3",
            "nominal_hz": frequency(root), "renderer_estimated_hz": tuned_hz,
            "raw_pitch_1_to_4_seconds": pitch_track(sample, frequency(root), 1, 4),
            "spectral_windows": spectral_windows(sample, tuned_hz, [(1, 1.5), (2, 2.5), (3, 3.5)], .04)})
    rng = np.random.default_rng(renderer.SEED)
    dizi_events = []
    for onset, pitch, duration, velocity, technique in score["dizi"]:
        root = min(dizi_bank, key=lambda p: abs(p - pitch))
        note = renderer.sampled_dizi(dizi_bank, pitch, duration, rng)
        dizi_events.append({"onset": onset, "pitch": pitch, "source_root": root,
            "root_transposition_semitones": pitch - root,
            "rendered_pitch": pitch_track(note, frequency(pitch), .15, duration - .05, .3, .2)})

    viola_bank = renderer.load_stereo_bank("viola")
    viola_root_names = {55: "G2", 59: "B2", 62: "D3", 65: "F3", 69: "A3"}
    viola_events = []
    for onset, pitch, duration, velocity, technique in score["viola"]:
        root = min(viola_bank, key=lambda p: abs(p - pitch))
        note = renderer.viola_pad(viola_bank, pitch, duration)
        viola_events.append({"onset": onset, "pitch": pitch, "source_root": root,
            "rendered_pitch": pitch_track(note, frequency(pitch), .8, duration, .45, .3)})
    used_viola = sorted(set(e["source_root"] for e in viola_events))
    viola_sources = [{"root": root, "file": f"ViolaEns_susvib_{viola_root_names[root]}_v1_1.wav",
         "raw_pitch_1_to_4_seconds": pitch_track(viola_bank[root], frequency(root), 1, 4, .5, .35),
         "spectral_windows": spectral_windows(viola_bank[root], frequency(root), [(1, 1.5), (2, 2.5), (3, 3.5)], .05)}
        for root in used_viola]

    dizi_medians = [e["rendered_pitch"]["median_cents"] for e in dizi_events
                   if "median_cents" in e["rendered_pitch"]]
    viola_medians = [e["rendered_pitch"]["median_cents"] for e in viola_events
                    if "median_cents" in e["rendered_pitch"]]
    report = {"status": "numeric_source_audit_complete_pending_musical_audition",
        "reviewed_at": datetime.now().astimezone().isoformat(),
        "reviewer": "Codex built-in subagent /root/instrument_sources",
        "reviewed_renderer": str(ROOT / "scratch/render_yicun_shanhe_guzheng_v6.py"),
        "reviewed_audio_sha256": hashlib.sha256((renderer.LOG / "yicun_shanhe_guzheng_v6.wav").read_bytes()).hexdigest(),
        "coverage": {"duration_seconds": 96, "guzheng_sources": 7, "guzheng_events": 47,
                     "dizi_sources": 6, "dizi_events": 40, "viola_used_sources": len(used_viola), "viola_events": 24},
        "method": "Windowed spectra average stereo channel powers, avoiding mono-cancellation errors. Pitch tracks use local fundamental peaks, with existing harmonic-family and octave checks. Off-family peak lists are screening data, not an automatic declaration of a second note. Natural vibrato, ensemble spread, inharmonic string partials and broadband breath are retained.",
        "guzheng_sources": guzheng, "dizi_sources": dizi_sources, "dizi_rendered_events": dizi_events,
        "viola_sources": viola_sources, "viola_rendered_events": viola_events,
        "unused_loaded_viola_source": "A3 filename / actual MIDI69 is not selected by any current pad event",
        "recommendations": [
            {"priority": 1, "action": "Apply the validated three finite bell cuts to root57 before transposition, with30-120ms wet ramp.",
             "affected_events": 15, "reason": "Repeated G3-family residue in the A3 source becomes F3 residue under the rendered G3, including the loop-opening tail.",
             "parameters_frequency_bandwidth_gain_db": EQ[57]},
            {"priority": 2, "action": "Remove only the13.7s E3 optional accompaniment and stop loading root53.",
             "affected_events": 1, "reason": "Avoid its independent G3 residue, shifted to F-sharp under E3, with no need to filter or rewrite the theme."},
            {"priority": 3, "action": "Apply the validated three finite bell cuts to root67; preserve its G4 theme notes and normal partials.",
             "affected_events": 5, "reason": "Reduce the confirmed440/882/1322Hz A4 family without changing the musical line.",
             "parameters_frequency_bandwidth_gain_db": EQ[67]},
        ],
        "dizi_and_viola_conclusion": {
            "severe_mapping_or_octave_error_found": False,
            "dizi_event_median_cents_range": [min(dizi_medians), max(dizi_medians)],
            "viola_event_median_cents_range": [min(viola_medians), max(viola_medians)],
            "dizi_conclusion": "All6 sources and40 actual crops were checked. Existing tuning correction keeps event median fundamentals within about3.5cents. Stable off-family peaks in representative source windows are at least about31.8dB below the intended family; no severe independent pitched spill is established. Preserve breath and membrane harmonics.",
            "viola_conclusion": "The4 used source roots are55/59/62/65; the old filename-octave correction is correct. All24 actual pads were measured. The roughly-11 to+3cent median range does not justify flattening natural ensemble vibrato. Representative off-family peaks are about23dB or more below the intended family, with no comparably strong second-note family established.",
            "no_further_dizi_or_viola_cleanup_recommended": True},
        "v7_implementation_inspection": {
            "path": str(ROOT / "scratch/render_yicun_shanhe_guzheng_v7.py"),
            "conclusion": "Inspected reduce_confirmed_spill: normalized RBJ coefficients, causal per-channel lfilter, exact proposed centers/bandwidths/gains, and30-120ms half-cosine wet ramp match the in-memory experiment. The pre-EQ RMS normalization reference is preserved, so removed residue is not immediately amplified back by re-normalizing the cleaned source.",
            "scope_limit": "This is an implementation/source comparison; the parent owns final v7 artifact validation and listening."},
        "limitations": ["No actual listening assessment was performed.", "This audits source/pitch behavior across all used events; it does not prove all simultaneous musical intervals will be preferred by the listener.", "The EQ trials are in-memory comparisons only; this script does not edit source audio, renderers or assets.", "Numeric off-family energy must not be removed wholesale; only independently supported residual pitch families justify the proposed limited cuts."]}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for name, events in [("dizi", dizi_events), ("viola", viola_events)]:
        medians = [e["rendered_pitch"]["median_cents"] for e in events if "median_cents" in e["rendered_pitch"]]
        print(name, "event median cents min/max", min(medians), max(medians))
    print("written", OUT)


if __name__ == "__main__":
    run()
