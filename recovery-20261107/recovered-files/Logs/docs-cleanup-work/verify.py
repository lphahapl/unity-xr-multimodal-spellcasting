from pathlib import Path
import hashlib
import json
import re
from urllib.parse import unquote, urlsplit

root = Path(r'D:\UnityProjects\NarutoVR').resolve()
backup = root / 'Logs/DOCS-20261101-210059'
manifest = json.loads((backup / 'manifest.json').read_text(encoding='utf-8'))
files = [root / 'README.md', root / 'AGENTS.md', root / 'GEMINI.md', *sorted((root / 'docs').rglob('*.md'))]
def body(text):
    fence = None
    for number, line in enumerate(text.splitlines(), 1):
        mark = re.match(r'^\s*(`{3,}|~{3,})', line)
        if mark:
            value = mark.group(1)
            if fence and value[0] == fence[0] and len(value) >= len(fence):
                fence = None
            elif fence is None:
                fence = value
            continue
        if fence is None:
            yield number, line

errors = []
checked = 0
stale = []
for file in files:
    text = file.read_text(encoding='utf-8-sig')
    for number, line in body(text):
        for match in re.finditer(r'!?\[[^\]\n]*\]\((<[^>]+>|[^\s)]+)(?:[^)\n]*)\)', line):
            raw = match.group(1).strip('<>')
            if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', raw) or raw.startswith('#'):
                continue
            target = (file.parent / unquote(urlsplit(raw).path)).resolve()
            checked += 1
            if not target.exists():
                errors.append({'file': str(file.relative_to(root)), 'line': number, 'target': raw})
        if '模型交火' not in file.parts:
            for old in manifest['moved'] | manifest['retired']:
                if old in line:
                    stale.append({'file': str(file.relative_to(root)), 'line': number, 'old': old})

# Imported review history is text-identical, including original newlines and relative paths.
code = (root / 'docs/模型交火/代码审阅交接.md').read_bytes()
world = (root / 'docs/模型交火/世界观变更交接.md').read_bytes()
glyph = (backup / 'backup/docs/temp/GLYPH_INTEGRATION_TODO_AND_REVIEW_ZH.md').read_bytes()
glyph_tail = glyph[glyph.index('### 追加记录'.encode()):]
assert glyph_tail in code, 'Glyph append history changed'
for old, ledger in [
    ('docs/KAO_GONG_LU_DU_TECH_CONFLICT_REVIEW_20261101_ZH.md', code),
    ('docs/KAO_GONG_LU_DU_V1_REVIEW_ZH.md', world),
    ('docs/KAO_GONG_LU_DU_V15_REVIEW_ZH.md', world),
]:
    raw = (backup / 'backup' / old).read_bytes()
    assert raw.removeprefix(b'\xef\xbb\xbf') in ledger, f'Imported review changed: {old}'
for old, new in manifest['moved'].items():
    assert not (root / old).exists(), f'Stale moved source: {old}'
    assert (root / new).exists(), f'New destination missing: {new}'
for old in manifest['retired']:
    assert not (root / old).exists(), f'Retired source still present: {old}'
table = root / manifest['xlsx']['to']
assert hashlib.sha256(table.read_bytes()).hexdigest() == manifest['xlsx']['sha256']
assert not (root / 'docs/temp').exists()
report = {'task': manifest['task'], 'markdown_files': len(files), 'local_links': checked, 'missing_links': errors, 'stale_paths': stale, 'history_imports': 'exact bytes preserved', 'reference_xlsx': 'same sha256', 'passed': not errors and not stale}
if (backup / 'ledger-prefixes.json').exists():
    prefixes = json.loads((backup / 'ledger-prefixes.json').read_text(encoding='utf-8'))
    for relative, expected in prefixes.items():
        raw = (root / relative).read_bytes()
        assert hashlib.sha256(raw[:expected['bytes']]).hexdigest() == expected['sha256'], f'Existing ledger edited: {relative}'
    report['ledger_prefixes_preserved'] = True
(backup / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
raise SystemExit(0 if report['passed'] else 1)
