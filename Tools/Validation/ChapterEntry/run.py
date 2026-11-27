"""Run against an isolated, writable project copy with the real Unity scenes and build settings."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("project", type=Path)
    parser.add_argument("unity", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("cases", nargs="*")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[3]
    project = args.project.resolve()
    out = args.output.resolve()
    cfg = project / "ProjectSettings/ProjectSettings.asset"
    assert project != repo and "  companyName: ChapterEntryValidation" in cfg.read_text(encoding="utf-8")
    fixture = repo / "Tools/Validation/Evidence/Chapter02Assembly/basket-transit-1126/B/Fixtures/hub.json"
    cs = Path(__file__).with_name("ChapterEntryPlayChecks.cs")
    checks = project / "Assets/scripts/logic/Editor/ChapterEntryQA/ChapterEntryPlayChecks.cs"
    checks.parent.mkdir(exist_ok=True)
    shutil.copy2(cs, checks)
    out.mkdir(exist_ok=True, parents=True)
    cases = args.cases or ["fresh", "legacy", "fresh-override", "travel", "redirect", "manual",
                           "training", "corrupt", "manual-missing", "seed-court", "exact-court", "unavailable", "backup-owner"]
    for case in cases:
        dest = out / case
        dest.mkdir(exist_ok=True)
        product = "ChapterEntryQA" + uuid.uuid4().hex
        text = cfg.read_text(encoding="utf-8")
        text = re.sub(r"(?m)^  productName:.*$", "  productName: " + product, text)
        cfg.write_text(text, encoding="utf-8")
        save = Path(os.environ["USERPROFILE"]) / "AppData/LocalLow/ChapterEntryValidation" / product
        save.mkdir(exist_ok=False)
        if case in ("redirect", "manual", "training", "exact-court", "unavailable", "backup-owner"):
            from_case = "seed-court" if case == "exact-court" else "legacy"
            prior = Path((out / from_case / "data-path.txt").read_text(encoding="utf-8"))
            assert prior.parent == save.parent and prior.name.startswith("ChapterEntryQA")
            for f in prior.glob("*.json"):
                shutil.copy2(f, save / f.name)
        if case == "backup-owner":
            primary = save / "chapter02-suspended-path.json"
            shutil.copy2(primary, primary.with_suffix(".json.bak"))
            wrong = json.loads(primary.read_text(encoding="utf-8-sig"))
            wrong["progress"]["scenePath"] = "Assets/Scene/Chapter01_Courtyard.unity"
            wrong["progress"]["currency"] = 441
            primary.write_text(json.dumps(wrong), encoding="utf-8")
        if case == "corrupt":
            (save / "chapter02-suspended-path.json").write_bytes(b"broken-legacy-no-backup")
        policy = {"legacy": "CurrentScene", "exact-court": "ChapterOne",
                  "corrupt": "CurrentScene"}.get(case, "LatestCampaign")
        env = dict(os.environ, ALLUSERSPROFILE="C:/ProgramData", ENTRY_QA_CASE=case,
                   ENTRY_QA_POLICY=policy, ENTRY_QA_OUTPUT=str(dest), ENTRY_QA_HUB_JSON=str(fixture))
        (dest / "checks.txt").write_text("", encoding="utf-8")
        before = {f.name: sha(f) for f in save.glob("*.json*")}
        cmd = [str(args.unity), "-batchmode", "-force-d3d11", "-job-worker-count", "3",
               "-projectPath", str(project), "-executeMethod",
               "NarutoVR.SealCombat.Editor.ChapterEntryPlayChecks.Run", "-logFile", str(dest / "unity.log")]
        build = project / "ProjectSettings/EditorBuildSettings.asset"
        build_original = build.read_bytes()
        try:
            rc = subprocess.run(cmd, env=env, timeout=450, creationflags=subprocess.CREATE_NO_WINDOW).returncode
        finally:
            if case == "unavailable":
                build.write_bytes(build_original)
        after = {f.name: sha(f) for f in save.glob("*.json*")}
        if case == "corrupt":
            assert before == after, (case, before, after)
        if case == "training":
            assert all(after.get(name) == digest for name, digest in before.items()), case
        if case == "backup-owner":
            key = "chapter02-suspended-path.json.bak"
            assert before[key] == after[key] and before[primary.name] != after[primary.name], case
        (dest / "result.json").write_text(json.dumps({"exit": rc, "before": before, "after": after}, indent=2))
        print(case, rc, flush=True)
        if rc:
            return rc
    return 0


if __name__ == "__main__":
    sys.exit(main())
