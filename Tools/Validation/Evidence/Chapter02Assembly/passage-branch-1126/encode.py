"""Encode the chapter's Unity frames. No synthetic geometry or replacement pictures."""
import csv
import json
import subprocess
import sys
from pathlib import Path

import cv2
import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

folder, output = map(Path, sys.argv[1:3])
rows = list(csv.DictReader((folder / "frames.csv").open(encoding="utf-8-sig")))
assert (folder / "checks.txt").read_text(encoding="utf-8").rstrip().endswith("PASS END")
captions = {
    "entrance":"二章新局 · 断桥尚未修复", "approach":"左食指向前 · 从石门走到画符石案",
    "square":"石案教学 · 取墨规笔画方直之矩", "mainBridge":"修复断桥 · 沿原桥面步行",
    "branch":"进入祭坛前 · 沿北侧崖沿寻找铜榫", "bank":"看到渡榫 · 可用通路符连接两岸",
    "weight":"千钧符 · 用原工具带墨规笔在主动板书写", "ink":"通路符 · 门框一笔、框内短横一笔",
    "cancel":"选岸中放低空手 · 取消本次搭路", "first":"松笔归槽 · 伸右食指锁定甲岸", "alternate":"第二落点 · 指向丙岸预览",
    "preview":"更换对岸 · 改选乙岸", "fist":"右手握拳 · 确认甲岸到乙岸",
    "grow":"淡金通路由近到远铺出", "joined":"前沿抵达乙岸 · 桥面可通行",
    "cross":"左食指向前 · 走过刚搭好的通路", "arrived":"已抵达乙岸 · 祭坛主线阶段保持"
}
first = next(i for i,r in enumerate(rows) if r["shot"]=="grow")
end = next(i for i,r in enumerate(rows) if r["shot"]=="joined")
assert end-first>=30 and all(r["collider"]=="False" for r in rows[first:end])
assert all(r["collider"]=="True" for r in rows[end:]) and rows[-1]["shot"]=="arrived"
output.parent.mkdir(parents=True, exist_ok=True)
font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
small=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",16)
cmd=[imageio_ffmpeg.get_ffmpeg_exe(),"-v","error","-y","-f","rawvideo","-pix_fmt","rgb24","-s","960x622","-r","30","-i","-","-c:v","libx264","-crf","17","-preset","fast","-pix_fmt","yuv420p","-movflags","+faststart",str(output)]
with subprocess.Popen(cmd, stdin=subprocess.PIPE) as enc:
    for i,r in enumerate(rows):
        with Image.open(folder/f"{i:05}.jpg") as im:
            page=Image.new("RGB",(960,622),"#1c2325");page.paste(im,(0,0))
        draw=ImageDraw.Draw(page)
        draw.text((18,549),captions[r["shot"]],font=font,fill="#eee7d4")
        draw.text((19,588),"Unity Play · 二章副本 / 桌面关节回放",font=small,fill="#c0c8c3")
        if i>=first:
            status=f'桥面 {float(r["progress"])*100:.0f}%    碰撞'+("开" if r["collider"]=="True" else "关")
            draw.text((935,588),status,font=small,fill="#efddaa",anchor="ra")
        enc.stdin.write(page.tobytes())
    enc.stdin.close()
    if enc.wait()!=0:raise RuntimeError("ffmpeg")
vid=cv2.VideoCapture(str(output))
assert vid.isOpened() and int(vid.get(cv2.CAP_PROP_FRAME_COUNT))==len(rows)
for n in [0,first,(first+end)//2,end,len(rows)-1]:
    vid.set(cv2.CAP_PROP_POS_FRAMES,n)
    ok,frame=vid.read()
    assert ok and frame.shape[:2]==(622,960)
vid.release()
print(json.dumps({"frames":len(rows),"seconds":len(rows)/30,"growthFrames":end-first,"firstGrowthFrame":first,"firstJoinedShot":end}))
