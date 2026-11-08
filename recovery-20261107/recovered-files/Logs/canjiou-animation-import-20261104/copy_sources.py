import hashlib
import json
import shutil
from pathlib import Path

downloads = Path.home() / 'Downloads'
target = Path('Assets/SealCombat/Art/Enemies/CanJiOu/Animations')
target.mkdir(exist_ok=True)
mapping = [
    ('Standing Idle.fbx', 'CanJiOu_Idle', True),
    ('Walking.fbx', 'CanJiOu_WalkForward_RootMotion', True),
    ('Standing Run Forward.fbx', 'CanJiOu_RunForward_RootMotion', True),
    ('Standing Run Forward (1).fbx', 'CanJiOu_RunForward_InPlace', True),
    ('Standing Turn Left 90.fbx', 'CanJiOu_TurnLeft90', False),
    ('Standing Turn Right 90.fbx', 'CanJiOu_TurnRight90', False),
    ('Standing Melee Attack Downward.fbx', 'CanJiOu_AttackDownward', False),
    ('Standing Melee Attack 360 High.fbx', 'CanJiOu_AttackSpin360', False),
    ('Standing React Large Gut.fbx', 'CanJiOu_HitReactAbdomen', False),
    ('Dying.fbx', 'CanJiOu_ParalysisEnter', False),
    ('Sitting Dazed.fbx', 'CanJiOu_DazedSeated', True),
]
manifest = []
for source, name, loop in mapping:
    path = target / (name + '.fbx')
    if path.exists():
        raise RuntimeError('Destination already exists: ' + str(path))
    shutil.copy2(downloads / source, path)
    source_hash = hashlib.sha256((downloads / source).read_bytes()).hexdigest()
    assert source_hash == hashlib.sha256(path.read_bytes()).hexdigest()
    manifest.append(dict(source=source, name=name, asset=str(path).replace('\\', '/'), loop=loop, sha256=source_hash))
Path(__file__).with_name('manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
print('Copied and verified', len(manifest), 'FBX files')
