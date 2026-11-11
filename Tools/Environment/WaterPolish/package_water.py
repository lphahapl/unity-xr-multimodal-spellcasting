"""Create deterministic Unity metadata and a self-contained, single-voice prefab."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / 'Assets/Art/Environment/Opening/WaterPolish'
def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'NarutoVR/WaterPolish/'+name).hex
def meta(path, importer):
    Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid(path.name)+'\n'+importer, encoding='utf-8')
script_meta = ROOT / 'Assets/scripts/logic/Environment/WaterAudio.cs.meta'
script_guid = re.search(r'^guid: ([a-f0-9]{32})$', script_meta.read_text(), re.M).group(1)
meta(BASE/'Canal_GentleFlow_Loop.wav', '''AudioImporter:
  externalObjects: {}
  serializedVersion: 6
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 32000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''')
# Reuse this Unity version's AudioSource serialization, without touching the scene.
scene = (ROOT/'Assets/Scene/Chapter01_Awakening.unity').read_text(encoding='utf-8')
block = re.search(r'^--- !u!82 &\d+\nAudioSource:\n.*?(?=^--- !u!)', scene, re.M|re.S).group()
block = re.sub(r'&\d+', '&82000', block, count=1)
block = re.sub(r'm_GameObject: \{fileID: \d+\}', 'm_GameObject: {fileID: 100001}', block)
clip = '{fileID: 8300000, guid: '+guid('Canal_GentleFlow_Loop.wav')+', type: 3}'
for field, value in dict(m_audioClip=clip, m_Resource=clip, m_Volume='0.19', Loop='1',
    DopplerLevel='0', MinDistance='1.2', MaxDistance='7.5', Priority='180',
    BypassReverbZones='1', rolloffMode='1').items():
    block = re.sub(r'^(  '+field+r': ).*$', r'\g<1>'+value, block, flags=re.M)
# panLevel is the serialized spatialBlend curve in Unity's AudioSource YAML.
start = block.index('  panLevelCustomCurve:')
end = block.index('  spreadCustomCurve:')
block = block[:start]+block[start:end].replace('value: 0', 'value: 1')+block[end:]

def game_object(id, name, components):
    return f'''--- !u!1 &{id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
'''+''.join(f'  - component: {{fileID: {c}}}\n' for c in components)+f'''  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
'''
def transform(id, go, father, child):
    return f'''--- !u!4 &{id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
'''+('  m_Children:\n  - {fileID: '+str(child)+'}\n' if child else '  m_Children: []\n')+f'''  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''
prefab = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
prefab += game_object(100000, 'WaterAudio', [40000, 114000])
prefab += transform(40000, 100000, 0, 40001)
prefab += f'''--- !u!114 &114000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 100000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  waterSource: {{fileID: 82000}}
  listener: {{fileID: 0}}
  volume: 0.19
  channelPoints:
  - {{x: 0.539, y: 0.13, z: 49.4}}
  - {{x: 0.539, y: 0.13, z: 60.9}}
'''
prefab += game_object(100001, 'CanalEmitter_OneVoice', [40001, 82000])
prefab += transform(40001, 100001, 40000, 0)
prefab += block
(BASE/'WaterAudio.prefab').write_text(prefab, encoding='utf-8')
meta(BASE/'WaterAudio.prefab', '''PrefabImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
''')
Path(str(BASE)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid('folder')+'''\nfolderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')
print(BASE/'WaterAudio.prefab')
