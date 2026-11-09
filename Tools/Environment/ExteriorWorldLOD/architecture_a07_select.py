from pathlib import Path
import sys,json
sys.stdout.reconfigure(encoding='utf8');sys.path.insert(0,'D:/Temp/NarutoVR-Environment-20261108/browser-deps')
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[3];LOG=ROOT/'Logs/exterior-world-lod-20261109/architecture/a07'
with sync_playwright() as p:
 b=p.chromium.connect_over_cdp('http://[::1]:9222');page=next(pg for c in b.contexts for pg in c.pages if '3d.hunyuan.tencent.com/creation' in pg.url);responses=[]
 page.on('response',lambda r: responses.append({'url':r.url,'status':r.status}) if '.glb' in r.url else None)
 page.get_by_text('查看模型',exact=True).first.click(timeout=5000);page.wait_for_timeout(3000)
 page.screenshot(path=str(LOG/'workbench-selected.png'));(LOG/'selected.txt').write_text(page.locator('body').inner_text(),encoding='utf8')
 print(page.locator('body').inner_text()[-7000:]);print('GLB responses',json.dumps(responses));(LOG/'selected-network.json').write_text(json.dumps(responses,indent=2))
