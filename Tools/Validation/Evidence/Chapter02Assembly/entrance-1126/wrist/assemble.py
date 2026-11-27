"""Copy only the approved wrist UI service and the physical case prefab into a new chapter scene file."""
from pathlib import Path
import re, sys

def blocks(data):
    parts = re.split(r"(?m)(?=^--- !u!)", data)[1:]
    return {re.match(r"--- !u!\d+ &(-?\d+)", part)[1]: part for part in parts}

def assemble(project, source, output):
    donor = (project / 'Assets/Scene/Chapter01_Courtyard.unity').read_text('utf-8')
    original = source.read_bytes()
    eol = '\r\n' if b'\r\n' in original else '\n'
    scene = original.decode('utf-8').replace('\r\n','\n')
    first, second = blocks(donor), blocks(scene)
    ids = ['121289189','121289190','121289191','121289192','505742255','505742256']
    assert not second.keys() & set(ids), 'Local ID collision or wrist service already installed'
    assert 'Rpg.WristMenuController' not in scene and scene.count('  wristMenu: {fileID: 0}\n') == 1
    assert 'guid: c1eb1761084677d43afc9e01ce8c014b' not in scene, 'Check existing worn case'
    graft = ''.join(first[key] for key in ids)
    # The first-chapter copper leaf puzzle is a scene-only addition, outside the wrist UI service.
    graft, n = re.subn(r'    m_AddedGameObjects:\n(?:    - targetCorrespondingSourceObject:[^\n]*\n      insertIndex: -1\n      addedObject: \{fileID: 516507892\}\n)', '    m_AddedGameObjects: []\n', graft)
    assert n == 1
    refs = set(re.findall(r'\{fileID: (-?\d+)\}', graft)) - {'0'}
    assert refs <= set(ids), refs - set(ids)
    roots = next(part for part in second.values() if '\nSceneRoots:\n' in part)
    assert scene.endswith(roots)
    result = scene.replace('  wristMenu: {fileID: 0}\n', '  wristMenu: {fileID: 121289191}\n')
    result = result[:-len(roots)] + graft + roots.rstrip('\n') + '\n  - {fileID: 121289192}\n  - {fileID: 505742255}\n'
    assert len(blocks(result)) == len(second) + len(ids)
    with output.open('xb') as f: f.write(result.replace('\n', eol).encode('utf-8'))

if __name__ == '__main__':
    assemble(*(Path(arg) for arg in sys.argv[1:]))
