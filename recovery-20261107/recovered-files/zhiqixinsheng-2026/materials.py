from pathlib import Path
import json,re,html,subprocess,shutil
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,KeepTogether
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.colors import HexColor
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from content import CONTENT
from build import ROOT,OUT,PROJECTS

NOTICE='https://mp.weixin.qq.com/s/908kuVaWyV-KrgsUdrQsdQ'
FORM='https://docs.qq.com/sheet/DZXhMbEpXeE1oWWpK'
for name,file in [('Yahei','msyh.ttc'),('YaheiBold','msyhbd.ttc')]:pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+file,subfontIndex=0))
COLORS={'collab':'#cf684e','event':'#39876a','delivery':'#5b62c9'}
def words(text):return len(re.sub(r'\s+','',text))
def chinese(text):return len(re.findall(r'[\u4e00-\u9fff]',text))

def make_pdf(p,path):
    color=COLORS[p['kind']];body=CONTENT[p['kind']]
    para=ParagraphStyle('body',fontName='Yahei',fontSize=10.5,leading=18.7,firstLineIndent=21,spaceAfter=9,wordWrap='CJK',textColor=HexColor('#334158'))
    heading=ParagraphStyle('heading',fontName='YaheiBold',fontSize=12,leading=19,spaceBefore=12,spaceAfter=7,keepWithNext=True,textColor=HexColor(color),wordWrap='CJK')
    title=ParagraphStyle('title',fontName='YaheiBold',fontSize=24,leading=34,spaceAfter=9,textColor=HexColor('#18263c'),wordWrap='CJK')
    sub=ParagraphStyle('sub',fontName='Yahei',fontSize=13,leading=22,spaceAfter=10,textColor=HexColor('#536179'),wordWrap='CJK')
    small=ParagraphStyle('small',fontName='Yahei',fontSize=9,leading=15,spaceAfter=10,wordWrap='CJK',textColor=HexColor('#7e899c'))
    story=[Paragraph('项目创意说明书',small),Paragraph(p['title'],title),Paragraph(p['subtitle'],sub),Paragraph('参赛赛道：'+p['track'],small),Spacer(1,10)]
    for h,text in body:story.extend([Paragraph(h,heading),Paragraph(html.escape(text),para)])
    story.extend([Spacer(1,8),Paragraph('赛事依据：2026 年“智启新声”大连市大学生数智应用与软件创新设计大赛通知。<br/>'+NOTICE,small)])
    def page(c,doc):
        c.setTitle(p['title']+'项目创意说明书');c.setAuthor('');c.setStrokeColor(HexColor(color));c.setLineWidth(3);c.line(48,807,547,807);c.setFont('Yahei',8);c.setFillColor(HexColor('#8b96a8'));c.drawString(48,819,'2026 智启新声 / '+p['title']);c.setLineWidth(.4);c.setStrokeColor(HexColor('#e0e5ee'));c.line(48,42,547,42);c.drawString(48,28,'项目创意说明书');c.drawRightString(547,28,str(doc.page))
    SimpleDocTemplate(str(path),pagesize=(595.28,841.89),leftMargin=48,rightMargin=48,topMargin=56,bottomMargin=55,title=p['title']+'项目创意说明书',author='').build(story,onFirstPage=page,onLaterPages=page)

def editable_html(p):
    body=''.join('<section><h2>'+html.escape(h)+'</h2><p>'+html.escape(t)+'</p></section>' for h,t in CONTENT[p['kind']])
    fields=''.join('<label>'+x+'<input aria-label="'+x+'" type="text" value=""></label>' for x in ['学校','学院','项目负责人','团队成员','指导教师'])
    return '''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>'''+p['title']+'''项目创意说明书</title><style>body{margin:0;background:#eef1f6;color:#27344a;font:15px/1.95 "Microsoft YaHei",sans-serif}.toolbar{position:sticky;top:0;background:#14253a;color:#dbe5f3;padding:14px 24px;display:flex;gap:14px;align-items:center;font-size:12px;z-index:5;flex-wrap:wrap}.toolbar button{border:0;border-radius:5px;background:white;padding:8px 12px;cursor:pointer}.paper{max-width:820px;margin:30px auto;padding:55px 65px;background:white;box-shadow:0 5px 35px #23364d12}h1{font-size:33px;margin:7px 0}h2{font-size:18px;color:'''+COLORS[p['kind']]+''';margin:25px 0 10px}p{margin:0 0 12px;text-indent:2em}.subtitle{font-size:18px;color:#536179}.track{color:#7d899d;font-size:13px;margin:12px 0 25px}.identity{display:none;grid-template-columns:1fr 1fr;gap:12px;margin-bottom:25px}.identity label{font-size:12px;color:#61748d}.identity input{display:block;border:0;border-bottom:1px solid #aab4c3;border-radius:0;padding:7px 0;font:14px Microsoft YaHei;width:95%;outline:none}.source{font-size:11px;color:#8894a8;overflow-wrap:anywhere;margin-top:30px}main:focus{outline:none}@media(max-width:700px){.paper{margin:12px;padding:30px 22px}.identity{grid-template-columns:1fr}}@media print{@page{size:A4;margin:18mm}.toolbar{display:none}body{background:white;font-size:10.5pt;line-height:1.8}.paper{padding:0;margin:0;max-width:none;box-shadow:none}h1{font-size:24pt}h2{font-size:12pt;break-after:avoid}p{orphans:3;widows:3}.identity input{font-size:10.5pt}.source{font-size:8pt}}</style></head><body><div class="toolbar"><span>可编辑源稿</span><button id="toggle-identity">显示首页身份字段</button><button id="save">下载修订 HTML</button><button onclick="window.print()">打印 / 另存为 PDF</button><span id="count"></span></div><article class="paper"><div style="font-size:12px;color:#8b96a8">项目创意说明书</div><h1 contenteditable="true">'''+p['title']+'''</h1><div class="subtitle" contenteditable="true">'''+p['subtitle']+'''</div><div class="track" contenteditable="true">参赛赛道：'''+p['track']+'''</div><div class="identity" id="identity">'''+fields+'''</div><main id="body" contenteditable="true">'''+body+'''</main><div class="source">赛事依据：<a href="'''+NOTICE+'''">2026 年“智启新声”大赛通知</a></div></article><script>function count(){document.getElementById('count').textContent='正文 '+document.getElementById('body').innerText.replace(/\s/g,'').length+' 字符（含标题）'}document.getElementById('body').addEventListener('input',count);count();document.getElementById('toggle-identity').onclick=()=>{const e=document.getElementById('identity');e.style.display=e.style.display==='grid'?'none':'grid'};document.getElementById('save').onclick=()=>{document.querySelectorAll('input').forEach(e=>e.setAttribute('value',e.value));const blob=new Blob(['<!doctype html>'+document.documentElement.outerHTML],{type:'text/html;charset=utf-8'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='''+json.dumps(p['title']+'项目创意说明书-修订.html',ensure_ascii=False)+''';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)};</script></body></html>'''

INTRO={
'collab':'协作有迹围绕课程小组作业的任务交接，关联任务、成果正文、版本、复核与贡献记录，帮助小组找到当前成果及其确认依据。',
'event':'活动准点围绕校园活动的名额流转，贯通正式报名、候补、取消递补、容量调整与签到，并保留可核对的变更记录。',
'delivery':'交付有据把软件需求、范围变更、交付版本和问题复验关联到具体需求基线，帮助小型团队明确本期范围和验收依据。'
}
EXTRA={
'collab':'建议补充真实课程小组的需求来源与试用反馈；当前为合成任务样例。原型未实现真实身份认证和多人实时同步，申报时不要写成已经上线的多人平台。',
'event':'建议补充实际活动组织者反馈及明确的候补、通知规则；当前没有自动通知、二维码设备和多人并发服务，申报时按现有原型范围表述。',
'delivery':'建议补充经授权的小型项目需求样本与使用反馈；当前场景与交付证据为合成示例，不要写成真实企业委托、合同验收或客户上线业绩。'
}
HOW={
'collab':'选择演示记录人，打开任务后操作。负责人先开始任务并提交版本；指定复核人可通过或退回。成果正文支持粘贴或导入 TXT/MD。逾期按页面参照日期计算。',
'event':'选择活动后录入参与者称呼和测试编号。正式名额满后自动进入候补；取消或扩容会按顺序递补。点击“切至当前活动签到时段”演示签到，业务时钟仅用于原型演示。',
'delivery':'选择演示角色。需求方建立需求或验收，实现方启动工作、提交版本与修复。问题需需求方复验；采纳范围变更会更新基线，相关需求需要重新交付。'
}
def tests(p):
    src=(ROOT/'test.cjs').read_text(encoding='utf-8');lines=[l for l in src.splitlines() if l.startswith("test('")];kind=p['kind'];alias={'collab':'collab','event':'event','delivery':'delivery'}[kind]
    selected=[l for l in lines if l.startswith("test('shared',")][:2]+[l for l in lines if l.startswith("test('"+kind+"',")]
    selected.insert(2,"test('shared','历史顺序和错误项目类型校验',()=>{const s="+alias+".demo();s.history[1].seq=s.history[0].seq;throws(()=>"+alias+".validate(s));const t="+alias+".demo();t.kind='wrong';throws(()=>"+alias+".validate(t));});")
    prefix="const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),C=require('../程序/common.js'),"+alias+"=require('../程序/core.js');const results=[];function test(kind,name,fn){fn();results.push({kind,name,result:'通过'});console.log('PASS '+name);}function throws(fn,phrase){assert.throws(fn,phrase?new RegExp(phrase):undefined);}\n"
    suffix="\nfs.writeFileSync(path.join(__dirname,'测试结果.json'),JSON.stringify({passed:results.length,results},null,2));console.log(results.length+' tests passed');"
    (OUT/p['folder']/'测试'/'test.cjs').write_text(prefix+'\n'.join(selected)+suffix,encoding='utf-8')

def build():
    counts=[];core=json.loads((ROOT/'core-results.json').read_text(encoding='utf-8'));browser=json.loads((ROOT/'browser-results.json').read_text(encoding='utf-8'))
    for p in PROJECTS:
        root=OUT/p['folder'];kind=p['kind'];sections=CONTENT[kind];text=''.join(h+t for h,t in sections);body=''.join(t for _,t in sections)
        assert 2000<=words(text)<=3000 and chinese(body)>=2000,(kind,words(text),chinese(body))
        pdf=root/'创意说明书'/(p['title']+'项目创意说明书.pdf');make_pdf(p,pdf)
        (root/'创意说明书'/(p['title']+'项目创意说明书.md')).write_text('# '+p['title']+'\n\n'+p['subtitle']+'\n\n参赛赛道：'+p['track']+'\n\n'+'\n\n'.join('## '+h+'\n\n'+t for h,t in sections)+'\n\n赛事依据：'+NOTICE+'\n',encoding='utf-8')
        (root/'创意说明书'/(p['title']+'项目创意说明书（可编辑）.html')).write_text(editable_html(p),encoding='utf-8')
        count={'project':p['title'],'正文含小标题不含空白字符':words(text),'正文汉字数（不含小标题）':chinese(body),'正文含小标题汉字数':chinese(text)};counts.append(count)
        tests(p)
        (root/'测试'/'浏览器验证结果.json').write_text(json.dumps(next(x for x in browser if x['project']==p['title']),ensure_ascii=False,indent=2),encoding='utf-8')
        (root/'待填写'/'官方学院报名汇总表入口.url').write_text('[InternetShortcut]\nURL='+FORM+'\n',encoding='utf-8')
        reminder=f'''【{p['title']}】项目介绍与提交前提醒\n\n本文件只供内部整理使用。阅读并处理完成后，请在正式提交前删除本 TXT，再重新压缩。正式创意说明书内不重复放置这些提醒。\n\n一、项目介绍\n作品名称：{p['title']}——{p['subtitle']}\n参赛赛道：{p['track']}\n{INTRO[kind]}\n\n二、已有成果\n可运行单机网页、完整源码、合成初始备份、{count['正文含小标题不含空白字符']} 字符创意说明书正文（另有可编辑 HTML 与 Markdown）、实际运行截图、字幕 MP4 操作视频、演示导出结果、核心测试与浏览器验证、空白报名资料整理表。\n\n三、需要补充和核对\n1. 本次按要求不在正式 PDF 中放空白身份表和待填提醒。比赛明确要求首页包含学校、学院、项目负责人、团队成员和指导教师，正式提交前必须补进首页。可打开“创意说明书/{p['title']}项目创意说明书（可编辑）.html”，点击“显示首页身份字段”，填好后打印为 PDF，替换原 PDF；修改后的 HTML 可下载保存。\n2. 填写“待填写/项目报名资料待填写（整理版）.xlsx”。身份与项目字段目前全部留空。它是根据官方入口已见栏目整理的内部资料表，不能代替学院指定联系人使用的官方报名汇总表。\n3. 官方表入口为共享汇总表，已有他人信息。不要复制或打包他人的姓名、学号、电话。由学院联系人使用正式汇总表并统一核验，官方链接：{FORM}\n4. 参赛对象是大连市高校、职业院校在校学生；团队原则上不超过 5 名学生、指导教师 1 名。每名学生作为负责人限报 1 项，参与作品总数原则上不超过 2 项。三项需分别核验人员安排，不要直接照搬同一整组成员。\n5. 根据真实开发过程核对各成员的分工、资料权利及 AI 辅助使用情况。正式说明书末尾保留了实际使用 Codex 的工具与用途说明，不含内部填报嘱咐。\n6. {EXTRA[kind]}\n7. 正文按 2000–3000 字要求准备，修改后应重新核对字数；保留目标用户、问题场景、核心功能、使用流程、产品特色及预期价值。核对 PDF、源稿、视频和原型的功能表述是否一致。\n8. 项目文件夹必须按“学校-学院-指导教师-团队负责人姓名-作品名称”命名；当前因身份信息未提供，未使用最终申报名。若无指导教师，命名方式请向学院确认。\n9. 学院总压缩包命名：“学校名称-学院名称-智启新声大赛报名材料”。须包括学院完整官方 Excel 汇总表及全部项目文件夹，作品必须列入汇总表。学生、团队或指导教师个人直接提交无效。\n10. 通知拟定征集截止 2026 年 11 月 6 日，评审 11 月 7–21 日，时间调整以组委会正式通知为准。报送邮箱 dasailianluoren@163.com，应由学院指定联系人统一报送，并预留校内汇总时间。\n11. 投稿涉及团队原创性声明及赛事范围内非商业使用作品材料的同意，请团队核对通知后由学院统一处理。本次仅整理本地文件，没有代为声明、签署或发送邮件。\n\n比赛通知：{NOTICE}\n\n四、正式提交前\n补齐首页身份信息和官方表 → 核对正文与人员安排 → 按规定重命名 → 删除本 TXT → 重新生成项目压缩包 → 交学院指定联系人统一汇总。\n当前单项 ZIP 和总 ZIP 是本次材料快照，修改文件后需重新压缩，不要提交旧包。\n'''
        (root/f'提交前必读（看完请删除）-{p['title']}.txt').write_text(reminder,encoding='utf-8-sig')
        readme=f'''# {p['title']}\n\n{p['subtitle']}\n\n## 运行\n完整解压后双击“打开程序.html”，推荐 Chrome 或 Edge。保持程序目录中文件的相对位置，运行不需要安装或联网。\n\n## 操作\n{HOW[kind]}\n\n数据缓存在本浏览器。浏览器限制存储或文件路径改变时可能无法恢复，以主动导出的 JSON 备份为准。导入备份、恢复样例均会替换工作区；本次会话支持撤销上一步。\n\n## 源码\n程序/index.html 为界面，style.css 为响应式样式，common.js 为校验、记录及导出函数，core.js 为纯业务流程，app.js 为交互和本地保存。无构建依赖，全部源码可直接阅读和修改。\n\n## 测试\n程序本身不需要 Node.js。开发验证可使用 Node.js 18 或更高版本，在测试目录运行 node test.cjs。测试会生成“测试结果.json”。\n\n## 文件\n创意说明书目录：PDF、可编辑 Markdown 和 HTML。\n演示材料目录：实际运行截图、字幕 MP4（无音轨）与实际导出的演示操作结果。\n样例与备份目录：合成初始项目 JSON，可导入恢复。\n\n## 数据与使用范围\n这是单个浏览器内的流程原型。所有初始人名、业务和记录均为合成样例，演示角色不验证真实身份，尚未实现多人实时服务。\n'''
        (root/'运行与源码说明.md').write_text(readme,encoding='utf-8')
        details=f'''# {p['title']}原型功能与验证\n\n{INTRO[kind]}\n\n## 核心场景\n{HOW[kind]}\n\n## 业务状态和实现\n业务函数在更新前后校验数据，失败时不修改原始对象。操作按序号保留类型、说明、角色和业务时间。界面文本经转义显示，CSV 文本型导出包含公式起始符防护。\n\n## 验证\n'''+ '\n'.join('- '+x['name']+'：通过。' for x in core['results'] if x['kind'] in ('shared',kind))+'\n\n浏览器端已验证离线文件协议、业务流程、本地保存、JSON 往返、CSV 导出、非法导入、撤销及 390 像素宽度布局。未发起外部 HTTP/HTTPS 请求。\n\n## 辅助开发\n使用 OpenAI Codex 辅助方案、实现、测试、样例及文档和演示字幕。页面运行不调用生成式模型。\n'
        (root/'原型功能与验证.md').write_text(details,encoding='utf-8')
        print('PDF '+p['title']+' '+str(count))
    (ROOT/'word-counts.json').write_text(json.dumps(counts,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':build()
