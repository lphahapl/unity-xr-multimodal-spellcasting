"""Run several isolated desktop Play cases in one editor, then record each result."""
from pathlib import Path
import argparse, subprocess, time, sys
from xr_isolation import isolate
p=argparse.ArgumentParser()
p.add_argument('cases',choices=('chapterpen','ink','stake','generic','ration'),nargs='+')
p.add_argument('--project',type=Path,default=Path('D:/Temp/NarutoVR-Ch02Supplies-1126'))
p.add_argument('--output',type=Path,default=Path('D:/Temp/SharedPickup1126'))
p.add_argument('--unity',type=Path,default=Path('D:/unity_hub/6000.3.10f1/Editor/Unity.exe'))
o=p.parse_args();o.output.mkdir(parents=True,exist_ok=True)
env=isolate(o.project)
env.update(PICKUP_QA=str(o.output.resolve()),PICKUP_CASE=','.join(o.cases))
log=o.output/'pickup-batch.log'
args=[str(o.unity),'-batchmode','-buildTarget','Win64','-force-d3d11','-job-worker-count','2',
      '-projectPath',str(o.project.resolve()),'-executeMethod','NarutoVR.SealCombat.Editor.HandPickupPlayChecks.Run','-logFile',str(log)]
started=time.time()
result=subprocess.run(args,env=env,creationflags=subprocess.CREATE_NO_WINDOW)
ok=True
for case in o.cases:
    receipt=o.output/(case+'.txt')
    passed=receipt.is_file() and receipt.stat().st_mtime>=started
    if passed:
        text=receipt.read_text('utf-8-sig')
        passed=text.rstrip().endswith('PASS END') and 'running XR displays=0' in text
    (o.output/(case+'.exit')).write_text('0' if passed else '1')
    print(case,passed,flush=True);ok=ok and passed
sys.exit(result.returncode or (0 if ok else 1))
