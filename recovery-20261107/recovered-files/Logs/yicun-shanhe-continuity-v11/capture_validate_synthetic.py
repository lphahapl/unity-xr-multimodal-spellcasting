"""Small, explicitly synthetic validation. Never reads a Unity capture."""
import importlib.util
import json
from pathlib import Path
import numpy as np
from scipy.signal import resample_poly
import soundfile as sf

LOG = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("capture_qa", LOG/"capture_qa.py")
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


def write_new(path, data, sr):
    if path.exists():
        raise FileExistsError(path)
    sf.write(path, data, sr, subtype="FLOAT")


def main():
    fixtures = LOG/"capture-synthetic-v2"
    fixtures.mkdir(exist_ok=False)
    source_sr, output_sr, seconds = 8000, 11025, 4
    rng = np.random.default_rng(118)
    t = np.arange(source_sr*seconds)/source_sr
    envelope = .28+.22*np.sin(2*np.pi*t/.91)**2
    left = envelope*(.22*np.sin(2*np.pi*(173*t+12*t*t))+.12*np.sin(2*np.pi*317*t))
    right = envelope*(.18*np.sin(2*np.pi*(193*t+8*t*t))+.10*np.sin(2*np.pi*419*t))
    source = np.column_stack((left, right))+.008*rng.standard_normal((len(t), 2))
    source[:round(.08*source_sr)] *= .00001  # A genuine very quiet reference region.
    source_path = fixtures/"source.wav"
    write_new(source_path, source, source_sr)
    decoded, _ = sf.read(source_path, always_2d=True)
    loop = resample_poly(decoded, 441, 320, axis=0, padtype="wrap")
    gain = .17
    head = round(.23*output_sr)
    content = np.tile(loop, (3, 1))*gain
    capture = np.concatenate((np.zeros((head, 2)), content))
    cases = []
    positive_path = fixtures/"positive.wav"
    write_new(positive_path, capture, output_sr)
    cases.append(("positive_stereo_resampling_gain_head", positive_path, False))
    insert_at = head+len(loop)
    gap_frames = round(.05*output_sr)
    inserted = np.concatenate((capture[:insert_at], np.zeros((gap_frames, 2)), capture[insert_at:]))
    inserted_path = fixtures/"inserted-50ms.wav"
    write_new(inserted_path, inserted, output_sr)
    cases.append(("negative_insert_50ms_at_first_seam", inserted_path, True))
    muted = capture.copy()
    mute_at = insert_at+round(.4*output_sr)
    muted[mute_at:mute_at+gap_frames] = 0
    muted_path = fixtures/"muted-active-50ms.wav"
    write_new(muted_path, muted, output_sr)
    cases.append(("negative_replace_50ms_with_zeros", muted_path, True))
    quiet = capture.copy()
    quiet[insert_at:insert_at+gap_frames] = 0
    quiet_path = fixtures/"muted-already-near-silent-50ms.wav"
    write_new(quiet_path, quiet, output_sr)
    cases.append(("quiet_reference_not_automatically_dropout", quiet_path, False))
    mono_path = fixtures/"positive-mono.wav"
    write_new(mono_path, capture.mean(axis=1), output_sr)
    cases.append(("positive_mono_downmix", mono_path, False))
    results = []
    for label, path, should_fail in cases:
        result = qa.audit(source_path, path, max_head=.8, expected_sha=qa.sha(source_path))
        report_path = fixtures/f"{label}.json"
        with report_path.open("x", encoding="utf-8") as file:
            json.dump(result, file, ensure_ascii=False, allow_nan=False, indent=2)
        passed = (result["status"] == "needs_review") == should_fail
        if not should_fail:
            passed &= result["alignment"]["offset_frames"] == head and abs(result["fitted_signed_gain"]-gain) < 1e-6
        results.append({"case": label, "fixture_kind": "synthetic, not Unity/device audio", "expected_needs_review": should_fail,
                        "passed": bool(passed), "status": result["status"], "issues": result["issues"],
                        "estimated_head_frames": result["alignment"]["offset_frames"], "expected_head_frames": head,
                        "estimated_gain": result["fitted_signed_gain"], "expected_gain": gain,
                        "periodicity": result["periodicity"], "report": str(report_path),
                        "fixture_sha256": qa.sha(path)})
    output = {"synthetic_only": True, "all_tests_passed": all(x["passed"] for x in results),
              "source_rate": source_sr, "capture_rate": output_sr, "source_period_seconds": seconds,
              "inserted_or_muted_frames": gap_frames, "inserted_or_muted_seconds": gap_frames/output_sr,
              "script_sha256": qa.sha(LOG/"capture_qa.py"), "validation_script_sha256": qa.sha(Path(__file__)), "cases": results}
    output["earlier_validation_note"] = "The first fixture version incorrectly demanded a hard dropout flag when muting the deliberately near-silent reference window. It remains archived; this revision tests an active mute and separately confirms that source quietness is not automatically failure. Detector code was not relaxed."
    with (LOG/"capture-synthetic-validation-v2.json").open("x", encoding="utf-8") as file:
        json.dump(output, file, ensure_ascii=False, allow_nan=False, indent=2)
    print(json.dumps(output, indent=2))
    if not output["all_tests_passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
