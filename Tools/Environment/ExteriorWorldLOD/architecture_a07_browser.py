from pathlib import Path
import sys,json
sys.stdout.reconfigure(encoding='utf8')
sys.path.insert(0,'D:/Temp/NarutoVR-Environment-20261108/browser-deps')
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture/a07';LOG.mkdir(parents=True,exist_ok=True)
with sync_playwright() as p:
 browser=p.chromium.connect_over_cdp('http://[::1]:9222')
 pages=[page for context in browser.contexts for page in context.pages if '3d.hunyuan.tencent.com/creation' in page.url]
 assert len(pages)==1,'Expected exactly one Hunyuan creation tab'
 page=pages[0]
 page.screenshot(path=str(LOG/'hunyuan-before.png'))
 text=page.locator('body').inner_text();(LOG/'hunyuan-before.txt').write_text(text,encoding='utf8')
 print(text[:18000])
 # Deliberately no browser.close(): user-owned Chrome remains open.
