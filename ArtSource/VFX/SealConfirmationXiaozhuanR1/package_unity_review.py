"""Package actual Unity frames and listener PCM; no substituted glyphs or source audio."""
from pathlib import Path
import hashlib,json,subprocess,wave
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
CAP=ROOT/'Logs/seal-confirmation-xiaozhuan-r1-20261125/unity'
OUT=HERE/'UnityReview';OUT.mkdir(exist_ok=True)
ORDER=[('Gui','规印','原 matcher：持印一次、退出、重入一次'),('Ju','矩印','原 matcher：持印一次、退出、重入一次'),
       ('Heng','衡印','原 matcher / 左腕；第二次显示准望匣'),('Lv','律印','原 matcher：持印一次、退出、重入一次'),
       ('Zhun','准印','未标定，直接 Prefab 模板预览'),('Sheng','绳印','未标定，直接 Prefab 模板预览')]
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
rows=[];clips=[]
contact=Image.new('RGB',(1440,700),(24,31,33));d=ImageDraw.Draw(contact)
d.text((20,10),'六印小篆｜实际 Unity 帧 / RuntimeGold 原轮廓',font=font(28),fill=(246,220,167))
for index,(code,title,note) in enumerate(ORDER):
    src=CAP/code
    frames=sorted((src/'frames').glob('frame_*.jpg'))
    assert len(frames)==120,(code,len(frames))
    pic=Image.open(frames[80 if code=='Heng' else 10]).convert('RGB')
    pic=pic.resize((480,270),Image.Resampling.LANCZOS)
    x=(index%3)*480;y=60+(index//3)*310;contact.paste(pic,(x,y))
    d.text((x+18,y+275),title+' · '+('模板' if index>=4 else '实际链路'),font=font(21),fill=(245,233,205))
    with wave.open(str(src/'unity-output.wav'),'rb') as w:
        rate=w.getframerate();channels=w.getnchannels();samples=w.getnframes();a=np.frombuffer(w.readframes(samples),dtype='<i2')
    peak=float(abs(a.astype(np.int32)).max()/32768);rms=float(np.sqrt(np.mean((a.astype(float)/32768)**2)))
    rows.append({'code':code,'title':title,'frames':len(frames),'sampleRate':rate,'channels':channels,
                 'samplesPerChannel':samples,'duration':samples/rate,'pcmPeak':peak,'pcmRMS':rms,
                 'clippedSamples':int((abs(a.astype(np.int32))>=32767).sum()),
                 'audioSource':'actual Unity AudioRenderer final listener mix','captureWavSha256':hashlib.sha256((src/'unity-output.wav').read_bytes()).hexdigest()})
    # Labels are a distinct band below each unchanged Unity frame, not redrawn seal artwork.
    labelled=OUT/f'{code}/frames';labelled.mkdir(parents=True,exist_ok=True)
    for f in frames:
        img=Image.new('RGB',(960,606),(20,25,27));img.paste(Image.open(f),(0,0));draw=ImageDraw.Draw(img)
        draw.text((20,542),title+' | '+note,font=font(22),fill=(246,220,167))
        draw.text((20,575),'Unity 6000.3.10f1 实际实例 + 实际 Listener 混音；合成腕姿，非头显实测',font=font(17),fill=(205,207,207))
        img.save(labelled/f.name,quality=93)
    dest=OUT/f'{code}_ActualUnity.mp4'
    cmd=[ffmpeg,'-hide_banner','-loglevel','error','-y','-framerate','30','-i',str(labelled/'frame_%04d.jpg'),
         '-i',str(src/'unity-output.wav'),'-c:v','libx264','-threads','2','-crf','20','-pix_fmt','yuv420p',
         '-c:a','aac','-b:a','192k','-t','4',str(dest)]
    p=subprocess.run(cmd,capture_output=True,text=True);assert p.returncode==0,p.stderr
    clips.append(dest)
d.text((20,680),'源帧仅排版缩略；字形未后贴。上规/矩/衡，下律/准/绳。',font=font(16),fill=(205,207,207))
contact.save(OUT/'SixSeal_ActualUnity_Contact.png')
concat=OUT/'concat.txt';concat.write_text(''.join("file '"+p.as_posix()+"'\n" for p in clips),encoding='utf8')
p=subprocess.run([ffmpeg,'-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(concat),'-c','copy',str(OUT/'SixSeal_ActualUnity_WithAudio.mp4')],capture_output=True,text=True)
assert p.returncode==0,p.stderr
# Actual simultaneous six-instance image, with non-overlapping text labels only.
b=Image.open(CAP/'SixSealActualInstances.png').convert('RGB');d=ImageDraw.Draw(b)
d.text((20,12),'六印 · 六个真实 Prefab 同龄脉冲（同屏只比较字形，不作六印识别测试）',font=font(20),fill=(246,220,167))
for i,(_,title,_) in enumerate(ORDER):d.text((270+(i%3)*180,225+(i//3)*190),title,font=font(20),fill=(231,231,224))
b.save(OUT/'SixSeal_ActualInstances_Labelled.png')
no=np.array(Image.open(CAP/'Heng/compare_no_case.png').convert('RGB')).astype(np.int16)
yes=np.array(Image.open(CAP/'Heng/compare_with_case.png').convert('RGB')).astype(np.int16)
case=np.array(Image.open(CAP/'Heng/case_only.png').convert('RGB')).astype(np.int16)
bg=no[0,0];strong=np.max(abs(no-bg),axis=2)>20
hard=strong & (np.max(abs(yes-case),axis=2)<=2)
core=np.max(abs(no-bg),axis=2)>40
core_hard=core & (np.max(abs(yes-case),axis=2)<=2)
occlusion={'pose':'one synthetic forward wrist pose, actual left anchor and original 0.12m depth material',
           'strongGlyphPixels':int(strong.sum()),'hardOccludedPixels':int(hard.sum()),
           'criterion':'no-case max channel delta from background >20 AND with-case vs case-only max channel delta <=2',
           'coreGlyphPixels':int(core.sum()),'coreHardOcclusionEquivalentPixels':int(core_hard.sum()),
           'coreCriterion':'same test but no-case delta >40; >20 threshold includes weak antialias edge pixels',
           'interpretation':'image-difference proxy only; similar glyph/case colors can coincide, so the 2 weak edge candidates are not proved geometric occlusions',
           'headsetTested':False,'allAnglesTested':False}
q={'audio':rows,'hengOcclusion':occlusion,'osExitCodeCaptured':False,'testRequestedExitCode':0,
   'testResultFile':'result.txt','knownEditorException':'UnityEditor.Search.SearchDatabase startup index ArgumentOutOfRangeException; no runtime/test stack frames in that exception; all later acceptance stages completed',
   'runtimeExceptionObserved':False,'packageManagerWarning':'path argument must be of type string, Received undefined',
   'videoSource':'120 actual Unity frames per seal and actual captured PCM; 4 s per clip, only final silent capture quantization remainder trimmed',
   'videoOrder':[c for c,_,_ in ORDER],'newRapidStressPerformed':False,'headsetTested':False}
(CAP/'media-qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'video':str(OUT/'SixSeal_ActualUnity_WithAudio.mp4'),'occlusion':occlusion,'peaks':[r['pcmPeak'] for r in rows]},ensure_ascii=False))
