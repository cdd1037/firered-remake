"""Bounded original-driver comparison, without gain fitting or time stretching.
Development analysis only; does not implement or modify the runtime synthesizer.
"""
from pathlib import Path
import hashlib,json,wave
import numpy as np
from scipy.signal import welch
root=Path(__file__).resolve().parents[1]
output=root/'results/phase6-comparison';output.mkdir(exist_ok=True)
def load(path):
    with wave.open(str(path)) as f:
        assert f.getframerate()==48000 and f.getnchannels()==2 and f.getsampwidth()==2
        return np.frombuffer(f.readframes(f.getnframes()),'<i2').reshape(-1,2).astype(float)
def metric(x):
    f,p=welch(x[:,0],48000,nperseg=8192)
    total=float(p.sum())
    bands={f'{lo}-{hi}':float(p[(f>=lo)&(f<hi)].sum()/total) if total else 0
           for lo,hi in [(0,500),(500,2000),(2000,8000),(8000,16000),(16000,24001)]}
    peak=float(np.max(np.abs(x)))
    active=np.flatnonzero(np.max(np.abs(x),axis=1)>max(1,peak*.001))
    return {'rms':float(np.sqrt(np.mean(x*x))),'peak':peak,'powerFractionsHz':bands,
            'lastFrameAboveMinus60Db':int(active[-1]) if len(active) else None}
rows=[]
for name in ['se_pc_on','se_door','mus_route1','mus_pewter','mus_vs_wild']:
    a_path=root/'results/phase6-music-final'/f'{name}.wav'
    b_path=root/'results/phase6-reference'/f'reference-{name}.wav'
    if not a_path.exists():
        rows.append({'name':name,'status':'no-successful-native-render'});continue
    a,b=load(a_path),load(b_path)
    # Both captures begin at the start-command frame. Retain hardware/mixer/filter delay.
    # Cry phase3's855-sample offset is not transferable to this different launch phase.
    lag=0
    n=min(len(a),len(b));a=a[:n];b=b[:n]
    envelopes=np.array([[np.sqrt(np.mean(a[i:i+480]**2)),np.sqrt(np.mean(b[i:i+480]**2))]
                        for i in range(0,n,480)])
    active=envelopes[:,1]>10
    rows.append({'name':name,'status':'compared','fixedLagSamples':lag,'comparedFrames':n,
                 'nativeSha256':hashlib.sha256(a_path.read_bytes()).hexdigest(),
                 'referenceSha256':hashlib.sha256(b_path.read_bytes()).hexdigest(),
                 'correlation':float(np.corrcoef(a.ravel(),b.ravel())[0,1]),
                 'rmsePcm16':float(np.sqrt(np.mean((a-b)**2))),
                 'activeEnvelopeRmsePcm16':float(np.sqrt(np.mean((envelopes[active,0]-envelopes[active,1])**2))),
                 'native':metric(a),'reference':metric(b),'exact':bool(np.array_equal(a,b))})
report={'scope':'Five representative source songs, SourceFiltered, same original configuration',
        'method':'Command-frame alignment without offset fitting; original mixer/filter onset delay retained; full stereo; no gain fit/stretch/phase search. Noise reset-phase/polarity differs by documented host convention, so waveform correlation is not a perceptual criterion. Power bands and envelope metrics remain diagnostic, not acceptance of all songs.', 'rows':rows}
(output/'comparison.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
