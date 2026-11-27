import http from 'node:http';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const here=path.dirname(fileURLToPath(import.meta.url));
const vendor=path.resolve(here,'../../../../SharedWorkSpace/vendor');
const types={'.html':'text/html; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.glb':'model/gltf-binary','.png':'image/png'};
http.createServer(async(req,res)=>{
 try {
  if(req.method!=='GET'&&req.method!=='HEAD') {res.writeHead(405);return res.end();}
  const url=new URL(req.url,'http://localhost');
  const isVendor=url.pathname.startsWith('/vendor/');
  const base=isVendor?vendor:here;
  const relative=decodeURIComponent(isVendor?url.pathname.slice(8):url.pathname.slice(1)||'index.html');
  const target=path.resolve(base,relative);
  if(!target.startsWith(base+path.sep)) {res.writeHead(403);return res.end();}
  const bytes=await readFile(target);
  res.writeHead(200,{'Content-Type':types[path.extname(target)]||'application/octet-stream','Cache-Control':'no-store'});
  res.end(req.method==='HEAD'?undefined:bytes);
 } catch {res.writeHead(404);res.end('Not found');}
}).listen(4196,'127.0.0.1',()=>console.log('Nib prototype http://127.0.0.1:4196/'));
