"""Encode only pixels captured from Unity Play; append measured cue captions below them."""
import argparse
import bisect
import hashlib
import json
from pathlib import Path
import subprocess

from PIL import Image, ImageDraw, ImageFont
import cv2
import imageio_ffmpeg


REPO = Path(__file__).resolve().parents[2]
FONT = REPO / "Assets/SealLab/Resources/Fonts/NotoSansCJKsc-Regular.otf"
OUTPUT = REPO / "docs/visuals/20261125_dodge_glyph_play/墨规笔画符实录.mp4"
BAR_HEIGHT = 80


def labels(raw_path):
    return [
        (int(index), caption)
        for index, caption in (
            row.split("\t", 1) for row in raw_path.read_text(encoding="utf-8-sig").splitlines()
        )
    ]


def banner(width, text):
    bar = Image.new("RGB", (width, BAR_HEIGHT), "#151c22")
    draw = ImageDraw.Draw(bar)
    tag = ImageFont.truetype(str(FONT), 19)
    title = ImageFont.truetype(str(FONT), 29)
    draw.text((28, 40), "方直之矩", font=tag, anchor="lm", fill="#e8ba7d")
    draw.line((139, 22, 139, 58), fill="#53616a", width=1)
    draw.text((162, 40), text, font=title, anchor="lm", fill="#f6f6f0")
    draw.text((width - 30, 40), "Unity Play · 桌面合成手部输入", font=tag, anchor="rm", fill="#c2ccd1")
    return bar


def raw_frame(source, frame, size):
    with Image.open(source / f"{frame:05d}.jpg") as image:
        rgb = image.convert("RGB")
        if rgb.size != size:
            raise ValueError(f"Unexpected source frame dimensions at {frame}: {rgb.size}")
        return rgb


def write_review(source, evidence, size):
    picks = [
        (0, "缺口 · 修复前"),
        (evidence["pressFrame"] + 3, "真实铜扣与实体石案"),
        (evidence["pickupFrame"] + 7, "从笔鞘中取出真实墨规笔"),
        (evidence["contactFrame"], "笔锋落板"),
        (evidence["dragFrame"] - 8, "沿板切向书写"),
        (evidence["stoppedFrame"], "贴板停住"),
        (evidence["liftFrame"], "抬离板面"),
        (evidence["recognitionFrame"] + 12, "匹配反馈"),
        (evidence["frames"] - 1, "桥面和碰撞已恢复"),
    ]
    grid = Image.new("RGB", (1440, 930), "#151c22")
    draw = ImageDraw.Draw(grid)
    font = ImageFont.truetype(str(FONT), 20)
    for i, (frame, title) in enumerate(picks):
        x, y = (i % 3) * 480, (i // 3) * 310
        frame = min(max(frame, 0), evidence["frames"] - 1)
        image = raw_frame(source, frame, size).resize((480, 270), Image.Resampling.LANCZOS)
        grid.paste(image, (x, y))
        draw.text((x + 12, y + 277), f"{frame / evidence['fps']:.2f} s  {title}", font=font, fill="#f6f6f0")
    grid.save(source / "review.jpg", quality=92)


def encode(source, final, review_only):
    evidence = json.loads((source / "capture.json").read_text(encoding="utf-8-sig"))
    size = evidence["width"], evidence["height"]
    width, height = size
    count, fps = evidence["frames"], evidence["fps"]
    if (
        evidence["template"] != "glyph.square-rule"
        or evidence["completionCount"] != 1
        or evidence["bridgeWasOpen"]
        or not evidence["bridgeColliderEnabled"]
        or not evidence["originalBarrierRemoved"]
        or not evidence["fixedNibMarker"]
        or not evidence["brushBInRuntime"]
        or evidence["peakBrushDeflectionMm"] <= 1
        or evidence["stoppedBrushDeflectionMm"] >= evidence["peakBrushDeflectionMm"] * 0.25
        or evidence["finalStage"] != "CH02_REACH_HUB"
    ):
        raise ValueError(f"Capture did not prove the requested Play chain: {evidence}")
    cues = labels(source / "cues.tsv")
    starts = [row[0] for row in cues]
    if starts[0] != 0 or starts != sorted(set(starts)) or starts[-1] >= count:
        raise ValueError("Capture cues must match the actual frame sequence.")
    write_review(source, evidence, size)
    if review_only:
        print(source / "review.jpg")
        return
    banners = [banner(width, cue).tobytes() for _, cue in cues]
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    staged = source / "play-encoded.mp4"
    args = [
        ffmpeg, "-nostdin", "-y", "-v", "error", "-f", "rawvideo", "-pixel_format", "rgb24",
        "-video_size", f"{width}x{height + BAR_HEIGHT}", "-framerate", str(fps), "-i", "pipe:0",
        "-an", "-c:v", "libx264", "-preset", "fast", "-crf", "17", "-pix_fmt", "yuv420p",
        "-movflags", "+faststart", str(staged),
    ]
    with subprocess.Popen(args, stdin=subprocess.PIPE) as process:
        for i in range(count):
            process.stdin.write(raw_frame(source, i, size).tobytes())
            process.stdin.write(banners[bisect.bisect_right(starts, i) - 1])
        process.stdin.close()
        if process.wait() != 0:
            raise RuntimeError("FFmpeg encoding failed.")
    subprocess.run([ffmpeg, "-v", "error", "-xerror", "-i", str(staged), "-f", "null", "-"], check=True)
    video = cv2.VideoCapture(str(staged))
    try:
        readback = {
            "frames": round(video.get(cv2.CAP_PROP_FRAME_COUNT)),
            "fps": video.get(cv2.CAP_PROP_FPS),
            "width": round(video.get(cv2.CAP_PROP_FRAME_WIDTH)),
            "height": round(video.get(cv2.CAP_PROP_FRAME_HEIGHT)),
        }
        if readback != {"frames": count, "fps": fps, "width": width, "height": height + BAR_HEIGHT}:
            raise ValueError(f"Unexpected encoded video geometry: {readback}")
        for frame_index in (0, evidence["contactFrame"], evidence["dragFrame"] - 7, count - 1):
            video.set(cv2.CAP_PROP_POS_FRAMES, frame_index)
            success, frame = video.read()
            if not success or frame[:height].std() < 5:
                raise ValueError(f"Missing or blank actual Play picture at frame {frame_index}.")
    finally:
        video.release()
    final.parent.mkdir(parents=True, exist_ok=True)
    staged.replace(final)
    evidence["mp4"] = {
        **readback, "seconds": count / fps, "byteLength": final.stat().st_size,
        "sha256": hashlib.sha256(final.read_bytes()).hexdigest(),
        "fullDecodePassed": True, "audioCaptured": False, "cueFrames": cues,
    }
    final.with_suffix(".capture.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(evidence["mp4"], ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("frames", type=Path)
    parser.add_argument("--out", type=Path, default=OUTPUT)
    parser.add_argument("--review-only", action="store_true")
    options = parser.parse_args()
    encode(options.frames, options.out, options.review_only)
