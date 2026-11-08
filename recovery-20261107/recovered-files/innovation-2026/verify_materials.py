from pathlib import Path
import subprocess,json,re,sys
from PIL import Image,ImageOps,ImageDraw
from pypdf import PdfReader
from build_apps import OUT,ROOT,PROJECTS

POPPLER=Path('C:/Users/鲁迅/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe')
FFMPEG=next((ROOT/'.deps/imageio_ffmpeg/binaries').glob('*.exe'))
qa=ROOT/'.qa';qa.mkdir(exist_ok=True)
results=[]
for folder,kind,title,sub,accent,en in PROJECTS:
    root=OUT/folder;pdf=root/'技术材料'/f'{title}项目书与技术报告.pdf'
    reader=PdfReader(pdf)
    assert len(reader.pages)==9
    for n,p in enumerate(reader.pages):
        text=p.extract_text()
        assert title in text and len(text)>150,(title,n,len(text))
        assert '\ufffd' not in text
    preview=qa/f'{kind}-pdf';preview.mkdir(exist_ok=True)
    subprocess.run([str(POPPLER),'-scale-to','900','-png',str(pdf),str(preview/'page')],check=True,capture_output=True)
    images=sorted(preview.glob('page-*.png'));sheet=Image.new('RGB',(1908,2760),'#dce2eb');d=ImageDraw.Draw(sheet)
    for i,p in enumerate(images):
        im=Image.open(p).convert('RGB');im.thumbnail((620,880));x=(i%3)*636+8;y=(i//3)*920+24;sheet.paste(im,(x,y));d.text((x,y-18),f'{title} / Page {i+1}',fill='black')
    sheet.save(qa/f'{kind}-pdf-contact.png')
    video=root/'演示材料'/f'{title}功能演示.mp4';meta=json.loads(Path(str(video)+'.meta.json').read_text())
    assert meta['durationSeconds']>25
    check=subprocess.run([str(FFMPEG),'-v','error','-i',str(video),'-f','null','-'],capture_output=True,text=True)
    assert check.returncode==0 and not check.stderr,check.stderr
    frames=[]
    for seconds in (3,16,28):
        frame=qa/f'{kind}-video-{seconds}.png'
        subprocess.run([str(FFMPEG),'-loglevel','error','-ss',str(seconds),'-i',str(video),'-frames:v','1','-y',str(frame)],check=True)
        im=Image.open(frame).convert('RGB');im.thumbnail((720,500));frames.append(im)
    contact=Image.new('RGB',(720,1500),'white')
    for i,im in enumerate(frames):contact.paste(im,(0,i*500))
    contact.save(qa/f'{kind}-video-contact.png')
    results.append({'project':title,'pdfPages':9,'pdfRendered':True,'videoDurationSeconds':meta['durationSeconds'],'videoDecodeErrors':False,'mp4Bytes':video.stat().st_size,'blankOfficialForm':True})
    print('VERIFIED '+title+' PDF 9 pages; MP4 '+str(meta['durationSeconds'])+'s')
(ROOT/'materials-verification.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
