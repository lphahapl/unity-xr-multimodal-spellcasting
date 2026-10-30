"""Read the supplied workbook without modifying it; export the first matrix sheet."""
import json, re, zipfile, xml.etree.ElementTree as ET
from pathlib import Path
root = Path(__file__).resolve().parent.parent
ns = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(root / 'NarutoVR_12Seals_FeatureConfig.xlsx') as z:
    shared = [''.join(n.itertext()) for n in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si', ns)] if 'xl/sharedStrings.xml' in z.namelist() else []
    cells = {}
    for c in ET.fromstring(z.read('xl/worksheets/sheet1.xml')).findall('.//s:sheetData/s:row/s:c', ns):
        if c.find('s:f', ns) is not None:
            raise ValueError('Formula requires evaluated review: ' + c.attrib['r'])
        value = c.findtext('s:v', '', ns)
        cells[c.attrib['r']] = shared[int(value)] if c.attrib.get('t') == 's' else ''.join(c.find('s:is', ns).itertext()) if c.attrib.get('t') == 'inlineStr' else value
    result = {'profiles': []}
    for col in 'FGHIJKLMNOPQ':
        p = {'sealName': cells[col+'3'].split('\n')[0].removesuffix('印'), 'threshold': float(cells[col+'5']), 'minimumCoverage': float(cells[col+'6']), 'holdSeconds': float(cells[col+'7']), 'rules': []}
        for row in range(11, 146, 3):
            p['rules'].append({'featureId': cells['C'+str(row)], 'weight': float(cells[col+str(row)]), 'tolerance': float(cells[col+str(row+1)]), 'required': cells[col+str(row+2)].startswith('是')})
        assert len({r['featureId'] for r in p['rules']}) == 45
        assert all(r['weight'] >= 0 and r['tolerance'] > 0 for r in p['rules'])
        result['profiles'].append(p)
    override = root / 'Assets/SealLab/Resources/SealVisibilityWeights.json'
    if override.exists():
        reviewed = {p['sealName']: p for p in json.loads(override.read_text(encoding='utf-8'))['profiles']}
        result['profiles'] = [reviewed.get(p['sealName'], p) for p in result['profiles']]
    dest = root / 'Assets/SealLab/Resources/SealTestWeights.json'
    dest.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps([{'seal': p['sealName'], 'totalWeight': round(sum(r['weight'] for r in p['rules']), 2), 'required': [r['featureId'] for r in p['rules'] if r['required']]} for p in result['profiles']], ensure_ascii=True))
