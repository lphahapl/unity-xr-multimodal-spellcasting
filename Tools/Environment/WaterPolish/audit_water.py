"""Static contract checks; Unity import/render acceptance is separate."""
from pathlib import Path
import hashlib
import json
import re
import wave

root = Path(__file__).resolve().parents[3]
base = root/'Assets/Art/Environment/Opening/WaterPolish'
shader = (root/'Assets/Shaders/CanalFlow.shader').read_text(encoding='utf-8')
code = (base/'WaterAudio.cs').read_text(encoding='utf-8')
prefab = (base/'WaterAudio.prefab').read_text(encoding='utf-8')
checks = {
    'one_shader_pass': len(re.findall(r'^\s*Pass\s*$', shader, re.M)) == 1,
    'one_texture_sample': shader.count('SAMPLE_TEXTURE2D(') == 1,
    'one_texture_declaration': shader.count('TEXTURE2D(') == 2,  # declaration + SAMPLE_TEXTURE2D
    'opaque': '"RenderType"="Opaque"' in shader and 'ZWrite On' in shader,
    'no_camera_depth_refraction_resources': not any(s in shader for s in ['CameraOpaqueTexture', 'CameraDepthTexture', 'GrabPass', 'Blend SrcAlpha']),
    'stereo_setup_retained': 'UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX' in shader,
    'one_audio_source': len(re.findall(r'^AudioSource:', prefab, re.M)) == 1,
    'duck_gain_read_only': 'duckingCoordinator.DuckGain' in code and 'Microphone.' not in code,
    'spatial_blend_curve': bool(re.search(r'panLevelCustomCurve:.*?value: 1', prefab, re.S)),
    'no_doppler': 'DopplerLevel: 0' in prefab,
}
for asset in ['WaterAudio.cs', 'Canal_GentleFlow_Loop.wav']:
    guid = re.search('guid: ([a-f0-9]{32})', (base/(asset+'.meta')).read_text()).group(1)
    checks['prefab_guid_'+asset] = guid in prefab
with wave.open(str(base/'Canal_GentleFlow_Loop.wav')) as w:
    checks['audio_format'] = (w.getnchannels(),w.getframerate(),w.getsampwidth(),w.getnframes()) == (1,32000,2,576000)
measure = json.loads((root/'Logs/exterior-polish-20261108/water/audio-measurements.json').read_text())
checks['no_clip'] = measure['clipped_samples'] == 0
checks['loop_step_within_two_ordinary_rms'] = measure['boundary_step_over_ordinary_rms'] < 2
files = [root/'Assets/Shaders/CanalFlow.shader', root/'Assets/Materials/Chapter01/CanalFlow.mat']
files += [p for p in base.rglob('*') if p.is_file()]
report = {'checks':checks,'passed':all(checks.values()),'hashes':{
    str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
    'not_measured':['GPU instruction count', 'Quest frame time', 'subjective listening', 'Unity render/import: parent clone acceptance']}
(root/'Logs/exterior-polish-20261108/water/static-audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(checks,indent=2))
raise SystemExit(0 if report['passed'] else 1)
