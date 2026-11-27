"""Package captured Unity frames with their actual AudioRenderer output, not source WAVs."""
from pathlib import Path
import argparse, json, subprocess, hashlib
import imageio_ffmpeg

ROOT=Path(__file__).resolve().parents[3]
INPUT=ROOT/'Logs/t01-seal-feedback-20261124/unity'
OUT=ROOT/'docs/visuals/20261125_six_seal_confirmation'
FF=imageio_ffmpeg.get_ffmpeg_exe()
ORDER=[('Gui','规'),('Ju','矩'),('Heng','衡'),('Lv','律'),('Zhun','准'),('Sheng','绳')]

def run(args):
    subprocess.run([FF,'-hide_banner','-loglevel','error',*args],cwd=OUT,check=True)
def main():
    global INPUT, OUT, ORDER
    parser=argparse.ArgumentParser()
    parser.add_argument('--input',type=Path,default=INPUT)
    parser.add_argument('--output',type=Path,default=OUT)
    parser.add_argument('--audio-v2',action='store_true')
    parser.add_argument('--audio-v3',action='store_true')
    parser.add_argument('--audio-v3-1',action='store_true')
    args=parser.parse_args()
    INPUT,OUT=args.input.resolve(),args.output.resolve()
    foley_version='V3.1' if args.audio_v3_1 else ('V3' if args.audio_v3 else None)
    if args.audio_v2 or foley_version: ORDER=[('Lv','律'),('Gui','规'),('Ju','矩'),('Zhun','准'),('Sheng','绳'),('Heng','衡')]
    sound_intents={'Lv':'铜鸣基准','Gui':'环擦回旋','Ju':'木尺双击','Zhun':'点声落定','Sheng':'绷弦收紧','Heng':'砝码落座'}
    if foley_version: sound_intents={'Lv':'铜鸣原样','Gui':'围合收口','Ju':'转折归角','Zhun':'校齐平复','Sheng':'联结束紧','Heng':'承重镇定'}
    OUT.mkdir(parents=True,exist_ok=True)
    report=[]
    for key,char in ORDER:
        folder=INPUT/key
        frames=sorted((folder/'frames').glob('frame_*.jpg'))
        assert len(frames)==120, (key,len(frames))
        audio=folder/'unity-output.wav'; assert audio.exists()
        uncalibrated=key in ['Zhun','Sheng']
        title=f'{char}印  /  '+('未标定模板预览' if uncalibrated else '稳定成印播放回归')
        if args.audio_v2: title=f'{char}印  /  {sound_intents[key]}  ·  声音 V2'
        if foley_version: title=f'{char}印  /  {sound_intents[key]}  ·  {foley_version} 候选'
        if key=='Lv': title+='  ·  原样板'
        detail=('直接预览 Prefab · 无手形识别事件 · 待真人采样' if uncalibrated else ('左手锚点 · 右手缺失 · 后半段加入准望匣' if key=='Heng' else '双手锚点 · 持续保持一次 · 松开后再次成印'))
        (OUT/f'{key}_title.txt').write_text(title,encoding='utf-8')
        (OUT/f'{key}_detail.txt').write_text(detail,encoding='utf-8')
        note='Unity 6000.3.10f1 实际渲染与混音输出  |  合成输入回归，非 Quest 实机'
        if foley_version: note=f'{foley_version}隔离副本 · 原音量0.62实际混音 · 合成输入回归，非Quest实机'
        (OUT/'capture_note.txt').write_text(note,encoding='utf-8')
        font="fontfile='C\\:/Windows/Fonts/msyh.ttc'"
        vf=f"drawtext={font}:textfile={key}_title.txt:fontsize=30:fontcolor=0xeee0c0:x=32:y=24,drawtext={font}:textfile={key}_detail.txt:fontsize=19:fontcolor=0xc7d4d5:x=32:y=h-72,drawtext={font}:textfile=capture_note.txt:fontsize=16:fontcolor=0x91a3a7:x=32:y=h-38"
        dest=OUT/f'{key}SealPreview.mp4'
        run(['-y','-framerate','30','-i',str(folder/'frames/frame_%04d.jpg'),'-i',str(audio),'-vf',vf,'-af','apad,atrim=0:4','-map','0:v:0','-map','1:a:0','-t','4','-c:v','libx264','-preset','medium','-crf','17','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-movflags','+faststart',str(dest)])
        report.append({'seal':key,'source_frame_count':len(frames),'source_audio':str(audio.relative_to(ROOT)),'source_audio_sha256':hashlib.sha256(audio.read_bytes()).hexdigest(),'mp4':dest.name})
    for name,items in [('SixSealPreview',ORDER),('GuiJuHengPreview',[('Gui','规'),('Ju','矩'),('Heng','衡')])]:
        listing=OUT/f'{name}.concat.txt'
        listing.write_text(''.join(f"file '{key}SealPreview.mp4'\n" for key,_ in items),encoding='utf-8')
        run(['-y','-f','concat','-safe','0','-i',str(listing),'-c','copy','-movflags','+faststart',str(OUT/f'{name}.mp4')])
        run(['-i',str(OUT/f'{name}.mp4'),'-f','null','-'])
    (OUT/'publish-report.json').write_text(json.dumps({'provenance':'Actual Unity captures. Overlaid labels only; original Unity mixed PCM used. No source clip replacement.','segments':report,'decode_checks':['SixSealPreview.mp4: exit 0','GuiJuHengPreview.mp4: exit 0']},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Published six individual clips, priority 12s and full 24s comparisons; decode PASS.')

if __name__=='__main__': main()
