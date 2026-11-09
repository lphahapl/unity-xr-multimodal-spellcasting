from pathlib import Path
import sys,json,time
sys.stdout.reconfigure(encoding='utf8');sys.path.insert(0,'D:/Temp/NarutoVR-Environment-20261108/browser-deps')
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture/a07'
with sync_playwright() as p:
 assert not (LOG/'after-submit.txt').exists(), 'A submission was already recorded; inspect current task instead of resubmitting'
 browser=p.chromium.connect_over_cdp('http://[::1]:9222');page=next(pg for c in browser.contexts for pg in c.pages if '3d.hunyuan.tencent.com/creation' in pg.url)
 # Inspect quota tooltip before spending; never use an older balance as the present one.
 for node in reversed(page.get_by_text('5',exact=True).all()):
  if node.is_visible():
   try:node.hover(timeout=1500);page.wait_for_timeout(500);break
   except Exception:continue
 (LOG/'quota-live.txt').write_text(page.locator('body').inner_text(),encoding='utf8')
 page.screenshot(path=str(LOG/'quota-live.png'))
 print('LIVE_QUOTA_PAGE',page.locator('body').inner_text()[:1600])
 page.locator('input[type=file]').first.set_input_files(str(ROOT/'ArtSource/Environment/Opening/ArchitectureLOD/A07Workbench/workbench-reference.png'))
 page.get_by_text('500k',exact=True).last.click()
 page.wait_for_timeout(2500)
 page.screenshot(path=str(LOG/'before-submit.png'))
 print('PREPARED',page.locator('body').inner_text()[:2500])
 # Submit exactly once. An uncertain result is inspected, never clicked again automatically.
 button=page.get_by_text('立即生成',exact=True).last;assert button.is_visible()
 button.click();page.wait_for_timeout(2500)
 (LOG/'after-submit.txt').write_text(page.locator('body').inner_text(),encoding='utf8');page.screenshot(path=str(LOG/'after-submit.png'))
 print('AFTER_SUBMIT',page.url,page.locator('body').inner_text()[:4500])
