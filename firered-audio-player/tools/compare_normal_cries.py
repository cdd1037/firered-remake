"""Six fixed original-driver comparisons. Development-only NumPy/SciPy, never runtime."""
from pathlib import Path
import hashlib, json, wave
import numpy as np
from scipy.signal import correlate, welch
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'results/phase4'
def load(path):
    with wave.open(str(path)) as f:
        assert f.getframerate() == 48000 and f.getnchannels() == 2 and f.getsampwidth() == 2
        return np.frombuffer(f.readframes(f.getnframes()), '<i2').reshape(-1, 2).astype(float)
def levels(x):
    peak = np.abs(x).max()
    active = np.flatnonzero(np.max(np.abs(x), axis=1) > max(1, peak * .001))
    f, p = welch(x[:, 0], 48000, nperseg=8192)
    return {'rms': float(np.sqrt(np.mean(x*x))), 'peak': float(peak),
            'lastFrameAboveMinus60Db': int(active[-1]) if len(active) else None,
            'dominantWelchBinHz': float(f[np.argmax(p)])}
rows = []
for name in ['bulbasaur', 'pikachu', 'mewtwo', 'treecko', 'wailord', 'deoxys']:
    a_path, b_path = OUT/f'native-{name}.wav', OUT/f'reference-{name}.wav'
    a, b = load(a_path), load(b_path)
    lag = int(np.argmax(correlate(a[:48000,0], b[:48000,0], method='fft'))) - 47999
    a = a[max(0,lag):]; b = b[max(0,-lag):]
    count = min(len(a),len(b)); a=a[:count]; b=b[:count]
    envelopes = np.array([[np.sqrt(np.mean(a[i:i+480]**2)), np.sqrt(np.mean(b[i:i+480]**2))]
                          for i in range(0,count,480)])
    active = envelopes[:,1] > 10
    rows.append({'name':name, 'lagSamples':lag, 'comparedFrames':count,
                 'nativeSha256':hashlib.sha256(a_path.read_bytes()).hexdigest(),
                 'referenceSha256':hashlib.sha256(b_path.read_bytes()).hexdigest(),
                 'correlation':float(np.corrcoef(a.ravel(),b.ravel())[0,1]),
                 'rmsePcm16':float(np.sqrt(np.mean((a-b)**2))),
                 'active10msEnvelopeRmse':float(np.sqrt(np.mean((envelopes[active,0]-envelopes[active,1])**2))),
                 'native':levels(a), 'reference':levels(b), 'exact':bool(np.array_equal(a,b))})
report = {'profile':'SourceFiltered, fresh reverb0', 'referenceConfiguration':'same fixed driver, normal params120/0/15360/140/0/0/0/10',
          'method':'One lag estimated from first second, fixed thereafter; full stereo, no gain fit/time stretch. Welch resolution5.859375Hz, dominant bin is not certified fundamental. Six samples do not prove all386 faithful.', 'rows':rows}
(OUT/'comparison.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
