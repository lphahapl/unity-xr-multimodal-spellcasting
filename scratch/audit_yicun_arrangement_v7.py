"""Read-only v6 score/timing audit and proposed v7 arrangement projection.

Does not render, listen to, or modify audio. Writes only its authorized JSON report.
Overlap is a numerical observation, not an automatic musical error classification.
"""
import copy
import hashlib
import itertools
import json
import math
from datetime import datetime, timezone, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Logs/yicun-shanhe-quiet-v6/render.json"
OUTPUT = ROOT / "Logs/yicun-shanhe-quiet-v7/arrangement-full-audit.json"
PITCH_NAMES = ("C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B")


def note_name(pitch):
    return PITCH_NAMES[pitch % 12] + str(pitch // 12 - 1)


def make_events(report):
    events = []
    for instrument, notes in report["score"].items():
        for index, (onset, pitch, duration, velocity, technique) in enumerate(notes):
            if instrument == "guzheng":
                actual = report["guzheng_rendered_notes"][index]["duration_seconds"]
            else:
                actual = duration + {"dizi": .14, "viola": .5, "bell": 0}[instrument]
            events.append({"id": f"{instrument}:{index}", "instrument": instrument,
                           "onset": onset, "midi": pitch, "note": note_name(pitch),
                           "nominal_duration": duration, "dry_duration": actual,
                           "dry_end": onset + actual, "velocity": velocity,
                           "technique": technique})
    return events


def overlap_record(first, second, start, end, shift=0):
    interval = abs(first["midi"] - second["midi"])
    return {"start": start, "end": end, "seconds": end - start,
            "first": first["id"], "first_note": first["note"],
            "second": second["id"], "second_note": second["note"],
            "semitones": interval, "pitch_class_interval": interval % 12,
            "literal_adjacent_semitones": interval in (1, 2),
            "second_copy_shift": shift}


def audit(events, period=96):
    pairs = []
    for first, second in itertools.combinations(events, 2):
        begin, end = max(first["onset"], second["onset"]), min(first["dry_end"], second["dry_end"])
        if end > begin:
            pairs.append(overlap_record(first, second, begin, end))
    wrapped = [e for e in events if e["dry_end"] > period]
    loop_pairs = []
    for first in wrapped:
        for second in events:
            begin = max(0, first["onset"] - period, second["onset"])
            end = min(first["dry_end"] - period, second["dry_end"])
            if end > begin:
                loop_pairs.append(overlap_record(first, second, begin, end, -period))
    flute = sorted((e for e in events if e["instrument"] == "dizi"), key=lambda e: e["onset"])
    flute_transitions = []
    for first, second in zip(flute, flute[1:]):
        gap = second["onset"] - first["dry_end"]
        flute_transitions.append({"first": first["id"], "first_note": first["note"],
                                  "second": second["id"], "second_note": second["note"],
                                  "next_onset": second["onset"], "dry_gap": gap,
                                  "nominal_gap": second["onset"] - first["onset"] - first["nominal_duration"]})
    counts = {i: sum(e["instrument"] == i for e in events) for i in ("guzheng", "dizi", "viola", "bell")}
    close = [p for p in pairs if p["literal_adjacent_semitones"]]
    return {"event_counts": counts,
            "pitch_classes": sorted(set(e["midi"] % 12 for e in events)),
            "lowest_pitch_by_instrument": {i: min(e["midi"] for e in events if e["instrument"] == i) for i in counts},
            "all_dry_overlaps": pairs,
            "literal_adjacent_intervals": close,
            "compound_second_or_seventh_colours": [p for p in pairs if p["pitch_class_interval"] in (1, 2, 10, 11) and not p["literal_adjacent_semitones"]],
            "semitone_or_tritone_overlaps": [p for p in pairs if p["pitch_class_interval"] in (1, 6, 11)],
            "dizi_transitions": flute_transitions,
            "dizi_overlap_count": sum(t["dry_gap"] < -1e-6 for t in flute_transitions),
            "wrapped_notes": [{"id": e["id"], "note": e["note"], "onset": e["onset"], "wrap_end": e["dry_end"]-period} for e in wrapped],
            "loop_boundary_dry_overlaps": loop_pairs,
            "loop_boundary_adjacent_intervals": [p for p in loop_pairs if p["pitch_class_interval"] in (1, 2, 10, 11)]}


def project_parent_plan(events):
    projected, changes = copy.deepcopy(events), []
    by_id = {e["id"]: e for e in projected}
    for event_id, new_end in (("viola:0", 5.75), ("viola:20", 85.75)):
        event = by_id[event_id]
        before = copy.deepcopy(event)
        event["dry_end"] = new_end
        event["dry_duration"] = new_end-event["onset"]
        event["nominal_duration"] = event["dry_duration"]-.5
        changes.append({"action": "shorten_pad", "before": before, "after": copy.deepcopy(event)})
    for event_id in ("viola:13", "viola:14"):
        event = by_id[event_id]
        before = copy.deepcopy(event)
        event["onset"] += .85
        event["dry_duration"] -= .85
        event["nominal_duration"] -= .85
        changes.append({"action": "delay_pad_keep_end", "before": before, "after": copy.deepcopy(event)})
    changes.append({"action": "remove_pad", "before": copy.deepcopy(by_id["viola:9"])})
    projected = [e for e in projected if e["id"] != "viola:9"]
    for pluck_id, flute_id in (("guzheng:9", "dizi:10"), ("guzheng:27", "dizi:28"), ("guzheng:36", "dizi:34")):
        event = by_id[pluck_id]
        before = copy.deepcopy(event)
        event["onset"] = by_id[flute_id]["onset"]+.08
        event["dry_end"] = event["onset"]+event["dry_duration"]
        changes.append({"action": "align_response_after_flute_C5", "before": before, "after": copy.deepcopy(event)})
    flute = sorted((e for e in projected if e["instrument"] == "dizi"), key=lambda e: e["onset"])
    for event, following in zip(flute, flute[1:]):
        cap = following["onset"]-.04
        if event["dry_end"] > cap:
            before = copy.deepcopy(event)
            event["dry_end"] = cap
            event["dry_duration"] = cap-event["onset"]
            event["nominal_duration"] = event["dry_duration"]-.14
            assert event["nominal_duration"] > .2
            changes.append({"action": "mono_flute_40ms_gap", "before": before, "after": copy.deepcopy(event)})
    return projected, changes


def verify_candidate(v6, projected):
    import numpy as np
    import pyloudnorm as pyln
    import soundfile as sf
    from scipy import signal

    folder = OUTPUT.parent
    report_path = folder / "render.json"
    wave_path = folder / "yicun_shanhe_guzheng_v7.wav"
    if not report_path.exists() or not wave_path.exists():
        return
    report_bytes = report_path.read_bytes()
    report = json.loads(report_bytes)
    wave_hash_before = hashlib.sha256(wave_path.read_bytes()).hexdigest()
    events = make_events(report)
    actual_by_instrument = {i: [n for n in report["all_rendered_notes"] if n["instrument"] == i] for i in report["score"]}
    for event in events:
        index = int(event["id"].split(":")[1])
        actual = actual_by_instrument[event["instrument"]][index]
        assert abs(actual["onset"]-event["onset"]) < 1e-9 and actual["pitch"] == event["midi"]
        event["dry_duration"] = actual["duration_seconds"]
        event["dry_end"] = event["onset"]+actual["duration_seconds"]
    observed = audit(events)
    assert observed["dizi_overlap_count"] == 0
    assert len(observed["literal_adjacent_intervals"]) == 1
    assert not observed["loop_boundary_adjacent_intervals"]
    old_melody = [tuple(e) for e in v6["score"]["guzheng"] if e[4] == "melody"]
    new_melody = [tuple(e) for e in report["score"]["guzheng"] if e[4] == "melody"]
    assert old_melody == new_melody
    for old, new in zip(v6["score"]["dizi"], report["score"]["dizi"]):
        assert (old[0], old[1], old[3], old[4]) == (new[0], new[1], new[3], new[4])
        assert new[2] <= old[2]
    expected = [e for e in projected if not (e["instrument"] == "guzheng" and e["midi"] == 52 and abs(e["onset"]-13.7) < 1e-8)]
    expected_signature = sorted((e["instrument"], e["onset"], e["midi"], e["nominal_duration"], e["velocity"]) for e in expected)
    actual_signature = sorted((e["instrument"], e["onset"], e["midi"], e["nominal_duration"], e["velocity"]) for e in events)
    assert len(expected_signature) == len(actual_signature)
    for wanted, actual in zip(expected_signature, actual_signature):
        assert wanted[0] == actual[0] and all(abs(x-y) < 1e-9 for x, y in zip(wanted[1:], actual[1:]))

    pcm, rate = sf.read(wave_path, always_2d=True)
    assert rate == 44100 and pcm.shape == (96*rate, 2) and np.isfinite(pcm).all()
    peak = float(np.max(np.abs(pcm)))
    lufs = float(pyln.Meter(rate).integrated_loudness(pcm))
    true_peak = 0.0
    # Margin avoids treating each chunk edge as an independent waveform edge.
    for start in range(0, len(pcm), 8*rate):
        end = min(start+8*rate, len(pcm))
        indices = np.arange(start-128, end+128) % len(pcm)
        up = signal.resample_poly(pcm[indices], 4, 1, axis=0)
        true_peak = max(true_peak, float(np.max(np.abs(up[512:-512]))))
    boundary = np.abs(pcm[0]-pcm[-1])
    neighborhood = np.concatenate((pcm[-rate//4:], pcm[:rate//4]), axis=0)
    local_p99 = np.quantile(np.abs(np.diff(neighborhood, axis=0)), .99, axis=0)
    stem_energy = {}
    stem_hashes = {}
    for instrument in report["score"]:
        stem_path = folder / f"dry_{instrument}.wav"
        stem, stem_rate = sf.read(stem_path, always_2d=True)
        assert stem_rate == rate and stem.shape == pcm.shape
        stem_energy[instrument] = float(np.mean(stem*stem))
        stem_hashes[instrument] = hashlib.sha256(stem_path.read_bytes()).hexdigest()
    total_energy = sum(stem_energy.values())
    shares = {key: val/total_energy for key, val in stem_energy.items()}
    flute, _ = sf.read(folder / "dry_dizi.wav", always_2d=True)
    flute_events = sorted((e for e in events if e["instrument"] == "dizi"), key=lambda e: e["onset"])
    gaps = []
    for first, second in zip(flute_events, flute_events[1:]):
        begin = round(first["onset"]*rate)+round(first["dry_duration"]*rate)
        end = round(second["onset"]*rate)
        assert end-begin >= round(.04*rate)-1
        silence_peak = float(np.max(np.abs(flute[begin:end])))
        assert silence_peak == 0
        gaps.append({"after_note": first["note"], "next_note": second["note"], "next_onset": second["onset"], "frames": end-begin, "seconds": (end-begin)/rate, "pcm_peak": silence_peak})
    responses = []
    for original in (22.7, 62.8, 78.8):
        resolved = next(e for e in flute_events if e["onset"] > original and e["midi"] == 72)
        response = next(e for e in events if e["instrument"] == "guzheng" and e["midi"] == 72 and abs(e["onset"]-resolved["onset"]-.08) < 1e-8)
        responses.append({"old_onset": original, "flute_C5_onset": resolved["onset"], "new_guzheng_C5_onset": response["onset"], "offset_seconds": response["onset"]-resolved["onset"]})
    wave_hash_after = hashlib.sha256(wave_path.read_bytes()).hexdigest()
    assert wave_hash_before == wave_hash_after and report_path.read_bytes() == report_bytes, "Candidate changed during audit; rerun"
    candidate = {
        "audit_type": "independent_numerical_score_and_PCM_verification_not_listening",
        "created_at": datetime.now(timezone(timedelta(hours=8))).isoformat(),
        "wave": str(wave_path), "wave_sha256": wave_hash_after,
        "render_report_sha256": hashlib.sha256(report_bytes).hexdigest(),
        "candidate_stable_during_audit": True,
        "score_matches_parent_plan_plus_E3_removal": True,
        "all_26_guzheng_melody_events_unchanged": old_melody == new_melody,
        "guzheng_melody_count": len(new_melody),
        "all_40_flute_pitch_onset_velocity_unchanged": True,
        "flute_duration_edits": sum(a[2] != b[2] for a, b in zip(v6["score"]["dizi"], report["score"]["dizi"])),
        "full_96s_score_audit": observed,
        "all_39_flute_dry_gaps": gaps,
        "three_responses": responses,
        "duration_seconds": len(pcm)/rate, "sample_rate": rate, "channels": pcm.shape[1],
        "lufs": lufs, "sample_peak_dbfs": 20*math.log10(peak),
        "four_times_oversampled_peak_dbtp_estimate": 20*math.log10(true_peak),
        "clipped_samples": int(np.count_nonzero(np.abs(pcm) >= 1)),
        "boundary_jump_by_channel": boundary.tolist(), "boundary_neighborhood_step_p99": local_p99.tolist(),
        "boundary_jump_below_local_step_p99_both_channels": bool(np.all(boundary < local_p99)),
        "measured_dry_stem_energy_share": shares,
        "measured_viola_to_dizi_rms_db": 10*math.log10(stem_energy["viola"]/stem_energy["dizi"]),
        "dry_stem_hashes": stem_hashes,
        "assessment": "PASS for intended score and numerical audio scope. Targeted close seconds removed. One soft 50.000-50.128 D4/C4 overlap remains by design (C4 pad attack <=0.049 envelope, -26.2dB relative to its sustain). No dry seam dissonance or flute polyphony.",
        "limits": ["No listening or device test.", "Recorded-source purity/EQ owned by separate source audit.", "1.4s room tail can overlap later notes; dry gap does not mean total mix is silent.", "Existing one-shot modeled bronze remains at 55.02s with 0.1% dry energy share."]
    }
    (folder / "candidate-review.json").write_text(json.dumps(candidate, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: candidate[k] for k in ("wave_sha256", "lufs", "sample_peak_dbfs", "four_times_oversampled_peak_dbtp_estimate", "boundary_jump_below_local_step_p99_both_channels", "measured_dry_stem_energy_share", "assessment")}, ensure_ascii=False, indent=2))


def main():
    report = json.loads(SOURCE.read_text(encoding="utf-8"))
    events = make_events(report)
    baseline = audit(events)
    projected, changes = project_parent_plan(events)
    after = audit(projected)
    # This checks the proposed score algebra, not a rendered v7 or perceived result.
    assert after["dizi_overlap_count"] == 0
    assert not after["loop_boundary_adjacent_intervals"]
    residual = after["literal_adjacent_intervals"]
    assert len(residual) == 1 and {residual[0]["first"], residual[0]["second"]} == {"guzheng:20", "viola:11"}
    envelope_peak = .5-.5*math.cos(math.pi*residual[0]["seconds"]/.9)
    output = {
        "audit_type": "numerical_score_and_timing_not_listening",
        "model": "GPT-6 / Codex; exact submodel not supplied",
        "created_at": datetime.now(timezone(timedelta(hours=8))).isoformat(),
        "source": str(SOURCE), "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        "period_seconds": 96, "sample_rate": report["sample_rate"],
        "timing_method": "Guzheng uses recorded rendered durations. Dizi duration + 0.14; viola duration + 0.5; bell nominal duration. Sample rounding error <= one sample. Room tail excluded from dry pair counts.",
        "classification": "A shared C pentatonic scale is not a guarantee of smooth vertical voicing; conversely compound seconds/sevenths are not automatically wrong. Prioritize literal close-register seconds and monophonic flute overlap.",
        "baseline": baseline, "v6_events": events,
        "top_findings": [
            {"id": "ARR-V7-01", "finding": "Low-mid close seconds", "windows": [[5.992996, 6.792996], [54.275, 54.794], [85.986030, 86.786030]], "notes": ["D4 over C4", "C4 D4 E4 cluster", "D4 over C4"], "minimum_change": "Shorten only exposed C4 pads; delay both pads at 54.2/54.275 until D4 has finished. Preserve motif pitch and onset."},
            {"id": "ARR-V7-02", "finding": "C5 guzheng response starts under previous D5 flute", "windows": [[22.7, 22.942693841307487], [62.8, 62.96365031456567], [78.8, 78.91812443552526]], "minimum_change": "Move C5 response to next flute C5 onset + 0.08 seconds: 23.055398, 63.073590, 79.090524."},
            {"id": "ARR-V7-03", "finding": "Sixteen actual flute tail overlaps", "longest": [{"next_onset": 32.2585828732205, "notes": "G4 -> E4", "seconds": .183692687184496}, {"next_onset": 24.274141444413733, "notes": "C5 -> G4", "seconds": .141256930459227}], "minimum_change": "Cap actual note end at next onset - 0.04; nominal duration must subtract the sampler's added 0.14. Preserve pitches/onsets/velocities."},
            {"id": "ARR-V7-04", "finding": "A4 flute against G4 pad", "window": [36.47523786197757, 37.91523786197757], "minimum_change": "Remove only the 35.775 G4 pad; keep 35.7 C4. Do not shorten it to <650ms with the current fixed release implementation."}
        ],
        "proposed_parent_plan": {"status": "projected_only_not_rendered", "changes": changes, "audit": after,
            "viola_gain_change_db": -3,
            "viola_previous_energy_share": .059,
            "viola_projected_energy_share_before_final_loudness_normalization": .059*10**(-3/10),
            "residual_close_second_assessment": {"pair": residual[0], "new_C4_pad_attack_envelope_max": envelope_peak, "envelope_gain_db": 20*math.log10(envelope_peak), "decision": "Keep: only first 128ms of 900ms pad fade-in against already fading D4. This is an envelope-aware exception, not proof of inaudibility."}},
        "loop_assessment": "95.7 G3 folds to 0.828s; 91.5 C4 to 0.800s; 91.575 G4 to 0.875s. Opening C4 at 0.25 is consonant with these. All fold tails finish before D4 at 1.525. No dry seam pitch correction needed.",
        "implementation_caution": "viola_pad uses release=0.65s without clipping to sound length. A 35.775->36.300 actual note would be 0.525s and cause a broadcast shape error; the corrected parent plan removes that note.",
        "limits": ["No auditory evaluation.", "No v7 rendered audio verified by this script.", "No source-recording spectral contamination judgment; another audit owns that.", "circular_room has a 1.4-second decaying wet response, so dry note separation is not zero acoustic tail overlap.", "No accidental semitone/tritone score overlaps found; wide pentatonic ninths/sevenths are listed for context, not labeled defects.", "Observed modeled bronze bell at 55.02s remains from v6; three-instrument-only scope would require the parent to decide whether to remove it."]
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(OUTPUT), "baseline_dizi_overlap_count": baseline["dizi_overlap_count"],
                      "planned_flute_duration_edits": sum(c["action"] == "mono_flute_40ms_gap" for c in changes),
                      "planned_residual_adjacent_pairs": residual,
                      "projected_events": after["event_counts"]}, ensure_ascii=False, indent=2))
    verify_candidate(report, projected)


if __name__ == "__main__":
    main()
