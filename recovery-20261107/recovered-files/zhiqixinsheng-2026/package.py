from pathlib import Path
import json,hashlib,zipfile,shutil,re,sys
from build import ROOT,OUT,PROJECTS

NAME='智启新声参赛材料'
def keep(p):return not p.name.endswith(('.inspect.ndjson','.meta.json'))
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def archive(root,target,arcroot=None):
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=7) as z:
        for p in sorted(root.rglob('*')):
            if p.is_file() and keep(p):z.write(p,Path(arcroot or root.name)/p.relative_to(root))
    with zipfile.ZipFile(target) as z:
        assert z.testzip() is None
        assert not any('.inspect.ndjson' in n or '.qa' in n or 'node_modules' in n for n in z.namelist())

def prepare():
    for p in PROJECTS:
        root=OUT/p['folder'];title=p['title']
        required=['打开程序.html','程序/index.html','程序/core.js',f'创意说明书/{title}项目创意说明书.pdf',f'演示材料/{title}功能演示.mp4',f'提交前必读（看完请删除）-{title}.txt','样例与备份/合成初始项目.json','待填写/项目报名资料待填写（整理版）.xlsx','测试/测试结果.json']
        for f in required:assert (root/f).stat().st_size>0,f
        for f in [root/'创意说明书'/(title+'项目创意说明书.md')]:
            text=f.read_text(encoding='utf-8')
            assert not any(x in text for x in ['待填写','提交前','不替团队','补充成员分工'])
        manifest={f.relative_to(root).as_posix():digest(f) for f in sorted(root.rglob('*')) if f.is_file() and keep(f) and f.name!='文件校验.json'}
        (root/'文件校验.json').write_text(json.dumps({'algorithm':'SHA-256','files':manifest},ensure_ascii=False,indent=2),encoding='utf-8')
        archive(root,OUT/(title+'.zip'))
    cards=''.join(f'''<article><img src="{p['folder']}/演示材料/01-工作区全景.png" alt="{p['title']}实际运行界面"><div class="content"><small>0{i+1} / {p['en']}</small><h2>{p['title']}</h2><p>{p['subtitle']}</p><div class="track">{p['track']}</div><div class="links"><a href="{p['folder']}/打开程序.html">打开原型</a><a href="{p['folder']}/创意说明书/{p['title']}项目创意说明书.pdf">创意说明书</a><a href="{p['folder']}/演示材料/{p['title']}功能演示.mp4">演示视频</a><a href="{p['title']}.zip" download>单项压缩包</a></div></div></article>''' for i,p in enumerate(PROJECTS))
    html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>智启新声参赛材料</title><style>*{box-sizing:border-box}body{font:14px/1.8 "Microsoft YaHei",sans-serif;background:#f4f6fa;color:#213149;margin:0;padding:50px 5vw}header{max-width:1350px;margin:auto auto 30px}header small{color:#7686a3;font-size:11px;letter-spacing:3px}h1{font-size:34px;letter-spacing:1px;margin:12px 0}header p{color:#7e8b9e}.grid{max-width:1350px;margin:auto;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:23px}article{background:white;border:1px solid #dfe5ee;border-radius:11px;overflow:hidden}article img{width:100%;height:230px;object-fit:cover;object-position:top;border-bottom:1px solid #e7ecf3}.content{padding:24px}.content small{font-size:10px;letter-spacing:1.5px;color:#8d99aa}h2{font-size:25px;margin:12px 0 3px}.content p{color:#65768e;min-height:45px}.track{font-size:10px;color:#8591a4;border-top:1px solid #edf0f5;padding-top:15px}.links{display:flex;gap:8px;flex-wrap:wrap;margin-top:22px}.links a{background:#edf1f8;color:#47668e;padding:8px 10px;border-radius:5px;text-decoration:none;font-size:11px}.note{max-width:1350px;margin:26px auto;color:#8591a4;font-size:11px;display:flex;justify-content:space-between;gap:15px}.note a{color:#607eaa}@media(max-width:900px){.grid{grid-template-columns:1fr}h1{font-size:27px}body{padding:28px 19px}.note{display:block}}</style><header><small>2026 / 智启新声</small><h1>把协作过程，做成清楚的产品。</h1><p>课程协作 · 校园活动 · 软件交付</p></header><section class="grid">'''+cards+'''</section><div class="note"><span>完整解压后可直接打开原型，无需安装和联网。</span><a href="https://mp.weixin.qq.com/s/908kuVaWyV-KrgsUdrQsdQ">比赛通知</a></div></html>'''
    (OUT/'材料总览.html').write_text(html,encoding='utf-8')
    summary={'projects':[{'作品':p['title'],'目录':p['folder'],'赛道':p['track'],'单项包':p['title']+'.zip','文件数':len([x for x in (OUT/p['folder']).rglob('*') if x.is_file() and keep(x)])} for p in PROJECTS]}
    (OUT/'材料目录.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    archive(OUT,ROOT/(NAME+'.zip'),NAME)
    print(json.dumps(summary,ensure_ascii=False,indent=2));print('PREPARED '+str((ROOT/(NAME+'.zip')).stat().st_size))

def copy_desktop():
    desktop=Path('C:/Users/鲁迅/Desktop').resolve();dest=desktop/NAME;zip_dest=desktop/(NAME+'.zip')
    assert dest.resolve().parent==desktop and zip_dest.resolve().parent==desktop
    if dest.exists() or zip_dest.exists():raise RuntimeError('桌面同名材料已存在，未覆盖')
    shutil.copytree(OUT,dest,ignore=shutil.ignore_patterns('*.inspect.ndjson','*.meta.json'));shutil.copy2(ROOT/(NAME+'.zip'),zip_dest)
    for f in OUT.rglob('*'):
        if f.is_file() and keep(f):assert digest(f)==digest(dest/f.relative_to(OUT))
    assert digest(ROOT/(NAME+'.zip'))==digest(zip_dest)
    for f in list(dest.glob('*.zip'))+[zip_dest]:
        with zipfile.ZipFile(f) as z:assert z.testzip() is None
    for ref in re.findall(r'(?:href|src)="([^"]+)"',(dest/'材料总览.html').read_text(encoding='utf-8')):
        if not ref.startswith(('https:','http:','#')):assert (dest/ref).exists(),ref
    print('DESKTOP '+str(dest));print('ZIP '+str(zip_dest));print('BYTES '+str(zip_dest.stat().st_size));print('SHA256 '+digest(zip_dest))

if __name__=='__main__':
    if '--desktop' in sys.argv:copy_desktop()
    else:prepare()
