"""V3 candidates: local edits of authorized existing publisher Foley only.

No oscillators, synthetic noise, generated body, pitch sweep, or V2 filtering.
Reads the original wood/arrow/leather WAVs. Writes ArtSource V3 only.
python -X utf8 ArtSource/Audio/SealConfirmationV3/render_candidates.py
"""
from pathlib import Path
import hashlib
import json
import math
import warnings
import numpy as np
from scipy import signal, ndimage
from scipy.io import wavfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
SOURCE_ROOT=ROOT/'Assets/Imported/BattleSoundLibrary/Battle 1'
FS=48000
ORDER=['Lv','Gui','Ju','Zhun','Sheng','Heng']
SOURCES={
 'wood5':'Fist/Fist On Wood/Wood/Fist_On_Wood_Wood_5.wav',
 'wood4':'Fist/Fist On Wood/Wood/Fist_On_Wood_Wood_4.wav',
 'wood1':'Fist/Fist On Wood/Wood/Fist_On_Wood_Wood_1.wav',
 'fist5':'Fist/Fist On Wood/Fist/Fist_On_Wood_Fist_5.wav',
 'pull5':'Bow & Arrow/Arrow Pulled Back/Arrow_Pulled_Back_5.wav',
 'pull2':'Bow & Arrow/Arrow Pulled Back/Arrow_Pulled_Back_2.wav',
 'notch5':'Bow & Arrow/Arrow Notched/Arrow_Notched_5.wav',
 'leather4':'Breaks/Leather Armour/Leather_Armour_Break_4.wav',
 'leather5':'Breaks/Leather Armour/Leather_Armour_Break_5.wav',
}

# Source key, source start/end seconds, placement seconds, layer gain dB.
DESIGNS={
 'Gui':dict(label='规',meaning='围合守界',action='柔软摩擦向内回拢，轻轻收口',duration=.42,
  layers=[('leather5',.180,.380,0,0),('pull5',.230,.350,.105,2),('notch5',.068,.130,.210,-8)]),
 'Ju':dict(label='矩',meaning='转折成形',action='摩擦包裹一次木面接触，短促转角归位',duration=.39,
  layers=[('leather4',.200,.360,0,3),('wood4',.075,.240,.025,-6),('fist5',.125,.220,.040,-6)]),
 'Zhun':dict(label='准',meaning='校齐稳定',action='一段平缓擦动后轻轻定住，避免承重砸落',duration=.40,
  layers=[('pull2',.240,.380,0,4),('wood5',.135,.230,.120,-2),('notch5',.050,.115,.110,-8)]),
 'Sheng':dict(label='绳',meaning='联结约束',action='短抽拉带出纤维细节，随即束紧',duration=.42,
  layers=[('pull2',.220,.380,0,5),('leather4',.200,.360,.100,3),('notch5',.060,.130,.220,-10)]),
 'Heng':dict(label='衡',meaning='承重镇定',action='包裹的低厚接触承重，保留轻微摆停尾声',duration=.51,
  layers=[('fist5',.075,.230,0,-8),('wood1',.085,.270,.040,-6),('leather4',.200,.400,.110,3),('wood4',.180,.290,.175,-1)]),
}


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def db(v):return float(20*np.log10(max(float(v),1e-12)))
def rms(x):return float(np.sqrt(np.mean(x*x)))


def read(p):
 with warnings.catch_warnings():
  warnings.simplefilter('ignore',wavfile.WavFileWarning)
  rate,y=wavfile.read(p)
 bits=y.dtype.itemsize*8
 x=y.astype(np.float64)/(2**(bits-1)) if y.dtype.kind=='i' else y.astype(np.float64)
 channels=1 if x.ndim==1 else x.shape[1]
 if x.ndim==2:x=np.mean(x,axis=1)
 return rate,x,dict(decoder_dtype=str(y.dtype),channels=channels,source_sample_rate=rate,
                   duration_seconds=len(x)/rate,sha256=sha(p),mono_downmix='arithmetic mean L/R' if channels==2 else 'none')


def k(x):
 x=signal.lfilter([1.53512485958697,-2.69169618940638,1.19839281085285],[1,-1.69065929318241,.73248077421585],x)
 return signal.lfilter([1,-2,1],[1,-1.99004745483398,.99007225036621],x)


def energy400(x):
 x=np.pad(x,(0,max(0,19200-len(x))))[:19200]
 return db(rms(k(x)))


def fade(x,attack=.012,release=.020):
 x=x.copy();a=min(len(x)//2,round(FS*attack));b=min(len(x)//2,round(FS*release))
 x[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2
 x[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
 return x


def soft_transients(x):
 # Limited peak-envelope gain control on recorded transients only, 1ms lookahead.
 # No makeup gain. At most 5dB attenuation; no audio synthesis or waveshaping.
 a=math.exp(-1/(FS*.00045));r=math.exp(-1/(FS*.018))
 envelope=np.empty(len(x));v=0
 detector=ndimage.maximum_filter1d(np.abs(x),size=round(FS*.002)+1,mode='constant')
 for i,sample in enumerate(detector):
  c=a if sample>v else r;v=c*v+(1-c)*sample;envelope[i]=v
 over=np.maximum(20*np.log10(np.maximum(envelope,1e-12))+13,0)
 reduction=np.minimum(over*(1-1/2.5),5)
 return x*10**(-reduction/20),dict(threshold_dbfs=-13,ratio=2.5,
  attack_ms=.45,release_ms=18,lookahead_ms=1,maximum_allowed_reduction_db=5,
  maximum_observed_reduction_db=float(np.max(reduction)),makeup_gain_db=0)


def write(p,x):
 assert np.max(np.abs(x))<1
 wavfile.write(p,FS,np.round(x*32767).astype(np.int16))


def preview(name,entries,spacing,master=1):
 out=np.zeros(round(FS*(len(entries)*spacing+1)))
 timeline=[]
 for i,(label,x) in enumerate(entries):
  start=round(i*spacing*FS);out[start:start+len(x)]+=x*master
  timeline.append(dict(label=label,start_seconds=start/FS))
 assert np.max(np.abs(out))<1
 write(HERE/name,out)
 return dict(file=name,spacing_seconds=spacing,master_gain_linear=master,
             master_gain_db=db(master),peak_dbfs=db(np.max(np.abs(out))),timeline=timeline,
             per_clip_loudness_normalization=False)


def main():
 HERE.mkdir(parents=True,exist_ok=True);(HERE/'Wav').mkdir(exist_ok=True)
 original={n:sha(ROOT/f'Assets/Art/VFX/Skills/SealConfirmation/{n}Chime.wav') for n in ORDER}
 cache={name:read(SOURCE_ROOT/path) for name,path in SOURCES.items()}
 lv=read(ROOT/'Assets/Art/VFX/Skills/SealConfirmation/LvChime.wav')[1]
 clips={'Lv':lv};recipes={};metrics={}
 for code,design in DESIGNS.items():
  mix=np.zeros(round(FS*design['duration']));layers=[]
  for key,begin,end,offset,gain in design['layers']:
   rate,raw,meta=cache[key]
   lo,hi=round(begin*rate),round(end*rate)
   segment=raw[lo:hi]
   divisor=math.gcd(FS,rate);segment=signal.resample_poly(segment,FS//divisor,rate//divisor)
   segment=fade(segment)*10**(gain/20)
   at=round(offset*FS);mix[at:at+len(segment)]+=segment
   p=SOURCE_ROOT/SOURCES[key]
   layers.append(dict(source_key=key,source_absolute_path=str(p.resolve()),
    source_project_relative_path=str(p.relative_to(ROOT)).replace('\\','/'),**meta,
    source_start_seconds=begin,source_end_seconds=end,source_start_sample=lo,source_end_sample=hi,
    placement_seconds=offset,gain_db=gain,fade_in_ms=12,fade_out_ms=20,
    crossfade_method='Overlapping real layers, each with 12ms sine-squared entry / 20ms cosine-squared exit',
    time_stretch_ratio=1,pitch_shift_semitones=0,
    sample_rate_conversion='scipy.signal.resample_poly 44100 -> 48000; no musical time stretch'))
  mix,comp=soft_transients(mix)
  mix=fade(mix,.010,.025);mix[0]=mix[-1]=0
  write(HERE/'Wav'/f'{code}Chime.wav',mix)
  clips[code]=read(HERE/'Wav'/f'{code}Chime.wav')[1]
  recipes[code]=dict(label=design['label'],meaning=design['meaning'],action=design['action'],
                    layers=layers,recorded_material_truth='Publisher-labeled arrow handling, fist/wood contact and leather handling. Meaning is an interpretation, not a claim of newly recorded compass/paper/scale.',
                    output_duration_seconds=design['duration'],output_master_gain_db=0,
                    transient_control=comp,oscillators_or_synthetic_layers=False)
  metrics[code]=dict(duration_seconds=len(mix)/FS,peak_dbfs=db(np.max(np.abs(clips[code]))),
                     first400_k_rms_dbfs=energy400(clips[code]),delta_k400_vs_Lv_db=energy400(clips[code])-energy400(lv),
                     last10ms_peak_dbfs=db(np.max(np.abs(clips[code][-480:]))),sha256=sha(HERE/'Wav'/f'{code}Chime.wav'))
  print(code,design['action'],'peak',round(metrics[code]['peak_dbfs'],2),'Kdiff',round(metrics[code]['delta_k400_vs_Lv_db'],2))
 auditions={}
 auditions['candidate_comparison.wav']=preview('candidate_comparison.wav',[(('律' if n=='Lv' else DESIGNS[n]['label'])+' '+n,clips[n]) for n in ORDER],1.2)
 ab=[]
 for code in ORDER:
  old=read(ROOT/f'ArtSource/Audio/SealConfirmationV2/Wav/{code}Chime.wav')[1] if code!='Lv' else lv
  ab.extend([(code+' V2',old),(code+' V3',clips[code])])
 auditions['AB_V2_V3.wav']=preview('AB_V2_V3.wav',ab,1.2)
 rapid=[(n+f' round{i+1}',clips[n]) for i in range(3) for n in ORDER]
 auditions['rapid_game_gain.wav']=preview('rapid_game_gain.wav',rapid,.4,.62)
 report=dict(version=3,status='first_audition_candidates_not_deployed',
  source_policy_file='Logs/t01-seal-audio-v3-20261125/source-policy.json',
  source_policy_sha256=sha(ROOT/'Logs/t01-seal-audio-v3-20261125/source-policy.json'),
  publisher='Infinity PBR / Magic Pig Games',asset_store_product_id='176417',publisher_id='4645',
  source_basis='Existing user-directed project assets; publisher-recorded Foley provenance per parent-verified policy. No new personal recording and no purchase-receipt claim.',
  raw_library_copied=False,raw_audio_uploaded=False,synthetic_body_added=False,
  production_sources_unchanged=all(sha(ROOT/f'Assets/Art/VFX/Skills/SealConfirmation/{n}Chime.wav')==original[n] for n in ORDER),
  production_source_sha256=original,recipes=recipes,preliminary_metrics=metrics,auditions=auditions,
  listening_status='NOT HUMAN-LISTENED by audio worker; send candidates to user for judgment before polishing or deploying.',
  loudness_policy='Preserve comfortable actual candidate gains. No equal-loudness lift toward V2 or Lv. Rapid uses common prefab .62 gain; spatial attenuation is not simulated.')
 (HERE/'source_recipe.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 assert report['production_sources_unchanged']
 print('Created five real-source edit candidates and audition WAVs; production unchanged.')


if __name__=='__main__':main()
