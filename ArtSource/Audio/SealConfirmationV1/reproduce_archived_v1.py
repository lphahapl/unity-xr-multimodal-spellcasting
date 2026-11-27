"""Reproduce V1 in its archive without overwriting the currently deployed audio."""
from pathlib import Path
import runpy, sys, hashlib, json

HERE=Path(__file__).resolve().parent
ARCHIVE=HERE/'Wav'
OUT=HERE/'Reproduced'
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'Wav').mkdir(exist_ok=True)
expected=json.loads((ARCHIVE/'sha256.json').read_text(encoding='utf-8'))
state=runpy.run_path(str(HERE/'render_seal_chimes.py'))
main=state['main']; context=main.__globals__
context['ASSETS']=OUT/'Wav'
context['REFERENCE']=ARCHIVE/'LvChime.wav'
context['HERE']=OUT
sys.argv=[str(HERE/'render_seal_chimes.py'),'--stage','all']
main()
rows=[]
for name,sha in expected.items():
    candidate=ARCHIVE/name if name=='LvChime.wav' else OUT/'Wav'/name
    got=hashlib.sha256(candidate.read_bytes()).hexdigest()
    rows.append({'file':name,'sha256':got,'matches_archived_v1':got==sha})
assert all(x['matches_archived_v1'] for x in rows)
(OUT/'reproduction-check.json').write_text(json.dumps({'all_six_match':True,'production_assets_written':False,'files':rows},indent=2),encoding='utf-8')
print('Archived V1 reproduced exactly; current production Assets were not written.')
