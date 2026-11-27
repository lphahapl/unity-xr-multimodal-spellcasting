"""Keep desktop pickup checks out of the system XR runtime."""
import os
from pathlib import Path
import re
import struct

def isolate(project):
    project = Path(project).resolve()
    if not project.is_relative_to(Path('D:/Temp').resolve()) or (project/'Temp/UnityLockfile').exists():
        raise RuntimeError('Expected an idle disposable project in D:/Temp')
    settings = project/'Assets/XR/XRGeneralSettingsPerBuildTarget.asset'
    config = (project/'ProjectSettings/EditorBuildSettings.asset').read_text('utf-8-sig')
    guid = re.search(r'(?m)^guid: (\w+)',Path(str(settings)+'.meta').read_text()).group(1)
    if not re.search(r'com\.unity\.xr\.management\.loader_settings: {fileID: 11400000, guid: '+guid+r',', config):
        raise ValueError('Wrong XR settings asset')
    raw = settings.read_text('utf-8-sig')
    parts = re.split(r'(?m)(?=^--- !u!114 &)',raw)
    ids = {p.splitlines()[0].split('&')[1]:i for i,p in enumerate(parts) if p.startswith('---')}
    root = parts[ids['11400000']]
    packed = bytes.fromhex(re.search(r'Keys: (\w+)',root).group(1))
    keys = [v for (v,) in struct.iter_unpack('<i',packed)]
    refs = re.findall(r'(?m)^  - {fileID: (-?\d+)}',root)
    if len(refs)!=len(keys) or keys.count(1)!=1: raise ValueError('Unknown build target map')
    index = ids[refs[keys.index(1)]]
    manager = ids[re.search(r'm_LoaderManagerInstance: {fileID: (-?\d+)}',parts[index]).group(1)]
    parts[index], matches = re.subn(r'(?m)^  m_InitManagerOnStart: [01]$', '  m_InitManagerOnStart: 0', parts[index])
    if matches!=1: raise ValueError('No unique init flag')
    parts[manager], matches = re.subn(r'(?m)^  m_Loaders:(?: \[\]|(?:\n  - \{[^\n]+\})*)$', '  m_Loaders: []', parts[manager])
    if matches!=1: raise ValueError('No unique loader list')
    result = ''.join(parts)
    if raw!=result:
        if not settings.resolve().is_relative_to(project): raise ValueError('Shared directory link')
        temp=settings.with_suffix('.pickup-xr-tmp')
        with temp.open('x',encoding='utf-8',newline='\n') as handle: handle.write(result)
        os.replace(temp,settings)
    if settings.read_text('utf-8-sig')!=result: raise RuntimeError('XR setup readback failed')
    paths = project/'Assets'
    start = re.compile(r'\b(?:InitializeLoader(?:Sync)?|StartSubsystems|AttemptInitializeXRSDKOnLoad|AttemptStartXRSDKOnBeforeSplashScreen)\s*\(')
    for f in paths.rglob('*.cs'):
        if 'Editor' in f.relative_to(paths).parts and f.name not in ('HandPickupPlayChecks.cs','RationPickupChecks.cs','GlyphHandPickupChecks.cs','StoryPickupChecks.cs'):
            continue
        if start.search(f.read_text('utf-8-sig',errors='replace')): raise RuntimeError(f'XR startup in {f}')
    runtime=project/'Temp'/'pickup-no-hardware-runtime.json'
    if runtime.exists(): raise RuntimeError('XR_RUNTIME_JSON must point to a nonexistent file')
    print(f'Standalone asset {guid} general {refs[keys.index(1)]}: init=false, loaders=[], no scripted XR startup',flush=True)
    return {**os.environ,'XR_RUNTIME_JSON':str(runtime),'PICKUP_ISOLATED_PROJECT':str(project),'ALLUSERSPROFILE':'C:/ProgramData'}
