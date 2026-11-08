#!/usr/bin/env python3
"""Submit bounded Unity BGM requests. A polling timeout never stops Unity."""
import argparse
import json
import os
from pathlib import Path
import re
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Logs/yicun-shanhe-continuity-v11"
REQUEST = OUTPUT / "runtime-request.json"


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None


def summary(request_id):
    result_path = OUTPUT / "runtime-runs" / request_id / "result.json"
    response_path = OUTPUT / "runtime-responses" / (request_id + ".json")
    value = read_json(result_path) or read_json(response_path)
    if not value:
        return {"requestId": request_id, "status": "pending", "complete": False,
                "resultPath": str(result_path), "responsePath": str(response_path)}
    keys = ("requestId", "status", "complete", "passed", "diagnosticPassed", "fullLoopCompleted",
            "reason", "detail", "sceneRestored", "wraps", "capturedFrames", "captureWav", "utc")
    answer = {key: value[key] for key in keys if key in value}
    answer.update(resultPath=str(result_path), responsePath=str(response_path))
    return answer


def submit(payload):
    OUTPUT.mkdir(parents=True, exist_ok=True)
    lock = OUTPUT / "runtime-submit.lock"
    temp = OUTPUT / ("runtime-submit-" + uuid.uuid4().hex + ".tmp")
    with lock.open("x", encoding="utf-8") as stream:
        stream.write(str(os.getpid()))
    try:
        if REQUEST.exists():
            raise RuntimeError("Another Unity request is pending; it was not overwritten.")
        with temp.open("x", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, ensure_ascii=False, allow_nan=False)
            stream.flush()
            os.fsync(stream.fileno())
        # A hard link publishes the complete file atomically and refuses overwrite.
        os.link(temp, REQUEST)
    finally:
        if temp.exists():
            temp.unlink()
        lock.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("status", "import", "run", "poll"))
    parser.add_argument("--id", dest="request_id")
    parser.add_argument("--sha256")
    parser.add_argument("--asset", default="Assets/SealCombat/Audio/kaogong_yicun_shanhe_ambient_loop.wav")
    parser.add_argument("--guid", default="ad629548de0b8db45a1a011d0f5dcd53")
    parser.add_argument("--duration", type=float, default=0)
    parser.add_argument("--volume", type=float, default=.28)
    parser.add_argument("--full-loops", type=int, default=2)
    parser.add_argument("--diagnostic-seconds", type=float, default=0)
    parser.add_argument("--lock-reload", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--capture", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--inject-reload-at-seconds", type=float, default=-1)
    parser.add_argument("--wait-seconds", type=float, default=10)
    args = parser.parse_args()
    if not 0 <= args.wait_seconds <= 55:
        parser.error("--wait-seconds must be between 0 and 55; poll again for unfinished work")
    request_id = args.request_id or (time.strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:8])
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,95}", request_id):
        parser.error("invalid request ID")
    if args.action == "poll" and not args.request_id:
        parser.error("poll requires --id")
    if args.action in ("import", "run") and not re.fullmatch(r"[0-9A-Fa-f]{64}", args.sha256 or ""):
        parser.error("import/run requires the expected --sha256")
    if args.action != "poll":
        existing = summary(request_id)
        if existing["status"] != "pending":
            print(json.dumps(existing, ensure_ascii=False))
            return 2 if existing.get("complete") and not (existing.get("passed") or existing.get("diagnosticPassed")) else 0
        submit(dict(action=args.action, requestId=request_id, expectedSha256=args.sha256 or "",
                    assetPath=args.asset, expectedGuid=args.guid, expectedDurationSeconds=args.duration,
                    volume=args.volume, fullLoops=args.full_loops, diagnosticSeconds=args.diagnostic_seconds,
                    lockReload=args.lock_reload, capture=args.capture, injectReloadAtSeconds=args.inject_reload_at_seconds,
                    submittedUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), submittedBy="run_bgm_unity_probe.py", submitterPid=os.getpid()))
    until = time.monotonic() + args.wait_seconds
    while True:
        value = summary(request_id)
        if value.get("complete") or time.monotonic() >= until:
            break
        time.sleep(min(.25, max(0, until - time.monotonic())))
    if not value.get("complete"):
        value["nextCommand"] = f'python -X utf8 scratch/run_bgm_unity_probe.py poll --id {request_id} --wait-seconds 30'
        value["unityStopped"] = False
    print(json.dumps(value, ensure_ascii=False, allow_nan=False))
    return 2 if value.get("complete") and not (value.get("passed") or value.get("diagnosticPassed")) else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError) as error:
        print(json.dumps({"status": "request_error", "error": str(error)}, ensure_ascii=False))
        sys.exit(2)
