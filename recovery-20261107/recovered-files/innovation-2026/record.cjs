const fs=require('node:fs'),path=require('node:path'),{spawn}=require('node:child_process'),{pathToFileURL}=require('node:url'),{once}=require('node:events');
const {createRequire}=require('node:module');const req=createRequire('C:/Users/鲁迅/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/_entry.js');const {chromium}=req('playwright');
const bin=path.join(__dirname,'.deps/imageio_ffmpeg/binaries');const ffmpeg=path.join(bin,fs.readdirSync(bin).find(s=>s.endsWith('.exe')));const root=path.join(__dirname,'交付');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{const browser=await chromium.launch({headless:true,channel:'chrome'});for(const folder of ['01-账有据','02-隐数','03-冻链有数']){
 const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});const page=await context.newPage();await page.goto(pathToFileURL(path.join(root,folder,'程序/index.html')).href);await page.waitForSelector('#stats .stat');
 await page.evaluate(()=>{const el=document.createElement('div');el.id='demo-caption';el.style='position:fixed;z-index:999999;bottom:15px;left:50%;transform:translateX(-50%);background:#111d32ed;color:white;padding:15px 25px;border-radius:8px;font:16px/1.6 Microsoft YaHei;max-width:90%;width:max-content;box-shadow:0 5px 25px #0003;pointer-events:none';document.body.append(el);});
 const videoPath=path.join(root,folder,'演示材料',folder.slice(3)+'功能演示.mp4');const process=spawn(ffmpeg,['-hide_banner','-loglevel','error','-f','image2pipe','-framerate','8','-i','pipe:0','-c:v','libx264','-preset','fast','-crf','23','-pix_fmt','yuv420p','-r','24','-movflags','+faststart','-y',videoPath],{windowsHide:true});let stderr='';process.stderr.on('data',b=>stderr+=b);let recording=true,frames=0;const capture=(async()=>{while(recording){const start=Date.now();const jpeg=await page.screenshot({type:'jpeg',quality:88});if(!process.stdin.write(jpeg))await once(process.stdin,'drain');frames++;await sleep(Math.max(0,125-(Date.now()-start)));}})();
 const caption=async(text,ms=5000)=>{await page.locator('#demo-caption').evaluate((e,t)=>e.textContent=t,text);await sleep(ms);};
 if(folder.startsWith('01')){
  await caption('账有据：导入订单与收款流水，核对差异并追溯到原始记录。',6000);
  await page.locator('#orders-file').setInputFiles(path.join(root,folder,'样例数据/演示订单.csv'));await page.locator('#payments-file').setInputFiles(path.join(root,folder,'样例数据/演示流水.csv'));await caption('导入两份 CSV 后，旧结果失效；点击“开始对账”重新核算。',5000);await page.click('#run');await caption('金额按分计算；重复订单、重复流水与孤立流水单独列入需复核。',5000);
  await page.locator('#results').scrollIntoViewIfNeeded();await page.selectOption('#filter','少收');await caption('筛选“少收”，两笔订单各少收 15 元。',5000);await page.selectOption('#filter','需复核');await caption('切换“需复核”，检查重复与无法匹配的数据。',5000);
  await page.locator('#rows button').first().click();await caption('点击“查看”，显示订单、流水原始记录和 CSV 记录序号。',6500);await page.click('#dialog-close');
  const [d]=await Promise.all([page.waitForEvent('download'),page.click('#export-json')]);await d.saveAs(path.join(root,folder,'演示材料/演示复核档案.json'));await caption('导出复核档案供人工处理；原始记录随档案保留。',5000);await page.evaluate(()=>scrollTo(0,0));await caption('离线双击运行，无需安装；演示使用合成数据。',4500);
 }else if(folder.startsWith('02')){
  await caption('隐数：在本地识别敏感片段，核查后导出脱敏文本。',6000);
  await page.locator('#text-file').setInputFiles(path.join(root,folder,'样例数据/合成测试文本.txt'));await caption('导入测试文本，选择检查类型和替换方式。',4500);await page.click('#run');await page.locator('#preview').scrollIntoViewIfNeeded();await caption('局部隐藏保留必要辨识；身份证校验失败的候选也会被隐藏。',6000);
  await page.selectOption('#mode','alias');await page.click('#run');await page.locator('#preview').scrollIntoViewIfNeeded();await caption('一致化代号：重复出现的同一号码保持同一代号。',6500);
  await page.selectOption('#mode','full');await page.click('#run');await page.locator('#preview').scrollIntoViewIfNeeded();await caption('完全隐藏：去除整个命中片段，供不同共享场景选择。',5500);await page.locator('#results').scrollIntoViewIfNeeded();await caption('命中明细保留行号、类型与替换结果，不显示原始敏感值。',6000);
  const [d]=await Promise.all([page.waitForEvent('download'),page.click('#export-audit')]);await d.saveAs(path.join(root,folder,'演示材料/演示处理记录.json'));await caption('审计记录可导出；规则匹配有边界，发布前仍需人工复核。',5500);
 }else{
  await caption('冻链有数：按批次分析温度记录，追溯越限事件和采样缺口。',6000);await page.locator('#cold-file').setInputFiles(path.join(root,folder,'样例数据/演示温度记录.csv'));await page.click('#run');await caption('样例含 72 条记录，已观测越限 80 分钟，另有 1 处采样缺口。',6500);
  await page.locator('#batches button').nth(1).click();await caption('切换批次，查看低于下限的连续事件。',5500);await page.locator('#batches button').nth(2).click();await caption('缺口使用虚线提示，40 分钟缺测区间不推算越限时长。',6500);await page.locator('#results').scrollIntoViewIfNeeded();await caption('事件表保留批次、设备、起止时间、持续时长与区间极值。',5500);
  await page.evaluate(()=>scrollTo(0,0));await page.locator('#high').fill('-16');await page.click('#run');await caption('将上限改为 -16 ℃ 后重新分析，样例已观测越限变为 30 分钟。',6500);
  const [d]=await Promise.all([page.waitForEvent('download'),page.click('#export-json')]);await d.saveAs(path.join(root,folder,'演示材料/演示追溯档案.json'));await caption('导出包含阈值、原始记录与事件的追溯档案。阈值按产品要求设定。',5500);
 }
 recording=false;await capture;process.stdin.end();const [code]=await once(process,'close');if(code!==0)throw Error(stderr);fs.writeFileSync(videoPath+'.meta.json',JSON.stringify({frames,durationSeconds:frames/8,width:1440,height:1000,content:'真实程序操作录屏，字幕说明，无音轨',source:'本项目 file:// 页面，合成测试数据'},null,2));console.log('RECORDED '+folder+' '+frames/8+'s');await context.close();
 }await browser.close();})().catch(e=>{console.error(e);process.exit(1);});
