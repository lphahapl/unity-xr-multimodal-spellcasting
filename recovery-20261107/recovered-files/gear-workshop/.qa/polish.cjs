const fs=require('node:fs');const file=require('node:path').join(__dirname,'../dist/app.js');let s=fs.readFileSync(file,'utf8');
s=s.replace("function draw(){update();",`function fitView(){const stage=$('stage');if(innerWidth>800){stage.setAttribute('viewBox','0 0 1000 600');return;}const gs=state.gears,minX=Math.min(...gs.map(g=>g.x-C.radius(g.z)))-65,maxX=Math.max(...gs.map(g=>g.x+C.radius(g.z)))+65,minY=Math.min(...gs.map(g=>g.y-C.radius(g.z)))-80,maxY=Math.max(...gs.map(g=>g.y+C.radius(g.z)))+85;stage.setAttribute('viewBox',[minX,minY,maxX-minX,maxY-minY].join(' '));}
function labelPositions(g){const r=C.radius(g.z),blocked=y=>state.gears.some(o=>o!==g&&Math.hypot(g.x-o.x,g.y+y-o.y)<C.radius(o.z)+17);let title=-r-28,sub=r+35;if(blocked(title)){title=r+35;sub=r+56;}else if(blocked(sub))sub=-r-8;return{title,sub};}
let resizeTimer;addEventListener('resize',()=>{clearTimeout(resizeTimer);resizeTimer=setTimeout(fitView,80);});
function draw(){update();fitView();`);
s=s.replace("y:-radius-28,'text-anchor'","y:labelPositions(g).title,'text-anchor'").replace("y:radius+35,'text-anchor'","y:labelPositions(g).sub,'text-anchor'");
s=s.replace("snapshot.setAttribute('width','1000');","snapshot.setAttribute('viewBox','0 0 1000 600');snapshot.setAttribute('width','1000');");
fs.writeFileSync(file,s);
