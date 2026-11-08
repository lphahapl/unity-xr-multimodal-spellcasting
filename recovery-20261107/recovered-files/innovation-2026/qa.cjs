const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),{pathToFileURL}=require('node:url');
const {createRequire}=require('node:module');const req=createRequire('C:/Users/鲁迅/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_entry.js');
const {chromium}=req('playwright');
const root=path.join(__dirname,'交付'),qa=path.join(__dirname,'.qa');fs.mkdirSync(qa,{recursive:true});
(async()=>{const browser=await chromium.launch({headless:true,channel:'chrome'});const results=[];
for(const folder of ['01-账有据','02-隐数','03-冻链有数']){
 const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});const page=await context.newPage();let errors=[],external=[];page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))external.push(r.url());});
 await page.goto(pathToFileURL(path.join(root,folder,'程序/index.html')).href);await page.locator('#stats .stat').first().waitFor();
 const capture=async name=>{await page.screenshot({path:path.join(root,folder,'演示材料',name+'.png'),fullPage:true});};
 await capture('01-工作台全景');
 if(folder.startsWith('01')){
  assert.equal(await page.locator('#rows tr').count(),25);await page.selectOption('#filter','需复核');assert.equal(await page.locator('#rows tr').count(),3);await capture('02-异常筛选');await page.locator('#rows button').first().click();assert.ok((await page.locator('#dialog-text').innerText()).includes('订单原始记录'));await capture('03-原始记录溯源');await page.click('#dialog-close');
  const [d]=await Promise.all([page.waitForEvent('download'),page.click('#export-main')]);await d.saveAs(path.join(qa,'ledger-result.csv'));assert.ok(fs.readFileSync(path.join(qa,'ledger-result.csv'),'utf8').includes('需复核'));
  await page.locator('#orders-file').setInputFiles(path.join(root,folder,'样例数据/演示订单.csv'));assert.equal(await page.locator('#export-main').isDisabled(),true);await page.click('#run');assert.equal(await page.locator('#export-main').isEnabled(),true);
  await page.locator('#orders-file').setInputFiles({name:'bad.csv',mimeType:'text/csv',buffer:Buffer.from('错误列\n1')});assert.equal(await page.locator('#error.show').count(),1);
 }else if(folder.startsWith('02')){
  assert.equal(await page.locator('#rows tr').count(),8);await page.selectOption('#mode','alias');assert.equal(await page.locator('#export-main').isDisabled(),true);await page.click('#run');assert.ok((await page.locator('#preview').innerText()).includes('<PHONE_001>'));await capture('02-一致化代号');
  const [d]=await Promise.all([page.waitForEvent('download'),page.click('#export-audit')]);await d.saveAs(path.join(qa,'privacy-audit.json'));assert.ok(!fs.readFileSync(path.join(qa,'privacy-audit.json'),'utf8').includes('13800138000'));
  await page.locator('#source').fill('<img src=x onerror=alert(1)> 13800138000');await page.click('#run');assert.equal(await page.locator('#preview img').count(),0);assert.ok((await page.locator('#preview').innerText()).includes('PHONE_001'));await page.click('#clear');assert.equal(await page.locator('#source').inputValue(),'');await page.click('#load-demo');
 }else{
  const stat=await page.locator('#stats').innerText();assert.ok(stat.includes('72'));assert.ok(stat.includes('80'));await page.locator('#batches button').nth(2).click();assert.ok((await page.locator('#rows').innerText()).includes('采样缺口'));await capture('02-缺测批次');
  await page.locator('#high').fill('-16');assert.equal(await page.locator('#export-main').isDisabled(),true);await page.click('#run');assert.ok((await page.locator('#stats').innerText()).includes('30'));await capture('03-调整阈值后的结果');
  const [d]=await Promise.all([page.waitForEvent('download'),page.click('#export-json')]);await d.saveAs(path.join(qa,'cold-trace.json'));const report=JSON.parse(fs.readFileSync(path.join(qa,'cold-trace.json'),'utf8'));assert.equal(report.bad,30);assert.equal(report.issues.length,0);
  await page.locator('#low').fill('-10');await page.click('#run');assert.equal(await page.locator('#error.show').count(),1);assert.equal(await page.locator('#export-main').isDisabled(),true);await page.click('#load-demo');
 }
 await page.click('#load-demo');await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(qa,folder+'-mobile.png'),fullPage:true});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 assert.deepEqual(errors,[]);assert.deepEqual(external,[]);results.push({project:folder,offlineFile:true,export:true,invalidInput:true,responsive:true,externalRequests:external.length,pageErrors:errors});console.log('PASS browser '+folder);await context.close();
}
fs.writeFileSync(path.join(__dirname,'browser-test-results.json'),JSON.stringify(results,null,2));await browser.close();})().catch(e=>{console.error(e);process.exit(1)});
