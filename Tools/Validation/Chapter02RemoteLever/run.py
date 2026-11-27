"""Apply focused remote-input checks to the existing B ride regression in an isolated Unity project."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


def replace_once(source, old, new):
    if source.count(old) != 1:
        raise RuntimeError("B ride suite changed at " + old)
    return source.replace(old, new)


def main():
    proj, unity, out = (Path(x).resolve() for x in sys.argv[1:4])
    root = Path(__file__).resolve().parents[3]
    qa = Path(__file__).resolve().parent
    assert proj != root and "companyName: ChapterEntryValidation" in (proj/"ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8")
    original = root/"Tools/Validation/Evidence/Chapter02Assembly/basket-transit-1126/B"
    out.mkdir(exist_ok=True,parents=True)
    tests = proj/"Assets/scripts/logic/Editor/RemoteLeverQA"
    tests.mkdir(exist_ok=True)
    src = (original/"Editor/Chapter02BasketBChecks.cs").read_text(encoding="utf-8-sig")
    src = replace_once(src,"static class Chapter02BasketBChecks","static partial class Chapter02BasketBChecks")
    src = replace_once(src,"            if (mode == \"early\")",
                       '            loc.head.position += Vector3.up * (1.63f - (loc.head.position-loc.PlayerFloorPosition).y);\n            Physics.SyncTransforms();\n            await Awaitable.NextFrameAsync();\n            if (mode == "early")')
    src = replace_once(src,'public static void Ride() => Begin("ride");',
                       'public static void Ride() => Begin("ride");\n        public static void Near() => Begin("near");')
    src = replace_once(src,"                Pull(b);\n                Check(!b.DepartureArmed",
                       "                await Remote(b,true);\n                Pull(b);\n                Check(!b.DepartureArmed")
    src = replace_once(src,"            Pull(b);\n            var thrown",
                       '            if (mode == "near") Pull(b); else await Remote(b,false);\n            var thrown')
    src = replace_once(src,'            await Walk(loc, lip + Vector3.right * .6f, "从原篮口实际登篮",',
                       '            await ProbeBoarding(b,lip);\n            if (mode != "interrupted") await Walk(loc, lip + Vector3.right * .6f, "从原篮口实际登篮",')
    src = replace_once(src,'            saved.progress.checkpointId = "CH02_HUB";',
                       '            ProbeOwnPen(p);\n            saved.progress.checkpointId = "CH02_HUB";')
    src = replace_once(src,'            Check(opaque && !curtain.gameObject.activeSelf', 
                       '            SupportReport(b);\n            Check(opaque && !curtain.gameObject.activeSelf')
    src = replace_once(src,'            if (mode == "interrupted")',
                       '            Check(!m.TryBeginB(), "运输中拒绝再次开程");\n            if (mode == "interrupted")')
    (tests/"Chapter02BasketBChecks.cs").write_text(src,encoding="utf-8")
    shutil.copy2(original/"Editor/Chapter02BasketBInstaller.cs",tests)
    shutil.copy2(qa/"RemoteLeverPlayChecks.cs",tests)
    for fixture in (original/"Fixtures").glob("*.json"):
        shutil.copy2(fixture,out)
    sources=Path("Assets/scripts/logic/Rpg/Chapter02Transport")
    for f in (root/sources).glob("*.cs*"):
        shutil.copy2(f,proj/sources/f.name)
    ballast=Path("Assets/scripts/logic/Enemies/BallastBasket.cs")
    shutil.copy2(root/ballast,proj/ballast)
    loc=Path("Assets/scripts/logic/Locomotion/RpgLocomotionController.cs")
    shutil.copy2(root/loc,proj/loc)
    hashes={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in (proj/sources).glob("*.cs")}
    (out/"sources.json").write_text(json.dumps(hashes,indent=2))
    for mode in sys.argv[4:] or ["Ride","Early","Interrupted","Near","Cold"]:
        env=dict(os.environ,CH02_BASKET_QA_OUT=str(out),ALLUSERSPROFILE="C:/ProgramData")
        cmd=[str(unity),"-batchmode","-force-d3d11","-job-worker-count","3","-projectPath",str(proj),"-executeMethod",
             "NarutoVR.SealCombat.Editor.Chapter02BasketBChecks."+mode,"-logFile",str(out/(mode+".log"))]
        rc=subprocess.run(cmd,env=env,timeout=400,creationflags=subprocess.CREATE_NO_WINDOW).returncode
        print(mode,rc,flush=True)
        if rc:return rc
    return 0


if __name__=="__main__":
    sys.exit(main())
