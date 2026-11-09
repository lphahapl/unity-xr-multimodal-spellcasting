from pathlib import Path
p=Path(__file__).resolve().parent
replacements={
'inspect.py':[('scale=3/(hi.x-lo.x)','scale=5/(hi.z-lo.z)'),('3 m across X','5 m tall'),('cam.data.ortho_scale=4.8','cam.data.ortho_scale=8.0'),('Vector((0,0,1.1))','Vector((0,0,2.5))'),('(4,-5,3.5)','(6,-10,5.5)'),('(-4,5,3.5)','(-6,10,5.5)'),('(-4,-5,3.5)','(-6,-10,5.5)'),('(0,-.5,7)','(0,-.5,12)')],
'build.py':[('3100','4400'),('2500<=tris(low)<=3500','3500<=tris(low)<=5000')],
'finalize.py':[('factor=3/(hi.x-lo.x)','factor=5/(hi.z-lo.z)'),('exact 3m width','exact 5m height'),('cam.data.ortho_scale=4.8','cam.data.ortho_scale=8.0'),('Vector((0,0,1.1))','Vector((0,0,2.5))'),('(4,-5,3.5)','(6,-10,5.5)'),('(-4,5,3.5)','(-6,10,5.5)'),('(-4,-5,3.5)','(-6,-10,5.5)'),('(0,-.5,7)','(0,-.5,12)')],
'verify_fbx.py':[('2500<=r[','3500<=r['),('<=3500','<=5000'),("r['bounds_blender_xyz'][1][0]-r['bounds_blender_xyz'][0][0]-3","r['bounds_blender_xyz'][1][2]-r['bounds_blender_xyz'][0][2]-5")]
}
for name,pairs in replacements.items():
 t=(p/name).read_text(encoding='utf8')
 for a,b in pairs:t=t.replace(a,b)
 (p/name).write_text(t,encoding='utf8')
