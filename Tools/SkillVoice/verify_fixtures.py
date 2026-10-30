"""Reproducible model checks; synthetic fixture hits are NOT human accuracy."""
import json
import time
from types import SimpleNamespace

import numpy as np

from skill_voice import ROOT, MODEL, Decoder, make_spotter, read_wav


def main():
    args = SimpleNamespace(model=MODEL, keywords=ROOT / "keywords.txt", threshold=.25, score=1.0,
                           energy_threshold=.012, end_silence=.12, debounce=.6)
    kws = make_spotter(args)
    rate = 16000
    cases = []
    samples = {}
    for name, phrase in [("shield", "护体咒"), ("fireball", "火球术"), ("cancel", "取消施法"), ("negative", None)]:
        audio, sr = read_wav(ROOT / "fixtures" / (name + ".wav"))
        assert sr == rate
        samples[name] = audio
        cases.append((name, audio, [phrase] if phrase else []))
    cases.append(("silence_3_seconds", np.zeros(3 * rate, dtype="float32"), []))
    gap = np.zeros(rate, dtype="float32")
    cases.append(("sequence_and_repeat", np.concatenate([samples["shield"], gap, samples["fireball"], gap,
                                                        samples["cancel"], gap, samples["shield"]]),
                  ["护体咒", "火球术", "取消施法", "护体咒"]))
    report = {"model": MODEL.name, "synthetic_tts_only": True, "human_accuracy_tested": False, "cases": []}
    for name, audio, expected in cases:
        decoder = Decoder(kws, args)
        audio = np.concatenate([audio, gap])
        started = time.perf_counter()
        for i in range(0, len(audio), 320):
            decoder.accept(audio[i:i + 320], rate, i / rate, "wav-sample-offset")
        decoder.emit(len(audio) / rate, "wav-sample-offset", force=True)
        report["cases"].append({"name": name, "expected": expected, "actual": decoder.hits,
                                "passed": expected == decoder.hits,
                                "audio_seconds_with_tail": len(audio) / rate,
                                "processing_seconds": time.perf_counter() - started})
    report["passed"] = all(case["passed"] for case in report["cases"])
    out = ROOT / "logs/fixtures-report.json"
    out.parent.mkdir(exist_ok=True)
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if not report["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
