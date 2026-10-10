"""Analysis only: existing numpy/scipy; never a runtime dependency."""
import pathlib,wave,json
import numpy as np
from scipy.signal import correlate
root=pathlib.Path(__file__).resolve().parents[1]
refs=root.parent/'runtime-audio-study'
def read(p):
 with wave.open(str(p)) as f:
  assert f.getframerate()==48000 and f.getnchannels()==2
  return np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').reshape(-1,2).astype(float)
results=[]
for ours,reference in [('select.wav','se-stripped.wav'),('pallet.wav','bgm-stripped.wav')]:
 a=read(root/'results'/ours);b=read(refs/reference)
 # One delay estimate from 1 sec only, no changing alignment/phase warping.
 lag=int(np.argmax(correlate(a[:48000,0],b[:48000,0],method='fft')))-47999
 if lag>=0:a=a[lag:]
 else:b=b[-lag:]
 n=min(len(a),len(b));a=a[:n];b=b[:n];
 results.append(dict(output=ours,reference=str(refs/reference),lagFrames=lag,frames=n,exact=bool(np.array_equal(a,b)),rmsePcm16=float(np.sqrt(np.mean((a-b)**2))),referenceRms=float(np.sqrt(np.mean(b*b))),outputRms=float(np.sqrt(np.mean(a*a))),correlation=float(np.corrcoef(a.ravel(),b.ravel())[0,1]),claim='not fidelity accepted; modern-dry experimental vs original-driver mGBA reference'))
(root/'results/comparison.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
