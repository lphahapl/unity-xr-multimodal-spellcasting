"""从逐帧原始观测汇总地面路径速度和真实蒙皮高度。"""
import csv
import itertools
import json
import math
from pathlib import Path
from statistics import median
import sys

folder = Path(sys.argv[1])
with (folder / "frames.csv").open(encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))

def interval(numbers):
    numbers = [x for x in numbers if math.isfinite(x)]
    return [round(x, 4) for x in (min(numbers), median(numbers), max(numbers))] if numbers else None

groups = {}
for key, group in itertools.groupby(sorted(rows, key=lambda r: (r["phase"], r["enemy"])), key=lambda r: (r["phase"],r["enemy"])):
    group = list(group)
    dt_values = [float(b["time"])-float(a["time"]) for a,b in zip(group,group[1:])]
    dt = median(dt_values)
    total = float(group[-1]["time"])-float(group[0]["time"])+dt
    speeds = [float(r["realspeed"]) for r in group]
    def offsets(column, against):
        return interval(float(r[column])-float(r[against]) for r in group if r.get(column) and r.get(against))
    origin = group[0]
    displacement = [math.hypot(float(r["x"])-float(origin["x"]),float(r["z"])-float(origin["z"])) for r in group]
    groups["/".join(key)] = {
        "frames": len(group), "duration_s":round(total,3),
        "observed_path_m":round(sum(speeds)*dt,4),"speed_mean_mps":round(sum(speeds)/len(speeds),4),
        "speed_peak_mps":round(max(speeds),4),"max_from_first_frame_m":round(max(displacement),4),
        "camera_visible_frames":sum(r["bodyEnabled"]==r["frustum"]==r["clearRay"]=="True" for r in group),
        "body_top_minus_sole_m":offsets("bodyTopY","footSoleY"),
        "eye_minus_player_floor_m":offsets("playerEyeY","playerFloorY"),
        "world_y_minus_floor_m":{c:offsets(c,"floorY") for c in ("y","bodyMinY","footSoleY","headBoneY","bodyTopY","auxTopY","stoneTopY","handDrivenTopY","withoutHandDrivenTopY","capsuleMinY","capsuleTopY")},
    }
(folder/"metrics.json").write_text(json.dumps(groups,indent=2,ensure_ascii=False),encoding="utf-8")
