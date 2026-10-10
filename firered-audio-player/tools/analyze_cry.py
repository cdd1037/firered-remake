"""Fixed normal-cry comparison; optional development analysis, never runtime decoding."""
from pathlib import Path
import hashlib,json,wave
import numpy as np
from scipy.signal import correlate,welch
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'results/phase3'
def load(path):
 with wave.open(str(path)) as f:
  assert f.getframerate()==48000 and f.getnchannels()==2 and f.getsampwidth()==2
  return np.frombuffer(f.readframes(f.getnframes()),'<i2').reshape(-1,2).astype(float)
def levels(x):
 peak=float(np.abs(x).max());envelope=np.max(np.abs(x),axis=1)
 spans={}
 for db in [-40,-60,-80]:
  active=np.flatnonzero(envelope>max(1,peak*10**(db/20)))
  spans[str(db)]={'firstSeconds':float(active[0]/48000),'lastSeconds':float(active[-1]/48000)} if len(active) else None
 f,p=welch(x[:48000,0],48000,nperseg=8192)
 return {'rmsPcm16':float(np.sqrt(np.mean(x*x))),'peakPcm16':peak,'dcPcm16':x.mean(axis=0).tolist(),'thresholdSpansDbBelowOwnPeak':spans,'dominantWelchBinHz':float(f[np.argmax(p)]),'welchBinWidthHz':float(f[1]-f[0])}
output=OUT/'charmander.wav';native=load(output)
results=[]
for label,path in [('stripped-original-driver',ROOT.parent/'runtime-audio-study/cry-stripped.wav'),('earlier-full-game-export',ROOT.parent/'audio-reference-build/render/initial-output/cry_charmander_normal.raw.wav')]:
 reference=load(path)
 lag=int(np.argmax(correlate(native[:48000,0],reference[:48000,0],method='fft')))-47999
 a=native[lag:] if lag>=0 else native
 b=reference[-lag:] if lag<0 else reference
 n=min(len(a),len(b));a=a[:n];b=b[:n]
 pairs=[]
 for pos in range(0,n,480):
  q=a[pos:pos+480];r=b[pos:pos+480];pairs.append([np.sqrt(np.mean(q*q)),np.sqrt(np.mean(r*r))])
 e=np.array(pairs);active=e[:,1]>10
 results.append({'referenceLabel':label,'referencePath':str(path),'referenceSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'fixedLagSamples':lag,'comparedFrames':n,'exact':bool(np.array_equal(a,b)),'correlation':float(np.corrcoef(a.ravel(),b.ravel())[0,1]),'rmsePcm16':float(np.sqrt(np.mean((a-b)**2))),'active10msEnvelopeRmsePcm16':float(np.sqrt(np.mean((e[active,0]-e[active,1])**2))),'native':levels(a),'reference':levels(b)})
report={'scope':'species4 Charmander normal only; volume120 pan0 pitch15360 length140 release0 progress0 chorus0 priority10 stereo1','profile':'SourceFiltered; fresh global reverb0 inherited by cry header0','nativeFile':str(output),'nativeSha256':hashlib.sha256(output.read_bytes()).hexdigest(),'decodedPlayableSamples':7133,'decodedGuardSamples':1,'sourcePlaybackHz':10512,'predictedUnfilteredSampleDurationSeconds':7133/10512,'templateWaitTicks':140,'templateWaitSeconds':140*280896/16777216,'method':'One delay estimated from first second per distinct named reference and then fixed for full stereo comparison. No gain fit, stretch, per-note alignment or parameter search. Dominant spectral bin is not a certified musical fundamental. Filtered threshold tails are not sample duration.','results':results}
(OUT/'comparison.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
