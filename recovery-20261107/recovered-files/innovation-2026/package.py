from pathlib import Path
import sys,json,hashlib,shutil,zipfile,subprocess
from html import escape
from build_apps import OUT,ROOT,PROJECTS

NAME='数启新域参赛材料'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def keep(p):return not p.name.endswith('.meta.json')
def archive(directory,target,root_name=None):
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=7) as z:
        for p in sorted(directory.rglob('*')):
            if p.is_file() and keep(p):z.write(p,Path(root_name or directory.name)/p.relative_to(directory))
    with zipfile.ZipFile(target) as z:
        assert z.testzip() is None
        assert all(not n.startswith(('/', '..')) for n in z.namelist())

def prepare():
    for folder,kind,title,sub,color,en in PROJECTS:
        root=OUT/folder
        assert (root/f'提交前必读（看完请删除）-{title}.txt').exists()
        required=['打开程序.html','程序/index.html','程序/core.js','程序/common.js',f'技术材料/{title}项目书与技术报告.pdf',f'演示材料/{title}功能演示.mp4','待填写/官方报名信息汇总表（待填写）.xlsx','测试/test.cjs']
        for s in required:assert (root/s).stat().st_size>0,s
        manifest={p.relative_to(root).as_posix():sha(p) for p in sorted(root.rglob('*')) if p.is_file() and keep(p) and p.name!='文件校验.json'}
        (root/'文件校验.json').write_text(json.dumps({'algorithm':'SHA-256','files':manifest},ensure_ascii=False,indent=2),encoding='utf-8')
        archive(root,OUT/f'技术创新赛道-{title}.zip')
    cards=''.join(f'''<article><img src="{folder}/演示材料/01-工作台全景.png" alt="{title}实际运行界面"><div class="content"><span class="number">0{i+1} / TECHNICAL INNOVATION</span><h2>{title}</h2><p>{sub}</p><div class="links"><a href="{folder}/打开程序.html">打开程序</a><a href="{folder}/技术材料/{title}项目书与技术报告.pdf">查看报告</a><a href="技术创新赛道-{title}.zip" download>下载单项包</a></div><small>团队资料留空；提交前先阅读该项目的独立提醒 TXT。</small></div></article>''' for i,(folder,kind,title,sub,color,en) in enumerate(PROJECTS))
    html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>数启新域参赛材料</title><style>*{box-sizing:border-box}body{font:14px/1.8 "Microsoft YaHei",sans-serif;background:#f4f6fa;color:#182239;margin:0;padding:55px 5vw}header{max-width:1320px;margin:auto auto 32px}header small{letter-spacing:3px;color:#476aaf}h1{font-size:35px;margin:12px 0}header p{color:#6d7d94}.grid{max-width:1320px;margin:auto;display:grid;grid-template-columns:repeat(3,1fr);gap:23px}article{background:white;border:1px solid #dfe6f0;border-radius:12px;overflow:hidden}article img{width:100%;height:230px;object-fit:cover;object-position:top;border-bottom:1px solid #edf0f6}.content{padding:24px}.number{font-size:10px;letter-spacing:2px;color:#7c8da5}h2{font-size:26px;margin:12px 0 3px}.content p{color:#65748a;min-height:50px}.links{display:flex;flex-wrap:wrap;gap:10px;margin:22px 0}.links a{font-size:12px;text-decoration:none;background:#edf2fb;color:#2753a5;border-radius:5px;padding:8px 12px}.content small{font-size:11px;color:#8793a5}.note{max-width:1320px;margin:28px auto;padding:24px 28px;border:1px solid #dfe6f0;background:white;border-radius:9px;color:#607187}.note h3{margin:0 0 12px;color:#182239}.note li{margin:7px 0}.note a{color:#2753a5}@media(max-width:900px){.grid{grid-template-columns:1fr}article img{height:260px}body{padding:26px 20px}h1{font-size:28px}}</style><header><small>2026 / 数启新域 · 智创未来</small><h1>三个场景，三项可运行成果。</h1><p>账务核对 · 隐私整理 · 冷链追溯　｜　离线程序、技术报告与实际操作演示</p></header><section class="grid">'''+cards+'''</section><section class="note"><h3>材料使用</h3><ol><li>完整解压后打开本页面，或进入各项目双击“打开程序.html”。程序不需要安装和联网。</li><li>每项已配完整源码、9 页项目书与技术报告、实际运行截图、字幕 MP4、样例数据、测试与官方空白报名表。</li><li>每个项目根目录各有独立“提交前必读（看完请删除）”TXT。请补齐身份信息、核对规则后，在正式提交前删除相应提醒，再重新打包。</li><li>三个单项 ZIP 是当前材料快照。修改文件后请重新压缩，避免提交旧 ZIP。</li><li>官方表格写 1–5 名成员、1 名指导教师，与通知正文不同，请向学院确认。表格说明和原始格式已保留。</li><li>当前文件名未包含学校与队长；补齐后按“赛道+作品名+学校/机构+队长姓名”命名。赛事截止 2026 年 10 月 25 日，以学院统一安排及组委会确认邮件为准。</li></ol><p><a href="https://mp.weixin.qq.com/s/P9FRUw4xluHI-yLAiHcLoA">查看比赛通知</a></p></section></html>'''
    (OUT/'材料总览.html').write_text(html,encoding='utf-8')
    summary={'projects':[],'officialForms':'原件副本，未填写团队信息','notes':'每项目有独立提醒 TXT，正式提交前阅读处理后删除。'}
    for folder,kind,title,sub,color,en in PROJECTS:
        root=OUT/folder;summary['projects'].append({'title':title,'folder':folder,'files':len([p for p in root.rglob('*') if p.is_file() and keep(p)]),'zip':f'技术创新赛道-{title}.zip','zipSha256':sha(OUT/f'技术创新赛道-{title}.zip')})
    (OUT/'材料目录.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    archive(OUT,ROOT/f'{NAME}.zip',NAME)
    print(json.dumps(summary,ensure_ascii=False,indent=2))

def desktop():
    source=OUT.resolve();desktop=Path('C:/Users/鲁迅/Desktop').resolve();target=desktop/NAME;zip_target=desktop/f'{NAME}.zip'
    if target.exists() or zip_target.exists():raise RuntimeError('桌面同名目录或压缩包已存在，未覆盖')
    assert target.resolve().parent==desktop and zip_target.resolve().parent==desktop
    shutil.copytree(source,target,ignore=shutil.ignore_patterns('*.meta.json'))
    shutil.copy2(ROOT/f'{NAME}.zip',zip_target)
    for p in source.rglob('*'):
        if p.is_file() and keep(p):assert sha(p)==sha(target/p.relative_to(source))
    assert sha(ROOT/f'{NAME}.zip')==sha(zip_target)
    for p in target.glob('*.zip'):
        with zipfile.ZipFile(p) as z:assert z.testzip() is None
    # Check every generated local href in the review entry point.
    html=(target/'材料总览.html').read_text(encoding='utf-8')
    import re
    for s in re.findall(r'(?:href|src)="([^"]+)"',html):
        if not s.startswith(('https:','http:','#')):assert (target/s).exists(),s
    print('DESKTOP '+str(target));print('ZIP '+str(zip_target));print('BYTES '+str(zip_target.stat().st_size));print('HASH '+sha(zip_target))

if __name__=='__main__':
    if '--desktop' in sys.argv:desktop()
    else:prepare()
