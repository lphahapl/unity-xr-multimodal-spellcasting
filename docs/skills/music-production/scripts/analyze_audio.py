#!/usr/bin/env python3
"""Measure audio without changing it or judging its musical quality.

Required: numpy, soundfile. Optional: scipy (4x peak estimate), pyloudnorm
(integrated LUFS). Missing optional metrics are null with an explanation.
Sections JSON: [{"start": 0, "end": 8, "name": "intro"}, ...], or an object
containing that list under "sections". Times are seconds; overlaps are allowed.
"""
import argparse
import hashlib
import importlib.metadata
import json
import math
import os
from pathlib import Path
import sys
import warnings


class AnalysisError(Exception):
    pass


def load_dependencies():
    global np, sf, signal, pyln
    try:
        import numpy as np
        import soundfile as sf
    except ImportError as exc:
        raise AnalysisError(f"Missing required dependency: {exc}. Requires numpy and soundfile; nothing was installed.") from exc
    try:
        from scipy import signal
    except ImportError:
        signal = None
    try:
        import pyloudnorm as pyln
    except ImportError:
        pyln = None
    dependencies = {}
    for name, available in [("numpy", True), ("soundfile", True),
                            ("scipy", signal is not None), ("pyloudnorm", pyln is not None)]:
        try:
            version = importlib.metadata.version(name) if available else None
        except importlib.metadata.PackageNotFoundError:
            version = None
        dependencies[name] = {"available": available, "version": version}
    return dependencies


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024*1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reject_constant(value):
    raise ValueError(f"Non-standard JSON numeric constant: {value}")


def read_sections(path, duration, sample_rate):
    if path is None:
        return []
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=reject_constant)
    except (OSError, UnicodeError, ValueError) as exc:
        raise AnalysisError(f"Cannot read sections JSON: {exc}") from exc
    if isinstance(data, dict):
        data = data.get("sections")
    if not isinstance(data, list):
        raise AnalysisError("Sections JSON must be an array, or an object with a 'sections' array.")
    sections = []
    for index, entry in enumerate(data):
        if not isinstance(entry, dict):
            raise AnalysisError(f"Section {index} must be an object.")
        start, end, name = entry.get("start"), entry.get("end"), entry.get("name")
        numeric = lambda x: isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x)
        if not numeric(start) or not numeric(end) or not 0 <= start < end <= duration:
            raise AnalysisError(f"Section {index} requires finite 0 <= start < end <= {duration:.9g} seconds.")
        if not isinstance(name, str) or not name.strip():
            raise AnalysisError(f"Section {index} requires a nonempty string name.")
        first, last = round(start*sample_rate), round(end*sample_rate)
        if last <= first:
            raise AnalysisError(f"Section {index} rounds to zero samples.")
        sections.append((name, first, last, start, end))
    return sections


def dbfs(amplitude):
    return float(20*math.log10(amplitude)) if amplitude > 0 else None


def rms_peak(data):
    peak = float(np.max(np.abs(data)))
    # Scaling first avoids overflow while squaring otherwise finite large floats.
    rms = peak*float(np.sqrt(np.mean((data/peak)**2))) if peak else 0.
    return {"rms_dbfs": dbfs(rms), "sample_peak_dbfs": dbfs(peak),
            "silence": peak == 0}


def loudness(data, sample_rate):
    if not np.any(data):
        return {"integrated_lufs": None, "lufs_status": "silent"}
    if len(data)/sample_rate < .4:
        return {"integrated_lufs": None, "lufs_status": "shorter_than_0.4_seconds"}
    if pyln is None:
        return {"integrated_lufs": None, "lufs_status": "pyloudnorm_unavailable"}
    if data.shape[1] not in (1, 2):
        return {"integrated_lufs": None, "lufs_status": "unknown_multichannel_layout"}
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", RuntimeWarning)
            value = float(pyln.Meter(sample_rate).integrated_loudness(data))
        if not math.isfinite(value):
            return {"integrated_lufs": None, "lufs_status": "below_gate_or_not_finite"}
        return {"integrated_lufs": value, "lufs_status": "measured"}
    except (ValueError, FloatingPointError, OverflowError) as exc:
        return {"integrated_lufs": None, "lufs_status": "measurement_unavailable", "lufs_note": str(exc)}


def peak_estimate(data, sample_rate):
    result = {"oversampling_factor": 4, "peak_dbfs": None,
              "method": "scipy.signal.resample_poly, zero-extended file edges; not a certified true-peak measurement"}
    if signal is None:
        return {**result, "status": "scipy_unavailable"}
    if not np.any(data):
        return {**result, "status": "silent"}
    peak = 0.
    # Bounded work buffers, with more overlap than the default resampling FIR needs.
    for start in range(0, len(data), 8*sample_rate):
        end = min(len(data), start+8*sample_rate)
        left, right = max(0, start-64), min(len(data), end+64)
        with np.errstate(over="ignore", invalid="ignore"):
            upsampled = signal.resample_poly(data[left:right], 4, 1, axis=0)
        section = upsampled[(start-left)*4:(end-left)*4]
        if not np.isfinite(section).all():
            return {**result, "status": "numeric_range_exceeded"}
        peak = max(peak, float(np.max(np.abs(section))))
    return {**result, "peak_dbfs": dbfs(peak), "status": "estimated"}


def segment(data, sample_rate, name, start, end):
    sliced = data[start:end]
    return {"name": name, "start_seconds": start/sample_rate, "end_seconds": end/sample_rate,
            "start_frame": start, "end_frame_exclusive": end,
            **rms_peak(sliced), **loudness(sliced, sample_rate)}


def finite_number(value):
    value = float(value)
    return value if math.isfinite(value) else None


def loop_metrics(data, sample_rate):
    near = min(len(data), max(1, round(.05*sample_rate)))
    with np.errstate(over="ignore", invalid="ignore"):
        jump = np.abs(data[0]-data[-1])
        differences = np.concatenate((np.abs(np.diff(data[:near], axis=0)),
                                      np.abs(np.diff(data[-near:], axis=0))))
    quantiles = {}
    for label, q in [("p50", .50), ("p95", .95), ("p99", .99)]:
        with np.errstate(over="ignore", invalid="ignore"):
            values = np.quantile(differences, q, axis=0) if len(differences) else None
        quantiles[label] = [finite_number(v) for v in values] if values is not None else None
    return {"sample_jump_absolute_per_channel": [finite_number(v) for v in jump],
            "nearby_seconds_per_edge": near/sample_rate,
            "nearby_differences_exclude_loop_jump": True,
            "nearby_absolute_difference_quantiles_per_channel": quantiles,
            "nearby_absolute_difference_max_per_channel":
                [finite_number(v) for v in differences.max(axis=0)] if len(differences) else None,
            "head": segment(data, sample_rate, "first_up_to_one_second", 0, min(sample_rate, len(data))),
            "tail": segment(data, sample_rate, "last_up_to_one_second", max(0, len(data)-sample_rate), len(data)),
            "note": "Measurements only: a small sample jump does not establish a musically natural loop. Numeric overflow is null."}


def analyze(args):
    audio = args.audio.expanduser().resolve()
    output = args.out.expanduser().resolve()
    sections_path = args.sections.expanduser().resolve() if args.sections else None
    if output == audio or output == sections_path:
        raise AnalysisError("Output must differ from the input audio and sections file.")
    # lexists also refuses dangling symlinks; exclusive creation below closes races.
    if os.path.lexists(args.out.expanduser()) or os.path.lexists(output):
        raise AnalysisError(f"Refusing to overwrite an existing output path: {output}")
    if not output.parent.is_dir():
        raise AnalysisError(f"Output directory does not exist: {output.parent}")
    if not math.isfinite(args.window_seconds) or args.window_seconds <= 0:
        raise AnalysisError("--window-seconds must be finite and greater than zero.")
    dependencies = load_dependencies()
    try:
        with sf.SoundFile(audio, mode="r") as stream:
            sample_rate, frames, channels = stream.samplerate, len(stream), stream.channels
            file_format, subtype = stream.format, stream.subtype
            if frames == 0:
                raise AnalysisError("Audio contains zero frames; no measurements can be made.")
            window_frames = round(args.window_seconds*sample_rate)
            if window_frames < 1:
                raise AnalysisError("--window-seconds rounds to zero samples at this sample rate.")
            sections = read_sections(sections_path, frames/sample_rate, sample_rate)
            data = stream.read(dtype="float64", always_2d=True)
    except (OSError, RuntimeError, ValueError) as exc:
        raise AnalysisError(f"Cannot decode audio: {exc}") from exc
    if len(data) != frames:
        raise AnalysisError("Audio decoding returned fewer frames than advertised.")
    if not np.isfinite(data).all():
        raise AnalysisError("Input audio contains non-finite samples (NaN or Infinity); no report was written.")
    windows = [segment(data, sample_rate, f"window_{index+1}", start, min(start+window_frames, frames))
               for index, start in enumerate(range(0, frames, window_frames))]
    section_reports = [{**segment(data, sample_rate, name, start, end),
                        "requested_start_seconds": requested_start, "requested_end_seconds": requested_end}
                       for name, start, end, requested_start, requested_end in sections]
    report = {"schema_version": 1,
              "audio": {"path": str(audio), "sha256": sha256(audio), "bytes": audio.stat().st_size,
                        "format": file_format, "subtype": subtype, "sample_rate": sample_rate,
                        "channels": channels, "frames": frames, "duration_seconds": frames/sample_rate},
              "dependencies": dependencies,
              "overall": {**rms_peak(data), **loudness(data, sample_rate),
                          "fourfold_peak_estimate": peak_estimate(data, sample_rate)},
              "window_seconds_requested": args.window_seconds,
              "window_frames": window_frames, "windows": windows,
              "sections": section_reports,
              "loop": loop_metrics(data, sample_rate) if args.loop else None,
              "notes": ["RMS combines all channel samples; sample peak is the maximum across all channels.",
                        "Zero-amplitude dBFS and unavailable LUFS values are null, never NaN or Infinity.",
                        "LUFS uses pyloudnorm's 0.4-second gating blocks; very short or below-gate audio has no finite integrated value.",
                        "Window and section boundaries are rounded to sample frames; the final window may be shorter.",
                        "The entire decoded input is held in memory; large files may require substantial RAM.",
                        "This report does not establish natural timbre, harmony, reverb quality, subjective loudness or loop quality."]}
    payload = json.dumps(report, ensure_ascii=False, allow_nan=False, indent=2)+"\n"
    # No existing file, including either input, can be overwritten at write time.
    with output.open("x", encoding="utf-8", newline="\n") as stream:
        stream.write(payload)
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("audio", type=Path, help="Audio readable by SoundFile/libsndfile")
    parser.add_argument("--out", required=True, type=Path, help="New JSON file; its directory must already exist")
    parser.add_argument("--sections", type=Path, help="Optional section definitions in UTF-8 JSON")
    parser.add_argument("--loop", action="store_true", help="Measure the wrap boundary without a pass/fail judgment")
    parser.add_argument("--window-seconds", type=float, default=4., help="Equal analysis-window length (default: 4 seconds)")
    args = parser.parse_args()
    try:
        output = analyze(args)
    except (AnalysisError, OSError, ValueError, OverflowError, MemoryError) as exc:
        print(f"analyze_audio: {exc}", file=sys.stderr)
        return 2
    print(json.dumps({"report": str(output)}, ensure_ascii=True, allow_nan=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
