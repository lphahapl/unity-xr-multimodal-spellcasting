"""Exercise the installed Pedalboard on actual v8 stems; no subjective verdict."""
import hashlib
import importlib.metadata
import json
import sys
import time
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
from pedalboard import Pedalboard, Reverb

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Logs/yicun-shanhe-shape-v8"
OUT = ROOT / "Logs/yicun-shanhe-pedalboard-v9/tool-setup"
START, END, TAIL = 16, 24, 6
SEND = {"guzheng": .18, "dizi": .12, "viola": .10}
PARAMETERS = {"room_size": .52, "damping": .65, "wet_level": 1.,
              "dry_level": 0., "width": .85, "freeze_mode": 0.}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rms(x):
    return float(np.sqrt(np.mean(np.asarray(x, dtype=np.float64)**2)))


def db(x):
    return float(20*np.log10(x)) if x > 0 else None


def stats(x):
    return {"shape_channels_frames": list(x.shape), "rms_dbfs": db(rms(x)),
            "sample_peak_dbfs": db(float(np.max(np.abs(x)))),
            "finite": bool(np.isfinite(x).all())}


def board(parameters=None):
    return Pedalboard([Reverb(**(parameters or PARAMETERS))])


def chunks(processor, samples, rate, preserve_state):
    position, pieces = 0, []
    sizes = [4096, 3000, 7777, 5120]
    while position < samples.shape[1]:
        size = sizes[len(pieces) % len(sizes)]
        end = min(samples.shape[1], position+size)
        if samples.shape[1]-end < 8:
            end = samples.shape[1]
        pieces.append(processor.process(samples[:, position:end], rate, buffer_size=512,
                                        reset=position == 0 or not preserve_state))
        position = end
    return np.concatenate(pieces, axis=1), len(pieces)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rate, inputs, sources = None, {}, {}
    for name in SEND:
        path = SOURCE/f"dry_{name}.wav"
        info = sf.info(path)
        if rate is None:
            rate = info.samplerate
        assert info.samplerate == rate and info.channels == 2
        clip, _ = sf.read(path, start=START*rate, stop=END*rate, dtype="float32", always_2d=True)
        assert len(clip) == (END-START)*rate and np.isfinite(clip).all()
        # Bench-only fade avoids truncating the final excerpt note. Source files stay untouched.
        fade_frames = round(.05*rate)
        clip[-fade_frames:] *= (.5+.5*np.cos(np.linspace(0, np.pi, fade_frames)))[:, None]
        cf = np.ascontiguousarray(clip.T)
        inputs[name] = np.pad(cf, ((0, 0), (0, TAIL*rate)))
        sources[name] = {"path": str(path), "sha256_before": sha(path),
                         "range_seconds": [START, END], "bench_end_fade_seconds": .05,
                         "audio": stats(cf)}
    dry = sum(inputs.values())
    shared_send = np.ascontiguousarray(sum(SEND[name]*x for name, x in inputs.items()), dtype=np.float32)
    processor = board()
    begin = time.perf_counter()
    wet = processor.process(shared_send, rate, buffer_size=512, reset=True)
    elapsed = time.perf_counter()-begin
    repeated = processor.process(shared_send, rate, buffer_size=512, reset=True)
    fresh = board().process(shared_send, rate, buffer_size=512, reset=True)
    changed = board({**PARAMETERS, "room_size": .28, "damping": .25}).process(
        shared_send, rate, buffer_size=512, reset=True)
    chunked, count = chunks(board(), shared_send, rate, preserve_state=True)
    wrong_reset, _ = chunks(board(), shared_send, rate, preserve_state=False)
    mixed = dry+wet
    tail = wet[:, (END-START)*rate:]
    first_tail, last_tail = rms(tail[:, :rate//2]), rms(tail[:, -rate//2:])
    tail_maximum = np.max(np.abs(tail), axis=0)
    audible_proxy = np.flatnonzero(tail_maximum > float(np.max(np.abs(wet)))*1e-4)
    metrics = {
        "render_wall_seconds": elapsed,
        "repeat_same_instance_max_abs_difference": float(np.max(np.abs(wet-repeated))),
        "repeat_fresh_instance_max_abs_difference": float(np.max(np.abs(wet-fresh))),
        "chunk_preserved_state_max_abs_difference": float(np.max(np.abs(wet-chunked))),
        "chunk_count": count,
        "wrong_reset_control_difference_rms": rms(wet-wrong_reset),
        "parameter_change_relative_rms": rms(wet-changed)/max(rms(wet), 1e-30),
        "tail_first_half_second_rms_dbfs": db(first_tail),
        "tail_last_half_second_rms_dbfs": db(last_tail),
        "tail_decay_first_to_last_half_second_db": db(last_tail/first_tail) if first_tail else None,
        "tail_last_time_above_minus80db_relative_to_wet_peak": (int(audible_proxy[-1])+1)/rate if len(audible_proxy) else None,
        "wet_minus_dry_rms_db": db(rms(wet)/rms(dry)),
    }
    checks = {
        "specified_pedalboard_version_loaded": importlib.metadata.version("pedalboard") == "0.9.26",
        "all_source_clips_non_silent": all(s["audio"]["rms_dbfs"] is not None for s in sources.values()),
        "finite_non_silent_wet": bool(np.isfinite(wet).all() and rms(wet) > 1e-10),
        "length_and_channel_count_preserved": wet.shape == (2, (END-START+TAIL)*rate),
        "parameters_change_output": metrics["parameter_change_relative_rms"] > .01,
        "tail_present_after_input_stops": first_tail > 1e-10,
        "six_second_padding_captures_decay": last_tail < first_tail*.001,
        "same_instance_reset_repeats": metrics["repeat_same_instance_max_abs_difference"] < 1e-7,
        "fresh_instance_repeats": metrics["repeat_fresh_instance_max_abs_difference"] < 1e-7,
        "reset_false_chunks_continuous": metrics["chunk_preserved_state_max_abs_difference"] < 1e-7,
        "reset_each_chunk_control_changes_output": metrics["wrong_reset_control_difference_rms"] > 1e-7,
        "preview_finite_no_clipping": bool(np.isfinite(mixed).all() and np.max(np.abs(mixed)) < 1),
        "source_files_unchanged": all(sha(Path(s["path"])) == s["sha256_before"] for s in sources.values()),
    }
    outputs = {}
    for name, data in [("preview_dry_16-24_plus6", dry),
                       ("preview_shared_return_16-24_plus6", wet),
                       ("preview_mix_16-24_plus6", mixed)]:
        path = OUT/f"{name}.wav"
        sf.write(path, data.T, rate, subtype="PCM_24")
        outputs[name] = {"path": str(path), "sha256": sha(path), **stats(data)}
    result = {
        "status": "passed" if all(checks.values()) else "needs_review",
        "timestamp": datetime.now().astimezone().isoformat(),
        "python_executable": sys.executable,
        "python_version": sys.version,
        "versions": {name: importlib.metadata.version(name) for name in
                     ["pedalboard", "numpy", "soundfile", "scipy", "pyloudnorm", "mido"]},
        "sample_rate": rate, "sources": sources,
        "routing": "Sum the three individually scaled sends, process one wet-only Reverb, then add this shared return to the retained dry mix.",
        "parameters": PARAMETERS, "send_gains_linear": SEND,
        "array_contract": "SoundFile returns (frames, channels); transpose and make contiguous float32 for Pedalboard (channels, frames); transpose back when writing SoundFile.",
        "call": "Pedalboard([Reverb(**parameters)]).process(audio_cf, sample_rate, buffer_size=512, reset=True)",
        "chunk_rule": "Use reset=True for the first chunk of a fresh render; reset=False for every later chunk, including appended zero blocks that flush tails.",
        "checks": checks, "metrics": metrics, "outputs": outputs,
        "limitations": ["Numeric technical tests only; no subjective listening or naturalness claim.",
                        "Parameters and sends are a tested starting configuration, not an artistic optimum.",
                        "This installs an effects library and reuses the existing real sample stems; it does not add a new instrument or sample library.",
                        "The excerpt uses a documented 50ms end fade and six-second tail; the full looping composition needs its own periodic-state and seam checks."]}
    (OUT/"test-render-report.json").write_text(json.dumps(result, ensure_ascii=False, allow_nan=False, indent=2), encoding="utf-8")
    (OUT/"api-docstrings.txt").write_text("Reverb\n"+(Reverb.__doc__ or "")+"\n\nProcess\n"+(Pedalboard.process.__doc__ or ""), encoding="utf-8")
    print(json.dumps({"status": result["status"], "checks": checks, "metrics": metrics,
                      "python_executable": sys.executable}, ensure_ascii=False, indent=2), flush=True)


if __name__ == "__main__":
    main()
