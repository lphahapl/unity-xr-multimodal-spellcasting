"""Read-only V2 Unity final-mix analysis. Writes only unity-output-qa.json.

python -X utf8 ArtSource/Audio/SealConfirmationV2/unity-output-qa.py

Requires completed Unity result.txt, six isolated mixes and Rapid/onsets.tsv.
Matches COMPLETE AudioClips; Ju's internal two impacts are not two events.
No renderer, source WAV, Unity project, metadata, or other QA file is changed.
"""
from datetime import datetime, timezone
import csv
import hashlib
import json
from pathlib import Path

import numpy as np
from scipy import ndimage, signal
from scipy.io import wavfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
BASE=ROOT/'Logs/t01-seal-audio-v2-20261125/unity'
ASSETS=ROOT/'Assets/Art/VFX/Skills/SealConfirmation'
ORDER=['Gui','Ju','Heng','Lv','Zhun','Sheng']
FS=48000


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def db(value):return float(20*np.log10(max(float(value),1e-12)))
def rms(x):return float(np.sqrt(np.mean(x*x)))
def tsv(path):return list(csv.DictReader(path.open(encoding='utf-8-sig'),delimiter='\t'))


def load(path,channels):
    rate,pcm=wavfile.read(path)
    assert rate==FS and pcm.dtype==np.int16
    assert (pcm.ndim==1 if channels==1 else pcm.ndim==2 and pcm.shape[1]==channels)
    return pcm.astype(np.float64)/32768,pcm


def wave_summary(path,channels):
    x,pcm=load(path,channels)
    return dict(file=str(path.relative_to(ROOT)).replace('\\','/'),sha256=sha(path),
                sample_rate=FS,channels=channels,pcm_bits=16,sample_frames=len(x),duration_seconds=len(x)/FS,
                peak_dbfs=db(np.max(np.abs(x))),whole_file_rms_dbfs=db(rms(x)),
                clipped_samples=int(np.count_nonzero(np.abs(pcm.astype(np.int32))>=32767)),
                last100ms_peak_dbfs=db(np.max(np.abs(x[-4800:]))))


def envelope_heads(x):
    if x.ndim==2:x=np.sqrt(np.mean(x*x,axis=1))
    env=np.sqrt(ndimage.uniform_filter1d(x*x,round(FS*.004),mode='constant'))
    active=np.flatnonzero(env>=max(env)*.025)
    if not len(active):return []
    groups=np.split(active,np.flatnonzero(np.diff(active)>round(FS*.012))+1)
    return [dict(start_seconds=float(g[0]/FS),end_seconds=float((g[-1]+1)/FS),
                 peak_time_seconds=float((g[0]+np.argmax(env[g[0]:g[-1]+1]))/FS)) for g in groups]


def find_start(x,source,nominal):
    # ±85ms encompasses AudioRenderer DSP block scheduling at 30 capture fps.
    lo=max(0,int(round(nominal*FS))-4080)
    max_start=min(len(x)-len(source),int(round(nominal*FS))+4080)
    assert max_start>=lo
    search=np.mean(x[lo:max_start+len(source)],axis=1)
    corr=signal.correlate(search,source,mode='valid',method='fft')
    return lo+int(np.argmax(corr))


def fit_complete_clips(x,events,sources):
    # Joint least squares separates overlapping sources at the same listener.
    # Using an individual output as the reference would hide source alterations;
    # use locked production mono WAVs and report Unity's residual explicitly.
    matrix=np.zeros((len(x),len(events)),dtype=np.float64)
    starts=[]
    for i,event in enumerate(events):
        source=sources[event['code']]
        start=find_start(x,source,event['nominal_seconds'])
        assert start+len(source)<=len(x)
        starts.append(start)
        matrix[start:start+len(source),i]=source
    gram=matrix.T@matrix
    gains=np.linalg.solve(gram,matrix.T@x)
    predicted=matrix@gains
    residual=x-predicted
    details=[]
    for i,event in enumerate(events):
        source=sources[event['code']];start=starts[i];end=start+len(source)
        own=matrix[start:end,i,None]*gains[i]
        isolated=x[start:end]-(predicted[start:end]-own)
        diff=isolated-own
        corr=[float(np.corrcoef(source,isolated[:,ch])[0,1]) for ch in range(2)]
        env=np.sqrt(ndimage.uniform_filter1d(source*source,round(FS*.004),mode='constant'))
        active=np.flatnonzero(env>=max(env)*.01)
        tail_end=min(len(source),int(active[-1])+1)
        tail_start=max(0,tail_end-round(FS*.030))
        expected_tail=own[tail_start:tail_end]
        actual_tail=isolated[tail_start:tail_end]
        observed_heads=envelope_heads(isolated)
        intended=2 if event['code']=='Ju' else 1
        expected_rms=rms(expected_tail)
        actual_rms=rms(actual_tail)
        detail={**event,'fit_start_sample':start,'fit_start_seconds':start/FS,
                'fit_end_seconds':end/FS,'source_duration_seconds':len(source)/FS,
                'start_minus_nominal_ms':1000*(start/FS-event['nominal_seconds']),
                'source_gain_per_channel':[float(v) for v in gains[i]],
                'full_source_correlation_per_channel':corr,
                'fit_residual_rms_dbfs':db(rms(diff)),
                'isolated_actual_peak_dbfs':db(np.max(np.abs(isolated))),
                'intended_internal_head_count':intended,
                'measured_internal_envelope_head_count':len(observed_heads),
                'internal_envelope_heads':observed_heads,
                'energetic_tail_relative_start_seconds':tail_start/FS,
                'energetic_tail_relative_end_seconds':tail_end/FS,
                'energetic_tail_expected_rms_dbfs':db(expected_rms),
                'energetic_tail_observed_rms_dbfs':db(actual_rms),
                'energetic_tail_rms_ratio_db':db(actual_rms/max(expected_rms,1e-12)),
                'energetic_tail_residual_rms_dbfs':db(rms(actual_tail-expected_tail)),
                'complete_window_last10ms_expected_peak_dbfs':db(np.max(np.abs(own[-480:]))),
                'complete_window_last10ms_isolated_actual_peak_dbfs':db(np.max(np.abs(isolated[-480:]))),
                'complete_clip_count':1,
                'checks':dict(correlation=min(corr)>.99,
                              energetic_tail_level=abs(db(actual_rms/max(expected_rms,1e-12)))<1,
                              intended_internal_heads=len(observed_heads)==intended,
                              full_source_window_present=end<=len(x))}
        details.append(detail)
    return dict(events=details,actual_peak_dbfs=db(np.max(np.abs(x))),
                global_source_mix_reconstruction_rms_dbfs=db(rms(predicted)),
                global_fit_residual_rms_dbfs=db(rms(residual)),
                global_fit_residual_peak_dbfs=db(np.max(np.abs(residual))),
                global_fit_residual_to_mix_rms_db=db(rms(residual)/max(rms(x),1e-12)),
                last100ms_actual_peak_dbfs=db(np.max(np.abs(x[-4800:]))),
                joint_fit_gram_condition=float(np.linalg.cond(gram)))


def main():
    assert (BASE/'result.txt').exists(), 'Wait for completed Unity run; result.txt absent'
    assert (BASE/'Rapid/unity-output.wav').exists(), 'Wait for Rapid final audio'
    assert (BASE/'Rapid/onsets.tsv').exists(), 'Wait for final complete-instance manifest'
    result_text=(BASE/'result.txt').read_text(encoding='utf-8-sig')
    assert 'PASS:' in result_text and 'Rapid:' in result_text
    source_locked=json.loads((HERE/'qa.json').read_text(encoding='utf-8'))
    sources={code:load(ASSETS/f'{code}Chime.wav',1)[0] for code in ORDER}
    source_files={code:wave_summary(ASSETS/f'{code}Chime.wav',1) for code in ORDER}
    for code in ORDER:
        expected=(source_locked['reference_sha256'] if code=='Lv' else source_locked['clips'][code]['sha256'])
        assert source_files[code]['sha256']==expected, f'Locked source changed: {code}'
    timeline=tsv(BASE/'timeline.tsv')
    individual_rows=[]
    for row in timeline:
        if row['type']=='rapid_begin':break
        individual_rows.append(row)
    report=dict(created_utc=datetime.now(timezone.utc).isoformat(),status='analyzed',
                task='Independent numerical acceptance of final Unity AudioRenderer V2 output',
                runtime_result_sha256=sha(BASE/'result.txt'),runtime_timeline_sha256=sha(BASE/'timeline.tsv'),
                runtime_result_text=result_text,source_files=source_files,
                method={
                    'clip_count':'Counts playback instances in timeline and complete matched source windows, NOT nonzero audio fragments.',
                    'Ju':'Two intended wooden impacts at 0/.095s are inside ONE AudioClip; two played clips contain four intended impacts.',
                    'fit':'Joint least-squares mono-source gains per output stereo channel, with full source windows located by cross-correlation near logged start offsets.',
                    'rapid_overlap':'Subtract other jointly fitted sources before measuring each clip and its energetic tail; no individual source replacement is used as final output.',
                    'heads':'4ms RMS >=2.5% of max, merge gaps <=12ms. A descriptive source-envelope count; event count comes from matched complete clips and timeline.',
                    'tail':'Last 30ms before source RMS drops below 1% of its peak envelope, plus complete-source-window and ending-silence checks.',
                    'correlation_limit':'Correlation here verifies delivery of the expected waveform; it is NOT a metric of perceptual distinguishability.',
                    'limits':'No human listening, no Quest headset loudness/occlusion/live recognition acceptance.'},
                individual={})
    all_checks=[]
    for stage,code in enumerate(ORDER):
        path=BASE/code/'unity-output.wav';x,_=load(path,2)
        triggers=[r for r in individual_rows if int(r['stage'])==stage and r['type'] in ('confirmed','template_preview')]
        events=[dict(ordinal=i,code=code,kind=r['type'],timeline_frame=int(r['frame']),
                     nominal_seconds=int(r['frame'])/30,timeline_detail=r['detail']) for i,r in enumerate(triggers)]
        fit=fit_complete_clips(x,events,sources)
        fit.update(wave=wave_summary(path,2),matched_complete_clip_count=len(events),
                   matcher_confirmation_count=sum(e['kind']=='confirmed' for e in events),
                   direct_template_preview_count=sum(e['kind']=='template_preview' for e in events),
                   intended_total_internal_head_count=sum(e['intended_internal_head_count'] for e in fit['events']))
        checks=dict(expected_clip_count=len(events)==(2 if stage<4 else 1),
                    zero_clipping=fit['wave']['clipped_samples']==0,
                    ending_silence=fit['wave']['last100ms_peak_dbfs']<=-200,
                    source_reconstruction=fit['global_fit_residual_to_mix_rms_db']<-25,
                    all_clip_checks=all(all(e['checks'].values()) for e in fit['events']))
        fit['checks']=checks;all_checks.append(all(checks.values()));report['individual'][code]=fit
        print(code,'clips',len(events),'heads',fit['intended_total_internal_head_count'],
              'peak',round(fit['actual_peak_dbfs'],3),'residual/mix',round(fit['global_fit_residual_to_mix_rms_db'],2),checks)
    onset_rows=tsv(BASE/'Rapid/onsets.tsv')
    rapid_events=[]
    for r in onset_rows:
        code=r['clip'].removesuffix('Chime')
        rapid_events.append(dict(ordinal=int(r['ordinal']),code=code,kind=r['kind'],
                                 instance_id=int(r['instance']),timeline_frame=int(r['frame']),
                                 nominal_seconds=int(r['pcmFrameOffset'])/FS,
                                 nominal_frame_seconds=int(r['frame'])/30,
                                 logged_pcm_frame_offset=int(r['pcmFrameOffset']),
                                 source_clip_samples=int(r['clipSamples']),
                                 max_observed_source_cursor=int(r['maxCursor']),
                                 source_completed=r['completed'].lower()=='true'))
    rapid_path=BASE/'Rapid/unity-output.wav';x,_=load(rapid_path,2)
    rapid=fit_complete_clips(x,rapid_events,sources)
    rapid.update(wave=wave_summary(rapid_path,2),onsets_sha256=sha(BASE/'Rapid/onsets.tsv'),
                 sources_tsv_sha256=sha(BASE/'Rapid/sources.tsv'),matched_complete_clip_count=len(rapid_events),
                 matcher_confirmation_count=sum(e['kind']=='matcher' for e in rapid_events),
                 direct_template_preview_count=sum(e['kind']=='direct_template_preview' for e in rapid_events),
                 distinct_instance_count=len(set(e['instance_id'] for e in rapid_events)),
                 intended_total_internal_head_count=sum(2 if e['code']=='Ju' else 1 for e in rapid_events))
    # Clip life intervals overlap, irrespective of whether quiet source tails are audible.
    edges=[]
    for e in rapid['events']:
        edges.extend([(e['fit_start_seconds'],1),(e['fit_end_seconds'],-1)])
    concurrent=maximum=0
    for _,delta in sorted(edges):concurrent+=delta;maximum=max(maximum,concurrent)
    rapid['maximum_overlapping_complete_clip_windows']=maximum
    checks=dict(twelve_clips=len(rapid_events)==12,eight_matcher=rapid['matcher_confirmation_count']==8,
                four_templates=rapid['direct_template_preview_count']==4,
                twelve_distinct_instances=rapid['distinct_instance_count']==12,
                complete_cursor_records=all(e['source_completed'] and e['max_observed_source_cursor']>=e['source_clip_samples']-3200 for e in rapid_events),
                simultaneous_sources=maximum>=2,zero_clipping=rapid['wave']['clipped_samples']==0,
                ending_silence=rapid['wave']['last100ms_peak_dbfs']<=-200,
                source_reconstruction=rapid['global_fit_residual_to_mix_rms_db']<-25,
                all_clip_checks=all(all(e['checks'].values()) for e in rapid['events']))
    rapid['checks']=checks;all_checks.append(all(checks.values()));report['rapid']=rapid
    report['all_checks_pass']=all(all_checks)
    report['status']='numerical_checks_passed' if report['all_checks_pass'] else 'numerical_checks_failed'
    report['listening_acceptance']='NOT PERFORMED; numerical waveform delivery and mixing evidence only.'
    report['source_hashes_unchanged_during_analysis']=all(sha(ASSETS/f'{code}Chime.wav')==source_files[code]['sha256'] for code in ORDER)
    (HERE/'unity-output-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Rapid clips',len(rapid_events),'heads',rapid['intended_total_internal_head_count'],
          'peak',round(rapid['actual_peak_dbfs'],3),'residual/mix',round(rapid['global_fit_residual_to_mix_rms_db'],2),checks)
    assert report['all_checks_pass'],'See unity-output-qa.json for exact failed checks; production files untouched'
    assert report['source_hashes_unchanged_during_analysis']
    print('PASS: actual Unity final mixes match complete locked source clips and timing manifests.')


if __name__=='__main__':main()
