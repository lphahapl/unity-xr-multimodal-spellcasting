"""Regression check for the pink ModularBoard materials in the user's URP project."""
from pathlib import Path
import re, json, sys

project=Path(sys.argv[1] if len(sys.argv)>1 else r'D:\unity_training\Runnner&&Lucy')
shader_meta=next((project/'Library/PackageCache').glob('com.unity.render-pipelines.universal@*/Shaders/Lit.shader.meta'))
shader_guid=re.search(r'^guid: (\w+)',shader_meta.read_text(),re.M).group(1)
materials=sorted((project/'Assets/ModularBoard/Materials').glob('MB_*.mat'))
bad=[]
for mat in materials:
    source=mat.read_text(encoding='utf-8-sig')
    guid=re.search(r'm_Shader:.*guid: (\w+)',source).group(1)
    if guid!=shader_guid: bad.append(f'{mat.name}: incompatible shader {guid}')
    elif not re.search(r'^    - _BaseColor:',source,re.M): bad.append(f'{mat.name}: missing URP BaseColor')
    elif not re.search(r'^    - _BaseMap:',source,re.M): bad.append(f'{mat.name}: missing URP BaseMap')
if len(materials)!=10: bad.append(f'Expected 10 materials, found {len(materials)}')
print(json.dumps({'project':str(project),'expected_shader':'Universal Render Pipeline/Lit','shader_guid':shader_guid,'checked':len(materials),'failures':bad,'pass':not bad},ensure_ascii=False,indent=2))
sys.exit(1 if bad else 0)
