const fs=require('node:fs'),path=require('node:path'),{execFileSync}=require('node:child_process');
const projects=require('./projects.json');
for(const p of projects){const root=path.join(__dirname,'交付',p.folder);require(path.join(root,'程序/common.js'));const core=require(path.join(root,'程序/core.js'));fs.writeFileSync(path.join(root,'样例与备份','合成初始项目.json'),JSON.stringify(core.demo(),null,2));process.stdout.write(execFileSync(process.execPath,[path.join(root,'测试/test.cjs')],{encoding:'utf8'}));}
