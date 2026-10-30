"""Apply image-informed starting weights; preserve recorded ranges and calibration flags."""
from pathlib import Path
import json, re, shutil, datetime
root = Path(__file__).resolve().parent.parent
selected = {
    '子': {'L.Index.Curl':2, 'R.Index.Curl':1,
           'Both.WristDistance':2.5, 'Both.PalmAngle':1, 'Both.RightWristInLeft.X':1,
           'Both.RightWristInLeft.Y':2, 'Both.RightWristInLeft.Z':1},
    '丑': {'L.Index.Curl':2, 'R.Index.Curl':2, 'L.Middle.Curl':1, 'R.Middle.Curl':1,
           'Both.WristDistance':1.5, 'Both.PalmAngle':1, 'Both.IndexTipDistance':1.5,
           'Both.IndexDirectionAngle':3, 'Both.RightWristInLeft.Y':1, 'Both.RightWristInLeft.Z':0.5},
    '寅': {'L.Index.Curl':3, 'R.Index.Curl':3, 'L.Middle.Curl':1.5, 'R.Middle.Curl':1.5,
           'Both.IndexTipDistance':3, 'Both.IndexDirectionAngle':2, 'Both.MiddleTipDistance':1,
           'Both.WristDistance':1, 'Both.PalmAngle':0.5}
}
path = root / 'Assets/SealLab/Resources/SealTestWeights.json'
backup = root / 'Logs' / ('BeforeVisibilityWeights-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
backup.mkdir(parents=True)
shutil.copy2(path, backup / path.name)
data = json.loads(path.read_text(encoding='utf-8-sig'))
overrides = {'profiles': []}
for p in data['profiles']:
    if p['sealName'] not in selected: continue
    weights = selected[p['sealName']]
    for r in p['rules']:
        fid = r['featureId']
        r['weight'] = weights.get(fid, 0)
        r['required'] = False
        r['tolerance'] = 25 if fid.endswith(('Curl','Flexion','Abduction','Angle')) else 0.03 if ('Wrist' in fid) else 0.025
    overrides['profiles'].append(p)
    asset = root / 'Assets/SealLab/Profiles' / (p['sealName'] + '.asset')
    shutil.copy2(asset, backup / asset.name)
    original = asset.read_text(encoding='utf-8-sig')
    config = {r['featureId']: r for r in p['rules']}
    def patch(m):
        block = m.group(0); fid = m.group(1).replace('.Max.', '.Pinky.')
        setting = config[fid]
        block = re.sub(r'(?m)^(    weight:) .*$', lambda _: '    weight: ' + str(setting['weight']), block)
        block = re.sub(r'(?m)^(    tolerance:) .*$', lambda _: '    tolerance: ' + str(setting['tolerance']), block)
        return re.sub(r'(?m)^    required: .*$', '    required: 0', block)
    changed = re.sub(r'  - featureId: (\S+)\n(?:(?!  - featureId:)[\s\S])*?(?=  - featureId:|\Z)', patch, original)
    assert re.findall(r'(?m)^    (?:min|max): .*$', original) == re.findall(r'(?m)^    (?:min|max): .*$', changed)
    assert re.search(r'calibrated: \d', original).group() == re.search(r'calibrated: \d', changed).group()
    asset.write_text(changed, encoding='utf-8')
path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
(root/'Assets/SealLab/Resources/SealVisibilityWeights.json').write_text(json.dumps(overrides,ensure_ascii=False,indent=2),encoding='utf-8')
print('Backup:', backup)
print(json.dumps({k: {'active':len(v),'total':sum(v.values())} for k,v in selected.items()},ensure_ascii=True))
