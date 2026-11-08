from pathlib import Path
import json
ROOT=Path(__file__).resolve().parent
SRC=ROOT/'src'
OUT=ROOT/'交付'
PROJECTS=[
 {'folder':'01-协作有迹','kind':'collab','title':'协作有迹','subtitle':'课程小组任务与贡献记录工具','track':'移动产品与用户体验','mark':'协','en':'TEAM TRACE','head':'让小组协作，有进度，也有依据。','desc':'任务、成果版本与复核记录，连成一条清楚的协作路径。','nav':[('tasks','任务看板'),('contributions','贡献记录'),('records','操作记录')],'actions':'<button id="export-csv">导出任务清单</button><button class="primary" id="new-task">新建任务</button>','note':'初始样例使用虚构成员；版本数量不等于贡献价值。'},
 {'folder':'02-活动准点','kind':'event','title':'活动准点','subtitle':'校园活动报名与候补管理工具','track':'移动产品与用户体验','mark':'活','en':'EVENT ON TIME','head':'每一个名额，都有清楚的去向。','desc':'报名、候补、递补与签到，在同一份活动名册里衔接。','nav':[('workspace','活动总览'),('roster','报名名册'),('records','变更记录')],'actions':'<button id="export-csv">导出当前名册</button><button class="primary" id="new-event">新建活动</button>','note':'初始参与者为合成样例；时间由页面演示时钟控制。'},
 {'folder':'03-交付有据','kind':'delivery','title':'交付有据','subtitle':'小型软件项目需求与验收工作台','track':'行业数字服务与软件解决方案','mark':'交','en':'DELIVERY EVIDENCE','head':'从需求说清楚，到交付验明白。','desc':'把范围变更、版本证据与问题复验，放回同一条交付流程。','nav':[('requirements','交付清单'),('changes','范围变更'),('issues','问题复验')],'actions':'<button id="export-csv">导出验收清单</button><button class="primary" id="new-requirement">新增需求</button>','note':'初始项目与记录均为合成示例；角色选择用于流程演示。'}]
def build():
    for p in PROJECTS:
        root=OUT/p['folder'];web=root/'程序';web.mkdir(parents=True,exist_ok=True)
        for name in ['创意说明书','演示材料','样例与备份','测试','待填写']:(root/name).mkdir(exist_ok=True)
        html=(SRC/'base.html').read_text(encoding='utf-8')
        values={**{k.upper():v for k,v in p.items() if isinstance(v,str)},'BODY':(SRC/(p['kind']+'-body.html')).read_text(encoding='utf-8'),'NAV':''.join('<a href="#'+id+'" class="'+('active' if i==0 else '')+'">'+label+'</a>' for i,(id,label) in enumerate(p['nav']))}
        for k,v in values.items():html=html.replace('__'+k+'__',v)
        (web/'index.html').write_text(html,encoding='utf-8')
        for source,target in [('common.js','common.js'),('style.css','style.css'),(p['kind']+'-core.js','core.js')]: (web/target).write_bytes((SRC/source).read_bytes())
        (web/'style.css').write_text((SRC/'style.css').read_text(encoding='utf-8')+'\n'+(SRC/'layout-fixes.css').read_text(encoding='utf-8'),encoding='utf-8')
        (web/'app.js').write_text('const APP_TITLE='+json.dumps(p['title'],ensure_ascii=False)+';\n'+(SRC/'ui-common.js').read_text(encoding='utf-8')+'\n'+(SRC/(p['kind']+'-ui.js')).read_text(encoding='utf-8'),encoding='utf-8')
        (root/'打开程序.html').write_text('<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=程序/index.html"><a href="程序/index.html">打开'+p['title']+'</a>',encoding='utf-8')
    (ROOT/'projects.json').write_text(json.dumps(PROJECTS,ensure_ascii=False,indent=2),encoding='utf-8')
    print(OUT)
if __name__=='__main__':build()
