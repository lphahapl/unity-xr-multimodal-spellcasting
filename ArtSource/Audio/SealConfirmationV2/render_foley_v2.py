"""T-01 V2: deterministic synthetic tool-gesture Foley, not real recordings.

python -X utf8 ArtSource/Audio/SealConfirmationV2/render_foley_v2.py
python -X utf8 ArtSource/Audio/SealConfirmationV2/render_foley_v2.py --publish

First writes V2/Wav candidates, validates all of them, then makes audition files.
--publish copies ONLY five passed WAVs into Assets, preserving all metadata.
Requires numpy and scipy. No Unity, network, limiter, or existing sample edits.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil

import numpy as np
from scipy import ndimage, signal
from scipy.io import wavfile


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ASSETS = ROOT / 'Assets/Art/VFX/Skills/SealConfirmation'
V1 = ROOT / 'ArtSource/Audio/SealConfirmationV1/Wav'
WAV = HERE / 'Wav'
FS = 48000
N400 = round(FS*.4)
LV_SHA = '603a8782c8355ec8d0af278f510e32e2bc684079444d9e118add0724277825f4'
ORDER = ['Lv', 'Gui', 'Ju', 'Zhun', 'Sheng', 'Heng']
NEW = ORDER[1:]
DESIGNS = {
    'Lv': dict(label='律', role='保留已认可的厚铜鸣', material='V1原文件，只读',
               intended_onsets_seconds=[0], source='Existing approved LvChime.wav'),
    'Gui': dict(label='规', role='短环擦回旋', material='摩擦噪声/回旋谱扫主导，非钟体',
                physical_approximation='Three overlapping filtered-noise friction bands follow one curved spectral sweep; one light initial touch.',
                intended_onsets_seconds=[0], duration=.46, seed=5201,
                envelope='4ms触面，约180ms连续环擦，快速收住',
                distinguishing_axis='宽带粗糙摩擦 + 高频向内收拢的连续运动',
                controls={'sweep_band_centers_hz':[1050,2400,4750],'friction_support_seconds':.215}),
    'Ju': dict(label='矩', role='两记干木尺相合，第二记锁定', material='干木接触 + 极短低Q木体',
               physical_approximation='Two independent short band-limited wood contacts and heavily damped signed modes, 95ms apart.',
               intended_onsets_seconds=[0,.095], duration=.36, seed=5202,
               envelope='两音头间隔95ms，第二记重音，两个木击尾均截止到70ms',
               distinguishing_axis='有意双击节奏；不是两次成印事件',
               controls={'inter_impact_seconds':.095,'first_impact_amplitude':.68,'second_impact_amplitude':1.0,'impact_support_seconds':.070}),
    'Zhun': dict(label='准', role='一滴落定清点', material='短石质点触 + 极短浅下滑共鸣',
                 physical_approximation='A short rough point contact plus rapidly damped low-Q shallow pitch-settling modes, not a cartoon water drop.',
                 intended_onsets_seconds=[0], duration=.34, seed=5203,
                 envelope='单个清点，前25ms浅下滑，145ms前收住',
                 distinguishing_axis='孤立短点、轻清收定；无长钟尾/水泡弹性',
                 controls={'settling_hz':[1390,1300],'settling_tau_seconds':.012,'point_support_seconds':.145}),
    'Sheng': dict(label='绳', role='绷弦短拨与纤维收紧', material='Karplus–Strong拨弦近似 + 干纤维摩擦',
                  physical_approximation='Single seeded noise excitation of a lossy delay loop; delayed-difference pluck-position filter; overlapping fiber tension friction.',
                  intended_onsets_seconds=[0], duration=.44, seed=5204,
                  envelope='一次拨动，约140ms绷紧余振与纤维短擦，干燥收尾',
                  distinguishing_axis='短谐波梳状弦体 + 纤维质地；不使用钟的非谐模态族',
                  controls={'delay_samples':119,'loop_feedback':.988,'pluck_position_ratio':.23,'external_damping_tau_seconds':.132}),
    'Heng': dict(label='衡', role='低秤杆摆停/砝码落座', material='厚闷落座 + 低Q杆体 + 短机械摩擦',
                 physical_approximation='Dense low-mid filtered impact and heavily damped bar modes, with a quiet continuous settling rub instead of a second hit.',
                 intended_onsets_seconds=[0], duration=.48, seed=5205,
                 envelope='单个厚落座，后接轻微机械余动，约200ms止住',
                 distinguishing_axis='中低频实体重量，保留350–1800Hz小扬声器线索',
                 controls={'body_modes_hz':[183,371,647,1133],'highpass_hz':130,'settling_support_seconds':.215}),
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rms(x) -> float:
    return float(np.sqrt(np.mean(np.square(x))))


def db(x: float) -> float:
    return float(20*np.log10(max(float(x),1e-12)))


def k_weight(x):
    x=signal.lfilter([1.53512485958697,-2.69169618940638,1.19839281085285],
                     [1,-1.69065929318241,.73248077421585],x)
    return signal.lfilter([1,-2,1],[1,-1.99004745483398,.99007225036621],x)


def first400(x):
    return np.pad(x,(0,max(0,N400-len(x))))[:N400]


def load(path):
    rate,data=wavfile.read(path)
    assert rate==FS and data.dtype==np.int16 and data.ndim==1, path
    return data.astype(np.float64)/32768


def bandnoise(rng,n,low,high):
    x=signal.sosfilt(signal.butter(2,[low,high],fs=FS,btype='bandpass',output='sos'),rng.standard_normal(n))
    return x/max(rms(x),1e-12)


def smooth_attack(t,seconds):
    return np.sin(np.minimum(t/seconds,1)*np.pi/2)**2


def finite(t,stop,fade=.015):
    return np.where(t<stop-fade,1,np.cos(np.minimum(np.maximum((t-stop+fade)/fade,0),1)*np.pi/2)**2)*(t<stop)


def modes(t,items):
    return sum(amp*np.sin(2*np.pi*freq*t)*np.exp(-t/tau) for freq,amp,tau in items)


def render_gui(t,rng):
    n=len(t)
    low=bandnoise(rng,n,550,1800)
    mid=bandnoise(rng,n,1600,3300)
    high=bandnoise(rng,n,3200,6500)
    # One elliptical rubbing motion: spectral movement, no pitched bell body.
    travel=np.sin(np.pi*np.minimum(t/.215,1))**1.2
    friction=(.72*(1-.55*travel)*low + .90*travel*mid + .43*travel**2*high)
    friction*=smooth_attack(t,.004)*np.exp(-t/.105)*finite(t,.215,.035)
    contact=bandnoise(rng,n,1000,5200)*np.exp(-t/.0045)*.13
    return friction+contact*smooth_attack(t,.001)


def wood_contact(t,rng):
    dry=bandnoise(rng,len(t),700,6400)*np.exp(-t/.017)*.28
    body=modes(t,[(472,.70,.061),(1017,-.72,.0465),(1879,.38,.023),(2941,-.16,.0125)])
    return (dry+body)*smooth_attack(t,.0045)*finite(t,.070,.015)


def render_ju(t,rng):
    first=wood_contact(t,rng)
    second=wood_contact(t,rng)
    shift=round(FS*.095)
    return .68*first+np.pad(second[:-shift],(shift,0))


def render_zhun(t,rng):
    # A shallow 90 Hz, 12ms settling trajectory. Rapid damping suppresses boing.
    phase=2*np.pi*(1300*t+90*.012*(1-np.exp(-t/.012)))
    point=np.sin(phase)*np.exp(-t/.065)*.66
    point+=np.sin(2*np.pi*(2180*t+120*.007*(1-np.exp(-t/.007))))*np.exp(-t/.014)*.24
    grit=bandnoise(rng,len(t),1400,5900)*np.exp(-t/.017)*.20
    base=bandnoise(rng,len(t),700,1800)*np.exp(-t/.038)*.17
    return (point+grit+base)*smooth_attack(t,.0012)*finite(t,.145,.025)


def render_sheng(t,rng):
    n=len(t); delay=119
    raw=rng.standard_normal(delay)
    # Excitation notch simulates plucking away from the bridge; one excitation.
    raw=raw-np.roll(raw,round(delay*.23))
    raw=signal.lfilter(signal.firwin(17,2500,fs=FS),[1],raw)
    raw-=np.mean(raw)
    x=np.zeros(n); x[:delay]=raw
    for i in range(delay,n):
        x[i]=.988*.5*(x[i-delay]+x[i-delay-1])
    x*=np.exp(-t/.132)
    fiber=bandnoise(rng,n,2100,7900)*smooth_attack(t,.004)*np.exp(-t/.032)*.105
    return (x+fiber)*smooth_attack(t,.0038)*finite(t,.285,.040)


def render_heng(t,rng):
    body=modes(t,[(183,.32,.055),(371,-.85,.075),(647,.35,.039),(1133,-.14,.022)])
    seat=bandnoise(rng,len(t),150,1250)*np.exp(-t/.043)*.35
    face=bandnoise(rng,len(t),1100,4200)*np.exp(-t/.008)*.19
    settle=bandnoise(rng,len(t),350,1700)*smooth_attack(t,.008)*np.exp(-t/.059)*.13
    x=(body+seat+face+settle)*smooth_attack(t,.0035)*finite(t,.215,.035)
    return signal.sosfilt(signal.butter(2,130,fs=FS,btype='highpass',output='sos'),x)


RENDERERS={'Gui':render_gui,'Ju':render_ju,'Zhun':render_zhun,'Sheng':render_sheng,'Heng':render_heng}


def render(name,ref):
    cfg=DESIGNS[name]
    t=np.arange(round(FS*cfg['duration']))/FS
    x=RENDERERS[name](t,np.random.default_rng(cfg['seed']))
    # Remove irrelevant subsonic offset and ensure every file ends at exact zero.
    x=signal.sosfilt(signal.butter(2,90,fs=FS,btype='highpass',output='sos'),x)
    x*=finite(t,cfg['duration']-.012,.030)
    x[0]=x[-1]=0
    target=db(rms(k_weight(first400(ref))))
    gain=10**((target-db(rms(k_weight(first400(x)))))/20)
    x*=gain
    peak=db(np.max(np.abs(x)))
    assert peak<0, f'{name} would clip ({peak:.2f}dB); redesign before serialization'
    return np.round(x*32767).astype(np.int16),dict(pre_quantization_gain=gain,pre_quantization_peak_dbfs=peak)


def runs(indices,gap):
    if len(indices)==0:return []
    return np.split(indices,np.flatnonzero(np.diff(indices)>gap)+1)


def analyze(name,x,ref):
    f,p=signal.welch(x,fs=FS,nperseg=4096,noverlap=3072)
    band=(f>=100)&(f<=8000)
    q=p[band]+1e-20
    flatness=float(np.exp(np.mean(np.log(q)))/np.mean(q))
    peak=float(np.max(np.abs(x)))
    env=np.sqrt(ndimage.uniform_filter1d(x*x,size=round(FS*.004),mode='constant'))
    active=np.flatnonzero(env>=np.max(env)*.025)
    episodes=runs(active,round(FS*.012))
    episode_rows=[]
    for g in episodes:
        lo,hi=int(g[0]),int(g[-1])+1
        half=np.flatnonzero(env[lo:hi]>=np.max(env[lo:hi])*.5)
        episode_rows.append(dict(start_seconds=lo/FS,end_seconds=hi/FS,
                                 peak_envelope_time_seconds=(lo+int(np.argmax(env[lo:hi])))/FS,
                                 envelope_fwhm_span_seconds=float((half[-1]-half[0]+1)/FS),
                                 envelope_time_above_half_peak_seconds=len(half)/FS,
                                 energy=float(np.sum(x[lo:hi]**2))))
    energy=np.cumsum(x*x); total=float(energy[-1])
    quantiles={str(v):float(np.searchsorted(energy,total*v)/FS) for v in [.1,.5,.9,.95,.99]}
    xx=first400(x); ac=signal.correlate(xx,xx,mode='full',method='fft')[len(xx)-1:]
    ac=ac/max(ac[0],1e-20)
    periodicity=float(np.max(ac[round(FS*.002):round(FS*.015)]))
    k400=db(rms(k_weight(first400(x))))
    row=dict(label=DESIGNS[name]['label'],sample_rate=FS,channels=1,pcm_bits=16,
             duration_seconds=len(x)/FS,peak_dbfs=db(peak),whole_file_rms_dbfs=db(rms(x)),
             first400_raw_rms_dbfs=db(rms(first400(x))),first400_k_rms_dbfs=k400,
             first400_k_delta_vs_Lv_db=k400-db(rms(k_weight(first400(ref)))),
             first_sample_pcm=int(round(x[0]*32768)),last_sample_pcm=int(round(x[-1]*32768)),
             last10ms_peak_dbfs=db(np.max(np.abs(x[-480:]))),clipped_samples=int(np.sum(np.abs(x)>=32767/32768)),
             spectral_centroid_hz=float(np.sum(f*p)/np.sum(p)),
             spectral_flatness_100_8000=flatness,
             flatness_definition='geometric / arithmetic mean Welch PSD in 100-8000Hz; a broad noise proxy, not a perception score',
             autocorrelation_peak_lag_2_to_15ms=periodicity,
             midband_350_to_1800_energy_percent=float(100*np.sum(p[(f>=350)&(f<=1800)])/np.sum(p)),
             sub150_energy_percent=float(100*np.sum(p[f<150])/np.sum(p)),
             energy_quantile_seconds=quantiles,
             energy_time_centroid_seconds=float(np.sum(np.arange(len(x))/FS*x*x)/total),
             intended_onsets_seconds=DESIGNS[name]['intended_onsets_seconds'],
             intended_onset_count=len(DESIGNS[name]['intended_onsets_seconds']),
             measured_envelope_episode_count=len(episodes),measured_envelope_episodes=episode_rows,
             envelope_4ms_episode_rule='4ms RMS >=2.5% of max RMS, joining silent gaps <=12ms; ringing extrema are not counted separately',
             envelope_10ms_rms_dbfs=[round(db(rms(x[i:i+480])),2) for i in range(0,len(x),480)])
    if name!='Lv':
        checks=dict(duration=row['duration_seconds']<=.74,peak_headroom=row['peak_dbfs']<=-2.5,
                    energy_match=abs(row['first400_k_delta_vs_Lv_db'])<=.3,
                    endpoints=row['first_sample_pcm']==row['last_sample_pcm']==0,
                    tail=row['last10ms_peak_dbfs']<-75,no_clipping=row['clipped_samples']==0,
                    intended_episode_count=len(episodes)==row['intended_onset_count'])
        row['checks']=checks
        row['passed']=all(checks.values())
    return row


def pcm_write(path,x):
    assert np.max(np.abs(x))<1
    wavfile.write(path,FS,np.round(x*32767).astype(np.int16))


def loudness_equal(x,ref):
    gain=10**((db(rms(k_weight(first400(ref))))-db(rms(k_weight(first400(x)))))/20)
    return x*gain,gain


def auditions(clips,ref):
    records={}
    def sequential(filename,entries,interval):
        length=round(FS*(interval*len(entries)+.2))
        mix=np.zeros(length); times=[]
        for i,(label,x) in enumerate(entries):
            x,gain=loudness_equal(x,ref)
            start=round(i*interval*FS)
            mix[start:start+len(x)]+=x
            times.append(dict(label=label,start_seconds=start/FS,energy_alignment_gain_db=db(gain)))
        common=min(1,10**(-2.5/20)/max(np.max(np.abs(mix)),1e-12))
        mix*=common
        pcm_write(HERE/filename,mix)
        records[filename]=dict(interval_seconds=interval,common_mix_gain_db=db(common),
                               output_peak_dbfs=db(np.max(np.abs(mix))),order=times)
    sequential('comparison_equal.wav',[(DESIGNS[n]['label']+' '+n,clips[n]) for n in ORDER],1.2)
    ab=[]
    for name in ORDER:
        ab.extend([(DESIGNS[name]['label']+' '+name+' V1',load(V1/f'{name}Chime.wav')),
                   (DESIGNS[name]['label']+' '+name+' V2',clips[name])])
    sequential('AB.wav',ab,1.2)
    rapid=[(DESIGNS[n]['label']+' '+n+f' round{rnd+1}',clips[n]) for rnd in range(3) for n in ORDER]
    sequential('rapid.wav',rapid,.4)
    records['rapid.wav']['mix_note']='18 events, 3 rounds. Production relative energies preserved, one common output gain only; no per-seal compressor or ducking. Ju has 2 intended internal hits, not extra seal events.'
    return records


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--publish',action='store_true')
    args=parser.parse_args()
    assert sha(ASSETS/'LvChime.wav')==LV_SHA
    before={p.name:sha(p) for p in ASSETS.glob('*.meta')}
    baseline=json.loads((ROOT/'Logs/t01-seal-audio-v2-20261125/before.json').read_text(encoding='utf-8-sig'))
    ref=load(ASSETS/'LvChime.wav')
    WAV.mkdir(parents=True,exist_ok=True)
    clips={'Lv':ref}; gains={}
    for name in NEW:
        data,gain=render(name,ref)
        wavfile.write(WAV/f'{name}Chime.wav',FS,data)
        clips[name]=load(WAV/f'{name}Chime.wav'); gains[name]=gain
    qa={n:analyze(n,clips[n],ref) for n in ORDER}
    for name in NEW:qa[name]['sha256']=sha(WAV/f'{name}Chime.wav')
    passing=all(qa[n]['passed'] for n in NEW)
    report=dict(version=2,provenance='Deterministic synthetic Foley approximations; not real acoustic recordings.',
                reference_sha256=LV_SHA,production_source_before={n:sha(ASSETS/f'{n}Chime.wav') for n in NEW},
                clips=qa,render_gains=gains,all_candidates_pass=passing,
                listening_acceptance='NOT PERFORMED: numerical measures do not establish perceived distinction or artistic acceptance.',
                unity_playback='NOT PERFORMED by audio worker for V2',published=False)
    (HERE/'designs.json').write_text(json.dumps(DESIGNS,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for name in ORDER:
        r=qa[name]
        print(f"{name:5s} dur={r['duration_seconds']:.2f}s peak={r['peak_dbfs']:.2f} Kdiff={r['first400_k_delta_vs_Lv_db']:+.3f} "
              f"centroid={r['spectral_centroid_hz']:.0f} flatness={r['spectral_flatness_100_8000']:.5f} "
              f"episodes={r['measured_envelope_episode_count']}/{r['intended_onset_count']} checks={r.get('checks','reference')}")
    if passing:
        report['auditions']=auditions(clips,ref)
        if args.publish:
            for name in NEW:shutil.copyfile(WAV/f'{name}Chime.wav',ASSETS/f'{name}Chime.wav')
            report['published']=True
            report['published_at_utc']=datetime.now(timezone.utc).isoformat()
    report['production_source_after']={n:sha(ASSETS/f'{n}Chime.wav') for n in NEW}
    report['metadata_hashes_unchanged']=before=={p.name:sha(p) for p in ASSETS.glob('*.meta')}
    report['baseline_unchanged_except_authorized_five_wavs']=all(
        sha(ROOT/path)==old for path,old in baseline.items()
        if path not in [f'Assets/Art/VFX/Skills/SealConfirmation/{n}Chime.wav' for n in NEW])
    assert sha(ASSETS/'LvChime.wav')==LV_SHA
    assert report['metadata_hashes_unchanged']
    (HERE/'qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    assert passing, 'Candidate QA failed; production assets not changed. Inspect V2/qa.json.'
    print('PASS: candidate format, energy, headroom, endpoints, tail and intended hit counts.')
    print('Published five WAVs.' if args.publish else 'Candidate-only run. Production audio not changed.')


if __name__=='__main__':main()
