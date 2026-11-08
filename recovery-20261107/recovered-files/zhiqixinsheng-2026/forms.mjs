import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const root=path.dirname(fileURLToPath(import.meta.url));
const projects=JSON.parse(await fs.readFile(path.join(root,'projects.json'),'utf8'));
const colors={collab:'#cf684e',event:'#39876a',delivery:'#5b62c9'};
for(const p of projects){
 const wb=Workbook.create(),s=wb.worksheets.add('项目资料待填');s.showGridLines=false;
 s.getRange('A1:C24').format.font={name:'Arial',size:11,color:'#26354a'};
 s.getRange('A1:C24').format.rowHeight=28;s.getRange('A1:A24').format.columnWidth=28;s.getRange('B1:B24').format.columnWidth=42;s.getRange('C1:C24').format.columnWidth=64;
 s.getRange('A2').values=[[p.title+'报名资料待填']];s.getRange('A2').format.font={name:'Arial',size:15,bold:true,color:colors[p.kind]};
 s.getRange('A3').values=[['整理版，供学院联系人汇总使用。']];s.getRange('A3').format.font={name:'Arial',size:10,color:'#76869a'};
 s.getRange('A4:C4').values=[['字段','待填写内容','填写提示']];s.getRange('A4:C4').format={fill:colors[p.kind],font:{name:'Arial',size:11,bold:true,color:'#ffffff'},rowHeight:30,verticalAlignment:'center',horizontalAlignment:'center'};
 const rows=[['学校',null,'与创意说明书首页一致'],['学院',null,'由项目负责人所在学院统一报送'],['系别',null,'按学院统一名称填写'],['团队成员',null,'上限 5 人，按实际参与情况填写'],['项目负责人姓名',null,'每人作为负责人限报 1 项'],['项目负责人学号',null,'按文本填写，保留前导零'],['联系电话',null,'按实际联系方式填写'],['参赛赛道',null,'从下拉选项选择；同一作品仅报一个赛道'],['作品名称',null,'与文件夹名称、PDF 首页一致'],['指导教师',null,'上限 1 人'],['作品简介',null,'提炼问题、用户与核心方案'],['学院指定赛事联系人',null,'由学院指定；个人不能作为报送主体'],['学院联系人电话',null,'由学院核验后填写'],['学院联系人邮箱',null,'由学院核验后填写']];
 s.getRange('A5:C18').values=rows;s.getRange('A5:C18').format.verticalAlignment='center';s.getRange('B5:B18').format.fill='#fff7df';s.getRange('B5:B18').setNumberFormat('@');s.getRange('B5:C18').format.wrapText=true;s.getRange('B15:C15').format.rowHeight=85;
 s.getRange('B12').dataValidation={rule:{type:'list',values:['人工智能与移动生活应用','移动产品与用户体验','行业数字服务与软件解决方案']}};
 s.getRange('A21:C22').values=[['官方报名入口','https://docs.qq.com/sheet/DZXhMbEpXeE1oWWpK','正式提交以学院官方汇总表为准'],['比赛通知','https://mp.weixin.qq.com/s/908kuVaWyV-KrgsUdrQsdQ','拟定征集截止 2026-11-06']];s.getRange('A21:C22').format.font={name:'Arial',size:10,color:'#7b8798'};s.getRange('B21:C22').format.wrapText=true;s.getRange('A21:C22').format.rowHeight=40;
 s.getRange('A24').values=[['黄色为待填项，身份信息均留空。']];s.getRange('A24').format.font={name:'Arial',size:10,color:'#8b7753'};
 wb.recalculate();const check=await wb.inspect({kind:'table',range:'项目资料待填!A4:C18',include:'values',tableMaxRows:15,tableMaxCols:3,maxChars:2500});console.log(p.title+' '+check.ndjson);
 const preview=await wb.render({sheetName:'项目资料待填',range:'A1:C24',scale:1,format:'png'});await fs.writeFile(path.join(root,'.qa',p.kind+'-form.png'),new Uint8Array(await preview.arrayBuffer()));
 const xlsx=await SpreadsheetFile.exportXlsx(wb);await xlsx.save(path.join(root,'交付',p.folder,'待填写','项目报名资料待填写（整理版）.xlsx'));
 console.log('EXPORTED '+p.title);
}
