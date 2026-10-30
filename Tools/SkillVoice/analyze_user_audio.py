"""Read only three explicitly authorized recordings; no network, playback, UDP, or audio copies."""
from __future__ import annotations

import hashlib
import json
import math
import time
import wave
from pathlib import Path
from types import SimpleNamespace

import numpy as np
from pypinyin import Style, lazy_pinyin

from skill_voice import ROOT, MODEL, Decoder, make_spotter

INPUT_ROOT = Path("C:/Users/鲁迅/Documents/录音")
FILES = {
    "火球术.wav": "火球术",
    "否定火球术.wav": "不要释放火球术",
    "领域展开,诛伏赐死.wav": "领域展开，诛伏赐死",
}
LONG_PHRASE = "领域展开诛伏赐死"


def fingerprint(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def dbfs(value):
    return 20 * math.log10(value) if value > 0 else None


def inspect_audio(path):
    with wave.open(str(path), "rb") as source:
        rate, channels, width, count = source.getframerate(), source.getnchannels(), source.getsampwidth(), source.getnframes()
        if width != 2 or source.getcomptype() != "NONE":
            raise ValueError("This baseline only accepts the already verified PCM16 originals")
        pcm = np.frombuffer(source.readframes(count), dtype="<i2").reshape(-1, channels)
    audio = pcm.astype("float32") / 32768.0
    mono = audio.mean(axis=1)
    peak = float(np.max(np.abs(audio)))
    rms = float(np.sqrt(np.mean(audio * audio)))
    block = max(1, int(rate * .02))
    frame_rms = [float(np.sqrt(np.mean(mono[i:i + block] ** 2))) for i in range(0, len(mono), block)]
    active = [i for i, value in enumerate(frame_rms) if value >= .012]
    stats = {
        "encoding": "PCM signed 16-bit little endian", "sample_rate_hz": rate, "channels": channels,
        "frames": count, "duration_seconds": count / rate, "bytes": path.stat().st_size,
        "peak_linear": peak, "peak_dbfs": dbfs(peak), "rms_linear": rms, "rms_dbfs": dbfs(rms),
        "clipped_sample_count": int(np.sum((pcm == -32768) | (pcm == 32767))),
        "clipped_sample_fraction": float(np.mean((pcm == -32768) | (pcm == 32767))),
        "near_full_scale_sample_fraction": float(np.mean(np.abs(audio) >= .99)),
        "channel_rms_dbfs": [dbfs(float(np.sqrt(np.mean(audio[:, c] ** 2)))) for c in range(channels)],
        "mono_rms_dbfs": dbfs(float(np.sqrt(np.mean(mono ** 2)))),
        "channel_correlation": float(np.corrcoef(audio.T)[0, 1]) if channels == 2 else None,
        "energy_active_fraction_20ms_threshold_0_012": len(active) / len(frame_rms),
        "first_active_frame_start_seconds": active[0] * .02 if active else None,
        "last_active_frame_end_seconds": min(count / rate, (active[-1] + 1) * .02) if active else None,
        "transcoded": False, "audio_copied": False,
        "preprocessing": "In-memory arithmetic stereo mean; original 48 kHz fed to sherpa internal resampler. No gain normalization.",
        "quality_note": "RMS is digital level, not LUFS/SPL. Energy-active frames are not semantic VAD. Zero clipped samples does not prove absence of distortion.",
    }
    return mono, rate, stats


class RecordingDecoder(Decoder):
    def __init__(self, kws, args):
        super().__init__(kws, args)
        self.records = []

    def emit(self, frame_end, basis, force=False):
        pending = self.pending
        voiced = self.last_voiced_end
        before = len(self.hits)
        super().emit(frame_end, basis, force)
        if len(self.hits) > before:
            phrase, _, audio_detection, token_time = pending
            end = min(frame_end, voiced if voiced is not None else audio_detection)
            self.records.append({"phrase": phrase, "audio_end_estimate_seconds": end,
                                 "audio_consumed_when_kws_hit_seconds": audio_detection,
                                 "audio_consumed_when_event_emitted_seconds": frame_end,
                                 "raw_token_last_timestamp_seconds": token_time,
                                 "end_method": "20ms RMS frame energy-end estimate; not manually annotated word boundary",
                                 "raw_token_timestamp_note": "Relative to native KWS segment; not an absolute file offset after resets."})


def run_case(kws, args, audio, rate):
    decoder = RecordingDecoder(kws, args)
    padded = np.concatenate([audio, np.zeros(rate, dtype="float32")])
    block = int(rate * .02)
    started = time.perf_counter()
    for i in range(0, len(padded), block):
        decoder.accept(padded[i:i + block], rate, i / rate, "wav-sample-offset")
    decoder.emit(len(padded) / rate, "wav-sample-offset", force=True)
    return {"hits": decoder.records, "processing_seconds": time.perf_counter() - started,
            "tail_padding_seconds": 1.0, "sent_to_unity": False,
            "note": "Offline processing time is not live microphone latency."}


def trial_keywords():
    syllables = lazy_pinyin(LONG_PHRASE, style=Style.TONE, neutral_tone_with_five=False)
    initials = ("zh", "ch", "sh", "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h", "j", "q", "x", "r", "z", "c", "s", "y", "w")
    encoded = []
    for syllable in syllables:
        initial = next((item for item in initials if syllable.startswith(item)), "")
        if initial:
            encoded.append(initial)
        encoded.append(syllable[len(initial):])
    tokens = {line.split()[0] for line in (MODEL / "tokens.txt").read_text(encoding="utf-8").splitlines() if line.strip()}
    missing = [token for token in encoded if token not in tokens]
    if missing:
        raise ValueError(f"Unsupported trial tokens: {missing}")
    trial = ROOT / "trial_keywords.txt"
    baseline = (ROOT / "keywords.txt").read_text(encoding="utf-8-sig").rstrip()
    line = " ".join(encoded) + " @" + LONG_PHRASE
    trial.write_text(baseline + "\n" + line + "\n", encoding="utf-8")
    return trial, {"phrase": LONG_PHRASE, "pinyin_source": "Local pypinyin 0.55.0 Style.TONE; every initial/final checked against official model tokens.txt",
                   "syllables": syllables, "line": line, "default_keywords_modified": False, "game_effect_added": False}


def main():
    before = {name: fingerprint(INPUT_ROOT / name) for name in FILES}
    baseline_keyword_hash = fingerprint(ROOT / "keywords.txt")
    args = SimpleNamespace(model=MODEL, keywords=ROOT / "keywords.txt", threshold=.25, score=1.0,
                           energy_threshold=.012, end_silence=.12, debounce=.6)
    baseline = make_spotter(args)
    trial_file, trial_info = trial_keywords()
    trial_args = SimpleNamespace(**{**vars(args), "keywords": trial_file})
    trial = make_spotter(trial_args)
    report = {"model": MODEL.name, "encoder": "chunk-8 int8", "threshold": .25, "keywords_score": 1.0,
              "runtime": "sherpa-onnx 2.0.1", "model_changed": False,
              "user_confirmed_transcript_source": "User supplied exact spoken content for each file; repetition counts were not supplied.",
              "accuracy_statistics_computed": False, "network_audio_upload": False, "unity_events_sent": False,
              "trial": trial_info, "files": []}
    for name, transcript in FILES.items():
        path = INPUT_ROOT / name
        audio, rate, stats = inspect_audio(path)
        report["files"].append({"filename": name, "path": str(path), "user_confirmed_spoken_content": transcript,
                                "sha256_before": before[name], "audio": stats,
                                "baseline": run_case(baseline, args, audio, rate),
                                "trial_extended_keywords": run_case(trial, trial_args, audio, rate),
                                "sha256_after": fingerprint(path)})
    report["originals_unchanged"] = all(item["sha256_before"] == item["sha256_after"] for item in report["files"])
    report["default_keywords_unchanged"] = baseline_keyword_hash == fingerprint(ROOT / "keywords.txt")
    report["limitations"] = ["KWS is acoustic keyword matching, not full-sentence semantic intent or negation understanding.",
                             "The long phrase is tested only with a separate trial keyword file; no domain expansion gameplay effect was connected.",
                             "Offsets are on the original audio timeline, estimated by energy frames. No inference of repetition ground truth or aggregate accuracy."]
    out = ROOT / "logs/user-audio-baseline.json"
    out.parent.mkdir(exist_ok=True)
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
