"""V3.1 independent action/dynamics revision of existing real publisher Foley.

python -X utf8 ArtSource/Audio/SealConfirmationV3_1/render_v3_1.py

Writes only ArtSource/Audio/SealConfirmationV3_1 and its Logs/audio folder.
No common loudness, RMS, peak, or Lv-relative target. No master gain lift.
No synthetic layers. Existing V3, main Assets and docs are read-only.
"""
from __future__ import annotations
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path
import warnings
import numpy as np
from scipy import ndimage, signal
from scipy.io import wavfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
V3=ROOT/'ArtSource/Audio/SealConfirmationV3'
LOG=ROOT/'Logs/t01-seal-audio-v3_1-20261125/audio'
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

# key, source start/end s, placement s, layer gain dB, entry/exit fade ms.
# Every layer is a cut from a named original waveform; no new sound is synthesized.
DESIGNS={
 'Gui':dict(label='规',meaning='围合守界',action='摩擦回拢，末端木面收口成为重点',duration=.43,
  layers=[('leather5',.195,.355,0,-3,12,24),('pull5',.230,.350,.075,1,12,22),('wood5',.075,.172,.192,1,9,24)],
  transient=dict(threshold_dbfs=-11,ratio=1.6,max_reduction_db=3,attack_ms=.6,release_ms=22,lookahead_ms=1),
  rationale='降低开头皮革摩擦；将原微弱搭扣尾换成独立木面短接触，形成回拢→收口，不整体抬高。'),
 'Ju':dict(label='矩',meaning='转折成形',action='短木靠合音头更明确，掌面衬托转角',duration=.40,
  layers=[('leather4',.200,.330,0,0,12,22),('wood4',.075,.250,.014,-.5,8,24),('fist5',.075,.165,.016,-10,10,24)],
  transient=dict(threshold_dbfs=-10.5,ratio=1.55,max_reduction_db=3,attack_ms=.35,release_ms=16,lookahead_ms=.75),
  rationale='主木层由-6改-0.5dB且提前11ms；辅层改为低增益掌面接触，不复制V2双木击，也不提升低电平底噪。'),
 'Zhun':dict(label='准',meaning='校齐稳定',action='短平移提示后轻点稳住，背景摩擦退后',duration=.35,
  layers=[('pull2',.265,.335,0,-4,12,22),('notch5',.018,.083,.045,-5,8,18),('wood5',.105,.185,.085,-5,12,24)],
  transient=dict(threshold_dbfs=-14,ratio=1.4,max_reduction_db=2,attack_ms=.35,release_ms=12,lookahead_ms=.75),
  rationale='摩擦层缩短一半并降低8dB；从搭扣原录音前段取明确轻点而非只放大其尾；后接很短木体平复。'),
 'Sheng':dict(label='绳',meaning='联结约束',action='较长原始抽拉连续积累张力，末端收束',duration=.47,
  layers=[('pull2',.145,.435,0,2.5,12,24),('leather4',.200,.370,.210,-3,14,24),('notch5',.050,.105,.285,-10,10,20)],
  transient=dict(threshold_dbfs=-11,ratio=1.35,max_reduction_db=2,attack_ms=.6,release_ms=24,lookahead_ms=1),
  rationale='抽拉从160ms延长到290ms，保留原片自然由疏到密的摩擦；增益反降2.5dB，末端搭扣前后连贯，不添加规则扫频或合成弦。'),
 'Heng':dict(label='衡',meaning='承重镇定',action='先承接，再饱满落稳，木体自然阻尼收尾',duration=.54,
  layers=[('fist5',.080,.190,0,-5,12,25),('wood1',.079,.315,.038,0,12,30),('wood4',.105,.295,.085,3,15,30)],
  transient=dict(threshold_dbfs=-9,ratio=1.75,max_reduction_db=4.5,attack_ms=.8,release_ms=35,lookahead_ms=1),
  rationale='主承重木体由-6改0dB并保留更长自然衰减；掌面先承接，Wood_4取主击之后的中频木体在85ms接续，提供承重细节而非只堆超低频；移除皮革底层，区别于准的轻点与细擦。'),
}


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def db(value):return float(20*np.log10(max(float(value),1e-12)))
def rms(x):return float(np.sqrt(np.mean(x*x)))


def read(path):
 with warnings.catch_warnings():
  warnings.simplefilter('ignore',wavfile.WavFileWarning)
  rate,data=wavfile.read(path)
 x=data.astype(np.float64)/(2**(data.dtype.itemsize*8-1)) if data.dtype.kind=='i' else data.astype(np.float64)
 channels=1 if x.ndim==1 else x.shape[1]
 if channels>1:x=x.mean(axis=1)
 return rate,x,dict(source_sample_rate=rate,source_channels=channels,decoder_dtype=str(data.dtype),
                    source_duration_seconds=len(x)/rate,sha256=sha(path),mono_downmix='L/R arithmetic mean' if channels>1 else 'none')


def fade(x,attack_ms,release_ms):
 x=x.copy();a=min(len(x)//2,round(FS*attack_ms/1000));b=min(len(x)//2,round(FS*release_ms/1000))
 if a:x[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2
 if b:x[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
 return x


def transient_control(x,p):
 # The controls are per action. No normalization or makeup gain follows them.
 lookahead=round(FS*p['lookahead_ms']/1000)
 detector=ndimage.maximum_filter1d(np.abs(x),size=2*lookahead+1,mode='constant')
 a=math.exp(-1/(FS*p['attack_ms']/1000));r=math.exp(-1/(FS*p['release_ms']/1000))
 envelope=np.empty(len(x));v=0
 for i,value in enumerate(detector):
  c=a if value>v else r;v=c*v+(1-c)*value;envelope[i]=v
 reduction=np.minimum(np.maximum(20*np.log10(np.maximum(envelope,1e-12))-p['threshold_dbfs'],0)*(1-1/p['ratio']),p['max_reduction_db'])
 return x*10**(-reduction/20),dict(**p,actual_max_reduction_db=float(max(reduction)),makeup_gain_db=0)


def write(path,x):
 assert np.max(np.abs(x))<1, f'Clipping risk: {path.name}; revise contributing layer, never auto-normalize'
 wavfile.write(path,FS,np.round(x*32767).astype(np.int16))


def k_weight(x):
 x=signal.lfilter([1.53512485958697,-2.69169618940638,1.19839281085285],[1,-1.69065929318241,.73248077421585],x)
 return signal.lfilter([1,-2,1],[1,-1.99004745483398,.99007225036621],x)


def measure(x,reference):
 f,power=signal.welch(x,fs=FS,nperseg=4096,noverlap=3072)
 total=float(np.sum(power));bands={}
 for name,lo,hi in [('sub_20_120',20,120),('body_120_350',120,350),('mid_350_1800',350,1800),('upper_1800_3500',1800,3500),('detail_3500_8000',3500,8000),('high_8000_20000',8000,20000)]:
  bands[name]=float(100*np.sum(power[(f>=lo)&(f<hi)])/max(total,1e-20))
 padded=np.pad(x,(0,max(0,round(FS*.4)-len(x))))
 weighted=k_weight(padded)
 trend={}
 for ms in [20,100,400]:
  width=round(FS*ms/1000)
  bins=[]
  for start in range(0,len(x),width):
   chunk=padded[start:start+width]
   if len(chunk)<width:chunk=np.pad(chunk,(0,width-len(chunk)))
   bins.append(dict(start_seconds=start/FS,rms_dbfs=db(rms(chunk)),energy_sum=float(np.sum(chunk*chunk))))
  square=np.convolve(padded*padded,np.ones(width)/width,mode='valid') if width<=4800 else signal.fftconvolve(padded*padded,np.ones(width)/width,mode='valid')
  at=int(np.argmax(square))
  trend[str(ms)+'ms']=dict(nonoverlapping_bins=bins,maximum_moving_window_rms_dbfs=db(np.sqrt(square[at])),maximum_window_start_seconds=at/FS)
 truepeak=float(np.max(np.abs(signal.resample_poly(x,4,1,window=('kaiser',8.6)))))
 energy=np.cumsum(x*x);total_time_energy=float(energy[-1])
 reference400=np.pad(reference,(0,max(0,19200-len(reference))))[:19200]
 row=dict(sample_rate=FS,channels=1,pcm_bits=16,duration_seconds=len(x)/FS,peak_dbfs=db(np.max(np.abs(x))),
          estimated_true_peak_4x_dbtp=db(truepeak),true_peak_method='4x scipy.signal.resample_poly, Kaiser beta 8.6; estimate, not a certified BS.1770 true-peak meter',
          rms_dbfs=db(rms(x)),crest_factor_db=db(np.max(np.abs(x)))-db(rms(x)),
          first400_k_rms_dbfs=db(rms(weighted[:19200])),
          descriptive_K400_difference_vs_Lv_db=db(rms(weighted[:19200]))-db(rms(k_weight(reference400))),
          Lv_difference_is_acceptance_condition=False,
          first_sample_pcm=int(round(x[0]*32768)),last_sample_pcm=int(round(x[-1]*32768)),
          last10ms_peak_dbfs=db(np.max(np.abs(x[-480:]))),last20ms_rms_dbfs=db(rms(x[-960:])),
          clipped_samples=int(np.count_nonzero(np.abs(x)>=32767/32768)),
          frequency_band_energy_percent=bands,spectral_centroid_hz=float(np.sum(f*power)/max(total,1e-20)),
          energy_quantile_seconds={str(v):float(np.searchsorted(energy,total_time_energy*v)/FS) for v in [.1,.5,.9,.95,.99]},
          energy_trends=trend)
 row['technical_checks']=dict(no_pcm_clipping=row['clipped_samples']==0,true_peak_below_0dbtp=truepeak<1,
    endpoints_zero=row['first_sample_pcm']==row['last_sample_pcm']==0,last10ms_below_minus75=row['last10ms_peak_dbfs']<-75,
    length_under_prefab_lifetime=row['duration_seconds']<=.74)
 return row


def compare_layers(old,code,newlayers):
 old_layers={x['source_key']:x for x in old['recipes'][code]['layers']}
 new_layers={x['source_key']:x for x in newlayers}
 fields=['source_start_seconds','source_end_seconds','placement_seconds','gain_db','fade_in_ms','fade_out_ms']
 result=[]
 for key in sorted(set(old_layers)|set(new_layers)):
  a=old_layers.get(key);b=new_layers.get(key)
  result.append(dict(source_key=key,operation='added' if a is None else 'removed' if b is None else 'revised',
    V3={f:a[f] for f in fields} if a else None,V3_1={f:b[f] for f in fields} if b else None))
 return result


def audition(name,entries,spacing,gain):
 length=max(round(i*spacing*FS)+len(x) for i,(_,x) in enumerate(entries))+round(FS*.35)
 mixed=np.zeros(length);timeline=[]
 for i,(label,x) in enumerate(entries):
  at=round(i*spacing*FS);mixed[at:at+len(x)]+=x*gain
  timeline.append(dict(label=label,start_seconds=at/FS,end_seconds=(at+len(x))/FS))
 write(HERE/name,mixed)
 return dict(file=name,common_playback_gain_linear=gain,common_playback_gain_db=db(gain),
             peak_dbfs=db(np.max(np.abs(mixed))),estimated_true_peak_4x_dbtp=db(np.max(np.abs(signal.resample_poly(mixed,4,1,window=('kaiser',8.6))))),
             timeline=timeline,normalization=False,individual_gains_changed_for_audition=False,
             remark='Local sample summation; not Unity capture or Quest spatial playback.')


def main():
 HERE.mkdir(parents=True,exist_ok=True);(HERE/'Wav').mkdir(exist_ok=True);LOG.mkdir(parents=True,exist_ok=True)
 old=json.loads((V3/'source_recipe.json').read_text(encoding='utf-8'))
 v3_before={str(p.relative_to(V3)).replace('\\','/'):sha(p) for p in V3.rglob('*') if p.is_file()}
 main_before={code:sha(ROOT/f'Assets/Art/VFX/Skills/SealConfirmation/{code}Chime.wav') for code in ORDER}
 cached={key:read(SOURCE_ROOT/path) for key,path in SOURCES.items()}
 reference=read(ROOT/'Assets/Art/VFX/Skills/SealConfirmation/LvChime.wav')[1]
 clips={'Lv':reference};recipes={};measurements={'Lv':measure(reference,reference)};changes={}
 for code,design in DESIGNS.items():
  x=np.zeros(round(FS*design['duration']));layers=[]
  for key,start,end,placement,gain,fadein,fadeout in design['layers']:
   rate,raw,source_meta=cached[key];lo=round(start*rate);hi=round(end*rate)
   divisor=math.gcd(FS,rate);chunk=signal.resample_poly(raw[lo:hi],FS//divisor,rate//divisor)
   chunk=fade(chunk,fadein,fadeout)*10**(gain/20)
   at=round(placement*FS);x[at:at+len(chunk)]+=chunk
   source=SOURCE_ROOT/SOURCES[key]
   layers.append(dict(source_key=key,source_absolute_path=str(source.resolve()),
    source_project_relative_path=str(source.relative_to(ROOT)).replace('\\','/'),**source_meta,
    source_start_seconds=start,source_end_seconds=end,source_start_sample=lo,source_end_sample=hi,
    placement_seconds=placement,gain_db=gain,fade_in_ms=fadein,fade_out_ms=fadeout,
    time_stretch_ratio=1,pitch_shift_semitones=0,crossfade='Overlap of real snippets with per-layer sine-squared entry and cosine-squared exit',
    resampling='44.1k -> 48k polyphase, no musical time stretching'))
  x,control=transient_control(x,design['transient'])
  x=fade(x,8,25);x[0]=x[-1]=0
  write(HERE/'Wav'/f'{code}Chime.wav',x)
  clips[code]=read(HERE/'Wav'/f'{code}Chime.wav')[1]
  measurements[code]=measure(clips[code],reference)
  measurements[code]['sha256']=sha(HERE/'Wav'/f'{code}Chime.wav')
  recipes[code]=dict(label=design['label'],meaning=design['meaning'],action=design['action'],rationale=design['rationale'],
                    layers=layers,transient_control=control,output_master_gain_db=0,automatic_loudness_or_peak_matching=False)
  changes[code]=dict(rationale=design['rationale'],layer_changes=compare_layers(old,code,layers),
                     V3_transient_control=old['recipes'][code]['transient_control'],V3_1_transient_control=control,
                     V3_output_master_gain_db=old['recipes'][code]['output_master_gain_db'],V3_1_output_master_gain_db=0,
                     not_a_global_gain_rewrite=True)
  print(code,design['action'],'peak',round(measurements[code]['peak_dbfs'],2),
        '4xTP',round(measurements[code]['estimated_true_peak_4x_dbtp'],2),
        'max20/100/400ms',[round(measurements[code]['energy_trends'][f'{ms}ms']['maximum_moving_window_rms_dbfs'],2) for ms in [20,100,400]])
 auditions={}
 auditions['comparison_original_gain.wav']=audition('comparison_original_gain.wav',[(('律' if n=='Lv' else DESIGNS[n]['label'])+' '+n,clips[n]) for n in ORDER],1.2,1)
 ab=[]
 for code in ORDER:
  previous=reference if code=='Lv' else read(V3/'Wav'/f'{code}Chime.wav')[1]
  ab.extend([(code+' V3',previous),(code+' V3.1',clips[code])])
 auditions['AB_V3_V3_1.wav']=audition('AB_V3_V3_1.wav',ab,1.2,1)
 rapid=[(code+f' round{rnd+1}',clips[code]) for rnd in range(3) for code in ORDER]
 auditions['rapid_game_gain.wav']=audition('rapid_game_gain.wav',rapid,.4,.62)
 provenance=dict(source_policy_file='Logs/t01-seal-audio-v3-20261125/source-policy.json',
                  source_policy_sha256=sha(ROOT/'Logs/t01-seal-audio-v3-20261125/source-policy.json'),
                  publisher='Infinity PBR / Magic Pig Games',product_id='176417',publisher_id='4645',
                  material_truth='Existing publisher-labeled arrow handling, fist/wood contact and leather Foley. Seal meanings describe use, not newly recorded compass/paper/scale.',
                  ownership_claim='User-directed existing project assets per recorded policy. No new purchase receipt or account entitlement independently inspected.',
                  synthetic_layers=False,raw_library_redistributed=False,raw_audio_uploaded=False)
 recipe=dict(version='3.1',status='ready_for_parent_review_not_deployed',provenance=provenance,
             design_policy='Per-action layers, timings, natural damping and limited individual transient control. Lv is unchanged reference only; no relative-Lv or five-seal equal-energy acceptance condition.',
             original_V3_recipe_sha256=sha(V3/'source_recipe.json'),recipes=recipes,auditions=auditions,
             subjective_listening='Not performed by audio worker; numerical QA is not user approval.')
 preserved=dict(V3_files_unchanged=v3_before=={str(p.relative_to(V3)).replace('\\','/'):sha(p) for p in V3.rglob('*') if p.is_file()},
                production_source_files_unchanged=main_before=={code:sha(ROOT/f'Assets/Art/VFX/Skills/SealConfirmation/{code}Chime.wav') for code in ORDER},
                original_Foley_unchanged=all(sha(SOURCE_ROOT/SOURCES[key])==meta['sha256'] for key,(_,_,meta) in cached.items()))
 qa=dict(version='3.1',measurements=measurements,auditions=auditions,preservation=preserved,
         technical_pass=all(all(measurements[code]['technical_checks'].values()) for code in ORDER[1:]),
         no_uniform_loudness_or_peak_target=True,relative_Lv_numbers_are_descriptive_only=True,
         Unity_overlap_acceptance='Pending independent Unity AudioRenderer test; offline .62 rapid preview is not a Unity result.',
         listening_acceptance='Pending user comments; no human-listening claim.')
 for name,obj in [('source_recipe.json',recipe),('layer_changes_from_v3.json',changes),('qa.json',qa)]:
  (HERE/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 manifest=dict(created_utc=datetime.now(timezone.utc).isoformat(),command='python -X utf8 ArtSource/Audio/SealConfirmationV3_1/render_v3_1.py',
               candidate_sha256={code:sha(HERE/'Wav'/f'{code}Chime.wav') for code in ORDER[1:]},
               original_V3_manifest=v3_before,main_source_manifest_before=main_before,preservation=preserved,
               audio_worker_did_not_start_Unity=True)
 (LOG/'render-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 assert qa['technical_pass'], 'Technical waveform check failed; inspect qa.json. Assets untouched.'
 assert all(preserved.values()), 'A read-only input changed during run; inspect provenance'
 print('PASS technical waveform and preservation checks. No Lv/loudness equality condition applied. Await Unity/user acceptance.')


if __name__=='__main__':main()
