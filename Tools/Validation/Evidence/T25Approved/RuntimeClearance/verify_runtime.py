"""用 Unity 生产角色 BakeMesh 的 87 帧顶点及同帧石根姿态，复测整身、双掌和右手长件。"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile

import numpy as np


ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / "Tools/Validation"))
from Chapter02BallastSceneClearance import run


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify(vertices, triangles, poses, out, bones):
    capture = ROOT / "Logs/CH02-carry-pose-20261125"
    stone = ROOT / "SharedWorkSpace/results/T25-A4/Model/T25-A4-stone-local.obj"
    assert sha(triangles) == sha(capture / "triangles.i32"), "生产网格的三角索引与分区对应关系已改变"
    assert sha(stone) == "8a3e80e4b81853967987ed0d18d0f66f5053198a5313aa597c7b93377297f9c3"
    geometry = np.fromfile(vertices, dtype="<f4").reshape(87, 5301, 3)
    assert np.isfinite(geometry).all()
    # 腕骨只用于选择网格区域和共同参照原点。实际求距的输入是同帧的生产蒙皮顶点和石根。
    with tempfile.TemporaryDirectory() as temp:
        scratch = Path(temp)
        for filename in ("manifest.json", "weights.csv"):
            shutil.copyfile(capture / filename, scratch / filename)
        shutil.copyfile(bones or capture / "bones.f32", scratch / "bones.f32")
        shutil.copyfile(triangles, scratch / "triangles.i32")
        shutil.copyfile(vertices, scratch / "vertices.f32")
        run(scratch, stone, poses, out)
    with (out / "frames.csv").open(encoding="utf-8-sig") as fp:
        rows = list(csv.DictReader(fp))
    assert [int(r["frame"]) for r in rows] == list(range(87))
    result = {"inputs": {p.name: sha(p) for p in (vertices, triangles, poses, stone, bones or capture / "bones.f32")}}
    for region in ("all", "left", "right", "extension"):
        gaps = [float(r[region + "_gap_m"]) * 1000 for r in rows]
        result[region] = {
            "min_mm": min(gaps), "min_frame": int(np.argmin(gaps)),
            "max_mm": max(gaps), "max_frame": int(np.argmax(gaps)),
            "hit_frames": [int(r["frame"]) for r in rows if r[region + "_hit"] == "1"],
        }
    result["containment_frames"] = [int(r["frame"]) for r in rows
                                    if "1" in (r["actor_shell_inside_stone"], r["stone_shell_inside_actor"])]
    result["right_extension_aabb_overlap_frames"] = sum(r["extension_aabb_overlap"] == "1" for r in rows)
    (out / "summary.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if result["all"]["hit_frames"] or result["containment_frames"] or result["all"]["min_mm"] <= 0:
        raise ValueError("实际蒙皮与镇石相交，见本次逐帧报告")


if __name__ == "__main__":
    arg = argparse.ArgumentParser(description=__doc__)
    for name in ("vertices", "triangles", "poses", "out"):
        arg.add_argument(name, type=Path)
    arg.add_argument("--bones", type=Path, help="与蒙皮同帧读取的运行时骨姿数据")
    verify(**vars(arg.parse_args()))
