#!/usr/bin/env python3
"""Rebuild the two ID-only Snorlax synth cues; no rendered audio or tool download."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

from pathlib import Path
import argparse,json
ROOT=_PROJECT_ROOT
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');a=p.parse_args()
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
for route,pin in [(12,'ea70631fa17e3042cf08b32c94fbc26fbb128936b61c00e61127b79af780368e'),(16,'680c86b2457428a348f8dd7ff006d13681c510cd5098c04b44f8f09a755a17a5')]:
 source=(ROOT/f'content/raw/pokefirered/037335f4/data/maps/Route{route}/scripts.inc').read_bytes()
 assert b'playmoncry SPECIES_SNORLAX, CRY_MODE_ENCOUNTER' in source
cues=[]
for name,mode in [('NORMAL',0),('ENCOUNTER',2)]:
 cues.append(dict(id='CRY_SNORLAX_' + name, sourceSymbol='SPECIES_SNORLAX', sourcePlayer=4, priority=10, sourceCounter=None, songAddress=0, species=143, looping=False, cryMode=mode))
b=runtime_manifest_bytes(dict(schemaVersion=1, cues=cues))
out=ROOT/'content/generated/audio/cues/snorlax.json'
if a.check:assert out.read_bytes()==b
else:out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(b)
print('PASS Snorlax two synth cue IDs, source maps, exact manifest bytes; no audio produced')
