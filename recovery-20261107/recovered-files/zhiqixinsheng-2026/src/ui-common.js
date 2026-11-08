'use strict';
const $=id=>document.getElementById(id),E=C.esc,KIND=document.body.dataset.kind,KEY='zhiqixinsheng.2026.'+KIND+'.v1';
let state=Core.demo(),undoState=null,toastTimer;let restoreIssue='';
try{const raw=localStorage.getItem(KEY);if(raw){state=Core.validate(JSON.parse(raw));$('data-label').textContent='已恢复本地项目';}}catch(e){restoreIssue='缓存未能恢复，已载入演示样例。可导入此前导出的项目 JSON。';}
function error(e,modal=false){const el=$(modal?'modal-error':'error');el.textContent=e?e.message||String(e):'';el.classList.toggle('show',!!e);}
function toast(s){$('toast').textContent=s;$('toast').classList.remove('hidden');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.add('hidden'),3000);}
function save(){try{localStorage.setItem(KEY,JSON.stringify(state));return '';}catch{return '更新已在本页生效，但浏览器缓存写入失败。请导出 JSON 保管，关闭页面可能丢失更改。';}}
function commit(next,message='操作已记录'){Core.validate(next);undoState=C.copy(state);state=next;const warning=save();$('undo').disabled=false;render();error(warning);toast(message);}
function apply(action,p,message){try{const next=Core.op(state,action,p);commit(next,message);error(null,true);return true;}catch(e){error(e,$('dialog').open);return false;}}
function modal(title,body){$('dialog-title').textContent=title;$('modal-body').innerHTML=body;error(null,true);if(!$('dialog').open)$('dialog').showModal();}
function closeModal(){$('dialog').close();}
$('close-modal').onclick=closeModal;
function metrics(items){$('metrics').innerHTML=items.map(([label,value,hint])=>`<div class="metric"><div class="label">${E(label)}</div><strong>${E(value)}</strong><small>${E(hint)}</small></div>`).join('');}
function history(target='history',limit=8){$(target).innerHTML=state.history.slice(-limit).reverse().map(h=>`<li><div><b>${E(h.type)}</b><small>${E(h.at.replace('T',' '))}</small></div><p>${E(h.detail)}　/　${E(actorName(h.actor))}</p></li>`).join('')||'<li>暂无操作记录</li>';}
function empty(n,text='暂无记录'){return `<tr><td class="empty" colspan="${n}">${E(text)}</td></tr>`;}
const opts=(rows,current)=>rows.map(([value,label])=>`<option value="${E(value)}" ${value===current?'selected':''}>${E(label)}</option>`).join('');
function field(label,id,type='text',value='',extra=''){return `<label class="field">${E(label)}<input id="${id}" type="${type}" value="${E(value)}" ${extra}></label>`;}
function area(label,id,value='',max=1000){return `<label class="field full">${E(label)}<textarea id="${id}" maxlength="${max}" required>${E(value)}</textarea></label>`;}
function select(label,id,rows,value){return `<label class="field">${E(label)}<select id="${id}">${opts(rows,value)}</select></label>`;}
$('undo').onclick=()=>{if(!undoState)return;state=undoState;undoState=null;const warning=save();$('undo').disabled=true;closeModal();render();error(warning);toast('已恢复上一步之前的状态');};
$('export-json').onclick=()=>{C.download(APP_TITLE+'-项目备份.json',JSON.stringify(state,null,2));toast('项目备份已导出');};
$('import-json').onchange=async e=>{const file=e.target.files[0];if(!file)return;try{C.check(file.size<=5*1024*1024,'备份文件超过 5 MB');const next=Core.validate(JSON.parse(await file.text()));modal('导入项目备份','<p class="smalltext">文件已通过结构检查。导入会替换当前工作区，可使用“撤销上一步”恢复。建议先导出当前项目备份。</p><div class="modal-actions"><button class="primary" id="confirm-import">确认导入</button></div>');$('confirm-import').onclick=()=>{commit(next,'项目备份已导入');$('data-label').textContent='本地项目数据';closeModal();};}catch(err){error(err);}e.target.value='';};
$('load-demo').onclick=()=>{modal('恢复合成演示样例','<p class="smalltext">将载入新的合成演示数据，替换当前工作区。建议先导出当前 JSON 备份。可通过“撤销上一步”恢复本次替换前的数据。</p><div class="modal-actions"><button id="confirm-demo" class="primary">载入演示样例</button></div>');$('confirm-demo').onclick=()=>{commit(Core.demo(),'已载入演示样例');$('data-label').textContent='合成演示数据';closeModal();};};
