(function(g){'use strict';
const check=(ok,msg)=>{if(!ok)throw Error(msg);};
function str(v,label,max=200,blank=false){check(typeof v==='string',label+'须为文本');check(v.length<=max,label+'过长');check(blank||v.trim().length>0,label+'不能为空');return v.trim();}
function date(v){if(typeof v!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(v))return false;const d=new Date(v+'T00:00:00Z');return Number.isFinite(+d)&&d.toISOString().slice(0,10)===v;}
function time(v){return typeof v==='string'&&/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(v)&&date(v.slice(0,10))&&+v.slice(11,13)<24&&+v.slice(14,16)<60;}
const stamp=v=>Date.parse(v+'Z');
function unique(xs,key){check(new Set(xs.map(x=>x[key])).size===xs.length,'记录编号重复');}
function base(s,kind){check(s&&s.kind===kind&&s.schemaVersion===1,'项目类型或文件版本不匹配');check(Array.isArray(s.history)&&s.history.length<=5000,'操作记录格式错误或超限');check(Number.isSafeInteger(s.seq)&&s.seq>=0,'序号无效');unique(s.history,'seq');let prev=0;for(const h of s.history){check(Number.isSafeInteger(h.seq)&&h.seq>prev&&h.seq<=s.seq,'操作记录顺序错误');prev=h.seq;str(h.actor,'操作者',50);str(h.type,'记录类型',50);str(h.detail,'记录说明',2000);check(date(h.at)||time(h.at),'记录时间无效');}}
function list(xs,max=500){check(Array.isArray(xs)&&xs.length<=max,'记录数量或格式无效');unique(xs,'id');for(const x of xs)str(x.id,'记录编号',40);}
function log(s,type,detail,actor){s.history.push({seq:++s.seq,type,detail,actor,at:s.clock});check(s.history.length<=5000,'操作记录已达 5000 条，请另建项目');}
function id(xs,prefix){return prefix+String(Math.max(0,...xs.map(x=>+(x.id.replace(/^\D+/,''))||0))+1).padStart(4,'0');}
const copy=s=>JSON.parse(JSON.stringify(s));
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function csv(rows,headers){return '\uFEFF'+[headers,...rows.map(r=>headers.map(h=>r[h]??''))].map(r=>r.map(v=>{let s=String(v);if(typeof v!=='number'&&/^\s*[=+\-@\t\r]/.test(s))s="'"+s;return '"'+s.replace(/"/g,'""')+'"';}).join(',')).join('\r\n');}
function download(name,body,type='application/json'){const u=URL.createObjectURL(new Blob([body],{type})),a=document.createElement('a');a.href=u;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(u),1500);}
g.C={check,str,date,time,stamp,base,list,log,id,copy,esc,csv,download,unique};if(typeof module!=='undefined')module.exports=g.C;
})(typeof window!=='undefined'?window:globalThis);
