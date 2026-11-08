"""Watch only new Unity crash dumps, then read the exception of the matching owned probe."""
import pathlib
import subprocess
import sys
import time

pid = int(sys.argv[1])
started = time.time()
root = pathlib.Path('D:/Temp/Unity/Editor/Crashes')
seen = set(root.glob('*/crash.dmp'))
while time.time() - started < 80:
    for dump in root.glob('*/crash.dmp'):
        if dump in seen:
            continue
        try:
            result = subprocess.run([sys.executable, 'Logs/read-crash-exception.py', str(dump), '--pid', str(pid)], capture_output=True, text=True, timeout=8)
            if result.returncode == 0:
                print(result.stdout, flush=True)
                raise SystemExit(0)
        except (OSError, subprocess.TimeoutExpired):
            pass
    time.sleep(0.2)
print('No matching completed crash dump appeared before the watch timeout.')
