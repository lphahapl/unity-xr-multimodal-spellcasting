import json,sys,bpy
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path.home()/'.codex'/'skills'/'blender-asset-qa'/'scripts'))
import qa_check
w=Path(sys.argv[sys.argv.index('--')+1]); name=bpy.path.display_name_from_filepath(bpy.data.filepath).replace('_pipeline','')
result={}
for n in range(3):
 ob=name+'_Tier'+str(n)
 r=qa_check.run(objects=[ob],profile='game',eps=.0002,tol=.003)
 result[ob]=r
 print(ob,r['pass'],r['errors'][:6],r['warnings'][:4],flush=True)
(w/'game-qa.json').write_text(json.dumps(result,indent=2))
