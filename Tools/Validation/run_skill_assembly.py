"""Run the skill assembly acceptance suite in a disposable Unity project under TEMP.

Example: python Tools/Validation/run_skill_assembly.py --unity <Unity.exe> --clone <TEMP/copy> --baseline-ref <commit>
Use an empty destination or a previously prepared copy containing .skill-assembly-test-project.
The baseline ref supplies old Assets files that differ from the live candidate. Results go to clone/Logs.
"""
import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import uuid


REPO = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).with_name("SkillAssemblyPlayFixture.cs")
MARKER = ".skill-assembly-test-project"
CONTROLLER = "Assets/scripts/logic/Rpg/LinearRpgController.cs"
SCENE = "Assets/Scene/Chapter01_Awakening.unity"
PHASES = ("baseline", "candidate", "unauthorized", "destroy", "retry", "legacy", "progression", "chapter", "preflight")
MVC_SCENES = {"mvc-opening": "Chapter01_Awakening", "mvc-p1": "KaogongP1Trial", "mvc-training": "LinearRpgTest"}
PLAY_REPORTS = {
    "progression": ("ProgressionIntegrationChecks.RunBatch", "progression-integration-check.txt"),
    "chapter": ("ChapterFlowChecks.RunBatch", "chapter-flow-check.txt"),
    "training-chapter": ("TrainingChapterChecks.RunBatch", "training-chapter-check.txt"),
    "fight-entry": ("CombatRoomNarrationChecks.RunBatch", "combat-room-narration.txt"),
    "objective-binding": ("NarutoVR.SealCombat.Editor.ChapterObjectiveBindingChecks.RunBatch", "chapter-objective-binding.txt"),
    "checkpoint-recovery": ("NarutoVR.SealCombat.Editor.CheckpointRecoveryChecks.RunBatch", "checkpoint-recovery.txt"),
    "enemy-hit": ("NarutoVR.SealCombat.Editor.EnemyHitFeedbackChecks.RunBatch", "enemy-hit-feedback.txt"),
    "enemy-animation": ("EnemyAnimationContinuityChecks.Run", "enemy-animation/result.txt"),
}


def git(*args):
    return subprocess.check_output(("git", "-C", str(REPO), *args))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paths(raw):
    return {name.decode("utf-8") for name in raw.split(b"\0") if name}


def local_file(clone, rel):
    path = (clone / rel).resolve()
    if not path.is_relative_to(clone / "Assets"):
        raise ValueError(f"File outside disposable Assets: {path}")
    return path


def write_if_changed(path, content):
    if path.is_file() and path.read_bytes() == content:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def sync(clone, revision, baseline):
    tracked = paths(git("diff", "--no-renames", "--name-only", "-z", revision, "--", "Assets"))
    extra = paths(git("ls-files", "--others", "--exclude-standard", "-z", "--", "Assets"))
    # A reusable copy may have imported a previously untracked source before it was moved.
    registry = clone / ".skill-assembly-synced-assets.json"
    previous = set(json.loads(registry.read_text(encoding="utf-8"))) if registry.is_file() else set()
    managed = tracked | extra | previous
    registry.write_text(json.dumps(sorted(managed), ensure_ascii=False), encoding="utf-8")
    for rel in sorted(managed - {CONTROLLER}):
        destination = local_file(clone, rel)
        source = REPO / rel
        if baseline:
            result = subprocess.run(("git", "-C", str(REPO), "show", revision + ":" + rel),
                                    stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
            content = result.stdout if result.returncode == 0 else None
        else:
            content = source.read_bytes() if source.is_file() else None
        if content is None:
            # Remove only individual obsolete files in this explicitly marked isolated Assets directory.
            destination.unlink(missing_ok=True)
        else:
            write_if_changed(destination, content)
    installed = clone / "Assets/scripts/logic/Editor/SkillAssemblyPlayFixture.cs"
    write_if_changed(installed, FIXTURE.read_bytes())
    controller = local_file(clone, CONTROLLER)
    pristine = git("show", revision + ":" + CONTROLLER) if baseline else (REPO / CONTROLLER).read_bytes()
    old = b"Path.Combine(Application.persistentDataPath, saveFileName)"
    new = b'Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "SkillAssemblyPlayerSave", saveFileName)'
    if pristine.count(old) != 1:
        raise RuntimeError("Player save location changed. Update isolated path guard first.")
    write_if_changed(controller, pristine.replace(old, new))
    scene = local_file(clone, SCENE)
    scene_original = git("show", revision + ":" + SCENE) if baseline else (REPO / SCENE).read_bytes()
    if scene.read_bytes().replace(b"\r\n", b"\n") != scene_original.replace(b"\r\n", b"\n"):
        raise RuntimeError("Unexpected Chapter01 source in disposable copy")
    group = local_file(clone, "Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset")
    return dict(sourceControllerSha256=hashlib.sha256(pristine).hexdigest(), isolatedControllerSha256=digest(controller),
                sceneSha256=digest(scene), addressableGroupSha256=digest(group), fixtureSha256=digest(installed))


def prepare_clone(clone):
    temporary = Path(tempfile.gettempdir()).resolve()
    if clone==temporary or not clone.is_relative_to(temporary) or clone.is_relative_to(REPO) or REPO.is_relative_to(clone):
        raise ValueError(f"Expected an independent project under {temporary}")
    if not clone.exists():
        clone.mkdir(parents=True)
        (clone / MARKER).write_text("isolated NarutoVR validation\n", encoding="utf-8")
        for rel in ("Assets", "Packages", "ProjectSettings"):
            shutil.copytree(REPO / rel, clone / rel)
    elif not (clone / MARKER).is_file() or (clone / "Temp/UnityLockfile").exists():
        raise RuntimeError("Use a marked, idle test project")
    settings = clone / "ProjectSettings/ProjectSettings.asset"
    content = settings.read_bytes()
    name = re.search(rb"(?m)^  productName: ([^\r\n]*)", content)
    if name is None:
        raise RuntimeError("Cannot isolate the Unity player settings for this test project")
    if not name[1].startswith(b"SkillAssemblyIsolated"):
        settings.write_bytes(content[:name.start(1)] + b"SkillAssemblyIsolated" + content[name.end(1):])
    for path in (clone / "Assets/XR").rglob("*.asset"):
        data=path.read_bytes()
        if b"m_InitManagerOnStart: 1" in data:
            path.write_bytes(data.replace(b"m_InitManagerOnStart: 1",b"m_InitManagerOnStart: 0"))


def execute(unity, clone, output, phase):
    before = set((clone/"Logs/quest-native-voice-20261108").glob("build-preflight-*.json"))
    report = output/(phase+".json")
    log = output/(phase+"-unity.log")
    args = [str(unity), "-batchmode", "-nographics", "-buildTarget", "StandaloneWindows64",
            "-projectPath", str(clone), "-logFile", str(log), "-executeMethod"]
    if phase in PLAY_REPORTS:
        if phase == "enemy-animation":
            write_if_changed(clone/"Assets/scripts/logic/Editor/EnemyAnimationContinuityChecks.cs",
                             Path(__file__).with_name("EnemyAnimationContinuityChecks.cs").read_bytes())
        method, filename = PLAY_REPORTS[phase]
        play_report = clone / "Logs" / filename
        # This project is explicitly marked and idle. Only accept the newly produced report.
        play_report.unlink(missing_ok=True)
        args += [method]
    elif phase=="preflight":
        args += ["NarutoVR.SealCombat.Editor.QuestVoiceBuild.Validate", "-quit"]
    else:
        if phase in MVC_SCENES:
            write_if_changed(clone/"Assets/scripts/logic/Editor/MvcSceneAcceptance.cs",
                             Path(__file__).with_name("MvcSceneAcceptance.cs").read_bytes())
            args += ["MvcSceneAcceptance.Run"]
        else:
            args += ["SkillAssemblyPlayFixture.Run", "-resourcePhase", phase, "-resourceResult", str(report)]
    environment=os.environ.copy()
    environment["XR_RUNTIME_JSON"]=str(clone/"Temp/NO_XR_RUNTIME.json")
    if phase in MVC_SCENES:
        environment.update(MVC_SCENE=MVC_SCENES[phase], MVC_REPORT=str(report), MVC_VARIANT="candidate",
                           MVC_AUTHORED_SCENES="|".join(sorted(p.name for p in (REPO/"Assets/Scene").glob("*.unity"))))
    print("Running",phase, str(log), flush=True)
    proc=subprocess.run(args, cwd=clone, env=environment, timeout=1200,
                        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if phase=="preflight":
        produced=set((clone/"Logs/quest-native-voice-20261108").glob("build-preflight-*.json"))-before
        if len(produced)==1:
            shutil.copyfile(produced.pop(),report)
    if phase in PLAY_REPORTS:
        if play_report.is_file():
            shutil.copyfile(play_report,output/(phase+".txt"))
        ok=play_report.is_file() and play_report.read_text(encoding="utf-8-sig").rstrip().endswith("PASS END")
    else:
        ok=report.is_file() and json.loads(report.read_text(encoding="utf-8-sig")).get("passed") is True
    print(f"{phase} exit={proc.returncode} passed={ok}", flush=True)
    return {"phase":phase,"exitCode":proc.returncode,"passed":proc.returncode==0 and ok}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity",type=Path,required=True)
    parser.add_argument("--clone",type=Path,required=True)
    parser.add_argument("--baseline-ref",required=True)
    parser.add_argument("--phases",nargs="+",choices=(*PHASES, "training-chapter", "fight-entry", "objective-binding", "checkpoint-recovery", "enemy-hit", "enemy-animation", *MVC_SCENES),default=list(PHASES))
    parser.add_argument("--baseline-save",type=Path,help="Previously captured v2 sample when repeating candidate phases")
    args=parser.parse_args()
    clone=args.clone.resolve()
    prepare_clone(clone)
    revision=git("rev-parse","--verify",args.baseline_ref+"^{commit}").decode().strip()
    output=clone/"Logs"/("skill-assembly-"+datetime.now().strftime("%Y%m%d-%H%M%S")+"-"+uuid.uuid4().hex[:6])
    output.mkdir(parents=True)
    report={"project":str(clone),"revision":revision,"results":[]}
    if args.baseline_save:
        shutil.copyfile(args.baseline_save,output/"baseline-v2.json")
    previous=None
    try:
        for phase in args.phases:
            variant="baseline" if phase=="baseline" else "candidate"
            if previous!=variant:
                report[variant]=sync(clone,revision,phase=="baseline")
                previous=variant
            result=execute(args.unity.resolve(),clone,output,phase)
            report["results"].append(result)
            if not result["passed"] and phase=="baseline":
                break
    finally:
        (output/"summary.json").write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding="utf-8")
        print("RESULT",output/"summary.json",flush=True)
    return 0 if len(report["results"])==len(args.phases) and all(r["passed"] for r in report["results"]) else 1


if __name__=="__main__":
    raise SystemExit(main())
