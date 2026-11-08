from pathlib import Path
import uuid, re, shutil

root = Path(r'D:\UnityProjects\Campus-Monopoly')
backup = Path(__file__).resolve().parent / '规范目录调整前备份'
backup.mkdir(exist_ok=False)
source = root / 'Docs/Standards'
target = root / 'Assets/Documentation'
names = ['GIT_COMMIT_CONVENTION.md', 'TEAM_RESPONSIBILITIES.md', 'UNITY_ASSET_CONVENTION.md']
for name in ['README.md', 'AGENTS.md', 'Assets/Documentation/ProjectStandards.md', 'Assets/Documentation/ProjectStandards.md.meta']:
    dest = backup / name
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / name, dest)
shutil.copytree(source, backup / 'Standards')
meta = (target / 'ProjectStandards.md.meta').read_text(encoding='utf-8')
assert len(list(source.iterdir())) == 3
for name in names:
    assert not (target / name).exists()
    content = (source / name).read_text(encoding='utf-8')
    if name == 'UNITY_ASSET_CONVENTION.md':
        content = content.replace('├── Settings/                   URP 等项目设置资产\n└── ThirdParty/                 新增第三方原始资源\nDocs/\n└── Standards/                  团队规范', '├── Settings/                   URP 等项目设置资产\n├── Documentation/              三份团队规范正文\n└── ThirdParty/                 新增第三方原始资源')
        assert 'Docs/\n└── Standards/' not in content
    (target / name).write_text(content, encoding='utf-8', newline='\n')
    (target / (name + '.meta')).write_text(re.sub(r'(?m)^guid: [a-f0-9]+$', 'guid: ' + uuid.uuid4().hex, meta), encoding='utf-8', newline='\n')
    (source / name).unlink()
for name in ['ProjectStandards.md', 'ProjectStandards.md.meta']:
    (target / name).unlink()
source.rmdir()
(root / 'Docs').rmdir()
for name in ['README.md', 'AGENTS.md']:
    text = (root / name).read_text(encoding='utf-8')
    text = text.replace('三份正式规范位于工程根目录 `Docs/Standards/`。Unity Project 窗口中可打开 `Assets/Documentation/ProjectStandards.md` 查看入口。', '三份规范正文直接放在 `Assets/Documentation/`，在 Unity Project 窗口中打开对应文件即可查看。')
    text = text.replace('Docs/Standards/', 'Assets/Documentation/')
    assert 'ProjectStandards.md' not in text
    (root / name).write_text(text, encoding='utf-8', newline='\n')
print('PASS: three actual standards in Assets/Documentation; link-only entry removed')
