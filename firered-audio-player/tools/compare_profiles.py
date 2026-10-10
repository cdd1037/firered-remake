"""Fixed-alignment ablation metrics, never a per-profile phase/gain fitting loop."""
from pathlib import Path
import wave,json,hashlib
import numpy as np
root=Path(__file__).resolve().parents[1]
def read(p):
 with wave.open(str(p)) as f:return np.frombuffer(f.readframes(f.getnframes()),'<i2').reshape(-1,2).astype(float)
out=[]
for song,ref,lag in [('select','se-stripped.wav',-43),('pallet','bgm-stripped.wav',-852)]:
 b=read(root.parent/'runtime-audio-study'/ref)[-lag:]
 for p in sorted((root/'results/phase2').glob(song+'-*.wav')):
  a=read(p);n=min(len(a),len(b));aa=a[:n];bb=b[:n]
  envelope=[]
  for start in range(0,n,480):
   q=aa[start:start+480];r=bb[start:start+480]
   envelope.append([np.sqrt(np.mean(q*q)),np.sqrt(np.mean(r*r))])
  e=np.array(envelope);active=e[:,1]>50
  out.append(dict(song=song,profile=p.stem[len(song)+1:],fixedLag=lag,frames=n,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),rms=float(np.sqrt(np.mean(aa*aa))),referenceRms=float(np.sqrt(np.mean(bb*bb))),rmse=float(np.sqrt(np.mean((aa-bb)**2))),correlation=float(np.corrcoef(aa.ravel(),bb.ravel())[0,1]),envelope10msRmse=float(np.sqrt(np.mean((e[active,0]-e[active,1])**2))),envelope10msCorrelation=float(np.corrcoef(e[active].T)[0,1]),peak=int(np.abs(aa).max())))
(root/'results/phase2/profile-comparison.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
