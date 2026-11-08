from pathlib import Path
import json,subprocess,zipfile,xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
from pypdf import PdfReader
from build import ROOT,OUT,PROJECTS
POPPLER=Path('C:/Users/鲁迅/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe')
FFMPEG=next((ROOT.parent/'innovation-2026/.deps/imageio_ffmpeg/binaries').glob('*.exe'))
NS={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
results=[]
for p in PROJECTS:
    root=OUT/p['folder'];kind=p['kind'];title=p['title'];pdf=root/'创意说明书'/(title+'项目创意说明书.pdf');reader=PdfReader(pdf);text=''.join(pg.extract_text() for pg in reader.pages)
    for term in ['待填写','待补齐','提交前','不替团队','补充成员分工','建议明确','团队应依据','参赛团队需']:
        assert term not in text,(title,term)
    assert title in text and p['track'] in text and 'OpenAI Codex' in text
    assert 2000<len(''.join(text.split()))<3000,(title,len(''.join(text.split())))
    preview=ROOT/'.qa'/(kind+'-pdf');preview.mkdir(exist_ok=True)
    subprocess.run([str(POPPLER),'-scale-to','1100','-png',str(pdf),str(preview/'page')],check=True,capture_output=True)
    images=sorted(preview.glob('page-*.png'));thumbs=[]
    for path in images:
        im=Image.open(path).convert('RGB');im.thumbnail((620,880));thumbs.append(im)
    sheet=Image.new('RGB',(636*len(thumbs),920),'#dce3ec');d=ImageDraw.Draw(sheet)
    for i,im in enumerate(thumbs):sheet.paste(im,(i*636+8,25));d.text((i*636+8,6),f'{title} / {i+1}',fill='black')
    sheet.save(ROOT/'.qa'/(kind+'-pdf-contact.png'))
    video=root/'演示材料'/(title+'功能演示.mp4');meta=json.loads((ROOT/'.qa'/(kind+'-video-meta.json')).read_text());assert meta['durationSeconds']>25
    check=subprocess.run([str(FFMPEG),'-v','error','-i',str(video),'-f','null','-'],capture_output=True,text=True);assert check.returncode==0 and not check.stderr,check.stderr
    contact=Image.new('RGB',(750,1500),'white')
    for i,sec in enumerate([3,15,27]):
        frame=ROOT/'.qa'/f'{kind}-video-{sec}.png';subprocess.run([str(FFMPEG),'-loglevel','error','-ss',str(sec),'-i',str(video),'-frames:v','1','-y',str(frame)],check=True)
        im=Image.open(frame).convert('RGB');im.thumbnail((750,500));contact.paste(im,(0,i*500))
    contact.save(ROOT/'.qa'/(kind+'-video-contact.png'))
    form=root/'待填写'/'项目报名资料待填写（整理版）.xlsx'
    with zipfile.ZipFile(form) as z:
        assert z.testzip() is None
        tree=ET.fromstring(z.read('xl/worksheets/sheet1.xml'))
        for row in range(5,19):
            c=tree.find(f'.//s:c[@r="B{row}"]',NS)
            assert c is None or all((v.text is None or v.text=='') for v in c.findall('s:v',NS)),(title,row)
        assert tree.find('.//s:dataValidations',NS) is not None
    results.append({'project':title,'pdfPages':len(reader.pages),'pdfTextCharsWithoutSpaces':len(''.join(text.split())),'pdfNoInternalChecklist':True,'mp4Seconds':meta['durationSeconds'],'mp4Decoded':True,'formIdentityCellsBlank':True})
    print('VERIFIED '+title+' '+str(results[-1]))
(ROOT/'verification.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
