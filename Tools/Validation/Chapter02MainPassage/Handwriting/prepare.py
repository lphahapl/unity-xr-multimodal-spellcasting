from pathlib import Path
import shutil
import subprocess
import sys

here = Path(__file__).resolve().parent
project = Path(sys.argv[1]).resolve()
subprocess.run([sys.executable, str(here.parent / "prepare.py"), str(project)], check=True)
fixture = project / "Assets/scripts/logic/Editor/Chapter02MainPassageReplay.cs"
s = fixture.read_text(encoding="utf8")
a, b = s.index("        IEnumerable Ink("), s.index("        IEnumerable Stow(")
s = s[:a] + (here / "PenRoute.cs.txt").read_text(encoding="utf-8-sig") + s[b:]
s=s.replace("pos=look-board.transform.forward*.58f+board.transform.up*.035f+board.transform.right*.055f;fov=58;",
            "pos=look-board.transform.forward*.27f+board.transform.up*.14f-board.transform.right*.04f;fov=86;")
s=s.replace("pen.activeBoard?.PointCount??0", '(label=="square"?l.panel.board:pen.activeBoard)?.PointCount??0')
s=s.replace("新局从原入口开始，符岸与笔未开放","新局从原入口开始，符岸已布、笔尚未开放")
fixture.write_text(s, encoding="utf8")
shutil.copyfile(here / "ink.json", project / "Logs/passage-handwriting.json")
shutil.copyfile(here / "PassageHandwritingChecks.cs", project / "Assets/scripts/logic/Editor/PassageHandwritingChecks.cs")
