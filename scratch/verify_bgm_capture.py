"""Compare an engine callback WAV with a periodic source; never claims device capture.

Use only after capture has finished. Dependencies: NumPy, SciPy, SoundFile.
All correlation and gain estimates use actual decoded PCM and WAV sample rates.
"""
import argparse
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
import sys

import numpy as np
from scipy.signal import correlate, resample_poly
import soundfile as sf


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def db(value):
    return float(20*np.log10(value)) if value > 0 else None


def rms(data):
    return float(np.sqrt(np.mean(data*data))) if data.size else 0.0


def normalized(a, b):
    aa, bb = float(np.sum(a*a)), float(np.sum(b*b))
    return float(np.sum(a*b)/math.sqrt(aa*bb)) if aa > 0 and bb > 0 else None


def channel_map(source, capture):
    if source.shape[1] == capture.shape[1]:
        return source, capture, "matching channels, shared fitted gain"
    if capture.shape[1] == 1:
        return source.mean(axis=1, keepdims=True), capture, "source arithmetic channel mean to captured mono"
    if source.shape[1] == 1:
        return np.repeat(source, capture.shape[1], axis=1), capture, "mono source repeated to captured channels"
    return source.mean(axis=1, keepdims=True), capture.mean(axis=1, keepdims=True), "unmatched multichannel layouts compared as arithmetic mono; channel routing unverified"


def correlations(search, template):
    numerator = sum(correlate(search[:, c], template[:, c], mode="valid", method="fft") for c in range(search.shape[1]))
    power = np.sum(search*search, axis=1)
    energy = np.r_[0.0, np.cumsum(power)]
    denominator = np.sqrt(np.maximum(energy[len(template):]-energy[:-len(template)], 0)*np.sum(template*template))
    return np.divide(numerator, denominator, out=np.zeros_like(numerator), where=denominator > 1e-30)


def align(source, capture, sr, max_head):
    width = min(round(12*sr), round(.75*len(source)))
    anchor = min(round(.2*sr), len(source)-width)
    template = source[anchor:anchor+width]
    search_end = min(len(capture), round(max_head*sr)+anchor+width)
    if search_end < anchor+width:
        raise ValueError("Capture is too short for the alignment template")
    # Full-rate FFT over the entire permitted head range. A decimated search
    # can select the wrong pitch-period peak; do not restrict refinement to it.
    fine = correlations(capture[:search_end], template)
    positions = np.arange(len(fine))-anchor
    eligible = (positions >= 0) & (positions <= round(max_head*sr))
    if not np.any(eligible):
        raise ValueError("No permitted alignment offset")
    best = float(np.max(np.abs(fine[eligible])))
    # Floating-point ties between exact repeated cycles choose the earliest;
    # this is much tighter than the separation between neighbouring pitch peaks.
    point = int(np.flatnonzero(eligible & (np.abs(fine) >= best-1e-10))[0])
    offset = int(positions[point])
    if offset < 0:
        raise ValueError("Fine alignment begins before capture; this script requires the scheduled first loop to be captured")
    return offset, {"offset_frames": offset, "offset_seconds": offset/sr,
                    "template_source_start_seconds": anchor/sr, "template_seconds": width/sr,
                    "normalized_uncentered_correlation": float(fine[point]),
                    "method": "full-rate normalized FFT correlation across complete allowed range",
                    "max_head_search_seconds": max_head}


def repeated(source, start, count):
    return source[np.arange(start, start+count, dtype=np.int64) % len(source)]


def block_sums(source, capture, offset, sr):
    rows = []
    for begin in range(offset, len(capture), sr):
        end = min(begin+sr, len(capture))
        actual = capture[begin:end]
        reference = repeated(source, begin-offset, end-begin)
        rows.append({"capture_start_frame": begin, "capture_end_frame": end,
                     "source_elapsed_seconds": (begin-offset)/sr, "count": actual.size,
                     "source_energy": float(np.sum(reference*reference)),
                     "capture_energy": float(np.sum(actual*actual)), "dot": float(np.sum(actual*reference))})
    return rows


def aggregate(rows, gain):
    aa, bb, dot = (sum(x[k] for x in rows) for k in ("source_energy", "capture_energy", "dot"))
    count = sum(x["count"] for x in rows)
    error = max(0.0, bb-2*gain*dot+gain*gain*aa)
    return {"normalized_uncentered_correlation": dot/math.sqrt(aa*bb) if aa*bb > 0 else None,
            "rms_error_dbfs": db(math.sqrt(error/count)) if count else None,
            "error_rms_over_expected_rms": math.sqrt(error/(gain*gain*aa)) if gain*gain*aa else None,
            "capture_rms_dbfs": db(math.sqrt(bb/count)) if count else None}


def local_window(source, capture, offset, gain, sr, start, end):
    first, last = max(offset, round(start*sr)), min(len(capture), round(end*sr))
    if last-first < round(.05*sr):
        return {"available": False}
    actual = capture[first:last]
    reference = repeated(source, first-offset, len(actual))
    error = actual-gain*reference
    margin = round(.1*sr)
    # Template includes +/-100 ms, allowing a detected insertion or dropped block.
    extended = repeated(source, first-offset-margin, len(actual)+2*margin)
    correlations_by_lag = correlations(extended, actual)
    best = int(np.argmax(np.abs(correlations_by_lag)))
    # Positive lag means captured content is later (delayed) relative to source time.
    delay = margin-best
    return {"available": True, "capture_start_seconds": first/sr, "capture_end_seconds": last/sr,
            "fixed_alignment_correlation": normalized(actual, reference),
            "fixed_alignment_error_rms_dbfs": db(rms(error)),
            "fixed_alignment_error_over_expected_rms": rms(error)/rms(gain*reference) if rms(gain*reference) else None,
            "best_local_additional_delay_frames": delay, "best_local_additional_delay_seconds": delay/sr,
            "best_local_correlation": float(correlations_by_lag[best]),
            "lag_search_seconds_each_side": .1}


def audit(source_path, capture_path, max_head=20, expected_sha=None):
    source_hash, capture_hash = sha(source_path), sha(capture_path)
    if expected_sha and expected_sha != source_hash:
        raise ValueError("Source hash differs from the expected frozen file")
    source, source_sr = sf.read(source_path, always_2d=True, dtype="float64")
    captured, sr = sf.read(capture_path, always_2d=True, dtype="float64")
    if not source.size or not captured.size or not np.isfinite(source).all() or not np.isfinite(captured).all():
        raise ValueError("Empty or non-finite audio")
    source_channels, captured_channels = source.shape[1], captured.shape[1]
    duration = len(source)/source_sr
    rational_period = len(source)*sr/source_sr
    period = round(rational_period)
    divisor = math.gcd(sr, source_sr)
    if sr != source_sr:
        source = resample_poly(source, sr//divisor, source_sr//divisor, axis=0, padtype="wrap")
    source = source[:period]
    source, captured, mapping = channel_map(source, captured)
    offset, alignment = align(source, captured, sr, max_head)
    rows = block_sums(source, captured, offset, sr)
    gain = sum(x["dot"] for x in rows)/sum(x["source_energy"] for x in rows)
    # Stable-block median is resistant to a damaged loop and does not assume AudioSource.volume staging.
    adequate = [x for x in rows if x["source_energy"]/x["count"] > np.mean(source*source)*.03
                and x["capture_energy"] > 0
                and abs(x["dot"])/math.sqrt(x["source_energy"]*x["capture_energy"]) > .98]
    if adequate:
        gain = float(np.median([x["dot"]/x["source_energy"] for x in adequate]))
    global_match = aggregate(rows, gain)
    windows = [{"capture_start_seconds": x["capture_start_frame"]/sr, "source_elapsed_seconds": x["source_elapsed_seconds"],
                **aggregate([x], gain)} for x in rows]
    available = len(captured)-offset-period
    periodicity = {"available": available > 0, "expected_period_frames": period,
                   "expected_period_seconds": period/sr, "paired_seconds": max(available, 0)/sr}
    if available > 0:
        first, second = captured[offset:offset+available], captured[offset+period:]
        delta = second-first
        periodicity.update({"correlation": normalized(first, second), "difference_rms_dbfs": db(rms(delta)),
                            "difference_peak": float(np.abs(delta).max()),
                            "difference_rms_over_pair_rms": rms(delta)/rms(first) if rms(first) else None})
    seams = []
    for number in (1, 2):
        at = (offset+number*period)/sr
        seams.append({"loop_number": number, "predicted_capture_seconds": at,
                      "source_loop_elapsed_seconds": number*duration,
                      "whole_plus_minus_two_seconds": local_window(source, captured, offset, gain, sr, at-2, at+2),
                      "before": local_window(source, captured, offset, gain, sr, at-2, at-.25),
                      "after": local_window(source, captured, offset, gain, sr, at+.25, at+2)})
    # Independent anchors, including throughout each repeat, distinguish a good
    # initial correlation from a subsequent insertion/deletion or time stretch.
    anchors = []
    for elapsed in np.arange(2, max(2, (len(captured)-offset)/sr-3), 8):
        at = offset/sr + float(elapsed)
        window = local_window(source, captured, offset, gain, sr, at, at+3)
        window['source_elapsed_seconds'] = float(elapsed)
        anchors.append(window)
    zero_mask = np.all(captured[offset:] == 0, axis=1)
    changes = np.diff(np.r_[False, zero_mask, False].astype(np.int8))
    zero_runs = []
    for start, end in zip(np.flatnonzero(changes == 1), np.flatnonzero(changes == -1)):
        if end-start < round(.005*sr):
            continue
        reference = repeated(source, int(start), int(end-start))
        source_level = rms(reference)
        active = source_level > rms(source)*10**(-35/20) and abs(gain)*source_level > 1e-7
        zero_runs.append({"capture_start_seconds": (offset+int(start))/sr, "capture_end_seconds": (offset+int(end))/sr,
                          "seconds": int(end-start)/sr, "expected_source_rms_dbfs": db(source_level),
                          "expected_captured_rms_dbfs": db(abs(gain)*source_level),
                          "source_is_above_quiet_review_floor": active,
                          "suspected_extra_dropout": bool(active and end-start >= round(.01*sr))})
    issues, notices = [], []
    if abs(alignment["normalized_uncentered_correlation"]) < .8:
        issues.append("weak_initial_content_alignment")
    if global_match["normalized_uncentered_correlation"] is None or abs(global_match["normalized_uncentered_correlation"]) < .98:
        issues.append("global_capture_differs_from_resampled_loop")
    # Bit-identical repeated samples are not a requirement of an engine's rate
    # converter. Preserve the stricter historical metric as a notice, while
    # continuity gates use content correlation and time offsets independently.
    if periodicity.get("difference_rms_over_pair_rms", 0) > .001:
        notices.append("cycles_differ_over_0_1_percent_rms; exact_repeat_not_claimed")
    if periodicity.get('correlation') is None or periodicity['correlation'] < .995:
        issues.append('weak_cycle_content_match')
    if any(x["suspected_extra_dropout"] for x in zero_runs):
        issues.append("extra_all_zero_audio_where_reference_is_active")
    for seam in seams:
        before, after = seam["before"], seam["after"]
        if before["available"] and after["available"]:
            shift = after["best_local_additional_delay_frames"]-before["best_local_additional_delay_frames"]
            seam["local_delay_change_frames"] = shift
            if abs(shift) > max(2, round(.0001*sr)):
                issues.append(f"timing_shift_across_loop_{seam['loop_number']}")
            for label in ('before', 'after', 'whole_plus_minus_two_seconds'):
                if abs(seam[label].get('best_local_correlation') or 0) < .98:
                    issues.append(f"weak_content_at_loop_{seam['loop_number']}_{label}")
    strong = [a for a in anchors if a['available'] and abs(a['best_local_correlation']) >= .98]
    if len(strong) < max(1, len(anchors)*.8):
        issues.append('insufficient_independent_content_anchors')
    if any(abs(a['best_local_additional_delay_frames']) > max(2, round(.0001*sr)) for a in strong):
        issues.append('content_time_shift_after_initial_alignment')
    complete_seams = all(x["before"]["available"] and x["after"]["available"] for x in seams)
    stable = source_hash == sha(source_path) and capture_hash == sha(capture_path)
    if not stable:
        issues.append("input_changed_during_analysis")
    return {"status": "needs_review" if issues else "numeric_capture_checks_passed" if complete_seams else "partial_capture_checks_passed",
            "timestamp": datetime.now().astimezone().isoformat(), "issues": issues, 'notices': notices,
            "source": {"path": str(source_path), "sha256": source_hash, "sample_rate": source_sr, "channels": source_channels,
                       "duration_seconds": duration},
            "capture": {"path": str(capture_path), "sha256": capture_hash, "sample_rate": sr, "channels": captured_channels,
                        "duration_seconds": len(captured)/sr, "subtype": sf.info(capture_path).subtype},
            "alignment": alignment, "channel_comparison": mapping, "fitted_signed_gain": gain,
            "gain_db": db(abs(gain)), "gain_method": "Median per-second least-squares gain over reference-active blocks; no assumption about Source.volume staging.",
            "head_before_aligned_start": {"seconds": offset/sr, "rms_dbfs": db(rms(captured[:offset]))},
            "resampling": {"method": "scipy.signal.resample_poly with wrap padding, then periodic indexing",
                           "period_frames_exact_before_rounding": rational_period, "period_frames_used": period},
            "global_match": global_match, "periodicity": periodicity, "natural_loop_seams": seams,
            'independent_anchors': anchors,
            'criteria': {'global_correlation_min': .98, 'cycle_correlation_min': .995,
                         'local_correlation_min': .98, 'timing_tolerance_seconds': max(2/sr,.0001),
                         'extra_active_zero_seconds_min': .01,
                         'note': 'Numerical continuity criteria set before new engine capture. Exact repeated PCM is diagnostic, not an acceptance requirement.'},
            "all_zero_intervals_at_least_5ms": zero_runs, "one_second_windows": windows,
            "inputs_unchanged": stable, "script_sha256": sha(Path(__file__)),
            "limits": ["Engine callback WAV verification, not physical output-device or headset recording and not subjective listening.",
                       "Exact first-loop scheduling is inferred from content; search permits nonnegative head delay up to the stated limit.",
                       "The resampler is an independent approximation of engine interpolation; residual waveform differences may be codec/resampler differences.",
                       "Source quietness alone is not dropout. All-zero intervals are flagged only when source RMS exceeds whole-source RMS minus35dB and predicted capture RMS exceeds1e-7.",
                       "Loop periodicity and local offset are numerical evidence, not an automatic musical or perceptual loop verdict.",
                       "The output period is rounded to the nearest frame; noninteger source-duration/output-rate products need separate timing interpretation."]}


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def audit_runtime(result_path, timeline_path, capture_path, source_hash):
    result, timeline = read_json(result_path), read_json(timeline_path)
    info = sf.info(capture_path)
    buffers = timeline.get('buffers', [])
    issues = []
    checks = {
        'complete_natural_two_loops': bool(result.get('complete') and result.get('passed')
                                          and result.get('fullLoopCompleted') and result.get('wraps', 0) >= 2),
        'scene_restored': result.get('sceneRestored') is True,
        'locks_released': result.get('locksReleased') is True,
        'source_hash_bound': result.get('asset', {}).get('sha256') == source_hash,
        'zero_capture_error_flags': result.get('captureErrorFlags') == 0,
        'wave_metadata_matches': info.frames == timeline.get('capturedFrames') == result.get('capturedFrames')
                                 and info.samplerate == timeline.get('sampleRate') == result.get('outputSampleRate')
                                 and info.channels == timeline.get('channels'),
        'buffer_count_matches': len(buffers) == timeline.get('bufferCount') == result.get('callbackBuffers'),
    }
    offset, max_dsp_error, max_wall_overrun, previous = 0, 0., 0., None
    consistent = bool(buffers)
    for i, item in enumerate(buffers):
        consistent &= (item.get('index') == i and item.get('sampleOffset') == offset
                       and item.get('channels') == info.channels
                       and item.get('sampleCount') == item.get('frameCount', -1)*info.channels)
        offset += item.get('sampleCount', 0)
        if previous:
            expected = previous['frameCount']/info.samplerate
            max_dsp_error = max(max_dsp_error, abs(item['dspTime']-previous['dspTime']-expected))
            max_wall_overrun = max(max_wall_overrun, item['stopwatchSeconds']-previous['stopwatchSeconds']-expected)
        previous = item
    checks['buffer_frame_sequence'] = bool(consistent and offset == info.frames*info.channels)
    checks['callback_dsp_continuity'] = max_dsp_error <= max(2/info.samplerate, .0001)
    # Wall scheduling jitter alone is not PCM dropout. A sustained quarter-second
    # starvation still requires review even if queued callback samples align.
    checks['no_sustained_callback_starvation'] = max_wall_overrun <= .25
    samples = result.get('mainSamples', [])
    wraps = sum(b.get('cursor', -1) < a.get('cursor', -1) for a,b in zip(samples,samples[1:]))
    checks['two_cursor_wraps_observed'] = wraps >= 2
    issues.extend(name for name, passed in checks.items() if not passed)
    return {'checks': checks, 'issues': issues, 'cursor_wraps': wraps,
            'max_callback_dsp_error_seconds': max_dsp_error,
            'max_callback_wall_overrun_seconds': max_wall_overrun,
            'result_sha256': sha(result_path), 'timeline_sha256': sha(timeline_path)}


def verify(source_path, capture_path, result_path, timeline_path, expected_sha=None, max_head=20):
    report = audit(source_path, capture_path, max_head, expected_sha)
    runtime = audit_runtime(result_path, timeline_path, capture_path, report['source']['sha256'])
    report['runtime'] = runtime
    report['issues'] += ['runtime:'+issue for issue in runtime['issues']]
    report['status'] = ('desktop_engine_continuity_checks_passed' if not report['issues']
                        and report['status'] == 'numeric_capture_checks_passed' else 'needs_review')
    return report


def save_json(path, value):
    with Path(path).open('x', encoding='utf-8') as output:
        json.dump(value, output, indent=2, ensure_ascii=False, allow_nan=False)


def synthetic_tests(folder):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    source_path = folder/'source.wav'
    if source_path.exists():
        raise ValueError('Self-test output must be new; preserve previous reports')
    sr, out_sr, seconds = 44100, 48000, 12
    t = np.arange(sr*seconds)/sr
    rng = np.random.default_rng(20261108)
    env = .018+.025*(1+np.sin(2*np.pi*t/3))
    mono = env*(np.sin(2*np.pi*261*t+.6*np.sin(2*np.pi*t/7))+.4*np.sin(2*np.pi*523*t))
    for at, frequency in [(0.7,430),(2.9,701),(4.8,512),(7.2,920),(9.4,650),(11.1,390)]:
        u = np.maximum(0,t-at)
        mono += np.where(t >= at, .12*np.exp(-u*7)*np.sin(2*np.pi*(frequency*u+20*u*u)), 0)
    source = np.column_stack([mono, mono*.85 + .002*rng.standard_normal(len(t))])
    source[(t > 5.6)&(t < 5.95)] = 0
    sf.write(source_path, source, sr, subtype='PCM_16')
    source, _ = sf.read(source_path, always_2d=True)
    converted = resample_poly(source, 160, 147, axis=0, padtype='wrap')
    head, gain = round(.91735*out_sr), .28
    base = np.concatenate([np.zeros((head,2)), np.tile(converted,(3,1))[:2*len(converted)+2*out_sr]])*gain
    inserted_at = head+len(converted)
    insertion = np.concatenate([base[:inserted_at], np.zeros((round(.05*out_sr),2)), base[inserted_at:]])
    muted = base.copy()
    muted_at = head+round(3.1*out_sr)
    muted[muted_at:muted_at+round(.05*out_sr)] = 0
    fractional = base.copy()
    # Small per-cycle rate-converter phase variation must not be mislabeled as
    # dropped PCM merely because cycles differ at the sample level.
    freqs = np.fft.rfftfreq(len(converted))
    shifted = np.fft.irfft(np.fft.rfft(converted,axis=0)*np.exp(-2j*np.pi*freqs*.15)[:,None],n=len(converted),axis=0)
    fractional[head+len(converted):head+2*len(converted)] = shifted*gain
    results = []
    for name, data, expected in [('positive',base,True),('fractional_phase_positive',fractional,True),
                                 ('insert_50ms_negative',insertion,False),('mute_50ms_negative',muted,False)]:
        wave = folder/(name+'.wav')
        sf.write(wave, data, out_sr, subtype='FLOAT')
        stamps = []
        for index, start in enumerate(range(0,len(data),1024)):
            count=min(1024,len(data)-start)
            stamps.append(dict(index=index,sampleOffset=start*2,sampleCount=count*2,frameCount=count,
                               channels=2,dspTime=100+start/out_sr,stopwatchSeconds=start/out_sr))
        timeline = dict(sampleRate=out_sr,channels=2,bufferCount=len(stamps),capturedFrames=len(data),buffers=stamps)
        result = dict(synthetic_fixture=True,complete=True,passed=True,fullLoopCompleted=True,wraps=2,
                      sceneRestored=True,locksReleased=True,captureErrorFlags=0,capturedFrames=len(data),
                      outputSampleRate=out_sr,callbackBuffers=len(stamps),asset={'sha256':sha(source_path)},
                      mainSamples=[{'cursor':0},{'cursor':sr*seconds-1},{'cursor':1},
                                   {'cursor':sr*seconds-1},{'cursor':2}])
        result_path, timeline_path = folder/(name+'-result.json'), folder/(name+'-timeline.json')
        save_json(result_path,result); save_json(timeline_path,timeline)
        report=verify(source_path,wave,result_path,timeline_path,max_head=3)
        report['synthetic_fixture']=True
        save_json(folder/(name+'-qa.json'),report)
        passed=report['status']=='desktop_engine_continuity_checks_passed'
        results.append({'name':name,'expected_acceptance':expected,'actual_acceptance':passed,
                        'test_passed':passed==expected,'issues':report['issues'], 'notices':report['notices'],
                        'alignment_seconds':report['alignment']['offset_seconds']})
    summary={'timestamp':datetime.now().astimezone().isoformat(),'synthetic_fixtures_only':True,
             'script_sha256':sha(Path(__file__)),'all_passed':all(r['test_passed'] for r in results),'tests':results}
    save_json(folder/'validation.json',summary)
    print(json.dumps(summary,ensure_ascii=False,indent=2))
    return 0 if summary['all_passed'] else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--self-test',type=Path)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--capture", type=Path)
    parser.add_argument('--result',type=Path)
    parser.add_argument('--timeline',type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--expected-source-sha")
    parser.add_argument("--max-head-seconds", type=float, default=20)
    args = parser.parse_args()
    if args.self_test:
        return synthetic_tests(args.self_test)
    if not all((args.source,args.capture,args.result,args.timeline,args.out)):
        parser.error('--source, --capture, --result, --timeline and --out are required')
    if args.out.exists() or args.out.resolve() in (args.source.resolve(), args.capture.resolve()):
        parser.error("Output must be a new file distinct from both inputs")
    if args.max_head_seconds < 0 or not math.isfinite(args.max_head_seconds):
        parser.error("max-head-seconds must be finite and nonnegative")
    result = verify(args.source,args.capture,args.result,args.timeline,args.expected_source_sha,args.max_head_seconds)
    result["command"] = sys.argv
    with args.out.open("x", encoding="utf-8") as file:
        json.dump(result, file, ensure_ascii=False, allow_nan=False, indent=2)
    print(json.dumps({"status": result["status"], "issues": result["issues"], "alignment": result["alignment"],
                      "gain": result["fitted_signed_gain"], "periodicity": result["periodicity"],
                      'notices':result['notices']}, indent=2))
    return 0 if result['status']=='desktop_engine_continuity_checks_passed' else 2


if __name__ == "__main__":
    sys.exit(main())
