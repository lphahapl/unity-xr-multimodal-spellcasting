from pathlib import Path
import sys,json
sys.stdout.reconfigure(encoding='utf8');sys.path.insert(0,'D:/Temp/NarutoVR-Environment-20261108/browser-deps')
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture/a07'
with sync_playwright() as p:
 browser=p.chromium.connect_over_cdp('http://[::1]:9222');page=next(pg for c in browser.contexts for pg in c.pages if '3d.hunyuan.tencent.com/creation' in pg.url)
 if '--open-assets' in sys.argv:
  page.locator('#assets-mini-panel').click(timeout=5000);page.wait_for_timeout(1500)
 text=page.locator('body').inner_text();(LOG/'task-status.txt').write_text(text,encoding='utf8');page.screenshot(path=str(LOG/'task-status.png'));print(text[:18000])
 print('URL',page.url)
