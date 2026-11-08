from pathlib import Path, PurePosixPath
import subprocess, hashlib, json, re

root = Path(r'D:\UnityProjects\Campus-Monopoly')
def git(*args):
    return subprocess.check_output(['git', '-C', str(root), *args])
paths = git('ls-files', '-z').decode('utf-8').strip('\0').split('\0')
assert paths and all(paths)
for name in paths:
    assert PurePosixPath(name).parts[0] not in {'Library', 'Temp', 'Logs', 'UserSettings', 'ProfilerCaptures', 'Campus-Monopoly'}, name
    assert not name.endswith(('.csproj', '.sln', '.log', '.unitypackage')), name
    assert not re.search(r'(^|/)(\.env(?:\..*)?|id_rsa|id_ed25519|credentials(?:\.json)?|.*\.(?:pfx|p12|pem|key))$', name, re.I), f'Credential-like filename: {name}'
    if name.startswith('Assets/') and not name.endswith('.meta'):
        assert name + '.meta' in paths, f'Missing meta: {name}'
    for parent in PurePosixPath(name).parents:
        if str(parent).startswith('Assets/'):
            assert str(parent) + '.meta' in paths, f'Missing folder meta: {parent}'

lfscount = 0
maxblob = 0
batch = git('cat-file', '--batch') if not paths else subprocess.check_output(['git', '-C', str(root), 'cat-file', '--batch'], input=''.join(':' + name + '\n' for name in paths).encode('utf-8'))
cursor = 0
for name in paths:
    end = batch.index(b'\n', cursor)
    header = batch[cursor:end].split()
    assert header[1] == b'blob', name
    size = int(header[2])
    blob = batch[end + 1:end + 1 + size]
    cursor = end + 2 + size
    maxblob = max(maxblob, len(blob))
    assert len(blob) < 50 * 1024 * 1024, f'Large non-LFS blob: {name}'
    if blob.startswith(b'version https://git-lfs.github.com/spec/v1\n'):
        content = (root / name).read_bytes()
        assert f'oid sha256:{hashlib.sha256(content).hexdigest()}'.encode() in blob, name
        assert f'size {len(content)}'.encode() in blob, name
        lfscount += 1
    elif b'\0' not in blob:
        patterns = [rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----', rb'(?i)(?:password|access[_-]?token|api[_-]?key|client[_-]?secret)[ \t]*[=:][ \t]*["\x27]?([A-Za-z0-9_+/.-]{16,})', rb'gh[pousr]_[A-Za-z0-9]{20,}', rb'AKIA[0-9A-Z]{16}']
        for pattern in patterns:
            assert not re.search(pattern, blob), f'Potential credential in {name}'
assert lfscount == 10, f'Unexpected LFS count: {lfscount}'
assert 'Assets/Documentation/ProjectStandards.md' not in paths
assert not any(name.startswith('Docs/') for name in paths)
for name in ['README.md', 'AGENTS.md', *[n for n in paths if n.startswith('Assets/Documentation/') and n.endswith('.md')]]:
    text = (root / name).read_text(encoding='utf-8')
    assert 'Docs/Standards' not in text and 'ProjectStandards.md' not in text, name
    for link in re.findall(r'\]\(([^)]+)\)', text):
        link = link.split('#')[0]
        if link and '://' not in link:
            assert ((root / name).parent / link).resolve().exists(), (name, link)
manifest = json.loads((Path(__file__).resolve().parents[1] / 'Campus编辑器清理/清理计划.json').read_text(encoding='utf-8'))
protected = manifest['protectedFiles']
for item in protected:
    assert hashlib.sha256((root / item['path']).read_bytes()).hexdigest() == item['sha256'], item['path']
print(json.dumps({'tracked_files': len(paths), 'lfs_files': lfscount, 'max_git_blob_bytes': maxblob, 'meta_and_credential_checks': 'pass'}, ensure_ascii=False))
