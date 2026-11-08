from pathlib import Path
import subprocess, hashlib, json

original = Path(r'D:\UnityProjects\Campus-Monopoly')
clone = Path(__file__).resolve().parent / '远端拉取验收'
def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args]).decode('utf-8').strip()
assert git(original, 'rev-parse', 'HEAD') == git(clone, 'rev-parse', 'HEAD')
names = git(clone, 'lfs', 'ls-files', '--name-only').splitlines()
assert len(names) == 10
for name in names:
    content = (clone / name).read_bytes()
    assert not content.startswith(b'version https://git-lfs.github.com/spec/v1')
    assert hashlib.sha256(content).hexdigest() == hashlib.sha256((original / name).read_bytes()).hexdigest(), name
assert not git(clone, 'status', '--porcelain')
assert not git(original, 'status', '--porcelain')
assert len(list((clone / 'Assets/Documentation').glob('*.md'))) == 3
assert not (clone / 'Assets/Documentation/ProjectStandards.md').exists()
assert not (clone / 'Library').exists()
print(json.dumps({'commit':git(clone,'rev-parse','--short','HEAD'), 'remote_clone':'pass', 'downloaded_lfs_models':len(names), 'working_trees':'clean', 'direct_standards':3}, ensure_ascii=False))
