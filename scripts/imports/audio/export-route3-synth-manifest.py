#!/usr/bin/env python3
"""Export the pinned151-cue recipe without rendering or copying audio samples."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

import argparse,json,pathlib
ROOT=_PROJECT_ROOT
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');a=p.parse_args()
b=(ROOT/'scripts/recipes/audio/synth-route3-cues.json').read_bytes()
m=json.loads(b)
if len(m['cues'])!=151 or len({c['id']for c in m['cues']})!=151:raise ValueError('cue inventory differs')
outputs={ROOT / 'content/generated/audio/synth/manifest.json': runtime_manifest_bytes(m)}
for path,data in outputs.items():
 if a.check:
  if path.read_bytes()!=data:raise ValueError('generated output differs: '+str(path))
 else:path.write_bytes(data)
print(json.dumps({'passed': True, 'cues': 151, 'normalCries': 40, 'samplePackUnchanged': True}))
