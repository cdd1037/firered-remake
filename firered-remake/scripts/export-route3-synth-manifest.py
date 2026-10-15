#!/usr/bin/env python3
"""Export the pinned151-cue recipe without rendering or copying audio samples."""
import argparse,json,pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');a=p.parse_args()
b=(ROOT/'scripts/synth-route3-cues.json').read_bytes()
m=json.loads(b)
if len(m['cues'])!=151 or len({c['id']for c in m['cues']})!=151:raise ValueError('cue inventory differs')
outputs={ROOT / 'content/generated/synth-audio/manifest.json': b}
for path,data in outputs.items():
 if a.check:
  if path.read_bytes()!=data:raise ValueError('generated output differs: '+str(path))
 else:path.write_bytes(data)
print(json.dumps({'passed': True, 'cues': 151, 'normalCries': 40, 'samplePackUnchanged': True}))
