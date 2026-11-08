"""Convert only the 10 delivered board materials; retain GUIDs and user colors."""
from pathlib import Path
import re, json, shutil, hashlib

project=Path(r'D:\unity_training\Runnner&&Lucy')
backup=Path(__file__).resolve().parent/'材质修复备份'
backup.mkdir(exist_ok=True)
shader_meta=next((project/'Library/PackageCache').glob('com.unity.render-pipelines.universal@*/Shaders/Lit.shader.meta'))
guid=re.search(r'^guid: (\w+)',shader_meta.read_text(),re.M).group(1)
materials=sorted((project/'Assets/ModularBoard/Materials').glob('MB_*.mat'))
assert len(materials)==10
changes=[]
for path in materials:
    original=path.read_text(encoding='utf-8-sig')
    original_guid=re.search(r'm_Shader:.*guid: (\w+)',original).group(1)
    assert original_guid in ('0000000000000000f000000000000000',guid),path
    backup_file=backup/path.name
    if not backup_file.exists(): shutil.copy2(path,backup_file)
    if not (backup/(path.name+'.meta')).exists(): shutil.copy2(str(path)+'.meta',backup/(path.name+'.meta'))
    source=re.sub(r'm_Shader: \{[^\n]+\}',f'm_Shader: {{fileID: 4800000, guid: {guid}, type: 3}}',original,count=1)
    if not re.search(r'^    - _BaseMap:',source,re.M):
        source=source.replace('    - _MainTex:', '    - _BaseMap:')
    color=re.search(r'^    - _Color: (\{[^\n]+\})',source,re.M)
    assert color is not None,path
    if not re.search(r'^    - _BaseColor:',source,re.M):
        source=source.replace('    m_Colors:\n','    m_Colors:\n    - _BaseColor: '+color.group(1)+'\n')
    gloss=re.search(r'^    - _Glossiness: ([^\n]+)',source,re.M)
    values={'_Smoothness':gloss.group(1) if gloss else '.36','_Surface':'0','_Blend':'0','_Cull':'2','_AlphaClip':'0',
            '_ReceiveShadows':'1','_WorkflowMode':'1','_SrcBlendAlpha':'1','_DstBlendAlpha':'0','_AlphaToMask':'0',
            '_QueueOffset':'0','_EnvironmentReflections':'1'}
    additions=''.join(f'    - {key}: {value}\n' for key,value in values.items() if not re.search(r'^    - '+re.escape(key)+':',source,re.M))
    source=source.replace('    m_Floats:\n','    m_Floats:\n'+additions)
    if source!=original:
        path.write_text(source,encoding='utf-8',newline='\n')
    changes.append({'file':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(backup/'修复记录.json').write_text(json.dumps({'shader_guid':guid,'changed':changes},ensure_ascii=False,indent=2),encoding='utf-8')
print('Converted',len(changes),'materials to URP/Lit. Original .meta GUIDs unchanged. Backup:',str(backup))
