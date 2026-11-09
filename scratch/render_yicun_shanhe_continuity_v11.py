"""Recorded guzheng/dizi/viola; editable score and Pedalboard periodic room.

Run in the isolated music-production environment. Writes only versioned candidates
under Logs; publication to Unity is a separate, verified step. No audio models,
GM substitute instruments, synthetic bell, per-stem loudness normalizing or limiter.
"""
from pathlib import Path
import hashlib
import json
import sys
import importlib.metadata

import numpy as np
import soundfile as sf
import pyloudnorm as pyln
import mido
from scipy.interpolate import CubicSpline
from pedalboard import Pedalboard, Reverb, HighpassFilter, LowpassFilter

import render_yicun_shanhe_guzheng_v7 as v7
from yicun_dizi_phrases_v11 import render_raw, build_score, CHANGES

ROOT = Path(__file__).resolve().parents[1]
LOG = ROOT / "Logs/yicun-shanhe-continuity-v11-rebuild-20261108"
SR = v7.SR
DURATION = 112
SEED = 2026110510  # Keep v10 sample selections; only the authorized transitions change.
# Fixed source calibration used in v8; never recalculated from sparse new stems.
CALIBRATION = {"guzheng": 1.4618132131851829, "dizi": 2.5111988816688595,
               "viola": 0.3838312735969084}
BALANCE_DB = {"guzheng": 1.4, "dizi": 0.0, "viola": -2.0}
ROOM_SEND = {"guzheng": .13, "dizi": .17, "viola": .20}
ROOM = {"room_size": .36, "damping": .64, "wet_level": 1., "dry_level": 0.,
        "width": .72, "freeze_mode": 0.}
GAIN_TIMES = [0, 4, 8, 16, 24, 32, 40, 48, 54, 56, 64, 68, 72, 80, 88, 96, 104, 108, 112]
GAIN_DB = [-1.9, -1.5, -.7, -.35, -.1, -.15, -1.2, -1.3, -.8, -.45, .2, .4,
           -.2, -.55, -.65, -1.1, -1.6, -1.9, -1.9]
SECTIONS = [
    (0, 8, "prelude", "fragment", "guzheng", "C/G open"),
    (8, 24, "theme_statement_twice", "repeat", "dizi", "C"),
    (24, 40, "outward_variation", "sequence/vary", "dizi", "G open then Am"),
    (40, 56, "open_space", "fragment/extend", "guzheng then dizi", "open pentatonic; no strings"),
    (56, 64, "returning_motion", "recap", "dizi", "C"),
    (64, 72, "brief_bloom", "vary", "dizi with guzheng answers", "C to G open"),
    (72, 80, "settle", "contract", "dizi", "C"),
    (80, 96, "homecoming", "recap/vary", "guzheng then dizi", "C"),
    (96, 104, "afterglow", "vary/fragment", "dizi then guzheng", "C/G open"),
    (104, 112, "turn_to_prelude", "fragment", "guzheng", "C/G open"),
]


def export_midi(score):
    """Score interchange, not a claim that GM programs reproduce these samples."""
    mid = mido.MidiFile(type=1, ticks_per_beat=960)
    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.extend([mido.MetaMessage("track_name", name="Yicun Shanhe v11: source-specific instruments"),
                 mido.MetaMessage("set_tempo", tempo=1000000),
                 mido.MetaMessage("time_signature", numerator=4, denominator=4),
                 mido.MetaMessage("marker", text="LOOP_START", time=0),
                 mido.MetaMessage("marker", text="LOOP_END_112_SECONDS", time=DURATION*960)])
    for channel, (name, events) in enumerate(score.items()):
        track = mido.MidiTrack(); mid.tracks.append(track)
        track.append(mido.MetaMessage("track_name", name=name))
        timed = []
        for onset, pitch, duration, velocity, _ in events:
            timed += [(round(onset*960), 1, mido.Message("note_on", channel=channel, note=pitch,
                         velocity=max(1, min(127, round(velocity*127))))),
                      (round((onset+duration)*960), 0, mido.Message("note_off", channel=channel,
                         note=pitch, velocity=0))]
        previous = 0
        for tick, _, message in sorted(timed, key=lambda x: (x[0], x[1])):
            track.append(message.copy(time=tick-previous)); previous = tick
    path = LOG / "yicun_shanhe_v11_editable.mid"
    mid.save(path)
    check = mido.MidiFile(path)
    assert sum(msg.type == "note_on" for track in check.tracks for msg in track) == sum(map(len, score.values()))
    return path


def periodic_room(send):
    board = Pedalboard([HighpassFilter(cutoff_frequency_hz=105), Reverb(**ROOM),
                        LowpassFilter(cutoff_frequency_hz=6200)])
    current = None
    convergence = []
    # Real state carried through successive loops. Stop based on observed convergence.
    for cycle in range(1, 5):
        output = board(np.ascontiguousarray(send.T, dtype=np.float32), SR,
                       buffer_size=8192, reset=(cycle == 1)).T.copy()
        assert output.shape == send.shape and np.isfinite(output).all()
        if current is not None:
            difference = output-current
            peak = float(np.max(np.abs(difference)))
            rms = float(np.sqrt(np.mean(difference.astype(np.float64)**2)))
            convergence.append({"cycle": cycle, "difference_peak": peak, "difference_rms": rms})
            if peak < 2e-7:
                return output, convergence
        current = output
    raise RuntimeError("Room state did not converge; do not publish an assumed loop: " + str(convergence))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def render():
    LOG.mkdir(parents=True, exist_ok=True)
    score, phrases = build_score()
    guzheng, mapping = v7.load_guzheng()
    banks = (guzheng, v7.load_stereo_bank("viola"), v7.load_dizi())
    stems, details, _ = render_raw(score, DURATION, banks, SEED, damp_all=True, phrases=phrases)
    del guzheng, banks
    t = np.arange(DURATION*SR)/SR
    contour = 10**(CubicSpline(GAIN_TIMES, GAIN_DB, bc_type="periodic")(t)/20)
    for name in stems:
        stems[name] = np.asarray(stems[name] * (CALIBRATION[name] * 10**(BALANCE_DB[name]/20))
                                 * contour[:, None], dtype=np.float32)
    dry = sum(stems.values())
    send = sum(stems[k]*ROOM_SEND[k] for k in stems)
    room, convergence = periodic_room(send)
    mix = dry + room
    meter = pyln.Meter(SR)
    global_gain = 10**((-29-meter.integrated_loudness(mix))/20)
    mix *= global_gain
    assert np.isfinite(mix).all() and np.max(np.abs(mix)) < .5
    output = LOG / "yicun_shanhe_continuity_v11.wav"
    sf.write(output, mix, SR, subtype="PCM_16")
    for name in stems:
        sf.write(LOG/f"dry_{name}.wav", stems[name]*global_gain, SR, subtype="PCM_24")
    sf.write(LOG/"shared_room_return.wav", room*global_gain, SR, subtype="PCM_24")
    pcm, _ = sf.read(output)
    sf.write(LOG/"preview_first_32s.wav", pcm[:32*SR], SR, subtype="PCM_16")
    sf.write(LOG/"preview_theme_8-24s.wav", pcm[8*SR:24*SR], SR, subtype="PCM_16")
    sf.write(LOG/"preview_dizi_dry_8-24s.wav", stems["dizi"][8*SR:24*SR]*global_gain, SR, subtype="PCM_24")
    seam = np.concatenate([pcm[-8*SR:], pcm[:12*SR]])
    sf.write(LOG/"preview_loop_seam_20s.wav", seam, SR, subtype="PCM_16")
    sf.write(LOG/"preview_middle_connection_38-58s.wav", pcm[38*SR:58*SR], SR, subtype="PCM_16")
    sf.write(LOG/"preview_return_connection_76-90s.wav", pcm[76*SR:90*SR], SR, subtype="PCM_16")
    midi = export_midi(score)
    sections = []
    for start, end, name, operation, lead, harmony in SECTIONS:
        segment = pcm[start*SR:end*SR]
        sections.append({"name": name, "start": start, "end": end, "development": operation,
                         "lead": lead, "harmony": harmony,
                         "lufs": float(meter.integrated_loudness(segment)),
                         "rms_dbfs": float(20*np.log10(np.sqrt(np.mean(segment**2)))),
                         "events": {k: sum(start <= e[0] < end for e in v) for k, v in score.items()}})
    sources = [ROOT/"ArtSource/Audio/YicunShanhe/Guzheng/Pufermufin_396868_original_hq.mp3"]
    sources += list((ROOT/"ArtSource/Audio/YicunShanhe").glob("dizi_*_Hypnotriod.mp3"))
    sources += list((ROOT/"ArtSource/Audio/YicunShanhe/VSCO").glob("ViolaEns_susvib_*_v1_1.wav"))
    sources += [ROOT/"ArtSource/Audio/YicunShanhe/Guzheng"/name
                for name in ["instrument-source.json", "selected-samples.json"]]
    modules = sorted({Path(m.__file__).resolve() for m in list(sys.modules.values())
                      if getattr(m, "__file__", None) and Path(m.__file__).resolve().parent == ROOT/"scratch"})
    report = {"version": 11, "client_date": "2026-11-08", "rebuild_of": "v11 2026-11-05", "seed": SEED, "bpm": 60,
              "meter": "4/4", "bars": 28, "duration_seconds": DURATION, "sample_rate": SR,
              "intro_seconds": 8, "intro_repeats_with_whole_clip": True,
              "lufs": float(meter.integrated_loudness(pcm)),
              "sample_peak_dbfs": float(20*np.log10(np.max(np.abs(pcm)))),
              "boundary_jump": np.abs(pcm[0]-pcm[-1]).tolist(), "global_gain": global_gain,
              "score": score, "phrases": phrases, "sections": sections,
              "arrangement_changes": CHANGES,
              "baseline_v10_wav_sha256": "2546777c0b1b6bdf5feff187e46e5d99d4350362f32f8adc3cc79af6d3e15384",
              "events": {k:len(v) for k,v in score.items()}, "all_rendered_notes": details,
              "wind_performance": "Recorded sustained samples joined by 24ms equal-power crossfades, continuous phrase expression, one attack/release per phrase; not recorded legato transitions.",
              "guzheng_mapping": mapping, "fixed_source_calibration": CALIBRATION,
              "balance_db": BALANCE_DB, "automation_seconds": GAIN_TIMES, "automation_db": GAIN_DB,
              "pedalboard_room": {"parameters": ROOM, "sends": ROOM_SEND, "highpass_hz":105,
                                  "lowpass_hz":6200, "convergence":convergence},
              "dry_stem_rms_dbfs": {k:float(20*np.log10(np.sqrt(np.mean((v*global_gain)**2)))) for k,v in stems.items()},
              "room_vs_dry_rms_db":float(10*np.log10(np.mean(room**2)/np.mean(dry**2))),
              "versions": {name:importlib.metadata.version(name) for name in
                           ["pedalboard", "numpy", "soundfile", "scipy", "pyloudnorm", "mido"]},
              "sources": [{"path":str(p.relative_to(ROOT)), "sha256":sha(p)} for p in sources],
              "code": [{"path":str(p.relative_to(ROOT)), "sha256":sha(p)} for p in modules],
              "output":str(output), "wav_sha256":sha(output), "midi_sha256":sha(midi),
              "midi_note": "Editable symbolic export without GM program substitutions; loop end marker at112s; held notes/releases may continue past marker. JSON/source is authoritative for sample mapping and rendered tails.",
              "limitations": "Recorded sample arrangement and signal verification; no actual subjective listening or target headset acceptance yet."}
    (LOG/"render.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    (LOG/"sections.json").write_text(json.dumps({"sections": [{"name":s["name"], "start":s["start"], "end":s["end"]}
                                                     for s in sections]}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k:report[k] for k in ["duration_seconds", "lufs", "sample_peak_dbfs", "boundary_jump", "events", "dry_stem_rms_dbfs", "room_vs_dry_rms_db", "sections"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    render()
