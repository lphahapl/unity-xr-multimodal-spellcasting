"""Prepare final ordinary warm-gold typesetting and isolated Unity review only."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,hashlib,shutil
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
TEMP=Path('D:/Temp/NarutoVR-T01Seal-20261124')
REL=Path('Assets/Art/VFX/Skills/SealConfirmation')
LOG=ROOT/'Logs/seal-confirmation-xiaozhuan-r1-20261125/unity'
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
codes=['Gui','Ju','Lv','Zhun','Sheng','Heng']
for d in [HERE/'Backup/MainV1',HERE/'Backup/IsolatedV1',HERE/'RuntimeGold',LOG]: d.mkdir(parents=True,exist_ok=True)

# Preserve both sets before the first isolated or main deployment; never overwrite backups.
for code in codes:
    for source,folder in [(ROOT/REL,'MainV1'),(TEMP/REL,'IsolatedV1')]:
        dest=HERE/f'Backup/{folder}/{code}Glyph.png'
        if not dest.exists():shutil.copy2(source/f'{code}Glyph.png',dest)

old=np.array(Image.open(HERE/'Backup/MainV1/LvGlyph.png').convert('RGBA'))
solid=old[old[:,:,3]>=240]
luminance=solid[:,:3]@np.array([.2126,.7152,.0722])
target=np.percentile(luminance,75)
color=solid[np.argmin(abs(luminance-target))]
rows=[]
for code in codes:
    source=HERE/f'Runtime/{code}Glyph.png'
    a=np.array(Image.open(source).convert('RGBA'));alpha=a[:,:,3].copy();a[:,:,:3]=color[:3]
    out=HERE/f'RuntimeGold/{code}Glyph.png';Image.fromarray(a).save(out)
    assert np.array_equal(np.asarray(Image.open(out))[:,:,3],alpha)
    rows.append({'code':code,'file':out.name,'whiteSourceSha256':digest(source),'goldSha256':digest(out),
                 'alphaByteIdenticalToWhite':True,'onlyRGBChanged':True,'rgb':color[:3].tolist()})
    shutil.copy2(out,TEMP/REL/out.name)
shutil.copy2(HERE/'Runtime/ATTRIBUTION.txt',HERE/'RuntimeGold/ATTRIBUTION.txt')
(HERE/'RuntimeGold/ATTRIBUTION.txt').write_text((HERE/'RuntimeGold/ATTRIBUTION.txt').read_text(encoding='utf8').replace('RGB 全白','RGB 为原律印采样暖金 246/220/167')+
    '\nRuntimeGold 仅将普通排字 RGB 设为原律印实心笔画采样色 246/220/167。alpha 与白版逐字节相同；没有描边、光晕、轮廓膨胀或笔画改作。\n',encoding='utf8')
manifest={'generatedAt':datetime.now(timezone(timedelta(hours=8))).isoformat(),'parentApprovedOrdinaryFontColoring':True,
          'colorSampling':{'file':'Backup/MainV1/LvGlyph.png','sha256':digest(HERE/'Backup/MainV1/LvGlyph.png'),
                          'sourceMask':'alpha >= 240','solidPixelCount':len(solid),'method':'pixel nearest 75th percentile of .2126 R + .7152 G + .0722 B among solid pixels',
                          'chosenSourceRGBA':color.tolist(),'runtimeRGB':color[:3].tolist(),'runtimeAlpha':'unchanged from each white typeset PNG'},'files':rows}
(HERE/'RuntimeGold/runtime-gold-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')

# Clone the known V3.1 driver. Keep original six-stage matcher/reentry and actual listener mixing.
src=TEMP/'Assets/scripts/logic/Editor/T01SealAudioV31Acceptance.cs'
s=src.read_text(encoding='utf8').replace('T01SealAudioV31Acceptance','T01SealXiaozhuanAcceptance')
s=s.replace('D:/UnityProjects/NarutoVR/Logs/t01-seal-audio-v3_1-20261125/unity',str(LOG).replace('\\','/'))
s=s.replace('        BeginRapid();\n    }','        BeginBoard();\n    }',1)
# No rapid/stress audio work is invoked by this run. Replace those helpers with a real six-instance board.
start=s.index('    static void BeginRapid()')
end=s.index('    static void Render()\n',start)
board=r'''    static int boardFrame;
    static void BeginBoard()
    {
        running=false;AudioRenderer.Stop();RecordedScoringChecks();
        wristCase.SetActive(false);
        foreach(var old in Obj.FindObjectsByType<SealGlyphPulse>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
        for(int i=0;i<6;i++)
        {
            // Six real production prefab instances. Normal Update drives their pulse; no font redraw.
            var obj=Obj.Instantiate(profiles[i].confirmationPrefab,new Vector3((i%3-1)*.25f,(i<3?.13f:-.13f),.80f),Quaternion.identity);
            obj.name="SixSealBoard_"+Codes[i];obj.GetComponent<AudioSource>().mute=true;
        }
        boardFrame=0;
        T01SealProbeDriver.Tick=()=>{try{BoardStep();}catch(Exception ex){Fail(ex);}};
    }
    static void BoardStep()
    {
        if(++boardFrame<4)return;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var old=RenderTexture.active;RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();RenderTexture.active=old;
        File.WriteAllBytes(Output+"/SixSealActualInstances.png",texture.EncodeToPNG());
        Require(Obj.FindObjectsByType<SealGlyphPulse>(FindObjectsSortMode.None).Count(x=>x.gameObject.name.StartsWith("SixSealBoard_"))==6,"Six real live prefab instances on board");
        File.WriteAllLines(Output+"/result.txt",report.Concat(new[]{
            "PASS: original matcher→ForwardSeal→PlaySealConfirmation→SealGlyphPulse; synthetic features and wrist injection, not live headset tracking.",
            "规/矩/律/衡 each: one sustained confirmation, no exit event, one reentry confirmation; original audio sources actually play.",
            "准/绳 calibrated=false; zero recognized events, direct production prefab previews only.",
            "Actual Unity AudioRenderer final listener mix saved per seal. No source-WAV substitution; no new rapid/stress cycle.",
            "SixSealActualInstances.png: six real prefab instances at same pulse age, arranged only for comparison, audio muted on board.",
            "Heng images use actual wrist-case prefab and original left anchor at a synthetic wrist pose, not a headset occlusion result.",
            "No Quest hardware/live tracking/headset acceptance claim."}));
        Note("COMPLETE","Xiaozhuan six individual stages and actual-instance board passed; requested exit 0");
        T01SealProbeDriver.Tick=null;Time.captureFramerate=0;EditorApplication.Exit(0);
    }
'''
s=s[:start]+board+s[end:]
# Dead rapid branch is removed, together with its unreachable tracking invocation.
s=s.replace('        if(rapid){RapidStep();return;}\n','')
s=s.replace('        if(rapid){rapidEvents++;TrackAudio(pulse.gameObject,stage,"matcher");}\n','')
dest=TEMP/'Assets/scripts/logic/Editor/T01SealXiaozhuanAcceptance.cs'
dest.write_text(s,encoding='utf8')
shutil.copy2(dest,HERE/'T01SealXiaozhuanAcceptance.cs')
(LOG/'preparation.json').write_text(json.dumps({'driver':str(dest),'driverSha256':digest(dest),'originalDriverSha256':digest(src),
    'isolatedProject':str(TEMP),'output':str(LOG),'finalRuntime':'RuntimeGold','mainAssetsModified':False,
    'runtimeGoldManifest':'ArtSource/VFX/SealConfirmationXiaozhuanR1/RuntimeGold/runtime-gold-manifest.json'},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'RGB':color[:3].tolist(),'driver':str(dest),'rows':rows},ensure_ascii=False))
