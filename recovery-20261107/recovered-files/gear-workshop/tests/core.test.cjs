const assert=require('node:assert/strict');const C=require('../dist/core.js');
const pair=[{id:'A',z:20,x:300,y:300},{id:'B',z:40,x:420,y:300}];
assert.equal(C.solve(pair,'A').ratios.B,-.5);
assert.equal(C.solve(pair,'B').ratios.A,-2);
const train=[...pair,{id:'C',z:60,x:620,y:300}];assert.equal(C.solve(train,'A').ratios.C,1/3);
// 中间轮齿数变化且保持啮合时，首末比不变。
const idler=[{id:'A',z:20,x:200,y:300},{id:'B',z:30,x:300,y:300},{id:'C',z:60,x:480,y:300}];assert.equal(C.solve(idler,'A').ratios.C,1/3);
assert.equal(C.solve([{id:'A',z:40,x:420,y:245},{id:'B',z:40,x:580,y:245},{id:'C',z:40,x:500,y:245+Math.sqrt(3)*80}],'A').conflicts.length>0,true);
assert.equal(C.solve([{id:'A',z:20,x:300,y:300},{id:'B',z:40,x:350,y:300}],'A').overlaps.length,1);
assert.equal(C.solve([{id:'A',z:20,x:300,y:300},{id:'B',z:40,x:800,y:300}],'A').ratios.B,undefined);
const snapped=C.snap({id:'B',z:40,x:431,y:300},[pair[0]]);assert.equal(snapped.x,420);
const nearTriangle=C.solve([{id:'A',z:20,x:200,y:300},{id:'B',z:20,x:280,y:300},{id:'C',z:20,x:240,y:230.718}],'A');assert.ok(nearTriangle.conflicts.length);
// 相位约束：接触点两侧“齿 / 槽”互补。
for(const phase of [0,.5,3.2]){const r=C.solve(pair,'A',phase),phi=0;const a=pair[0].z*(phi-r.angles.A),b=pair[1].z*(phi+Math.PI-r.angles.B);assert.ok(Math.abs(Math.cos(a+b)+1)<1e-8);}
for(const z of C.TEETH)assert.ok(!/NaN|Infinity/.test(C.gearPath(z)));
const data={format:'gear-workshop-v1',gears:pair,motor:'A',output:'B',speed:20,direction:1};assert.equal(C.validateDesign(data).gears.length,2);assert.throws(()=>C.validateDesign({...data,gears:[{id:'A',z:999,x:200,y:200}]}));assert.throws(()=>C.validateDesign({...data,gears:[pair[0],{...pair[0]}]}));
console.log('PASS: ratio, reverse, idler, cycle, overlap, disconnection, snapping, phase, profile, import validation');
