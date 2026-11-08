from pathlib import Path
import difflib

target = Path(r'D:\unity_training\Runnner&&Lucy\Assets\Scripts\Extra\ChessBoardCamera.cs')
raw = target.read_bytes()
try:
    source = raw.decode('utf-8-sig')
    encoding = 'utf-8-sig' if raw.startswith(b'\xef\xbb\xbf') else 'utf-8'
except UnicodeDecodeError:
    encoding = 'gb18030'
    source = raw.decode(encoding)
newline = '\r\n' if '\r\n' in source else '\n'
source = source.replace('\r\n', '\n')
updated = source.replace('''        if (Input.GetMouseButton(2))
        {
            isLocked = true;''', '''        bool isPanning = Input.GetMouseButton(0) && Input.GetMouseButton(1);
        bool isRotating = Input.GetMouseButton(2) && !isPanning;
        isDragging = isPanning || isRotating;
        isLocked = !isDragging;
        if (isDragging)
        {''', 1)
updated = updated.replace('if (Input.GetMouseButton(0))', 'if (isPanning)', 1)
updated = updated.replace('                isLocked=false;\n', '', 1)
updated = updated.replace('''        yaw = 0;
        isLocked =false;
        isDragging = Input.GetMouseButton(2);
''', '', 1)
updated = updated.replace('//中键在拖动，控制摄像机转向', '// 左右键同时按住平移视角，中键旋转视角')
assert updated != source
assert 'bool isPanning = Input.GetMouseButton(0) && Input.GetMouseButton(1);' in updated
assert 'isLocked =false;' not in updated
assert 'isDragging = Input.GetMouseButton(2);' not in updated
backup = Path(__file__).resolve().parent / '摄像机输入修改前备份.cs.txt'
if not backup.exists(): backup.write_bytes(raw)
target.write_bytes(updated.replace('\n', newline).encode(encoding))
print('Updated:', str(target))
for line in difflib.unified_diff(source.splitlines(), updated.splitlines(), fromfile='before', tofile='after', lineterm=''):
    print(line)
