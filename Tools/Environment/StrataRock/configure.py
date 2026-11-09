from pathlib import Path
p=Path(__file__).resolve().parent
replacements={
'inspect.py':[('scale=6/(hi.x-lo.x)','scale=3/(hi.x-lo.x)'),('6 m across X','3 m across X'),('cam.data.ortho_scale=8','cam.data.ortho_scale=4.8'),('Vector((0,0,2))','Vector((0,0,1.1))'),('(8,-10,7)','(4,-5,3.5)'),('(-8,10,7)','(-4,5,3.5)'),('(-8,-10,7)','(-4,-5,3.5)'),('(0,-1,14)','(0,-.5,7)')],
'build.py':[('9400','3100'),('2048','1024'),('8000<=tris(low)<=10000','2500<=tris(low)<=3500')],
'finalize.py':[('factor=6/(hi.x-lo.x)','factor=3/(hi.x-lo.x)'),('exact 6m','exact 3m'),('cam.data.ortho_scale=9.4','cam.data.ortho_scale=4.8'),('Vector((0,0,2.7))','Vector((0,0,1.1))'),('(8,-10,7.6)','(4,-5,3.5)'),('(-8,10,7.6)','(-4,5,3.5)'),('(-8,-10,7.6)','(-4,-5,3.5)'),('(0,-1,14)','(0,-.5,7)'),('2048','1024')],
'verify_fbx.py':[('8000<=r[','2500<=r['),('<=10000','<=3500'),('[0][0]-6)','[0][0]-3)')],
'unity_metadata.py':[(".replace('maxTextureSize: 1024','maxTextureSize: 2048')",'')]
}
for name,pairs in replacements.items():
 t=(p/name).read_text(encoding='utf8')
 for a,b in pairs:t=t.replace(a,b)
 (p/name).write_text(t,encoding='utf8')
