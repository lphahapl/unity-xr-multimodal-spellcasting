import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {NibDragState,deformPosition} from './drag-core.mjs';

const $=id=>document.getElementById(id);
const TIP=.108,ROOT=.0677,DURATION=6;
const gltf=await new GLTFLoader().loadAsync('./MoGuiPen_LOD0.glb');
let original;
gltf.scene.traverse(o=>{if(o.isMesh) original=o;});
if(!original)throw Error('Original mesh missing');
const rest=original.geometry.attributes.position.array.slice();
const tipIndices=[];
for(let i=0;i<rest.length;i+=3)if(Math.abs(rest[i+2]-TIP)<1e-7)tipIndices.push(i/3);
const penQ=new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0,0,1),new THREE.Vector3(-.23,-.25,-1).normalize());
const inversePenQ=penQ.clone().invert();
const logicalTip=new THREE.Vector3(0,0,TIP);
let playing=true,time=0,last=performance.now(),scenario='line',amplitude=.0045;

function motion(t,name){
 let x=-.022,y=0,z=0,contact=t>=.4&&t<3.8,tracked=true,phase='接近板面';
 if(t<.4)z=.012*(1-t/.4);
 if(t>=.4&&t<1.8){x+=.025*(t-.4);phase='向右运笔';}
 else if(t>=1.8&&t<2.6){x=.013;phase='停笔回正';}
 else if(t>=2.6&&t<3.8){x=.013-.026*(t-2.6);phase='向左运笔';}
 else if(t>=3.8){x=-.0182;z=Math.min(.012,(t-3.8)*.04);phase='抬笔回正';}
 if(name==='corner'){
  if(t>=1.8&&t<3.8){x=.013;y=Math.min(.025,(t-1.8)*.025);phase=t<2.8?'直角转向上行':'停笔回正';}
  if(t>=3.8){x=.013;y=.025;}
 }
 if(name==='circle'){
  const a=Math.max(0,Math.min(3.4,t-.4))*2*Math.PI/2;
  x=.015*Math.cos(a);y=.015*Math.sin(a);
  if(contact)phase='圆形运笔';
 }
 if(name==='normal'){x=0;y=0;if(contact){z=.012*Math.sin((t-.4)*5);phase='沿法线压入，切向速度为零';}}
 if(name==='loss'&&t>=1.1&&t<1.55){tracked=false;phase='短暂失追，回正';}
 return {time:t,point:[x,y,z],contact,tracked,boardId:'same-board',phase};
}

function replay(t,name,amp){
 const state=new NibDragState({maxBend:amp,gainSeconds:.08});
 let latest,previousInput=null,velocity=[0,0,0];const lines=[];let lastDrawn=null;
 const times=[];for(let s=0;s<t-1e-8;s+=1/120)times.push(s);times.push(t);
 for(const s of times){
  const input=motion(s,name);latest=state.update(input);
  velocity=[0,0,0];
  if(input.contact&&input.tracked&&previousInput?.contact&&previousInput.tracked&&s>previousInput.time){
   velocity=[(input.point[0]-previousInput.point[0])/(s-previousInput.time),(input.point[1]-previousInput.point[1])/(s-previousInput.time),0];
  }
  previousInput=input;
  if(input.contact&&input.tracked){
   const p=new THREE.Vector3(input.point[0],input.point[1],.00008);
   if(!lastDrawn)lastDrawn=p.clone();
   else if(p.distanceTo(lastDrawn)>.0025){lines.push(lastDrawn.x,lastDrawn.y,.00008,p.x,p.y,.00008);lastDrawn=p.clone();}
  }else {lastDrawn=null;}
 }
 return {state:latest,input:motion(t,name),lines,velocity};
}

const views=['a','b','c'].map((id,index)=>{
 const renderer=new THREE.WebGLRenderer({canvas:$(id),antialias:true,preserveDrawingBuffer:true});
 renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.setClearColor(0x192831);renderer.outputColorSpace=THREE.SRGBColorSpace;
 renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.4;
 const scene=new THREE.Scene(),planeRoot=new THREE.Group();scene.add(planeRoot);
 scene.add(new THREE.HemisphereLight(0xd8eeff,0x554633,3));
 for(const [pos,intensity] of [[[.2,.25,.3],5],[[-.2,.03,.15],2]]){const l=new THREE.DirectionalLight(0xffe4ba,intensity);l.position.set(...pos);scene.add(l);}
 const board=new THREE.Mesh(new THREE.PlaneGeometry(.18,.15),new THREE.MeshStandardMaterial({color:0x34474b,roughness:.9,side:THREE.DoubleSide}));planeRoot.add(board);
 const grid=[];for(let k=-8;k<=8;k++){grid.push(k*.01,-.075,.00002,k*.01,.075,.00002);grid.push(-.09,k*.01,.00002,.09,k*.01,.00002);}
 const gridGeo=new THREE.BufferGeometry();gridGeo.setAttribute('position',new THREE.Float32BufferAttribute(grid,3));planeRoot.add(new THREE.LineSegments(gridGeo,new THREE.LineBasicMaterial({color:0x56737a,transparent:true,opacity:.35})));
 const pen=new THREE.Mesh(original.geometry.clone(),original.material.clone());pen.material.envMapIntensity=.7;
 pen.quaternion.copy(penQ);planeRoot.add(pen);
 const marker=new THREE.Mesh(new THREE.SphereGeometry(.0008,12,8),new THREE.MeshBasicMaterial({color:0x59e0e3,depthTest:false}));marker.renderOrder=10;planeRoot.add(marker);
 const arrow=new THREE.ArrowHelper(new THREE.Vector3(1,0,0),new THREE.Vector3(),.012,0xf5c67b,.004,.002);planeRoot.add(arrow);
 const ink=new THREE.LineSegments(new THREE.BufferGeometry(),new THREE.LineBasicMaterial({color:0xff5432}));planeRoot.add(ink);
 const camera=new THREE.OrthographicCamera(-.07,.07,.07,-.07,.001,3);
 return {id,index,renderer,scene,planeRoot,pen,marker,arrow,ink,camera,metrics:{}};
});

function draw(t){
 const data=replay(t,scenario,amplitude),input=data.input;
 const bend=new THREE.Vector3(...data.state.bend).applyQuaternion(inversePenQ);
 const point=new THREE.Vector3(...input.point);if(input.contact)point.z=0;
 const angle=Number($('board').value)*Math.PI/180;
 for(const v of views){
  v.planeRoot.rotation.y=angle;
  const pos=v.pen.geometry.attributes.position;let maxMoved=0,rigidMoved=0,tipError=0;
  for(let i=0;i<rest.length;i+=3){
   const p=[rest[i],rest[i+1],rest[i+2]];let d=p;
   if(v.index===1)d=deformPosition(p,bend.toArray(),{rootZ:ROOT,tipZ:TIP});
   if(v.index===2&&p[2]>ROOT&&p[2]<=TIP+1e-7){const u=Math.min(1,Math.max(0,(p[2]-ROOT)/(TIP-ROOT))),w=u*u;d=[p[0]+bend.x*w,p[1]+bend.y*w,p[2]+bend.z*w];}
   pos.setXYZ(i/3,...d);const movement=Math.hypot(d[0]-p[0],d[1]-p[1],d[2]-p[2]);maxMoved=Math.max(maxMoved,movement);
   if(p[2]<=ROOT)rigidMoved=Math.max(rigidMoved,movement);
  }
  pos.needsUpdate=true;v.pen.geometry.computeVertexNormals();v.pen.geometry.computeBoundingSphere();
  v.pen.position.copy(point).sub(logicalTip.clone().applyQuaternion(penQ));
  for(const i of tipIndices)tipError=Math.max(tipError,new THREE.Vector3().fromBufferAttribute(pos,i).distanceTo(logicalTip));
  v.marker.position.copy(point);v.marker.visible=$('markers').checked;
  const velocity=new THREE.Vector3(...data.velocity);
  v.arrow.visible=$('markers').checked&&data.state.speed>.005;
  if(velocity.length()>1e-8)v.arrow.setDirection(velocity.normalize());
  v.arrow.position.copy(point).add(new THREE.Vector3(0,-.009,.001));
  v.ink.geometry.dispose();v.ink.geometry=new THREE.BufferGeometry();v.ink.geometry.setAttribute('position',new THREE.Float32BufferAttribute(data.lines,3));
  const rect=v.renderer.domElement.getBoundingClientRect();const width=Math.round(rect.width),height=Math.round(rect.height);
  v.renderer.setSize(width,height,false);
  const whole=$('view').value==='whole',span=whole?.15:.046;
  v.camera.left=-span*width/height;v.camera.right=span*width/height;v.camera.top=span;v.camera.bottom=-span;
  const target=(whole?new THREE.Vector3(0,.006,.08):point.clone().add(new THREE.Vector3(0,.001,.018))).applyAxisAngle(new THREE.Vector3(0,1,0),angle);
  v.camera.position.copy(target).add(new THREE.Vector3(.035,.18,.065));v.camera.lookAt(target);v.camera.updateProjectionMatrix();
  v.renderer.render(v.scene,v.camera);
  v.metrics={maxVertexBendMm:maxMoved*1000,visibleTipToNibMm:tipError*1000,rigidVertexMovementMm:rigidMoved*1000,logicalNib:point.toArray(),samplePoints:data.lines.length/6};
  $('m'+v.id).innerHTML=`笔锋最大位移 ${(maxMoved*1000).toFixed(2)} mm<br>可见尖端距 Nib ${(tipError*1000).toFixed(3)} mm<br>笔杆位移 ${(rigidMoved*1000).toFixed(3)} mm`;
 }
 $('phase').textContent=`${t.toFixed(2)} s · ${input.phase} · 切向速度 ${(data.state.speed*1000).toFixed(0)} mm/s`;
 $('scrub').value=t;
 $('status').textContent='原模型 2,228 三角面 · 保留原顶点、UV、纹理与索引 · 候选最大拖弯 '+(amplitude*1000).toFixed(1)+' mm · 未写入 Unity Assets';
 return {time:t,phase:input.phase,state:data.state,views:views.map(v=>v.metrics)};
}

function animate(now){const dt=Math.min(.1,(now-last)/1000);last=now;if(playing)time=(time+dt)%DURATION;draw(time);requestAnimationFrame(animate);}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放';};
$('restart').onclick=()=>{time=0;playing=true;$('play').textContent='暂停';};
$('scrub').oninput=()=>{playing=false;time=Number($('scrub').value);$('play').textContent='播放';draw(time);};
$('scenario').onchange=()=>{scenario=$('scenario').value;time=0;};
$('amplitude').oninput=()=>{amplitude=Number($('amplitude').value)/1000;$('amp').textContent=(amplitude*1000).toFixed(1)+' mm';};
window.penPrototype={ready:true,geometryIdentity:{vertices:rest.length/3,triangles:original.geometry.index.count/3,uvUnchanged:views.every(v=>v.pen.geometry.attributes.uv.array.every((x,i)=>x===original.geometry.attributes.uv.array[i])),indicesUnchanged:views.every(v=>v.pen.geometry.index.array.every((x,i)=>x===original.geometry.index.array[i]))},set({t=1.4,mode='line',amp=4.5,board=0,view='close',markers=true}={}){playing=false;time=t;scenario=mode;amplitude=amp/1000;$('scenario').value=mode;$('board').value=String(board);$('view').value=view;$('markers').checked=markers;$('amplitude').value=String(amp);$('amp').textContent=amp+' mm';$('play').textContent='播放';return draw(t);},replay,motion,rest:Array.from(rest),tipIndices};
requestAnimationFrame(animate);
