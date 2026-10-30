"""Download only data from the official sherpa-onnx release, never remote scripts."""
import hashlib
import json
import pathlib
import tarfile
import urllib.request
import time
from concurrent.futures import ThreadPoolExecutor

ROOT = pathlib.Path(__file__).resolve().parent
NAME = "sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20"
URL = f"https://github.com/k2-fsa/sherpa-onnx/releases/download/kws-models/{NAME}.tar.bz2"
MAX_BYTES = 150 * 1024 * 1024
EXPECTED_SHA256 = "68447f4fbc67e70eee3a93961f36e81e98f47aef73ce7e7ca00885c6cd3616a6"


def download(url, target):
    request = urllib.request.Request(url, headers={"User-Agent": "NarutoVR-SkillVoice/1.0"})
    with urllib.request.urlopen(request, timeout=60) as response, target.open("wb") as out:
        size = 0
        while block := response.read(1024 * 1024):
            size += len(block)
            if size > MAX_BYTES:
                raise RuntimeError("Official download exceeds the 150 MiB limit")
            out.write(block)
    return size


def download_release(url, target):
    # Bounded byte ranges avoid a single stalled release CDN connection losing all progress.
    request = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "NarutoVR-SkillVoice/1.0"})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                length = int(response.headers["Content-Length"])
                resolved_url = response.geturl()
            break
        except Exception:
            if attempt == 2:
                raise
    if not 0 < length <= MAX_BYTES:
        raise RuntimeError("Unexpected release archive size")
    chunk = 2 * 1024 * 1024
    chunks = target.parent / (NAME + ".chunks")
    chunks.mkdir(exist_ok=True)
    # Resume complete, sequential chunks from an interrupted prior assembly.
    if target.exists():
        with target.open("rb") as previous:
            for start in range(0, length, chunk):
                data = previous.read(min(chunk, length - start))
                if len(data) != min(chunk, length - start):
                    break
                (chunks / str(start)).write_bytes(data)
    def part(start):
        end = min(length - 1, start + chunk - 1)
        cached = chunks / str(start)
        if cached.exists() and cached.stat().st_size == end - start + 1:
            return cached.read_bytes()
        partial = cached.with_suffix(".download")
        for attempt in range(3):
            try:
                have = partial.stat().st_size if partial.exists() else 0
                begin = start + have
                if begin > end:
                    partial.replace(cached)
                    return cached.read_bytes()
                request = urllib.request.Request(resolved_url, headers={"Range": f"bytes={begin}-{end}"})
                with urllib.request.urlopen(request, timeout=10) as response:
                    if response.status != 206 or response.headers.get("Content-Range") != f"bytes {begin}-{end}/{length}":
                        raise RuntimeError("Release server did not honor the byte range")
                    deadline = time.perf_counter() + 45
                    with partial.open("ab") as sink:
                        while have <= end - start:
                            block = response.read1(min(65536, end - start + 1 - have))
                            if not block:
                                break
                            sink.write(block)
                            have += len(block)
                            if time.perf_counter() > deadline:
                                raise TimeoutError("Slow range; progress saved for resume")
                    if have != end - start + 1:
                        raise RuntimeError("Truncated release range")
                    partial.replace(cached)
                    print(f"Downloaded bytes {start}-{end}/{length}", flush=True)
                    return cached.read_bytes()
            except Exception as error:
                print(f"Range {start} attempt {attempt + 1}: {error}", flush=True)
                if attempt == 2:
                    raise
    with ThreadPoolExecutor(max_workers=4) as pool, target.open("wb") as out:
        for content in pool.map(part, range(0, length, chunk)):
            out.write(content)
    return length


def main():
    models = ROOT / "models"
    models.mkdir(exist_ok=True)
    target = models / NAME
    archive = models / (NAME + ".tar.bz2")
    if not archive.exists():
        partial = archive.with_suffix(".part")
        print("Downloading official release:", URL, flush=True)
        download_release(URL, partial)
        partial.replace(archive)
    with archive.open("rb") as source:
        actual_hash = hashlib.file_digest(source, "sha256").hexdigest()
    if actual_hash != EXPECTED_SHA256:
        raise RuntimeError(f"Official release SHA-256 mismatch: {actual_hash}")
    with tarfile.open(archive, "r:bz2") as tar:
        members = tar.getmembers()
        if sum(m.size for m in members) > MAX_BYTES:
            raise RuntimeError("Expanded model exceeds 150 MiB limit")
        for member in members:
            destination = (models / member.name).resolve()
            if not destination.is_relative_to(models.resolve()) or member.issym() or member.islnk():
                raise RuntimeError("Unsafe path in model archive")
        tar.extractall(models, filter="data")
    license_path = models / "SHERPA_ONNX_LICENSE.txt"
    if not license_path.exists():
        download("https://raw.githubusercontent.com/k2-fsa/sherpa-onnx/v2.0.1/LICENSE", license_path)
    evidence = {
        "model": NAME, "release_url": URL,
        "archive_bytes": archive.stat().st_size,
        "archive_sha256": actual_hash,
        "sha256_note": "Matched SHA-256 displayed by the official GitHub release expanded_assets/kws-models (checked 2026-10-29). Not a signed provenance attestation.",
        "upstream": "https://github.com/k2-fsa/sherpa-onnx",
        "documentation": "https://k2-fsa.github.io/sherpa/onnx/kws/pretrained_models/index.html",
        "runtime_license": "Apache-2.0, copied verbatim into SHERPA_ONNX_LICENSE.txt",
        "model_license_note": "Original release contents retained. Repository license is not an independent model-rights warranty.",
    }
    (models / "SOURCE.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    chunks = models / (NAME + ".chunks")
    if chunks.is_dir():
        for start in range(0, archive.stat().st_size, 2 * 1024 * 1024):
            (chunks / str(start)).unlink(missing_ok=True)
            (chunks / (str(start) + ".download")).unlink(missing_ok=True)
        if not any(chunks.iterdir()):
            chunks.rmdir()
    print(json.dumps(evidence, ensure_ascii=False, indent=2))
    print("Ready:", target)


if __name__ == "__main__":
    main()
