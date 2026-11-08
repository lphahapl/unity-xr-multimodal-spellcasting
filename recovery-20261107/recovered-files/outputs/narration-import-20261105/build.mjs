import fs from 'node:fs/promises';
import { constants } from 'node:fs';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const out = 'D:/UnityProjects/NarutoVR/outputs/narration-import-20261105';
const desktop = 'C:/Users/鲁迅/Desktop';
const rows = [
  ['audioFileName', 'speaker', 'delayAfter'],
  ['intro_01.wav', '老师', 0.5],
  ['fireball_hint.wav', '老师', 2],
  ['training_complete.wav', '老师', 0],
];
const workbook = Workbook.create();
const data = workbook.worksheets.add('对话数据');
data.showGridLines = false;
data.getRange('A1:C4').values = rows;
data.getRange('A1:C1').format.fill = '#27364B';
data.getRange('A1:C1').format.font.color = '#FFFFFF';
data.getRange('A1:C1').format.font.bold = true;
data.getRange('A1:C4').format.rowHeight = 27;
data.getRange('A1:A4').format.columnWidth = 30;
data.getRange('B1:B4').format.columnWidth = 18;
data.getRange('C1:C4').format.columnWidth = 18;
data.getRange('A2:C4').format.fill = '#FFF5D6';
data.getRange('C2:C4').setNumberFormat('0.0');

// 说明单独放第二页，不混入第一张工作表的导入记录。
const notes = workbook.worksheets.add('填写说明');
notes.showGridLines = false;
const help = [
  ['项目', '说明'],
  ['导入范围', 'Unity 只读取第一张工作表“对话数据”。保留英文表头。'],
  ['audioFileName', '音频文件名或子目录相对路径，可带扩展名，必填。'],
  ['speaker', '说话者，必填。'],
  ['delayAfter', '到下一句的间隔，单位秒。填 0、0.5、2 等非负数字。'],
  ['空白间隔', '存在 delayAfter 列时，空白按 0 导入。'],
  ['旧表兼容', '没有 delayAfter 列时，已有句保留原间隔，新句默认 0。'],
  ['重导入', '同音频和说话者保留推进方式与事件；有间隔列则覆盖间隔。'],
  ['音频位置', '示例文件名为占位。替换为自己的音频，放在 Assets/Resources/Narration。'],
  ['推进方式', '在 Inspector 选自动继续、等待操作或音频和操作都完成。'],
  ['示例第 2 句', '可在 Inspector 选双条件，并绑定命中靶子后通知完成。'],
  ['间隔时机', '本句条件通过后等待；最后一句无下一句，忽略间隔。'],
  ['事件绑定', '操作成功绑定 NarrationController.PublishCurrentOperation()。'],
  ['保存配置', '导入并配置后保存场景或 Prefab；无需另挂计时器。'],
];
notes.getRange('A1:B14').values = help;
notes.getRange('A1:B14').format.rowHeight = 28;
notes.getRange('A1:A14').format.columnWidth = 19;
notes.getRange('B1:B14').format.columnWidth = 103;
notes.getRange('A1:B1').format.fill = '#27364B';
notes.getRange('A1:B1').format.font.color = '#FFFFFF';
notes.getRange('A1:B1').format.font.bold = true;
workbook.recalculate();
await fs.writeFile(`${out}/inspection.ndjson`, (await workbook.inspect({kind:'table',range:'对话数据!A1:C4',include:'values,formulas',tableMaxRows:4,tableMaxCols:3})).ndjson);
for (const [sheetName, file] of [['对话数据','data'],['填写说明','instructions']]) {
  const preview = await workbook.render({sheetName,autoCrop:'all',scale:1,format:'png'});
  await fs.writeFile(`${out}/${file}.png`, new Uint8Array(await preview.arrayBuffer()));
}
const xlsx = await SpreadsheetFile.exportXlsx(workbook);
await xlsx.save(`${out}/对话导入示例.xlsx`);
await fs.writeFile(`${out}/对话导入示例.csv`, '\ufeff' + rows.map(r=>r.join(',')).join('\r\n')+'\r\n', 'utf8');
for (const ext of ['xlsx','csv'])
  await fs.copyFile(`${out}/对话导入示例.${ext}`, `${desktop}/对话导入示例.${ext}`, constants.COPYFILE_EXCL);
console.log('已创建并复制到桌面：对话导入示例.xlsx、对话导入示例.csv');
