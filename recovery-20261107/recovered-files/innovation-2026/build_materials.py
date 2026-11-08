from pathlib import Path
import json,shutil,hashlib,re,sys
from html import escape
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph,Table,TableStyle
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from PIL import Image
from build_apps import PROJECTS,OUT,ROOT

for name,file in [('Yahei','msyh.ttc'),('YaheiBold','msyhbd.ttc')]:pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+file,subfontIndex=0))
SOURCE='https://mp.weixin.qq.com/s/P9FRUw4xluHI-yLAiHcLoA'
FORM=next(Path('C:/Users/鲁迅/Documents/xwechat_files/wxid_14sevnc0jq5q22_1bc8/msg/file/2026-10').glob('*数启新域*.xlsx'))

INFO={
'ledger':{
'intro':'账有据面向使用表格管理订单的中小商家，提供订单与收款流水的关联核对。系统在本地读取 CSV，以分为单位计算净应收和实收差额，识别未收款、少收、多收、重复订单、重复流水及孤立流水。每项异常均可打开原始记录，导出结果和复核档案，支持从发现差异到人工复核的连续工作流程。',
'direction':'数据处理与存储服务创新 / 软件开发与应用创新',
'scenario':'一家同时经营线上商城、门店零售和社区团购的小商家，在业务系统中导出订单，在收款渠道中导出流水。退款、分次收款和重复导出会使逐行比较失效。本项目围绕“导出后核对”这一环节设计，不依赖特定支付平台接口。场景为需求假设，尚未开展真实商家访谈。',
'needs':[['经营者','查看同一业务期间的应收、实收和待核对项','汇总范围和排除项清楚可见'],['对账人员','按异常类型筛选并查看原始记录','订单号可定位到输入记录序号'],['复核人员','带着原始依据处理重复或孤立流水','异常档案可导出并线下保管']],
'workflow':'导入订单表与收款流水 → 必填字段检查 → 订单号分组 → 分币计算 → 数据质量隔离 → 异常筛选 → 原始记录溯源 → 导出 CSV / JSON。',
'modules':[['common.js','CSV 状态机、金额转分、编码解码、转义与下载'],['core.js','订单与流水分组、重复标记、金额核算与状态判定'],['app.js','导入状态、失效控制、筛选、溯源弹窗和导出'],['demo.js','合成样例，含可复现的正常与异常记录']],
'algo':[
('金额以整数分参与运算','金额字段仅接受非负十进制数字，整数最多八位，小数最多两位。将整数部分乘 100，再补齐两位小数，避免直接用二进制浮点金额相减。每文件最多 20000 行，在输入上限内汇总分值处于 JavaScript 安全整数范围。'),
('区分业务差异与数据质量','先统计订单号和流水号。重复订单、重复或缺失流水号、无效金额、找不到订单的流水，整项转为需复核，不参与应收与实收汇总。该策略避免把存在歧义的数据按零差额展示为正常。空订单号记录进入独立问题清单。'),
('支持拆分收款与净额比较','同一订单可以对应多条不同流水。净应收为订单金额减退款金额；实收字段采用已扣除退款后的净收款。差额为实收减净应收，等于零为正常，小于零为少收，大于零为多收，无流水且有差额为未收款。'),
('保持核查依据','结果对象保留订单原始行、流水原始行与 CSV 记录序号。导出复核档案包含这些记录与问题清单；结果 CSV 只输出核对所需字段。显示层进行 HTML 转义，文本型 CSV 导出增加公式起始符防护。')],
'pseudo':'订单表和流水表分别按订单号建立 Map\n统计流水号出现次数\n对每个订单组：\n  如果有重复 / 缺失 / 无效数据：状态 = 需复核\n  否则：净应收 = 金额分值 - 退款分值\n       实收 = 不同流水的金额分值合计\n       按实收 - 净应收确定状态\n汇总只采用没有数据歧义的相同订单集合',
'schema':[['订单表','订单号','必填文本，保留前导零'],['订单表','订单金额、退款金额','元；退款不得高于订单金额；空退款按 0'],['订单表','渠道','必有此列，可填写渠道来源'],['流水表','订单号、流水号','必填文本；不同收款使用不同流水号'],['流水表','实收金额','元；已扣除退款后的净收款']],
'case':'内置 25 行订单记录和 24 行流水，合并为 25 个待核对项。实际结果为正常 17 项、未收款 2 项、少收 2 项、多收 1 项、需复核 3 项。参与核算净应收为 5146.25 元，实收为 4645.75 元，差额为 -500.50 元；需复核项已从两侧金额同时排除。',
'pilot':'先与一家商家确认两张表的字段和退款口径，使用经授权、脱敏的历史数据进行平行核对；第二轮由对账人员逐项确认异常；确认规则稳定后，作为日常导出后的辅助工具使用。验收依据为同一输入反复计算一致、每项差异能回到原始记录、金额合计与独立手算一致。',
'value':'部署为静态文件即可使用，不要求采购服务器或连接支付账号。后续服务可围绕字段适配、规则配置、维护培训展开。成本主要来自需求确认与规则维护；当前没有客户付费、节省工时或收入数据，不据此估算商业回报。',
'limits':'目前只支持单币种、同一业务期间的 CSV 核对；不连接银行和支付平台，不处理手续费、跨币种结算或自动冲正。没有金额容差配置。结果供人工核对，不自动改变账目或发起支付。下一阶段应增加字段映射、账期过滤和经授权的实际业务验证。',
'features':'整数分币计算；业务差异与数据质量分层；原始记录双向溯源；离线使用。',
'video':'导入演示订单与流水 → 开始对账 → 筛选少收 → 筛选需复核 → 查看原始记录 → 导出复核档案。',
'more':'核实净实收与退款的业务口径；如需证明实际应用价值，可补充经授权的商家试用反馈，当前材料没有虚构试用单位或效益数据。'
},
'privacy':{
'intro':'隐数面向分享日志、测试材料和业务文本前的隐私整理需求，提供在浏览器本地完成的敏感片段识别和替换。工具支持手机号、邮箱、身份证候选、银行卡候选及 IPv4 地址，提供局部隐藏、完全隐藏和一致化代号三种方式。处理明细记录命中行号、信息类型和替换结果，导出的处理记录不包含原始敏感值。',
'direction':'网络与信息安全技术 / 数据隐私保护方案',
'scenario':'技术人员经常需要将日志交给同事排错，或将业务样例用于演示。直接删除全部文本会损失上下文，逐项查找又容易漏掉重复号码。本项目把规则、替换和处理记录放在同一工作区，帮助操作者在共享前完成一次明确可查的整理。场景来自常见协作流程，未使用真实个人资料。',
'needs':[['材料整理者','识别文本中常见的敏感片段','可选择信息类型及替换模式'],['技术协作者','保留重复实体之间的关联','同次处理相同原值对应相同代号'],['复核者','检查处理范围和命中依据','记录含行号与规则，避免再暴露原值']],
'workflow':'粘贴或导入文本 → 选择检测规则 → 选择替换方式 → 检查并脱敏 → 对照上下文复核 → 导出文本与处理记录。修改原文、规则或模式后，旧结果立即失效。',
'modules':[['common.js','文件读取、编码回退、文本下载与显示转义'],['core.js','候选识别、校验、区间去重、行号计算与替换'],['app.js','输入工作区、规则选择、结果失效与审计导出'],['demo.js','虚构文本与标准测试号码，无真实个人资料']],
'algo':[
('按规则识别并补充校验','手机号使用大陆 11 位格式；邮箱采用常见文本格式；IPv4 检查四段范围。18 位身份证候选补充出生日期与校验位检查，即使校验失败也保留为需隐藏候选。16 至 19 位银行卡候选通过 Luhn 校验后纳入处理。校验通过不代表身份或卡号真实有效。'),
('对重叠候选做区间消歧','按身份证、手机号、邮箱、银行卡、IP 的优先顺序收集候选，使用已接纳区间列表的二分查找排除重叠片段。之后按起始位置排序，单次扫描原文换行位置，附加命中行号。避免在每一条命中上重新切分整段原文。'),
('三种替换方式共享命中结果','局部隐藏保留部分字符；完全隐藏替换整个命中片段；一致化代号在单次处理内维护类型与原值到代号的映射。同一值多次出现保持同一代号，映射只保留在当前处理内存中，不导出原值映射。'),
('限制原文进入非必要输出','预览通过 textContent 显示，避免将输入当作 HTML 执行。审计条目仅保留类型、行号、替换结果和校验说明。页面没有网络请求、上传接口、localStorage 或 IndexedDB 写入。局部隐藏仍可能被关联识别，因此保留导出前的人工复核环节。')],
'pseudo':'按所选信息类型扫描候选\n逐类校验格式或校验位，排除重叠区间\n按起始偏移排序，并一次扫描确定原文行号\n对每个候选生成局部隐藏 / 完全隐藏 / 一致代号\n拼接未命中原文片段与替换文本\n输出脱敏结果及不含原值的处理记录',
'schema':[['输入','TXT / LOG / CSV 文本','UTF-8 或 GB18030，单文件最大 10 MB'],['处理上限','文本长度','最多 200 万字符'],['可选规则','5 种类型','可单独开启与关闭'],['脱敏文本','TXT','保留未命中的上下文文本'],['处理记录','JSON','模式、规则、命中数、行号、替换结果']],
'case':'合成文本产生 8 个命中片段，覆盖 5 种类型和 7 行。两个身份证候选中，一个通过校验，另一个校验失败，两者都被隐藏。同一手机号在联系信息和回访记录中重复出现；选择一致化代号后，两处均变为 PHONE_001。处理记录经自动测试检查，不含测试原始手机号。',
'pilot':'先选取不含真实隐私的日志样例，由复核者标注目标片段，比较遗漏与误报；再在授权范围内使用脱敏样例测试本地操作。验收应同时记录查全与误报情况，不能只统计命中总数。正式应用前，应按材料类型补充规则并完成上下文人工复核。',
'value':'适合小型团队、实训环境与资料整理人员，静态文件可在隔离环境中使用。维护重点是规则解释、样例覆盖及发布前复核培训。当前没有第三方安全测评或生产部署，商业价值是可验证的工作流程提案，未声称达到特定法规合规或匿名化认证。',
'limits':'不识别人名、住址、图片文字或未列出的证件类型；不能处理加密文件和二进制文档。局部隐藏可能保留可关联信息。邮箱规则、手机号范围等不能覆盖所有真实情况；数字校验不能证明号码真实性。CSV 按文本处理并导出 TXT，不保证可直接回填原业务表。',
'features':'规则与校验分离；错误身份证候选仍隐藏；同值代号一致；审计不携带原值；本地内存处理。',
'video':'导入合成文本 → 局部隐藏 → 一致化代号 → 完全隐藏 → 核查命中明细 → 导出处理记录。',
'more':'核实待共享资料的具体类型与检测范围；如补充真实样例或效果评估，须使用有授权的材料，并记录漏检与误报，不要把命中数写成识别准确率。'
},
'cold':{
'intro':'冻链有数面向冻品运输记录复核，支持导入批次、设备、时间和温度 CSV，按批次设备展示曲线、连续越限事件与采样缺口。系统通过前值保持法计算有观测支持的越限时长，将大采样间隔和末点时长未知单独处理，并导出包含阈值、原始记录、事件与数据问题的追溯档案。',
'direction':'数据处理与存储服务创新 / 信息系统运维技术',
'scenario':'围绕辽宁水产冻品运输设定应用场景。运输结束后，管理人员拿到温度记录，需要知道何时越限、持续多久，以及是否存在记录中断。单看折线图容易把缺测区间当成连续正常记录。本项目针对历史记录核查设计，使用程序合成数据，不声称已与冷链企业或真实传感器对接。',
'needs':[['运输管理者','查看批次与设备的温度变化','可切换批次，阈值和单位明确'],['质控复核者','定位连续越限与缺测区间','缺口与越限分别列出，不推算未知时间'],['资料归档者','保存可复算的记录依据','档案包含当前参数、原始记录和问题清单']],
'workflow':'导入记录 → 校验时间与温度 → 按批次设备分组 → 排除冲突时间戳 → 时间排序 → 分析相邻采样区间 → 展示曲线和事件 → 导出追溯档案。',
'modules':[['common.js','CSV 解析、编码解码、导出与转义'],['core.js','时间校验、分组排序、缺口识别与越限区间合并'],['app.js','阈值控制、结果失效、SVG 曲线与批次切换'],['demo.js','3 个批次设备组合的合成运输记录']],
'algo':[
('输入时钟与分组有明确约束','时间格式为 YYYY-MM-DD HH:mm[:ss]，采用统一业务时钟，不做跨时区转换。检查日期和时分秒的真实有效性；温度需为 -100 至 100 ℃ 的有限数。按批次与设备组合分组，同组同时间戳的冲突记录全部排除并保留问题说明。'),
('以有观测支持的区间计算时长','对相邻采样点采用前值保持：前一点温度代表到下一点之前的区间。温度严格高于上限或低于下限计为越限，等于边界计为区间内。连续同方向的越限区间合并为事件，区间极值只取对这些区间起作用的采样点。'),
('缺口不按正常或异常外推','相邻时间间隔大于预期采样间隔的 1.5 倍时，整个间隔列为采样缺口，不计入覆盖时长和越限时长。末个采样点没有后续观测，其越限持续时间未知，单独提示。单点且无覆盖区间时，占比显示未知而非 100%。'),
('统计口径与展示保持一致','区间内时长占比为区间内已观测时长除以总覆盖时长。多个设备的时长为设备分钟合计，不等于某次运输的历时。SVG 曲线以虚线显示缺口，实线仅辅助观察趋势；事件计算仍以前值保持模型为准。')],
'pseudo':'按 [批次, 设备] 分组，排除重复时间戳后排序\n对相邻采样点 a、b：\n  间隔 > 1.5 × 预期间隔：记为缺口，不累计时长\n  否则：累计覆盖时长\n       a 温度越界时累计越限时长，合并连续同类事件\n末点越界：标注时长未知\n区间内占比 = (覆盖时长 - 越限时长) / 覆盖时长',
'schema':[['批次','文本','必填，区分运输批次'],['设备','文本','必填，同批次不同设备分别分析'],['时间','YYYY-MM-DD HH:mm[:ss]','统一业务时钟，不接受无效日期'],['温度','摄氏度数值','必填，支持负数和小数'],['规则参数','上下限 / 预期间隔','下限小于上限，间隔大于 0']],
'case':'内置 72 条有效记录，构成 3 个批次设备组合。默认下限 -22 ℃、上限 -18 ℃、预期间隔 10 分钟。结果为越限 80 设备分钟，覆盖 680 设备分钟，区间内占比 88.2%；另有 1 处 40 分钟采样缺口。将上限改为 -16 ℃ 后重新分析，样例越限时长变为 30 分钟。以上阈值仅为演示配置。',
'pilot':'先由业务人员按具体产品要求确认阈值、采样间隔与设备时钟，再导入授权的历史记录，与人工标注的异常区间逐项比对。验收应包括缺口识别、尾点未知、重复时间戳和参数变更后的复算。接入传感器前另行设计设备校时、采集重试和权限管理。',
'value':'针对已有 CSV 导出能力的运输记录器，可先作为历史数据核查工具使用，降低系统接入门槛。后续服务可围绕设备导出格式适配和批次归档展开。当前尚无企业试点、真实运输测试或经济效益数据；本报告不把合成事件作为实际运输事故。',
'limits':'尚未接入实时传感器、地图定位、短信和企业平台；不做食品安全、报废或合规结论。阈值需由业务方按产品要求确定。前值保持法属于可解释的区间估计，不能恢复采样之间的真实温度；缺口与尾点的未知部分保留为未知。',
'features':'缺口与越限分别建模；尾点时长未知；批次设备隔离；参数可复算；档案保留计算口径。',
'video':'导入温度 CSV → 查看汇总 → 切换低温批次 → 查看缺口批次 → 调整阈值复算 → 导出追溯档案。',
'more':'核实产品对应的温度要求、设备时钟及采样间隔；可补充经授权的实际记录与人工复核结果。当前没有真实传感器接入，不要在申报时写成实时物联网系统。'
}}

class Report:
    def __init__(self,path,title,subtitle,accent):
        self.c=canvas.Canvas(str(path),pagesize=(595.28,841.89));self.title=title;self.subtitle=subtitle;self.accent=colors.HexColor(accent);self.n=0;self.y=0
        self.c.setTitle(title+'项目书与技术报告');self.c.setAuthor('');self.c.setSubject(subtitle)
        self.style=ParagraphStyle('Body',fontName='Yahei',fontSize=10,leading=17,wordWrap='CJK',textColor=colors.HexColor('#344158'),spaceAfter=8)
    def page(self,number,title,kicker='TECHNICAL REPORT'):
        if self.n:self.c.showPage()
        self.n+=1;c=self.c;c.setFillColor(colors.HexColor('#f6f8fc'));c.rect(0,0,595.28,841.89,fill=1,stroke=0);c.setFillColor(self.accent);c.rect(0,816,595.28,26,fill=1,stroke=0)
        c.setFillColor(colors.white);c.setFont('Yahei',8);c.drawString(46,825,self.title+' / '+self.subtitle)
        c.setFillColor(self.accent);c.setFont('YaheiBold',9);c.drawString(46,781,kicker+'  /  '+str(number).zfill(2))
        c.setFillColor(colors.HexColor('#162238'));c.setFont('YaheiBold',23);c.drawString(46,742,title);self.y=714
        c.setStrokeColor(colors.HexColor('#dce3ed'));c.line(46,52,549,52);c.setFillColor(colors.HexColor('#718096'));c.setFont('Yahei',8);c.drawString(46,35,'2026 数启新域 · 智创未来 / 技术创新赛道');c.drawRightString(549,35,f'{self.n:02d}')
    def p(self,text,size=10,leading=17,color=None):
        style=ParagraphStyle('p',parent=self.style,fontSize=size,leading=leading,textColor=color or self.style.textColor)
        p=Paragraph(escape(text).replace('\n','<br/>'),style);w,h=p.wrap(503,700)
        if self.y-h<68:raise RuntimeError(f'Page {self.n} overflow at {text[:25]} y={self.y} h={h}')
        p.drawOn(self.c,46,self.y-h);self.y-=h+10
    def heading(self,text):self.y-=7;self.p(text,12,19,colors.HexColor('#162238'))
    def table(self,rows,widths):
        style=ParagraphStyle('cell',parent=self.style,fontSize=8.6,leading=14)
        data=[[Paragraph(escape(str(x)),style) for x in r] for r in rows];t=Table(data,colWidths=widths,hAlign='LEFT');t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#e5ebf4')),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),10),('RIGHTPADDING',(0,0),(-1,-1),10),('TOPPADDING',(0,0),(-1,-1),9),('BOTTOMPADDING',(0,0),(-1,-1),9),('LINEBELOW',(0,0),(-1,-1),.4,colors.HexColor('#dfe5ee'))]));w,h=t.wrap(503,700)
        if self.y-h<68:raise RuntimeError(f'Table overflow page {self.n}')
        t.drawOn(self.c,46,self.y-h);self.y-=h+15
    def picture(self,path,max_h=510):
        im=Image.open(path);w,h=im.size;scale=min(503/w,max_h/h);dw,dh=w*scale,h*scale
        if self.y-dh<68:raise RuntimeError('Image overflow')
        self.c.drawImage(str(path),46+(503-dw)/2,self.y-dh,width=dw,height=dh);self.y-=dh+14
    def save(self):self.c.save()

def materials():
    core_source=(ROOT/'test_core.cjs').read_text(encoding='utf-8');test_lines=[l for l in core_source.splitlines() if l.startswith('test(')]
    core_results=json.loads((ROOT/'core-test-results.json').read_text(encoding='utf-8'))['results']
    for folder,kind,title,sub,color,en in PROJECTS:
        root=OUT/folder;info=INFO[kind];dest=root/'待填写'/'官方报名信息汇总表（待填写）.xlsx';shutil.copyfile(FORM,dest)
        assert hashlib.sha256(FORM.read_bytes()).digest()==hashlib.sha256(dest.read_bytes()).digest()
        own={'ledger':range(4,9),'privacy':range(9,14),'cold':range(14,19)}[kind];indices=list(range(4))+list(own)
        testdir=root/'测试';testdir.mkdir(exist_ok=True)
        prefix="const assert=require('node:assert/strict');const C=require('../程序/common.js');const "+{'ledger':'L','privacy':'P','cold':'T'}[kind]+"=require('../程序/core.js');let passed=0;function test(name,fn){fn();passed++;console.log('PASS '+name);}\n"
        prefix+="const O=(id,a,r='0')=>({'订单号':id,'订单金额':a,'退款金额':r,'渠道':'测试',_row:2});const R=(id,a,t)=>({'订单号':id,'实收金额':a,'流水号':t,_row:2});\nconst row=(time,temp,b='B',sensor='S')=>({'批次':b,'设备':sensor,'时间':'2026-10-20 '+time,'温度':String(temp),_row:2});\n"
        (testdir/'test.cjs').write_text(prefix+'\n'.join(test_lines[i] for i in indices)+"\nconsole.log(passed+' tests passed');",encoding='utf-8')
        (testdir/'测试结果.json').write_text(json.dumps({'核心测试':[core_results[i] for i in indices],'浏览器检查':{'离线file协议':True,'文件导入':True,'下载导出':True,'无效输入与结果失效':True,'390像素移动布局':True,'外部网络请求数':0}},ensure_ascii=False,indent=2),encoding='utf-8')
        readme=f'''{title}运行与源码说明\n\n一、运行\n将本项目完整解压后，双击“打开程序.html”，推荐使用当前版本 Chrome 或 Edge。无需安装、无需联网。不要只移动 HTML 文件，程序目录内的 CSS 与 JS 须保持相对位置。\n\n二、项目介绍\n{info['intro']}\n\n三、现场演示\n{info['video']}\n页面默认使用合成演示数据，可通过“载入演示数据”恢复。\n\n四、文件与源码\n程序/index.html：完整界面；style.css：响应式布局；common.js：通用处理；core.js：核心算法；app.js：交互；demo.js：合成样例。均为可直接阅读与修改的源代码，没有压缩和隐藏逻辑。\n样例数据目录：演示文件与适用的 CSV 空模板。\n技术材料目录：项目书与技术报告 PDF、可编辑 Markdown、资料与开发说明。\n演示材料目录：实际运行截图、带字幕的 MP4 操作演示、从程序实际导出的演示档案。视频无音轨，字幕说明操作。\n待填写目录：官方报名表原件副本，团队信息未填写。\n测试目录：可独立执行的核心测试。开发测试需 Node.js 18 或更高版本；在该目录运行 node test.cjs。程序本身不需要 Node.js。\n\n五、输入与输出\n所有导入内容在本地内存处理，不向服务器上传，不写入浏览器持久化存储。关闭或刷新页面后需重新导入。导出文件保存在浏览器默认下载位置或由使用者选择的位置。\n\n六、适用范围\n{info['limits']}\n\n七、资料性质\n当前交付为可运行技术原型，样例为程序合成；没有虚构企业合作、用户数量、准确率、获奖情况或业务效益。参赛成员、队长、学校、指导教师及个人分工需按实际情况补齐。\n'''
        (root/'使用说明.txt').write_text(readme,encoding='utf-8-sig')
        reminder=f'''【{title}】项目介绍与提交前待补清单\n\n请先阅读：本文件仅供团队内部整理材料使用。阅读完成并处理以下事项后，请在正式提交前删除本 TXT，再重新压缩提交包。删除本提醒不影响程序运行。\n\n一、项目介绍\n作品名称：{title}——{sub}\n建议赛道：技术创新赛道\n对应方向：{info['direction']}\n{info['intro']}\n主要技术实现：{info['features']}\n\n二、已备材料\n可离线运行的完整程序与源码；合成演示数据；项目书与技术报告 PDF 及可编辑 Markdown；实际运行截图；带字幕的 MP4 操作演示；运行说明；独立核心测试与验证结果；官方报名信息汇总表空白原件副本。\n\n三、必须补填与核对\n1. 在“待填写/官方报名信息汇总表（待填写）.xlsx”中填写学院、系部、作品名称、参赛类别、成员数量、最高学历、参赛成员（队长写首位）、指导教师姓名、职称、联系方式。原表未填入任何团队身份信息。\n2. 核对项目书封面的学校/机构、队长、团队成员和指导教师空栏；可编辑技术材料中的 Markdown，再导出最终 PDF，或按学院要求追加正式封面。\n3. 补齐每位成员的真实分工与完成情况，核对 AI 辅助开发与资料来源说明；不要将工具辅助工作虚写为学生独立完成的工作。\n4. 人数规则有冲突：通知正文为每队 1-6 人、教师 1-2 名；你提供的官方报名表表尾为每队 1-5 人、指导教师 1 名且需为在校专职教师。请向学院/主办方确认；未确认前可按表格较严口径准备，不要改写原表说明。\n5. 报名表 E5 的下拉选项为“技术创新赛道/产业实践赛道”，后续行选项为“创新赛道/创业赛道”。本项目建议技术创新赛道，若填后续行请确认统一填法。\n6. 检查最终封面、表格和文件名使用同一作品名称、学校与队长信息。通知要求命名：“赛道+作品名+学校/机构+队长姓名”。当前项目包名因身份信息留空，尚未采用最终申报命名。\n7. {info['more']}\n8. 本项目按技术创新赛道准备。通知要求该类提交技术报告、源代码（如适用）、演示视频；报名另需项目书 PDF、团队信息 Excel。项目书与技术报告合在同一份 PDF 内。若学院要求分开提交，请用报告可编辑源稿分拆。\n9. 入围后按组委会要求签署知识产权声明；当前没有代签或虚构签章。若学院另有承诺书、盖章、视频格式或表格要求，需补充对应文件。\n\n四、提交安排\n通知作品提交阶段截止：2026 年 10 月 25 日。\n报名与投递以学校院系或机构统一提交为准，通知邮箱：guozhuofan1@qq.com，以组委会确认邮件为准。请预留学院汇总时间。本次仅制作与整理本地材料，没有替你发送报名邮件。\n比赛通知：{SOURCE}\n\n五、正式打包前\n完整解压运行一次 → 核对 PDF 与视频 → 填好官方表和作者信息 → 检查身份及分工一致 → 按通知重命名 → 删除本 TXT → 重新压缩。\n不要直接提交包含三项所有作品的总汇总包，应按学院要求提供各项目材料。\n'''
        (root/f'提交前必读（看完请删除）-{title}.txt').write_text(reminder,encoding='utf-8-sig')
        provenance=f'''# {title}开发与资料来源说明\n\n本项目在 OpenAI Codex 辅助环境中完成界面构思、HTML/CSS/JavaScript 实现、测试脚本、样例数据设计、技术材料整理及演示字幕制作。团队应核对实际参与过程并补充每位成员真实分工。\n\n运行程序不接入生成式 AI 模型或远程推理服务。全部业务样例由程序合成。截图和视频来自本项目实际运行页面，视频字幕为演示后期说明，没有真实业务或企业试点数据。\n\n## 技术参考\n- MDN：Using files from web applications，https://developer.mozilla.org/en-US/docs/Web/API/File_API/Using_files_from_web_applications\n- RFC 4180：Common Format and MIME Type for CSV Files，https://www.rfc-editor.org/rfc/rfc4180\n- OWASP：CSV Injection，https://community.owasp.org/attacks/CSV_Injection\n\n上述资料用于文件处理、CSV 引号与导出防护的技术参考；没有复制其页面图片或使用外部商业素材。界面为 HTML/CSS/SVG 实现，字体调用使用者系统字体。\n\n## 赛事依据\n{SOURCE}\n官方报名表为用户提供的 Excel 原件副本，未改动格式与说明。\n'''
        (root/'技术材料'/'开发与资料来源说明.md').write_text(provenance,encoding='utf-8')
        pdf=root/'技术材料'/f'{title}项目书与技术报告.pdf';r=Report(pdf,title,sub,color)
        sections=[]
        def sec(h,ps):sections.append('## '+h+'\n\n'+'\n\n'.join(ps))
        r.page(1,title+'项目书与技术报告','PROJECT DOSSIER');r.p(sub,16,25);r.p('技术创新赛道 / '+info['direction'],10,18);r.y-=10;r.heading('项目概述');r.p(info['intro']);r.heading('已完成成果');r.p('可运行离线网页、完整源代码、合成样例、结果导出、核心逻辑测试、浏览器交互验证、实际运行截图与字幕演示视频。');r.heading('核心实现');r.p(info['features']);r.y-=16;r.table([['申报信息','待填写'],['学校 / 机构',''],['队长 / 团队成员',''],['指导教师','']],[130,373]);r.p('版本：V1.0　材料日期：2026 年 10 月 20 日',9,16);r.p('身份信息按要求留空，提交前需补齐。本材料中的案例和计算结果来自合成样例。',9,16)
        sec('项目概述',[info['intro'],info['features'],'学校/机构：\n队长：\n团队成员：\n指导教师：'])
        r.page(2,'业务场景与需求');r.p(info['scenario']);r.heading('使用者与验收要求');r.table([['使用者','任务','验收依据']]+info['needs'],[75,201,227]);r.heading('业务流程');r.p(info['workflow']);r.heading('需求边界');r.p(info['limits']);sec('业务场景与需求',[info['scenario'],info['workflow'],info['limits']])
        r.page(3,'系统结构与实现');r.p('采用无构建依赖的 HTML、CSS 与 JavaScript。界面层与核心算法分离，核心函数同时支持浏览器调用与 Node.js 独立测试。静态文件可通过 file 协议运行，导入与导出均在使用者本机完成。');r.table([['源文件','职责']]+info['modules'],[100,403]);r.heading('数据流与信任边界');r.p('使用者选定本地文件 → 浏览器解码 → 字段与记录校验 → 核心算法 → 界面结果 → 使用者主动下载。外部数据按文本处理；界面中的业务文本通过转义或 textContent 输出。程序没有服务端账号、网络 API 或持久化数据库。');r.heading('状态一致性');r.p('导入新文件或修改关键规则后，清空旧统计并禁用导出。使用者必须重新执行处理，避免把旧结果误认为新输入的计算结果。浏览器测试已检查该状态变化。');r.heading('部署与运行');r.p('将完整项目解压后双击“打开程序.html”。推荐当前版本 Chrome 或 Edge。普通使用无需安装依赖；修改代码后直接刷新。390 像素手机布局已进行浏览器检查。');sec('系统结构与实现',['界面层、通用文件处理和核心算法分离。源文件职责：']+[' / '.join(x) for x in info['modules']])
        r.page(4,'核心算法与异常处理');
        for h,p in info['algo']:r.heading(h);r.p(p,9.5,16)
        sec('核心算法与异常处理',[h+'：'+p for h,p in info['algo']])
        r.page(5,'输入输出与复算示例');r.table([['数据项','字段 / 形式','约束与含义']]+info['schema'],[88,155,260]);r.heading('算法步骤');r.p(info['pseudo'],9,16);r.heading('合成样例的实际结果');r.p(info['case'],9.5,16);sec('输入输出与复算示例',[' / '.join(x) for x in info['schema']]+[info['pseudo'],info['case']])
        r.page(6,'实际运行界面');r.p('下图来自本项目实际运行页面。页面预置合成样例，截图展示处理结果与主要操作入口。完整分辨率图片随包提供。',9,16);r.picture(root/'演示材料'/'01-工作台全景.png',max_h=525);r.p('现场演示顺序：'+info['video'],9,16);sec('实际运行与演示',[info['video'],'界面图片位于演示材料目录，视频由真实程序操作录制并附字幕。'])
        r.page(7,'测试方法与验证结果');r.p('核心测试在 Node.js 中独立执行，采用手工构造的输入和明确预期值检查边界。三项目共有 19 个不同核心用例；下表列出与本项目有关的 9 项（含 4 项共用文件处理用例），均已执行通过。测试脚本和结果随包提供。',9.5,16)
        r.table([['用例','结果']]+[[core_results[i]['name'],'通过'] for i in indices],[445,58]);r.heading('浏览器端验证');r.p('使用 Chrome 以 file 协议打开程序，检查文件导入、筛选或规则变更、无效输入、下载文件内容及移动端布局。三个项目均未产生页面脚本错误，未发起外部 HTTP/HTTPS 请求。没有以测试结果推导生产识别准确率或真实业务收益。',9.5,16);sec('测试方法与验证结果',['核心用例：'+core_results[i]['name']+'，结果通过。' for i in indices]+['浏览器完成离线、导入、导出、无效输入与移动布局检查。'])
        r.page(8,'应用可行性与落地路径');r.heading('定位与差异');r.p(info['features']);r.heading('小范围试用计划');r.p(info['pilot']);r.heading('部署与维护成本');r.p(info['value']);r.heading('完成情况与下一步');r.p(info['limits']);r.heading('团队执行信息');r.p('参赛成员、指导教师与具体分工按实际情况补填。建议明确需求确认、核心实现、测试验证、材料与演示各环节的负责人，并为相应提交记录保留真实证据。当前报告不替团队编造分工。',9.5,16);sec('应用可行性与落地路径',[info['pilot'],info['value'],info['limits']])
        r.page(9,'交付、资料与开发说明');r.heading('交付内容');r.p('程序：完整可读源代码与离线运行入口。\n数据：合成演示样例及适用的空 CSV 模板。\n技术材料：本报告 PDF、可编辑 Markdown 和开发资料说明。\n演示材料：实际运行截图、字幕 MP4、实际导出的演示档案。\n测试：独立核心测试与结果。\n待填写：用户提供的官方报名表原件副本。');r.heading('AI 辅助与原创贡献核对');r.p('本项目使用 OpenAI Codex 辅助界面构思、程序实现、样例设计、测试脚本和材料整理；运行程序不接入生成式 AI 服务。参赛团队需结合实际参与过程补充真实成员分工，核对各项原创贡献。界面使用 HTML/CSS/SVG 构建，没有使用外部商业图像素材。');r.heading('参考资料');r.p('1. MDN，Using files from web applications。用于本地文件读取与下载接口参考。\nhttps://developer.mozilla.org/en-US/docs/Web/API/File_API/Using_files_from_web_applications\n2. RFC 4180，Common Format and MIME Type for CSV Files。用于 CSV 引号与换行格式参考。\nhttps://www.rfc-editor.org/rfc/rfc4180\n3. OWASP，CSV Injection。用于 CSV 导出时的公式起始符防护参考。\nhttps://community.owasp.org/attacks/CSV_Injection',8.5,14);r.heading('赛事依据');r.p('2026“数启新域・智创未来”信息技术创新大赛通知，DNUI 信息与商务管理学院，2026 年 10 月 9 日。\n'+SOURCE,8.5,14);r.save()
        sec('开发与资料来源',[provenance])
        (root/'技术材料'/f'{title}项目书与技术报告.md').write_text('# '+title+'项目书与技术报告\n\n'+sub+'\n\n'+'\n\n'.join(sections),encoding='utf-8')
        print('PDF '+str(pdf))

if __name__=='__main__':materials()
