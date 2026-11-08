from pathlib import Path
from datetime import datetime, timezone, timedelta
import hashlib
import json

root = Path(r'D:\UnityProjects\NarutoVR')
backup = root / 'Logs/DOCS-20261101-210059'
manifest = json.loads((backup / 'manifest.json').read_text(encoding='utf-8'))
report = json.loads((backup / 'verification.json').read_text(encoding='utf-8'))
assert report['passed']
task = manifest['task']
now = datetime.now(timezone(timedelta(hours=8))).isoformat(timespec='seconds')
model = manifest['model']
ledgers = ['docs/模型交火/需求变更交接.md', 'docs/模型交火/代码审阅交接.md', 'docs/模型交火/世界观变更交接.md']
prefixes = {}
for relative in ledgers:
    data = (root / relative).read_bytes()
    assert hashlib.sha256(data).hexdigest() == manifest['afterSha256'][relative], f'Ledger changed since creation: {relative}'
    prefixes[relative] = {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
(backup / 'ledger-prefixes.json').write_text(json.dumps(prefixes, ensure_ascii=False, indent=2), encoding='utf-8')

req = f'''
## {task}-VERIFY｜整理完成与复查补充

- 任务 ID：{task}；模型：{model}；执行入口：Codex；时间：{now}。
- 来源与状态：本任务最后复查；已完成文档验证。本条补充初始清单，不改写上一条记录。

### 后续具体文件变化

| 文件 | 初次整理之后的补充修改 |
|---|---|
| `README.md` | 将 531 项及 Play 通过描述限定为 2026-11-01 既有报告，添加验证说明链接，明确本次整理未重跑。 |
| `docs/README.md` | 增加一次性文档/表格生成器的现有旧输出路径说明；复用须核对内置历史正文与现行目录。 |
| `docs/architecture/LINEAR_RPG_DESIGN_ZH.md` | 复查阶段 A 与验收表，清除残留四向选择/手轨迹瞄准，替换为战斗选点、胸前后撤、探索行走与头向火球；旧首次检查数量改为历史说明；修正配置入口。 |
| `docs/guides/SEAL_LAB_QUICKSTART_ZH.md` | 开头状态说明去掉已删除的“下文 Meta Link 步骤”，保持当前 Steam Link 与录制范围一致。 |
| `docs/guides/CONFIGURATION_GUIDE_ZH.md` | 修正项目 README 链接为 `../../README.md`，与文档索引的 `../README.md` 区分。 |
| `docs/guides/SKILL_SHOWCASES_ZH.md` | 同上，项目 README 链接指回仓库展示入口。 |
| `docs/guides/SKILL_VOICE_SETUP_ZH.md` | 同上，项目 README 链接指回仓库展示入口。 |
| `docs/architecture/SKILL_FRAMEWORK_AND_INTERACTION_ZH.md` | 同上，保留开发索引链接指向 docs 总索引。 |
| `docs/模型交火/需求变更交接.md` | 仅追加本次最终验收、上述逐文件补充和局限。 |
| `docs/模型交火/代码审阅交接.md` | 仅追加同任务的整理复核结果，不回改导入审阅。 |

### 实际验证与证据

- 命令：`python -X utf8 Logs/docs-cleanup-work/verify.py`。结果：{report['markdown_files']} 份项目 Markdown、{report['local_links']} 个本地文件链接检查通过；缺失链接 0，现行文档中旧仓库路径引用 0。只检查本地链接目标存在，未联网验证外部网址，也不把本地证据日志作为仓库文件。
- 四份历史文本（画符原追加区、技术冲突审阅、V1/V1.5 设定审阅）已与清理前备份逐字节比对，全部保留；三个账本初始化前缀另存哈希，最后追加后再次核对前缀。
- 34 份 Markdown 迁移到分类目录；21 份旧文件删除；新增 9 份 Markdown（含两个模型入口）。最终共 44 份 Markdown。`docs/temp` 已移空并删除，参考表迁入 reference 后 SHA-256 不变。
- 证据：`Logs/{task}/verification.json`、`manifest.json`、`ledger-prefixes.json` 和 `final-state.json`；清理前备份位于 `Logs/{task}/backup/`。迁移清单包含原文件哈希，最终状态单独保存，不修改初始清单。
- 业务代码、Unity 资产和既有 DOCX/图片：本次没有写入。`scratch/build_full_lore_docs.py`、`scratch/build_revised_world_docs.py`、`Tools/make_seal_excel.py` 的旧路径是现有生成稿行为，本次未改；下一次复用时先维护其路径/正文，不能据其存在宣称已与新目录同步。
- 未运行：Unity、头显和语音模型；本次为文档整理，不产生新的玩法通过结论。现行世界观/专业 Word 需求的语义一致性和原技术冲突问题仍需对应任务逐条裁决。
'''
code = f'''
## {task}-CODE-VERIFY｜文档整理验收

- 任务 ID：{task}；模型：{model}；执行入口：Codex；时间：{now}。
- 审查范围与状态：文档目录、引用和迁移内容；已验证，没有修改游戏代码、场景或配置资产。
- 文档变化：最终 8 份普通文档的复查修改及本次两个账本追加，逐项见[需求变更交接](需求变更交接.md)中的 `{task}-VERIFY`；初始完整迁移/删除清单也在同任务记录中。
- 检查：`python -X utf8 Logs/docs-cleanup-work/verify.py`，44 份 Markdown、270 个本地链接，无缺失与现行旧路径残留；历史原文及参考表内容比对通过。不是新的 Unity/真机验收。
- 保留问题：原技术冲突审阅未在本任务自动关闭；旧一次性生成器尚用旧文档路径，后续复用需调整，已记录在 docs 索引和需求交接。
- 交接规则：本条和前条均使用末尾追加；导入的历史正文与已有记录完整保留。
'''
for relative, value in [(ledgers[0], req), (ledgers[1], code)]:
    target = root / relative
    # Re-read current tail immediately before append; reject unowned concurrent changes.
    current = target.read_bytes()
    expected = prefixes[relative]
    assert len(current) == expected['bytes'] and hashlib.sha256(current).hexdigest() == expected['sha256']
    with target.open('a', encoding='utf-8', newline='\n') as stream:
        stream.write(value)
for relative, expected in prefixes.items():
    data = (root / relative).read_bytes()
    assert hashlib.sha256(data[:expected['bytes']]).hexdigest() == expected['sha256']
files = [root/'README.md', root/'AGENTS.md', root/'GEMINI.md', *sorted((root/'docs').rglob('*.md'))]
final = {'task': task, 'time': now, 'ledgerPrefixesPreserved': True, 'sha256': {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}}
(backup / 'final-state.json').write_text(json.dumps(final, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'task': task, 'appended': ledgers[:2], 'ledgerPrefixesPreserved': True}, ensure_ascii=False))
