from pathlib import Path
import sys,json
sys.stdout.reconfigure(encoding='utf8');sys.path.insert(0,'D:/Temp/NarutoVR-Environment-20261108/browser-deps')
from playwright.sync_api import sync_playwright
with sync_playwright() as p:
 b=p.chromium.connect_over_cdp('http://[::1]:9222');page=next(pg for c in b.contexts for pg in c.pages if '3d.hunyuan.tencent.com/creation' in pg.url)
 print('VIEW_MODEL',page.get_by_text('查看模型',exact=True).first.evaluate('e=>e.parentElement.parentElement.outerHTML'))
 print('IMAGES',page.locator('.mini-assets-dialog img').count())
