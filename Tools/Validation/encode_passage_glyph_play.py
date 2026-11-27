"""Encode camera frames and verify recorded Unity Play state. Run with capture folder, output.mp4."""
import csv
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import subprocess
import sys

import cv2
import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFont

source, movie = map(Path, sys.argv[1:3])
repo = Path(__file__).resolve().parents[2]
fps = 30
rows = list(csv.DictReader((source / "frames.csv").open(encoding="utf-8-sig")))
assert rows and rows[-1]["stage"] == "arrived", "Replay did not finish on bank B"
assert all(int(row["frame"]) == i for i, row in enumerate(rows))
assert all(int(b["unityFrame"]) > int(a["unityFrame"]) for a,b in zip(rows,rows[1:]))
first_lock = next(i for i,r in enumerate(rows) if r["firstSelected"] == "True")
assert any(r["stage"] == "preview" and r["preview"] == "True" for r in rows)
confirm = next(i for i,r in enumerate(rows) if r["stage"] == "confirm" and r["rightFist"] == "True")
joined = next(i for i,r in enumerate(rows) if r["collider"] == "True")
if "rayVisible" in rows[0]:
    assert all(r["contactVisible"] != "True" or r["rayVisible"]=="True" for r in rows)
    assert all(r["contactVisible"]=="True" for r in rows if r["stage"]=="preview")
    assert any(r["rayVisible"]=="True" and r["contactVisible"]=="False" for r in rows)
    assert all(r["rayVisible"]=="False" and r["contactVisible"]=="False" for r in rows[confirm:])
if "approachHint" in rows[0]:
    assert any(r["stage"]=="approach" and r["approachHint"]=="True" for r in rows)
    assert all(r["approachHint"]=="False" for r in rows if r["stage"] not in ("approach","socket"))
def front(row):
    return [float(row["front" + axis]) for axis in "XYZ"]

a, b = front(rows[confirm]), front(rows[joined])
assert math.dist(a,b) > 1 and rows[confirm]["completeCount"] == "1"
progress = [float(r["progress"]) for r in rows]
assert progress == sorted(progress) and len(set(progress)) > 30
assert all(r["collider"] == "False" for r in rows[:joined])
assert all(r["collider"] == "True" and p==1 for r,p in zip(rows[joined:],progress[joined:]))
assert all(p < 1 for p in progress[:joined-1])
for row,p in zip(rows[confirm:],progress[confirm:]):
    assert math.dist(front(row), [x + (y-x)*p for x,y in zip(a,b)]) < .001

captions = {
    "approach": "走近断口：两岸石座上铸有相同的通路符", "socket": "渡榫近景：镂空铜纹与地面嵌纹指示接岸位置",
    "ink": "取墨规笔，在符板上画通路符", "idle": "抬笔，等候辨符",
    "recognized": "辨符成功，右手浮现通路符纹", "first": "伸右食指，淡金流光指向甲岸",
    "aim": "保留甲岸，移向乙岸", "preview": "射线命中乙岸，落点符纹与搭路轮廓同时亮起",
    "confirm": "收指握拳，确认搭路", "forming": "淡金桥面从甲岸向乙岸延伸",
    "joined": "桥头接上乙岸，开放通行", "walk": "左食指行走，跨过新桥",
    "arrived": "玩家已抵达乙岸",
}
regular = "C:/Windows/Fonts/msyh.ttc"
bold = "C:/Windows/Fonts/msyhbd.ttc"
body = ImageFont.truetype(bold, 28)
small = ImageFont.truetype(regular, 19)
tiny = ImageFont.truetype(regular, 15)
movie.parent.mkdir(parents=True, exist_ok=True)
tmp = movie.with_suffix(".part.mp4")
cmd = [imageio_ffmpeg.get_ffmpeg_exe(), "-v", "error", "-y", "-f", "rawvideo",
       "-pix_fmt", "rgb24", "-s", "1280x810", "-r", str(fps), "-i", "-",
       "-an", "-c:v", "libx264", "-crf", "17", "-preset", "fast", "-pix_fmt",
       "yuv420p", "-movflags", "+faststart", str(tmp)]
first_overview = next(i for i,r in enumerate(rows) if r["stage"] == "first")
picture_replacements = {}
def pink_fraction(path):
    with Image.open(path) as im:
        pixels = np.asarray(im)
        return float(np.mean((pixels[:,:,0] > 190) & (pixels[:,:,1] < 60) & (pixels[:,:,2] > 175)))
def overview_pixel(world):
    # Same pinhole camera and position as Film.Snap, from the recorded player's spawn.
    floor = np.array([float(rows[0]["player"+k]) for k in "XYZ"])
    camera = floor + [-2.35, 2.85, .28]
    direction = floor + [.02,.12,3.54] - camera
    direction /= np.linalg.norm(direction)
    right = np.cross([0,1,0],direction)
    right /= np.linalg.norm(right)
    up = np.cross(direction,right)
    delta = world-camera
    scale = 360/math.tan(math.radians(53)/2)/np.dot(direction,delta)
    return round(640+scale*np.dot(right,delta)),round(360-scale*np.dot(up,delta))
with subprocess.Popen(cmd, stdin=subprocess.PIPE) as encoder:
    for i,row in enumerate(rows):
        stage = row["stage"]
        image_frame = i
        if i == first_overview and pink_fraction(source/f"{i:05}.jpg") > .003:
            assert rows[i+1]["stage"] == "first" and pink_fraction(source/f"{i+1:05}.jpg") < .003
            image_frame = i+1
            picture_replacements[i] = image_frame
        with Image.open(source / f"{image_frame:05}.jpg") as frame:
            canvas = Image.new("RGB", (1280,810), "#121a20")
            canvas.paste(frame, (0,0))
        draw = ImageDraw.Draw(canvas)
        if stage in {"first", "aim", "preview", "confirm", "forming", "joined"}:
            for landing, prefix, label, dx in ((a,"a","甲岸",36),(b,"b","乙岸",-76)):
                if prefix+"ScreenX" in row:
                    x=round(float(row[prefix+"ScreenX"])*1280)
                    y=round((1-float(row[prefix+"ScreenY"]))*720)
                else:
                    x,y = overview_pixel(landing)
                if not (20<x<1200 and 40<y<690):
                    continue
                tx,ty = x+dx,y-36
                draw.line((x,y,tx+20,ty+19),fill="#e9dab5",width=2)
                draw.rounded_rectangle((tx-3,ty-2,tx+44,ty+23),radius=3,fill="#1c292b")
                draw.text((tx,ty),label,font=small,fill="#f0e6c9")
        target = source / f"target-{image_frame:05}.jpg"
        inset = target if target.exists() else source / f"hand-{image_frame:05}.jpg"
        if inset.exists():
            with Image.open(inset) as hand:
                canvas.paste(hand.resize((256,144), Image.Resampling.LANCZOS),(1005,16))
            draw.rectangle((1004,15,1261,160),outline="#b7a580")
            draw.rectangle((1005,160,1260,185), fill="#182027")
            draw.text((1013,161),"射线落点近景" if target.exists() else "右手关节近景",font=tiny,fill="#eee4c8")
        draw.text((28,728), captions[stage],font=body,fill="#f3ead3")
        draw.text((30,774),"通路符   /   Unity Play 桌面回放",font=small,fill="#a9b8b8")
        status = f"{i/fps:05.2f} s    帧 {i:03d}"
        if i >= confirm:
            percent = ">99.9" if i < joined and progress[i] >= .9995 else f"{progress[i]*100:.1f}"
            status += f"    成路 {percent}%    碰撞" + ("开" if i >= joined else "关")
        draw.text((1252,767), status, anchor="rs",font=small,fill="#e6d5a5")
        encoder.stdin.write(canvas.tobytes())
    encoder.stdin.close()
    assert encoder.wait() == 0
video = cv2.VideoCapture(str(tmp))
assert round(video.get(cv2.CAP_PROP_FRAME_COUNT)) == len(rows)
assert abs(video.get(cv2.CAP_PROP_FPS)-fps) < .01
for index in (0, confirm, (joined+confirm)//2, joined, len(rows)-1):
    video.set(cv2.CAP_PROP_POS_FRAMES,index)
    ok, pixels=video.read()
    assert ok and pixels.shape[:2] == (810,1280)
video.release()
tmp.replace(movie)
shutil.copyfile(source/"frames.csv", movie.with_suffix(".frames.csv"))
def event(index):
    return dict(frame=index, seconds=round(index/fps,3), unityFrame=int(rows[index]["unityFrame"]),
                progress=progress[index],front=front(rows[index]),collider=rows[index]["collider"]=="True")
milestones = {stage:event(next(i for i,r in enumerate(rows) if r["stage"] == stage))
              for stage in dict.fromkeys(r["stage"] for r in rows)}
milestones["firstLock"] = event(first_lock)
milestones["fistAccepted"] = event(confirm)
for part in (0,.25,.50,.75,1):
    milestones[str(part)] = event(joined if part==1 else min(range(confirm,joined+1), key=lambda i:abs(progress[i]-part)))
sources = ["Assets/Scene/PassageGlyphLab.unity", "Assets/Art/Glyphs/Passage.asset",
           "Assets/Art/Glyphs/PassageBankSocket.prefab","Assets/Art/Glyphs/PassageBankSocket.asset","Assets/Art/Glyphs/PassageBankCrest.asset",
           "Assets/Materials/Chapter01/CourtyardStone.mat","Assets/Art/Environment/Opening/PreqinGateFittings/GateHammeredBronze.mat",
           "Assets/scripts/logic/Rpg/Glyphs/PassageGlyphAnchor.cs","Assets/scripts/logic/Rpg/Glyphs/PassageApproachHint.cs",
           "Assets/Art/Glyphs/PassageEffect.asset","Assets/Art/Glyphs/PassageGold.mat","Assets/Art/Glyphs/PassageCueGold.mat","Assets/Art/Glyphs/PassageHandSigil.mat",
           "Assets/Art/Glyphs/PassageGold.shader","Assets/scripts/logic/Rpg/Glyphs/PassageGlyphRoad.cs",
           "Assets/scripts/logic/Rpg/Glyphs/PassageGlyphEffect.cs","Assets/scripts/logic/Rpg/PlayerInputController.cs",
           "Assets/scripts/logic/Rpg/Glyphs/GlyphEffectDefinition.cs","Assets/scripts/logic/Rpg/Glyphs/GlyphPenController.cs","Tools/Validation/PassageGlyphPlayCapture.cs",
           "Tools/Validation/encode_passage_glyph_play.py"]
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
settings = (repo/"Assets/Art/Glyphs/PassageEffect.asset").read_text(encoding="utf-8")
parameters = {key:float(re.search(r"^  "+key+r": (\S+)$",settings,re.M).group(1))
              for key in ("formationSeconds","reach","maxSpan","deckWidth")}
report = dict(unity="6000.3.10f1", scene=sources[0], fps=fps, frames=len(rows),
              seconds=len(rows)/fps, size=[1280,810], fromLanding=a,toLanding=b,spanMetres=math.dist(a,b),
              experimentalParameters=parameters,
              fixture=str(source), markers=milestones, sha256=digest(movie),
              cameraFrameReplacements=picture_replacements,
              precision="Unity reports progress to 6 decimal places. The collider and stage columns record the unrounded in-game completion decision.",
              sourceSha256={p:digest(repo/p) for p in sources},
              input="Desktop Play. Pen contact replay, Meta OpenXR right-hand pose samples through the player input adapter, real PassageGlyphRoad.Update, left navigation input and physics. The capture uses no live headset input.")
movie.with_suffix(".capture.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(len(rows), "frames;", confirm, "confirm;", joined, "landed;", movie)
